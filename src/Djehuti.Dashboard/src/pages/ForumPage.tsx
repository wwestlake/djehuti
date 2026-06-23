import { useEffect, useState } from 'react'
import { forumApi, ForumCategory, ForumForum } from '../components/forum/forumApi'
import ForumCategoryList from '../components/forum/ForumCategoryList'

interface Props {
  onNavigateForum: (forumId: string) => void
}

export default function ForumPage({ onNavigateForum }: Props) {
  const [categories, setCategories] = useState<ForumCategory[]>([])
  const [forums, setForums] = useState<Record<string, ForumForum[]>>({})
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    forumApi.getCategories()
      .then(async cats => {
        setCategories(cats)
        const forumsMap: Record<string, ForumForum[]> = {}
        await Promise.all(cats.map(async cat => {
          forumsMap[cat.id] = await forumApi.getForums(cat.id)
        }))
        setForums(forumsMap)
      })
      .catch(() => setError('Failed to load forum'))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="forum-loading">Loading forum…</div>
  if (error) return <div className="forum-error">{error}</div>

  return (
    <div className="forum-page">
      <h1 className="forum-title">Community Forum</h1>
      {categories.length === 0
        ? <p className="forum-empty">No forum categories yet.</p>
        : categories.map(cat => (
            <ForumCategoryList
              key={cat.id}
              category={cat}
              forums={forums[cat.id] ?? []}
              onNavigateForum={onNavigateForum}
            />
          ))
      }
    </div>
  )
}
