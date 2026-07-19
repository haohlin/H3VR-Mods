# H3VR-Mods

For every H3VR mod task, read and follow
`skills/h3vr-remote-development/SKILL.md` before editing, building, deploying,
or publishing. The Windows checkout is the authoritative build and runtime
environment.

## Private Windows configuration

Private connection and filesystem values must resolve automatically from the
current machine without entering Git:

- On macOS, run `h3vr-remote run <Action> [Mod]` from this repository. It
  delegates to `tools/h3vr-remote.sh`, which reads the user-owned, mode-`600`
  file `${XDG_CONFIG_HOME:-~/.config}/h3vr-mods/remote.env` (or
  `H3VR_PRIVATE_CONFIG`). It must provide `H3VR_WINDOWS_HOST` and
  `H3VR_WINDOWS_REPOSITORY`.
- On Windows, the authoritative checkout keeps its machine-specific H3VR,
  r2modman, Unity, and source-cache locations in ignored
  `build/environment.local.json`, copied from
  `build/environment.local.example.json`.
- Use the resolver before every remote H3VR command. Do not guess, scan for,
  print, commit, or add private host names, account names, paths, IDs,
  credentials, or tokens to public files, command output, package payloads, or
  documentation.
- If required private configuration is missing, stop with the missing variable
  name. Do not substitute a public default or silently target another machine.

Use `h3vr-remote status` to inspect remote Git state and
`h3vr-remote sync <branch>` for its guarded fetch/fast-forward sync. Do not
use raw SSH for normal H3VR Git or pipeline work. Use
`h3vr-remote git <arguments>` only for scoped remote Git work.

The tracked `tools/h3vr-remote.env.example` contains variable names only;
private values belong only in the per-user file above.
