import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Backpack, Compass, Eye, Locate, LogOut, Map as MapIcon, Maximize2, Minimize2, ScrollText, Settings2, UserRound, ZoomIn, ZoomOut } from 'lucide-react'
import {
  mudApi,
  type MudArchetype,
  type MudCharacterSummary,
  type MudChatMessageView,
  type MudChatSyncView,
  type MudCommandResult,
  type MudCompanionSettings,
  type MudItemView,
  type MudMapExitView,
  type MudMapRoomView,
  type MudRealmCharacterView,
  type MudRoomState,
  type MudRosterView,
  type MudStatRoll,
  type MudStats,
} from '../api/mudApi'
import { uploadToS3 } from '../api/mediaApi'
import { useAuth } from '../contexts/AuthContext'
import { THEMES, useTheme } from '../contexts/ThemeContext'

const STAT_KEYS: (keyof MudStats)[] = ['presence', 'wit', 'resolve', 'lore', 'craft', 'guile']
const STAT_LABELS: Record<keyof MudStats, string> = {
  presence: 'Presence', wit: 'Wit', resolve: 'Resolve', lore: 'Lore', craft: 'Craft', guile: 'Guile',
}

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
  world: [
    { id: 'room', label: 'Room' },
    { id: 'chat', label: 'Chat' },
    { id: 'realm', label: 'Realm' },
  ],
  items: [
    { id: 'take', label: 'Take' },
    { id: 'inspect', label: 'Inspect' },
  ],
  character: [
    { id: 'stats', label: 'Stats' },
    { id: 'roster', label: 'Roster' },
  ],
}

const CHAT_TAGS: Record<string, string> = {
  room: 'say',
  shout: 'shout',
  whisper: 'whisper',
  group: 'party',
  announce: 'notice',
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

const MAP_CELL_SIZE = 130
const MAP_PADDING = 70

const MAP_ZOOM_MIN = 0.5
const MAP_ZOOM_MAX = 2.5
const MAP_ZOOM_STEP = 0.25

function ZoneMapPanel({ rooms, exits, onJump }: { rooms: MudMapRoomView[]; exits: MudMapExitView[]; onJump: (command: string) => void }) {
  const mapContainerRef = useRef<HTMLDivElement | null>(null)
  const [zoom, setZoom] = useState(1)
  const dragState = useRef<{ startX: number; startY: number; scrollLeft: number; scrollTop: number; dragged: boolean } | null>(null)
  const justDraggedRef = useRef(false)

  const xs = rooms.map(room => room.x)
  const ys = rooms.map(room => room.y)
  const minX = rooms.length ? Math.min(...xs) : 0
  const maxX = rooms.length ? Math.max(...xs) : 0
  const minY = rooms.length ? Math.min(...ys) : 0
  const maxY = rooms.length ? Math.max(...ys) : 0
  const gridWidth = Math.max(1, maxX - minX)
  const gridHeight = Math.max(1, maxY - minY)
  const canvasWidth = gridWidth * MAP_CELL_SIZE + MAP_PADDING * 2
  const canvasHeight = gridHeight * MAP_CELL_SIZE + MAP_PADDING * 2
  const positionOf = (room: MudMapRoomView) => {
    const leftPx = MAP_PADDING + (room.x - minX) * MAP_CELL_SIZE
    const topPx = MAP_PADDING + (room.y - minY) * MAP_CELL_SIZE
    return { left: `${leftPx}px`, top: `${topPx}px`, leftPx, topPx }
  }
  const roomPositions = Object.fromEntries(rooms.map(room => [room.roomId, positionOf(room)]))
  const currentRoom = rooms.find(room => room.current) ?? rooms[0]
  const uniqueExitTypes = Array.from(new Set(exits.map(exit => exit.exitType)))

  const centerOnCurrentRoom = useCallback((behavior: ScrollBehavior = 'auto') => {
    const container = mapContainerRef.current
    const pos = currentRoom ? roomPositions[currentRoom.roomId] : undefined
    if (!container || !pos) return
    container.scrollTo({
      left: Math.max(0, pos.leftPx * zoom - container.clientWidth / 2),
      top: Math.max(0, pos.topPx * zoom - container.clientHeight / 2),
      behavior,
    })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentRoom?.roomId, zoom])

  useEffect(() => {
    centerOnCurrentRoom('auto')
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentRoom?.roomId, rooms.length])

  const applyZoom = (next: number) => {
    const container = mapContainerRef.current
    const clamped = Math.min(MAP_ZOOM_MAX, Math.max(MAP_ZOOM_MIN, next))
    if (!container) { setZoom(clamped); return }
    // Keep the point currently at the viewport center under the same
    // screen position after the scale changes, instead of snapping to
    // the canvas origin.
    const centerX = container.scrollLeft + container.clientWidth / 2
    const centerY = container.scrollTop + container.clientHeight / 2
    const ratio = clamped / zoom
    setZoom(clamped)
    requestAnimationFrame(() => {
      if (!mapContainerRef.current) return
      mapContainerRef.current.scrollTo({
        left: centerX * ratio - mapContainerRef.current.clientWidth / 2,
        top: centerY * ratio - mapContainerRef.current.clientHeight / 2,
      })
    })
  }

  const handlePointerDown = (event: React.PointerEvent<HTMLDivElement>) => {
    if (event.pointerType !== 'mouse' || event.button !== 0) return
    const container = mapContainerRef.current
    if (!container) return
    dragState.current = {
      startX: event.clientX,
      startY: event.clientY,
      scrollLeft: container.scrollLeft,
      scrollTop: container.scrollTop,
      dragged: false,
    }
    container.setPointerCapture(event.pointerId)
  }

  const handlePointerMove = (event: React.PointerEvent<HTMLDivElement>) => {
    const drag = dragState.current
    const container = mapContainerRef.current
    if (!drag || !container) return
    const dx = event.clientX - drag.startX
    const dy = event.clientY - drag.startY
    if (Math.abs(dx) > 3 || Math.abs(dy) > 3) drag.dragged = true
    container.scrollLeft = drag.scrollLeft - dx
    container.scrollTop = drag.scrollTop - dy
  }

  const handlePointerUp = (event: React.PointerEvent<HTMLDivElement>) => {
    const container = mapContainerRef.current
    if (container && container.hasPointerCapture(event.pointerId)) {
      container.releasePointerCapture(event.pointerId)
    }
    justDraggedRef.current = dragState.current?.dragged ?? false
    dragState.current = null
  }

  const handleRoomClick = (room: MudMapRoomView) => {
    if (justDraggedRef.current) {
      justDraggedRef.current = false
      return
    }
    if (room.current) onJump('look')
  }

  if (!rooms.length) return <p className="mud-empty">No map data yet.</p>

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
      <div className="mud-map-viewport">
      <div
        className="mud-map mud-map-enhanced"
        ref={mapContainerRef}
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={handlePointerUp}
        onPointerLeave={handlePointerUp}
      >
        <div
          className="mud-map-scale-wrapper"
          style={{ width: `${canvasWidth * zoom}px`, height: `${canvasHeight * zoom}px` }}
        >
          <div
            className="mud-map-canvas"
            style={{ width: `${canvasWidth}px`, height: `${canvasHeight}px`, transform: `scale(${zoom})` }}
          >
            <svg
              className="mud-map-lines"
              viewBox={`0 0 ${canvasWidth} ${canvasHeight}`}
              preserveAspectRatio="none"
              aria-hidden="true"
            >
              {exits.map(exit => {
                const from = roomPositions[exit.fromRoomId]
                const to = roomPositions[exit.toRoomId]
                if (!from || !to) return null
                return (
                  <line
                    key={`${exit.fromRoomId}-${exit.toRoomId}-${exit.direction}`}
                    className={`mud-map-line exit-${exit.exitType}`}
                    x1={from.leftPx}
                    y1={from.topPx}
                    x2={to.leftPx}
                    y2={to.topPx}
                  />
                )
              })}
            </svg>
            {rooms.map(room => {
              const pos = roomPositions[room.roomId]
              return (
                <button
                  key={room.roomId}
                  className={`mud-map-room${room.current ? ' current' : ''}${!room.visited ? ' unvisited' : ''}`}
                  style={{ left: pos.left, top: pos.top }}
                  onClick={() => handleRoomClick(room)}
                >
                  <strong>{room.roomName}</strong>
                  <small>{room.current ? 'You are here' : room.visited ? 'Mapped room' : 'Unexplored (admin view)'}</small>
                </button>
              )
            })}
          </div>
        </div>
      </div>
        <div className="mud-map-zoom-controls">
          <button type="button" onClick={() => applyZoom(zoom - MAP_ZOOM_STEP)} disabled={zoom <= MAP_ZOOM_MIN} aria-label="Zoom out">
            <ZoomOut size={16} />
          </button>
          <button type="button" onClick={() => applyZoom(1)} className="mud-map-zoom-reset" aria-label="Reset zoom">
            {Math.round(zoom * 100)}%
          </button>
          <button type="button" onClick={() => applyZoom(zoom + MAP_ZOOM_STEP)} disabled={zoom >= MAP_ZOOM_MAX} aria-label="Zoom in">
            <ZoomIn size={16} />
          </button>
          <button type="button" onClick={() => centerOnCurrentRoom('smooth')} aria-label="Center on current room">
            <Locate size={16} />
          </button>
        </div>
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
      {'currencyBalance' in character && (
        <div className="mud-pill-grid">
          <span className="mud-stat-pill currency">{character.currencyBalance} {character.currencyNamePlural}</span>
        </div>
      )}
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
  const [subView, setSubView] = useState('room')
  const [chat, setChat] = useState<MudChatSyncView | null>(null)
  const [chatMessages, setChatMessages] = useState<MudChatMessageView[]>([])
  const [chatInput, setChatInput] = useState('')
  const [chatChannel, setChatChannel] = useState('room')
  const [chatNotice, setChatNotice] = useState<string | null>(null)
  const chatSinceRef = useRef<string | null>(null)
  const chatFeedRef = useRef<HTMLDivElement | null>(null)
  const [realmRoster, setRealmRoster] = useState<MudRealmCharacterView[] | null>(null)
  const [realmRosterLoading, setRealmRosterLoading] = useState(false)
  const [createRealm, setCreateRealm] = useState('medieval')
  const [createName, setCreateName] = useState('')
  const [createDisplayName, setCreateDisplayName] = useState('')
  const [createBio, setCreateBio] = useState('')
  const [archetypes, setArchetypes] = useState<MudArchetype[]>([])
  const [createArchetype, setCreateArchetype] = useState('')
  const [statRoll, setStatRoll] = useState<MudStatRoll | null>(null)
  const [rolling, setRolling] = useState(false)
  const [statAllocation, setStatAllocation] = useState<MudStats>({
    presence: 0, wit: 0, resolve: 0, lore: 0, craft: 0, guile: 0,
  })
  const [portraitUploading, setPortraitUploading] = useState<string | null>(null)
  const [bioDraftByCharacter, setBioDraftByCharacter] = useState<Record<string, string>>({})
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
    mudApi.getArchetypes().then(setArchetypes).catch(() => setArchetypes([]))
  }, [])

  const realmArchetypes = useMemo(
    () => archetypes.filter(archetype => archetype.realmSlug === createRealm),
    [archetypes, createRealm],
  )

  useEffect(() => {
    if (realmArchetypes.length && !realmArchetypes.some(archetype => archetype.slug === createArchetype)) {
      setCreateArchetype(realmArchetypes[0].slug)
    }
  }, [realmArchetypes, createArchetype])

  const bonusPool = statRoll?.bonusPool ?? 0
  const statPointsUsed = STAT_KEYS.reduce((sum, key) => sum + statAllocation[key], 0)
  const statPointsRemaining = bonusPool - statPointsUsed

  const adjustStat = (key: keyof MudStats, delta: number) => {
    setStatAllocation(current => {
      const nextValue = current[key] + delta
      if (nextValue < 0) return current
      const nextTotal = statPointsUsed - current[key] + nextValue
      if (nextTotal > bonusPool) return current
      return { ...current, [key]: nextValue }
    })
  }

  const handleRollStats = async () => {
    setRolling(true)
    try {
      const roll = await mudApi.rollStats()
      setStatRoll(roll)
      setStatAllocation({ presence: 0, wit: 0, resolve: 0, lore: 0, craft: 0, guile: 0 })
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Could not roll stats.')
    } finally {
      setRolling(false)
    }
  }

  const openView = (view: GameView) => {
    setActiveView(view)
    setSubView(SUB_VIEWS[view]?.[0]?.id ?? '')
  }

  useEffect(() => {
    if (state) openView('world')
  }, [state?.characterId])

  const syncChat = useCallback(async () => {
    try {
      const view = await mudApi.chatSync(chatSinceRef.current ?? undefined)
      if (!view) return
      chatSinceRef.current = view.serverTime
      setChat(view)
      if (view.messages.length) {
        setChatMessages(current => {
          const seen = new Set(current.map(item => item.id))
          const merged = [...current, ...view.messages.filter(item => !seen.has(item.id))]
          return merged.slice(-100)
        })
      }
    } catch {
      // polling errors are transient; the next tick retries
    }
  }, [])

  useEffect(() => {
    if (!state?.characterId) return
    void syncChat()
    const interval = setInterval(() => { void syncChat() }, 7000)
    return () => clearInterval(interval)
  }, [state?.characterId, state?.roomId, syncChat])

  useEffect(() => {
    const feed = chatFeedRef.current
    if (feed) feed.scrollTop = feed.scrollHeight
  }, [chatMessages])

  useEffect(() => {
    if (activeView !== 'world' || subView !== 'realm' || !state?.realmSlug) return
    let cancelled = false
    setRealmRosterLoading(true)
    mudApi.getRealmRoster(state.realmSlug)
      .then(roster => { if (!cancelled) setRealmRoster(roster) })
      .catch(() => { if (!cancelled) setRealmRoster([]) })
      .finally(() => { if (!cancelled) setRealmRosterLoading(false) })
    return () => { cancelled = true }
  }, [activeView, subView, state?.realmSlug])

  const sendChat = async (event: React.FormEvent) => {
    event.preventDefault()
    const raw = chatInput.trim()
    if (!raw) return
    let channel = chatChannel
    let target: string | undefined
    let text = raw
    if (raw.toLowerCase().startsWith('/w ')) {
      const parts = raw.slice(3).trim().split(/\s+/)
      target = parts[0]
      text = parts.slice(1).join(' ')
      channel = 'whisper'
    }
    setChatNotice(null)
    try {
      await mudApi.chatPost({ channel, text, target })
      setChatInput('')
      await syncChat()
    } catch (error) {
      setChatNotice(error instanceof Error ? error.message : 'That message did not send.')
    }
  }

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
    if (!createName.trim() || !createArchetype || !statRoll || statPointsRemaining !== 0) return
    setBusy(true)
    try {
      const nextRoster = await mudApi.createCharacter({
        realmSlug: createRealm,
        name: createName.trim(),
        displayName: createDisplayName.trim() || undefined,
        archetypeSlug: createArchetype,
        bio: createBio.trim() || undefined,
        presence: statAllocation.presence,
        wit: statAllocation.wit,
        resolve: statAllocation.resolve,
        lore: statAllocation.lore,
        craft: statAllocation.craft,
        guile: statAllocation.guile,
      })
      setRoster(nextRoster)
      const nextState = await mudApi.getMe()
      setState(nextState)
      setMessage(`Character created in ${createRealm}.`)
      setCreateName('')
      setCreateDisplayName('')
      setCreateBio('')
      setStatRoll(null)
      setStatAllocation({ presence: 0, wit: 0, resolve: 0, lore: 0, craft: 0, guile: 0 })
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Could not create that character.')
    } finally {
      setBusy(false)
    }
  }

  const handleUploadPortrait = async (character: MudCharacterSummary, file: File) => {
    setPortraitUploading(character.id)
    try {
      const media = await uploadToS3(file, 'mud-character-portrait', character.id)
      const nextRoster = await mudApi.updateCharacterPortrait(character.id, media.url)
      setRoster(nextRoster)
      const nextState = await mudApi.getMe()
      setState(nextState)
      setMessage(`Portrait updated for ${character.displayName}.`)
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Could not upload that portrait.')
    } finally {
      setPortraitUploading(null)
    }
  }

  const handleSaveBio = async (character: MudCharacterSummary) => {
    const bio = bioDraftByCharacter[character.id] ?? character.bio ?? ''
    setBusy(true)
    try {
      const nextRoster = await mudApi.updateCharacterBio(character.id, bio)
      setRoster(nextRoster)
      const nextState = await mudApi.getMe()
      setState(nextState)
      setMessage(`Bio updated for ${character.displayName}.`)
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Could not save that bio.')
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

  const handleBackToRoster = async () => {
    setBusy(true)
    try {
      const nextRoster = await mudApi.getRoster()
      setRoster(nextRoster)
      setState(null)
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Could not load the roster.')
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

  const renderRosterCard = (character: MudCharacterSummary, playLabel: string) => {
    const archetype = archetypes.find(a => a.slug === character.archetypeSlug)
    const bioDraft = bioDraftByCharacter[character.id] ?? character.bio ?? ''
    return (
      <div key={character.id} className={`mud-roster-card${character.isSelected ? ' selected' : ''}`}>
        <div className="mud-roster-head">
          <div className="mud-roster-identity">
            {character.portraitUrl ? (
              <img className="mud-roster-portrait" src={character.portraitUrl} alt={`${character.displayName} portrait`} />
            ) : (
              <div className="mud-roster-portrait mud-roster-portrait-placeholder">
                <UserRound size={22} />
              </div>
            )}
            <div>
              <strong>{character.displayName}</strong>
              {character.title && <span className="mud-status-pill ok" style={{ marginLeft: 8 }}>{character.title}</span>}
              <div className="mud-empty">
                {character.realmName} · {character.currentRoomName}
                {archetype && ` · ${archetype.name}`}
              </div>
            </div>
          </div>
          <div className="mud-meta">
            <span>{character.mudTierName}</span>
            <span>{character.inventoryCount} items</span>
          </div>
        </div>
        <StatPills character={character} />
        {character.bio && <p className="mud-roster-bio">{character.bio}</p>}
        <div className="mud-form-grid" style={{ marginTop: 12 }}>
          <textarea
            className="mud-command-input"
            value={bioDraft}
            onChange={e => setBioDraftByCharacter(current => ({ ...current, [character.id]: e.target.value }))}
            placeholder="Character bio"
            rows={2}
            disabled={busy}
          />
          <div className="mud-quick-commands">
            <button className="mud-quick-chip" onClick={() => void handleSaveBio(character)} disabled={busy}>
              Save bio
            </button>
            <label className="mud-quick-chip" style={{ cursor: 'pointer' }}>
              {portraitUploading === character.id ? 'Uploading…' : 'Upload portrait'}
              <input
                type="file"
                accept="image/*"
                style={{ display: 'none' }}
                disabled={portraitUploading === character.id}
                onChange={e => {
                  const file = e.target.files?.[0]
                  e.target.value = ''
                  if (file) void handleUploadPortrait(character, file)
                }}
              />
            </label>
          </div>
        </div>
        <div className="mud-quick-commands" style={{ marginTop: 12 }}>
          {!character.isSelected && (
            <button className="mud-quick-chip" onClick={() => void handleSelectCharacter(character.id)} disabled={busy}>
              {playLabel}
            </button>
          )}
          <button className="mud-quick-chip danger" onClick={() => void handleDeleteCharacter(character)} disabled={busy}>
            Delete
          </button>
        </div>
      </div>
    )
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
                {chat ? ` · ${chat.onlineCount} online` : ''}
              </span>
            </div>
            <div className="mud-game-header-actions">
              <button
                className="mud-icon-btn"
                onClick={() => void handleBackToRoster()}
                disabled={busy}
                title="Switch character or realm"
                aria-label="Switch character or realm"
              >
                <UserRound size={16} />
              </button>
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
                    {roster.characters.map(character => renderRosterCard(character, 'Play'))}
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

                  <label className="mud-field-label">
                    Archetype
                    <select
                      className="mud-command-input"
                      value={createArchetype}
                      onChange={e => setCreateArchetype(e.target.value)}
                      disabled={busy || !realmArchetypes.length}
                    >
                      {realmArchetypes.map(archetype => (
                        <option key={archetype.slug} value={archetype.slug}>{archetype.name}</option>
                      ))}
                    </select>
                  </label>
                  {realmArchetypes.find(archetype => archetype.slug === createArchetype) && (
                    <p className="mud-empty">
                      {realmArchetypes.find(archetype => archetype.slug === createArchetype)?.description}
                      {' '}Starts with: {realmArchetypes.find(archetype => archetype.slug === createArchetype)?.starterItemName}.
                    </p>
                  )}

                  <textarea
                    className="mud-command-input"
                    value={createBio}
                    onChange={e => setCreateBio(e.target.value)}
                    placeholder="Character bio (optional)"
                    rows={3}
                    disabled={busy}
                  />

                  <div>
                    <button
                      type="button"
                      className="mud-command-btn"
                      onClick={() => void handleRollStats()}
                      disabled={busy || rolling}
                    >
                      {rolling ? 'Rolling…' : statRoll ? 'Reroll stats' : 'Roll stats'}
                    </button>
                    <p className="mud-empty" style={{ marginTop: 8 }}>
                      Each stat and your bonus point pool are rolled as 3d6 (3-18). Once you create
                      the character the roll locks in — reroll before then if you want a new draw.
                    </p>
                  </div>

                  {statRoll && (
                    <>
                      <div className="mud-pill-grid">
                        {STAT_KEYS.map(key => (
                          <span key={key} className="mud-stat-pill">{STAT_LABELS[key]}: {statRoll.stats[key]}</span>
                        ))}
                      </div>

                      <div>
                        <div className="mud-meta" style={{ marginBottom: 8 }}>
                          <span>Bonus points remaining: {statPointsRemaining} / {bonusPool}</span>
                        </div>
                        <div className="mud-stat-allocator">
                          {STAT_KEYS.map(key => (
                            <div key={key} className="mud-stat-allocator-row">
                              <span className="mud-stat-allocator-label">{STAT_LABELS[key]}</span>
                              <button
                                type="button"
                                className="mud-quick-chip"
                                onClick={() => adjustStat(key, -1)}
                                disabled={busy || statAllocation[key] <= 0}
                              >
                                −
                              </button>
                              <span className="mud-stat-allocator-value">{statAllocation[key]}</span>
                              <button
                                type="button"
                                className="mud-quick-chip"
                                onClick={() => adjustStat(key, 1)}
                                disabled={busy || statPointsRemaining <= 0}
                              >
                                +
                              </button>
                            </div>
                          ))}
                        </div>
                      </div>
                    </>
                  )}

                  <button
                    className="mud-command-btn"
                    onClick={() => void handleCreateCharacter()}
                    disabled={busy || !createName.trim() || !createArchetype || !statRoll || statPointsRemaining !== 0}
                  >
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
              {activeView === 'world' && subView !== 'chat' && (
                <div className="mud-game-view">
                  <div className="mud-card mud-card-flat">
                    <div className="mud-room-text">{roomBody}</div>
                    <p className="mud-presence-line">
                      {chat && chat.here.length > 0
                        ? `Also here: ${chat.here.join(', ')}`
                        : 'No one else is here.'}
                    </p>
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

              {activeView === 'world' && subView === 'chat' && (
                <div className="mud-game-view">
                  <div className="mud-card mud-card-flat mud-chat-card">
                    <div className="mud-chat-presence">
                      {chat && chat.here.length > 0
                        ? `Also here: ${chat.here.join(', ')}`
                        : 'No one else is nearby.'}
                      {chat && ` · ${chat.onlineCount} online`}
                      {chat?.partyName && ` · party: ${chat.partyName}`}
                    </div>
                    <div className="mud-chat-feed" ref={chatFeedRef}>
                      {chatMessages.length === 0 && (
                        <p className="mud-empty">Nothing said yet. Speak up — the room is listening.</p>
                      )}
                      {chatMessages.map(item => (
                        <div key={item.id} className={`mud-chat-line ${item.channel}${item.self ? ' self' : ''}`}>
                          <span className="mud-chat-tag">{CHAT_TAGS[item.channel] ?? item.channel}</span>{' '}
                          <strong>{item.senderName}</strong>
                          {item.channel === 'whisper' && item.recipientName ? ` → ${item.recipientName}` : ''}
                          {': '}
                          {item.body}
                        </div>
                      ))}
                    </div>
                    <div className="mud-game-subnav mud-chat-channels">
                      {['room', 'shout', 'group'].map(channel => (
                        <button
                          key={channel}
                          type="button"
                          className={`mud-subnav-btn${chatChannel === channel ? ' active' : ''}`}
                          onClick={() => setChatChannel(channel)}
                        >
                          {channel === 'room' ? 'Say' : channel === 'shout' ? 'Shout' : 'Party'}
                        </button>
                      ))}
                      {user?.role === 'admin' && (
                        <button
                          type="button"
                          className={`mud-subnav-btn${chatChannel === 'announce' ? ' active' : ''}`}
                          onClick={() => setChatChannel('announce')}
                        >
                          Announce
                        </button>
                      )}
                    </div>
                    <form className="mud-command-row" onSubmit={sendChat}>
                      <input
                        className="mud-command-input"
                        value={chatInput}
                        onChange={e => setChatInput(e.target.value)}
                        placeholder="Message — or /w <name> <text> to whisper"
                        disabled={!user}
                      />
                      <button className="mud-command-btn" type="submit" disabled={!user || !chatInput.trim()}>
                        Send
                      </button>
                    </form>
                    {chatNotice && <p className="mud-empty">{chatNotice}</p>}
                  </div>
                </div>
              )}

              {activeView === 'world' && subView === 'realm' && (
                <div className="mud-game-view">
                  <div className="mud-card mud-card-flat">
                    <h2>Active in {state?.realmName}</h2>
                    {realmRosterLoading && <p className="mud-empty">Loading…</p>}
                    {!realmRosterLoading && realmRoster && realmRoster.length === 0 && (
                      <p className="mud-empty">No one else is active in this realm right now.</p>
                    )}
                    {!realmRosterLoading && realmRoster && realmRoster.length > 0 && (
                      <div className="mud-roster-list">
                        {realmRoster.map(character => (
                          <div key={character.characterId} className={`mud-roster-card${character.isSelf ? ' selected' : ''}`}>
                            <div className="mud-roster-head">
                              <div className="mud-roster-identity">
                                {character.portraitUrl ? (
                                  <img className="mud-roster-portrait" src={character.portraitUrl} alt={`${character.displayName} portrait`} />
                                ) : (
                                  <div className="mud-roster-portrait mud-roster-portrait-placeholder">
                                    <UserRound size={22} />
                                  </div>
                                )}
                                <div>
                                  <strong>{character.displayName}</strong>
                                  {character.isSelf && <span className="mud-status-pill ok" style={{ marginLeft: 8 }}>You</span>}
                                  {character.title && <span className="mud-status-pill ok" style={{ marginLeft: 8 }}>{character.title}</span>}
                                  <div className="mud-empty">{character.currentRoomName}</div>
                                </div>
                              </div>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
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
                    {subView !== 'roster' && (
                      <>
                        {(selectedCharacter ?? state) && (
                          <div className="mud-roster-head" style={{ marginBottom: 12 }}>
                            <div className="mud-roster-identity">
                              {(() => {
                                const sheet = selectedCharacter ?? state
                                const portraitUrl = sheet && 'portraitUrl' in sheet ? sheet.portraitUrl : undefined
                                const title = sheet && 'title' in sheet ? sheet.title : undefined
                                const name = sheet && 'displayName' in sheet ? sheet.displayName : sheet?.characterName
                                return (
                                  <>
                                    {portraitUrl ? (
                                      <img className="mud-roster-portrait" src={portraitUrl} alt={`${name} portrait`} />
                                    ) : (
                                      <div className="mud-roster-portrait mud-roster-portrait-placeholder">
                                        <UserRound size={22} />
                                      </div>
                                    )}
                                    <div>
                                      <strong>{name}</strong>
                                      {title && <span className="mud-status-pill ok" style={{ marginLeft: 8 }}>{title}</span>}
                                    </div>
                                  </>
                                )
                              })()}
                            </div>
                          </div>
                        )}
                        {selectedCharacter?.bio && <p className="mud-roster-bio">{selectedCharacter.bio}</p>}
                        <StatPills character={selectedCharacter ?? state} />
                      </>
                    )}
                    {subView === 'roster' && roster && (
                      <div className="mud-roster-list mud-roster-inline">
                        {roster.characters.map(character => renderRosterCard(character, 'Switch'))}
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
