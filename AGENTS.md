# Agent Rules — Djehuti Project

Rules for AI agents (Claude, Copilot, etc.) working in this repository.
**When the user gives you a new rule, add it here immediately.**

---

## Deployment

**Never place content directly on the server.**
All files — HTML, config, static assets, scripts — must live in this repository and be deployed exclusively through the GitHub Actions pipeline (`.github/workflows/deploy.yml`). No `scp`, no manual SSH file drops, no out-of-band changes to `/var/www` or `/opt`. If a file isn't in the repo, it doesn't exist in production.

---

## Branching & PRs

- Feature branches merge directly to `develop` — no PR required.
- Only `develop → main` requires a PR. Claude opens the PR with a full write-up; the user approves and merges using **"Merge without waiting for requirements to be met (bypass rules)"** in the GitHub UI.
- Never attempt `gh pr merge --admin` or `gh pr review --approve` — GitHub blocks PR authors from approving their own PRs. The bypass button is the correct mechanism.
- The `gh` CLI is authenticated as the repo owner (wwestlake), so every PR Claude opens is authored by the user. Self-approval is always blocked by GitHub. This is by design.

---

## Pasting & Truncation

If a paste appears to be cut off, malformed, or incomplete (ends mid-sentence, mid-JSON, mid-thought), stop immediately and tell the user before doing any work with it. Do not proceed, trim, or guess at the missing content.

**Why:** User pasted a large JSON dataset that was truncated mid-record. Assistant proceeded silently, corrupting sample data.

---

## When Things Go Sideways

If something fails twice without a clear diagnosis, stop taking action and talk it through with the user. Do not blindly try a third approach.

---

## Writing Rules

When the user states a new rule or preference, add it to this file (`AGENTS.md`) immediately — in the same response, before moving on. Do not rely solely on memory files. This file is the authoritative source of project-level rules for all agents.
