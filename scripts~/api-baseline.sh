#!/usr/bin/env bash
# Regenerates the public API baseline of the runtime assembly from Runtime/:
#
#   scripts~/api-baseline.sh           # rewrite api~/Appstack.Runtime.api.cs
#   scripts~/api-baseline.sh --check   # fail if the committed baseline is stale
#
# Needs the .NET 8 SDK (or newer). CI runs --check, so a change to the public
# API must commit the regenerated baseline; api-change-gate.yml then requires
# a label for it.
set -euo pipefail

CHECK=false
if [[ "${1:-}" == "--check" ]]; then
  CHECK=true
elif [[ -n "${1:-}" ]]; then
  echo "::error::Unknown option: $1"
  exit 1
fi

cd "$(dirname "$0")/.."

BASELINE="api~/Appstack.Runtime.api.cs"
PROJECT="scripts~/api-dump/ApiDump.csproj"

if [[ "$CHECK" == "true" ]]; then
  # The lock file pins Roslyn and its dependencies; CI must not resolve anew.
  dotnet restore "$PROJECT" --locked-mode --verbosity quiet
else
  dotnet restore "$PROJECT" --verbosity quiet
fi
dotnet run --project "$PROJECT" --no-restore --configuration Release -- Runtime "$BASELINE"

if [[ "$CHECK" == "true" ]]; then
  if ! git ls-files --error-unmatch -- "$BASELINE" >/dev/null 2>&1; then
    echo "::error::$BASELINE is not committed. Run scripts~/api-baseline.sh and commit the result (see DEVELOPMENT.md)."
    exit 1
  fi
  if ! git diff --exit-code -- "$BASELINE"; then
    echo "::error::$BASELINE is out of date with Runtime/. Run scripts~/api-baseline.sh and commit the result (see DEVELOPMENT.md)."
    exit 1
  fi
  echo "$BASELINE matches Runtime/."
fi
