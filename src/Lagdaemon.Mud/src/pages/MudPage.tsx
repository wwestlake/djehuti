import { useEffect, useMemo, useState } from 'react'
import { Backpack, Compass, Eye, LogOut, Map as MapIcon, Maximize2, Minimize2, ScrollText, Settings2, UserRound } from 'lucide-react'
import {
  mudApi,
  type MudCharacterSummary,
  type MudCommandResult,
  type MudCompanionSettings,
  type MudItemView,
  type MudMapExitView,
  type MudMapRoomView,
  type MudRoomState,
  type MudRosterView,
} from '../api/mudApi'
import { useAuth } from '../contexts/AuthContext'
import { THEMES, useTheme } from '../contexts/ThemeContext'

const QUICK_COMMANDS = [
  { label: 'Look', command: 'look' },
  { label: 'Search', command: 'search' },
  { label: 'Recipes', command: 'recipes' },
  { label: 'Room', command: 'examine room' },
  { label: 'Craft torch', command: 'craft torch' },
]

type MudPageProps = {
  embedded?: boolean
  onExit?: () => void
}

type MudCompanionDraft = {
  enabled: boolean
  mode: string
  model: string
  disclosure: string
  allowOnlineConcurrency: boolean
  useByoOpenAiKey: boolean
  openAiApiKey: string
}

type GameView = 'world' | 'map' | 'items' | 'inventory' | 'character' | 'settings'

const SUB_VIEWS: Partial<Record<GameView, { id: string; label: string }[]>> = {
  items: [
    { id: 'take', label: 'Take' },
    { id: 'inspect', label: 'Inspect' },
  ],
  character: [
    { id: 'stats', label: 'Stats' },
    { id: 'roster', label: 'Roster' },
  ],
}

function toCompanionDraft(settings: MudCompanionSettings): MudCompanionDraft {
  return {
    enabled: settings.enabled,
    mode: settings.mode,
    model: settings.model,
    disclosure: settings.disclosure,
    allowOnlineConcurrency: settings.allowOnlineConcurrency,
    useByoOpenAiKey: settings.useByoOpenAiKey,
    openAiApiKey: '',
  }
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

void MapPanel

function ZoneMapPanel({ rooms, exits, onJump }: { rooms: MudMapRoomView[]; exits: MudMapExitView[]; onJump: (command: string) => void }) {
  if (!rooms.length) return <p className="mud-empty">No map data yet.</p>
  const xs = rooms.map(room => room.x)
  const ys = rooms.map(room => room.y)
  const minX = Math.min(...xs)
  const maxX = Math.max(...xs)
  const minY = Math.min(...ys)
  const maxY = Math.max(...ys)
  const width = Math.max(1, maxX - minX)
  const height = Math.max(1, maxY - minY)
  const positionOf = (room: MudMapRoomView) => {
    const leftPercent = 10 + ((room.x - minX) / width) * 80
    const topPercent = 12 + ((room.y - minY) / height) * 70
    return {
      left: `${leftPercent}%`,
      top: `${topPercent}%`,
      leftPercent,
      topPercent,
    }
  }
  const roomPositions = Object.fromEntries(rooms.map(room => [room.roomId, positionOf(room)]))
  const currentRoom = rooms.find(room => room.current) ?? rooms[0]
  const uniqueExitTypes = Array.from(new Set(exits.map(exit => exit.exitType)))

  const exitTypeGlyph = (exitType: string) => {
    switch (exitType) {
      case 'portal':
        return '◉'
      case 'stairs-up':
      case 'stairs-down':
        return '⇅'
      case 'elevator':
        return '▣'
      case 'gate':
      case 'door':
      case 'sealed-door':
      case 'bulkhead':
        return '▤'
      case 'catwalk':
        return '╌'
      default:
        return '—'
    }
  }

  return (
    <>
      <div className="mud-map mud-map-enhanced">
        <svg className="mud-map-lines" viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true">
          {exits.map(exit => {
            const from = roomPositions[exit.fromRoomId]
            const to = roomPositions[exit.toRoomId]
            if (!from || !to) return null
            return (
              <line
                key={`${exit.fromRoomId}-${exit.toRoomId}-${exit.direction}`}
                className={`mud-map-line exit-${exit.exitType}`}
                x1={from.leftPercent}
                y1={from.topPercent}
                x2={to.leftPercent}
                y2={to.topPercent}
              />
            )
          })}
        </svg>
        {rooms.map(room => {
          const pos = roomPositions[room.roomId]
          return (
            <button
              key={room.roomId}
              className={`mud-map-room${room.current ? ' current' : ''}`}
              style={{ left: pos.left, top: pos.top }}
              onClick={() => room.current ? onJump('look') : undefined}
            >
              <strong>{room.roomName}</strong>
              <small>{room.current ? 'You are here' : 'Mapped room'}</small>
            </button>
          )
        })}
      </div>
      <div className="mud-map-summary">
        <span className="mud-map-summary-card">
          <strong>Current</strong>
          <span>{currentRoom.roomName}</span>
        </span>
        <span className="mud-map-summary-card">
          <strong>Rooms</strong>
          <span>{rooms.length}</span>
        </span>
        <span className="mud-map-summary-card">
          <strong>Paths</strong>
          <span>{exits.length}</span>
        </span>
      </div>
      <div className="mud-map-legend">
        {uniqueExitTypes.map(exitType => (
          <span key={exitType} className="mud-map-badge">
            {exitTypeGlyph(exitType)} {exitType.replace(/-/g, ' ')}
          </span>
        ))}
      </div>
    </>
  )
}

const COMPASS_ROWS: (string | null)[][] = [
  ['northwest', 'north', 'northeast'],
  ['west', null, 'east'],
  ['southwest', 'south', 'southeast'],
]

const COMPASS_LABELS: Record<string, string> = {
  north: 'N',
  south: 'S',
  east: 'E',
  west: 'W',
  northeast: 'NE',
  northwest: 'NW',
  southeast: 'SE',
  southwest: 'SW',
  up: 'Up',
  down: 'Dn',
}

function CompassPad({ exits, onCommand, disabled }: { exits: MudRoomState['exits']; onCommand: (command: string) => void; disabled: boolean }) {
  const byDirection = new Map(exits.map(exit => [exit.direction.toLowerCase(), exit]))
  const otherExits = exits.filter(exit => !(exit.direction.toLowerCase() in COMPASS_LABELS))

  const renderButton = (direction: string) => {
    const exit = byDirection.get(direction)
    return (
      <button
        key={direction}
        type="button"
        className={`mud-compass-btn${exit ? ' available' : ''}`}
        onClick={() => exit && onCommand(direction)}
        disabled={disabled || !exit}
        title={exit ? `${direction} → ${exit.targetRoomName}` : direction}
        aria-label={exit ? `Go ${direction} to ${exit.targetRoomName}` : `${direction} (no exit)`}
      >
        {COMPASS_LABELS[direction]}
      </button>
    )
  }

  return (
    <div className="mud-compass-block">
      <div className="mud-compass-wrap">
        <div className="mud-compass">
          {COMPASS_ROWS.flatMap(row => row.map(direction => direction
            ? renderButton(direction)
            : (
              <button
                key="look"
                type="button"
                className="mud-compass-btn look"
                onClick={() => onCommand('look')}
                disabled={disabled}
                title="Look"
                aria-label="Look around"
              >
                <Eye size={16} />
              </button>
            )))}
        </div>
        <div className="mud-compass-vert">
          {renderButton('up')}
          {renderButton('down')}
        </div>
      </div>
      {exits.length > 0 ? (
        <p className="mud-exit-names">
          {exits.map(exit => `${exit.direction}: ${exit.targetRoomName}`).join(' · ')}
        </p>
      ) : (
        <p className="mud-empty">No exits visible.</p>
      )}
      {otherExits.length > 0 && (
        <div className="mud-quick-commands">
          {otherExits.map(exit => (
            <button
              key={`${exit.direction}-${exit.targetRoomId}`}
              className="mud-quick-chip"
              onClick={() => onCommand(exit.direction)}
              disabled={disabled}
            >
              {exit.direction}
            </button>
          ))}
        </div>
      )}
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

function StatPills({ character }: { character: MudCharacterSummary | MudRoomState }) {
  const stats = [
    ['Presence', character.stats.presence],
    ['Wit', character.stats.wit],
    ['Resolve', character.stats.resolve],
    ['Lore', character.stats.lore],
    ['Craft', character.stats.craft],
    ['Guile', character.stats.guile],
  ]

  const skills = [
    ['Searching', character.skills.searching],
    ['Crafting', character.skills.crafting],
    ['Navigation', character.skills.navigation],
    ['Lorekeeping', character.skills.lorekeeping],
    ['Negotiation', character.skills.negotiation],
    ['Devices', character.skills.devices],
    ['Survival', character.skills.survival],
  ]

  return (
    <>
      <div className="mud-pill-grid">
        {stats.map(([label, value]) => (
          <span key={label} className="mud-stat-pill">{label}: {value}</span>
        ))}
      </div>
      <div className="mud-pill-grid">
        {skills.map(([label, value]) => (
          <span key={label} className="mud-stat-pill skill">{label}: {value}</span>
        ))}
      </div>
    </>
  )
}

function GameNavButton(
  {
    active,
    label,
    onClick,
    children,
  }: {
    active: boolean
    label: string
    onClick: () => void
    children: React.ReactNode
  },
) {
  return (
    <button
      type="button"
      className={`mud-game-nav-btn${active ? ' active' : ''}`}
      onClick={onClick}
      aria-label={label}
      title={label}
    >
      <span className="mud-game-nav-icon">{children}</span>
      <span className="mud-game-nav-label">{label}</span>
    </button>
  )
}

export default function MudPage({ embedded = false, onExit }: MudPageProps) {
  const { user } = useAuth()
  const handleExit = () => {
    if (onExit) onExit()
    else window.location.href = '/'
  }
  const { theme, setTheme } = useTheme()
  const [roster, setRoster] = useState<MudRosterView | null>(null)
  const [state, setState] = useState<MudRoomState | null>(null)
  const [command, setCommand] = useState('look')
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [fullScreen, setFullScreen] = useState(false)
  const [loading, setLoading] = useState(true)
  const [activeView, setActiveView] = useState<GameView>('world')
  const [subView, setSubView] = useState('')
  const [createRealm, setCreateRealm] = useState('medieval')
  const [createName, setCreateName] = useState('')
  const [createDisplayName, setCreateDisplayName] = useState('')
  const [companion, setCompanion] = useState<MudCompanionSettings | null>(null)
  const [companionDraft, setCompanionDraft] = useState<MudCompanionDraft | null>(null)
  const [companionLoading, setCompanionLoading] = useState(false)
  const selectedCharacter = roster?.characters.find(character => character.isSelected) ?? null

  const loadAll = async () => {
    setLoading(true)
    try {
      const [nextRoster, nextState] = await Promise.all([mudApi.getRoster(), mudApi.getMe()])
      setRoster(nextRoster)
      setState(nextState)
      setMessage(null)
    } catch (error) {
      setRoster(null)
      setState(null)
      setMessage(error instanceof Error ? error.message : 'Failed to load the MUD.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadAll()
  }, [])

  const openView = (view: GameView) => {
    setActiveView(view)
    setSubView(SUB_VIEWS[view]?.[0]?.id ?? '')
  }

  useEffect(() => {
    if (state) openView('world')
  }, [state?.characterId])

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

  const handleCreateCharacter = async () => {
    if (!createName.trim()) return
    setBusy(true)
    try {
      const nextRoster = await mudApi.createCharacter({
        realmSlug: createRealm,
        name: createName.trim(),
        displayName: createDisplayName.trim() || undefined,
      })
      setRoster(nextRoster)
      const nextState = await mudApi.getMe()
      setState(nextState)
      setMessage(`Character created in ${createRealm}.`)
      setCreateName('')
      setCreateDisplayName('')
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Could not create that character.')
    } finally {
      setBusy(false)
    }
  }

  const handleSelectCharacter = async (characterId: string) => {
    setBusy(true)
    try {
      const nextState = await mudApi.selectCharacter(characterId)
      setState(nextState)
      const nextRoster = await mudApi.getRoster()
      setRoster(nextRoster)
      setMessage(`Now playing ${nextState.characterName}.`)
      openView('world')
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Could not select that character.')
    } finally {
      setBusy(false)
    }
  }

  const handleDeleteCharacter = async (character: MudCharacterSummary) => {
    const confirmed = window.confirm(`Delete ${character.displayName}? Inventory and carried progress on that character will be lost.`)
    if (!confirmed) return

    setBusy(true)
    try {
      const nextRoster = await mudApi.deleteCharacter(character.id)
      setRoster(nextRoster)
      const nextState = await mudApi.getMe()
      setState(nextState)
      setMessage(`${character.displayName} was deleted.`)
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Could not delete that character.')
    } finally {
      setBusy(false)
    }
  }

  useEffect(() => {
    const characterId = selectedCharacter?.id
    if (!characterId) {
      setCompanion(null)
      setCompanionDraft(null)
      return
    }

    let cancelled = false
    setCompanionLoading(true)
    void mudApi.getCompanion(characterId)
      .then(settings => {
        if (cancelled) return
        setCompanion(settings)
        setCompanionDraft(toCompanionDraft(settings))
      })
      .catch(error => {
        if (cancelled) return
        setCompanion(null)
        setCompanionDraft(null)
        setMessage(error instanceof Error ? error.message : 'Could not load companion settings.')
      })
      .finally(() => {
        if (!cancelled) setCompanionLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [selectedCharacter?.id])

  const handleSaveCompanion = async () => {
    if (!selectedCharacter || !companionDraft) return
    setBusy(true)
    try {
      const nextSettings = await mudApi.saveCompanion(selectedCharacter.id, companionDraft)
      setCompanion(nextSettings)
      setCompanionDraft(toCompanionDraft(nextSettings))
      setMessage(nextSettings.useByoOpenAiKey
        ? 'Companion settings saved. Your key is stored on the server and not shown back to the screen.'
        : 'Companion settings saved.')
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Could not save companion settings.')
    } finally {
      setBusy(false)
    }
  }

  const handleRemoveCompanionKey = async () => {
    if (!selectedCharacter) return
    const confirmed = window.confirm('Remove the saved OpenAI key for this character?')
    if (!confirmed) return

    setBusy(true)
    try {
      const nextSettings = await mudApi.removeCompanionKey(selectedCharacter.id)
      setCompanion(nextSettings)
      setCompanionDraft(toCompanionDraft(nextSettings))
      setMessage('Saved OpenAI key removed for this character.')
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Could not remove that key.')
    } finally {
      setBusy(false)
    }
  }

  const roomTitle = state?.roomName ?? 'LagDaemon MUD'
  const roomBody = state?.roomDescription ?? 'Choose a character to enter the world.'
  const portableVisibleItems = useMemo(() => (state?.visibleItems ?? []).filter(item => item.portable), [state])
  const shareUrl = typeof window !== 'undefined' ? window.location.href : 'https://lagdaemon.com/mud'
  const facebookShareUrl = `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(shareUrl)}`
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
        {state ? (
          <header className="mud-game-header">
            <div className="mud-game-header-main">
              <strong>{roomTitle}</strong>
              <span className="mud-game-header-meta">
                {state.characterName} · {state.mudTierName} · {state.realmName} · {state.zoneName}
              </span>
            </div>
            <div className="mud-game-header-actions">
              {embedded && (
                <button
                  className="mud-icon-btn"
                  onClick={() => setFullScreen(v => !v)}
                  title={fullScreen ? 'Exit full screen' : 'Full screen'}
                  aria-label={fullScreen ? 'Exit full screen' : 'Full screen'}
                >
                  {fullScreen ? <Minimize2 size={16} /> : <Maximize2 size={16} />}
                </button>
              )}
              {!embedded && (
                <button
                  className="mud-icon-btn"
                  onClick={handleExit}
                  title="Exit game"
                  aria-label="Exit game"
                >
                  <LogOut size={16} />
                </button>
              )}
            </div>
          </header>
        ) : (
          <div className="mud-hero">
            <div>
              <div className="mud-kicker">LagDaemon MUD</div>
              <h1>Character Roster</h1>
              <p className="mud-zone">
                {loading ? 'Loading world…' : 'Choose or create the character you want to play.'}
              </p>
            </div>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              {embedded && (
                <button className="mud-back-btn" onClick={() => setFullScreen(v => !v)}>
                  {fullScreen ? 'Exit full screen' : 'Full screen'}
                </button>
              )}
              {!embedded && <button className="mud-back-btn" onClick={handleExit}>Exit game</button>}
            </div>
          </div>
        )}

        {message && <div className="mud-message" style={{ marginBottom: 16 }}>{message}</div>}

        {!state && !loading && (
          <>
            <div className="mud-card" style={{ marginBottom: 16 }}>
              <div className="blog-share-row" style={{ margin: 0, padding: 0, borderTop: 'none', borderBottom: 'none' }}>
                <span className="blog-share-label">Share</span>
                <a
                  className="blog-share-btn"
                  href={facebookShareUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  title="Share on Facebook"
                >
                  Facebook
                </a>
              </div>
            </div>

            <div className="mud-grid mud-grid-roster">
              <div className="mud-card">
              <h2>Roster</h2>
              {roster ? (
                <>
                  <div className="mud-meta" style={{ marginBottom: 16 }}>
                    <span>Paid slots: {roster.paidSlotsUsed} / {roster.paidSlotsTotal}</span>
                    <span>Remaining: {roster.paidSlotsRemaining}</span>
                    <span>Bonus slots: {roster.bonusSlots}</span>
                  </div>

                  <div className="mud-pill-grid" style={{ marginBottom: 16 }}>
                    {roster.realms.map(realm => (
                      <span key={realm.realmSlug} className="mud-stat-pill">
                        {realm.realmName}: {realm.characterCount} {realm.canCreateFreeStarter ? '· free starter open' : '· starter used'}
                      </span>
                    ))}
                  </div>

                  <div className="mud-roster-list">
                    {roster.characters.length === 0 && <p className="mud-empty">No characters yet. Create one below.</p>}
                    {roster.characters.map(character => (
                      <div key={character.id} className={`mud-roster-card${character.isSelected ? ' selected' : ''}`}>
                        <div className="mud-roster-head">
                          <div>
                            <strong>{character.displayName}</strong>
                            <div className="mud-empty">{character.realmName} · {character.currentRoomName}</div>
                          </div>
                          <div className="mud-meta">
                            <span>{character.mudTierName}</span>
                            <span>{character.inventoryCount} items</span>
                          </div>
                        </div>
                        <StatPills character={character} />
                        <div className="mud-quick-commands" style={{ marginTop: 12 }}>
                          {!character.isSelected && (
                            <button className="mud-quick-chip" onClick={() => void handleSelectCharacter(character.id)} disabled={busy}>
                              Play
                            </button>
                          )}
                          <button className="mud-quick-chip danger" onClick={() => void handleDeleteCharacter(character)} disabled={busy}>
                            Delete
                          </button>
                        </div>
                      </div>
                    ))}
                  </div>
                </>
              ) : (
                <p className="mud-empty">Loading roster…</p>
              )}
              </div>

              <div className="mud-card">
                <h2>Create Character</h2>
                <div className="mud-form-grid">
                  <select className="mud-command-input" value={createRealm} onChange={e => setCreateRealm(e.target.value)} disabled={busy}>
                    {(roster?.realms ?? [{ realmSlug: 'medieval', realmName: 'Medieval' }, { realmSlug: 'sci-fi', realmName: 'Sci-Fi' }]).map(realm => (
                      <option key={realm.realmSlug} value={realm.realmSlug}>{realm.realmName}</option>
                    ))}
                  </select>
                  <input
                    className="mud-command-input"
                    value={createName}
                    onChange={e => setCreateName(e.target.value)}
                    placeholder="Character name"
                    disabled={busy}
                  />
                  <input
                    className="mud-command-input"
                    value={createDisplayName}
                    onChange={e => setCreateDisplayName(e.target.value)}
                    placeholder="Display name (optional)"
                    disabled={busy}
                  />
                  <button className="mud-command-btn" onClick={() => void handleCreateCharacter()} disabled={busy || !createName.trim()}>
                    Create character
                  </button>
                </div>
                <p className="mud-empty" style={{ marginTop: 16 }}>
                  Free players get one starter character in each realm. More characters use paid slots.
                </p>
                <p className="mud-empty">
                  If the roster is full, delete a character, upgrade tier, or add another slot.
                </p>
              </div>
            </div>
          </>
        )}

        {state && (
          <div className="mud-game-shell">
            <div className="mud-game-panel">
              {activeView === 'world' && (
                <div className="mud-game-view">
                  <div className="mud-card mud-card-flat">
                    <div className="mud-room-text">{roomBody}</div>
                  </div>
                  <div className="mud-card mud-card-flat">
                    <CompassPad exits={state.exits ?? []} onCommand={runCommand} disabled={busy || !user} />
                  </div>
                  <div className="mud-card mud-card-flat">
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
                    </div>
                  </div>
                </div>
              )}

              {activeView === 'map' && (
                <div className="mud-game-view">
                  <div className="mud-card mud-card-flat">
                    <ZoneMapPanel rooms={state.mapRooms ?? []} exits={state.mapExits ?? []} onJump={runCommand} />
                  </div>
                </div>
              )}

              {activeView === 'items' && (
                <div className="mud-game-view">
                  <div className="mud-card mud-card-flat">
                    {subView === 'inspect' ? (
                      <ItemList items={state.visibleItems ?? []} onCommand={runCommand} emptyText="No visible items." action="examine" />
                    ) : (
                      <ItemList items={portableVisibleItems} onCommand={runCommand} emptyText="No items to take here." action="get" />
                    )}
                  </div>
                </div>
              )}

              {activeView === 'inventory' && (
                <div className="mud-game-view">
                  <div className="mud-card mud-card-flat">
                    <ItemList items={state.inventoryItems ?? []} onCommand={runCommand} emptyText="You are carrying nothing." action="drop" />
                  </div>
                </div>
              )}

              {activeView === 'character' && (
                <div className="mud-game-view">
                  <div className="mud-card mud-card-flat">
                    {subView !== 'roster' && <StatPills character={selectedCharacter ?? state} />}
                    {subView === 'roster' && roster && (
                      <div className="mud-roster-list mud-roster-inline">
                        {roster.characters.map(character => (
                          <div key={character.id} className={`mud-roster-card${character.isSelected ? ' selected' : ''}`}>
                            <div className="mud-roster-head">
                              <div>
                                <strong>{character.displayName}</strong>
                                <div className="mud-empty">{character.realmName} · {character.currentRoomName}</div>
                              </div>
                              <div className="mud-meta">
                                <span>{character.mudTierName}</span>
                                <span>{character.inventoryCount} items</span>
                              </div>
                            </div>
                            <div className="mud-quick-commands" style={{ marginTop: 12 }}>
                              {!character.isSelected && (
                                <button className="mud-quick-chip" onClick={() => void handleSelectCharacter(character.id)} disabled={busy}>
                                  Switch
                                </button>
                              )}
                              <button className="mud-quick-chip danger" onClick={() => void handleDeleteCharacter(character)} disabled={busy}>
                                Delete
                              </button>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              )}

              {activeView === 'settings' && (
                <div className="mud-game-view">
                  <div className="mud-card mud-card-flat">
                    <div className="mud-form-grid">
                      <label className="mud-field-label">
                        Style
                        <select className="mud-command-input" value={theme} onChange={e => setTheme(e.target.value as typeof theme)}>
                          {THEMES.map(option => (
                            <option key={option.id} value={option.id}>{option.label}</option>
                          ))}
                        </select>
                      </label>
                    </div>

                    {!selectedCharacter && (
                      <p className="mud-empty">Pick a character first. Companion settings are saved per character.</p>
                    )}
                    {selectedCharacter && companionLoading && (
                      <p className="mud-empty">Loading companion settings…</p>
                    )}
                    {selectedCharacter && !companionLoading && companion && companionDraft && (
                      <div className="mud-form-grid">
                        <div className="mud-companion-head">
                          <strong>{selectedCharacter.displayName}</strong>
                          <span className={`mud-status-pill${companion.eligible ? ' ok' : ''}`}>
                            {companion.eligible ? 'Eligible' : 'Upgrade required'}
                          </span>
                        </div>

                        {!companion.eligible && <p className="mud-empty">{companion.eligibilityReason}</p>}

                        <label className="mud-check-row">
                          <input
                            type="checkbox"
                            checked={companionDraft.enabled}
                            onChange={e => setCompanionDraft(current => current ? { ...current, enabled: e.target.checked } : current)}
                            disabled={busy || !companion.eligible}
                          />
                          <span>Enable this character’s AI companion</span>
                        </label>

                        <label className="mud-field-label">
                          Companion mode
                          <select
                            className="mud-command-input"
                            value={companionDraft.mode}
                            onChange={e => setCompanionDraft(current => current ? { ...current, mode: e.target.value } : current)}
                            disabled={busy || !companion.eligible}
                          >
                            <option value="solitary">Solitary</option>
                            <option value="social">Social</option>
                          </select>
                        </label>

                        <label className="mud-field-label">
                          Model
                          <input
                            className="mud-command-input"
                            value={companionDraft.model}
                            onChange={e => setCompanionDraft(current => current ? { ...current, model: e.target.value } : current)}
                            placeholder="gpt-4.1-mini"
                            disabled={busy || !companion.eligible}
                          />
                        </label>

                        <label className="mud-field-label">
                          Disclosure
                          <select
                            className="mud-command-input"
                            value={companionDraft.disclosure}
                            onChange={e => setCompanionDraft(current => current ? { ...current, disclosure: e.target.value } : current)}
                            disabled={busy || !companion.eligible}
                          >
                            <option value="tagged">Tagged</option>
                            <option value="contextual">Contextual</option>
                            <option value="hidden">Hidden</option>
                          </select>
                        </label>

                        <label className="mud-check-row">
                          <input
                            type="checkbox"
                            checked={companionDraft.allowOnlineConcurrency}
                            onChange={e => setCompanionDraft(current => current ? { ...current, allowOnlineConcurrency: e.target.checked } : current)}
                            disabled={busy || !companion.eligible}
                          />
                          <span>Allow the companion to stay active while you are online elsewhere</span>
                        </label>

                        <label className="mud-check-row">
                          <input
                            type="checkbox"
                            checked={companionDraft.useByoOpenAiKey}
                            onChange={e => setCompanionDraft(current => current ? { ...current, useByoOpenAiKey: e.target.checked } : current)}
                            disabled={busy || !companion.eligible}
                          />
                          <span>Use my own OpenAI API key for this character</span>
                        </label>

                        <label className="mud-field-label">
                          OpenAI API key
                          <input
                            className="mud-command-input"
                            type="password"
                            autoComplete="new-password"
                            value={companionDraft.openAiApiKey}
                            onChange={e => setCompanionDraft(current => current ? { ...current, openAiApiKey: e.target.value } : current)}
                            placeholder={companion.hasByoOpenAiKey ? 'Saved key on file. Paste a new one to replace it.' : 'Paste a key to save it for this character.'}
                            disabled={busy || !companion.eligible}
                          />
                        </label>

                        <div className="mud-meta">
                          <span>Saved key: {companion.hasByoOpenAiKey ? 'Yes' : 'No'}</span>
                          <span>Last update: {companion.updatedAt ? new Date(companion.updatedAt).toLocaleString() : 'Not saved yet'}</span>
                        </div>

                        <div className="mud-quick-commands">
                          <button className="mud-command-btn" onClick={() => void handleSaveCompanion()} disabled={busy || !companion.eligible}>
                            Save companion
                          </button>
                          {companion.hasByoOpenAiKey && (
                            <button className="mud-quick-chip danger" onClick={() => void handleRemoveCompanionKey()} disabled={busy}>
                              Remove saved key
                            </button>
                          )}
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              )}
            </div>

            <div className="mud-game-dock">
              <div className="mud-card mud-game-command-dock">
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
              </div>

              {SUB_VIEWS[activeView] && (
                <div className="mud-game-subnav">
                  {SUB_VIEWS[activeView]!.map(tab => (
                    <button
                      key={tab.id}
                      type="button"
                      className={`mud-subnav-btn${subView === tab.id ? ' active' : ''}`}
                      onClick={() => setSubView(tab.id)}
                    >
                      {tab.label}
                    </button>
                  ))}
                </div>
              )}

              <nav className="mud-game-nav">
                <GameNavButton active={activeView === 'world'} label="World" onClick={() => openView('world')}>
                  <Compass size={18} />
                </GameNavButton>
                <GameNavButton active={activeView === 'map'} label="Map" onClick={() => openView('map')}>
                  <MapIcon size={18} />
                </GameNavButton>
                <GameNavButton active={activeView === 'items'} label="Items" onClick={() => openView('items')}>
                  <ScrollText size={18} />
                </GameNavButton>
                <GameNavButton active={activeView === 'inventory'} label="Inventory" onClick={() => openView('inventory')}>
                  <Backpack size={18} />
                </GameNavButton>
                <GameNavButton active={activeView === 'character'} label="Character" onClick={() => openView('character')}>
                  <UserRound size={18} />
                </GameNavButton>
                <GameNavButton active={activeView === 'settings'} label="Settings" onClick={() => openView('settings')}>
                  <Settings2 size={18} />
                </GameNavButton>
              </nav>
            </div>
          </div>
        )}
      </div>
    </section>
  )
}
