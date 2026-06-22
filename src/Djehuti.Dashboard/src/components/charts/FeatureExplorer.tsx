import { useMemo } from 'react'
import { AlertTriangle } from 'lucide-react'
import { maxFeatureRailHits, maxFeatureRows } from '../../config/dashboard'
import { detectFeatures } from '../../features/featureFinder/featureDetection'
import { sampleEvenly } from '../../lib/format'
import type { AttractorEventDto, FeatureHit, FeatureSeverity, TurnMetricDto } from '../../types'

export function FeatureExplorer({
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
