import { useMemo } from 'react'
import { BarChart3 } from 'lucide-react'
import { MiniTimelineChart } from './MiniTimelineChart'
import type { TimelineSeries, TurnMetricDto } from '../../types'

export function MetricTimelines({ turns }: { turns: TurnMetricDto[] }) {
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
