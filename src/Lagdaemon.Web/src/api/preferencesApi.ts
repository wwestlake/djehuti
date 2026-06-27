const API = '/djehuti/api'
const opts = { credentials: 'include' as RequestCredentials }

export type UserPrefs = Record<string, string | boolean | number>

export const PREF_DEFAULTS: UserPrefs = {
  // Email notifications
  email_notify_replies: false,
  email_notify_mentions: true,
  email_notify_achievements: true,
  email_notify_announcements: false,
  email_notify_blog_comments: true,
  email_notify_paper_collaborators: true,
  // In-app notifications
  inapp_notify_replies: true,
  inapp_notify_mentions: true,
  inapp_notify_achievements: true,
  inapp_notify_announcements: true,
  // Forum
  forum_default_subscription: 'tracking',
  forum_show_bot_posts: true,
  forum_show_achievements_on_profile: true,
  forum_compact_view: false,
  forum_signature: '',
  // Blog
  blog_default_visibility: 'public',
  blog_autosave_interval: '1min',
  blog_allow_comments: true,
  blog_comment_notify: true,
  blog_editor_mode: 'rich',
  // Papers
  papers_default_visibility: 'private',
  papers_collab_notify: true,
  papers_comment_notify: true,
  papers_show_word_count: true,
  papers_citation_style: 'APA',
  // Privacy
  privacy_show_online_status: true,
  privacy_show_profile_public: true,
  privacy_index_posts: true,
}

export const preferencesApi = {
  getPreferences: (): Promise<UserPrefs> =>
    fetch(`${API}/users/me/preferences`, opts)
      .then(r => r.ok ? r.json() : { ...PREF_DEFAULTS }),

  patchPreferences: (patch: Partial<UserPrefs>): Promise<UserPrefs> =>
    fetch(`${API}/users/me/preferences`, {
      ...opts, method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(patch),
    }).then(r => r.json()),
}
