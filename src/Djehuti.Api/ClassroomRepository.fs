module Djehuti.Api.ClassroomRepository

open System
open Npgsql

type Classroom = {
    Id:               Guid
    TeacherId:        Guid
    LessonPlanId:     Guid option
    Name:             string
    Slug:             string
    Status:           string  // "preparing" | "live" | "archived"
    Mode:             string  // "solo_ai" | "co_teach" | "teacher_directed" | "student_guided"
    MaxStudents:      int
    RecordingEnabled: bool
    CreatedAt:        DateTimeOffset
    UpdatedAt:        DateTimeOffset
}

type ClassroomMember = {
    Id:            Guid
    ClassroomId:   Guid
    UserId:        Guid
    Role:          string  // "student" | "observer"
    JoinedAt:      DateTimeOffset
    LastSeenAt:    DateTimeOffset option
    CreatedAt:     DateTimeOffset
}

type ClassroomMessage = {
    Id:           Guid
    ClassroomId:  Guid
    SenderId:     Guid option
    MessageType:  string  // "chat" | "directive" | "system"
    Content:      string
    TargetUserId: Guid option  // For directives (sideband)
    Metadata:     string option  // JSON
    CreatedAt:    DateTimeOffset
}

type ClassroomState = {
    Id:                Guid
    ClassroomId:       Guid
    CurrentTopicId:    string option
    TeachingCanvas:    string option  // JSON
    UpdatedAt:         DateTimeOffset
}

let private readClassroom (r: System.Data.Common.DbDataReader) : Classroom =
    {
        Id                = r.GetGuid(0)
        TeacherId         = r.GetGuid(1)
        LessonPlanId      = if r.IsDBNull(2) then None else Some (r.GetGuid(2))
        Name              = r.GetString(3)
        Slug              = r.GetString(4)
        Status            = r.GetString(5)
        Mode              = r.GetString(6)
        MaxStudents       = r.GetInt32(7)
        RecordingEnabled  = r.GetBoolean(8)
        CreatedAt         = r.GetFieldValue<DateTimeOffset>(9)
        UpdatedAt         = r.GetFieldValue<DateTimeOffset>(10)
    }

let private readMember (r: System.Data.Common.DbDataReader) : ClassroomMember =
    {
        Id            = r.GetGuid(0)
        ClassroomId   = r.GetGuid(1)
        UserId        = r.GetGuid(2)
        Role          = r.GetString(3)
        JoinedAt      = r.GetFieldValue<DateTimeOffset>(4)
        LastSeenAt    = if r.IsDBNull(5) then None else Some (r.GetFieldValue<DateTimeOffset>(5))
        CreatedAt     = r.GetFieldValue<DateTimeOffset>(6)
    }

let private readMessage (r: System.Data.Common.DbDataReader) : ClassroomMessage =
    {
        Id            = r.GetGuid(0)
        ClassroomId   = r.GetGuid(1)
        SenderId      = if r.IsDBNull(2) then None else Some (r.GetGuid(2))
        MessageType   = r.GetString(3)
        Content       = r.GetString(4)
        TargetUserId  = if r.IsDBNull(5) then None else Some (r.GetGuid(5))
        Metadata      = if r.IsDBNull(6) then None else Some (r.GetString(6))
        CreatedAt     = r.GetFieldValue<DateTimeOffset>(7)
    }

let private selectClassroomColumns = "id, teacher_id, lesson_plan_id, name, slug, status, mode, max_students, recording_enabled, created_at, updated_at"
let private selectMemberColumns = "id, classroom_id, user_id, role, joined_at, last_seen_at, created_at"
let private selectMessageColumns = "id, classroom_id, sender_id, message_type, content, target_user_id, metadata, created_at"

// Classrooms
let tryGetById (id: Guid) : Classroom option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"SELECT {selectClassroomColumns} FROM classrooms WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (readClassroom reader) else None

let tryGetBySlug (slug: string) : Classroom option =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"SELECT {selectClassroomColumns} FROM classrooms WHERE slug = @slug", conn)
    cmd.Parameters.AddWithValue("slug", slug) |> ignore
    use reader = cmd.ExecuteReader()
    if reader.Read() then Some (readClassroom reader) else None

let listByTeacher (teacherId: Guid) : Classroom list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"SELECT {selectClassroomColumns} FROM classrooms WHERE teacher_id = @teacherId ORDER BY created_at DESC", conn)
    cmd.Parameters.AddWithValue("teacherId", teacherId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do results <- readClassroom reader :: results
    List.rev results

let create (teacherId: Guid) (name: string) (lessonPlanId: Guid option) (mode: string) : Classroom =
    let baseSlug = name.Trim().ToLowerInvariant() |> fun s -> System.Text.RegularExpressions.Regex.Replace(s, @"[^a-z0-9]+", "-").Trim('-')
    let suffix = Guid.NewGuid().ToString("N").Substring(0, 6)
    let slug = $"{baseSlug}-{suffix}"

    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"""
        INSERT INTO classrooms (teacher_id, lesson_plan_id, name, slug, mode)
        VALUES (@teacherId, @lessonPlanId, @name, @slug, @mode)
        RETURNING {selectClassroomColumns}
    """, conn)
    cmd.Parameters.AddWithValue("teacherId", teacherId) |> ignore
    cmd.Parameters.AddWithValue("lessonPlanId", (lessonPlanId |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("name", name) |> ignore
    cmd.Parameters.AddWithValue("slug", slug) |> ignore
    cmd.Parameters.AddWithValue("mode", mode) |> ignore
    use reader = cmd.ExecuteReader()
    reader.Read() |> ignore
    readClassroom reader

let updateStatus (id: Guid) (status: string) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("UPDATE classrooms SET status = @status, updated_at = NOW() WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.Parameters.AddWithValue("status", status) |> ignore
    cmd.ExecuteNonQuery() > 0

let updateMode (id: Guid) (mode: string) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("UPDATE classrooms SET mode = @mode, updated_at = NOW() WHERE id = @id", conn)
    cmd.Parameters.AddWithValue("id", id) |> ignore
    cmd.Parameters.AddWithValue("mode", mode) |> ignore
    cmd.ExecuteNonQuery() > 0

// Membership
let addMember (classroomId: Guid) (userId: Guid) (role: string) : ClassroomMember =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"""
        INSERT INTO classroom_members (classroom_id, user_id, role)
        VALUES (@classroomId, @userId, @role)
        ON CONFLICT (classroom_id, user_id) DO UPDATE SET last_seen_at = NOW()
        RETURNING {selectMemberColumns}
    """, conn)
    cmd.Parameters.AddWithValue("classroomId", classroomId) |> ignore
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    cmd.Parameters.AddWithValue("role", role) |> ignore
    use reader = cmd.ExecuteReader()
    reader.Read() |> ignore
    readMember reader

let removeMember (classroomId: Guid) (userId: Guid) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("DELETE FROM classroom_members WHERE classroom_id = @classroomId AND user_id = @userId", conn)
    cmd.Parameters.AddWithValue("classroomId", classroomId) |> ignore
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    cmd.ExecuteNonQuery() > 0

let listMembers (classroomId: Guid) : ClassroomMember list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"SELECT {selectMemberColumns} FROM classroom_members WHERE classroom_id = @classroomId ORDER BY joined_at ASC", conn)
    cmd.Parameters.AddWithValue("classroomId", classroomId) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do results <- readMember reader :: results
    List.rev results

let isMember (classroomId: Guid) (userId: Guid) : bool =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand("SELECT 1 FROM classroom_members WHERE classroom_id = @classroomId AND user_id = @userId LIMIT 1", conn)
    cmd.Parameters.AddWithValue("classroomId", classroomId) |> ignore
    cmd.Parameters.AddWithValue("userId", userId) |> ignore
    use reader = cmd.ExecuteReader()
    reader.Read()

// Messages
let addMessage (classroomId: Guid) (senderId: Guid option) (messageType: string) (content: string) (targetUserId: Guid option) (metadata: string option) : ClassroomMessage =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"""
        INSERT INTO classroom_messages (classroom_id, sender_id, message_type, content, target_user_id, metadata)
        VALUES (@classroomId, @senderId, @messageType, @content, @targetUserId, @metadata)
        RETURNING {selectMessageColumns}
    """, conn)
    cmd.Parameters.AddWithValue("classroomId", classroomId) |> ignore
    cmd.Parameters.AddWithValue("senderId", (senderId |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("messageType", messageType) |> ignore
    cmd.Parameters.AddWithValue("content", content) |> ignore
    cmd.Parameters.AddWithValue("targetUserId", (targetUserId |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    cmd.Parameters.AddWithValue("metadata", (metadata |> Option.map box |> Option.defaultValue (box DBNull.Value))) |> ignore
    use reader = cmd.ExecuteReader()
    reader.Read() |> ignore
    readMessage reader

let listMessages (classroomId: Guid) (limit: int) : ClassroomMessage list =
    use conn = Database.openConnection()
    use cmd = new NpgsqlCommand($"SELECT {selectMessageColumns} FROM classroom_messages WHERE classroom_id = @classroomId ORDER BY created_at DESC LIMIT @limit", conn)
    cmd.Parameters.AddWithValue("classroomId", classroomId) |> ignore
    cmd.Parameters.AddWithValue("limit", limit) |> ignore
    use reader = cmd.ExecuteReader()
    let mutable results = []
    while reader.Read() do results <- readMessage reader :: results
    List.rev results
