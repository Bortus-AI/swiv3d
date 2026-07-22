#!/usr/bin/env bash
# Usage: tools-unity-batch.sh [optional unity username] [password]
# Opens the project in batchmode to import assets and compile scripts.
set -euo pipefail
UNITY="${UNITY_PATH:-/home/bortus/tools/unity/Editor/Unity}"
PROJECT="$(cd "$(dirname "$0")/swiv-unity" && pwd)"
LOG="${TMPDIR:-/tmp}/unity-swiv3d-batch.log"
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT="${DOTNET_SYSTEM_GLOBALIZATION_INVARIANT:-1}"
ARGS=(-batchmode -nographics -quit -projectPath "$PROJECT" -logFile "$LOG" -accept-apiupdate)
if [[ $# -ge 2 ]]; then
  ARGS+=(-username "$1" -password "$2")
fi
echo "Running: $UNITY ${ARGS[*]}"
xvfb-run -a "$UNITY" "${ARGS[@]}"
echo "Log: $LOG"
rg -n "error CS|Scripts have compiler errors|Exiting batchmode" "$LOG" || true
