module Djehuti.Api.RemoteSignalingManager

open System
open System.Collections.Concurrent
open System.Net.WebSockets
open System.Text

// Signaling relay for Creation Remote's P2P handshake (Creation-Suite #83,
// CR-M2/M4 -- see docs/architecture/Creation-Remote-Protocol.md §4 in that
// repo). Deliberately dumber than ClassroomConnectionManager: this never
// parses the WebRTC offer/answer/ICE payloads it relays, just forwards raw
// JSON text between exactly two peers (the receiver and the currently-
// signaling phone) for one host session. No captured media ever flows
// through this -- only the small signaling messages needed to negotiate a
// direct connection, per the suite's "no relay of media, ever" rule.

type PeerRole = Host | Phone

type ConnectedPeer = {
    HostSessionId: Guid
    Role: PeerRole
    WebSocket: WebSocket
    ConnectedAt: DateTimeOffset
}

type RemoteSignalingManager() =
    // One slot per role per host session -- a second connection for the
    // same (hostSessionId, role) replaces the first rather than queuing,
    // since only one active signaling attempt per role makes sense at a time.
    let connections = ConcurrentDictionary<Guid * PeerRole, ConnectedPeer>()

    member _.AddConnection (hostSessionId: Guid) (role: PeerRole) (ws: WebSocket) : unit =
        let peer = { HostSessionId = hostSessionId; Role = role; WebSocket = ws; ConnectedAt = DateTimeOffset.UtcNow }
        connections.AddOrUpdate((hostSessionId, role), peer, fun _ _ -> peer) |> ignore

    member _.RemoveConnection (hostSessionId: Guid) (role: PeerRole) : unit =
        connections.TryRemove((hostSessionId, role)) |> ignore

    member private _.GetPeer (hostSessionId: Guid) (role: PeerRole) : ConnectedPeer option =
        match connections.TryGetValue((hostSessionId, role)) with
        | true, peer -> Some peer
        | false, _ -> None

    member this.OtherRole (role: PeerRole) = match role with | Host -> Phone | Phone -> Host

    // Relays a raw text payload from one role to the other, for the same
    // host session. Silently drops it if the other side isn't connected --
    // signaling failure is handled the same as "peer unreachable" by the
    // callers (phone queues locally, per the suite's transport rules).
    member this.RelayAsync (hostSessionId: Guid) (fromRole: PeerRole) (payload: string) : System.Threading.Tasks.Task =
        task {
            match this.GetPeer hostSessionId (this.OtherRole fromRole) with
            | None -> ()
            | Some peer ->
                try
                    if peer.WebSocket.State = WebSocketState.Open then
                        let bytes = Encoding.UTF8.GetBytes(payload)
                        do! peer.WebSocket.SendAsync(
                            ArraySegment<byte>(bytes),
                            WebSocketMessageType.Text,
                            true,
                            System.Threading.CancellationToken.None)
                with _ -> ()
        }
