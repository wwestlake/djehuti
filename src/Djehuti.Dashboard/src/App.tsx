import { useEffect, useMemo, useState } from 'react'
import logoUrl from '/logo.png'
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
} from '@tanstack/react-table'
import type { ColumnDef } from '@tanstack/react-table'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { FeatureExplorer } from './components/charts/FeatureExplorer'
import { MetricTimelines } from './components/charts/MetricTimelines'
import { PhaseSpace3D } from './components/charts/PhaseSpace3D'
import { VelocityChart } from './components/charts/VelocityChart'
import { MetricTile } from './components/MetricTile'
import { LoginModal } from './components/auth/LoginModal'
import { SignupModal } from './components/auth/SignupModal'
import { ForgotPasswordModal } from './components/auth/ForgotPasswordModal'
import { UserMenu } from './components/auth/UserMenu'
import {
  Activity,
  AlertTriangle,
  BarChart3,
  Bot,
  Braces,
  ChevronRight,
  Database,
  FileText,
  FileJson,
  Gauge,
  GraduationCap,
  KeyRound,
  Menu,
  MessageSquare,
  Moon,
  PanelRightClose,
  PanelRightOpen,
  Play,
  Route,
  Send,
  Settings,
  Sun,
  Table2,
  Trash2,
  Users,
  X,
} from 'lucide-react'
import { analyzeDatasetJson } from './api/djehutiApi'
import { maxVisibleRows, visualizationIdeas } from './config/dashboard'
import { askLiveProvider, deriveLiveWarnings, liveTurnsToDatasetJson } from './features/live/liveLab'
import { buildMlmceConfigPreview, validateMlmceConfig } from './features/mlmce/mlmceConfig'
import { apiBase } from './lib/apiBase'
import { formatNumber, readErrorMessage } from './lib/format'
import { parseTourFromResponse, startTour } from './lib/tour'
import type {
  AnalystMessage,
  AnalystResponse,
  AnalyzeResponse,
  AnalyzeView,
  AppMode,
  DataSetCatalogItem,
  LiveProviderConfig,
  LiveTurn,
  LiveWarning,
  MlmceParticipantConfig,
  MlmceSessionKind,
  MlmceThresholdConfig,
  MlmceTurnMode,
  TurnMetricDto,
} from './types'
import ForumPage from './pages/ForumPage'
import ForumForumPage from './pages/ForumForumPage'
import ForumThreadPage from './pages/ForumThreadPage'
import BlogPage from './pages/BlogPage'
import BlogArticlePage from './pages/BlogArticlePage'
import BlogEditorPage from './pages/BlogEditorPage'
import './App.css'

const sampleJson = `{
  "source": {
    "id": "dashboard-sample",
    "kind": "manual-transcript",
    "name": "10-turn troubleshooting replay"
  },
  "constants": {
    "distanceMetric": "cosine",
    "conversationType": "technical-troubleshooting",
    "turnCount": 10
  },
  "interactions": [
    {
      "sessionId": "sample-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 0,
      "prompt": "I need to design a resource routing pipeline for a distributed simulation. What is the most efficient way to handle continuous material flow between three distinct processing nodes?",
      "response": "For a continuous material flow across three nodes, a directed acyclic graph architecture using a message broker like Apache Kafka or RabbitMQ is highly efficient. You can treat each processing node as a consumer or producer, ensuring asynchronous transfer and buffering against node downtime."
    },
    {
      "sessionId": "sample-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 1,
      "prompt": "Let's avoid heavy message brokers. I want to handle this at the application layer using direct memory access or shared state if possible. The nodes are running on the same local cluster.",
      "response": "If the nodes are on the same local cluster and you want to avoid broker overhead, use shared memory with memory-mapped files or a distributed cache. Direct gRPC streaming between nodes is also a practical low-latency option."
    },
    {
      "sessionId": "sample-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 2,
      "prompt": "Let's go with gRPC streaming. Node A generates raw input, Node B filters it, and Node C aggregates it. Can you outline the gRPC service definitions for this?",
      "response": "You need a FilterService hosted by Node B that receives RawInput from Node A and emits FilteredData. Then Node B should act as a client to an AggregationService hosted by Node C, streaming the filtered output forward."
    },
    {
      "sessionId": "sample-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 3,
      "prompt": "Wait, if Node B is returning a stream to Node A, how does the data get to Node C? Node A shouldn't be responsible for passing it along.",
      "response": "You are right. Node A should only stream raw input to Node B. Node B is both a server receiving from A and a client streaming filtered results to Node C. Node C terminates the pipeline."
    },
    {
      "sessionId": "sample-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 4,
      "prompt": "Okay, write the protobuf file for the connection between Node B and Node C only.",
      "response": "service AggregationService { rpc StreamFilteredData (stream FilteredPayload) returns (AggregationAck); } message FilteredPayload { string item_id = 1; bytes processed_data = 2; int64 timestamp = 3; } message AggregationAck { bool success = 1; int32 items_processed = 2; }"
    },
    {
      "sessionId": "sample-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 5,
      "prompt": "Node C needs to return a continuous stream of the aggregate, not just a single acknowledgment. Change the return type to a stream.",
      "response": "service AggregationService { rpc StreamFilteredData (stream FilteredPayload) returns (stream AggregateUpdate); } message FilteredPayload { string item_id = 1; bytes processed_data = 2; int64 timestamp = 3; } message AggregateUpdate { double current_moving_average = 1; int64 latest_timestamp = 2; }"
    },
    {
      "sessionId": "sample-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 6,
      "prompt": "Actually, moving average won't work for this data type. It's categorical data, not numerical. How do we aggregate categories over a stream?",
      "response": "For categorical data, track frequency distributions over a sliding time window. AggregateUpdate should return a map of category strings to integer counts rather than numerical averages."
    },
    {
      "sessionId": "sample-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 7,
      "prompt": "Update the protobuf to reflect a frequency map.",
      "response": "message FilteredPayload { string item_id = 1; string category = 2; int64 timestamp = 3; } message AggregateUpdate { map<string, int32> category_frequencies = 1; int64 window_end_timestamp = 2; }"
    },
    {
      "sessionId": "sample-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 8,
      "prompt": "We need to ensure Node B doesn't overwhelm Node C. How do we implement backpressure in gRPC if the map gets too large?",
      "response": "gRPC uses HTTP/2 flow control for network-level backpressure. If Node C reads too slowly, the receive window fills and Node B's sending slows until Node C catches up."
    },
    {
      "sessionId": "sample-session",
      "modelId": "simulated-llm",
      "sequenceIndex": 9,
      "prompt": "So I don't need to write any custom application-level backpressure logic for the category map?",
      "response": "Usually no. Start with HTTP/2 flow control. Add application-level throttling only if Node C's memory management or category map growth creates pressure before the transport-level signal is enough."
    }
  ]
}`

const modeItems = [
  { id: 'analyze', label: 'Analyze', icon: Gauge },
  { id: 'live', label: 'Live Lab', icon: MessageSquare },
  { id: 'mlmce', label: 'MLMCE', icon: Users },
  { id: 'forum', label: 'Forum', icon: MessageSquare },
  { id: 'blog', label: 'Blog', icon: FileText },
  { id: 'reports', label: 'Reports', icon: FileText },
  { id: 'settings', label: 'Settings', icon: Settings },
] satisfies Array<{ id: AppMode; label: string; icon: typeof Gauge }>

const analyzeNavItems = [
  { id: 'overview', label: 'Run summary', icon: Gauge },
  { id: 'phase', label: '3D phase space', icon: Route },
  { id: 'timelines', label: 'Metric timelines', icon: BarChart3 },
  { id: 'features', label: 'Feature finder', icon: AlertTriangle },
  { id: 'data', label: 'Turn metrics', icon: Table2 },
  { id: 'input', label: 'JSON input', icon: Braces },
] satisfies Array<{ id: AnalyzeView; label: string; icon: typeof Gauge }>


function App() {
  const [showLoginModal, setShowLoginModal] = useState(false)
  const [showSignupModal, setShowSignupModal] = useState(false)
  const [showForgotPasswordModal, setShowForgotPasswordModal] = useState(false)
  const [datasetJson, setDatasetJson] = useState(sampleJson)
  const [catalog, setCatalog] = useState<DataSetCatalogItem[]>([])
  const [theme, setTheme] = useState<'light' | 'dark' | 'midnight'>(
    () => (localStorage.getItem('djehuti.theme') as 'light' | 'dark' | 'midnight') ?? 'dark'
  )
  const [selectedDataSetId, setSelectedDataSetId] = useState('')
  const [isRenamingDataSet, setIsRenamingDataSet] = useState(false)
  const [renameDataSetName, setRenameDataSetName] = useState('')
  const [renameDataSetDesc, setRenameDataSetDesc] = useState('')
  const [renameError, setRenameError] = useState<string | null>(null)
  const [isDeletingDataSet, setIsDeletingDataSet] = useState(false)
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const [analysis, setAnalysis] = useState<AnalyzeResponse | null>(null)
  const [selectedTurn, setSelectedTurn] = useState<TurnMetricDto | null>(null)
  const [isAnalyzing, setIsAnalyzing] = useState(false)
  const [isLoadingDataSet, setIsLoadingDataSet] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isMenuOpen, setIsMenuOpen] = useState(false)
  const [activeMode, setActiveMode] = useState<AppMode>('analyze')
  const [activeAnalyzeView, setActiveAnalyzeView] = useState<AnalyzeView>('overview')
  const [isToolsOpen, setIsToolsOpen] = useState(true)
  const [isToolsWide, setIsToolsWide] = useState(false)
  const [analystQuestion, setAnalystQuestion] = useState(
    'What are the most important measurement features in this run?',
  )
  const [analystMessages, setAnalystMessages] = useState<AnalystMessage[]>([])
  const [isAskingAnalyst, setIsAskingAnalyst] = useState(false)
  const [analystError, setAnalystError] = useState<string | null>(null)
  const [liveApiKey, setLiveApiKey] = useState(() => localStorage.getItem('djehuti.liveApiKey') ?? '')
  const [liveModel, setLiveModel] = useState(() => localStorage.getItem('djehuti.liveModel') ?? 'gpt-4.1-mini')
  const [liveEndpoint, setLiveEndpoint] = useState(() => localStorage.getItem('djehuti.liveEndpoint') ?? 'https://api.openai.com/v1/responses')
  const [livePrompt, setLivePrompt] = useState('')
  const [liveTurns, setLiveTurns] = useState<LiveTurn[]>(() => {
    try { return JSON.parse(localStorage.getItem('djehuti.liveTurns') ?? '[]') as LiveTurn[] }
    catch { return [] }
  })
  const [liveWarnings, setLiveWarnings] = useState<LiveWarning[]>(() => {
    try { return JSON.parse(localStorage.getItem('djehuti.liveWarnings') ?? '[]') as LiveWarning[] }
    catch { return [] }
  })
  const [liveWebSearch, setLiveWebSearch] = useState(false)
  const [isLiveSending, setIsLiveSending] = useState(false)
  const [showTourPrompt, setShowTourPrompt] = useState(false)
  const [tourQuestion, setTourQuestion] = useState('Walk me through ')
  const [liveError, setLiveError] = useState<string | null>(null)
  const [isSavingRun, setIsSavingRun] = useState(false)
  const [saveRunName, setSaveRunName] = useState('')
  const [saveRunError, setSaveRunError] = useState<string | null>(null)
  const [saveRunSuccess, setSaveRunSuccess] = useState<string | null>(null)
  const [mlmceParticipants, setMlmceParticipants] = useState<MlmceParticipantConfig[]>([
    { id: 'participant-a', roleLabel: 'Advocate', modelId: 'gpt-4.1-mini' },
    { id: 'participant-b', roleLabel: 'Critic', modelId: 'gpt-4.1-mini' },
  ])
  const [mlmceModeratorModel, setMlmceModeratorModel] = useState('gpt-4.1')
  const [mlmceModeratorProfileVersion, setMlmceModeratorProfileVersion] = useState('1')
  const [mlmceTurnMode, setMlmceTurnMode] = useState<MlmceTurnMode>('sequential')
  const [mlmceSessionKind, setMlmceSessionKind] =
    useState<MlmceSessionKind>('sequential-dialogue')
  const [mlmceSeedPrompt, setMlmceSeedPrompt] = useState(
    'Discuss whether measurement changes observable model behavior.',
  )
  const [forumView, setForumView] = useState<{ page: 'home' | 'forum' | 'thread'; id?: string }>({ page: 'home' })
  const [blogView, setBlogView] = useState<{ page: 'list' | 'article' | 'editor'; slug?: string; articleId?: string }>({ page: 'list' })

  const [mlmceThresholds, setMlmceThresholds] = useState<MlmceThresholdConfig>({
    stabilityCriterionMargin: 0.1,
    leakageBudgetFraction: 0.8,
    torsionalAccumulationCeiling: 1,
    attractorWindow: 3,
    divergenceThreshold: 0.25,
  })

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme)
    localStorage.setItem('djehuti.theme', theme)
  }, [theme])

  useEffect(() => { localStorage.setItem('djehuti.liveApiKey', liveApiKey) }, [liveApiKey])
  useEffect(() => { localStorage.setItem('djehuti.liveModel', liveModel) }, [liveModel])
  useEffect(() => { localStorage.setItem('djehuti.liveEndpoint', liveEndpoint) }, [liveEndpoint])
  useEffect(() => { localStorage.setItem('djehuti.liveTurns', JSON.stringify(liveTurns)) }, [liveTurns])
  useEffect(() => { localStorage.setItem('djehuti.liveWarnings', JSON.stringify(liveWarnings)) }, [liveWarnings])

  const closeMenu = () => setIsMenuOpen(false)

  const updateMlmceParticipant = (
    index: number,
    field: keyof MlmceParticipantConfig,
    value: string,
  ) => {
    setMlmceParticipants((participants) =>
      participants.map((participant, participantIndex) =>
        participantIndex === index ? { ...participant, [field]: value } : participant,
      ),
    )
  }

  const addMlmceParticipant = () => {
    setMlmceParticipants((participants) => [
      ...participants,
      {
        id: `participant-${String.fromCharCode(97 + participants.length)}`,
        roleLabel: 'Participant',
        modelId: participants[participants.length - 1]?.modelId || 'gpt-4.1-mini',
      },
    ])
  }

  const removeMlmceParticipant = (index: number) => {
    setMlmceParticipants((participants) =>
      participants.filter((_, participantIndex) => participantIndex !== index),
    )
  }

  const updateMlmceThreshold = (field: keyof MlmceThresholdConfig, value: string) => {
    const parsed = Number(value)
    setMlmceThresholds((thresholds) => ({
      ...thresholds,
      [field]: Number.isFinite(parsed) ? parsed : thresholds[field],
    }))
  }

  useEffect(() => {
    let ignore = false

    const loadCatalog = async () => {
      try {
        const response = await fetch(`${apiBase}/api/datasets`)
        if (!response.ok) {
          throw new Error(`Dataset catalog failed with ${response.status}`)
        }
        const result = (await response.json()) as DataSetCatalogItem[]
        if (!ignore) {
          setCatalog(result)
          setSelectedDataSetId((current) => current || result[0]?.id || '')
        }
      } catch (err) {
        if (!ignore) {
          setError(err instanceof Error ? err.message : 'Dataset catalog failed')
        }
      }
    }

    loadCatalog()

    return () => {
      ignore = true
    }
  }, [])

  const deleteSelectedDataSet = async () => {
    if (!selectedDataSetId) return
    if (!window.confirm(`Delete "${catalog.find(c => c.id === selectedDataSetId)?.name ?? selectedDataSetId}"? This cannot be undone.`)) return
    setIsDeletingDataSet(true)
    setDeleteError(null)
    try {
      const response = await fetch(`${apiBase}/api/datasets/${encodeURIComponent(selectedDataSetId)}`, { method: 'DELETE' })
      if (!response.ok) {
        const text = await response.text()
        throw new Error(text || `Delete failed with ${response.status}`)
      }
      const refreshed = await fetch(`${apiBase}/api/datasets`)
      if (refreshed.ok) {
        const items = await refreshed.json() as DataSetCatalogItem[]
        setCatalog(items)
        setSelectedDataSetId(items[0]?.id ?? '')
      }
      setIsRenamingDataSet(false)
    } catch (err) {
      setDeleteError(err instanceof Error ? err.message : 'Delete failed')
    } finally {
      setIsDeletingDataSet(false)
    }
  }

  const loadSelectedDataSet = async () => {
    if (!selectedDataSetId) {
      return
    }

    setIsLoadingDataSet(true)
    setError(null)

    try {
      const response = await fetch(`${apiBase}/api/datasets/${encodeURIComponent(selectedDataSetId)}`)
      if (!response.ok) {
        const message = await response.text()
        throw new Error(message || `Dataset load failed with ${response.status}`)
      }
      const json = await response.text()
      setDatasetJson(json)
      setAnalysis(null)
      setSelectedTurn(null)
      setAnalystMessages([])
      setAnalystError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Dataset load failed')
    } finally {
      setIsLoadingDataSet(false)
    }
  }

  const analyze = async () => {
    setIsAnalyzing(true)
    setError(null)

    const controller = new AbortController()
    const timeoutId = window.setTimeout(() => controller.abort(), 10000)

    try {
      const result = await analyzeDatasetJson(datasetJson, controller.signal)
      setAnalysis(result)
      setSelectedTurn(result.turns[0] ?? null)
      setAnalystMessages([])
      setAnalystError(null)
    } catch (err) {
      setError(
        err instanceof Error && err.name === 'AbortError'
          ? 'Analysis request timed out. Check that the API is running on http://localhost:5087.'
          : err instanceof Error
            ? err.message
            : 'Analysis failed',
      )
    } finally {
      window.clearTimeout(timeoutId)
      setIsAnalyzing(false)
    }
  }

  const resetLiveLab = () => {
    setLiveTurns([])
    setLiveWarnings([])
    setLivePrompt('')
    setLiveError(null)
    localStorage.removeItem('djehuti.liveTurns')
    localStorage.removeItem('djehuti.liveWarnings')
    const emptyJson = liveTurnsToDatasetJson([])
    setDatasetJson(emptyJson)
    setAnalysis(null)
    setSelectedTurn(null)
    setAnalystMessages([])
    setAnalystError(null)
  }

  const sendLivePrompt = async () => {
    const prompt = livePrompt.trim()
    const key = liveApiKey.trim()

    if (!prompt || !key) {
      return
    }

    setIsLiveSending(true)
    setLiveError(null)

    const controller = new AbortController()
    const timeoutId = window.setTimeout(() => controller.abort(), 120000)

    try {
      const config: LiveProviderConfig = {
        protocol: 'openai-responses',
        apiKey: key,
        model: liveModel.trim() || 'gpt-4.1-mini',
        endpoint: liveEndpoint.trim() || 'https://api.openai.com/v1/responses',
        webSearch: liveWebSearch,
      }

      const responseText = await askLiveProvider(
        config,
        liveTurns,
        prompt,
        controller.signal,
      )

      const nextTurns = [
        ...liveTurns,
        {
          sequenceIndex: liveTurns.length,
          prompt,
          response: responseText,
          modelId: config.model,
          webSearch: liveWebSearch,
        },
      ]
      const nextJson = liveTurnsToDatasetJson(nextTurns)
      const nextAnalysis = await analyzeDatasetJson(nextJson, controller.signal)
      const nextWarnings = deriveLiveWarnings(nextAnalysis)

      setLiveTurns(nextTurns)
      setDatasetJson(nextJson)
      setAnalysis(nextAnalysis)
      setSelectedTurn(nextAnalysis.turns[nextAnalysis.turns.length - 1] ?? null)
      setLiveWarnings((current) => {
        const known = new Set(current.map((warning) => warning.id))
        return [
          ...current,
          ...nextWarnings.filter((warning) => !known.has(warning.id)),
        ]
      })
      setLivePrompt('')
      setActiveMode('live')
    } catch (err) {
      setLiveError(
        err instanceof Error && err.name === 'AbortError'
          ? 'Live provider request timed out.'
          : err instanceof TypeError
            ? 'Browser could not reach the provider. This may be a provider CORS policy; the experiment key was not sent to the Djehuti server.'
            : err instanceof Error
              ? err.message
              : 'Live provider request failed',
      )
    } finally {
      window.clearTimeout(timeoutId)
      setIsLiveSending(false)
    }
  }

  const askAnalyst = async (overrideQuestion?: string) => {
    const question = (overrideQuestion ?? analystQuestion).trim()
    if (!analysis || !question) {
      return
    }

    setIsAskingAnalyst(true)
    setAnalystError(null)

    const controller = new AbortController()
    const timeoutId = window.setTimeout(() => controller.abort(), 95000)

    try {
      const response = await fetch(`${apiBase}/api/analyst/ask`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          datasetJson,
          question,
          temperature: 0.1,
          maxOutputTokens: 900,
        }),
        signal: controller.signal,
      })

      if (!response.ok) {
        const message = await readErrorMessage(response)
        throw new Error(message || `Analyst request failed with ${response.status}`)
      }

      const result = (await response.json()) as AnalystResponse
      const { tour, cleaned } = parseTourFromResponse(result.answer)
      const displayedResult = tour ? { ...result, answer: cleaned } : result
      setAnalystMessages((messages) => [
        {
          id: `${Date.now()}-${messages.length}`,
          question,
          response: displayedResult,
        },
        ...messages,
      ])
      setAnalystQuestion('')
      if (tour) startTour(tour)
    } catch (err) {
      setAnalystError(
        err instanceof Error && err.name === 'AbortError'
          ? 'Analyst request timed out. The API may still be processing or the provider may be unavailable.'
          : err instanceof Error
            ? err.message
            : 'Analyst request failed',
      )
    } finally {
      window.clearTimeout(timeoutId)
      setIsAskingAnalyst(false)
    }
  }

  const columns = useMemo<ColumnDef<TurnMetricDto>[]>(
    () => [
      {
        header: 't',
        accessorKey: 'sequenceIndex',
        size: 56,
      },
      {
        header: 'strategy',
        accessorKey: 'strategy',
      },
      {
        header: 'velocity',
        accessorKey: 'velocityFromPrevious',
        cell: ({ row }) => formatNumber(row.original.velocityFromPrevious),
      },
      {
        header: 'p-r cosine',
        accessorKey: 'promptResponseCosine',
        cell: ({ row }) => formatNumber(row.original.promptResponseCosine),
      },
      {
        header: 'jaccard',
        accessorKey: 'jaccardSimilarity',
        cell: ({ row }) => formatNumber(row.original.jaccardSimilarity),
      },
      {
        header: 'word delta',
        accessorKey: 'wordCountDelta',
      },
      {
        header: 'shared',
        accessorKey: 'sharedWordCount',
      },
      {
        header: 'state',
        accessorKey: 'contaminationDepth',
      },
    ],
    [],
  )

  const visibleTurns = useMemo(
    () => analysis?.turns.slice(0, maxVisibleRows) ?? [],
    [analysis],
  )

  const hiddenTurnCount = analysis
    ? Math.max(analysis.turns.length - visibleTurns.length, 0)
    : 0

  const table = useReactTable({
    data: visibleTurns,
    columns,
    getCoreRowModel: getCoreRowModel(),
  })

  const activeModeItem = modeItems.find((item) => item.id === activeMode) ?? modeItems[0]
  const activeAnalyzeNavItem =
    analyzeNavItems.find((item) => item.id === activeAnalyzeView) ?? analyzeNavItems[0]
  const pageTitle = activeMode === 'analyze' ? activeAnalyzeNavItem.label : activeModeItem.label
  const showToolsPanel = activeMode === 'analyze'

  const renderRunContext = () =>
    analysis ? (
      <section className="status-band">
        <div>
          <strong>{analysis.summary.sourceName}</strong>
          <span>{analysis.summary.sourceKind}</span>
          <span>{analysis.summary.sessionId}</span>
          <span>{analysis.summary.modelId}</span>
        </div>
        <div>
          {analysis.constants.map((constant) => (
            <span key={constant.name}>
              {constant.name}: {constant.value}
            </span>
          ))}
        </div>
        {analysis.warnings.length > 0 && (
          <div className="warning-list">
            {analysis.warnings.map((warning) => (
              <span key={warning}>{warning}</span>
            ))}
          </div>
        )}
      </section>
    ) : null

  const renderTurnTable = () => (
    <section className="table-panel view-panel">
      <div className="panel-heading">
        <BarChart3 size={18} />
        <h2>Turn metrics</h2>
      </div>
      {hiddenTurnCount > 0 && (
        <p className="render-note">
          Showing first {visibleTurns.length} rows from {analysis?.turns.length} turns.
        </p>
      )}
      <div className="table-wrap">
        <table>
          <thead>
            {table.getHeaderGroups().map((headerGroup) => (
              <tr key={headerGroup.id}>
                {headerGroup.headers.map((header) => (
                  <th key={header.id}>
                    {flexRender(
                      header.column.columnDef.header,
                      header.getContext(),
                    )}
                  </th>
                ))}
              </tr>
            ))}
          </thead>
          <tbody>
            {table.getRowModel().rows.map((row) => (
              <tr
                key={row.id}
                className={
                  selectedTurn?.sequenceIndex === row.original.sequenceIndex
                    ? 'selected'
                    : undefined
                }
                onClick={() => setSelectedTurn(row.original)}
              >
                {row.getVisibleCells().map((cell) => (
                  <td key={cell.id}>
                    {flexRender(
                      cell.column.columnDef.cell,
                      cell.getContext(),
                    )}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
        {!analysis && (
          <div className="empty-state">Run analysis to populate metrics.</div>
        )}
      </div>
    </section>
  )

  const renderLiveLab = () => (
    <section className="live-lab view-panel">
      <div className="live-config-panel">
        <div className="panel-heading">
          <KeyRound size={18} />
          <h2>Client-side provider</h2>
        </div>
        <div className="live-config-grid">
          <label>
            <span>Protocol</span>
            <select value="openai-responses" disabled aria-label="Live provider protocol">
              <option value="openai-responses">OpenAI Responses API</option>
            </select>
          </label>
          <label>
            <span>Model</span>
            <input
              value={liveModel}
              onChange={(event) => setLiveModel(event.target.value)}
              placeholder="gpt-4.1-mini"
            />
          </label>
          <label className="live-endpoint-field">
            <span>Endpoint</span>
            <input
              value={liveEndpoint}
              onChange={(event) => setLiveEndpoint(event.target.value)}
              placeholder="https://api.openai.com/v1/responses"
            />
          </label>
          <label className="live-key-field">
            <span>API key</span>
            <input
              value={liveApiKey}
              onChange={(event) => setLiveApiKey(event.target.value)}
              placeholder="Stored only in this browser session"
              type="password"
              autoComplete="off"
            />
          </label>
        </div>
        <div className="live-policy-strip">
          <span>Vanilla chat: no Djehuti system context is sent to the provider.</span>
          <span>Only prompt-response turns are analyzed by Djehuti after each reply.</span>
        </div>
      </div>

      <div className="live-work-grid">
        <section className="live-chat-panel">
          <div className="panel-heading">
            <MessageSquare size={18} />
            <h2>Live conversation</h2>
            <form
              className="live-save-form"
              data-tour="live-save-form"
              onSubmit={async (event) => {
                event.preventDefault()
                if (!liveTurns.length) return
                setIsSavingRun(true)
                setSaveRunError(null)
                setSaveRunSuccess(null)
                try {
                  const name = saveRunName.trim() || `Live Lab ${new Date().toLocaleString()}`
                  const response = await fetch(`${apiBase}/api/datasets`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                      name,
                      description: `Live Lab run - ${liveTurns.length} turns, model: ${liveModel.trim() || 'gpt-4.1-mini'}`,
                      sourceKind: 'live-lab',
                      turnCount: liveTurns.length,
                      datasetJson: liveTurnsToDatasetJson(liveTurns),
                    }),
                  })
                  if (!response.ok) {
                    const text = await response.text()
                    throw new Error(text || `Save failed with ${response.status}`)
                  }
                  const saved = await response.json() as { id: string; name: string }
                  setSaveRunSuccess(`Saved as "${saved.name}"`)
                  setSaveRunName('')
                  const refreshed = await fetch(`${apiBase}/api/datasets`)
                  if (refreshed.ok) {
                    const items = await refreshed.json() as DataSetCatalogItem[]
                    setCatalog(items)
                  }
                } catch (err) {
                  setSaveRunError(err instanceof Error ? err.message : 'Save failed')
                } finally {
                  setIsSavingRun(false)
                }
              }}
            >
              <input
                className="live-save-name"
                value={saveRunName}
                onChange={(e) => { setSaveRunName(e.target.value); setSaveRunSuccess(null) }}
                placeholder="Run name (optional)"
                disabled={isSavingRun || liveTurns.length === 0}
              />
              <button
                className="panel-heading-action"
                type="submit"
                disabled={isSavingRun || liveTurns.length === 0}
                title="Save conversation to dataset library"
              >
                <FileJson size={14} />
                {isSavingRun ? 'Saving...' : 'Save run'}
              </button>
            </form>
          </div>
          {saveRunSuccess && <p className="live-save-success">{saveRunSuccess}</p>}
          {saveRunError && <p className="error-line live-error">{saveRunError}</p>}
          <div className="live-transcript" data-tour="live-transcript">
            {liveTurns.length === 0 ? (
              <div className="empty-state">
                Start a vanilla provider conversation. Djehuti will observe each completed turn.
              </div>
            ) : (
              liveTurns.map((turn) => (
                <article className="live-turn" key={turn.sequenceIndex}>
                  <div className="live-turn-header">
                    <strong>t={turn.sequenceIndex}</strong>
                    <span>{turn.modelId}</span>
                    {turn.webSearch && <span className="live-search-badge"><Database size={11} /> web search</span>}
                  </div>
                  <div className="live-bubble live-prompt">
                    <span>Prompt</span>
                    <div className="live-markdown"><ReactMarkdown remarkPlugins={[remarkGfm]}>{turn.prompt}</ReactMarkdown></div>
                  </div>
                  <div className="live-bubble live-response">
                    <span>Response</span>
                    <div className="live-markdown"><ReactMarkdown remarkPlugins={[remarkGfm]}>{turn.response}</ReactMarkdown></div>
                  </div>
                </article>
              ))
            )}
          </div>
          <form
            className="live-compose"
            data-tour="live-compose"
            onSubmit={(event) => {
              event.preventDefault()
              void sendLivePrompt()
            }}
          >
            <textarea
              value={livePrompt}
              onChange={(event) => setLivePrompt(event.target.value)}
              placeholder="Send a prompt to the selected model..."
              disabled={isLiveSending}
              aria-label="Live provider prompt"
            />
            <div className="live-compose-actions">
              <button
                className="secondary-action"
                type="button"
                onClick={resetLiveLab}
                disabled={isLiveSending || liveTurns.length === 0}
              >
                Reset run
              </button>
              <button
                className={`live-web-search-toggle${liveWebSearch ? ' active' : ''}`}
                type="button"
                onClick={() => setLiveWebSearch((v) => !v)}
                aria-pressed={liveWebSearch}
                title={liveWebSearch ? 'Web search on - click to disable' : 'Web search off - click to enable'}
              >
                <Database size={14} />
                Web search
              </button>
              <button
                className="primary-action"
                type="submit"
                disabled={!liveApiKey.trim() || !livePrompt.trim() || isLiveSending}
              >
                <Send size={15} />
                {isLiveSending ? 'Sending' : 'Send prompt'}
              </button>
            </div>
          </form>
          {liveError && <p className="error-line live-error">{liveError}</p>}
        </section>

        <section className="live-watch-panel">
          <div className="panel-heading">
            <AlertTriangle size={18} />
            <h2>Background watch</h2>
          </div>
          <div className="live-watch-summary">
            <span>{liveTurns.length} turns captured</span>
            <span>{liveWarnings.length} conditions logged</span>
          </div>
          <div className="live-warning-list">
            {liveWarnings.length === 0 ? (
              <div className="empty-state compact-empty">
                Watching for high velocity, low alignment, large word-count deltas, and attractor events.
              </div>
            ) : (
              liveWarnings
                .slice()
                .reverse()
                .map((warning) => (
                  <article className={`live-warning severity-${warning.severity}`} key={warning.id}>
                    <div>
                      <strong>{warning.label}</strong>
                      <span>t={warning.sequenceIndex}</span>
                    </div>
                    <p>{warning.evidence}</p>
                  </article>
                ))
            )}
          </div>
          <div className="experimenter-choice">
            <h3>Experimenter response</h3>
            <p>
              Review the warning, then continue normally, redirect the next prompt,
              apply a controlled perturbation, or stop and export the run.
            </p>
          </div>
        </section>
      </div>
    </section>
  )

  const renderMlmceWorkspace = () => (
    <section className="workspace-home view-panel">
      <div className="workspace-hero-panel">
        <p className="eyebrow">Multi-LLM Moderated Conversation Engine</p>
        <h2>Moderated multi-model sessions</h2>
        <p>
          Configure participant models, a shared seed prompt, turn-taking mode,
          intervention thresholds, and moderator behavior. This workspace will run
          sequential, prompted, broadcast, and interferometer protocols over the
          typed MLMCE core model.
        </p>
      </div>

      <div className="mlmce-grid">
        <section className="mlmce-panel">
          <div className="panel-heading">
            <Users size={18} />
            <h2>Participants</h2>
          </div>
          <div className="participant-list">
            {mlmceParticipants.map((participant, index) => (
              <div className="participant-row" key={`${participant.id}-${index}`}>
                <label>
                  <span>Id</span>
                  <input
                    value={participant.id}
                    onChange={(event) =>
                      updateMlmceParticipant(index, 'id', event.target.value)
                    }
                  />
                </label>
                <label>
                  <span>Role</span>
                  <input
                    value={participant.roleLabel}
                    onChange={(event) =>
                      updateMlmceParticipant(index, 'roleLabel', event.target.value)
                    }
                  />
                </label>
                <label>
                  <span>Model</span>
                  <input
                    value={participant.modelId}
                    onChange={(event) =>
                      updateMlmceParticipant(index, 'modelId', event.target.value)
                    }
                  />
                </label>
                <button
                  className="icon-button"
                  type="button"
                  onClick={() => removeMlmceParticipant(index)}
                  disabled={mlmceParticipants.length <= 2}
                  aria-label={`Remove ${participant.id}`}
                >
                  <X size={16} />
                </button>
              </div>
            ))}
          </div>
          <button className="secondary-action" type="button" onClick={addMlmceParticipant}>
            <Users size={16} />
            Add participant
          </button>
        </section>

        <section className="mlmce-panel">
          <div className="panel-heading">
            <Bot size={18} />
            <h2>Moderator</h2>
          </div>
          <div className="mlmce-form-grid">
            <label>
              <span>Moderator model</span>
              <input
                value={mlmceModeratorModel}
                onChange={(event) => setMlmceModeratorModel(event.target.value)}
              />
            </label>
            <label>
              <span>Profile version</span>
              <input
                value={mlmceModeratorProfileVersion}
                onChange={(event) => setMlmceModeratorProfileVersion(event.target.value)}
              />
            </label>
          </div>
        </section>

        <section className="mlmce-panel">
          <div className="panel-heading">
            <Route size={18} />
            <h2>Protocol</h2>
          </div>
          <div className="mlmce-form-grid">
            <label>
              <span>Turn mode</span>
              <select
                value={mlmceTurnMode}
                onChange={(event) => setMlmceTurnMode(event.target.value as MlmceTurnMode)}
              >
                <option value="sequential">Sequential</option>
                <option value="prompted">Prompted</option>
                <option value="broadcast">Broadcast</option>
              </select>
            </label>
            <label>
              <span>Session kind</span>
              <select
                value={mlmceSessionKind}
                onChange={(event) =>
                  setMlmceSessionKind(event.target.value as MlmceSessionKind)
                }
              >
                <option value="sequential-dialogue">Sequential dialogue</option>
                <option value="forked-interferometer-run">Forked interferometer run</option>
              </select>
            </label>
          </div>
          <label className="mlmce-seed-field">
            <span>Seed prompt</span>
            <textarea
              value={mlmceSeedPrompt}
              onChange={(event) => setMlmceSeedPrompt(event.target.value)}
            />
          </label>
        </section>

        <section className="mlmce-panel">
          <div className="panel-heading">
            <AlertTriangle size={18} />
            <h2>Intervention thresholds</h2>
          </div>
          <div className="threshold-grid">
            <label>
              <span>Stability margin</span>
              <input
                type="number"
                step="0.01"
                value={mlmceThresholds.stabilityCriterionMargin}
                onChange={(event) =>
                  updateMlmceThreshold('stabilityCriterionMargin', event.target.value)
                }
              />
            </label>
            <label>
              <span>Leakage fraction</span>
              <input
                type="number"
                step="0.01"
                min="0"
                max="1"
                value={mlmceThresholds.leakageBudgetFraction}
                onChange={(event) =>
                  updateMlmceThreshold('leakageBudgetFraction', event.target.value)
                }
              />
            </label>
            <label>
              <span>Torsional ceiling</span>
              <input
                type="number"
                step="0.01"
                value={mlmceThresholds.torsionalAccumulationCeiling}
                onChange={(event) =>
                  updateMlmceThreshold('torsionalAccumulationCeiling', event.target.value)
                }
              />
            </label>
            <label>
              <span>Attractor window</span>
              <input
                type="number"
                step="1"
                min="1"
                value={mlmceThresholds.attractorWindow}
                onChange={(event) => updateMlmceThreshold('attractorWindow', event.target.value)}
              />
            </label>
            <label>
              <span>Divergence threshold</span>
              <input
                type="number"
                step="0.01"
                value={mlmceThresholds.divergenceThreshold}
                onChange={(event) =>
                  updateMlmceThreshold('divergenceThreshold', event.target.value)
                }
              />
            </label>
          </div>
        </section>

        <section className="mlmce-panel mlmce-preview-panel">
          <div className="panel-heading">
            <FileJson size={18} />
            <h2>Session preview</h2>
          </div>
          {(() => {
            const issues = validateMlmceConfig(
              mlmceParticipants,
              mlmceModeratorModel,
              mlmceSeedPrompt,
              mlmceThresholds,
            )
            const preview = buildMlmceConfigPreview(
              mlmceParticipants,
              mlmceModeratorModel,
              mlmceModeratorProfileVersion,
              mlmceTurnMode,
              mlmceSessionKind,
              mlmceSeedPrompt,
              mlmceThresholds,
            )

            return (
              <>
                <div className="mlmce-status-strip">
                  <span>{mlmceParticipants.length} participants</span>
                  <span>{mlmceTurnMode}</span>
                  <span>{mlmceSessionKind}</span>
                  <span>{issues.length === 0 ? 'ready to plan' : `${issues.length} issue(s)`}</span>
                </div>
                {issues.length > 0 && (
                  <ul className="mlmce-issues">
                    {issues.map((issue) => (
                      <li key={issue}>{issue}</li>
                    ))}
                  </ul>
                )}
                <pre className="config-preview">
                  {JSON.stringify(preview, null, 2)}
                </pre>
              </>
            )
          })()}
        </section>
      </div>
    </section>
  )

  const renderReportsWorkspace = () => (
    <section className="workspace-home view-panel">
      <div className="workspace-hero-panel">
        <p className="eyebrow">Reports</p>
        <h2>Export and interpretation center</h2>
        <p>
          Reports will collect analysis JSON, attractor events, Live Lab transcripts,
          MLMCE session artifacts, moderator events, and analyst summaries into
          human-readable and machine-readable exports.
        </p>
      </div>
      <div className="workspace-card-grid">
        <article className="workspace-card">
          <FileJson size={20} />
          <h3>Analysis JSON</h3>
          <p>Current run exports preserve metrics, warnings, and attractor diagnostics.</p>
          <span>{analysis ? `${analysis.summary.turnCount} turns available` : 'No active run'}</span>
        </article>
        <article className="workspace-card">
          <Bot size={20} />
          <h3>Analyst Notes</h3>
          <p>Embedded analyst answers and evidence lists can become report sections.</p>
          <span>{analystMessages.length} notes in this session</span>
        </article>
        <article className="workspace-card">
          <AlertTriangle size={20} />
          <h3>Warning Logs</h3>
          <p>Live Lab and MLMCE warning conditions will be exportable as audit trails.</p>
          <span>{liveWarnings.length} live warnings logged</span>
        </article>
      </div>
    </section>
  )

  const themes: Array<{ id: 'light' | 'dark' | 'midnight'; label: string; description: string }> = [
    { id: 'light', label: 'Light', description: 'Clean light background, default workbench look.' },
    { id: 'dark', label: 'Dark', description: 'Dark grey surfaces, easy on the eyes in low light.' },
    { id: 'midnight', label: 'Midnight', description: 'Deep blue palette matching the phase-space canvas.' },
  ]

  const renderSettingsWorkspace = () => (
    <section className="workspace-home view-panel">
      <div className="workspace-hero-panel">
        <p className="eyebrow">Settings</p>
        <h2>Local workbench configuration</h2>
        <p>
          Runtime configuration belongs here: provider defaults, model aliases,
          dashboard behavior, export preferences, and local-only client settings.
        </p>
      </div>

      <div className="settings-section">
        <h3>Appearance</h3>
        <div className="theme-picker">
          {themes.map((t) => (
            <button
              key={t.id}
              type="button"
              className={`theme-card${theme === t.id ? ' active' : ''}`}
              onClick={() => setTheme(t.id)}
            >
              <div className={`theme-swatch theme-swatch-${t.id}`}>
                <div className="swatch-topbar" />
                <div className="swatch-body">
                  <div className="swatch-sidebar" />
                  <div className="swatch-content">
                    <div className="swatch-line" />
                    <div className="swatch-line short" />
                  </div>
                </div>
              </div>
              <strong>{t.label}</strong>
              <p>{t.description}</p>
            </button>
          ))}
        </div>
      </div>

      <div className="settings-section">
        <h3>Live Lab provider</h3>
        <div className="settings-grid">
          <label>
            <span>Default model</span>
            <input value={liveModel} onChange={(event) => setLiveModel(event.target.value)} />
          </label>
          <label>
            <span>Endpoint</span>
            <input value={liveEndpoint} onChange={(event) => setLiveEndpoint(event.target.value)} />
          </label>
          <div className="settings-note">
            Provider experiment keys are not persisted here - Live Lab stores them in browser session only.
          </div>
        </div>
      </div>
    </section>
  )

  const renderAnalyzeView = () => {
    switch (activeAnalyzeView) {
      case 'phase':
        return <PhaseSpace3D turns={analysis?.turns ?? []} />
      case 'timelines':
        return <MetricTimelines turns={analysis?.turns ?? []} />
      case 'features':
        return (
          <FeatureExplorer
            turns={analysis?.turns ?? []}
            attractorEvents={analysis?.attractorEvents ?? []}
            selectedTurn={selectedTurn}
            onSelectTurn={setSelectedTurn}
          />
        )
      case 'data':
        return renderTurnTable()
      case 'input':
        return (
          <section className="import-panel view-panel">
            <div className="panel-heading">
              <Braces size={18} />
              <h2>JSON input</h2>
            </div>
            <textarea
              value={datasetJson}
              onChange={(event) => setDatasetJson(event.target.value)}
              spellCheck={false}
              aria-label="Djehuti JSON dataset"
            />
            {error && <p className="error-line">{error}</p>}
          </section>
        )
      case 'overview':
      default:
        return (
          <div className="overview-stack">
            <section className="summary-band">
              <MetricTile
                icon={<FileJson size={18} />}
                label="turns"
                value={analysis ? String(analysis.summary.turnCount) : '-'}
              />
              <MetricTile
                icon={<Activity size={18} />}
                label="velocities"
                value={analysis ? String(analysis.summary.velocityCount) : '-'}
              />
              <MetricTile
                icon={<BarChart3 size={18} />}
                label="avg p-r cosine"
                value={
                  analysis
                    ? formatNumber(analysis.summary.averagePromptResponseCosine)
                    : '-'
                }
              />
              <MetricTile
                icon={<AlertTriangle size={18} />}
                label="gaps"
                value={analysis ? String(analysis.summary.gapCount) : '-'}
              />
            </section>

            <section className="chart-panel view-panel">
              <div className="panel-heading">
                <Activity size={18} />
                <h2>Response velocity</h2>
              </div>
              <VelocityChart points={analysis?.velocities ?? []} />
            </section>

            <section className="visualization-panel view-panel">
              <div className="panel-heading">
                <Route size={18} />
                <h2>Visualization map</h2>
              </div>
              <div className="visualization-grid">
                {visualizationIdeas.map((idea) => (
                  <article key={idea.name} className="visualization-card">
                    <strong>{idea.name}</strong>
                    <p>{idea.detail}</p>
                  </article>
                ))}
              </div>
            </section>

            {renderRunContext()}
          </div>
        )
    }
  }

  const renderForumView = () => {
    switch (forumView.page) {
      case 'forum':
        return <ForumForumPage forumId={forumView.id!} onNavigateThread={id => setForumView({ page: 'thread', id })} onNavigateHome={() => setForumView({ page: 'home' })} />
      case 'thread':
        return <ForumThreadPage threadId={forumView.id!} onNavigateHome={() => setForumView({ page: 'home' })} onNavigateForum={id => setForumView({ page: 'forum', id })} />
      case 'home':
      default:
        return <ForumPage onNavigateForum={id => setForumView({ page: 'forum', id })} />
    }
  }

  const renderBlogView = () => {
    switch (blogView.page) {
      case 'article':
        return <BlogArticlePage
          slug={blogView.slug!}
          onNavigateBack={() => setBlogView({ page: 'list' })}
          onNavigateEditor={id => setBlogView({ page: 'editor', articleId: id })}
        />
      case 'editor':
        return <BlogEditorPage
          articleId={blogView.articleId}
          onSaved={slug => setBlogView({ page: 'article', slug })}
          onCancel={() => setBlogView({ page: 'list' })}
        />
      case 'list':
      default:
        return <BlogPage
          onNavigateArticle={slug => setBlogView({ page: 'article', slug })}
          onNavigateEditor={() => setBlogView({ page: 'editor' })}
        />
    }
  }

  const renderMainView = () => {
    switch (activeMode) {
      case 'live':
        return renderLiveLab()
      case 'mlmce':
        return renderMlmceWorkspace()
      case 'forum':
        return renderForumView()
      case 'blog':
        return renderBlogView()
      case 'reports':
        return renderReportsWorkspace()
      case 'settings':
        return renderSettingsWorkspace()
      case 'analyze':
      default:
        return renderAnalyzeView()
    }
  }

  return (
    <div
      className={[
        'app-frame',
        isMenuOpen ? 'menu-open' : '',
        showToolsPanel && isToolsOpen ? 'tools-open' : 'tools-collapsed',
        isToolsWide ? 'tools-wide' : '',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      <aside className="side-menu" aria-label="Dashboard navigation" data-tour="side-menu">
        <div className="side-menu-header">
          <button className="icon-button menu-close" type="button" onClick={closeMenu} aria-label="Close menu">
            <X size={18} />
          </button>
        </div>
        <nav className="mode-nav" aria-label="Top-level workbench modes">
          {modeItems.map((item) => {
            const Icon = item.icon
            return (
              <button
                key={item.id}
                className={activeMode === item.id ? 'active' : undefined}
                type="button"
                onClick={() => {
                  setActiveMode(item.id)
                  closeMenu()
                }}
              >
                <Icon size={17} />
                <span>{item.label}</span>
                <ChevronRight size={15} />
              </button>
            )
          })}
        </nav>
        {activeMode === 'analyze' && (
          <div className="subnav-block">
            <span className="subnav-label">Analyze pages</span>
            <nav aria-label="Analyze pages">
              {analyzeNavItems.map((item) => {
                const Icon = item.icon
                return (
                  <button
                    key={item.id}
                    className={activeAnalyzeView === item.id ? 'active' : undefined}
                    type="button"
                    onClick={() => {
                      setActiveAnalyzeView(item.id)
                      closeMenu()
                    }}
                  >
                    <Icon size={17} />
                    <span>{item.label}</span>
                    <ChevronRight size={15} />
                  </button>
                )
              })}
            </nav>
          </div>
        )}
        <div className="menu-status">
          <span>{activeModeItem.label}</span>
          <span>{analysis ? `${analysis.summary.turnCount} turns loaded` : 'No run loaded'}</span>
          <span>{liveTurns.length > 0 ? `${liveTurns.length} live turns` : 'Live Lab idle'}</span>
        </div>
      </aside>

      {isMenuOpen && <button className="menu-backdrop" type="button" onClick={closeMenu} aria-label="Dismiss menu" />}

      <main className="dashboard-shell">
        <header className="topbar" data-tour="topbar">
          <div className="title-row">
            <button
              className="icon-button menu-trigger"
              type="button"
              onClick={() => setIsMenuOpen(true)}
              aria-label="Open menu"
            >
              <Menu size={20} />
            </button>
            <img src={logoUrl} alt="Djehuti Cyberscope AI+" className="topbar-logo" />
            <div>
              <p className="eyebrow">Djehuti Cyberscope AI+</p>
              <h1>{pageTitle}</h1>
            </div>
            <UserMenu onOpenLogin={() => setShowLoginModal(true)} />
            <button
              className="icon-button theme-toggle"
              type="button"
              onClick={() => setTheme((t) => t === 'light' ? 'dark' : t === 'dark' ? 'midnight' : 'light')}
              title={`Theme: ${theme} - click to cycle`}
              aria-label="Cycle theme"
            >
              {theme === 'light' ? <Sun size={18} /> : <Moon size={18} />}
            </button>
            <button
              className={`icon-button tour-trigger${showTourPrompt ? ' active' : ''}`}
              type="button"
              title="Ask the AI to walk you through something"
              aria-label="Start a guided tour"
              onClick={() => setShowTourPrompt((v) => !v)}
            >
              <GraduationCap size={18} />
            </button>
          </div>
          {(showTourPrompt || isAskingAnalyst) && (
            <form
              className="tour-prompt-bar"
              onSubmit={async (e) => {
                e.preventDefault()
                const q = tourQuestion.trim()
                if (!q || !analysis) return
                setShowTourPrompt(false)
                setTourQuestion('Walk me through ')
                await askAnalyst(q)
              }}
            >
              {isAskingAnalyst ? (
                <span className="tour-generating">
                  <span className="tour-spinner" />
                  Generating your tour...
                </span>
              ) : (
                <input
                  className="tour-prompt-input"
                  value={tourQuestion}
                  onChange={(e) => setTourQuestion(e.target.value)}
                  placeholder="Walk me through analyzing a dataset..."
                  autoFocus
                  aria-label="Tour request"
                />
              )}
              <button className="secondary-action" type="submit" disabled={!tourQuestion.trim() || !analysis || isAskingAnalyst}>
                {isAskingAnalyst ? 'Working...' : 'Go'}
              </button>
              <button className="icon-button" type="button" onClick={() => setShowTourPrompt(false)} disabled={isAskingAnalyst}>
                <X size={15} />
              </button>
            </form>
          )}
          {activeMode === 'analyze' && (
            <div className="topbar-actions">
              <label className="dataset-picker" data-tour="dataset-picker">
                <Database size={16} />
                <select
                  aria-label="Dataset library"
                  value={selectedDataSetId}
                  onChange={(event) => {
                    setSelectedDataSetId(event.target.value)
                    setIsRenamingDataSet(false)
                    setRenameError(null)
                  }}
                >
                  <option value="">Manual JSON</option>
                  {catalog.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.name} ({item.turnCount}
                      {item.status === 'partial' && item.declaredTurnCount
                        ? `/${item.declaredTurnCount}`
                        : ''})
                    </option>
                  ))}
                </select>
              </label>
              {selectedDataSetId && (
                <>
                  <button
                    className={`icon-button${isRenamingDataSet ? ' active' : ''}`}
                    type="button"
                    title="Rename dataset"
                    onClick={() => {
                      const item = catalog.find((c) => c.id === selectedDataSetId)
                      setRenameDataSetName(item?.name ?? '')
                      setRenameDataSetDesc(item?.description ?? '')
                      setRenameError(null)
                      setDeleteError(null)
                      setIsRenamingDataSet((v) => !v)
                    }}
                  >
                    <FileText size={16} />
                  </button>
                  <button
                    className="icon-button"
                    type="button"
                    title="Export dataset as JSON"
                    onClick={() => {
                      const item = catalog.find((c) => c.id === selectedDataSetId)
                      const blob = new Blob([datasetJson], { type: 'application/json' })
                      const url = URL.createObjectURL(blob)
                      const a = document.createElement('a')
                      a.href = url
                      a.download = `${item?.id ?? selectedDataSetId}.json`
                      a.click()
                      URL.revokeObjectURL(url)
                    }}
                    disabled={!datasetJson}
                  >
                    <FileJson size={16} />
                  </button>
                  <button
                    className="icon-button danger"
                    type="button"
                    title="Delete dataset"
                    onClick={deleteSelectedDataSet}
                    disabled={isDeletingDataSet}
                  >
                    <Trash2 size={16} />
                  </button>
                </>
              )}
              <button
                className="secondary-action"
                onClick={loadSelectedDataSet}
                disabled={!selectedDataSetId || isLoadingDataSet}
              >
                <FileJson size={16} />
                {isLoadingDataSet ? 'Loading' : 'Load Data'}
              </button>
              <button className="primary-action" onClick={analyze} disabled={isAnalyzing} data-tour="analyze-button">
                <Play size={16} />
                {isAnalyzing ? 'Analyzing' : 'Analyze JSON'}
              </button>
              <button
                className="icon-button tools-toggle"
                type="button"
                onClick={() => setIsToolsOpen((value) => !value)}
                aria-label={isToolsOpen ? 'Collapse tools panel' : 'Expand tools panel'}
              >
                {isToolsOpen ? <PanelRightClose size={18} /> : <PanelRightOpen size={18} />}
              </button>
            </div>
          )}
          {activeMode === 'analyze' && isRenamingDataSet && selectedDataSetId && (
            <form
              className="rename-dataset-bar"
              onSubmit={async (event) => {
                event.preventDefault()
                const name = renameDataSetName.trim()
                if (!name) return
                setRenameError(null)
                try {
                  const response = await fetch(`${apiBase}/api/datasets/${encodeURIComponent(selectedDataSetId)}`, {
                    method: 'PATCH',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ name, description: renameDataSetDesc.trim() }),
                  })
                  if (!response.ok) {
                    const text = await response.text()
                    throw new Error(text || `Rename failed with ${response.status}`)
                  }
                  const refreshed = await fetch(`${apiBase}/api/datasets`)
                  if (refreshed.ok) setCatalog(await refreshed.json() as DataSetCatalogItem[])
                  setIsRenamingDataSet(false)
                } catch (err) {
                  setRenameError(err instanceof Error ? err.message : 'Rename failed')
                }
              }}
            >
              <input
                className="rename-input"
                value={renameDataSetName}
                onChange={(e) => setRenameDataSetName(e.target.value)}
                placeholder="Dataset name"
                aria-label="Dataset name"
                autoFocus
              />
              <input
                className="rename-input rename-desc"
                value={renameDataSetDesc}
                onChange={(e) => setRenameDataSetDesc(e.target.value)}
                placeholder="Description (optional)"
                aria-label="Dataset description"
              />
              <button className="secondary-action" type="submit" disabled={!renameDataSetName.trim()}>
                Save name
              </button>
              <button className="icon-button" type="button" onClick={() => { setIsRenamingDataSet(false); setRenameError(null) }}>
                <X size={15} />
              </button>
              {renameError && <span className="rename-error">{renameError}</span>}
            </form>
          )}
          {deleteError && (
            <div className="rename-dataset-bar">
              <span className="rename-error">{deleteError}</span>
              <button className="icon-button" type="button" onClick={() => setDeleteError(null)}><X size={15} /></button>
            </div>
          )}
        </header>

        <section className="workbench">
          <div className="view-stage">{renderMainView()}</div>

          {showToolsPanel && isToolsOpen && (
          <aside className="tools-panel" aria-label="Analysis tools" data-tour="tools-panel">
            <div className="tools-panel-header">
              <div>
                <p className="eyebrow">Tools</p>
                <h2>Workbench</h2>
              </div>
              <div className="tools-panel-actions">
                <button
                  className="icon-button"
                  type="button"
                  onClick={() => setIsToolsWide((value) => !value)}
                  aria-label={isToolsWide ? 'Reduce tools panel' : 'Expand tools panel'}
                >
                  <ChevronRight size={18} />
                </button>
                <button
                  className="icon-button"
                  type="button"
                  onClick={() => setIsToolsOpen(false)}
                  aria-label="Close tools panel"
                >
                  <X size={18} />
                </button>
              </div>
            </div>

            <section className="tool-card">
              <div className="tool-card-heading">
                <Bot size={17} />
                <h3>Analyst AI</h3>
              </div>
              <p>
                Framework-grounded assistant for reading the current run, metrics,
                attractor events, and warnings.
              </p>
              <form
                className="analyst-form"
                data-tour="analyst-form"
                onSubmit={(event) => {
                  event.preventDefault()
                  void askAnalyst()
                }}
              >
                <textarea
                  value={analystQuestion}
                  onChange={(event) => setAnalystQuestion(event.target.value)}
                  placeholder={
                    analysis
                      ? 'Ask about the current run...'
                      : 'Analyze a dataset before asking the analyst.'
                  }
                  disabled={!analysis || isAskingAnalyst}
                  aria-label="Ask the Djehuti analyst"
                />
                <button
                  className="primary-action tool-action"
                  type="submit"
                  disabled={!analysis || !analystQuestion.trim() || isAskingAnalyst}
                >
                  <Send size={15} />
                  {isAskingAnalyst ? 'Asking' : 'Ask analyst'}
                </button>
              </form>
              {analystError && <p className="tool-error">{analystError}</p>}
              <div className="analyst-thread" aria-live="polite">
                {analystMessages.length === 0 ? (
                  <div className="empty-state compact-empty">
                    {analysis
                      ? 'Ask a question to inspect the measured run with the embedded analyst.'
                      : 'Run analysis to enable the embedded analyst.'}
                  </div>
                ) : (
                  analystMessages.map((message) => (
                    <article className="analyst-message" key={message.id}>
                      <div className="analyst-question">{message.question}</div>
                      <div className="analyst-answer markdown-content">
                        <ReactMarkdown remarkPlugins={[remarkGfm]}>
                          {message.response.answer}
                        </ReactMarkdown>
                      </div>
                      <div className="analyst-meta">
                        <span>{message.response.model}</span>
                        {message.response.metadata['djehuti.analyst_profile'] && (
                          <span>{message.response.metadata['djehuti.analyst_profile']}</span>
                        )}
                      </div>
                      {message.response.evidence.length > 0 && (
                        <details className="analyst-evidence">
                          <summary>{message.response.evidence.length} evidence items</summary>
                          <ul>
                            {message.response.evidence.slice(0, 16).map((evidence, index) => (
                              <li key={`${message.id}-${evidence.label}-${index}`}>
                                <strong>{evidence.label}</strong>
                                <span>{evidence.value}</span>
                                {evidence.source && <em>{evidence.source}</em>}
                              </li>
                            ))}
                          </ul>
                        </details>
                      )}
                    </article>
                  ))
                )}
              </div>
            </section>

            <section className="tool-card">
              <div className="tool-card-heading">
                <FileJson size={17} />
                <h3>Turn inspector</h3>
              </div>
              {selectedTurn ? (
                <div className="inspector-content compact">
                  <dl>
                    <div>
                      <dt>t</dt>
                      <dd>{selectedTurn.sequenceIndex}</dd>
                    </div>
                    <div>
                      <dt>velocity</dt>
                      <dd>{formatNumber(selectedTurn.velocityFromPrevious)}</dd>
                    </div>
                    <div>
                      <dt>source</dt>
                      <dd>{selectedTurn.sourceId}</dd>
                    </div>
                  </dl>
                  <h3>Prompt</h3>
                  <p>{selectedTurn.prompt}</p>
                  <h3>Response</h3>
                  <p>{selectedTurn.response}</p>
                </div>
              ) : (
                <div className="empty-state">Select a turn to inspect text.</div>
              )}
            </section>
          </aside>
          )}
        </section>

        {showToolsPanel && !isToolsOpen && (
          <button
            className="floating-tools-button"
            type="button"
            onClick={() => setIsToolsOpen(true)}
            aria-label="Open tools panel"
          >
            <PanelRightOpen size={18} />
          </button>
        )}
      </main>

      <LoginModal
        open={showLoginModal}
        onClose={() => setShowLoginModal(false)}
        onSwitchToSignup={() => {
          setShowLoginModal(false)
          setShowSignupModal(true)
        }}
        onSwitchToForgotPassword={() => {
          setShowLoginModal(false)
          setShowForgotPasswordModal(true)
        }}
      />

      <SignupModal
        open={showSignupModal}
        onClose={() => setShowSignupModal(false)}
        onSwitchToLogin={() => {
          setShowSignupModal(false)
          setShowLoginModal(true)
        }}
      />

      <ForgotPasswordModal
        open={showForgotPasswordModal}
        onClose={() => setShowForgotPasswordModal(false)}
        onSwitchToLogin={() => {
          setShowForgotPasswordModal(false)
          setShowLoginModal(true)
        }}
      />
    </div>
  )
}

export default App

