#!/usr/bin/env bash
# Capture contemporary-shell screenshots from an isolated demo profile on macOS.
#
#   Scripts/capture-ui.sh <profile-dir> <plan.json> <out-dir> [--no-build]
#
# Avalonia's macOS RenderTargetBitmap omits some composed controls. The app therefore
# walks the real capture plan and signals visual readiness, while this driver captures
# the live window content with screencapture. The real Libation profile is never opened.
set -euo pipefail

if [[ $# -lt 3 || $# -gt 4 ]]; then
	echo "usage: $0 <profile-dir> <plan.json> <out-dir> [--no-build]" >&2
	exit 2
fi
if [[ $# -eq 4 && $4 != "--no-build" ]]; then
	echo "unknown option: $4" >&2
	exit 2
fi
if [[ $(uname -s) != "Darwin" ]]; then
	echo "capture-ui.sh currently uses the macOS screencapture fallback required by S0." >&2
	exit 2
fi

PROFILE="$(cd "$1" && pwd)"
PLAN="$(cd "$(dirname "$2")" && pwd)/$(basename "$2")"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$3"
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
BUILD_ROOT="$ROOT/../.demo/capture-app"
mkdir -p "$OUT" "$BUILD_ROOT"
OUT="$(cd "$OUT" && pwd)"

for required in "$PROFILE/Settings.json" "$PROFILE/LibationContext.db" "$PROFILE/libation-master.key" "$PLAN"; do
	if [[ ! -f $required ]]; then
		echo "required capture input is missing: $required" >&2
		exit 2
	fi
done

if [[ ${4:-} != "--no-build" ]]; then
	DOTNET_ROOT="$(dirname "$DOTNET")" "$DOTNET" build \
		"$ROOT/Source/LibationAvalonia/LibationAvalonia.csproj" \
		-c Release --disable-build-servers -m:1 -v:minimal -o "$BUILD_ROOT"
fi
APP="$BUILD_ROOT/Libation"
if [[ ! -x $APP ]]; then
	echo "capture apphost is missing: $APP (run without --no-build)" >&2
	exit 2
fi

HANDSHAKE="$(mktemp -d "${TMPDIR:-/tmp}/libation-capture.XXXXXX")"
MANIFEST="$HANDSHAKE/entries.tsv"
CAPTURE_LOG="$HANDSHAKE/capture-log.txt"
WINDOW_HELPER="$HANDSHAKE/macos-window-id"
/usr/bin/xcrun swiftc "$ROOT/Scripts/macos-window-id.swift" -o "$WINDOW_HELPER"
python3 - "$PLAN" "$MANIFEST" <<'PY'
import json
import sys

plan_path, manifest_path = sys.argv[1:]
with open(plan_path, encoding="utf-8") as source:
    plan = json.load(source)
with open(manifest_path, "w", encoding="utf-8") as target:
    for index, entry in enumerate(plan["entries"]):
        name = entry.get("file") or (
            f"{entry['profile'].lower()}-{entry['route'].lower()}-"
            f"{entry['width']}x{entry['height']}.png"
        )
        if any(character in name for character in ("\t", "\n")):
            raise SystemExit(f"capture file name contains a control character: {name!r}")
        target.write(f"{index}\t{name}\t{entry['width']}\t{entry['height']}\n")
PY

APP_PID=""
CAFFEINATE_PID=""
cleanup() {
	if [[ -n $APP_PID ]] && kill -0 "$APP_PID" 2>/dev/null; then
		kill "$APP_PID" 2>/dev/null || true
		wait "$APP_PID" 2>/dev/null || true
	fi
	if [[ -n $CAFFEINATE_PID ]] && kill -0 "$CAFFEINATE_PID" 2>/dev/null; then
		kill "$CAFFEINATE_PID" 2>/dev/null || true
		wait "$CAFFEINATE_PID" 2>/dev/null || true
	fi
	rm -f "$HANDSHAKE"/ready-*.txt "$HANDSHAKE"/ack-*.txt \
		"$HANDSHAKE"/window-*.png "$MANIFEST" "$CAPTURE_LOG" "$WINDOW_HELPER"
	rmdir "$HANDSHAKE" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

DOTNET_ROOT="$(dirname "$DOTNET")"
export DOTNET_ROOT
export DOTNET_ROOT_ARM64="$DOTNET_ROOT"
export LIBATION_FILES_DIR="$PROFILE"
export LIBATION_MASTER_KEY_FILE="$PROFILE/libation-master.key"
export LIBATION_CAPTURE_PLAN="$PLAN"
export LIBATION_CAPTURE_OUT="$OUT"
export LIBATION_CAPTURE_OS_HANDSHAKE="$HANDSHAKE"

"$APP" &
APP_PID=$!
/usr/bin/caffeinate -d -u -w "$APP_PID" &
CAFFEINATE_PID=$!

# The S0 plan owns the 900-second full-run timeout.
DEADLINE=$((SECONDS + 900))
PLANNED=0
MISSING=0
while IFS=$'\t' read -r INDEX NAME WIDTH HEIGHT; do
	PLANNED=$((PLANNED + 1))
	STEM="$(printf '%04d' "$INDEX")"
	READY="$HANDSHAKE/ready-$STEM.txt"
	ACK="$HANDSHAKE/ack-$STEM.txt"
	while [[ ! -s $READY ]]; do
		if ! kill -0 "$APP_PID" 2>/dev/null; then
			set +e
			wait "$APP_PID"
			STATUS=$?
			set -e
			APP_PID=""
			echo "Libation exited $STATUS before capture $NAME became ready." >&2
			exit 1
		fi
		if ((SECONDS >= DEADLINE)); then
			echo "capture plan exceeded its 900-second timeout while waiting for $NAME" >&2
			exit 124
		fi
		sleep 0.05
	done

	IFS=$'\t' read -r READY_NAME READY_WIDTH READY_HEIGHT < "$READY"
	if [[ $READY_NAME != "$NAME" || $READY_WIDTH != "$WIDTH" || $READY_HEIGHT != "$HEIGHT" ]]; then
		echo "capture handshake mismatch for entry $INDEX" >&2
		exit 1
	fi

	WINDOW_INFO=""
	while [[ -z $WINDOW_INFO ]]; do
		set +e
		WINDOW_INFO="$("$WINDOW_HELPER" "$APP_PID" 2>/dev/null)"
		WINDOW_STATUS=$?
		set -e
		if [[ $WINDOW_STATUS -eq 0 && -n $WINDOW_INFO ]]; then
			break
		fi
		WINDOW_INFO=""
		if ! kill -0 "$APP_PID" 2>/dev/null; then
			echo "Libation exited before its ready window could be identified for $NAME." >&2
			exit 1
		fi
		if ((SECONDS >= DEADLINE)); then
			echo "capture plan exceeded its 900-second timeout while identifying the window for $NAME" >&2
			exit 124
		fi
		sleep 0.05
	done
	IFS=$'\t' read -r WINDOW_ID WINDOW_WIDTH WINDOW_HEIGHT <<< "$WINDOW_INFO"
	if [[ -z ${WINDOW_HEIGHT:-} || $WINDOW_WIDTH -lt $WIDTH || $WINDOW_HEIGHT -lt $HEIGHT ]]; then
		echo "invalid Libation window metadata for $NAME: $WINDOW_INFO" >&2
		exit 1
	fi

	TARGET="$OUT/$NAME"
	RAW="$HANDSHAKE/window-$STEM.png"
	mkdir -p "$(dirname "$TARGET")"
	/usr/sbin/screencapture -x -o -l"$WINDOW_ID" "$RAW"
	python3 "$ROOT/Scripts/crop-macos-window.py" \
		"$RAW" "$TARGET" "$WINDOW_WIDTH" "$WINDOW_HEIGHT" "$WIDTH" "$HEIGHT"
	rm -f "$RAW"
	if [[ ! -s $TARGET ]]; then
		echo "screencapture did not write $TARGET" >&2
		MISSING=$((MISSING + 1))
	fi
	PIXEL_WIDTH="$(/usr/bin/sips -g pixelWidth "$TARGET" 2>/dev/null | awk '/pixelWidth/ {print $2}')"
	PIXEL_HEIGHT="$(/usr/bin/sips -g pixelHeight "$TARGET" 2>/dev/null | awk '/pixelHeight/ {print $2}')"
	printf '%s\t%sx%s\trequested %sx%s\tmacOS direct-window screencapture\n' \
		"$NAME" "$PIXEL_WIDTH" "$PIXEL_HEIGHT" "$WIDTH" "$HEIGHT" >> "$CAPTURE_LOG"
	: > "$ACK"
done < "$MANIFEST"

set +e
wait "$APP_PID"
STATUS=$?
set -e
APP_PID=""
cp "$CAPTURE_LOG" "$OUT/capture-log.txt"

while IFS=$'\t' read -r _ NAME _ _; do
	if [[ ! -s $OUT/$NAME ]]; then
		echo "missing: $NAME" >&2
		MISSING=$((MISSING + 1))
	fi
done < "$MANIFEST"

echo "app exit $STATUS; $PLANNED planned; $MISSING missing; capture: macOS direct-window screencapture; output: $OUT"
[[ $STATUS -eq 0 && $MISSING -eq 0 ]]
