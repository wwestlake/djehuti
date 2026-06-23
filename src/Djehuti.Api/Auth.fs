module Djehuti.Api.Auth

open System
open System.Security.Cryptography
open System.Text
open System.IdentityModel.Tokens.Jwt
open Microsoft.IdentityModel.Tokens
open System.Security.Claims

// ── JWT ──────────────────────────────────────────────────────────────────────

type JwtClaims =
    { UserId: string
      Email: string
      DisplayName: string option
      Role: string
      IssuedAt: DateTime
      ExpiresAt: DateTime }

let private getSigningKey () =
    let keyStr = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY")
    if String.IsNullOrWhiteSpace(keyStr) then
        failwith "JWT_SIGNING_KEY environment variable is not set"
    let keyBytes = Convert.FromBase64String(keyStr)
    new SymmetricSecurityKey(keyBytes)

let generateToken (claims: JwtClaims) : string =
    let key = getSigningKey ()
    let credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)

    let claimsIdentity = new ClaimsIdentity()
    claimsIdentity.AddClaim(new Claim("sub", claims.UserId))
    claimsIdentity.AddClaim(new Claim("email", claims.Email))
    if claims.DisplayName.IsSome then
        claimsIdentity.AddClaim(new Claim("display_name", claims.DisplayName.Value))
    claimsIdentity.AddClaim(new Claim("role", claims.Role))

    let tokenDescriptor = SecurityTokenDescriptor(
        Subject = claimsIdentity,
        Expires = claims.ExpiresAt,
        IssuedAt = claims.IssuedAt,
        NotBefore = claims.IssuedAt,
        Issuer = "djehuti",
        Audience = "djehuti-client",
        SigningCredentials = credentials
    )

    let handler = new JwtSecurityTokenHandler()
    let token = handler.CreateToken(tokenDescriptor)
    handler.WriteToken(token)

let verifyToken (token: string) : JwtClaims option =
    try
        let key = getSigningKey ()
        let handler = new JwtSecurityTokenHandler()
        let validationParameters = TokenValidationParameters(
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = "djehuti",
            ValidateAudience = true,
            ValidAudience = "djehuti-client",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        )

        let mutable validatedToken: SecurityToken = null
        let principal: ClaimsPrincipal = handler.ValidateToken(token, validationParameters, &validatedToken)

        let getClaim (name: string) : string option =
            let claim = principal.FindFirst(name)
            if claim <> null then Some claim.Value else None

        match getClaim "sub", getClaim "email", getClaim "role" with
        | Some userId, Some email, Some role ->
            Some {
                UserId = userId
                Email = email
                DisplayName = getClaim "display_name"
                Role = role
                IssuedAt = DateTime.UtcNow
                ExpiresAt = DateTime.UtcNow.AddHours(1.0)
            }
        | _ -> None
    with _ ->
        None

// ── Password Hashing ─────────────────────────────────────────────────────────

let hashPassword (password: string) : string =
    use hasher = new Rfc2898DeriveBytes(password, 32, 10000, HashAlgorithmName.SHA256)
    let salt = hasher.Salt
    let hash = hasher.GetBytes(32)
    let combined = Array.concat [salt; hash]
    Convert.ToBase64String(combined)

let verifyPassword (password: string) (hash: string) : bool =
    try
        let combined = Convert.FromBase64String(hash)
        let salt = combined.[0..31]
        let storedHash = combined.[32..63]
        use hasher = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256)
        let computedHash = hasher.GetBytes(32)
        storedHash = computedHash
    with _ ->
        false

// ── Token Generation (email verification, password reset) ───────────────────

let generateSecureToken () : string =
    let bytes = RandomNumberGenerator.GetBytes(32)
    Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "")

// ── hCaptcha Verification ────────────────────────────────────────────────────

let verifyHCaptcha (token: string) : Async<bool> =
    async {
        try
            let secret = Environment.GetEnvironmentVariable("HCAPTCHA_SECRET_KEY")
            if String.IsNullOrWhiteSpace(secret) then
                return false
            else
                use client = new System.Net.Http.HttpClient()
                let content = new System.Net.Http.FormUrlEncodedContent([
                    new System.Collections.Generic.KeyValuePair<string, string>("secret", secret)
                    new System.Collections.Generic.KeyValuePair<string, string>("response", token)
                ])

                let! response = client.PostAsync("https://hcaptcha.com/siteverify", content) |> Async.AwaitTask
                if response.IsSuccessStatusCode then
                    let! body = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    let json = System.Text.Json.JsonDocument.Parse(body)
                    match json.RootElement.TryGetProperty("success") with
                    | true, prop -> return prop.GetBoolean()
                    | _ -> return false
                else
                    return false
        with _ ->
            return false
    }
