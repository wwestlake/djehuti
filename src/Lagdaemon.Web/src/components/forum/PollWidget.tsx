import { useState } from 'react'
import { forumApi } from '../../api/forumApi'
import type { PollData } from '../../api/forumApi'

interface Props {
  poll: PollData
  userId?: string
  onRefresh: () => void
}

export default function PollWidget({ poll, userId, onRefresh }: Props) {
  const [selected, setSelected] = useState<Set<string>>(new Set(poll.userVotes))
  const [voting, setVoting] = useState(false)
  const [voted, setVoted] = useState(poll.userVotes.length > 0)

  const totalVotes = poll.options.reduce((s, o) => s + o.voteCount, 0)
  const isClosed = poll.closesAt ? new Date(poll.closesAt) < new Date() : false

  const toggle = (id: string) => {
    if (isClosed || !userId) return
    if (poll.allowMultiple) {
      setSelected(prev => {
        const next = new Set(prev)
        if (next.has(id)) next.delete(id); else next.add(id)
        return next
      })
    } else {
      setSelected(new Set([id]))
    }
  }

  const castVote = async () => {
    if (!userId || selected.size === 0 || voting) return
    setVoting(true)
    try {
      await forumApi.votePoll(poll.id, [...selected])
      setVoted(true)
      onRefresh()
    } finally {
      setVoting(false)
    }
  }

  return (
    <div className="poll-widget">
      <div className="poll-question">{poll.question}</div>
      {isClosed && <div className="poll-closed-badge">Poll closed</div>}
      {poll.closesAt && !isClosed && (
        <div className="poll-closes">Closes {new Date(poll.closesAt).toLocaleDateString()}</div>
      )}
      <div className="poll-options">
        {poll.options.map(opt => {
          const pct = totalVotes > 0 ? Math.round((opt.voteCount / totalVotes) * 100) : 0
          const isSelected = selected.has(opt.id)
          const hasVoted = voted || isClosed
          return (
            <div
              key={opt.id}
              className={`poll-option${isSelected ? ' poll-option-selected' : ''}${!hasVoted && userId && !isClosed ? ' poll-option-clickable' : ''}`}
              onClick={() => !hasVoted && toggle(opt.id)}
            >
              <div className="poll-option-bar" style={{ width: hasVoted ? `${pct}%` : '0%' }} />
              <div className="poll-option-content">
                {!hasVoted && userId && !isClosed && (
                  <span className={`poll-option-dot${isSelected ? ' selected' : ''}`} />
                )}
                <span className="poll-option-text">{opt.text}</span>
                {hasVoted && <span className="poll-option-pct">{pct}% ({opt.voteCount})</span>}
              </div>
            </div>
          )
        })}
      </div>
      {!voted && !isClosed && userId && (
        <button className="primary-action poll-vote-btn" onClick={castVote} disabled={voting || selected.size === 0}>
          {voting ? 'Voting…' : 'Vote'}
        </button>
      )}
      <div className="poll-total">{totalVotes} vote{totalVotes !== 1 ? 's' : ''}</div>
    </div>
  )
}
