---
name: commit-changes
description: Stage and commit the current working-tree changes per the project's commit policy (Conventional Commits, no AI co-author lines, atomic commits, pre-commit build/test verification). Use when the user says "commit", "let's commit this", "make a commit", or similar.
---

# Commit current changes

Apply the commit policy from [CLAUDE.local.md](../../../CLAUDE.local.md#1-commit-policy). Read it before generating a message.

## Hard rules (do not violate)

1. **No `Co-Authored-By: Claude` / Anthropic / AI attribution lines.** Ever.
2. **No "Generated with Claude Code" trailers or emojis.**
3. **Never `git add .` or `git add -A`.** Stage explicit paths.
4. **Never commit secrets** (`.env*`, real credentials in `appsettings.*.json`).
5. **Never `--no-verify`** unless the user explicitly says so.
6. **Never amend a pushed commit.**

## Steps

### 1. Inspect the working tree

Run in parallel:
- `git status --porcelain`
- `git diff --stat` (unstaged)
- `git diff --cached --stat` (already staged, if any)
- `git log -5 --oneline` (to match the repo's commit style)
- `git rev-parse --abbrev-ref HEAD` (must NOT be `main`)

If on `main`, STOP. Ask the user to switch to a feature branch (suggest `/start-feature`) before committing.

### 2. Pre-commit verification

Decide what to run based on what changed:

| Changes touch | Run |
|---|---|
| Any `.cs` files | `dotnet build Odisea.sln` (must succeed) |
| Files under `tests/` or business logic (`FilterResolver`, `CollectionResolver`, endpoints) | `dotnet test Odisea.sln` (must pass) |
| `frontend/portal/**` | `cd frontend/portal && npx ng build --configuration=production` |
| `frontend/components/**` | `cd frontend/components && npm run build` |
| Only `*.md` / `*.gitignore` / `compose.yaml` | skip build/test |

If any check fails: STOP. Report what failed. Do not commit.

### 3. Plan the commit(s)

Look at the diff and decide: is this **one logical change** or **multiple**?

If multiple (e.g. a refactor + an unrelated bug fix + a doc tweak):
- Propose splitting into N separate commits with N distinct messages
- Ask the user to confirm the split before staging

If one logical change, continue.

### 4. Stage explicit paths

```sh
git add <path1> <path2> ...
```

Never `git add .`. If a file in the diff shouldn't be in this commit, skip it. If `.env*` / `appsettings.*.json` with secrets appear, warn the user — do not stage.

### 5. Craft the message

Format:
```
<type>(<scope>): <summary ≤ 72 chars, imperative, lowercase, no period>

<optional body — wrap at 72 chars, explain WHY, not WHAT>

<optional footer: refs #123, BREAKING CHANGE: ...>
```

- Match the style of recent `git log` output
- Body is optional. Use it when the WHY is non-obvious from the diff.
- **No co-author lines, no emojis, no AI attribution, no "Generated with" trailers.**

Show the message to the user and ask for confirmation before committing.

### 6. Commit using a heredoc

```sh
git commit -m "$(cat <<'EOF'
feat(catalog): add tag-in operator to filter resolver

Operators previously supported tag-contains for single-value matches.
Tag-in lets a collection match offers having any tag in a list,
required for the upcoming "destination cluster" collections.
EOF
)"
```

Use single-quoted heredoc (`<<'EOF'`) so `$`, backticks, etc. inside the message stay literal.

### 7. Verify

Run `git log -1 --format=fuller` and `git status`. Report:
- Commit SHA + message summary
- Files included
- Working tree state (should be clean for that scope)

## When the user has not explicitly asked

The system rule is: do not commit unless asked. If the user describes work that ends with "and that's done" or similar — that's not an ask to commit. Confirm first.

## Failure modes

- **Build fails** → report the failure, do not commit. Fix root cause first.
- **Tests fail** → same. Do not commit broken tests.
- **Pre-commit hook fails** → do not retry with `--no-verify`. Fix the underlying issue. Create a new commit; do not amend.
- **Mixed concerns in diff** → propose splitting before staging anything.
- **On `main`** → block. Require a feature branch.

## Do not

- Add `Co-Authored-By: Claude <noreply@anthropic.com>` (or any variant)
- Add "🤖 Generated with [Claude Code]" trailers
- Stage with `git add .`
- Push after committing (that's `finish-feature`)
- Commit `bin/`, `obj/`, `dist/`, `node_modules/`, `.angular/`
