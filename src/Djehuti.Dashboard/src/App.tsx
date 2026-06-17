import { useMemo, useState, type ReactNode } from 'react'
import ReactECharts from 'echarts-for-react'
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
  FileJson,
  Play,
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
  const [analysis, setAnalysis] = useState<AnalyzeResponse | null>(null)
  const [selectedTurn, setSelectedTurn] = useState<TurnMetricDto | null>(null)
  const [isAnalyzing, setIsAnalyzing] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const analyze = async () => {
    setIsAnalyzing(true)
    setError(null)

    try {
      const response = await fetch('/api/analyze', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ datasetJson }),
      })

      if (!response.ok) {
        const message = await response.text()
        throw new Error(message || `Request failed with ${response.status}`)
      }

      const result = (await response.json()) as AnalyzeResponse
      setAnalysis(result)
      setSelectedTurn(result.turns[0] ?? null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Analysis failed')
    } finally {
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

  const table = useReactTable({
    data: analysis?.turns ?? [],
    columns,
    getCoreRowModel: getCoreRowModel(),
  })

  const chartOptions = useMemo(
    () => ({
      backgroundColor: 'transparent',
      tooltip: {
        trigger: 'axis',
        valueFormatter: (value: number) => formatNumber(value),
      },
      grid: { left: 42, right: 18, top: 30, bottom: 34 },
      xAxis: {
        type: 'category',
        name: 't',
        data: analysis?.velocities.map((point) => point.sequenceIndex) ?? [],
        axisLine: { lineStyle: { color: '#667085' } },
        axisLabel: { color: '#526070' },
      },
      yAxis: {
        type: 'value',
        min: 0,
        max: 1,
        name: 'velocity',
        axisLine: { lineStyle: { color: '#667085' } },
        axisLabel: { color: '#526070' },
        splitLine: { lineStyle: { color: '#d9e2ec' } },
      },
      series: [
        {
          name: 'response velocity',
          type: 'line',
          smooth: false,
          symbolSize: 8,
          lineStyle: { width: 3, color: '#087f8c' },
          itemStyle: { color: '#087f8c' },
          areaStyle: { color: 'rgba(8, 127, 140, 0.12)' },
          data: analysis?.velocities.map((point) => point.value) ?? [],
        },
      ],
    }),
    [analysis],
  )

  return (
    <main className="dashboard-shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">Djehuti Dashboard</p>
          <h1>Prompt-response measurement run</h1>
        </div>
        <button className="primary-action" onClick={analyze} disabled={isAnalyzing}>
          <Play size={16} />
          {isAnalyzing ? 'Analyzing' : 'Analyze JSON'}
        </button>
      </header>

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

      <section className="workspace-grid">
        <div className="import-panel">
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

        <div className="chart-panel">
          <div className="panel-heading">
            <Activity size={18} />
            <h2>Response velocity</h2>
          </div>
          <ReactECharts
            option={chartOptions}
            style={{ height: 300, width: '100%' }}
            notMerge
          />
        </div>
      </section>

      <section className="data-grid">
        <div className="table-panel">
          <div className="panel-heading">
            <BarChart3 size={18} />
            <h2>Turn metrics</h2>
          </div>
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

      {analysis && (
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
      )}
    </main>
  )
}

export default App
