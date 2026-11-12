#!/usr/bin/env bash
# Runs the canonical Windows H3VR pipeline using private, per-user settings.
set -euo pipefail

config_path="${H3VR_PRIVATE_CONFIG:-${XDG_CONFIG_HOME:-$HOME/.config}/h3vr-mods/remote.env}"

if [[ ! -r "$config_path" ]]; then
  printf 'Missing private H3VR configuration file.\n' >&2
  exit 2
fi

while IFS='=' read -r key value || [[ -n "$key" ]]; do
  [[ -z "$key" || "$key" == \#* ]] && continue
  case "$key" in
    H3VR_WINDOWS_HOST|H3VR_WINDOWS_REPOSITORY|H3VR_PRIVATE_ASSET_LAB)
      export "$key=$value"
      ;;
    *)
      printf 'Unsupported private H3VR configuration key: %s\n' "$key" >&2
      exit 2
      ;;
  esac
done < "$config_path"

for key in H3VR_WINDOWS_HOST H3VR_WINDOWS_REPOSITORY; do
  if [[ -z "${!key:-}" ]]; then
    printf 'Missing private H3VR configuration variable: %s\n' "$key" >&2
    exit 2
  fi
done

# This wrapper always targets the canonical checkout. Stop instead of running
# a requested worktree's action there.
if [[ -n "${H3VR_WINDOWS_WORKTREE_BRANCH:-}" ]]; then
  printf 'This wrapper cannot route to a Windows worktree; unset H3VR_WINDOWS_WORKTREE_BRANCH or use a checkout whose wrapper supports it.\n' >&2
  exit 2
fi

if [[ $# -lt 1 ]]; then
  printf 'Usage: %s <PipelineAction> [h3vr.ps1 arguments]\n' "$0" >&2
  exit 2
fi

action="$1"
shift
case "$action" in
  Preflight|SourceStatus|RefreshSource|FindType|FindMethod|GrepSource|Verify|Build|Test|Package|Deploy|Logs|TailLogs|ClearLogs|SetupAssetInspector|InspectAssets|SetPublishToken|Publish)
    ;;
  *)
    printf 'Unsupported H3VR pipeline action: %s\n' "$action" >&2
    exit 2
    ;;
esac

windows_quote() {
  local value="$1"
  value=${value//\"/\\\"}
  printf '"%s"' "$value"
}

remote_command=""
for argument in powershell.exe -NoProfile -ExecutionPolicy Bypass -File "${H3VR_WINDOWS_REPOSITORY}\\tools\\h3vr.ps1" -Action "$action" "$@"; do
  remote_command+="$(windows_quote "$argument") "
done

# ssh names the host in its own connection errors; keep them quiet and report
# the failure without the private value.
ssh_status=0
ssh -o BatchMode=yes -o ConnectTimeout=15 -o LogLevel=QUIET "$H3VR_WINDOWS_HOST" "$remote_command" || ssh_status=$?
if [[ $ssh_status -eq 255 ]]; then
  printf 'SSH to H3VR_WINDOWS_HOST failed (exit 255). Verify the host in private configuration.\n' >&2
fi
exit "$ssh_status"
