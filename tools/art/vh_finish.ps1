# Finishes the Visiting Hours slice art (the main conversation runs this: background removal is a heavy job).
#   powershell -File tools/art/vh_finish.ps1 [-Session VibeJamKit]
# 1. rembg cut-outs of the chosen takes (characters, props, houses, phone) under the session's memory cap,
#    into Assets/JamKit/Templates/VisitingHours/Art/ (each file is written to .tmp and renamed when done: safe to re-run).
# 2. docs/art/vh-final-sheet.jpg: every file over a checkerboard.
# 3. A fresh review page (FableAnimationTests/art/review/<today>/visiting-hours-art-N.html) with that sheet on top.
# The first run downloads rembg's isnet-general-use model (about 170 MB) into ~/.u2net.
param([string]$Session = "VibeJamKit")
$ErrorActionPreference = "Continue"   # native tools report through $LASTEXITCODE
$vj = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $vj
uv run python "$HOME/.local/bin/capped-run.py" --session $Session -- uv run --with "rembg[cpu]" --with pillow --with numpy python tools/art/vh_process.py --cutouts --sheet
if ($LASTEXITCODE -ne 0) { Write-Host "cut-outs failed (exit $LASTEXITCODE)"; exit $LASTEXITCODE }
uv run python tools/art/vh_review_spec.py
Set-Location (Join-Path (Split-Path $vj -Parent) "FableAnimationTests")
node art-tools/review-page.mjs ../VibeJamKit/docs/art/vh-review.json --no-open
