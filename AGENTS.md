# Agent Rules — Djehuti Project

Rules for AI agents (Claude, Copilot, etc.) working in this repository.
**When the user gives you a new rule, add it here immediately.**

---

## Deployment

**Never place content directly on the server.**
All files — HTML, config, static assets, scripts — must live in this repository and be deployed exclusively through the GitHub Actions pipeline (`.github/workflows/deploy.yml`). No `scp`, no manual SSH file drops, no out-of-band changes to `/var/www` or `/opt`. If a file isn't in the repo, it doesn't exist in production.

---

## Database Migrations

**Claude runs all migrations — never ask the user to do it.**

The PEM key is at `D:\0000 Turo Business\AppDev\secure\KwestKarz.pem`. The server is `ubuntu@kwestkarz.com`. The database is `djehuti`, owned by `djehuti`, accessed via `sudo -u postgres psql -d djehuti`.

When a migration is needed:
1. Write the `.sql` file to `migrations/` in the repo.
2. SSH to the server using the PEM key and run the migration directly.
3. Confirm success before creating the PR.

Never put migration instructions in the PR body and tell the user to run them. Claude does it.

---

## Branching & PRs

Step-by-step — follow this exactly every time, no exceptions:

1. **New feature or fix**: create a new branch from `develop`. Branch names must be descriptive and unique. Never reuse a branch that had a failed build or was already merged.
2. **Do the work** on that branch.
3. **Merge the branch into `develop`** directly — no PR needed for this step. Claude may merge its own branches into `develop` without user approval.
4. **To deploy**: create a PR from `develop → main`. Post the PR link to the user with "Action needed: please merge [URL]". Wait for the user to merge it. Claude must never merge `develop → main`.
5. **If the build fails after a merge to main**: create a new branch from `develop`, fix it, merge back to `develop`, then create a new `develop → main` PR.
6. **Never reuse a broken branch.** If a branch had a build failure or mistake, it is dead. Create a new one.
7. **Never merge your own PRs.** Never attempt `gh pr merge --admin` or `gh pr review --approve`. The user merges all PRs via the GitHub UI.
8. **Never push directly to `main`.**
9. **Open PRs ready for review by default.** Do not create draft PRs unless the user explicitly asks for a draft.
10. **Before reporting a PR to the user, check the PR merge state for conflicts.** If it is conflicted, resolve the conflicts first and only then report the PR link.

---

## Pasting & Truncation

If a paste appears to be cut off, malformed, or incomplete (ends mid-sentence, mid-JSON, mid-thought), stop immediately and tell the user before doing any work with it. Do not proceed, trim, or guess at the missing content.

**Why:** User pasted a large JSON dataset that was truncated mid-record. Assistant proceeded silently, corrupting sample data.

---

## When Things Go Sideways

If something fails twice without a clear diagnosis, stop taking action and talk it through with the user. Do not blindly try a third approach.

If `git` or GitHub fails, stop immediately and notify the user before making further assumptions or retries.

Known GitHub auth recovery command on this machine:
`gh auth login -h github.com --git-protocol https --web`

Known local Git credential fix on this machine:
the user-level `credential.helper` was set to `manager-core` and was corrected to `manager` because the installed Git credential helper name differed.

---

## Communication Style

Use plain, precise language. Avoid slang and informal shorthand. The user has a DoD background and expects clear, unambiguous communication — define terms before using them, and do not assume shared informal vocabulary.

**Always include clickable PR links** — When creating a pull request, always provide the full GitHub URL link in the format `https://github.com/wwestlake/djehuti/pull/{number}`. Make it easy for the user to open and review.

## Session Startup

**At the start of every session, read `AGENTS.md` before doing any work.** If the user has not already prompted this, ask them to say "read agents.md" or read it proactively. Do not rely on memory files alone — this file is the authoritative source of project rules and overrides any remembered behavior from prior sessions.

---

## Writing Rules

When the user states a new rule or preference, add it to this file (`AGENTS.md`) immediately — in the same response, before moving on. Do not rely solely on memory files. This file is the authoritative source of project-level rules for all agents.

---

## Privacy

**Never display email addresses anywhere in the UI.**
Display names are optional. The fallback chain is: `user_profiles.display_name` → `users.display_name` → `'Anonymous'`. Email is never a fallback. Exposing email addresses is a privacy violation.

---

## Precision in Communication

**Use specific numbers, not vague generalizations.**
Say "4 articles" not "all articles." Say "3 PRs" not "several PRs." When quantity matters — and it usually does — state the exact number. Vague terms like "all", "some", "a few", or "several" are not acceptable when the precise count is known.

---

## Look It Up — Don't Guess

**Before answering any question about how the repo, deploy pipeline, server config, or environment works — look it up.**

SSH access is available via the PEM key. The deploy workflow is in `.github/workflows/deploy.yml`. The server runs at kwestkarz.com. Read the actual files rather than assuming or reasoning from memory.

**Why:** Claude stated confidently that an env var "goes in api.env on the server" without checking how api.env is actually written. It is generated entirely by the GitHub Actions deploy step from GitHub secrets — a direct server edit would be overwritten on the next deploy.

**How to apply:** Any time a question involves deployment, environment variables, server configuration, or repo structure — open the relevant file first, then answer.
