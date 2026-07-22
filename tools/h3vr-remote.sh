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

windows_quote() {
  local value="$1"
  value=${value//\"/\\\"}
  printf '"%s"' "$value"
}

pipeline_repository="$H3VR_WINDOWS_REPOSITORY"

resolve_windows_worktree() {
  local requested_branch="${H3VR_WINDOWS_WORKTREE_BRANCH:-}"
  local worktree_listing
  local line
  local candidate_repository=""

  if [[ -z "$requested_branch" ]]; then
    return
  fi

  case "$requested_branch" in
    *[!A-Za-z0-9._/-]* | '')
      printf 'H3VR_WINDOWS_WORKTREE_BRANCH contains invalid characters.\n' >&2
      exit 2
      ;;
  esac

  worktree_listing=$(ssh -o BatchMode=yes "$H3VR_WINDOWS_HOST" \
    "git -C $(windows_quote "$H3VR_WINDOWS_REPOSITORY") worktree list --porcelain")
  while IFS= read -r line; do
    case "$line" in
      'worktree '*)
        candidate_repository="${line#worktree }"
        ;;
      "branch refs/heads/$requested_branch")
        if [[ -n "$candidate_repository" ]]; then
          pipeline_repository="$candidate_repository"
          return
        fi
        ;;
    esac
  done <<< "$worktree_listing"

  printf 'Requested Windows worktree branch was not found.\n' >&2
  exit 2
}

if [[ $# -lt 1 ]]; then
  printf 'Usage: %s <PipelineAction> [h3vr.ps1 arguments]\n' "$0" >&2
  exit 2
fi

action="$1"
shift
case "$action" in
  Preflight|SourceStatus|RefreshSource|FindType|FindMethod|GrepSource|PrepareUnitySourceSync|SyncUnitySource|AuditItemId|AuditUnityDeployment|AuditManagedDeployment|StopH3VR|InventoryProfilePackages|RemoveProfilePackage|AssetRipStatus|FindAssetRip|InspectAssetRip|UnityAssetRipStatus|UnityVanillaImportSmokeTest|UnityVanillaPrefabSmokeTest|UnityVanillaPrefabCompareNightForce|UnityVanillaPrefabAuditNightForce|UnityVanillaRuntimeCandidatePrepare|UnityVanillaRuntimeCandidateStatus|UnityVanillaPrefabImportStatus|UnityVanillaImportStatus|QuarantineVanillaScopeImports|UnityNightForcePrefabStatus|UnityBuildStatus|Verify|Build|Test|Package|Deploy|ShutdownWindows|Logs|TailLogs|ClearLogs|SetPublishToken|Publish)
    ;;
  *)
    printf 'Unsupported H3VR pipeline action: %s\n' "$action" >&2
    exit 2
    ;;
  esac

resolve_windows_worktree

pipeline_arguments=(
  powershell.exe
  -NoProfile
  -ExecutionPolicy Bypass
  -File "${pipeline_repository}\\tools\\h3vr.ps1"
)
if [[ -n "${H3VR_WINDOWS_WORKTREE_BRANCH:-}" ]]; then
  private_environment_config="${H3VR_WINDOWS_REPOSITORY}\\build\\environment.local.json"
  pipeline_arguments+=(-EnvironmentConfigPath "$private_environment_config")
fi
pipeline_arguments+=(-Action "$action" "$@")

remote_command=""
for argument in "${pipeline_arguments[@]}"; do
  remote_command+="$(windows_quote "$argument") "
done

asset_lab_prefix=""
if [[ -n "${H3VR_PRIVATE_ASSET_LAB:-}" ]]; then
  case "$H3VR_PRIVATE_ASSET_LAB" in
    *'&'*|*'|'*|*'<'*|*'>'*|*'('*|*')'*|*'^'*|*'"'*)
      printf 'H3VR_PRIVATE_ASSET_LAB contains unsupported shell characters.\n' >&2
      exit 2
      ;;
  esac
  asset_lab_prefix="set \"H3VR_PRIVATE_ASSET_LAB=${H3VR_PRIVATE_ASSET_LAB}\" && "
fi

exec ssh -o BatchMode=yes "$H3VR_WINDOWS_HOST" "$asset_lab_prefix$remote_command"
