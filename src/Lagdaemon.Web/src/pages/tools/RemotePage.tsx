import { useEffect, useRef, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import {
  approvePairing,
  clearGrant,
  loadStoredGrant,
  revokeGrant,
  saveGrant,
  signalingUrl,
  validateGrant,
  type HostSession,
} from '../../api/remoteApi'

// ── State machine ─────────────────────────────────────────────────────────────

type Phase =
  | { kind: 'checking' }
  | { kind: 'pairing'; error?: string }
  | { kind: 'connecting'; session: HostSession }
  | { kind: 'waiting'; session: HostSession }   // desktop offline, retrying
  | { kind: 'connected'; session: HostSession }
  | { kind: 'error'; message: string }

// ── Main component ─────────────────────────────────────────────────────────────

export default function RemotePage() {
  const { user, openLogin } = useAuth()
  const [phase, setPhase] = useState<Phase>({ kind: 'checking' })
  const [pairingCode, setPairingCode] = useState('')
  const [pairingBusy, setPairingBusy] = useState(false)
  const grantRef = useRef<string | null>(null)
  const wsRef = useRef<WebSocket | null>(null)
  const retryRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const pcRef = useRef<RTCPeerConnection | null>(null)
  const dcRef = useRef<RTCDataChannel | null>(null)

  // ── On mount: check for stored grant ────────────────────────────────────────

  useEffect(() => {
    if (!user) return
    let cancelled = false

    async function check() {
      const token = loadStoredGrant()
      if (!token) { setPhase({ kind: 'pairing' }); return }
      grantRef.current = token
      const result = await validateGrant(token)
      if (cancelled) return
      if (!result) {
        clearGrant()
        grantRef.current = null
        setPhase({ kind: 'pairing' })
      } else {
        connectSignaling(result.hostSession)
      }
    }

    check()
    return () => { cancelled = true; cleanup() }
  }, [user])

  // ── Signaling + WebRTC ────────────────────────────────────────────────────

  function cleanup() {
    if (retryRef.current) clearTimeout(retryRef.current)
    if (dcRef.current) { try { dcRef.current.close() } catch { /**/ } }
    if (pcRef.current) { try { pcRef.current.close() } catch { /**/ } }
    if (wsRef.current) { try { wsRef.current.close() } catch { /**/ } }
    dcRef.current = null; pcRef.current = null; wsRef.current = null
  }

  function connectSignaling(session: HostSession) {
    cleanup()
    setPhase({ kind: 'connecting', session })

    const ws = new WebSocket(signalingUrl(session.hostSessionId))
    wsRef.current = ws

    ws.onopen = () => {
      // Create peer connection and data channel (phone initiates)
      const pc = new RTCPeerConnection({
        iceServers: [{ urls: 'stun:stun.l.google.com:19302' }],
      })
      pcRef.current = pc

      const dc = pc.createDataChannel('control')
      dcRef.current = dc

      dc.onopen = () => setPhase({ kind: 'connected', session })
      dc.onclose = () => scheduleReconnect(session)
      dc.onmessage = handleDataChannelMessage

      pc.onicecandidate = ({ candidate }) => {
        if (candidate && ws.readyState === WebSocket.OPEN) {
          ws.send(JSON.stringify({ type: 'ice', candidate }))
        }
      }

      pc.createOffer()
        .then(offer => pc.setLocalDescription(offer))
        .then(() => {
          if (ws.readyState === WebSocket.OPEN)
            ws.send(JSON.stringify({ type: 'offer', sdp: pc.localDescription }))
        })
        .catch(() => scheduleReconnect(session))
    }

    ws.onmessage = async (ev) => {
      try {
        const msg = JSON.parse(ev.data as string)
        const pc = pcRef.current
        if (!pc) return
        if (msg.type === 'answer') {
          await pc.setRemoteDescription(new RTCSessionDescription(msg.sdp))
        } else if (msg.type === 'ice' && msg.candidate) {
          await pc.addIceCandidate(new RTCIceCandidate(msg.candidate))
        } else if (msg.type === 'unavailable') {
          // Desktop signaled it's not ready yet
          setPhase({ kind: 'waiting', session })
          scheduleReconnect(session)
        }
      } catch { /* ignore malformed messages */ }
    }

    ws.onerror = () => {
      setPhase({ kind: 'waiting', session })
      scheduleReconnect(session)
    }

    ws.onclose = () => {
      // Only schedule retry if we haven't moved to connected
      setPhase(p => p.kind === 'connected' ? p : { kind: 'waiting', session })
      scheduleReconnect(session)
    }
  }

  function scheduleReconnect(session: HostSession) {
    if (retryRef.current) clearTimeout(retryRef.current)
    retryRef.current = setTimeout(() => connectSignaling(session), 10_000)
  }

  function handleDataChannelMessage(ev: MessageEvent) {
    try {
      const msg = JSON.parse(ev.data as string)
      console.log('[Djehuti Remote] message from desktop:', msg)
      // Future: dispatch to control surface state
    } catch { /* ignore */ }
  }

  function sendCommand(cmd: object) {
    if (dcRef.current?.readyState === 'open') {
      dcRef.current.send(JSON.stringify(cmd))
    }
  }

  // ── Pairing flow ───────────────────────────────────────────────────────────

  async function handlePair() {
    if (!pairingCode.trim()) return
    setPairingBusy(true)
    try {
      const result = await approvePairing(pairingCode)
      saveGrant(result.grantToken)
      grantRef.current = result.grantToken
      setPairingCode('')
      connectSignaling(result.hostSession)
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Pairing failed'
      setPhase({ kind: 'pairing', error: msg })
    } finally {
      setPairingBusy(false)
    }
  }

  async function handleForget() {
    if (grantRef.current) await revokeGrant(grantRef.current)
    clearGrant()
    grantRef.current = null
    cleanup()
    setPhase({ kind: 'pairing' })
  }

  // ── Render ─────────────────────────────────────────────────────────────────

  if (!user) {
    return (
      <div className="remote-page">
        <div className="remote-card">
          <div className="remote-logo">📡</div>
          <h1>Djehuti Remote</h1>
          <p>Sign in to connect to your Djehuti Suite.</p>
          <button className="remote-btn primary" onClick={openLogin}>Sign In</button>
        </div>
      </div>
    )
  }

  if (phase.kind === 'checking') {
    return (
      <div className="remote-page">
        <div className="remote-card">
          <div className="remote-spinner" />
          <p>Checking authorization…</p>
        </div>
      </div>
    )
  }

  if (phase.kind === 'pairing') {
    return (
      <div className="remote-page">
        <div className="remote-card">
          <div className="remote-logo">📡</div>
          <h1>Djehuti Remote</h1>
          <p className="remote-hint">
            Open Djehuti Station on your desktop and click <strong>Remote</strong> to get
            an 8-character pairing code.
          </p>
          <input
            className="remote-code-input"
            type="text"
            placeholder="XXXXXXXX"
            value={pairingCode}
            maxLength={8}
            autoCapitalize="characters"
            onChange={e => setPairingCode(e.target.value.toUpperCase())}
            onKeyDown={e => e.key === 'Enter' && handlePair()}
          />
          {phase.error && <div className="remote-error">{phase.error}</div>}
          <button
            className="remote-btn primary"
            onClick={handlePair}
            disabled={pairingBusy || pairingCode.trim().length < 8}
          >
            {pairingBusy ? 'Pairing…' : 'Connect'}
          </button>
          <p className="remote-sub">
            You only need to do this once. After pairing, this phone connects automatically.
          </p>
        </div>
      </div>
    )
  }

  if (phase.kind === 'connecting') {
    return (
      <div className="remote-page">
        <div className="remote-card">
          <div className="remote-spinner" />
          <p>Connecting to <strong>{phase.session.deviceName ?? phase.session.productSlug}</strong>…</p>
        </div>
      </div>
    )
  }

  if (phase.kind === 'waiting') {
    return (
      <div className="remote-page">
        <div className="remote-card">
          <div className="remote-logo waiting">⏳</div>
          <h2>Waiting for Desktop</h2>
          <p className="remote-hint">
            <strong>{phase.session.deviceName ?? phase.session.productSlug}</strong> isn't
            responding right now. Make sure Djehuti Station is running on your desktop.
          </p>
          <p className="remote-sub">Retrying automatically…</p>
          <button className="remote-btn secondary" onClick={handleForget}>Forget this device</button>
        </div>
      </div>
    )
  }

  // Connected!
  const session = phase.session

  return (
    <div className="remote-page connected">
      <div className="remote-header">
        <span className="remote-connected-badge">● Connected</span>
        <span className="remote-device-name">{session.deviceName ?? session.productSlug}</span>
        <button className="remote-forget-btn" onClick={handleForget} title="Forget this device">✕</button>
      </div>

      {/* ── Transport controls ── */}
      <div className="remote-section">
        <div className="remote-transport">
          <button className="remote-transport-btn" onClick={() => sendCommand({ action: 'stop' })} title="Stop">■</button>
          <button className="remote-transport-btn large" onClick={() => sendCommand({ action: 'play' })} title="Play">▶</button>
          <button className="remote-transport-btn" onClick={() => sendCommand({ action: 'pause' })} title="Pause">⏸</button>
        </div>
      </div>

      {/* ── AI Agent chat (if available) ── */}
      {session.agentAvailable && (
        <div className="remote-section">
          <AgentChat sendCommand={sendCommand} />
        </div>
      )}

      {/* ── Camera (if on same network — future) ── */}
      <div className="remote-section muted">
        <p>Camera streaming — coming soon</p>
      </div>
    </div>
  )
}

// ── Inline AI agent chat ──────────────────────────────────────────────────────

function AgentChat({ sendCommand }: { sendCommand: (cmd: object) => void }) {
  const [messages, setMessages] = useState<{ role: 'user' | 'agent'; text: string }[]>([])
  const [input, setInput] = useState('')
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  function send() {
    const text = input.trim()
    if (!text) return
    setMessages(m => [...m, { role: 'user', text }])
    sendCommand({ action: 'agent_message', text })
    setInput('')
  }

  return (
    <div className="remote-agent-chat">
      <div className="remote-agent-label">AI Agent</div>
      <div className="remote-agent-messages">
        {messages.length === 0 && (
          <div className="remote-agent-empty">Ask the AI agent anything about this session…</div>
        )}
        {messages.map((m, i) => (
          <div key={i} className={`remote-agent-msg ${m.role}`}>{m.text}</div>
        ))}
        <div ref={bottomRef} />
      </div>
      <div className="remote-agent-input-row">
        <input
          className="remote-agent-input"
          type="text"
          placeholder="Message the agent…"
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && send()}
        />
        <button className="remote-btn primary small" onClick={send} disabled={!input.trim()}>↑</button>
      </div>
    </div>
  )
}
