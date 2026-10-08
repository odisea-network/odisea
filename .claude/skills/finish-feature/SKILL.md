---
name: finish-feature
description: Finish the current feature branch — run final build and tests, push to remote, optionally open a pull request. Use when the user says "finish this", "push this", "wrap up the feature", "open a PR", or similar. Do NOT use mid-feature; this is the closing step.
---

# Finish a feature branch

Closing step for work started with `start-feature`. Verifies everything is green, then pushes (and optionally PRs).

## Hard rules

1. **Never push to `main`** without explicit permission.
2. **Never force-push** unless the user explicitly asks for it. Never force-push to a shared branch.
3. **Never bypass hooks** (`--no-verify`).
4. **Do not open a PR** unless the user asked for one (or args contain `--pr`).

## Args

- (none) → push the branch, do not open a PR
- `--pr` → push and open a pull request
- `--pr-title "..."` / `--pr-body "..."` → override defaults

## Steps

### 1. Sanity checks

```sh
git rev-parse --abbrev-ref HEAD     # must NOT be main
git status --porcelain              # must be empty
git log main..HEAD --oneline        # must be non-empty (commits to push)
```

- If on `main` → STOP. Tell the user.
- If working tree dirty → STOP. Ask: commit (use `commit-changes`), stash, or discard.
- If zero commits ahead of main → STOP. Nothing to push.

### 2. Final build + test

Apply the same matrix as `commit-changes` but always run:

| If commits in this branch touch | Run |
|---|---|
| Any `.cs` | `dotnet build Odisea.sln` |
| `.cs` under `src/Odisea.Application`, `Odisea.Domain`, or `tests/` | `dotnet test Odisea.sln` |
| `frontend/portal/**` | `cd frontend/portal && npx ng build --configuration=production` |
| `frontend/components/**` | `cd frontend/components && npm run build` |

If any fails → STOP. Report. Do not push.

### 3. Push

```sh
git push -u origin <branch-name>
```

`-u` sets upstream tracking on first push. If the branch is already tracked, drop `-u`.

If push is rejected (non-fast-forward), STOP. Ask the user how to reconcile — don't auto-rebase or force-push.

### 4. (Optional) Open PR

Only if user passed `--pr` or asked verbally. Skip otherwise.

Check `gh` availability: `gh --version`. If missing, tell the user to create the PR through the GitHub UI.

```sh
gh pr create \
  --base main \
  --title "<title>" \
  --body "$(cat <<'EOF'
## Summary
<2-4 bullets of what changed and why>

## Test plan
- [ ] <how to verify>
- [ ] <edge cases>
EOF
)"
```

PR title rules:
- Match the primary commit's `<type>(<scope>): <summary>` format
- ≤ 72 chars
- No emojis, no AI attribution

PR body rules:
- **No `🤖 Generated with [Claude Code]` footer.** Ever.
- **No `Co-Authored-By` lines.** Ever.
- Summary section explains *why* the change exists
- Test plan section is a checklist the reviewer can actually run
- Reference related issues with `Refs #123` or `Closes #123`

### 5. Link to the project board

If the branch is tied to a board issue (branch name starts with `<type>/<num>-...` or any commit body contains `Refs #<num>`), do these in order:

1. Move the project item from `In Progress` → `In Review` if that column exists, else leave at `In Progress` until merge.
2. Make sure the PR body has `Closes #<num>` (GitHub will auto-close the issue on merge).
3. After the PR merges, move the project item to `Done`:

```powershell
$env:Path = "C:\Program Files\GitHub CLI;$env:Path"
gh project item-edit `
  --id <item-id> `
  --project-id PVT_kwDOETQk884BZv6d `
  --field-id PVTSSF_lADOETQk884BZv6dzhUsb7E `
  --single-select-option-id 98236657
```

(`98236657` = Done. Status field ID and other option IDs are in CLAUDE.local.md §5.)

To find the item ID for an issue URL:
```powershell
gh project item-list 1 --owner odisea-network --format json --limit 100 |
  ConvertFrom-Json | Select-Object -ExpandProperty items |
  Where-Object { $_.content.url -eq '<issue-url>' } |
  Select-Object -ExpandProperty id
```

If the branch isn't tied to a board issue (rare — most work should be), skip this step.

### 6. Confirm

Report:
- Branch pushed: `origin/<branch>`
- Commits pushed: `<count>` (list the summaries)
- PR URL (if created)
- Board issue linked: `#<num>` (status moved if applicable)
- Suggested next step: review locally, then merge through the GitHub UI

## Failure modes

- **Build / tests fail** → block. Don't push broken code.
- **Push rejected** → don't force. Ask the user how to reconcile.
- **PR creation fails** (`gh` not auth'd, no remote, etc.) → tell the user; don't retry blindly.
- **Branch already merged** → tell the user; suggest deleting the local branch.

## After merge (separate manual step — not automated here)

The user should, after the PR merges:
```sh
git checkout main
git pull --ff-only
git branch -d <branch-name>
```

## Do not

- Push to `main` directly
- Force-push to shared branches
- Add `--no-verify` to bypass hooks
- Auto-merge the PR
- Add Claude / Anthropic / AI attribution anywhere
- Add `🤖 Generated with [Claude Code]` to the PR body
