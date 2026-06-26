# Agent Rules — Djehuti Project

Rules for AI agents (Claude, Copilot, etc.) working in this repository.
**When the user gives you a new rule, add it here immediately.**

---

## Deployment

**Never place content directly on the server.**
All files — HTML, config, static assets, scripts — must live in this repository and be deployed exclusively through the GitHub Actions pipeline (`.github/workflows/deploy.yml`). No `scp`, no manual SSH file drops, no out-of-band changes to `/var/www` or `/opt`. If a file isn't in the repo, it doesn't exist in production.

---

## Branching & PRs

Step-by-step — follow this exactly every time, no exceptions:

1. **New feature or fix**: create a new branch from `develop`. Branch names must be descriptive and unique. Never reuse a branch that had a failed build or was already merged.
2. **Do the work** on that branch.
3. **Merge the branch into `develop`** directly — no PR needed for this step.
4. **To deploy**: create a PR from `develop → main`. Post the PR link to the user with "Action needed: please merge [URL]". Wait for the user to merge it.
5. **If the build fails after a merge to main**: create a new branch from `develop`, fix it, merge back to `develop`, then create a new `develop → main` PR.
6. **Never reuse a broken branch.** If a branch had a build failure or mistake, it is dead. Create a new one.
7. **Never merge your own PRs.** Never attempt `gh pr merge --admin` or `gh pr review --approve`. The user merges all PRs via the GitHub UI.
8. **Never push directly to `main`.**

---

## Pasting & Truncation

If a paste appears to be cut off, malformed, or incomplete (ends mid-sentence, mid-JSON, mid-thought), stop immediately and tell the user before doing any work with it. Do not proceed, trim, or guess at the missing content.

**Why:** User pasted a large JSON dataset that was truncated mid-record. Assistant proceeded silently, corrupting sample data.

---

## When Things Go Sideways

If something fails twice without a clear diagnosis, stop taking action and talk it through with the user. Do not blindly try a third approach.

---

## Communication Style

Use plain, precise language. Avoid slang and informal shorthand. The user has a DoD background and expects clear, unambiguous communication — define terms before using them, and do not assume shared informal vocabulary.

## Writing Rules

When the user states a new rule or preference, add it to this file (`AGENTS.md`) immediately — in the same response, before moving on. Do not rely solely on memory files. This file is the authoritative source of project-level rules for all agents.
