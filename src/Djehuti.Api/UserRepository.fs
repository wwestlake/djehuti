module Djehuti.Api.UserRepository

open System
open Npgsql

type User =
    { Id: Guid
      Email: string
      EmailVerifiedAt: DateTime option
      PasswordHash: string option
      DisplayName: string option
      AvatarUrl: string option
      Bio: string option
      Pronouns: string option
      Location: string option
      NotifyByEmail: bool
      Role: string
      Status: string
      CreatedAt: DateTime
      UpdatedAt: DateTime }

type UserPublicProfile =
    { Id: Guid
      DisplayName: string option
      AvatarUrl: string option
      Bio: string option
      Pronouns: string option
      Location: string option }

let private openConn () = Database.openConnection ()

// ── Create ───────────────────────────────────────────────────────────────────

let createUser (email: string) (passwordHash: string option) : Async<User option> =
    async {
        try
            use conn = openConn ()
            use cmd = new NpgsqlCommand(
                """INSERT INTO users (email, password_hash, status)
                   VALUES (@email, @password_hash, 'pending')
                   RETURNING id, email, email_verified_at, password_hash, display_name, avatar_url, bio, pronouns, location,
                             notify_by_email, role, status, created_at, updated_at""", conn)
            cmd.Parameters.AddWithValue("email", email) |> ignore
            cmd.Parameters.AddWithValue("password_hash", if passwordHash.IsSome then box passwordHash.Value else box DBNull.Value) |> ignore

            use reader = cmd.ExecuteReader()
            if reader.Read() then
                let user = {
                    Id = reader.GetGuid(0)
                    Email = reader.GetString(1)
                    EmailVerifiedAt = if reader.IsDBNull(2) then None else Some (reader.GetFieldValue<DateTime>(2))
                    PasswordHash = if reader.IsDBNull(3) then None else Some (reader.GetString(3))
                    DisplayName = if reader.IsDBNull(4) then None else Some (reader.GetString(4))
                    AvatarUrl = if reader.IsDBNull(5) then None else Some (reader.GetString(5))
                    Bio = if reader.IsDBNull(6) then None else Some (reader.GetString(6))
                    Pronouns = if reader.IsDBNull(7) then None else Some (reader.GetString(7))
                    Location = if reader.IsDBNull(8) then None else Some (reader.GetString(8))
                    NotifyByEmail = reader.GetBoolean(9)
                    Role = reader.GetString(10)
                    Status = reader.GetString(11)
                    CreatedAt = reader.GetFieldValue<DateTime>(12)
                    UpdatedAt = reader.GetFieldValue<DateTime>(13)
                }
                return Some user
            else
                return None
        with ex ->
            printfn "[UserRepository] Create failed: %s" ex.Message
            return None
    }

// ── Read ─────────────────────────────────────────────────────────────────────

let tryGetByEmail (email: string) : Async<User option> =
    async {
        try
            use conn = openConn ()
            use cmd = new NpgsqlCommand(
                """SELECT id, email, email_verified_at, password_hash, display_name, avatar_url, bio, pronouns, location,
                         notify_by_email, role, status, created_at, updated_at
                   FROM users WHERE email = @email""", conn)
            cmd.Parameters.AddWithValue("email", email) |> ignore

            use reader = cmd.ExecuteReader()
            if reader.Read() then
                let user = {
                    Id = reader.GetGuid(0)
                    Email = reader.GetString(1)
                    EmailVerifiedAt = if reader.IsDBNull(2) then None else Some (reader.GetFieldValue<DateTime>(2))
                    PasswordHash = if reader.IsDBNull(3) then None else Some (reader.GetString(3))
                    DisplayName = if reader.IsDBNull(4) then None else Some (reader.GetString(4))
                    AvatarUrl = if reader.IsDBNull(5) then None else Some (reader.GetString(5))
                    Bio = if reader.IsDBNull(6) then None else Some (reader.GetString(6))
                    Pronouns = if reader.IsDBNull(7) then None else Some (reader.GetString(7))
                    Location = if reader.IsDBNull(8) then None else Some (reader.GetString(8))
                    NotifyByEmail = reader.GetBoolean(9)
                    Role = reader.GetString(10)
                    Status = reader.GetString(11)
                    CreatedAt = reader.GetFieldValue<DateTime>(12)
                    UpdatedAt = reader.GetFieldValue<DateTime>(13)
                }
                return Some user
            else
                return None
        with _ ->
            return None
    }

let tryGetById (id: Guid) : Async<User option> =
    async {
        try
            use conn = openConn ()
            use cmd = new NpgsqlCommand(
                """SELECT id, email, email_verified_at, password_hash, display_name, avatar_url, bio, pronouns, location,
                         notify_by_email, role, status, created_at, updated_at
                   FROM users WHERE id = @id""", conn)
            cmd.Parameters.AddWithValue("id", id) |> ignore

            use reader = cmd.ExecuteReader()
            if reader.Read() then
                let user = {
                    Id = reader.GetGuid(0)
                    Email = reader.GetString(1)
                    EmailVerifiedAt = if reader.IsDBNull(2) then None else Some (reader.GetFieldValue<DateTime>(2))
                    PasswordHash = if reader.IsDBNull(3) then None else Some (reader.GetString(3))
                    DisplayName = if reader.IsDBNull(4) then None else Some (reader.GetString(4))
                    AvatarUrl = if reader.IsDBNull(5) then None else Some (reader.GetString(5))
                    Bio = if reader.IsDBNull(6) then None else Some (reader.GetString(6))
                    Pronouns = if reader.IsDBNull(7) then None else Some (reader.GetString(7))
                    Location = if reader.IsDBNull(8) then None else Some (reader.GetString(8))
                    NotifyByEmail = reader.GetBoolean(9)
                    Role = reader.GetString(10)
                    Status = reader.GetString(11)
                    CreatedAt = reader.GetFieldValue<DateTime>(12)
                    UpdatedAt = reader.GetFieldValue<DateTime>(13)
                }
                return Some user
            else
                return None
        with _ ->
            return None
    }

let getPublicProfile (id: Guid) : Async<UserPublicProfile option> =
    async {
        let! user = tryGetById id
        return user |> Option.map (fun u -> {
            Id = u.Id
            DisplayName = u.DisplayName
            AvatarUrl = u.AvatarUrl
            Bio = u.Bio
            Pronouns = u.Pronouns
            Location = u.Location
        })
    }

// ── Update ───────────────────────────────────────────────────────────────────

let verifyEmail (id: Guid) : Async<bool> =
    async {
        try
            use conn = openConn ()
            use cmd = new NpgsqlCommand(
                """UPDATE users SET email_verified_at = now(), updated_at = now() WHERE id = @id""", conn)
            cmd.Parameters.AddWithValue("id", id) |> ignore
            let rows = cmd.ExecuteNonQuery()
            return rows > 0
        with _ ->
            return false
    }

let updatePassword (id: Guid) (passwordHash: string) : Async<bool> =
    async {
        try
            use conn = openConn ()
            use cmd = new NpgsqlCommand(
                """UPDATE users SET password_hash = @password_hash, updated_at = now() WHERE id = @id""", conn)
            cmd.Parameters.AddWithValue("id", id) |> ignore
            cmd.Parameters.AddWithValue("password_hash", passwordHash) |> ignore
            let rows = cmd.ExecuteNonQuery()
            return rows > 0
        with _ ->
            return false
    }

let updateProfile (id: Guid) (displayName: string option) (bio: string option) (pronouns: string option) (location: string option) (notifyByEmail: bool) : Async<User option> =
    async {
        try
            use conn = openConn ()
            use cmd = new NpgsqlCommand(
                """UPDATE users
                   SET display_name = @display_name, bio = @bio, pronouns = @pronouns, location = @location,
                       notify_by_email = @notify_by_email, updated_at = now()
                   WHERE id = @id
                   RETURNING id, email, email_verified_at, password_hash, display_name, avatar_url, bio, pronouns, location,
                             notify_by_email, role, status, created_at, updated_at""", conn)
            cmd.Parameters.AddWithValue("id", id) |> ignore
            cmd.Parameters.AddWithValue("display_name", if displayName.IsSome then box displayName.Value else box DBNull.Value) |> ignore
            cmd.Parameters.AddWithValue("bio", if bio.IsSome then box bio.Value else box DBNull.Value) |> ignore
            cmd.Parameters.AddWithValue("pronouns", if pronouns.IsSome then box pronouns.Value else box DBNull.Value) |> ignore
            cmd.Parameters.AddWithValue("location", if location.IsSome then box location.Value else box DBNull.Value) |> ignore
            cmd.Parameters.AddWithValue("notify_by_email", notifyByEmail) |> ignore

            use reader = cmd.ExecuteReader()
            if reader.Read() then
                let user = {
                    Id = reader.GetGuid(0)
                    Email = reader.GetString(1)
                    EmailVerifiedAt = if reader.IsDBNull(2) then None else Some (reader.GetFieldValue<DateTime>(2))
                    PasswordHash = if reader.IsDBNull(3) then None else Some (reader.GetString(3))
                    DisplayName = if reader.IsDBNull(4) then None else Some (reader.GetString(4))
                    AvatarUrl = if reader.IsDBNull(5) then None else Some (reader.GetString(5))
                    Bio = if reader.IsDBNull(6) then None else Some (reader.GetString(6))
                    Pronouns = if reader.IsDBNull(7) then None else Some (reader.GetString(7))
                    Location = if reader.IsDBNull(8) then None else Some (reader.GetString(8))
                    NotifyByEmail = reader.GetBoolean(9)
                    Role = reader.GetString(10)
                    Status = reader.GetString(11)
                    CreatedAt = reader.GetFieldValue<DateTime>(12)
                    UpdatedAt = reader.GetFieldValue<DateTime>(13)
                }
                return Some user
            else
                return None
        with ex ->
            printfn "[UserRepository] UpdateProfile failed: %s" ex.Message
            return None
    }

// ── Email Verification Tokens ───────────────────────────────────────────────

let createEmailVerificationToken (userId: Guid) (token: string) : Async<bool> =
    async {
        try
            use conn = openConn ()
            use cmd = new NpgsqlCommand(
                """INSERT INTO email_verification_tokens (user_id, token, expires_at)
                   VALUES (@user_id, @token, now() + interval '24 hours')""", conn)
            cmd.Parameters.AddWithValue("user_id", userId) |> ignore
            cmd.Parameters.AddWithValue("token", token) |> ignore
            let rows = cmd.ExecuteNonQuery()
            return rows > 0
        with _ ->
            return false
    }

let verifyEmailToken (token: string) : Async<Guid option> =
    async {
        try
            use conn = openConn ()
            use cmd = new NpgsqlCommand(
                """SELECT user_id FROM email_verification_tokens
                   WHERE token = @token AND expires_at > now()
                   LIMIT 1""", conn)
            cmd.Parameters.AddWithValue("token", token) |> ignore

            use reader = cmd.ExecuteReader()
            if reader.Read() then
                return Some (reader.GetGuid(0))
            else
                return None
        with _ ->
            return None
    }

let deleteEmailVerificationToken (token: string) : Async<bool> =
    async {
        try
            use conn = openConn ()
            use cmd = new NpgsqlCommand("DELETE FROM email_verification_tokens WHERE token = @token", conn)
            cmd.Parameters.AddWithValue("token", token) |> ignore
            let rows = cmd.ExecuteNonQuery()
            return rows > 0
        with _ ->
            return false
    }

// ── Password Reset Tokens ───────────────────────────────────────────────────

let createPasswordResetToken (userId: Guid) (token: string) : Async<bool> =
    async {
        try
            use conn = openConn ()
            use cmd = new NpgsqlCommand(
                """INSERT INTO password_reset_tokens (user_id, token, expires_at)
                   VALUES (@user_id, @token, now() + interval '1 hour')""", conn)
            cmd.Parameters.AddWithValue("user_id", userId) |> ignore
            cmd.Parameters.AddWithValue("token", token) |> ignore
            let rows = cmd.ExecuteNonQuery()
            return rows > 0
        with _ ->
            return false
    }

let verifyPasswordResetToken (token: string) : Async<Guid option> =
    async {
        try
            use conn = openConn ()
            use cmd = new NpgsqlCommand(
                """SELECT user_id FROM password_reset_tokens
                   WHERE token = @token AND expires_at > now()
                   LIMIT 1""", conn)
            cmd.Parameters.AddWithValue("token", token) |> ignore

            use reader = cmd.ExecuteReader()
            if reader.Read() then
                return Some (reader.GetGuid(0))
            else
                return None
        with _ ->
            return None
    }

let deletePasswordResetToken (token: string) : Async<bool> =
    async {
        try
            use conn = openConn ()
            use cmd = new NpgsqlCommand("DELETE FROM password_reset_tokens WHERE token = @token", conn)
            cmd.Parameters.AddWithValue("token", token) |> ignore
            let rows = cmd.ExecuteNonQuery()
            return rows > 0
        with _ ->
            return false
    }

// ── OAuth Identity Management ───────────────────────────────────────────────

type UserIdentity =
    { Id: Guid
      UserId: Guid
      Provider: string
      ProviderId: string
      Email: string option
      DisplayName: string option
      AvatarUrl: string option
      CreatedAt: DateTime }

let createUserIdentity (userId: Guid) (provider: string) (providerId: string) (email: string option) (displayName: string option) (avatarUrl: string option) : Async<UserIdentity option> =
    async {
        try
            use conn = openConn ()
            use cmd = new NpgsqlCommand(
                """INSERT INTO user_identities (user_id, provider, subject_id, email, display_name, avatar_url)
                   VALUES (@user_id, @provider, @provider_id, @email, @display_name, @avatar_url)
                   RETURNING id, user_id, provider, subject_id, email, display_name, avatar_url, linked_at""", conn)
            cmd.Parameters.AddWithValue("user_id", userId) |> ignore
            cmd.Parameters.AddWithValue("provider", provider) |> ignore
            cmd.Parameters.AddWithValue("provider_id", providerId) |> ignore
            cmd.Parameters.AddWithValue("email", if email.IsSome then box email.Value else box DBNull.Value) |> ignore
            cmd.Parameters.AddWithValue("display_name", if displayName.IsSome then box displayName.Value else box DBNull.Value) |> ignore
            cmd.Parameters.AddWithValue("avatar_url", if avatarUrl.IsSome then box avatarUrl.Value else box DBNull.Value) |> ignore

            use reader = cmd.ExecuteReader()
            if reader.Read() then
                let identity = {
                    Id = reader.GetGuid(0)
                    UserId = reader.GetGuid(1)
                    Provider = reader.GetString(2)
                    ProviderId = reader.GetString(3)
                    Email = if reader.IsDBNull(4) then None else Some (reader.GetString(4))
                    DisplayName = if reader.IsDBNull(5) then None else Some (reader.GetString(5))
                    AvatarUrl = if reader.IsDBNull(6) then None else Some (reader.GetString(6))
                    CreatedAt = reader.GetFieldValue<DateTime>(7)
                }
                return Some identity
            else
                return None
        with _ ->
            return None
    }

let tryGetUserByIdentity (provider: string) (providerId: string) : Async<User option> =
    async {
        try
            use conn = openConn ()
            use cmd = new NpgsqlCommand(
                """SELECT u.id, u.email, u.email_verified_at, u.password_hash, u.display_name, u.avatar_url, u.bio, u.pronouns, u.location,
                         u.notify_by_email, u.role, u.status, u.created_at, u.updated_at
                   FROM users u
                   INNER JOIN user_identities ui ON u.id = ui.user_id
                   WHERE ui.provider = @provider AND ui.subject_id = @provider_id""", conn)
            cmd.Parameters.AddWithValue("provider", provider) |> ignore
            cmd.Parameters.AddWithValue("provider_id", providerId) |> ignore

            use reader = cmd.ExecuteReader()
            if reader.Read() then
                let user = {
                    Id = reader.GetGuid(0)
                    Email = reader.GetString(1)
                    EmailVerifiedAt = if reader.IsDBNull(2) then None else Some (reader.GetFieldValue<DateTime>(2))
                    PasswordHash = if reader.IsDBNull(3) then None else Some (reader.GetString(3))
                    DisplayName = if reader.IsDBNull(4) then None else Some (reader.GetString(4))
                    AvatarUrl = if reader.IsDBNull(5) then None else Some (reader.GetString(5))
                    Bio = if reader.IsDBNull(6) then None else Some (reader.GetString(6))
                    Pronouns = if reader.IsDBNull(7) then None else Some (reader.GetString(7))
                    Location = if reader.IsDBNull(8) then None else Some (reader.GetString(8))
                    NotifyByEmail = reader.GetBoolean(9)
                    Role = reader.GetString(10)
                    Status = reader.GetString(11)
                    CreatedAt = reader.GetFieldValue<DateTime>(12)
                    UpdatedAt = reader.GetFieldValue<DateTime>(13)
                }
                return Some user
            else
                return None
        with _ ->
            return None
    }
