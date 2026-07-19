# LiteSemRag API Protocol

Unified semantic context retrieval for all AI-enabled apps (Seshat, Architect, Learn, CreationStation, CyberScope).

## Authentication

All endpoints require:
- User session token (via `djehuti_auth` cookie or `Authorization: Bearer <token>`)
- Valid app account (via `X-App-Name` header)

## Endpoints

### 1. GET /api/semantic/context

**Purpose:** Retrieve combined context (app instructions + user history) for AI prompt building.

**Request:**
```http
GET /api/semantic/context?query=<string>&limit=<int>&app=<string>
Authorization: Bearer <session-token>
X-App-Name: seshat
```

**Query Parameters:**
- `query` (required): User's question or prompt. Used to find relevant context.
- `limit` (optional, default 5): Max chunks to return. Semantic graph returns top-K by relevance.
- `app` (optional): App name override. If not provided, read from `X-App-Name` header.
- `scopes` (optional): Comma-separated list of source types to search (e.g., "app-seshat,user-history"). Default: all.

**Response:**
```json
{
  "appInstructions": "## System Prompt\nSeshat is a math professor...",
  "userHistory": "Previous conversation snippets and relevant context...",
  "relevantChunks": [
    {
      "sourceType": "app-seshat",
      "sourceKey": "spinoza-examples-v1",
      "title": "Working Spinoza Code Examples",
      "content": "Example 1: ...\nExample 2: ...",
      "similarity": 0.92,
      "chunkPosition": 0
    }
  ],
  "totalTokensEstimate": 2847
}
```

**Status Codes:**
- `200` OK — Context retrieved
- `401` Unauthorized — Invalid session
- `403` Forbidden — App not authorized or user not in app's scope
- `404` Not Found — App account not found
- `500` Server Error — Semantic graph error

---

### 2. POST /api/semantic/save-conversation

**Purpose:** Save a conversation to S3 for later retrieval/resumption.

**Request:**
```http
POST /api/semantic/save-conversation
Authorization: Bearer <session-token>
Content-Type: application/json

{
  "appName": "seshat",
  "title": "CERN Data Analysis - Session 3",
  "turns": [
    { "role": "user", "content": "Analyze invariant mass..." },
    { "role": "assistant", "content": "I'll help with that..." }
  ]
}
```

**Response:**
```json
{
  "conversationId": "550e8400-e29b-41d4-a716-446655440000",
  "title": "CERN Data Analysis - Session 3",
  "s3Path": "s3://djehuti-conversations/seshat/550e8400.json",
  "createdAt": "2026-07-19T10:30:00Z",
  "turnCount": 2
}
```

**Status Codes:**
- `201` Created — Conversation saved
- `400` Bad Request — Invalid turns format
- `401` Unauthorized
- `500` S3 write error

---

### 3. GET /api/semantic/conversations

**Purpose:** List all saved conversations for the current user.

**Request:**
```http
GET /api/semantic/conversations?app=seshat&limit=20&offset=0
Authorization: Bearer <session-token>
```

**Query Parameters:**
- `app` (optional): Filter by app name
- `limit` (optional, default 20): Page size
- `offset` (optional, default 0): Pagination offset

**Response:**
```json
{
  "conversations": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "appName": "seshat",
      "title": "CERN Data Analysis - Session 3",
      "turnCount": 42,
      "createdAt": "2026-07-19T10:30:00Z",
      "updatedAt": "2026-07-19T11:45:00Z"
    }
  ],
  "total": 47,
  "limit": 20,
  "offset": 0
}
```

---

### 4. GET /api/semantic/conversations/{conversationId}

**Purpose:** Load a saved conversation.

**Request:**
```http
GET /api/semantic/conversations/550e8400-e29b-41d4-a716-446655440000
Authorization: Bearer <session-token>
```

**Response:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "title": "CERN Data Analysis - Session 3",
  "appName": "seshat",
  "turns": [
    { "role": "user", "content": "Analyze invariant mass..." },
    { "role": "assistant", "content": "I'll help with that..." }
  ],
  "createdAt": "2026-07-19T10:30:00Z"
}
```

---

### 5. DELETE /api/semantic/conversations/{conversationId}

**Purpose:** Delete a saved conversation.

**Request:**
```http
DELETE /api/semantic/conversations/550e8400-e29b-41d4-a716-446655440000
Authorization: Bearer <session-token>
```

**Response:**
```json
{
  "deleted": true,
  "id": "550e8400-e29b-41d4-a716-446655440000"
}
```

---

## App Accounts

Each AI-enabled app registers an `application_account`:

| App | User ID | App Name | Notes |
|-----|---------|----------|-------|
| Seshat (DjeLab AI) | `<guid>` | `seshat` | Math professor for analysis |
| Architect (C4 diagrams) | `<guid>` | `architect` | Architecture design assistant |
| CreationStation | `<guid>` | `creation-station` | Creative writing & content |
| Learn (Tutor) | `<guid>` | `learn` | Educational tutor |
| CyberScope | `<guid>` | `cyberscope` | Security analysis |
| System (shared bot context) | `00000000-0000-0000-0000-000000000001` | `system` | Shared across all bots |

App instructions are stored in `semantic_documents` with:
- `user_id` = app account user_id
- `source_type` = `app-{appName}` (e.g., `app-seshat`)
- `content` = System prompt, examples, guidelines

When an app calls `/api/semantic/context`:
1. Load docs with `user_id = app_account.user_id` and `source_type = app-{appName}`
2. Load docs with `user_id = current_user.id` (user's history)
3. Combine and return to app

---

## Implementation Notes

- **Token Estimation:** `totalTokensEstimate` is a rough calculation (approx 1 token per 4 chars). Use for display only; actual token counts vary by model.
- **S3 Path Format:** `s3://djehuti-conversations/{appName}/{userId}/{conversationId}.json`
- **User Scoping:** All endpoints are scoped to `current_user.id` except when accessing shared `system_user` context.
- **Rate Limiting:** Conversations API is rate-limited (10 saves/min per user). Context retrieval has no hard limit but should be called once per prompt, not per token.

---

## Example: Seshat Flow

```
1. User enters prompt in Seshat UI
2. Seshat calls: GET /api/semantic/context?query={prompt}&app=seshat
3. API returns:
   - Seshat's system prompt (app instructions)
   - User's conversation history (recent turns)
   - Relevant Spinoza examples (matched by semantic similarity)
4. Seshat combines into final prompt:
   [App instructions] + [User history] + [Examples] + [Current prompt]
5. Seshat sends to AI (via BYOK OpenAI/Claude key)
6. On save, Seshat calls: POST /api/semantic/save-conversation
7. Conversation stored to S3, indexed in conversations table
8. User can list/load/delete via GET/DELETE /api/semantic/conversations/*
```

---

## Headers

All endpoints accept:
- `X-App-Name`: Override app name (optional, defaults to resolved from token)
- `X-Request-Id`: Optional correlation ID for logging

