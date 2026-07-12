module Djehuti.Api.DesktopAuthRepository

open System
open System.Security.Cryptography
open System.Text
open Npgsql

// Loopback-only redirect: the desktop app runs a temporary local HTTP
// listener and gives us its own port, we send the browser back to exactly
// that address. Rejecting anything else means the authorization code can
// only ever be delivered to a process running on the same machine as the
// user's own browser session, which is the actual security property this
// flow depends on (see RFC 8252, "OAuth 2.0 for Native Apps").
let isLoopbackRedirect (uri: string) : bool =
    match Uri.TryCreate(uri, UriKind.Absolute) with
    | true, parsed ->
        parsed.Scheme = "http" && (parsed.Host = "127.0.0.1" || parsed.Host = "localhost")
    | false, _ -> false

// RFC 7636 S256: code_challenge = BASE64URL(SHA256(code_verifier)).
let private base64UrlEncode (bytes: byte array) : string =
    Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')

let private computeChallenge (verifier: string) : string =
    SHA256.HashData(Encoding.ASCII.GetBytes(verifier)) |> base64UrlEncode

let private generateCode () : string =
    let bytes = RandomNumberGenerator.GetBytes(32)
    base64UrlEncode bytes

// Short-lived (2 minutes) and single-use by construction -- consumeCode's
// UPDATE ... WHERE used = FALSE only ever succeeds once for a given code,
// so a code can't be replayed even if it leaks (e.g. via shell history or a
// proxy log) after the legitimate exchange already happened.
let createCode (userId: Guid) (redirectUri: string) (codeChallenge: string) : string =
    let code = generateCode ()
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("""
        INSERT INTO desktop_auth_codes (code, user_id, redirect_uri, code_challenge, expires_at)
        VALUES (@code, @userId, @redirectUri, @challenge, NOW() + INTERVAL '2 minutes')
    """, conn)
    cmd.Parameters.AddWithValue("code", code) |> ignore
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    cmd.Parameters.AddWithValue("redirectUri", redirectUri) |> ignore
    cmd.Parameters.AddWithValue("challenge", codeChallenge) |> ignore
    cmd.ExecuteNonQuery() |> ignore
    code

// Verifies PKCE (the caller must present the original code_verifier, whose
// SHA256 must match the code_challenge given at authorize time) AND atomically
// marks the code used in the same UPDATE, so two concurrent exchange attempts
// for the same code can't both succeed (classic use-after-check race,
// avoided by making "is this code still unused" and "mark it used" one
// statement instead of two).
let consumeCode (code: string) (codeVerifier: string) : Guid option =
    if String.IsNullOrWhiteSpace code || String.IsNullOrWhiteSpace codeVerifier then
        None
    else
        use conn = Database.openConnection()
        use cmd = new NpgsqlCommand("""
            UPDATE desktop_auth_codes
            SET used = TRUE
            WHERE code = @code AND used = FALSE AND expires_at > NOW()
            RETURNING user_id, code_challenge
        """, conn)
        cmd.Parameters.AddWithValue("code", code) |> ignore
        use reader = cmd.ExecuteReader()
        if reader.Read() then
            let userId = reader.GetGuid(0)
            let storedChallenge = reader.GetString(1)
            if computeChallenge codeVerifier = storedChallenge then Some userId else None
        else
            None
