#!/bin/bash
# Generates the Visiting Hours art with Google Flow (0 credits), one item after another.
#   [MODEL="Nano Banana 2.1"] [PDIR=<prompt dir>] bash tools/art/vh_generate.sh <name:takes[:attach|attach2]> ...
# Prompts are <PDIR>/<name>.txt (default docs/art/prompts/). Takes land in FableAnimationTests/art/incoming/<name>-take<N>.jpg.
# Nano Banana Pro first; if Flow reports its usage limit, the item is retried once on Nano Banana 2.1 and so is the rest
# of the run. No waiting: a busy lock is reported as FAILED.
VJ=/c/Users/User/Desktop/yougotgamesonyophone/VibeJamKit
PDIR=${PDIR:-$VJ/docs/art/prompts}
MODEL=${MODEL:-Nano Banana Pro}
cd /c/Users/User/Desktop/yougotgamesonyophone/FableAnimationTests
gen() {   # name takes attach
  if [ -n "$3" ]; then node art-tools/flow.mjs --image --out "$1" --prompt-file "$PDIR/$1.txt" --takes "$2" --model "$MODEL" --attach "$3" 2>&1
  else node art-tools/flow.mjs --image --out "$1" --prompt-file "$PDIR/$1.txt" --takes "$2" --model "$MODEL" 2>&1; fi
}
for spec in "$@"; do
  IFS=: read -r name takes att <<< "$spec"
  t0=$(date +%s)
  out=$(gen "$name" "$takes" "$att"); rc=$?
  if [ $rc -ne 0 ] && echo "$out" | grep -qi "usage limit" && [ "$MODEL" = "Nano Banana Pro" ]; then
    echo "== $name: Pro is at its usage limit, switching to Nano Banana 2.1"; MODEL="Nano Banana 2.1"
    out=$(gen "$name" "$takes" "$att"); rc=$?
  fi
  echo "== $name rc=$rc $(( $(date +%s) - t0 ))s [$MODEL]"; echo "$out" | grep -v "^\s*$" | tail -6
  [ $rc -eq 0 ] || echo "FAILED $name"
done
echo ALLDONE
