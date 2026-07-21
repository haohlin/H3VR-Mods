# H3VR-Mods agent instructions

## Branch and worktree lifecycle

- Use one dedicated branch and worktree for each active change.
- Before cleanup, fetch and prove branch tip is merged into `origin/main`, no open pull request uses it, and associated worktree has no tracked or untracked files.
- For release branches, complete build/package/deploy/runtime validation and official delivery first; then merge exact shipped commit into `main`, push `main`, and perform cleanup.
- Cleanup sequence: remove clean worktree, delete merged local branch, delete matching remote branch, then prune on macOS and Windows.
- Never delete `main`, protected or active branches, unmerged branches, branches with open pull requests, or any dirty/untracked worktree. Retain `migration/*` and `backup/*` only for a documented recovery window; delete them after verification and explicit owner approval.
- Audit this lifecycle after every merge and release; record any explicit retention exception.
