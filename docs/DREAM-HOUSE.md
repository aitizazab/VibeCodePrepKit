# Night 2's house

Redesigned 2026-10-12 (House agent). **The design is `docs/redesign/house.md`** (the house, hide-and-seek, Theo, the
memories and their cards, the escape, being thrown out, the lines, captures), with its plans
`docs/redesign/house-plan-floors.png`, `house-plan-site.png` and `house-escape-route.png`. This page is how to run,
build and change it. The greybox before it (2026-10-11) is in git history.

## Running it

- Standalone: **JamKit > Use Template > Dream House**, then Play (switch back with **Use Template > Visiting Hours**).
  Batch: `-executeMethod JamKit.DreamEscape.EditorTools.DreamHouseTemplate.UseFromCommandLine`. Playing the DreamHouse
  scene directly also works (GameRoot boots itself).
- Rebuild: **JamKit > Dream House > Rebuild Scene**, or `-executeMethod
  JamKit.DreamEscape.EditorTools.DreamHouseBuilder.RebuildFromCommandLine`, or Rebuild Shell Scenes (build step 246).
  The log has `JAMKIT_HOUSE_LINT <n> overlaps in <parts> parts` (the plan's boxes: must be 0), the mesh lint
  (`JAMKIT_HOUSE_MESHLINT ...`: what was drawn, by bounds; the command line exits 3 when it finds anything) and
  `JAMKIT_HOUSE_OK <scene>`. Optional, GPU: Look's bake for the probes and APV
  (`-executeMethod JamKit.Look.EditorTools.VhLook.BakeFromCommandLine -vhScene <DreamHouse.unity>`), then Perf's
  occlusion bake (`JamKit.Perf.EditorTools.VhPerfOcclusion.BakeFromCommandLine -perfScene <DreamHouse.unity>`).
- Chapters (presenter mode): The house, Hide and seek, The argument, The house tips, The front door. The debug skip
  moves to the next phase. F6 toggles Night 1's Saturday paper (for the calendar's deduction).
- On the hill: World puts the house at `HillAnchors.HouseOrigin` by calling `DreamHouseExterior.Build(Transform)` and
  wires `DreamHouseGame.houseRoot` to it; the escape is built at the house's local (0, -260, 400) (keep that space
  empty); she comes out at `HillAnchors.EscapeExit`. The chain: `DreamHouseGame.Escaped` (the night's end),
  `Chain.ForceOut(reason)` when she's thrown out, `Chain.SetLenaOutcome`.
- The argument: `DreamHouseGame.StartBattle` (plugged by `DreamBattles`); without it, debug keys F7/F8 choose
  Unsure/Condemns.

## Changing it

- **The plan** (rooms, walls, openings, stairs): `DreamHousePlan.cs`. **Furniture, hiding places, memories, Theo's
  points, lamps:** `DreamHouseFurnish.cs`. Then run `core-tests/DreamEscape` (the lint, reach, the graph, the round) and
  redraw the plans (`tools/house-plan`, `tools/art/vh_house_plan.py`).
- **Theo:** `TheoBrain.cs` (`LevelSpeed`, `LevelCheck`, `LevelFill` per catch; `ThroughFloor`). **The round's rules:**
  `HideAndSeekRound.cs` (`CountSeconds`, `MaxCatches`, `TheoRoomOpensAt`).
- **The escape:** `HouseEscape.cs` (rooms, beats, `StretchCorridor` threshold/bay/slope, `HouseFold` gaps).
- **Lines:** `DreamHouseLines` in `DreamHouseStory.cs`.
- **Looks:** `Dream/Editor/House/HouseMaterials.cs` (surfaces, each room's finish, the Look library's materials, model
  materials and fit specs from `Dream/House/house-assets.json`), `HouseFurnishing.cs` (models fitted into boxes, the
  lamps, the memories), `HouseProps.cs` + `HouseShapes.cs` (the furniture built where no model fits),
  `HouseEscape*.cs` (the escape, Chapel Row), `DreamHouseExterior.cs`. New Poly Haven models:
  `uv run python -I tools/house/ph-fetch.py <id>=<role>[,<alt role>]` (1k, into the manifest and LICENSES.md).
- Never hand-write a `.meta` for the downloaded assets in `Dream/House/`: let Unity import them.

## Tests

- dotnet: `cd core-tests/DreamEscape && dotnet test` (`HouseMeshLintTests`: the mesh lint's rules, every part resting
  or hung, every flight and the treehouse ladder walked up and down at a walk and a sprint, every memory in reach of
  where she stands; `DreamHouseTests`: the lint, every room and spot reachable, Theo
  hears and sees through the right things, the round's costs, a whole round; `HouseEscapeTests`: the corridor stretches
  and breaks through, the autopilot at 20-300 fps, sprinting alone gets her thrown out). Also `Stealth`, `Movement`, `All`.
- EditMode `DreamHouseEditorTests`: the scene's wiring, every solid part's collider on a layer she hits, 0 overlaps.
- PlayMode `DreamHousePlayTests`: hide-and-seek starts, a catch costs the last memory, the third catch and Theo's
  question, the creator's all-found line, the escape's autopilot out onto Chapel Row, thrown out and its costs.

## Captures

`tools/house-capture.ps1` (stills into `out/house-captures/<time>/`; recorded runs into `out/house-runs/<time>/` by
the built player's `-house-record` flag, no editor window) and
`tools/dream-capture.ps1` (the same stills, then the dream escape's). See `docs/redesign/house.md` section 8.
