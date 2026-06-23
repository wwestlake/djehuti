import type { ForumCategory, ForumForum } from './forumApi'

interface Props {
  category: ForumCategory
  forums: ForumForum[]
  onNavigateForum: (forumId: string) => void
}

export default function ForumCategoryList({ category, forums, onNavigateForum }: Props) {
  return (
    <div className="forum-category">
      <h2 className="forum-category-name">{category.name}</h2>
      {category.description && <p className="forum-category-desc">{category.description}</p>}
      <div className="forum-list">
        {forums.length === 0
          ? <p className="forum-empty">No forums in this category.</p>
          : forums.map(f => (
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
            ))
        }
      </div>
    </div>
  )
}
