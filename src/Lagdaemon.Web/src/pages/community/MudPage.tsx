import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { mudApi, type MudRoomState, type MudCommandResult, type MudItemView, type MudMapRoomView, type MudMapExitView } from '../../api/mudApi'
import { useAuth } from '../../contexts/AuthContext'

const QUICK_COMMANDS = [
  { label: 'Look', command: 'look' },
  { label: 'Search', command: 'search' },
  { label: 'Recipes', command: 'recipes' },
  { label: 'Room', command: 'examine room' },
  { label: 'Inventory', command: 'inventory' },
  { label: 'Craft torch', command: 'craft torch' },
]

type MudPageProps = {
  embedded?: boolean
}

function MapPanel({ rooms, exits, onJump }: { rooms: MudMapRoomView[]; exits: MudMapExitView[]; onJump: (command: string) => void }) {
  if (!rooms.length) return <p className="mud-empty">No map data yet.</p>
  const xs = rooms.map(room => room.x)
  const ys = rooms.map(room => room.y)
  const minX = Math.min(...xs)
  const maxX = Math.max(...xs)
  const minY = Math.min(...ys)
  const maxY = Math.max(...ys)
  const width = Math.max(1, maxX - minX)
  const height = Math.max(1, maxY - minY)
  const positionOf = (room: MudMapRoomView) => ({
    left: `${10 + ((room.x - minX) / width) * 80}%`,
    top: `${12 + ((room.y - minY) / height) * 70}%`,
  })

  return (
    <>
      <div className="mud-map">
        {rooms.map(room => {
          const pos = positionOf(room)
          return (
            <button
              key={room.roomId}
              className={`mud-map-room${room.current ? ' current' : ''}`}
              style={pos}
              onClick={() => room.current ? onJump('look') : undefined}
            >
              <strong>{room.roomName}</strong>
            </button>
          )
        })}
      </div>
      <div className="mud-map-legend">
        {exits.map(exit => (
          <span key={`${exit.fromRoomId}-${exit.toRoomId}-${exit.direction}`} className="mud-map-badge">
            {exit.direction} · {exit.exitType}
          </span>
        ))}
      </div>
    </>
  )
}

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

function ItemList({ items, onCommand, emptyText, action }: { items: MudItemView[]; onCommand: (command: string) => void; emptyText: string; action: 'get' | 'drop' | 'examine' }) {
  if (!items.length) return <p className="mud-empty">{emptyText}</p>
  return (
    <div className="mud-exits">
      {items.map(item => (
        <button
          key={item.slug}
          className="mud-exit-chip"
          onClick={() => onCommand(`${action} ${item.slug}`)}
        >
          <span>{item.name}</span>
          <small>{action === 'get' ? (item.portable ? 'Take' : 'Fixed') : action === 'drop' ? 'Drop' : item.readable ? 'Readable' : item.portable ? 'Portable' : 'Fixed'}</small>
        </button>
      ))}
    </div>
  )
}

export default function MudPage({ embedded = false }: MudPageProps) {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [state, setState] = useState<MudRoomState | null>(null)
  const [command, setCommand] = useState('look')
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [fullScreen, setFullScreen] = useState(false)
  const [loading, setLoading] = useState(true)
  const [showMap, setShowMap] = useState(false)

  useEffect(() => {
    const load = async () => {
      setLoading(true)
      try {
        const nextState = await mudApi.getMe()
        setState(nextState)
        setMessage(null)
      } catch (error) {
        setState(null)
        setMessage(error instanceof Error ? error.message : 'Failed to load the MUD.')
      } finally {
        setLoading(false)
      }
    }
    void load()
  }, [])

  const runCommand = async (nextCommand = command) => {
    if (!nextCommand.trim()) return
    setBusy(true)
    try {
      const result: MudCommandResult = await mudApi.command(nextCommand)
      if (result.state) setState(result.state)
      setMessage(result.message)
      if (result.success) setCommand('')
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'That command failed.')
    } finally {
      setBusy(false)
    }
  }

  const roomTitle = state?.roomName ?? 'LagDaemon MUD'
  const roomBody = state?.roomDescription ?? 'Enter the world and move through the keep.'
  const portableVisibleItems = useMemo(() => (state?.visibleItems ?? []).filter(item => item.portable), [state])
  const quickMovement = useMemo(() => (state?.exits ?? []).slice(0, 4), [state])
  const isFullScreen = embedded ? fullScreen : true
  const shellStyle = isFullScreen
    ? {
        position: 'fixed' as const,
        inset: 0,
        zIndex: 1200,
        overflowY: 'auto' as const,
        padding: embedded ? '16px' : '0',
        background: 'var(--bg)',
      }
    : undefined

  return (
    <section className={`mud-page${embedded ? ' mud-page-embedded' : ' mud-page-app'}`}>
      <div className="mud-shell" style={shellStyle}>
        <div className="mud-hero">
          <div>
            <div className="mud-kicker">LagDaemon MUD</div>
            <h1>{roomTitle}</h1>
            <p className="mud-zone">{state ? `Zone: ${state.zoneName}` : loading ? 'Loading world…' : 'Admin-only access for now.'}</p>
          </div>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <button className="mud-back-btn mud-map-toggle" onClick={() => setShowMap(v => !v)}>
              {showMap ? 'Hide map' : 'Map'}
            </button>
            {embedded && (
              <button className="mud-back-btn" onClick={() => setFullScreen(v => !v)}>
                {fullScreen ? 'Exit full screen' : 'Full screen'}
              </button>
            )}
            {!embedded && <button className="mud-back-btn" onClick={() => navigate('/')}>Exit game</button>}
          </div>
        </div>

        <div className="mud-card mud-room">
          <div className="mud-room-text">{roomBody}</div>
          <div className="mud-meta">
            <span>Character: {state?.characterName ?? user?.displayName ?? 'Admin'}</span>
            <span>Room: {state?.roomName ?? (loading ? 'Loading…' : 'Not placed yet')}</span>
            <span>Rank: {state?.mudTierName ?? 'Wanderer'}</span>
          </div>
        </div>

        <div className="mud-grid">
          <div className={`mud-card mud-map-card${showMap ? ' open' : ''}`}>
            <h2>Map</h2>
            <MapPanel rooms={state?.mapRooms ?? []} exits={state?.mapExits ?? []} onJump={runCommand} />
          </div>

          <div className="mud-card">
            <h2>Exits</h2>
            <ExitList exits={state?.exits ?? []} onCommand={runCommand} />
          </div>

          <div className="mud-card">
            <h2>Command</h2>
            {message && <div className="mud-message">{message}</div>}
            <form
              className="mud-command-row"
              onSubmit={e => {
                e.preventDefault()
                void runCommand()
              }}
            >
              <input
                className="mud-command-input"
                value={command}
                onChange={e => setCommand(e.target.value)}
                placeholder="search, get rag strip, recipes, craft torch..."
                disabled={busy || !user}
              />
              <button className="mud-command-btn" type="submit" disabled={busy || !user}>
                {busy ? 'Running' : 'Run'}
              </button>
            </form>

            <div className="mud-quick-commands">
              {QUICK_COMMANDS.map(item => (
                <button
                  key={item.command}
                  className="mud-quick-chip"
                  onClick={() => {
                    setCommand(item.command)
                    void runCommand(item.command)
                  }}
                  disabled={busy || !user}
                >
                  {item.label}
                </button>
              ))}
              {quickMovement.map(exit => (
                <button
                  key={exit.direction}
                  className="mud-quick-chip"
                  onClick={() => {
                    setCommand(exit.direction)
                    void runCommand(exit.direction)
                  }}
                  disabled={busy || !user}
                >
                  {exit.direction}
                </button>
              ))}
            </div>

            {!user && <p className="mud-empty">Sign in to enter the MUD and issue commands.</p>}
          </div>

          <div className="mud-card">
            <h2>Visible Items</h2>
            <ItemList items={portableVisibleItems} onCommand={runCommand} emptyText="No items to take here." action="get" />
            {(state?.visibleItems?.length ?? 0) > portableVisibleItems.length && (
              <>
                <h2 className="mud-subhead">Inspect</h2>
                <ItemList items={state?.visibleItems ?? []} onCommand={runCommand} emptyText="No visible items." action="examine" />
              </>
            )}
          </div>

          <div className="mud-card">
            <h2>Inventory</h2>
            <ItemList items={state?.inventoryItems ?? []} onCommand={runCommand} emptyText="You are carrying nothing." action="drop" />
          </div>
        </div>
      </div>
    </section>
  )
}
