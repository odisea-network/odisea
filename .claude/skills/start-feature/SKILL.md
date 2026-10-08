---
name: start-feature
description: Start a new feature, fix, refactor, or chore. Verifies clean working tree, pulls main, creates a properly named branch. Use when the user says "start a new feature", "let's work on X", or names a new piece of work. ALWAYS use this when beginning a discrete unit of work — every feature must live on its own branch.
---

# Start a feature branch

Apply the branch policy from [CLAUDE.local.md](../../../CLAUDE.local.md#2-branch-policy). Every feature, fix, or non-trivial change lives on its own branch.

## Args

The user may pass a description (`/start-feature add collection builder UI`). If no args, ask:
1. What's the change type? `feat` / `fix` / `refactor` / `chore` / `docs` / `test` / `perf` / `build` / `ci`
2. What's a 3-5 word description of the work?

## Steps

### 1. Verify working tree is clean

```sh
git status --porcelain
```

- If output is non-empty → STOP. Tell the user there are uncommitted changes and list them. Ask whether to commit (use `commit-changes` skill), stash, or discard. **Do not switch branches over uncommitted work.**
- If we're not in a git repo → STOP. Tell the user. Offer `git init` only if they confirm.

### 2. Confirm we're on main (or the configured base branch)

```sh
git rev-parse --abbrev-ref HEAD
```

- If not on `main` → ask: switch to main, or branch off the current branch? Default: switch to main.

### 3. Pull latest main

```sh
git checkout main
git pull --ff-only
```

If the fast-forward fails (local main diverged from remote), STOP and ask the user how to reconcile. Don't auto-rebase or merge.

### 4. Compute the branch name

Format: `<type>/<short-kebab-description>`

- `<type>` from user input — must be one of: `feat`, `fix`, `refactor`, `chore`, `docs`, `test`, `perf`, `build`, `ci`
- `<short-kebab-description>` — lowercase, hyphens, ≤ 40 chars
  - Strip filler words ("the", "a", "an", "add", "update" — usually redundant given the type prefix)
  - Drop articles. "Add the collection builder UI" → `feat/collection-builder-ui`

Show the proposed branch name and ask for confirmation before creating it.

### 5. Create and switch to the branch

```sh
git checkout -b <branch-name>
```

### 6. Confirm

Report:
- Branch created: `<name>`
- Based on: `main` at commit `<sha7>`
- Next: describe the work, then make edits. Commit with `/commit-changes` when ready.

## Failure modes

- **Uncommitted changes present** → don't switch. Offer commit/stash/discard.
- **Branch name conflicts with existing branch** → propose a suffix (`-2`, `-v2`) or ask for a different name.
- **`main` not the default base branch** → check `git symbolic-ref refs/remotes/origin/HEAD` and use that. If unset, ask the user.

## Do not

- Auto-commit anything
- Push the new branch to remote (push happens via `finish-feature`)
- Skip the clean-tree check
- Use any other branch naming scheme than `<type>/<kebab-desc>`
