#!/bin/bash
# SessionStart hook — makes sure `dotnet` is available so a fresh Claude Code web session
# can immediately run `dotnet test tests/DinoRush.Core.Tests` (the Unity-free Core logic
# tests, see docs/DECISIONS.md D9). Unity itself is not installed here and isn't handled by
# this hook — only the Core .NET toolchain, which is all this repo needs before milestone M3.
set -euo pipefail

# Only run in Claude Code's remote/web environment — a local machine already has whatever
# toolchain its developer set up.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

if command -v dotnet >/dev/null 2>&1; then
  exit 0
fi

# Idempotent, non-interactive install of the .NET 8 SDK via apt (confirmed available in this
# container's package sources).
sudo apt-get update -qq
sudo apt-get install -y -qq dotnet-sdk-8.0
