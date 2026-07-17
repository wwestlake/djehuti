module Djehuti.Api.ClassroomConnectionManager

#nowarn "FS0035"

open System
open System.Collections.Concurrent
open System.Net.WebSockets
open System.Text
open System.Text.Json

// Represents a connected user in a classroom
type ConnectedUser = {
    UserId: Guid
    Role: string  // "teacher" | "student"
    WebSocket: WebSocket
    ConnectedAt: DateTimeOffset
}

// Message types exchanged over WebSocket
type WebSocketMessage =
    | ChatMessage of { senderId: Guid; senderName: string; content: string; timestamp: DateTimeOffset }
    | Directive of { from: Guid; toUser: Guid; action: string; payload: JsonElement; timestamp: DateTimeOffset }
    | StateSync of { currentTopic: string option; teachingCanvas: JsonElement option; timestamp: DateTimeOffset }
    | UserJoined of { userId: Guid; userName: string; role: string }
    | UserLeft of { userId: Guid; timestamp: DateTimeOffset }
    | SystemMessage of { content: string; timestamp: DateTimeOffset }

// Serialize/deserialize WebSocket messages
let serializeMessage (msg: WebSocketMessage) : string =
    match msg with
    | ChatMessage data ->
        JsonSerializer.Serialize({|
            ``type`` = "chat"
            senderId = data.senderId
            senderName = data.senderName
            content = data.content
            timestamp = data.timestamp
        |})
    | Directive data ->
        JsonSerializer.Serialize({|
            ``type`` = "directive"
            from = data.from
            toUser = data.toUser
            action = data.action
            payload = data.payload
            timestamp = data.timestamp
        |})
    | StateSync data ->
        JsonSerializer.Serialize({|
            ``type`` = "state"
            currentTopic = data.currentTopic
            teachingCanvas = data.teachingCanvas
            timestamp = data.timestamp
        |})
    | UserJoined data ->
        JsonSerializer.Serialize({|
            ``type`` = "user_joined"
            userId = data.userId
            userName = data.userName
            role = data.role
        |})
    | UserLeft data ->
        JsonSerializer.Serialize({|
            ``type`` = "user_left"
            userId = data.userId
            timestamp = data.timestamp
        |})
    | SystemMessage data ->
        JsonSerializer.Serialize({|
            ``type`` = "system"
            content = data.content
            timestamp = data.timestamp
        |})

// Connection manager: tracks all connected users per classroom
type ClassroomConnectionManager() =
    let connections = ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, ConnectedUser>>()  // classroom -> (userId -> ConnectedUser)

    // Add a user connection to a classroom
    member this.AddConnection (classroomId: Guid) (userId: Guid) (role: string) (ws: WebSocket) : unit =
        let classroomConnections = connections.GetOrAdd(classroomId, fun _ -> ConcurrentDictionary())
        let user = { UserId = userId; Role = role; WebSocket = ws; ConnectedAt = DateTimeOffset.UtcNow }
        classroomConnections.AddOrUpdate(userId, user, fun _ _ -> user) |> ignore

    // Remove a user from a classroom
    member this.RemoveConnection (classroomId: Guid) (userId: Guid) : ConnectedUser option =
        match connections.TryGetValue(classroomId) with
        | true, classroomConnections ->
            match classroomConnections.TryRemove(userId) with
            | true, user -> Some user
            | false, _ -> None
        | false, _ -> None

    // Get a specific user connection
    member this.GetConnection (classroomId: Guid) (userId: Guid) : ConnectedUser option =
        match connections.TryGetValue(classroomId) with
        | true, classroomConnections ->
            match classroomConnections.TryGetValue(userId) with
            | true, user -> Some user
            | false -> None
        | false, _ -> None

    // Get all connections in a classroom
    member this.GetClassroomConnections (classroomId: Guid) : ConnectedUser list =
        match connections.TryGetValue(classroomId) with
        | true, classroomConnections -> classroomConnections.Values |> List.ofSeq
        | false, _ -> []

    // Broadcast message to all users in classroom (except sender if specified)
    member this.BroadcastAsync (classroomId: Guid) (message: WebSocketMessage) (exceptUser: Guid option) : System.Threading.Tasks.Task =
        task {
            let connections = this.GetClassroomConnections classroomId
            let messageBytes = serializeMessage message |> Encoding.UTF8.GetBytes

            for user in connections do
                match exceptUser with
                | Some excludeId when user.UserId = excludeId -> ()  // Skip sender
                | _ ->
                    try
                        if user.WebSocket.State = WebSocketState.Open then
                            do! user.WebSocket.SendAsync(
                                System.ArraySegment<byte>(messageBytes),
                                WebSocketMessageType.Text,
                                true,
                                System.Threading.CancellationToken.None)
                    with _ -> ()  // Ignore individual send failures
        }

    // Send message to specific user (unicast/sideband)
    member this.SendToUserAsync (classroomId: Guid) (userId: Guid) (message: WebSocketMessage) : System.Threading.Tasks.Task =
        task {
            match this.GetConnection classroomId userId with
            | Some user ->
                try
                    if user.WebSocket.State = WebSocketState.Open then
                        let messageBytes = serializeMessage message |> Encoding.UTF8.GetBytes
                        do! user.WebSocket.SendAsync(
                            System.ArraySegment<byte>(messageBytes),
                            WebSocketMessageType.Text,
                            true,
                            System.Threading.CancellationToken.None)
                with _ -> ()  // Ignore send failures
            | None -> ()
        }

    // Get active member count in classroom
    member this.GetActiveCount (classroomId: Guid) : int =
        (this.GetClassroomConnections classroomId).Length

    // Clean up all connections in a classroom (e.g., when classroom ends)
    member this.CloseClassroom (classroomId: Guid) : System.Threading.Tasks.Task =
        task {
            match connections.TryRemove(classroomId) with
            | true, classroomConnections ->
                for user in classroomConnections.Values do
                    try
                        if user.WebSocket.State = WebSocketState.Open then
                            do! user.WebSocket.CloseAsync(
                                WebSocketCloseStatus.NormalClosure,
                                "Classroom ended",
                                System.Threading.CancellationToken.None)
                        user.WebSocket.Dispose()
                    with _ -> ()
            | false, _ -> ()
        }
