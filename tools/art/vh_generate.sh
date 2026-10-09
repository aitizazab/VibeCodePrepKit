#!/bin/bash
# Generates the Visiting Hours slice art with Google Flow (Nano Banana Pro, 0 credits), one item after another.
#   bash tools/art/vh_generate.sh <name:takes[:attach]> ...      (names are prompt files in docs/art/prompts/)
# Takes land in FableAnimationTests/art/incoming/<name>-take<N>.jpg. No waiting: a busy lock is reported as FAILED.
VJ=/c/Users/User/Desktop/yougotgamesonyophone/VibeJamKit
cd /c/Users/User/Desktop/yougotgamesonyophone/FableAnimationTests
for spec in "$@"; do
  IFS=: read -r name takes att <<< "$spec"
  t0=$(date +%s)
  if [ -n "$att" ]; then out=$(node art-tools/flow.mjs --image --out "$name" --prompt-file "$VJ/docs/art/prompts/$name.txt" --takes "$takes" --attach "$att" 2>&1); rc=$?
  else out=$(node art-tools/flow.mjs --image --out "$name" --prompt-file "$VJ/docs/art/prompts/$name.txt" --takes "$takes" 2>&1); rc=$?; fi
  echo "== $name rc=$rc $(( $(date +%s) - t0 ))s"; echo "$out" | grep -v "^\s*$" | tail -6
  [ $rc -eq 0 ] || echo "FAILED $name"
done
echo ALLDONE
