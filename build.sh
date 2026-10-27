#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
GAME="${1:?Usage: bash build.sh /path/to/FINAL_FANTASY_IX}"
MOD="$(basename "$(dirname "$(find "$ROOT" -mindepth 2 -maxdepth 2 -name ModDescription.xml -print -quit)")")"
CSC=""
while IFS= read -r line; do
  if [[ "$line" =~ ^([^[:space:]]+)[[:space:]]+\[(.*)\]$ ]]; then
    version="${BASH_REMATCH[1]}"
    if [[ "$version" != *-* ]]; then
      candidate="${BASH_REMATCH[2]}/$version/Roslyn/bincore/csc.dll"
      [[ ! -f "$candidate" ]] || CSC="$candidate"
    fi
  fi
done < <(dotnet --list-sdks)
[[ -n "$CSC" ]] || { echo 'Install a stable .NET SDK (8 or newer).' >&2; exit 1; }
OUT="$ROOT/$MOD/StreamingAssets/Scripts/Memoria.Scripts.$MOD.dll"
args=(-nologo -noconfig -nostdlib+ -langversion:5 -target:library -deterministic+ "-out:$OUT")
for name in mscorlib System System.Core Assembly-CSharp Memoria.Prime UnityEngine; do
  ref="$GAME/x64/FF9_Data/Managed/$name.dll"
  [[ -f "$ref" ]] || { echo "Missing game reference: $ref" >&2; exit 1; }
  args+=("-r:$ref")
done
mkdir -p "$(dirname "$OUT")"
dotnet exec "$CSC" "${args[@]}" "$ROOT"/Source/*.cs
echo "Built $OUT"