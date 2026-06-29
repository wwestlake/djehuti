module Djehuti.Api.NginxLogParser

open System
open System.IO
open System.IO.Compression
open System.Net.Http
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open Npgsql

// Matches: IP - - [date] "METHOD /path HTTP/x" status bytes "referrer" "ua"
let private logPattern =
    Regex(@"^(\S+) \S+ \S+ \[([^\]]+)\] ""(\w+) ([^ ]+) [^""]*"" (\d+) \d+ ""([^""]*)"" ""([^""]*)""",
          RegexOptions.Compiled)

// Paths we care about — lagdaemon.com pages and djehuti dashboard
let private isPageRequest (method: string) (path: string) (status: string) =
    method = "GET"
    && status = "200"
    && not (path.StartsWith("/api/"))
    && not (path.StartsWith("/djehuti/api/"))
    && not (String.IsNullOrEmpty path)
    && not (path.Contains(".php"))
    && not (path.Contains("wp-"))
    && not (path.Contains(".env"))
    && not (path.Contains(".."))

// Known bots/scanners to skip
let private isBotUA (ua: string) =
    let lower = ua.ToLowerInvariant()
    lower.Contains("bot") || lower.Contains("crawler") || lower.Contains("spider")
    || lower.Contains("scan") || lower.Contains("python-requests")
    || lower.Contains("curl/") || lower.Contains("wget/")
    || lower.Contains("zgrab") || lower.Contains("masscan")

let private parseDate (s: string) =
    // nginx outputs +0000 but .NET zzz expects +00:00 — insert colon before last 2 digits
    let normalized =
        if s.Length >= 5 then
            let sign = s.[s.Length - 5]
            if sign = '+' || sign = '-' then s.[..s.Length - 3] + ":" + s.[s.Length - 2..]
            else s
        else s
    match DateTime.TryParseExact(normalized, "dd/MMM/yyyy:HH:mm:ss zzz",
                                  System.Globalization.CultureInfo.InvariantCulture,
                                  System.Globalization.DateTimeStyles.None) with
    | true, dt -> Some (dt.ToUniversalTime())
    | _ -> None

type LogEntry = {
    Ip       : string
    Path     : string
    Referrer : string
    UserAgent: string
    ViewedAt : DateTime
}

let parseLine (line: string) : LogEntry option =
    let m = logPattern.Match(line)
    if not m.Success then None
    else
        let ip     = m.Groups.[1].Value
        let date   = m.Groups.[2].Value
        let method = m.Groups.[3].Value
        let path   = m.Groups.[4].Value
        let status = m.Groups.[5].Value
        let ref_   = m.Groups.[6].Value
        let ua     = m.Groups.[7].Value
        if not (isPageRequest method path status) || isBotUA ua then None
        else
            match parseDate date with
            | None -> None
            | Some dt ->
                Some { Ip = ip; Path = path; Referrer = (if ref_ = "-" then "" else ref_)
                       UserAgent = ua; ViewedAt = dt }

let readLogFile (path: string) : LogEntry list =
    try
        let lines =
            if path.EndsWith(".gz") then
                use fs = File.OpenRead(path)
                use gz = new GZipStream(fs, CompressionMode.Decompress)
                use sr = new StreamReader(gz)
                sr.ReadToEnd().Split('\n')
            else
                File.ReadAllLines(path)
        lines |> Array.choose parseLine |> Array.toList
    with _ -> []

// Geo-enrich IPs via ipwho.is (free, HTTPS, no batch limit)
type GeoInfo = { Country: string; Region: string; City: string; Domain: string }

let private enrichOneIp (http: HttpClient) (ip: string) : (string * GeoInfo) option =
    try
        let resp = http.GetAsync($"https://ipwho.is/{ip}").Result
        let json = resp.Content.ReadAsStringAsync().Result
        let doc  = JsonDocument.Parse(json)
        let root = doc.RootElement
        let mutable successEl = Unchecked.defaultof<JsonElement>
        if root.TryGetProperty("success", &successEl) && successEl.GetBoolean() then
            let get (k: string) =
                let mutable v = Unchecked.defaultof<JsonElement>
                if root.TryGetProperty(k, &v) then v.GetString() else ""
            let getIsp () =
                let mutable conn = Unchecked.defaultof<JsonElement>
                if root.TryGetProperty("connection", &conn) then
                    let mutable isp = Unchecked.defaultof<JsonElement>
                    if conn.TryGetProperty("isp", &isp) then isp.GetString() else ""
                else ""
            let geo = {
                Country = get "country"
                Region  = get "region"
                City    = get "city"
                Domain  = getIsp ()
            }
            Some (ip, geo)
        else None
    with ex ->
        eprintfn "[enrichIps] %s: %s" ip ex.Message
        None

let enrichIps (ips: string list) : Map<string, GeoInfo> =
    if ips.IsEmpty then Map.empty
    else
        try
            use http = new HttpClient()
            http.Timeout <- TimeSpan.FromSeconds(10.0)
            let distinct = ips |> List.distinct
            let results =
                distinct
                |> List.choose (enrichOneIp http)
            let map = results |> Map.ofList
            eprintfn "[enrichIps] Done: %d/%d IPs enriched" map.Count distinct.Length
            map
        with ex ->
            eprintfn "[enrichIps] EXCEPTION: %s" ex.Message
            Map.empty

let insertEntries (entries: LogEntry list) (geoMap: Map<string, GeoInfo>) =
    eprintfn "[NginxLogParser] insertEntries called with %d entries" entries.Length
    use conn = Database.openConnection()
    let mutable inserted = 0
    let mutable firstError = true
    for e in entries do
        try
            let geo = geoMap |> Map.tryFind e.Ip |> Option.defaultValue { Country = ""; Region = ""; City = ""; Domain = "" }
            let ipBytes  = Text.Encoding.UTF8.GetBytes(e.Ip)
            let ipHash   = Convert.ToHexString(Security.Cryptography.SHA256.HashData(ipBytes)).ToLowerInvariant()
            use cmd = new NpgsqlCommand("""
                INSERT INTO anonymous_page_views
                    (ip_hash, ip_address, path, referrer, user_agent, country, region, city, domain, viewed_at, source)
                VALUES (@hash, @ip, @path, @ref, @ua, @country, @region, @city, @domain, @at, 'nginx_log')
                ON CONFLICT (ip_address, path, viewed_at) DO NOTHING
            """, conn)
            cmd.Parameters.AddWithValue("hash",    ipHash)       |> ignore
            cmd.Parameters.AddWithValue("ip",      e.Ip)         |> ignore
            cmd.Parameters.AddWithValue("path",    e.Path)       |> ignore
            cmd.Parameters.AddWithValue("ref",     e.Referrer)   |> ignore
            cmd.Parameters.AddWithValue("ua",      e.UserAgent)  |> ignore
            cmd.Parameters.AddWithValue("country", geo.Country)  |> ignore
            cmd.Parameters.AddWithValue("region",  geo.Region)   |> ignore
            cmd.Parameters.AddWithValue("city",    geo.City)     |> ignore
            cmd.Parameters.AddWithValue("domain",  geo.Domain)   |> ignore
            cmd.Parameters.AddWithValue("at",      e.ViewedAt)   |> ignore
            inserted <- inserted + cmd.ExecuteNonQuery()
        with ex ->
            if firstError then
                eprintfn "[NginxLogParser] INSERT EXCEPTION (first of %d): %s" entries.Length ex.Message
                firstError <- false
    inserted

// Enrich existing rows that have ip_address but no geo data (beacon-tracked visits)
let enrichUnenrichedRecords () =
    try
        use conn = Database.openConnection()
        use cmd = new NpgsqlCommand("""
            SELECT DISTINCT ip_address FROM anonymous_page_views
            WHERE ip_address IS NOT NULL AND ip_address != '' AND (country IS NULL OR country = '')
        """, conn)
        use r = cmd.ExecuteReader()
        let ips = [ while r.Read() do yield r.GetString(0) ]
        r.Close()
        eprintfn "[enrichUnenrichedRecords] Found %d unenriched IPs" ips.Length
        if ips.IsEmpty then 0
        else
            let geoMap = enrichIps ips
            eprintfn "[enrichUnenrichedRecords] Geo map size: %d" geoMap.Count
            let mutable updated = 0
            for ip in ips do
                match geoMap |> Map.tryFind ip with
                | Some geo ->
                    use upd = new NpgsqlCommand("""
                        UPDATE anonymous_page_views
                        SET country = @country, region = @region, city = @city, domain = @domain
                        WHERE ip_address = @ip
                    """, conn)
                    upd.Parameters.AddWithValue("country", geo.Country) |> ignore
                    upd.Parameters.AddWithValue("region",  geo.Region)  |> ignore
                    upd.Parameters.AddWithValue("city",    geo.City)    |> ignore
                    upd.Parameters.AddWithValue("domain",  geo.Domain)  |> ignore
                    upd.Parameters.AddWithValue("ip",      ip)          |> ignore
                    updated <- updated + upd.ExecuteNonQuery()
                | None -> ()
            eprintfn "[enrichUnenrichedRecords] Updated %d rows" updated
            updated
    with ex ->
        eprintfn "[enrichUnenrichedRecords] EXCEPTION: %s" ex.Message
        0

let nginxLogDir = "/var/log/nginx"
let logFiles () =
    if Directory.Exists(nginxLogDir) then
        Directory.GetFiles(nginxLogDir, "access.log*")
        |> Array.filter (fun f -> not (f.EndsWith(".gz")) || true)
        |> Array.toList
    else []

let runRefresh () =
    try
        let files = logFiles ()
        eprintfn "[NginxLogParser] Found %d log files: %A" files.Length files
        let cutoff = DateTime.UtcNow.AddDays(-30.0)
        let entries =
            files
            |> List.collect readLogFile
            |> List.filter (fun e -> e.ViewedAt >= cutoff)
        eprintfn "[NginxLogParser] Parsed %d entries after filter (cutoff=%A)" entries.Length cutoff
        let ips = entries |> List.map (fun e -> e.Ip) |> List.distinct
        eprintfn "[NginxLogParser] Unique IPs: %d" ips.Length
        let geoMap = enrichIps ips
        eprintfn "[NginxLogParser] Geo map size: %d" geoMap.Count
        let inserted = insertEntries entries geoMap
        eprintfn "[NginxLogParser] Inserted: %d" inserted
        let enriched = enrichUnenrichedRecords ()
        eprintfn "[NginxLogParser] Beacon records enriched: %d" enriched
        {| Parsed = entries.Length; UniqueIps = ips.Length; Inserted = inserted; BeaconEnriched = enriched |}
    with ex ->
        eprintfn "[NginxLogParser] runRefresh EXCEPTION: %s\n%s" ex.Message ex.StackTrace
        {| Parsed = 0; UniqueIps = 0; Inserted = 0; BeaconEnriched = 0 |}
