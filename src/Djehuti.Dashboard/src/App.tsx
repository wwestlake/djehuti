import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import * as THREE from 'three'
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js'
import { ConvexGeometry } from 'three/examples/jsm/geometries/ConvexGeometry.js'
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
} from '@tanstack/react-table'
import type { ColumnDef } from '@tanstack/react-table'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
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
  Users,
  X,
} from 'lucide-react'
import {
  analyzeDatasetJson,
  askAnalystApi,
  fetchDataSetCatalog,
  fetchDataSetJson,
} from './api/djehutiApi'
import {
  maxChartPoints,
  maxFeatureRailHits,
  maxFeatureRows,
  maxPhasePoints,
  maxTimelinePoints,
  maxVisibleRows,
  phaseRenderModes,
  strategyColors,
  visualizationIdeas,
} from './config/dashboard'
import { detectFeatures } from './features/featureFinder/featureDetection'
import { askLiveProvider, deriveLiveWarnings, liveTurnsToDatasetJson } from './features/live/liveLab'
import { buildMlmceConfigPreview, validateMlmceConfig } from './features/mlmce/mlmceConfig'
import { formatNumber, sampleEvenly } from './lib/format'
import type {
  AnalystMessage,
  AnalyzeResponse,
  AnalyzeView,
  AppMode,
  AttractorEventDto,
  DataSetCatalogItem,
  FeatureHit,
  FeatureSeverity,
  LiveProviderConfig,
  LiveTurn,
  LiveWarning,
  MlmceParticipantConfig,
  MlmceSessionKind,
  MlmceThresholdConfig,
  MlmceTurnMode,
  PhaseRenderMode,
  TimelineSeries,
  TurnMetricDto,
  VelocityPointDto,
} from './types'
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


function VelocityChart({ points }: { points: VelocityPointDto[] }) {
  const width = 720
  const height = 260
  const padding = { top: 20, right: 16, bottom: 34, left: 42 }
  const plotWidth = width - padding.left - padding.right
  const plotHeight = height - padding.top - padding.bottom
  const visiblePoints = sampleEvenly(points, maxChartPoints)
  const xDenominator = Math.max(visiblePoints.length - 1, 1)

  const coordinates = visiblePoints.map((point, index) => {
    const x = padding.left + (index / xDenominator) * plotWidth
    const y = padding.top + (1 - point.value) * plotHeight
    return { ...point, x, y }
  })

  const path =
    coordinates.length === 0
      ? ''
      : coordinates
          .map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x} ${point.y}`)
          .join(' ')

  const areaPath =
    coordinates.length === 0
      ? ''
      : `${path} L ${coordinates[coordinates.length - 1].x} ${height - padding.bottom} L ${coordinates[0].x} ${height - padding.bottom} Z`

  const gridLines = [0, 0.25, 0.5, 0.75, 1]

  return (
    <div className="chart-frame">
      <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Response velocity chart">
        {gridLines.map((value) => {
          const y = padding.top + (1 - value) * plotHeight
          return (
            <g key={value}>
              <line
                className="chart-grid"
                x1={padding.left}
                x2={width - padding.right}
                y1={y}
                y2={y}
              />
              <text className="chart-label" x={10} y={y + 4}>
                {value.toFixed(value === 0 || value === 1 ? 0 : 2)}
              </text>
            </g>
          )
        })}
        <line
          className="chart-axis"
          x1={padding.left}
          x2={width - padding.right}
          y1={height - padding.bottom}
          y2={height - padding.bottom}
        />
        <line
          className="chart-axis"
          x1={padding.left}
          x2={padding.left}
          y1={padding.top}
          y2={height - padding.bottom}
        />
        {areaPath && <path className="chart-area" d={areaPath} />}
        {path && <path className="chart-line" d={path} />}
        {coordinates.map((point, index) => (
          <g key={point.sequenceIndex}>
            <circle className="chart-point" cx={point.x} cy={point.y} r="4.5" />
            {index % Math.max(Math.floor(coordinates.length / 12), 1) === 0 && (
              <text className="chart-x-label" x={point.x} y={height - 10}>
                {point.sequenceIndex}
              </text>
            )}
            <title>
              t={point.sequenceIndex}, velocity={formatNumber(point.value)}
            </title>
          </g>
        ))}
        <text className="chart-title-y" x={8} y={14}>
          velocity
        </text>
        <text className="chart-title-x" x={width - 12} y={height - 10}>
          t
        </text>
      </svg>
      {points.length === 0 && (
        <div className="empty-state chart-empty">Analyze a dataset to plot velocity.</div>
      )}
      {points.length > visiblePoints.length && (
        <div className="chart-note">
          Showing {visiblePoints.length} sampled points from {points.length} velocities.
        </div>
      )}
    </div>
  )
}

function MiniTimelineChart({ series }: { series: TimelineSeries }) {
  const width = 720
  const height = 112
  const padding = { top: 12, right: 12, bottom: 24, left: 44 }
  const plotWidth = width - padding.left - padding.right
  const plotHeight = height - padding.top - padding.bottom
  const visiblePoints = sampleEvenly(series.points, maxTimelinePoints)
  const values = visiblePoints.map((point) => point.value)
  const minValue = values.length > 0 ? Math.min(...values) : 0
  const maxValue = values.length > 0 ? Math.max(...values) : 1
  const valueSpan = Math.max(maxValue - minValue, 0.0001)
  const sequenceValues = visiblePoints.map((point) => point.sequenceIndex)
  const minSequence = sequenceValues.length > 0 ? Math.min(...sequenceValues) : 0
  const maxSequence = sequenceValues.length > 0 ? Math.max(...sequenceValues) : 1
  const sequenceSpan = Math.max(maxSequence - minSequence, 1)

  const coordinates = visiblePoints.map((point) => {
    const x = padding.left + ((point.sequenceIndex - minSequence) / sequenceSpan) * plotWidth
    const y = padding.top + (1 - (point.value - minValue) / valueSpan) * plotHeight
    return { ...point, x, y }
  })

  const path =
    coordinates.length === 0
      ? ''
      : coordinates
          .map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x} ${point.y}`)
          .join(' ')

  const format = series.formatter ?? ((value: number) => formatNumber(value))

  return (
    <article className="timeline-card">
      <div className="timeline-card-heading">
        <strong>{series.label}</strong>
        <span>
          {format(minValue)} - {format(maxValue)}
        </span>
      </div>
      <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`${series.label} timeline`}>
        <line
          className="timeline-grid"
          x1={padding.left}
          x2={width - padding.right}
          y1={padding.top + plotHeight / 2}
          y2={padding.top + plotHeight / 2}
        />
        <line
          className="timeline-axis"
          x1={padding.left}
          x2={width - padding.right}
          y1={height - padding.bottom}
          y2={height - padding.bottom}
        />
        {path && <path className="timeline-line" d={path} style={{ stroke: series.color }} />}
        {coordinates.map((point, index) =>
          index % Math.max(Math.floor(coordinates.length / 18), 1) === 0 ? (
            <circle
              key={`${point.sequenceIndex}-${index}`}
              className="timeline-point"
              cx={point.x}
              cy={point.y}
              r="3"
              style={{ fill: series.color }}
            >
              <title>
                t={point.sequenceIndex}, {series.label}={format(point.value)}
              </title>
            </circle>
          ) : null,
        )}
        <text className="timeline-label" x={8} y={padding.top + 4}>
          {format(maxValue)}
        </text>
        <text className="timeline-label" x={8} y={height - padding.bottom + 4}>
          {format(minValue)}
        </text>
        <text className="timeline-x-label" x={padding.left} y={height - 6}>
          t={minSequence}
        </text>
        <text className="timeline-x-label timeline-x-label-end" x={width - padding.right} y={height - 6}>
          t={maxSequence}
        </text>
      </svg>
      {series.points.length > visiblePoints.length && (
        <p className="timeline-note">
          Showing {visiblePoints.length} sampled points from {series.points.length}.
        </p>
      )}
    </article>
  )
}

function MetricTimelines({ turns }: { turns: TurnMetricDto[] }) {
  const series = useMemo<TimelineSeries[]>(
    () => [
      {
        label: 'prompt-response cosine',
        color: '#087f8c',
        points: turns.map((turn) => ({
          sequenceIndex: turn.sequenceIndex,
          value: turn.promptResponseCosine,
        })),
      },
      {
        label: 'velocity',
        color: '#7c3aed',
        points: turns
          .filter((turn) => turn.velocityFromPrevious !== null)
          .map((turn) => ({
            sequenceIndex: turn.sequenceIndex,
            value: turn.velocityFromPrevious ?? 0,
          })),
      },
      {
        label: 'jaccard similarity',
        color: '#2563eb',
        points: turns.map((turn) => ({
          sequenceIndex: turn.sequenceIndex,
          value: turn.jaccardSimilarity,
        })),
      },
      {
        label: 'word delta',
        color: '#d97706',
        points: turns.map((turn) => ({
          sequenceIndex: turn.sequenceIndex,
          value: turn.wordCountDelta,
        })),
        formatter: (value) => value.toFixed(0),
      },
      {
        label: 'response length',
        color: '#b42318',
        points: turns.map((turn) => ({
          sequenceIndex: turn.sequenceIndex,
          value: turn.responseWordCount,
        })),
        formatter: (value) => value.toFixed(0),
      },
    ],
    [turns],
  )

  return (
    <section className="timelines-panel" id="timelines">
      <div className="panel-heading">
        <BarChart3 size={18} />
        <h2>Metric timelines</h2>
      </div>
      {turns.length === 0 ? (
        <div className="empty-state">Analyze a dataset to populate timelines.</div>
      ) : (
        <div className="timeline-grid-panel">
          {series.map((item) => (
            <MiniTimelineChart key={item.label} series={item} />
          ))}
        </div>
      )}
    </section>
  )
}

function FeatureExplorer({
  turns,
  attractorEvents,
  selectedTurn,
  onSelectTurn,
}: {
  turns: TurnMetricDto[]
  attractorEvents: AttractorEventDto[]
  selectedTurn: TurnMetricDto | null
  onSelectTurn: (turn: TurnMetricDto) => void
}) {
  const features = useMemo(
    () => detectFeatures(turns, attractorEvents),
    [turns, attractorEvents],
  )
  const visibleRailHits = useMemo(
    () => sampleEvenly(features, maxFeatureRailHits),
    [features],
  )
  const visibleRows = features.slice(0, maxFeatureRows)
  const turnBySequence = useMemo(
    () => new Map(turns.map((turn) => [turn.sequenceIndex, turn])),
    [turns],
  )
  const sequenceValues = turns.map((turn) => turn.sequenceIndex)
  const minSequence = sequenceValues.length > 0 ? Math.min(...sequenceValues) : 0
  const maxSequence = sequenceValues.length > 0 ? Math.max(...sequenceValues) : 1
  const sequenceSpan = Math.max(maxSequence - minSequence, 1)
  const railWidth = 720
  const railHeight = 138
  const padding = { left: 56, right: 18 }
  const plotWidth = railWidth - padding.left - padding.right
  const severityY: Record<FeatureSeverity, number> = {
    high: 34,
    medium: 68,
    low: 102,
  }
  const severityCounts = features.reduce<Record<FeatureSeverity, number>>(
    (counts, feature) => {
      counts[feature.severity] += 1
      return counts
    },
    { high: 0, medium: 0, low: 0 },
  )

  const selectFeature = (feature: FeatureHit) => {
    const turn = turnBySequence.get(feature.sequenceIndex)
    if (turn) {
      onSelectTurn(turn)
    }
  }

  return (
    <section className="features-panel" id="features">
      <div className="panel-heading">
        <AlertTriangle size={18} />
        <h2>Feature finder</h2>
      </div>
      {turns.length === 0 ? (
        <div className="empty-state">Analyze a dataset to locate conversation features.</div>
      ) : (
        <>
          <div className="feature-summary">
            <span>
              <strong>{features.length}</strong>
              detected features
            </span>
            <span className="severity-pill severity-high">
              {severityCounts.high} high
            </span>
            <span className="severity-pill severity-medium">
              {severityCounts.medium} medium
            </span>
            <span className="severity-pill severity-low">
              {severityCounts.low} low
            </span>
          </div>

          <div className="feature-rail-wrap">
            <svg
              className="feature-rail"
              viewBox={`0 0 ${railWidth} ${railHeight}`}
              role="img"
              aria-label="Detected features over conversation time"
            >
              {(['high', 'medium', 'low'] as FeatureSeverity[]).map((severity) => (
                <g key={severity}>
                  <text className="feature-rail-label" x="8" y={severityY[severity] + 4}>
                    {severity}
                  </text>
                  <line
                    className="feature-rail-line"
                    x1={padding.left}
                    x2={railWidth - padding.right}
                    y1={severityY[severity]}
                    y2={severityY[severity]}
                  />
                </g>
              ))}
              <text className="feature-rail-time" x={padding.left} y={railHeight - 8}>
                t={minSequence}
              </text>
              <text
                className="feature-rail-time feature-rail-time-end"
                x={railWidth - padding.right}
                y={railHeight - 8}
              >
                t={maxSequence}
              </text>
              {visibleRailHits.map((feature) => {
                const x =
                  padding.left +
                  ((feature.sequenceIndex - minSequence) / sequenceSpan) * plotWidth
                const selected = selectedTurn?.sequenceIndex === feature.sequenceIndex
                return (
                  <circle
                    key={feature.id}
                    className={`feature-rail-marker severity-${feature.severity}${
                      selected ? ' selected' : ''
                    }`}
                    cx={x}
                    cy={severityY[feature.severity]}
                    r={selected ? 5.5 : 4}
                    tabIndex={0}
                    role="button"
                    onClick={() => selectFeature(feature)}
                    onKeyDown={(event) => {
                      if (event.key === 'Enter' || event.key === ' ') {
                        selectFeature(feature)
                      }
                    }}
                  >
                    <title>
                      t={feature.sequenceIndex}, {feature.label}: {feature.evidence}
                    </title>
                  </circle>
                )
              })}
            </svg>
            {features.length > visibleRailHits.length && (
              <p className="render-note">
                Showing {visibleRailHits.length} sampled feature markers from {features.length}.
              </p>
            )}
          </div>

          <div className="feature-table-wrap">
            <table className="feature-table">
              <thead>
                <tr>
                  <th>t</th>
                  <th>feature</th>
                  <th>source</th>
                  <th>severity</th>
                  <th>evidence</th>
                </tr>
              </thead>
              <tbody>
                {visibleRows.map((feature) => (
                  <tr
                    key={feature.id}
                    className={
                      selectedTurn?.sequenceIndex === feature.sequenceIndex
                        ? 'selected'
                        : undefined
                    }
                    onClick={() => selectFeature(feature)}
                  >
                    <td>{feature.sequenceIndex}</td>
                    <td>{feature.label}</td>
                    <td>{feature.source}</td>
                    <td>
                      <span className={`severity-pill severity-${feature.severity}`}>
                        {feature.severity}
                      </span>
                    </td>
                    <td>{feature.evidence}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            {features.length > visibleRows.length && (
              <p className="render-note">
                Showing first {visibleRows.length} rows from {features.length} detected features.
              </p>
            )}
          </div>
        </>
      )}
    </section>
  )
}

function PhaseSpace3D({ turns }: { turns: TurnMetricDto[] }) {
  const hostRef = useRef<HTMLDivElement | null>(null)
  const [renderMode, setRenderMode] = useState<PhaseRenderMode>('points')
  const visibleTurns = useMemo(() => sampleEvenly(turns, maxPhasePoints), [turns])

  useEffect(() => {
    const host = hostRef.current
    if (!host) {
      return undefined
    }

    host.replaceChildren()

    const scene = new THREE.Scene()
    scene.background = new THREE.Color(0x10202c)

    const camera = new THREE.PerspectiveCamera(52, 1, 0.1, 100)
    camera.position.set(6.8, 4.8, 7.4)

    const renderer = new THREE.WebGLRenderer({ antialias: true })
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2))
    renderer.setSize(host.clientWidth, host.clientHeight)
    host.appendChild(renderer.domElement)

    const controls = new OrbitControls(camera, renderer.domElement)
    controls.enableDamping = true
    controls.target.set(0, 0.2, 0)

    scene.add(new THREE.AmbientLight(0xffffff, 0.82))
    const keyLight = new THREE.DirectionalLight(0xffffff, 1.6)
    keyLight.position.set(3, 7, 5)
    scene.add(keyLight)

    const grid = new THREE.GridHelper(8, 8, 0x486477, 0x263b4a)
    grid.position.y = -2.05
    scene.add(grid)

    const axisMaterial = new THREE.LineBasicMaterial({ color: 0xb6c7d4 })
    const axisGeometry = new THREE.BufferGeometry().setFromPoints([
      new THREE.Vector3(-4.2, -2, -2.2),
      new THREE.Vector3(4.2, -2, -2.2),
      new THREE.Vector3(-4.2, -2, -2.2),
      new THREE.Vector3(-4.2, 2.2, -2.2),
      new THREE.Vector3(-4.2, -2, -2.2),
      new THREE.Vector3(-4.2, -2, 2.2),
    ])
    scene.add(new THREE.LineSegments(axisGeometry, axisMaterial))

    const pointGeometry = new THREE.SphereGeometry(0.075, 16, 16)
    const pointMaterials: THREE.Material[] = []
    const lineGeometries: THREE.BufferGeometry[] = [axisGeometry]
    const transientMaterials: THREE.Material[] = []

    if (visibleTurns.length > 0) {
      const sequenceValues = visibleTurns.map((turn) => turn.sequenceIndex)
      const minSequence = Math.min(...sequenceValues)
      const maxSequence = Math.max(...sequenceValues)
      const sequenceSpan = Math.max(maxSequence - minSequence, 1)

      const positions = visibleTurns.map((turn) => {
        const x = ((turn.sequenceIndex - minSequence) / sequenceSpan) * 8 - 4
        const y = Math.max(Math.min(turn.promptResponseCosine, 1), 0) * 4 - 2
        const z = Math.max(Math.min(turn.velocityFromPrevious ?? 0, 1), 0) * 4 - 2
        return new THREE.Vector3(x, y, z)
      })

      if (positions.length > 1 && renderMode === 'deform') {
        const columns = 56
        const rows = 34
        const vertices: number[] = []
        const indices: number[] = []
        const colors: number[] = []
        const color = new THREE.Color()

        for (let row = 0; row < rows; row += 1) {
          const y = -2 + (row / (rows - 1)) * 4
          for (let column = 0; column < columns; column += 1) {
            const x = -4 + (column / (columns - 1)) * 8
            let displacement = 0
            let influence = 0

            positions.forEach((position, index) => {
              const turn = visibleTurns[index]
              const dx = x - position.x
              const dy = y - position.y
              const distanceSquared = dx * dx + dy * dy
              const velocity = turn.velocityFromPrevious ?? 0
              const alignmentStress = 1 - Math.max(Math.min(turn.promptResponseCosine, 1), 0)
              const wordDeltaStress = Math.min(Math.abs(turn.wordCountDelta) / 140, 1)
              const radius = 0.48 + velocity * 0.34 + wordDeltaStress * 0.18
              const falloff = Math.exp(-distanceSquared / (radius * radius))
              const signedPull = velocity * 1.25 + alignmentStress * 0.72 + wordDeltaStress * 0.48

              displacement += falloff * signedPull
              influence += falloff
            })

            const normalizedDisplacement = influence > 0 ? displacement / Math.max(influence, 0.35) : 0
            const z = -2 + Math.min(normalizedDisplacement, 2.55)
            vertices.push(x, y, z)

            const heat = Math.min(Math.max((z + 2) / 2.55, 0), 1)
            color.setHSL(0.52 - heat * 0.38, 0.66, 0.32 + heat * 0.22)
            colors.push(color.r, color.g, color.b)
          }
        }

        for (let row = 0; row < rows - 1; row += 1) {
          for (let column = 0; column < columns - 1; column += 1) {
            const current = row * columns + column
            const right = current + 1
            const below = current + columns
            const belowRight = below + 1
            indices.push(current, below, right, right, below, belowRight)
          }
        }

        const surfaceGeometry = new THREE.BufferGeometry()
        surfaceGeometry.setIndex(indices)
        surfaceGeometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3))
        surfaceGeometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3))
        surfaceGeometry.computeVertexNormals()
        lineGeometries.push(surfaceGeometry)

        const surfaceMaterial = new THREE.MeshStandardMaterial({
          emissive: 0x063a43,
          emissiveIntensity: 0.08,
          metalness: 0.02,
          opacity: 0.74,
          roughness: 0.58,
          side: THREE.DoubleSide,
          transparent: true,
          vertexColors: true,
        })
        transientMaterials.push(surfaceMaterial)
        scene.add(new THREE.Mesh(surfaceGeometry, surfaceMaterial))

        const wireGeometry = new THREE.WireframeGeometry(surfaceGeometry)
        lineGeometries.push(wireGeometry)
        const wireMaterial = new THREE.LineBasicMaterial({
          color: 0xdce8ee,
          opacity: 0.11,
          transparent: true,
        })
        transientMaterials.push(wireMaterial)
        scene.add(new THREE.LineSegments(wireGeometry, wireMaterial))
      }

      if (positions.length > 1 && renderMode === 'envelope') {
        const curve = new THREE.CatmullRomCurve3(positions)
        const sampleCount = Math.min(Math.max(positions.length * 4, 64), 360)
        const envelopePoints: THREE.Vector3[] = []
        const radialDirections = [
          new THREE.Vector3(1, 0, 0),
          new THREE.Vector3(-1, 0, 0),
          new THREE.Vector3(0, 1, 0),
          new THREE.Vector3(0, -1, 0),
          new THREE.Vector3(0, 0, 1),
          new THREE.Vector3(0, 0, -1),
          new THREE.Vector3(1, 1, 0).normalize(),
          new THREE.Vector3(-1, 1, 0).normalize(),
          new THREE.Vector3(1, 0, 1).normalize(),
          new THREE.Vector3(-1, 0, 1).normalize(),
          new THREE.Vector3(0, 1, 1).normalize(),
          new THREE.Vector3(0, -1, 1).normalize(),
        ]

        for (let index = 0; index < sampleCount; index += 1) {
          const t = sampleCount === 1 ? 0 : index / (sampleCount - 1)
          const center = curve.getPoint(t)
          const turnIndex = Math.min(
            Math.round(t * (visibleTurns.length - 1)),
            visibleTurns.length - 1,
          )
          const turn = visibleTurns[turnIndex]
          const velocity = turn.velocityFromPrevious ?? 0
          const wordDeltaWeight = Math.min(Math.abs(turn.wordCountDelta) / 120, 1)
          const radius = 0.2 + velocity * 0.18 + wordDeltaWeight * 0.12

          envelopePoints.push(center)
          radialDirections.forEach((direction) => {
            envelopePoints.push(center.clone().add(direction.clone().multiplyScalar(radius)))
          })
        }

        const envelopeGeometry = new ConvexGeometry(envelopePoints)
        envelopeGeometry.computeVertexNormals()
        lineGeometries.push(envelopeGeometry)
        const envelopeMaterial = new THREE.MeshStandardMaterial({
          color: 0x8bd3dd,
          emissive: 0x087f8c,
          emissiveIntensity: 0.14,
          metalness: 0.04,
          opacity: 0.62,
          roughness: 0.28,
          side: THREE.DoubleSide,
          transparent: true,
        })
        transientMaterials.push(envelopeMaterial)
        scene.add(new THREE.Mesh(envelopeGeometry, envelopeMaterial))

        const wireGeometry = new THREE.EdgesGeometry(envelopeGeometry, 16)
        lineGeometries.push(wireGeometry)
        const wireMaterial = new THREE.LineBasicMaterial({
          color: 0xdce8ee,
          opacity: 0.18,
          transparent: true,
        })
        transientMaterials.push(wireMaterial)
        scene.add(new THREE.LineSegments(wireGeometry, wireMaterial))
      }

      if (
        positions.length > 1 &&
        renderMode !== 'points' &&
        renderMode !== 'envelope' &&
        renderMode !== 'deform'
      ) {
        const curve = new THREE.CatmullRomCurve3(positions)
        const tubeSegments = Math.min(Math.max(positions.length * 3, 48), 320)
        const tubeGeometry = new THREE.TubeGeometry(
          curve,
          tubeSegments,
          renderMode === 'solid' ? 0.13 : 0.09,
          14,
          false,
        )
        lineGeometries.push(tubeGeometry)
        const tubeMaterial = new THREE.MeshStandardMaterial({
          color: 0x8bd3dd,
          emissive: 0x087f8c,
          emissiveIntensity: 0.18,
          metalness: 0.08,
          opacity: renderMode === 'solid' ? 0.92 : 0.7,
          roughness: 0.34,
          transparent: true,
        })
        transientMaterials.push(tubeMaterial)
        scene.add(new THREE.Mesh(tubeGeometry, tubeMaterial))

        if (renderMode === 'solid') {
          const glowGeometry = new THREE.TubeGeometry(curve, tubeSegments, 0.24, 14, false)
          lineGeometries.push(glowGeometry)
          const glowMaterial = new THREE.MeshBasicMaterial({
            color: 0x8bd3dd,
            opacity: 0.11,
            transparent: true,
          })
          transientMaterials.push(glowMaterial)
          scene.add(new THREE.Mesh(glowGeometry, glowMaterial))
        }
      }

      if (renderMode !== 'solid' && renderMode !== 'envelope') {
        const lineGeometry = new THREE.BufferGeometry().setFromPoints(positions)
        lineGeometries.push(lineGeometry)
        const lineMaterial = new THREE.LineBasicMaterial({
          color: 0x8bd3dd,
          transparent: true,
          opacity: 0.74,
        })
        transientMaterials.push(lineMaterial)
        scene.add(new THREE.Line(lineGeometry, lineMaterial))

        visibleTurns.forEach((turn, index) => {
          const color = strategyColors[turn.strategy] ?? strategyColors.Unknown
          const material = new THREE.MeshStandardMaterial({
            color,
            emissive: color,
            emissiveIntensity: index === 0 ? 0.18 : 0.08,
            roughness: 0.42,
          })
          pointMaterials.push(material)
          const point = new THREE.Mesh(pointGeometry, material)
          point.position.copy(positions[index])
          point.scale.setScalar(index === 0 || index === visibleTurns.length - 1 ? 1.55 : 1)
          scene.add(point)
        })
      } else if (renderMode === 'solid' || renderMode === 'envelope') {
        const endCapMaterial = new THREE.MeshStandardMaterial({
          color: 0xffffff,
          emissive: 0x8bd3dd,
          emissiveIntensity: 0.22,
          roughness: 0.4,
        })
        transientMaterials.push(endCapMaterial)

        const startCap = new THREE.Mesh(pointGeometry, endCapMaterial)
        startCap.position.copy(positions[0])
        startCap.scale.setScalar(1.7)
        scene.add(startCap)

        const endCap = new THREE.Mesh(pointGeometry, endCapMaterial)
        endCap.position.copy(positions[positions.length - 1])
        endCap.scale.setScalar(1.7)
        scene.add(endCap)
      }
    }

    const resizeObserver = new ResizeObserver(() => {
      const width = host.clientWidth
      const height = host.clientHeight
      camera.aspect = width / Math.max(height, 1)
      camera.updateProjectionMatrix()
      renderer.setSize(width, height)
    })
    resizeObserver.observe(host)

    let frameId = 0
    const render = () => {
      frameId = window.requestAnimationFrame(render)
      controls.update()
      renderer.render(scene, camera)
    }
    render()

    return () => {
      window.cancelAnimationFrame(frameId)
      resizeObserver.disconnect()
      controls.dispose()
      renderer.dispose()
      axisMaterial.dispose()
      pointGeometry.dispose()
      pointMaterials.forEach((material) => material.dispose())
      transientMaterials.forEach((material) => material.dispose())
      lineGeometries.forEach((geometry) => geometry.dispose())
      host.replaceChildren()
    }
  }, [renderMode, visibleTurns])

  return (
    <section className="phase-space-section" id="phase-space">
      <div className="phase-copy">
        <div>
          <p className="eyebrow">3D phase space</p>
          <h2>Conversation trajectory</h2>
        </div>
        <div className="phase-mode-control" aria-label="3D render mode">
          {phaseRenderModes.map((mode) => (
            <button
              key={mode.id}
              className={renderMode === mode.id ? 'active' : undefined}
              type="button"
              onClick={() => setRenderMode(mode.id)}
            >
              {mode.label}
            </button>
          ))}
        </div>
        <div className="phase-axis-list">
          <span>X: time</span>
          <span>Y: prompt-response cosine</span>
          <span>Z: velocity</span>
        </div>
      </div>
      <div className="phase-canvas-shell">
        <div className="phase-canvas" ref={hostRef} aria-label="3D phase-space graph" />
        {turns.length === 0 && (
          <div className="phase-empty">Analyze a dataset to render the trajectory.</div>
        )}
        {turns.length > visibleTurns.length && (
          <div className="phase-sample-note">
            Showing {visibleTurns.length} sampled turns from {turns.length}.
          </div>
        )}
        <div className="phase-legend">
          {Object.entries(strategyColors).map(([name, color]) => (
            <span key={name}>
              <i style={{ backgroundColor: `#${color.toString(16).padStart(6, '0')}` }} />
              {name}
            </span>
          ))}
        </div>
      </div>
    </section>
  )
}

function MetricTile({
  icon,
  label,
  value,
}: {
  icon: ReactNode
  label: string
  value: string
}) {
  return (
    <div className="metric-tile">
      <span className="metric-icon">{icon}</span>
      <span className="metric-label">{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

function App() {
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
        const response = await fetch('/api/datasets')
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

  const loadSelectedDataSet = async () => {
    if (!selectedDataSetId) {
      return
    }

    setIsLoadingDataSet(true)
    setError(null)

    try {
      const response = await fetch(`/api/datasets/${encodeURIComponent(selectedDataSetId)}`)
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

  const askAnalyst = async () => {
    const question = analystQuestion.trim()
    if (!analysis || !question) {
      return
    }

    setIsAskingAnalyst(true)
    setAnalystError(null)

    const controller = new AbortController()
    const timeoutId = window.setTimeout(() => controller.abort(), 95000)

    try {
      const response = await fetch('/api/analyst/ask', {
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
      setAnalystMessages((messages) => [
        {
          id: `${Date.now()}-${messages.length}`,
          question,
          response: result,
        },
        ...messages,
      ])
      setAnalystQuestion('')
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
              onSubmit={async (event) => {
                event.preventDefault()
                if (!liveTurns.length) return
                setIsSavingRun(true)
                setSaveRunError(null)
                setSaveRunSuccess(null)
                try {
                  const name = saveRunName.trim() || `Live Lab ${new Date().toLocaleString()}`
                  const response = await fetch('/api/datasets', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                      name,
                      description: `Live Lab run — ${liveTurns.length} turns, model: ${liveModel.trim() || 'gpt-4.1-mini'}`,
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
                  const refreshed = await fetch('/api/datasets')
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
                {isSavingRun ? 'Saving…' : 'Save run'}
              </button>
            </form>
          </div>
          {saveRunSuccess && <p className="live-save-success">{saveRunSuccess}</p>}
          {saveRunError && <p className="error-line live-error">{saveRunError}</p>}
          <div className="live-transcript">
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
                title={liveWebSearch ? 'Web search on — click to disable' : 'Web search off — click to enable'}
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
            Provider experiment keys are not persisted here — Live Lab stores them in browser session only.
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

  const renderMainView = () => {
    switch (activeMode) {
      case 'live':
        return renderLiveLab()
      case 'mlmce':
        return renderMlmceWorkspace()
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
      <aside className="side-menu" aria-label="Dashboard navigation">
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
        <header className="topbar">
          <div className="title-row">
            <button
              className="icon-button menu-trigger"
              type="button"
              onClick={() => setIsMenuOpen(true)}
              aria-label="Open menu"
            >
              <Menu size={20} />
            </button>
            <img src="/logo.png" alt="Djehuti Cyberscope AI+" className="topbar-logo" onError={(e) => { (e.target as HTMLImageElement).style.display = 'none' }} />
            <div>
              <p className="eyebrow">Djehuti Cyberscope AI+</p>
              <h1>{pageTitle}</h1>
            </div>
            <button
              className="icon-button theme-toggle"
              type="button"
              onClick={() => setTheme((t) => t === 'light' ? 'dark' : t === 'dark' ? 'midnight' : 'light')}
              title={`Theme: ${theme} — click to cycle`}
              aria-label="Cycle theme"
            >
              {theme === 'light' ? <Sun size={18} /> : theme === 'dark' ? <Moon size={18} /> : <Moon size={18} />}
            </button>
          </div>
          {activeMode === 'analyze' && (
            <div className="topbar-actions">
              <label className="dataset-picker">
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
                <button
                  className={`icon-button${isRenamingDataSet ? ' active' : ''}`}
                  type="button"
                  title="Rename dataset"
                  onClick={() => {
                    const item = catalog.find((c) => c.id === selectedDataSetId)
                    setRenameDataSetName(item?.name ?? '')
                    setRenameDataSetDesc(item?.description ?? '')
                    setRenameError(null)
                    setIsRenamingDataSet((v) => !v)
                  }}
                >
                  <FileText size={16} />
                </button>
              )}
              <button
                className="secondary-action"
                onClick={loadSelectedDataSet}
                disabled={!selectedDataSetId || isLoadingDataSet}
              >
                <FileJson size={16} />
                {isLoadingDataSet ? 'Loading' : 'Load Data'}
              </button>
              <button className="primary-action" onClick={analyze} disabled={isAnalyzing}>
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
                  const response = await fetch(`/api/datasets/${encodeURIComponent(selectedDataSetId)}`, {
                    method: 'PATCH',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ name, description: renameDataSetDesc.trim() }),
                  })
                  if (!response.ok) {
                    const text = await response.text()
                    throw new Error(text || `Rename failed with ${response.status}`)
                  }
                  const refreshed = await fetch('/api/datasets')
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
        </header>

        <section className="workbench">
          <div className="view-stage">{renderMainView()}</div>

          {showToolsPanel && isToolsOpen && (
          <aside className="tools-panel" aria-label="Analysis tools">
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
    </div>
  )
}

export default App
