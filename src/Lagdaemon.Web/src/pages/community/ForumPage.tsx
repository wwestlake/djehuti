import { useEffect, useState } from 'react'
import { forumApi } from '../../api/forumApi'
import type { ForumCategory, ForumForum } from '../../api/forumApi'

interface Props {
  onNavigateForum: (forumId: string) => void
  onNavigateSearch?: () => void
}

function ForumCategoryList({ category, forums, subCategories, subForums, onNavigateForum, depth = 0 }: {
  category: ForumCategory
  forums: ForumForum[]
  subCategories: ForumCategory[]
  subForums: Record<string, ForumForum[]>
  onNavigateForum: (forumId: string) => void
  depth?: number
}) {
  return (
    <div className={`forum-category${depth > 0 ? ' forum-subcategory' : ''}`}>
      <h2 className={`forum-category-name${depth > 0 ? ' forum-subcategory-name' : ''}`}>{category.name}</h2>
      {category.description && <p className="forum-category-desc">{category.description}</p>}
      <div className="forum-list">
        {forums.map(f => (
          <button key={f.id} className="forum-list-item" onClick={() => onNavigateForum(f.id)}>
            <div className="forum-list-item-info">
              <span className="forum-list-item-name">{f.name}</span>
              {f.description && <span className="forum-list-item-desc">{f.description}</span>}
            </div>
            <div className="forum-list-item-stats">
              <span>{f.threadCount} threads</span>
              <span>{f.postCount} posts</span>
            </div>
          </button>
        ))}
        {forums.length === 0 && subCategories.length === 0 && (
          <p className="forum-empty">No forums in this category.</p>
        )}
      </div>
      {subCategories.map(sub => (
        <ForumCategoryList
          key={sub.id}
          category={sub}
          forums={subForums[sub.id] ?? []}
          subCategories={[]}
          subForums={subForums}
          onNavigateForum={onNavigateForum}
          depth={depth + 1}
        />
      ))}
    </div>
  )
}

export default function ForumPage({ onNavigateForum, onNavigateSearch }: Props) {
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
  if (error) return <div className="forum-error-msg">{error}</div>

  const topLevel = categories.filter(c => !c.parentCategoryId)
  const childMap: Record<string, ForumCategory[]> = {}
  categories.filter(c => c.parentCategoryId).forEach(c => {
    const pid = c.parentCategoryId!
    childMap[pid] = [...(childMap[pid] ?? []), c]
  })

  return (
    <div className="community-page">
      <div className="forum-header-row">
        <h1 className="community-page-title">Community Forum</h1>
        {onNavigateSearch && (
          <button className="forum-search-nav-btn" onClick={onNavigateSearch}>🔍 Search</button>
        )}
      </div>
      {topLevel.length === 0
        ? <p className="forum-empty">No forum categories yet.</p>
        : topLevel.map(cat => (
            <ForumCategoryList
              key={cat.id}
              category={cat}
              forums={forums[cat.id] ?? []}
              subCategories={childMap[cat.id] ?? []}
              subForums={forums}
              onNavigateForum={onNavigateForum}
            />
          ))
      }
    </div>
  )
}
