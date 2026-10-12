# World: Larch Hill, one hill for Nights 1-3 (the World agent's level design)

Status: **anchors published (2026-10-12)**; the full design, plan image and beat chart follow in this file.

## Anchors (for the House agent and anyone placing things on the hill)

The numbers live in code: `Packages/com.vibejam.jamkit/Runtime/Core/DeepBlock/HillAnchors.cs` (namespace
`JamKit.DeepBlock`, a static class, usable from core, the Dream scripts and the editor). World axes: X east, Z north (up
the hill), Y up; the creek's north bank is Y 0.

| Anchor | Where | What |
|---|---|---|
| `HouseOrigin` | (24, 9.6, 122), root yaw 0 | The Lasname house's root: the front door's threshold. The house's own frame is `DreamHouseExterior`'s (front faces local -Z), so on the hill its front faces **south, down the hill, onto Rowan Street**. |
| `LotX0..LotZ1` | X 10-42, Z 116-146 | The Lasnames' lot: front garden, side garden (the barbecue, east), back garden. The hill puts nothing inside it but fences, gates and ground. |
| `HouseMax*` | X 14-36, Z 122-142, up to Y 31.6 | The most the house's body may take. The round 5 exterior fills X 16.4-30, Z 122-137.4. |
| `FrontGate` | (24, 9, 116.3) | The front garden's gate on Rowan Street. Night 1 ends here. |
| `BackDoor`, `BackGate` | (24, 9.6, 137.4), (24, 9.6, 145.6) | The back door (movable along the back wall) and the back garden's gate at the foot of the Chapel Steps. |
| `Night2Start` | (8, 9, 110), yaw 60 | Night 2 starts on Rowan Street outside the house, the loop replaying. |
| `EscapeExit` | (24, 15, 153), yaw 0 | Where Night 2's escape bursts out into the middle band: the top of the Chapel Steps on Chapel Row (the first terrace above the house). |
| `EscapeEnd` | (104, 30, 238) | The end of the middle band's escape line: the Route 9 bus stop on Hill Road. |
| `Night1Arrival` | (21, 0, -38), yaw -4 | The creek's far bank. |
| `HospitalEntrance` | (0, 46, 322), yaw 180 | St. Brigid's main doors on the summit (Night 3). |

Band heights: the foot 0, Mill Street 4 (Back Lane B 5), Rowan Street 9, the middle band's terraces 15 / 22 / 30 / 37,
Crest Road 42, the summit 46.
