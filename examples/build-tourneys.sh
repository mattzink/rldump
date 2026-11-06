#!/usr/bin/env bash
# build-tourneys.sh — generate examples/tourneys.html from the tourneys.html.in
# template by splicing in live rldump JSON.
#
#   RLDUMP=./dist/rldump ./examples/build-tourneys.sh [TEMPLATE] [OUT] [REGION]
#
# Each marker is a WHOLE LINE in the template (e.g. a line that is exactly
# "PUT_SCHEDULE", optional surrounding whitespace) and is replaced, verbatim,
# with that endpoint's `--compact` JSON. The page's own JS parses the embedded
# "JSON islands" and renders the lists/tables. Any non-zero rldump exit aborts
# before the file is written, so you never emit a half-filled page.
#
# NOTE on argument order: rldump's global options (--compact, --raw, -o,
# --timeout, --config) are defined on the root command, so they must come
# BEFORE the subcommand (before the group, even). `--region` belongs to
# `schedule`, so it comes after it:
#   rldump --compact tournament schedule --region USW
set -euo pipefail

RLDUMP=${RLDUMP:-rldump}
TEMPLATE=${1:-examples/tourneys.html.in}
OUT=${2:-examples/tourneys.html}
REGION=${3:-USW}

command -v "$RLDUMP" >/dev/null 2>&1 || {
  echo "build-tourneys: '$RLDUMP' not found on PATH (try RLDUMP=./dist/rldump)" >&2
  exit 127
}

[ -f "$TEMPLATE" ] || { echo "build-tourneys: no template at '$TEMPLATE'" >&2; exit 3; }

tmpdir=$(mktemp -d)
trap 'rm -rf "$tmpdir"' EXIT

# Fetch compact (single-line) JSON. `set -e` aborts on any rldump failure,
# before the file is written (propagating rldump's exit codes 1/3/4).
"$RLDUMP" --compact tournament schedule --region "$REGION" > "$tmpdir/schedule.json"
"$RLDUMP" --compact tournament regions > "$tmpdir/regions.json"

# Replace each whole-line marker with the file's contents (verbatim, one pass).
# The inserted JSON is emitted as-is and not re-scanned, so a `$` inside a
# value can't be mistaken for a marker.
sed \
  -e "/^[[:space:]]*PUT_SCHEDULE[[:space:]]*$/r $tmpdir/schedule.json" \
  -e "/^[[:space:]]*PUT_SCHEDULE[[:space:]]*$/d" \
  -e "/^[[:space:]]*PUT_REGIONS[[:space:]]*$/r $tmpdir/regions.json" \
  -e "/^[[:space:]]*PUT_REGIONS[[:space:]]*$/d" \
  "$TEMPLATE" > "$OUT"

echo "build-tourneys: wrote $OUT (region $REGION)" >&2
