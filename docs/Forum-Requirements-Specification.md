# Forum Requirements Specification

## 1. Core Structural & Navigation Features

The platform must organize information logically to support discovery, high-velocity reading, and structured deep-dives.

### Hierarchical Taxonomies
Support for multi-nested categories (Categories → Sub-categories → Boards) to allow precise partitioning of technical or niche topics.

### Tagging Engine
A parallel, non-hierarchical categorization system allowing users to attach multiple metadata tags to a single thread. Must support:
- Tag-based filtering
- Intersection searches (e.g., threads tagged with both `F#` and `Database`)
- Tag descriptions

### Unified Global Search
A search engine utilizing tokenized text processing to index and query thread titles, body text, tags, and author profiles. Must support:
- Boolean operators (AND, OR, NOT)
- Author filters
- Date-range constraints

---

## 2. Rich Forum Toolsets & Engagement Components

### Advanced Content Editor
A WYSIWYG or optimized Markdown input interface supporting:
- Inline images and file attachments with automatic background compression
- Syntax-highlighted code blocks supporting major programming languages
- Blockquotes, tables, bulleted/numbered lists, embedded hyperlinks

### Reaction & Voting Subsystems
- **Thread Level:** Upvoting/downvoting or emoji reactions to gauge community sentiment without cluttering threads
- **Solution Marking:** Thread authors or moderators can flag a specific reply as the "Accepted Solution," automatically pinning a copy beneath the original question

### User Mentions & Cross-Referencing
- Dynamic `@username` triggers that link to user profiles and generate instant platform notifications
- Internal thread linking via `#ThreadID` that parses into the target thread title as an active hyperlink

### Polls & Surveys
An interface for users to embed multi-choice, single-choice, or anonymous polls directly within the initial post of a thread, tracking unique user votes at the data layer.

---

## 3. Moderation & Administrative Tools

### Inline Moderation Controls
Privileged users (Moderators and Administrators) must have immediate access to actions within the thread view:

**Thread Actions:**
- Pin / Sticky
- Lock (disable new replies)
- Move (transfer to a different category)
- Split (break a subset of replies into a new thread)
- Merge (combine two similar threads)

**Post Actions:**
- Hide / Soft-delete
- Edit content history
- Internal audit trails for edits

### Reporting Workflow Engine
Users can flag posts violating community guidelines. Flags route to a centralized queue showing:
- Reporter's rationale
- Link to offending content
- Quick resolution buttons: Dismiss, Keep, Warn Author, Delete

### User Ban & Restriction System
Graduated administrative penalties:
- **Read-Only State:** User can view but cannot post, reply, or edit
- **Temporary/Permanent Ban:** Complete block of account authentication
- **IP/Network Blocking:** Infrastructure-level blocks to prevent re-registration

### AI Persona Staff Policy
AI persona accounts are staff-branded site participants, not ordinary users. They must:
- Display a staff badge in the UI
- Be able to post as domain experts in their assigned areas
- Respond to direct questions, mentions, or persona-directed threads
- Participate in first-pass moderation triage for forum content

Persona moderation behavior must follow a severity spectrum:
- **Normal / acceptable:** no action beyond ordinary participation
- **Minor issue:** issue a professional moderator-style warning or guidance
- **Moderate issue:** mark the post `flagged` for human review
- **Serious issue:** mark the post `quarantined` so it is hidden pending review
- **Repeated or severe issue:** create an admin notice so a human moderator or administrator can act

Persona accounts must not be treated as the final enforcement authority. For severe, ambiguous, or repeated behavior, a human moderator or administrator remains the final decision-maker.

---

## 4. Notification & Subscription Engine

### Subscription Levels
Users can set their watch status per category or thread:
- **Watching:** Notified of every new thread or reply
- **Tracking:** Notified only when directly mentioned or replied to; thread shows in unread lists
- **Muted:** Completely hidden from unread counts and global feeds

### Multi-Channel Delivery
Configurable notification endpoints:
- **On-platform:** Live alert badges within the UI
- **Asynchronous:** Digest emails or instant transactional notifications

---

## Implementation Notes

The existing codebase already has a foundational forum layer:
- Categories, forums, threads, and posts (CRUD)
- Thread pinning and locking (admin/moderator)
- Post voting
- Solution marking (`markAsAnswer`)
- Context-based moderator roles (RBAC)

The epics above build on this foundation.
