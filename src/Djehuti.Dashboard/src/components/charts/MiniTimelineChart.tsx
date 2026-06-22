import { maxTimelinePoints } from '../../config/dashboard'
import { formatNumber, sampleEvenly } from '../../lib/format'
import type { TimelineSeries } from '../../types'

export function MiniTimelineChart({ series }: { series: TimelineSeries }) {
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
