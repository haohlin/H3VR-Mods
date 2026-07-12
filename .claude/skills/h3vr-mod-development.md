---
name: h3vr-mod-development
description: Use for any H3VR-Mods task that is developed remotely from macOS and built, tested, deployed, or released on the Windows H3VR machine.
---

# H3VR Remote Development

Read and follow the canonical project skill at
`skills/h3vr-remote-development/SKILL.md` before making H3VR mod changes.

The private Windows checkout configured through `H3VR_WINDOWS_REPOSITORY` is
authoritative for H3VR builds, deployment, VR testing, and Thunderstore
releases. The macOS checkout is a Git mirror for review and synchronized source
edits.

Before synchronizing, verify that the Windows checkout and `origin/main` share
history with `git merge-base HEAD origin/main`. If they do not, preserve the
Windows head on a recovery branch and stop for a user decision; never assume
matching branch names are equivalent or force histories together.
