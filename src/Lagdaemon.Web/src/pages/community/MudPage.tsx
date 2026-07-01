import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { mudApi, type MudRoomState, type MudCommandResult } from '../../api/mudApi'
import { useAuth } from '../../contexts/AuthContext'

const QUICK_COMMANDS = ['look', 'east', 'west', 'say hello']

function ExitList({ exits, onCommand }: { exits: MudRoomState['exits']; onCommand: (command: string) => void }) {
  if (!exits.length) return <p className="mud-empty">No exits visible.</p>
  return (
    <div className="mud-exits">
      {exits.map(exit => (
        <button
          key={`${exit.direction}-${exit.targetRoomId}`}
          className="mud-exit-chip"
          onClick={() => onCommand(exit.direction)}
        >
          <span>{exit.direction}</span>
          <small>{exit.targetRoomName}</small>
        </button>
      ))}
    </div>
  )
}

export default function MudPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [state, setState] = useState<MudRoomState | null>(null)
  const [command, setCommand] = useState('look')
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    mudApi.getMe().then(setState).catch(() => setState(null))
  }, [])

  const runCommand = async (nextCommand = command) => {
    if (!nextCommand.trim()) return
    setBusy(true)
    setMessage(null)
    try {
      const result: MudCommandResult = await mudApi.command(nextCommand)
      if (result.state) setState(result.state)
      setMessage(result.message)
      if (result.success) setCommand('')
    } finally {
      setBusy(false)
    }
  }

  const roomTitle = state?.roomName ?? 'The MUD is waiting'
  const roomBody = state?.roomDescription ?? 'Sign in and issue a command to enter the world.'

  return (
    <section className="mud-page">
      <div className="mud-shell">
        <div className="mud-hero">
          <div>
            <div className="mud-kicker">LagDaemon MUD</div>
            <h1>{roomTitle}</h1>
            <p className="mud-zone">{state ? `Zone: ${state.zoneName}` : 'Anonymous visitors can view the shell, but need to sign in to act.'}</p>
          </div>
          <button className="mud-back-btn" onClick={() => navigate('/forum')}>Back to forum</button>
        </div>

          <div className="mud-card mud-room">
          <div className="mud-room-text">{roomBody}</div>
          <div className="mud-meta">
            <span>Character: {state?.characterName ?? user?.displayName ?? 'Guest'}</span>
            <span>Room: {state?.roomId ?? 'unplaced'}</span>
            <span>Rank: {state?.mudTierName ?? 'Wanderer'}</span>
          </div>
        </div>

        <div className="mud-grid">
          <div className="mud-card">
            <h2>Exits</h2>
            <ExitList exits={state?.exits ?? []} onCommand={runCommand} />
          </div>

          <div className="mud-card">
            <h2>Command</h2>
            <form
              className="mud-command-row"
              onSubmit={e => {
                e.preventDefault()
                runCommand()
              }}
            >
              <input
                className="mud-command-input"
                value={command}
                onChange={e => setCommand(e.target.value)}
                placeholder="look, east, say hello..."
                disabled={busy || !user}
              />
              <button className="mud-command-btn" type="submit" disabled={busy || !user}>
                {busy ? 'Running' : 'Run'}
              </button>
            </form>

            <div className="mud-quick-commands">
              {QUICK_COMMANDS.map(item => (
                <button
                  key={item}
                  className="mud-quick-chip"
                  onClick={() => {
                    setCommand(item)
                    void runCommand(item)
                  }}
                  disabled={busy || !user}
                >
                  {item}
                </button>
              ))}
            </div>

            {!user && <p className="mud-empty">Sign in to enter the MUD and issue commands.</p>}
            {message && <div className="mud-message">{message}</div>}
          </div>
        </div>
      </div>
    </section>
  )
}
