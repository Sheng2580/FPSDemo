#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
LUBAN_DLL="$SCRIPT_DIR/Luban/Luban.dll"
CONF_ROOT="$SCRIPT_DIR"
OUTPUT_DIR="$PROJECT_DIR/Assets/StreamingAssets"

if command -v dotnet >/dev/null 2>&1; then
    DOTNET_CMD="$(command -v dotnet)"
elif [ -x "/Applications/Rider.app/Contents/lib/ReSharperHost/macos-arm64/dotnet/dotnet" ]; then
    DOTNET_CMD="/Applications/Rider.app/Contents/lib/ReSharperHost/macos-arm64/dotnet/dotnet"
else
    echo "[Error] dotnet not found. Install .NET 8 SDK or open this project on a machine with Rider installed."
    exit 1
fi

if [ ! -f "$LUBAN_DLL" ]; then
    echo "[Error] Luban.dll not found: $LUBAN_DLL"
    exit 1
fi

mkdir -p "$OUTPUT_DIR"

echo "[Info] Using dotnet: $DOTNET_CMD"
echo "[Info] Using Luban: $LUBAN_DLL"
echo "[Info] Output json: $OUTPUT_DIR"

DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp/luban-dotnet-home}" \
DOTNET_ROLL_FORWARD=Major \
"$DOTNET_CMD" "$LUBAN_DLL" \
    -t all \
    -d json \
    --conf "$CONF_ROOT/luban.conf" \
    -x outputDataDir="$OUTPUT_DIR"

echo "[Success] Luban json generated."
