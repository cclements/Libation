#!/usr/bin/env bash
# Capture contemporary-shell screenshots from an isolated demo profile on macOS.
#
#   Scripts/capture-ui.sh <profile-dir> <plan.json> <empty-out-dir> [--no-build]
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
if [[ -n $(ls -A "$OUT") ]]; then
	echo "capture output directory must be empty to preserve earlier evidence: $OUT" >&2
	exit 2
fi

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
CAPTURE_LOG="$OUT/driver-log.txt"
WINDOW_HELPER="$HANDSHAKE/macos-window-id"
APP_PID=""
CAFFEINATE_PID=""
cleanup() {
	local status=$?
	trap - EXIT
	# Never wait indefinitely for a native process that failed during setup.
	for pid in "$APP_PID" "$CAFFEINATE_PID"; do
		if [[ -n $pid ]] && kill -0 "$pid" 2>/dev/null; then
			kill "$pid" 2>/dev/null || true
			for ((attempt=0; attempt<30; attempt++)); do
				kill -0 "$pid" 2>/dev/null || break
				sleep 0.1
			done
			kill -KILL "$pid" 2>/dev/null || true
			wait "$pid" 2>/dev/null || true
		fi
	done
	printf 'driver exit %s; last entry %s: %s\n' "$status" "${INDEX:-startup}" "${NAME:-startup}" >> "$CAPTURE_LOG"
	[[ ! -f $MANIFEST ]] || cp "$MANIFEST" "$OUT/planned-entries.tsv"
	# This namespace is reserved by both plan readers; raw failures cannot become
	# planned PNGs or replace an earlier successfully cropped frame.
	mkdir -p "$OUT/capture-diagnostics"
	for evidence in "$HANDSHAKE"/ready-*.txt "$HANDSHAKE"/window-*.png; do
		[[ ! -f $evidence ]] || cp "$evidence" "$OUT/capture-diagnostics/"
	done
	python3 - "$MANIFEST" "$OUT" "$status" <<'RESULT'
import csv
import hashlib
import json
from pathlib import Path
import sys

manifest, out, status = Path(sys.argv[1]), Path(sys.argv[2]), int(sys.argv[3])
entries = []
if manifest.exists():
    for index, name, width, height in csv.reader(manifest.open(), delimiter="\t"):
        path = out / name
        produced = path.is_file() and path.stat().st_size > 0
        entries.append({
            "index": int(index), "file": name, "requestedClientDips": [int(width), int(height)],
            "produced": produced, "sha256": hashlib.sha256(path.read_bytes()).hexdigest() if produced else None,
            "stateReceipt": f"frame-{int(index):04d}.json" if (out / f"frame-{int(index):04d}.json").is_file() else None,
        })
(out / "result.json").write_text(json.dumps({
    "driverExitCode": status, "entries": entries,
    "visualAcceptance": "Requires pixel inspection; a produced frame is not product design or accessibility acceptance.",
}, indent=2) + "\n")
RESULT
	rm -rf "$HANDSHAKE"
	exit "$status"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
export CLANG_MODULE_CACHE_PATH="${CLANG_MODULE_CACHE_PATH:-$HANDSHAKE/module-cache}"
export SWIFT_MODULECACHE_PATH="${SWIFT_MODULECACHE_PATH:-$CLANG_MODULE_CACHE_PATH}"
/usr/bin/xcrun swiftc "$ROOT/Scripts/macos-window-id.swift" -o "$WINDOW_HELPER"
python3 - "$PLAN" "$MANIFEST" <<'PY'
import json
import sys

plan_path, manifest_path = sys.argv[1:]
with open(plan_path, encoding="utf-8") as source:
    plan = json.load(source)
with open(manifest_path, "w", encoding="utf-8") as target:
    for index, entry in enumerate(plan["entries"]):
        surface = entry.get("surface", "Route").lower()
        if surface == "componentgallery":
            subject = "componentgallery"
        elif surface == "onboarding":
            subject = f"onboarding-step{entry.get('onboardingStep', 1)}"
        else:
            subject = entry["route"].lower()
        name = entry.get("file") or (
            f"{entry['profile'].lower()}-{subject}-"
            f"{entry['width']}x{entry['height']}.png"
        )
        if (not name.lower().endswith(".png") or name.startswith("/") or "\\" in name or ":" in name
                or name.split("/", 1)[0].lower() == "capture-diagnostics"
                or any(part in ("", ".", "..") for part in name.split("/"))
                or any(ord(character) < 32 for character in name)):
            raise SystemExit(f"invalid relative PNG capture file name: {name!r}")
        target.write(f"{index}\t{name}\t{entry['width']}\t{entry['height']}\n")
PY

# Preserve the exact plan and app identity even when the run fails before its first frame.
cp "$PLAN" "$OUT/plan.json"
/usr/bin/shasum -a 256 "$APP" "$BUILD_ROOT/Libation.dll" "$PLAN" > "$OUT/input-sha256.txt"
git -C "$ROOT" rev-parse HEAD > "$OUT/source-head.txt"
git -C "$ROOT" diff --binary > "$OUT/source-diff.patch"
python3 - "$ROOT" "$PROFILE" "$BUILD_ROOT" "$OUT" <<'IDENTITY'
import hashlib
import json
from pathlib import Path
import subprocess
import sys

root, profile, build, out = map(Path, sys.argv[1:])
def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
source_paths = subprocess.check_output(
    ["git", "-C", str(root), "ls-files", "-cz", "--others", "--exclude-standard", "--", "Source", "Scripts"]
).decode().split("\0")
identity = {
    "sourceFilesAtLaunch": {path: digest(root / path) for path in sorted(set(source_paths)) if path and (root / path).is_file()},
    "assembliesAtLaunch": {path.name: digest(path) for path in sorted(build.glob("*.dll"))},
    "fixtureAtLaunch": {name: digest(profile / name) for name in ["Settings.json", "AccountsSettings.json", "LibationContext.db"] if (profile / name).is_file()},
    "note": "Source hashes describe the checkout at launch; --no-build does not establish that these sources produced the apphost.",
}
(out / "identity.json").write_text(json.dumps(identity, indent=2) + "\n")
IDENTITY
while IFS=$'\t' read -r _ NAME _ _; do
	if [[ -e $OUT/$NAME ]]; then
		echo "refusing to replace an existing capture: $OUT/$NAME" >&2
		exit 2
	fi
done < "$MANIFEST"

DOTNET_ROOT="$(dirname "$DOTNET")"
export DOTNET_ROOT
export DOTNET_ROOT_ARM64="$DOTNET_ROOT"
export LIBATION_FILES_DIR="$PROFILE"
export LIBATION_MASTER_KEY_FILE="$PROFILE/libation-master.key"
export LIBATION_CAPTURE_PLAN="$PLAN"
export LIBATION_CAPTURE_OUT="$OUT"
export LIBATION_CAPTURE_OS_HANDSHAKE="$HANDSHAKE"

"$APP" > "$OUT/app-stdout.txt" 2>&1 &
APP_PID=$!
/usr/bin/caffeinate -d -u -w "$APP_PID" &
CAFFEINATE_PID=$!

# The S0 plan owns the 900-second full-run timeout.
DEADLINE=$((SECONDS + 900))
STAGE_SECONDS=150
PLANNED=0
MISSING=0
while IFS=$'\t' read -r INDEX NAME WIDTH HEIGHT; do
	PLANNED=$((PLANNED + 1))
	STEM="$(printf '%04d' "$INDEX")"
	READY="$HANDSHAKE/ready-$STEM.txt"
	ACK="$HANDSHAKE/ack-$STEM.txt"
	STAGE_DEADLINE=$((SECONDS + STAGE_SECONDS))
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
		if ((SECONDS >= DEADLINE || SECONDS >= STAGE_DEADLINE)); then
			echo "capture stage/full-run deadline exceeded while waiting for $NAME" >&2
			exit 124
		fi
		sleep 0.05
	done

	IFS=$'\t' read -r READY_NAME READY_WIDTH READY_HEIGHT READY_SCALE < "$READY"
	if [[ $READY_NAME != "$NAME" || $READY_WIDTH != "$WIDTH" || $READY_HEIGHT != "$HEIGHT" || -z $READY_SCALE ]]; then
		echo "capture handshake mismatch for entry $INDEX" >&2
		exit 1
	fi

	STAGE_DEADLINE=$((SECONDS + 15))
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
		if ((SECONDS >= DEADLINE || SECONDS >= STAGE_DEADLINE)); then
			echo "capture stage/full-run deadline exceeded while identifying the window for $NAME" >&2
			exit 124
		fi
		sleep 0.05
	done
	IFS=$'\t' read -r WINDOW_ID WINDOW_WIDTH WINDOW_HEIGHT <<< "$WINDOW_INFO"
	if [[ -z ${WINDOW_HEIGHT:-} || $WINDOW_WIDTH -ne $WIDTH || $WINDOW_HEIGHT -lt $HEIGHT ]]; then
		echo "invalid Libation window metadata for $NAME: $WINDOW_INFO" >&2
		exit 1
	fi

	TARGET="$OUT/$NAME"
	RAW="$HANDSHAKE/window-$STEM.png"
	mkdir -p "$(dirname "$TARGET")"
	/usr/sbin/screencapture -x -o -l"$WINDOW_ID" "$RAW"
	python3 "$ROOT/Scripts/crop-macos-window.py" \
		"$RAW" "$TARGET" "$WINDOW_WIDTH" "$WINDOW_HEIGHT" "$WIDTH" "$HEIGHT" "$READY_SCALE"
	rm -f "$RAW"
	if [[ ! -s $TARGET ]]; then
		echo "screencapture did not write $TARGET" >&2
		MISSING=$((MISSING + 1))
	fi
	PIXEL_WIDTH="$(/usr/bin/sips -g pixelWidth "$TARGET" 2>/dev/null | awk '/pixelWidth/ {print $2}')"
	PIXEL_HEIGHT="$(/usr/bin/sips -g pixelHeight "$TARGET" 2>/dev/null | awk '/pixelHeight/ {print $2}')"
	printf '%s\t%sx%s\trequested %sx%s\twindow %sx%s\tmacOS direct-window screencapture\n' \
		"$NAME" "$PIXEL_WIDTH" "$PIXEL_HEIGHT" "$WIDTH" "$HEIGHT" \
		"$WINDOW_WIDTH" "$WINDOW_HEIGHT" >> "$CAPTURE_LOG"
	: > "$ACK"
done < "$MANIFEST"

STAGE_DEADLINE=$((SECONDS + 15))
while kill -0 "$APP_PID" 2>/dev/null; do
	if ((SECONDS >= DEADLINE || SECONDS >= STAGE_DEADLINE)); then
		echo "Libation did not exit after the final capture acknowledgement" >&2
		exit 124
	fi
	sleep 0.05
done
set +e
wait "$APP_PID"
STATUS=$?
set -e
APP_PID=""
while IFS=$'\t' read -r _ NAME _ _; do
	/usr/bin/shasum -a 256 "$OUT/$NAME"
done < "$MANIFEST" > "$OUT/output-sha256.txt"

while IFS=$'\t' read -r _ NAME _ _; do
	if [[ ! -s $OUT/$NAME ]]; then
		echo "missing: $NAME" >&2
		MISSING=$((MISSING + 1))
	fi
done < "$MANIFEST"

echo "app exit $STATUS; $PLANNED planned; $MISSING missing; capture: macOS direct-window screencapture; output: $OUT"
[[ $STATUS -eq 0 && $MISSING -eq 0 ]]
