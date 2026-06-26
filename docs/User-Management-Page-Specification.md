# User Management Page Specification

## 1. Data Schema & Model Requirements

| Field | Type | Description |
|-------|------|-------------|
| User ID | UUID | Primary key |
| Email Address | string (unique) | Authentication and admin communication |
| Display Name | string | Public-facing name for posts and comments |
| Role | enum | Administrator, Moderator, Author, User/Member |
| Account Status | enum | Active, Suspended, Unverified |
| CreatedAt | timestamp | Account creation date |
| LastLoginAt | timestamp | Last successful login (auditing) |

## 2. Interface Layout & UI Components

- **User Data Table:** Grid displaying Display Name, Email, Role, Status, and Creation Date.
- **Search and Filter Bar:** Text search on Email/Display Name; dropdown filters for Role and Status.
- **Action Modals:** Contextual overlays for edit, create, and delete — no page reloads.

## 3. Core Administrative Operations

| Operation | Endpoint | Description |
|-----------|----------|-------------|
| Read | `GET /api/admin/users?page=&pageSize=&search=&role=&status=` | Paginated, filterable |
| Update | `PATCH /api/admin/users/{id}` | Edit display name, role, status |
| Create | `POST /api/admin/users/invite` | Send invitation email with predefined role |
| Suspend | `PATCH /api/admin/users/{id}/suspend` | Blocks login, preserves data |
| Hard Delete | `DELETE /api/admin/users/{id}` | Permanent erasure |

## 4. Security & Access Control

- **RBAC:** Every endpoint verifies Administrator role from JWT before executing.
- **Input Validation:** Server-side sanitization of email and display name.
- **Audit Logging:** Every admin action logs: administrator ID, target user ID, field changed, old/new value, timestamp.
