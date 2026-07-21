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
    H3VR_PRIVATE_ASSET_LAB)
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

canonical_windows_repository="$H3VR_WINDOWS_REPOSITORY"

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

pipeline_environment_config=""
if [[ -n "${H3VR_WINDOWS_WORKTREE_BRANCH:-}" ]]; then
  case "$H3VR_WINDOWS_WORKTREE_BRANCH" in
    *[!A-Za-z0-9._/-]* | '')
      printf 'Invalid Windows worktree branch.\n' >&2
      exit 2
      ;;
  esac

  worktree_listing="$(ssh -o BatchMode=yes "$H3VR_WINDOWS_HOST" \
    "git -C $(windows_quote "$H3VR_WINDOWS_REPOSITORY") worktree list --porcelain")"
  worktree_repository=""
  candidate_repository=""
  while IFS= read -r line; do
    case "$line" in
      'worktree '*)
        candidate_repository="${line#worktree }"
        ;;
      "branch refs/heads/$H3VR_WINDOWS_WORKTREE_BRANCH")
        worktree_repository="$candidate_repository"
        break
        ;;
    esac
  done <<< "$worktree_listing"

  if [[ -z "$worktree_repository" ]]; then
    printf 'Requested Windows worktree branch was not found.\n' >&2
    exit 2
  fi

  H3VR_WINDOWS_REPOSITORY="$worktree_repository"
  pipeline_environment_config="${canonical_windows_repository}\\build\\environment.local.json"
fi

remote_command=""
for argument in powershell.exe -NoProfile -ExecutionPolicy Bypass -File "${H3VR_WINDOWS_REPOSITORY}\\tools\\h3vr.ps1" -Action "$action" "$@"; do
  remote_command+="$(windows_quote "$argument") "
done

if [[ -n "${H3VR_PRIVATE_ASSET_LAB:-}" || -n "$pipeline_environment_config" ]]; then
  powershell_quote() {
    local value="$1"
    value=${value//\'/\'\'}
    printf "'%s'" "$value"
  }

  encoded_script=""
  if [[ -n "${H3VR_PRIVATE_ASSET_LAB:-}" ]]; then
    encoded_script+="\$env:H3VR_PRIVATE_ASSET_LAB = $(powershell_quote "$H3VR_PRIVATE_ASSET_LAB"); "
  fi
  if [[ -n "$pipeline_environment_config" ]]; then
    encoded_script+="\$env:H3VR_PIPELINE_ENVIRONMENT_CONFIG = $(powershell_quote "$pipeline_environment_config"); "
  fi
  encoded_script+="& $(powershell_quote "${H3VR_WINDOWS_REPOSITORY}\\tools\\h3vr.ps1") -Action $(powershell_quote "$action")"
  for argument in "$@"; do
    if [[ "$argument" =~ ^-[A-Za-z][A-Za-z0-9]*$ ]]; then
      encoded_script+=" $argument"
    else
      encoded_script+=" $(powershell_quote "$argument")"
    fi
  done
  encoded_command="$(printf '%s' "$encoded_script" | iconv -f UTF-8 -t UTF-16LE | base64 | tr -d '\n')"

  exec ssh -o BatchMode=yes "$H3VR_WINDOWS_HOST" "powershell.exe -NoProfile -ExecutionPolicy Bypass -EncodedCommand $encoded_command"
fi

exec ssh -o BatchMode=yes "$H3VR_WINDOWS_HOST" "$remote_command"
