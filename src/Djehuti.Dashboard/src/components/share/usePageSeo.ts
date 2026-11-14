import { useEffect } from 'react'

export type PageSeo = {
  title: string
  description?: string
  image?: string
}

const DEFAULT_DESCRIPTION =
  'Djehuti Cyberscope AI+ — measure and visualize Information Space Dynamics across conversation transcripts.'
const DEFAULT_IMAGE_PATH = '/djehuti/logo.png'

function upsertMeta(attr: 'name' | 'property', key: string, content: string) {
  let el = document.head.querySelector<HTMLMetaElement>(`meta[${attr}="${key}"]`)
  if (!el) {
    el = document.createElement('meta')
    el.setAttribute(attr, key)
    document.head.appendChild(el)
  }
  el.setAttribute('content', content)
}

/** Sets the document title and Open Graph / Twitter card meta tags for the current page. */
export function usePageSeo({ title, description, image }: PageSeo) {
  useEffect(() => {
    const url = window.location.href
    const desc = description ?? DEFAULT_DESCRIPTION
    const img = image ?? `${window.location.origin}${DEFAULT_IMAGE_PATH}`

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
