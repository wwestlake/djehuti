import { Link } from 'react-router-dom'

export default function ElectronicsWorkBenchPage() {
  return (
    <div className="community-page electronics-workbench-page">
      <section className="electronics-workbench-hero">
        <p className="electronics-workbench-kicker">Djehuti Electronics Lab</p>
        <h1 className="community-page-title">Electronics Work Bench</h1>
        <p className="electronics-workbench-intro">
          A public home for the Djehuti Electronics Lab: circuit design,
          simulation, documentation, and engineering examples as the work bench
          moves toward release.
        </p>
      </section>

      <section className="electronics-workbench-docs" aria-label="Electronics Work Bench documentation">
        <div>
          <p className="electronics-workbench-kicker">Documentation</p>
          <h2>Djehuti User Guide</h2>
          <p>
            Read the first Electronics Work Bench manual in the browser, with
            section navigation, technical tables, and documentation figures
            preserved from DOC-030.
          </p>
        </div>
        <Link className="btn-primary" to="/electronics-work-bench/docs">
          Open Documentation
        </Link>
      </section>

      <section className="electronics-workbench-showcase" aria-label="Upcoming product showcase">
        <div className="electronics-workbench-preview" aria-hidden="true">
          <div className="electronics-workbench-preview-bar" />
          <div className="electronics-workbench-preview-grid">
            <span />
            <span />
            <span />
            <span />
            <span />
            <span />
          </div>
        </div>
        <div className="electronics-workbench-copy">
          <h2>Product Showcase Coming Soon</h2>
          <p>
            This space will introduce the Electronics Work Bench, highlight
            representative diagrams and simulation workflows, and connect to the
            documentation center as those pieces are published.
          </p>
        </div>
      </section>
    </div>
  )
}
