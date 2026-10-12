#!/bin/sh
# Model sheets for Tripo (People agent, 2026-10-12). Runs flow.mjs (Nano Banana Pro, 0 credits) one sheet at a time.
cd C:/Users/User/Desktop/yougotgamesonyophone/FableAnimationTests
powershell -NoProfile -ExecutionPolicy Bypass -File art-tools/chrome.ps1 start
S=C:/Users/User/Desktop/yougotgamesonyophone/VibeJamKit/tools/tripo/sheets
node art-tools/flow.mjs --image --out vh3d-lena --prompt-file $S/lena.prompt.txt --attach "C:/Users/User/Desktop/yougotgamesonyophone/FableAnimationTests/art/incoming/lena5-turn-take1.jpg|C:/Users/User/Desktop/yougotgamesonyophone/VibeJamKit/Assets/JamKit/Templates/VisitingHours/Argument/Resources/ArgumentArt/lena_colour_neutral.png" --takes 2
node art-tools/flow.mjs --image --out vh3d-theo --prompt-file $S/theo.prompt.txt --attach "C:/Users/User/Desktop/yougotgamesonyophone/FableAnimationTests/art/incoming/sheet-theo-turn-take2.jpg|C:/Users/User/Desktop/yougotgamesonyophone/VibeJamKit/Assets/JamKit/Templates/VisitingHours/Art/Dream/dream_theo.png" --takes 2
node art-tools/flow.mjs --image --out vh3d-elias-dream --prompt-file $S/elias-dream.prompt.txt --attach "C:/Users/User/Desktop/yougotgamesonyophone/VibeJamKit/Assets/JamKit/Templates/VisitingHours/Art/Dream/dream_elias.png|C:/Users/User/Desktop/yougotgamesonyophone/FableAnimationTests/art/incoming/sheet-elias-turn-take1.jpg" --takes 2
node art-tools/flow.mjs --image --out vh3d-walt --prompt-file $S/walt.prompt.txt --attach "C:/Users/User/Desktop/yougotgamesonyophone/FableAnimationTests/art/incoming/sheet-walt-bb-take1.jpg" --takes 2
node art-tools/flow.mjs --image --out vh3d-elias-bed --prompt-file $S/elias-bed.prompt.txt --attach "C:/Users/User/Desktop/yougotgamesonyophone/FableAnimationTests/art/incoming/sheet-elias-bed-take1.jpg|C:/Users/User/Desktop/yougotgamesonyophone/FableAnimationTests/art/incoming/sheet-elias-turn-take1.jpg" --takes 2
echo SHEETS-DONE
