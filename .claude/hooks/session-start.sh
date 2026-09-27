#!/bin/bash
# SessionStart hook: installs npm dependencies so a fresh Claude Code web session can immediately
# run `npm test`, `npm run build` and the headless screenshot scripts.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

cd "$CLAUDE_PROJECT_DIR"
if [ ! -d node_modules ] || [ package-lock.json -nt node_modules ]; then
  npm ci --no-audit --no-fund
fi
