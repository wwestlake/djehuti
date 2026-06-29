import { useState, useMemo } from 'react'

export interface ColDef<T> {
  key: string
  label: string
  sortable?: boolean
  /** Extract a comparable value for sorting */
  sortVal?: (row: T) => string | number
  /** Custom cell renderer — return JSX or string */
  render?: (row: T) => React.ReactNode
}

interface Props<T> {
  columns: ColDef<T>[]
  data: T[]
  rowKey: (row: T) => string
  pageSize?: number
  searchKeys?: (keyof T)[]
  emptyText?: string
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function AdminTable<T extends Record<string, any>>({
  columns,
  data,
  rowKey,
  pageSize = 25,
  searchKeys = [],
  emptyText = 'No data.',
}: Props<T>) {
  const [query, setQuery] = useState('')
  const [sortCol, setSortCol] = useState<string | null>(null)
  const [sortAsc, setSortAsc] = useState(true)
  const [page, setPage] = useState(1)

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q || searchKeys.length === 0) return data
    return data.filter(row =>
      searchKeys.some(k => String(row[k] ?? '').toLowerCase().includes(q))
    )
  }, [data, query, searchKeys])

  const sorted = useMemo(() => {
    if (!sortCol) return filtered
    const col = columns.find(c => c.key === sortCol)
    if (!col) return filtered
    const getter = col.sortVal ?? ((row: T) => String(row[sortCol] ?? ''))
    return [...filtered].sort((a, b) => {
      const av = getter(a), bv = getter(b)
      if (av < bv) return sortAsc ? -1 : 1
      if (av > bv) return sortAsc ? 1 : -1
      return 0
    })
  }, [filtered, sortCol, sortAsc, columns])

  const totalPages = Math.max(1, Math.ceil(sorted.length / pageSize))
  const safePage = Math.min(page, totalPages)
  const pageData = sorted.slice((safePage - 1) * pageSize, safePage * pageSize)

  const toggleSort = (key: string) => {
    if (sortCol === key) setSortAsc(a => !a)
    else { setSortCol(key); setSortAsc(true) }
    setPage(1)
  }

  const handleSearch = (v: string) => { setQuery(v); setPage(1) }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      {searchKeys.length > 0 && (
        <input
          className="papers-new-input"
          placeholder="Search…"
          value={query}
          onChange={e => handleSearch(e.target.value)}
          style={{ maxWidth: 320 }}
        />
      )}
      <div className="admin-table-wrap">
        <table className="admin-table">
          <thead>
            <tr>
              {columns.map(col => (
                <th
                  key={col.key}
                  onClick={col.sortable !== false ? () => toggleSort(col.key) : undefined}
                  style={col.sortable !== false ? { cursor: 'pointer', userSelect: 'none', whiteSpace: 'nowrap' } : undefined}
                >
                  {col.label}
                  {col.sortable !== false && (
                    <span style={{ marginLeft: 4, opacity: sortCol === col.key ? 1 : 0.3, fontSize: '0.75em' }}>
                      {sortCol === col.key ? (sortAsc ? '▲' : '▼') : '▲'}
                    </span>
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {pageData.length === 0 ? (
              <tr><td colSpan={columns.length} style={{ textAlign: 'center', color: 'var(--text-muted)', padding: '16px 0' }}>{emptyText}</td></tr>
            ) : pageData.map(row => (
              <tr key={rowKey(row)}>
                {columns.map(col => (
                  <td key={col.key}>
                    {col.render ? col.render(row) : String(row[col.key] ?? '')}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {totalPages > 1 && (
        <div className="admin-pagination">
          <button className="admin-action-btn" disabled={safePage <= 1} onClick={() => setPage(p => p - 1)}>← Prev</button>
          <span>Page {safePage} of {totalPages} ({sorted.length} rows)</span>
          <button className="admin-action-btn" disabled={safePage >= totalPages} onClick={() => setPage(p => p + 1)}>Next →</button>
        </div>
      )}
    </div>
  )
}
