import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { ChevronDown, ChevronLeft, ChevronRight, Link as LinkIcon, Menu, X } from 'lucide-react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { Link, Navigate, useLocation, useParams } from 'react-router-dom'
import { electronicsManuals, firstElectronicsManual, firstElectronicsSection } from './electronicsDocs'
import type { DocManual } from './electronicsDocs'

function headingId(text: string) {
  return text
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/(^-|-$)/g, '')
}

function textFromChildren(children: ReactNode): string {
  if (typeof children === 'string' || typeof children === 'number') return String(children)
  if (Array.isArray(children)) return children.map(textFromChildren).join('')
  return ''
}

function markdownHeading(level: 2 | 3 | 4, children: ReactNode) {
  const text = textFromChildren(children)
  const id = headingId(text)
  const Tag = `h${level}` as const

  return (
    <Tag id={id} className="docs-content-heading">
      <a href={`#${id}`} aria-label={`Link to ${text}`}>
        <LinkIcon size={14} />
      </a>
      {children}
    </Tag>
  )
}

function sectionUrl(manualSlug: string, sectionSlug: string) {
  return `/electronics-work-bench/docs/${manualSlug}/${sectionSlug}`
}

function allSections(manual: DocManual) {
  return manual.sections.map(section => ({
    manualSlug: manual.slug,
    section,
  }))
}

export default function ElectronicsDocumentationPage() {
  const { manualSlug, sectionSlug } = useParams()
  const location = useLocation()
  const [tocOpen, setTocOpen] = useState(false)
  const [expandedManuals, setExpandedManuals] = useState<Set<string>>(() => new Set([firstElectronicsManual.slug]))

  const manual = useMemo(
    () => electronicsManuals.find(item => item.slug === manualSlug),
    [manualSlug],
  )
  const activeManual = manual ?? firstElectronicsManual
  const activeSection = activeManual.sections.find(item => item.slug === sectionSlug)
  const sections = allSections(activeManual)
  const currentIndex = activeSection ? sections.findIndex(item => item.section.slug === activeSection.slug) : -1
  const previous = currentIndex > 0 ? sections[currentIndex - 1] : null
  const next = currentIndex >= 0 && currentIndex < sections.length - 1 ? sections[currentIndex + 1] : null

  useEffect(() => {
    setTocOpen(false)
  }, [location.pathname])

  useEffect(() => {
    setExpandedManuals(prev => {
      if (prev.has(activeManual.slug)) return prev
      const nextSet = new Set(prev)
      nextSet.add(activeManual.slug)
      return nextSet
    })
  }, [activeManual.slug])

  if (!manualSlug || !sectionSlug) {
    return <Navigate to={sectionUrl(firstElectronicsManual.slug, firstElectronicsSection.slug)} replace />
  }

  if (!manual) {
    return (
      <div className="community-page docs-reader-shell docs-reader-empty">
        <Link className="breadcrumb-link" to="/electronics-work-bench">Electronics Work Bench</Link>
        <h1 className="community-page-title">Documentation Not Found</h1>
        <p>The requested manual is not available in the Electronics Work Bench documentation library.</p>
      </div>
    )
  }

  if (!activeSection) {
    return <Navigate to={sectionUrl(activeManual.slug, activeManual.sections[0].slug)} replace />
  }

  return (
    <div className="docs-reader-shell">
      <div className="docs-reader-header">
        <div>
          <Link className="breadcrumb-link" to="/electronics-work-bench">Electronics Work Bench</Link>
          <h1 className="community-page-title">Documentation</h1>
          <p>{activeManual.title}</p>
        </div>
        <button
          type="button"
          className="docs-mobile-toc-toggle"
          aria-expanded={tocOpen}
          onClick={() => setTocOpen(open => !open)}
        >
          {tocOpen ? <X size={16} /> : <Menu size={16} />}
          Contents
        </button>
      </div>

      <div className="docs-reader-layout">
        <aside className={`docs-toc${tocOpen ? ' open' : ''}`} aria-label="Documentation table of contents">
          <div className="docs-toc-label">Manuals</div>
          {electronicsManuals.map(item => {
            const isExpanded = expandedManuals.has(item.slug)
            const isActiveManual = item.slug === activeManual.slug

            return (
              <div className="docs-toc-manual" key={item.slug}>
                <button
                  type="button"
                  className={`docs-toc-manual-button${isActiveManual ? ' active' : ''}`}
                  aria-expanded={isExpanded}
                  onClick={() => {
                    setExpandedManuals(prev => {
                      const nextSet = new Set(prev)
                      if (nextSet.has(item.slug)) nextSet.delete(item.slug)
                      else nextSet.add(item.slug)
                      return nextSet
                    })
                  }}
                >
                  <ChevronDown size={14} />
                  <span>{item.title}</span>
                </button>
                {isExpanded && (
                  <div className="docs-toc-sections">
                    {item.sections.map(section => (
                      <Link
                        key={section.slug}
                        className={`docs-toc-link${isActiveManual && section.slug === activeSection.slug ? ' active' : ''}`}
                        to={sectionUrl(item.slug, section.slug)}
                      >
                        {section.title}
                      </Link>
                    ))}
                  </div>
                )}
              </div>
            )
          })}
        </aside>

        <article className="docs-content">
          <header className="docs-content-header">
            <div className="docs-manual-meta">
              <span>{activeManual.documentId}</span>
              <span>Rev {activeManual.revision}</span>
              <span>{activeManual.status}</span>
            </div>
            <h2 id={activeSection.slug}>
              <a href={`#${activeSection.slug}`} aria-label={`Link to ${activeSection.title}`}>
                <LinkIcon size={16} />
              </a>
              {activeSection.title}
            </h2>
            <p>{activeSection.summary}</p>
          </header>

          <div className="docs-markdown">
            <ReactMarkdown
              remarkPlugins={[remarkGfm]}
              components={{
                h2: ({ children }) => markdownHeading(2, children),
                h3: ({ children }) => markdownHeading(3, children),
                h4: ({ children }) => markdownHeading(4, children),
              }}
            >
              {activeSection.content}
            </ReactMarkdown>
          </div>

          <nav className="docs-prev-next" aria-label="Documentation section navigation">
            {previous ? (
              <Link className="docs-step docs-step-prev" to={sectionUrl(previous.manualSlug, previous.section.slug)}>
                <ChevronLeft size={16} />
                <span>
                  <small>Previous</small>
                  {previous.section.title}
                </span>
              </Link>
            ) : (
              <span className="docs-step docs-step-disabled">Start of manual</span>
            )}
            {next ? (
              <Link className="docs-step docs-step-next" to={sectionUrl(next.manualSlug, next.section.slug)}>
                <span>
                  <small>Next</small>
                  {next.section.title}
                </span>
                <ChevronRight size={16} />
              </Link>
            ) : (
              <span className="docs-step docs-step-disabled">End of manual</span>
            )}
          </nav>
        </article>
      </div>
    </div>
  )
}
