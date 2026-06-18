import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import * as THREE from 'three'
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js'
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
} from '@tanstack/react-table'
import type { ColumnDef } from '@tanstack/react-table'
import {
  Activity,
  AlertTriangle,
  BarChart3,
  Braces,
  ChevronRight,
  Database,
  FileJson,
  Gauge,
  Menu,
  Play,
  Route,
  Table2,
  X,
} from 'lucide-react'
import './App.css'

type SummaryDto = {
  sourceId: string
  sourceName: string
  sourceKind: string
  sessionId: string
  modelId: string
  turnCount: number
  velocityCount: number
  gapCount: number
  averagePromptResponseCosine: number
  averageWordCountDelta: number
}

type TurnMetricDto = {
  sequenceIndex: number
  prompt: string
  response: string
  promptWordCount: number
  responseWordCount: number
  wordCountDelta: number
  sharedWordCount: number
  promptResponseCosine: number
  jaccardSimilarity: number
  editSimilarity: number
  velocityFromPrevious: number | null
  strategy: string
  contaminationDepth: string
  sourceId: string
}

type VelocityPointDto = {
  sequenceIndex: number
  value: number
  basis: string
}

type ConstantDto = {
  name: string
  value: string
}

type AnalyzeResponse = {
  summary: SummaryDto
  turns: TurnMetricDto[]
  velocities: VelocityPointDto[]
  constants: ConstantDto[]
  warnings: string[]
}

type DataSetCatalogItem = {
  id: string
  name: string
  description: string
  file: string
  sourceKind: string
  turnCount: number
  declaredTurnCount: number | null
  status: string
}

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

const formatNumber = (value: number | null | undefined, digits = 3) =>
  typeof value === 'number' && Number.isFinite(value)
    ? value.toFixed(digits)
    : 'n/a'

const maxVisibleRows = 250
const maxChartPoints = 300
const maxPhasePoints = 600
const maxTimelinePoints = 360
const maxFeatureRailHits = 800
const maxFeatureRows = 220

const strategyColors: Record<string, number> = {
  Stable: 0x087f8c,
  Expansive: 0x7c3aed,
  Corrective: 0xd97706,
  Drifting: 0xb42318,
  Unknown: 0x667085,
}

const navItems = [
  { href: '#summary', label: 'Run summary', icon: Gauge },
  { href: '#phase-space', label: '3D phase space', icon: Route },
  { href: '#timelines', label: 'Metric timelines', icon: BarChart3 },
  { href: '#features', label: 'Feature finder', icon: AlertTriangle },
  { href: '#input', label: 'JSON input', icon: Braces },
  { href: '#velocity', label: 'Velocity', icon: Activity },
  { href: '#metrics', label: 'Turn metrics', icon: Table2 },
  { href: '#visualizations', label: 'Visualization map', icon: Route },
  { href: '#context', label: 'Run context', icon: Database },
]

const visualizationIdeas = [
  {
    name: 'Metric timelines',
    detail: 'Plot cosine, Jaccard, edit similarity, and word delta over integer time.',
  },
  {
    name: 'Prompt-response phase space',
    detail: 'Render time, alignment, and velocity as an interactive trajectory through measurement space.',
  },
  {
    name: 'Strategy bands',
    detail: 'Show contiguous spans of stable, corrective, expansive, or drifting behavior.',
  },
  {
    name: 'State transition graph',
    detail: 'Connect each turn to the next by velocity and annotate large jumps.',
  },
  {
    name: 'Gap detector',
    detail: 'Highlight missing sequence indexes and abrupt metric discontinuities.',
  },
  {
    name: 'Corpus distribution',
    detail: 'Histogram or density view for response length, velocity, and similarity.',
  },
]

const sampleEvenly = <T,>(items: T[], limit: number) => {
  if (items.length <= limit) {
    return items
  }

  return Array.from({ length: limit }, (_, index) => {
    const sourceIndex = Math.round((index / (limit - 1)) * (items.length - 1))
    return items[sourceIndex]
  })
}

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

type TimelinePoint = {
  sequenceIndex: number
  value: number
}

type TimelineSeries = {
  label: string
  color: string
  points: TimelinePoint[]
  formatter?: (value: number) => string
}

type FeatureSource = 'prompt' | 'response' | 'transition' | 'metrics'

type FeatureSeverity = 'low' | 'medium' | 'high'

type FeatureHit = {
  id: string
  label: string
  sequenceIndex: number
  source: FeatureSource
  severity: FeatureSeverity
  evidence: string
}

const topicMarkers = [
  { label: 'Descartes', terms: ['descartes', 'cartesian'] },
  { label: 'Gassendi', terms: ['gassendi'] },
  { label: 'pineal gland', terms: ['pineal'] },
  { label: 'vortex theory', terms: ['vortex', 'vortices'] },
  { label: 'dualism', terms: ['dualism', 'dualist'] },
]

const featureSeverityOrder: Record<FeatureSeverity, number> = {
  high: 0,
  medium: 1,
  low: 2,
}

const normalizeFeatureText = (text: string) =>
  text
    .toLowerCase()
    .replace(/[^a-z0-9\s]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()

const evidenceSnippet = (text: string, term?: string) => {
  const compact = text.replace(/\s+/g, ' ').trim()
  if (!compact) {
    return 'No text evidence available.'
  }

  if (!term) {
    return compact.length > 180 ? `${compact.slice(0, 177)}...` : compact
  }

  const lower = compact.toLowerCase()
  const index = lower.indexOf(term.toLowerCase())
  if (index < 0) {
    return compact.length > 180 ? `${compact.slice(0, 177)}...` : compact
  }

  const start = Math.max(index - 70, 0)
  const end = Math.min(index + term.length + 90, compact.length)
  const prefix = start > 0 ? '...' : ''
  const suffix = end < compact.length ? '...' : ''
  return `${prefix}${compact.slice(start, end)}${suffix}`
}

function detectFeatures(turns: TurnMetricDto[]) {
  const hits: FeatureHit[] = []
  const seenPrompts = new Map<string, number>()

  const emit = (
    turn: TurnMetricDto,
    label: string,
    source: FeatureSource,
    severity: FeatureSeverity,
    evidence: string,
  ) => {
    hits.push({
      id: `${turn.sequenceIndex}-${label}-${source}-${hits.length}`,
      label,
      sequenceIndex: turn.sequenceIndex,
      source,
      severity,
      evidence,
    })
  }

  for (const turn of turns) {
    const velocity = turn.velocityFromPrevious ?? 0
    const prompt = turn.prompt ?? ''
    const response = turn.response ?? ''

    if (velocity > 0.65) {
      emit(
        turn,
        'high velocity',
        'transition',
        'high',
        `Velocity from previous turn is ${formatNumber(velocity)}.`,
      )
    }

    if (turn.promptResponseCosine < 0.15) {
      emit(
        turn,
        'low prompt-response alignment',
        'metrics',
        'high',
        `Prompt-response cosine is ${formatNumber(turn.promptResponseCosine)}.`,
      )
    }

    if (Math.abs(turn.wordCountDelta) > 40) {
      emit(
        turn,
        'large word-count delta',
        'metrics',
        'medium',
        `Prompt has ${turn.promptWordCount} words; response has ${turn.responseWordCount}.`,
      )
    }

    if (turn.responseWordCount < 12) {
      emit(
        turn,
        'short response',
        'response',
        'medium',
        evidenceSnippet(response),
      )
    }

    const normalizedPrompt = normalizeFeatureText(prompt)
    if (normalizedPrompt.length > 24) {
      const firstSeen = seenPrompts.get(normalizedPrompt)
      if (firstSeen !== undefined) {
        emit(
          turn,
          'repeated prompt',
          'prompt',
          'medium',
          `Prompt first appeared at t=${firstSeen}.`,
        )
      } else {
        seenPrompts.set(normalizedPrompt, turn.sequenceIndex)
      }
    }

    const lowerPrompt = prompt.toLowerCase()
    const lowerResponse = response.toLowerCase()
    for (const marker of topicMarkers) {
      const term = marker.terms.find(
        (candidate) =>
          lowerPrompt.includes(candidate) || lowerResponse.includes(candidate),
      )

      if (term) {
        const source = lowerPrompt.includes(term) ? 'prompt' : 'response'
        emit(
          turn,
          `topic: ${marker.label}`,
          source,
          'low',
          evidenceSnippet(source === 'prompt' ? prompt : response, term),
        )
      }
    }
  }

  return hits.sort((left, right) => {
    if (left.sequenceIndex !== right.sequenceIndex) {
      return left.sequenceIndex - right.sequenceIndex
    }

    return featureSeverityOrder[left.severity] - featureSeverityOrder[right.severity]
  })
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
  selectedTurn,
  onSelectTurn,
}: {
  turns: TurnMetricDto[]
  selectedTurn: TurnMetricDto | null
  onSelectTurn: (turn: TurnMetricDto) => void
}) {
  const features = useMemo(() => detectFeatures(turns), [turns])
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

      const lineGeometry = new THREE.BufferGeometry().setFromPoints(positions)
      lineGeometries.push(lineGeometry)
      const lineMaterial = new THREE.LineBasicMaterial({
        color: 0x8bd3dd,
        transparent: true,
        opacity: 0.74,
      })
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
      lineGeometries.forEach((geometry) => geometry.dispose())
      host.replaceChildren()
    }
  }, [visibleTurns])

  return (
    <section className="phase-space-section" id="phase-space">
      <div className="phase-copy">
        <div>
          <p className="eyebrow">3D phase space</p>
          <h2>Conversation trajectory</h2>
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
  const [selectedDataSetId, setSelectedDataSetId] = useState('')
  const [analysis, setAnalysis] = useState<AnalyzeResponse | null>(null)
  const [selectedTurn, setSelectedTurn] = useState<TurnMetricDto | null>(null)
  const [isAnalyzing, setIsAnalyzing] = useState(false)
  const [isLoadingDataSet, setIsLoadingDataSet] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isMenuOpen, setIsMenuOpen] = useState(false)

  const closeMenu = () => setIsMenuOpen(false)

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
      const response = await fetch('/api/analyze', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ datasetJson }),
        signal: controller.signal,
      })

      if (!response.ok) {
        const message = await response.text()
        throw new Error(message || `Request failed with ${response.status}`)
      }

      const result = (await response.json()) as AnalyzeResponse
      setAnalysis(result)
      setSelectedTurn(result.turns[0] ?? null)
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

  return (
    <div className={isMenuOpen ? 'app-frame menu-open' : 'app-frame'}>
      <aside className="side-menu" aria-label="Dashboard navigation">
        <div className="side-menu-header">
          <div>
            <span className="menu-kicker">Djehuti</span>
            <strong>Analysis Console</strong>
          </div>
          <button className="icon-button menu-close" type="button" onClick={closeMenu} aria-label="Close menu">
            <X size={18} />
          </button>
        </div>
        <nav>
          {navItems.map((item) => {
            const Icon = item.icon
            return (
              <a key={item.href} href={item.href} onClick={closeMenu}>
                <Icon size={17} />
                <span>{item.label}</span>
                <ChevronRight size={15} />
              </a>
            )
          })}
        </nav>
        <div className="menu-status">
          <span>{analysis ? `${analysis.summary.turnCount} turns loaded` : 'No run loaded'}</span>
          <span>{analysis ? `${analysis.summary.velocityCount} velocities` : 'Waiting for JSON'}</span>
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
          <div>
            <p className="eyebrow">Djehuti Dashboard</p>
            <h1>Prompt-response measurement run</h1>
          </div>
        </div>
        <div className="topbar-actions">
          <label className="dataset-picker">
            <Database size={16} />
            <select
              aria-label="Dataset library"
              value={selectedDataSetId}
              onChange={(event) => setSelectedDataSetId(event.target.value)}
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
        </div>
      </header>

      <section className="summary-band" id="summary">
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

      <PhaseSpace3D turns={analysis?.turns ?? []} />

      <MetricTimelines turns={analysis?.turns ?? []} />

      <FeatureExplorer
        turns={analysis?.turns ?? []}
        selectedTurn={selectedTurn}
        onSelectTurn={setSelectedTurn}
      />

      <section className="workspace-grid">
        <div className="import-panel" id="input">
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
        </div>

        <div className="chart-panel" id="velocity">
          <div className="panel-heading">
            <Activity size={18} />
            <h2>Response velocity</h2>
          </div>
          <VelocityChart points={analysis?.velocities ?? []} />
        </div>
      </section>

      <section className="data-grid">
        <div className="table-panel" id="metrics">
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
        </div>

        <aside className="inspector-panel">
          <div className="panel-heading">
            <FileJson size={18} />
            <h2>Turn inspector</h2>
          </div>
          {selectedTurn ? (
            <div className="inspector-content">
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
        </aside>
      </section>

      <section className="visualization-panel" id="visualizations">
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

      {analysis && (
        <section className="status-band" id="context">
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
      )}
      </main>
    </div>
  )
}

export default App
