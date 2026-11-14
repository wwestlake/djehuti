import { useEffect } from 'react'

export interface PageSeoOptions {
  title: string
  description?: string
  image?: string
  url?: string
  type?: string
}

function upsertMeta(attr: 'name' | 'property', key: string, content: string) {
  if (typeof document === 'undefined') return
  let el = document.head.querySelector<HTMLMetaElement>(`meta[${attr}="${key}"]`)
  if (!el) {
    el = document.createElement('meta')
    el.setAttribute(attr, key)
    document.head.appendChild(el)
  }
  el.setAttribute('content', content)
}

/**
 * Sets the document title and Open Graph / Twitter card meta tags for the
 * current page. Pure client-side head update -- there is no server render
 * step for this app, so this mainly keeps the browser tab and any
 * JS-aware link preview in sync with what's actually on screen; the
 * default tags in index.html cover crawlers that don't execute JS.
 */
export default function usePageSeo({ title, description, image, url, type = 'website' }: PageSeoOptions) {
  useEffect(() => {
    const prevTitle = document.title
    document.title = title

    const resolvedUrl = url ?? window.location.href
    const resolvedImage = image ?? `${window.location.origin}/logo.png`

    upsertMeta('property', 'og:title', title)
    upsertMeta('property', 'og:url', resolvedUrl)
    upsertMeta('property', 'og:type', type)
    upsertMeta('property', 'og:image', resolvedImage)
    upsertMeta('name', 'twitter:card', 'summary_large_image')
    upsertMeta('name', 'twitter:title', title)
    upsertMeta('name', 'twitter:image', resolvedImage)

    if (description) {
      upsertMeta('property', 'og:description', description)
      upsertMeta('name', 'twitter:description', description)
    }

    return () => {
      document.title = prevTitle
    }
  }, [title, description, image, url, type])
}
