---
name: pick-task
description: Find the next task to work on from the Odisea project board, propose it with full context, and on confirm move it to In Progress and start a feature branch. Use when the user says "what should we work on", "pick next", "what's next", "next task", or names a board item by number. ALWAYS prefer this over inventing scope — the board is the source of truth (see CLAUDE.local.md §5).
---

# Pick the next task from the project board

The board at https://github.com/orgs/odisea-network/projects/1 is the source of truth for what work is open. Don't invent scope that isn't on the board — if the user describes truly new scope, create an issue and add it to the board first, then pick it.

## Steps

### 1. Query the board

```powershell
$env:Path = "C:\Program Files\GitHub CLI;$env:Path"
gh project item-list 1 --owner odisea-network --format json --limit 100 | ConvertFrom-Json
```

Each item carries `content.title`, `content.url`, `content.number`, `content.labels`, `status`, and `phase` (the custom field). Filter to items where `status` is `Todo` (or null — null counts as backlog).

### 2. Rank candidates

Apply these heuristics in order:

1. **Phase order**: Phase 0 work blocks Phase 1; Phase 1 blocks Phase 2; etc. Don't reach forward unless the user explicitly says so.
2. **The strategy doc's `immediate next slice`**: #95 — Backend solution scaffold (SharedKernel + module projects + Api host) is the named-next slice; #96–#98 follow it. Prefer it over other Phase 1 items unless an explicit blocker exists. (v2 roadmap: `C:\Users\arkan\.claude\plans\ok-so-you-will-melodic-hinton.md`)
3. **Dependency hints in issue bodies**: many epics reference earlier prerequisites in their `## References` section.
4. **User stated preference**: if the user named an area (`area:theme`, `area:auth`, `area:components`), filter to that.
5. **Atomic vs epic**: prefer concrete `type:task` items if any exist over `type:epic` (epics need decomposition first).

### 3. Propose the candidate

Reply with:

```
**Next:** #<num> — <title>
**Phase:** <phase>   **Area:** <area>   **Type:** <type>

<2-3 sentence summary of what it's about, drawn from the issue body's Context section>

**Why this one:** <one-line reason based on the ranking above>

**Suggested branch:** `<type>/<num>-<short-kebab-description>`

OK to proceed?
```

Wait for confirmation. Do not start before confirm.

### 4. On confirm — move to In Progress + start the branch

```powershell
# Move project item to In Progress
gh project item-edit `
  --id <item-id> `
  --project-id PVT_kwDOETQk884BZv6d `
  --field-id PVTSSF_lADOETQk884BZv6dzhUsb7E `
  --single-select-option-id 47fc9ee4
```

Status field/option IDs (from CLAUDE.local.md §5):
- Status field: `PVTSSF_lADOETQk884BZv6dzhUsb7E`
- Todo: `f75ad846` · In Progress: `47fc9ee4` · Done: `98236657`

Then invoke `start-feature` skill with the suggested branch name (`<type>/<num>-<short>`). The branch name MUST start with the issue number after the type prefix so the link is obvious from `git log` and PR title.

### 5. Commit + PR linking

Once you start editing, every commit body should include `Refs #<num>` so commits link to the issue. The PR title should be `<type>(<scope>): <summary>` and the PR body must include `Closes #<num>` so the issue auto-closes on merge.

When `finish-feature` runs, it will also flip the project item to `Done` (or you can do it manually after merge).

## When the user names a specific issue

If the user says "let's work on #22" or "the Theme epic", skip ranking — go straight to the propose step with that item.

## When the board is empty / nothing matches

Tell the user the board has no `Todo` items in the requested phase/area. Offer:
1. Pick a `Tech debt` item instead
2. Open the board in browser for them to add/edit
3. Reach forward to the next phase (only if explicit)

## Do not

- Invent work that isn't on the board
- Skip the confirmation step before moving status / branching
- Branch off `feat/...` when the issue is `fix/...` or `chore/...` — the branch type prefix must match the type label on the issue
- Pick an item already in `In Progress` without checking who assigned it (in parallel-agent setups, that means someone else is working on it — see CLAUDE.local.md on worktrees)
