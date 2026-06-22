import { maxChartPoints } from '../../config/dashboard'
import { formatNumber, sampleEvenly } from '../../lib/format'
import type { VelocityPointDto } from '../../types'

export function VelocityChart({ points }: { points: VelocityPointDto[] }) {
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
