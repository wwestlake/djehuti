import { useEffect, useMemo, useRef, useState } from 'react'
import * as THREE from 'three'
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js'
import { ConvexGeometry } from 'three/examples/jsm/geometries/ConvexGeometry.js'
import { maxPhasePoints, phaseRenderModes, strategyColors } from '../../config/dashboard'
import { sampleEvenly } from '../../lib/format'
import type { PhaseRenderMode, TurnMetricDto } from '../../types'

export function PhaseSpace3D({ turns }: { turns: TurnMetricDto[] }) {
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
