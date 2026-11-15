import { useEffect } from 'react'

export type PageSeoOptions = {
  title: string
  description?: string
  /** Absolute or site-relative image URL. Defaults to the site-wide share image. */
  image?: string
}

const DEFAULT_DESCRIPTION =
  'Lag Daemon — W. Westlake · AI research on Information Space Dynamics and the Djehuti Cyberscope AI+ measurement instrument.'
const DEFAULT_IMAGE = '/cyberscope-analyze-hero.png'

function absoluteUrl(path: string): string {
  if (/^https?:\/\//i.test(path)) return path
  if (typeof window === 'undefined') return path
  return `${window.location.origin}${path.startsWith('/') ? '' : '/'}${path}`
}

function upsertMeta(attr: 'name' | 'property', key: string, content: string) {
  let el = document.head.querySelector<HTMLMetaElement>(`meta[${attr}="${key}"]`)
  if (!el) {
    el = document.createElement('meta')
    el.setAttribute(attr, key)
    document.head.appendChild(el)
  }
  el.setAttribute('content', content)
}

/**
 * Sets the document title and upserts Open Graph / Twitter card meta tags for the
 * current page. Client-side only (no SSR), so it keeps in-app tab titles and link
 * previews correct for anything that reads the live DOM (Web Share API, copy-link,
 * JS-aware crawlers); it does not change what a non-JS crawler sees on first paint —
 * that's covered by the static defaults in index.html.
 */
export function usePageSeo({ title, description, image }: PageSeoOptions) {
  useEffect(() => {
    const desc = description ?? DEFAULT_DESCRIPTION
    const img = absoluteUrl(image ?? DEFAULT_IMAGE)
    const url = window.location.href

    document.title = title

    upsertMeta('name', 'description', desc)

    upsertMeta('property', 'og:title', title)
    upsertMeta('property', 'og:description', desc)
    upsertMeta('property', 'og:image', img)
    upsertMeta('property', 'og:url', url)
    upsertMeta('property', 'og:type', 'website')

    upsertMeta('name', 'twitter:card', 'summary_large_image')
    upsertMeta('name', 'twitter:title', title)
    upsertMeta('name', 'twitter:description', desc)
    upsertMeta('name', 'twitter:image', img)
  }, [title, description, image])
}
