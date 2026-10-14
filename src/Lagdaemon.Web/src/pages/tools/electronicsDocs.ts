import { doc010 } from './manuals/doc010'
import { doc020 } from './manuals/doc020'
import { doc030 } from './manuals/doc030'
import { doc040 } from './manuals/doc040'
import { doc050 } from './manuals/doc050'
import { doc060 } from './manuals/doc060'
import { doc065 } from './manuals/doc065'
import { doc070 } from './manuals/doc070'
import { doc080 } from './manuals/doc080'
import { doc090 } from './manuals/doc090'

export type DocSection = {
  slug: string
  title: string
  summary: string
  content: string
}

export type DocManual = {
  slug: string
  title: string
  documentId: string
  revision: string
  status: string
  date?: string
  sections: DocSection[]
}

export type DocSearchResult = {
  manual: DocManual
  section: DocSection
  excerpt: string
  score: number
}

export const electronicsManuals: DocManual[] = [
  doc010,
  doc020,
  doc030,
  doc040,
  doc050,
  doc060,
  doc065,
  doc070,
  doc080,
  doc090,
]

export const firstElectronicsManual = electronicsManuals[0]
export const firstElectronicsSection = firstElectronicsManual.sections[0]

function searchableText(value: string) {
  return value
    .replace(/!\[[^\]]*\]\([^)]*\)/g, ' ')
    .replace(/\[([^\]]+)\]\([^)]*\)/g, '$1')
    .replace(/[`*_>#|]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
}

function searchTerms(query: string) {
  return query
    .trim()
    .toLocaleLowerCase()
    .split(/\s+/)
    .filter(Boolean)
}

function excerptFor(section: DocSection, terms: string[]) {
  const sources = [section.title, section.summary, searchableText(section.content)]
  const source = sources.find(value => terms.some(term => value.toLocaleLowerCase().includes(term))) ?? sources[2]
  const lower = source.toLocaleLowerCase()
  const firstMatch = terms
    .map(term => lower.indexOf(term))
    .filter(index => index >= 0)
    .sort((a, b) => a - b)[0]

  if (source.length <= 190 || firstMatch === undefined) return source

  const start = Math.max(0, firstMatch - 70)
  const end = Math.min(source.length, start + 190)
  return `${start > 0 ? '... ' : ''}${source.slice(start, end).trim()}${end < source.length ? ' ...' : ''}`
}

export function searchElectronicsManuals(query: string): DocSearchResult[] {
  const terms = searchTerms(query)
  if (terms.length === 0) return []

  return electronicsManuals
    .flatMap(manual => manual.sections.map(section => {
      const manualText = manual.title.toLocaleLowerCase()
      const titleText = section.title.toLocaleLowerCase()
      const summaryText = section.summary.toLocaleLowerCase()
      const contentText = searchableText(section.content).toLocaleLowerCase()
      const allText = `${manualText} ${titleText} ${summaryText} ${contentText}`

      if (!terms.every(term => allText.includes(term))) return null

      const score = terms.reduce((total, term) => total
        + (manualText.includes(term) ? 500 : 0)
        + (titleText.includes(term) ? 700 : 0)
        + (summaryText.includes(term) ? 250 : 0)
        + (contentText.includes(term) ? 100 : 0), 0)
        + (allText.includes(terms.join(' ')) ? 150 : 0)

      return { manual, section, excerpt: excerptFor(section, terms), score }
    }))
    .filter((result): result is DocSearchResult => result !== null)
    .sort((a, b) => b.score - a.score)
}
