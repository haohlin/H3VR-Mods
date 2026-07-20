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
    H3VR_WINDOWS_HOST|H3VR_WINDOWS_REPOSITORY)
      export "$key=$value"
      ;;
    H3VR_PRIVATE_*)
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

if [[ $# -lt 1 ]]; then
  printf 'Usage: %s <PipelineAction> [h3vr.ps1 arguments]\n' "$0" >&2
  exit 2
fi

action="$1"
shift
case "$action" in
  Preflight|SourceStatus|RefreshSource|FindType|FindMethod|GrepSource|Verify|Build|Test|Package|Deploy|Logs|TailLogs|ClearLogs|SetPublishToken|Publish)
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

exec ssh -o BatchMode=yes "$H3VR_WINDOWS_HOST" "$remote_command"
