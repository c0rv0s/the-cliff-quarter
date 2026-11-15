# Cliff city — 14 November 2026

## Dense quarter pass

`AgentScripts/BuildQuarter.cs` → `Assets/Scenes/CliffQuarter.unity` (the build scene).
Shaders and palette are unchanged; `TinyWorld/Waterfall` is the only new shader.

- Each tier is a street, a row of attached houses, then the retaining wall that
  carries the next street. Houses come in five looks (island, Venetian, Moorish,
  Tuscan, merchant) plus bell towers, weighted by district. Sills, string courses
  and cornices are solid handholds.
- West uplands: temple acropolis, Palace of Winds, theatre, armillary sphere,
  Colossus and aqueduct. East uplands: fields, hamlet, barn, windmills, watermill
  and livestock; the stream falls off the end cliff into the harbour. Grand stairs
  lead up from the top street on both banks.
- Harbour beyond the gorge mouth: quays, warehouses, customs tower, cranes,
  piers, breakwaters, lighthouse and four climbable galleons (rope ladders,
  crow's nests, yard rings, plank walk).
- 186 buildings, 69 swing rings, 50 sunstones, 2.14 M triangles.

## Free-running pass

- Sprinting vaults low obstacles and mantles onto wide tops without a jump; vaults
  and mantles keep running speed. Air control steers carried speed instead of
  braking it. Hard landings while moving roll.
- Wall runs catch the ledge above and chain into a pull-up. Holding W while
  hanging leaps up the facade; Space + A/D leaps sideways.
- Sticky hand: Space while attached zips to the hand and catches its ledge; shots
  that would miss snap to a nearby swing ring.
- Poses blend between states; mantles, climb leaps, rolls and zips have poses.

## Checks (15 November 2026)

- 0 ungrounded house corners; 847 street samples with no holes or overhangs;
  no default materials; console clean.
- Play mode: 14 stair routes and both grand stairs walked; 9/9 flow checks
  (auto-vault, pace kept, roll, air speed, 22 m facade climb, zip, rope ladder,
  grounded livestock, chickens scatter).
- Regression suites on `RiverCityCliffside`: grapple 31/31, stair mantles 45/45,
  climb assist 13/13. Failures that remain predate this pass: stale coordinates
  (movement, cliff spawn) and real-time-sensitive checks (vine top, ledge holds).
- Native M3 Pro build, 1920×1018 window: 58.8 fps average, 17.4 ms p95 (60 Hz cap).
- Evidence: `output/dense-city/`. Restore points: `before-dense.tar.gz`,
  `before-parkour/`.

Playable scene: `TinyWorld/Assets/Scenes/RiverCityCliffside.unity`.
Native build: `Builds/The Cliff Quarter.app` (Apple Silicon, 177.0 MB).

## City

The approved concepts guide the larger river district: continuous irregular
cliffs, narrow winding streets, roof terraces, arcades, domes, bridges, gardens,
and water access. The gorge runs roughly 440 m; the skyline reaches about 148 m.
The terrain no longer consists of three broad steps on each bank.

There are 14 stair routes and 30 swing anchors. Generated geometry contains
1,101,429 triangles, 597 renderers, and 596 city colliders. Buildings and vegetation
leave the authored stair corridors clear. Street undersides are closed;
rectangular foundations own the bottom surface instead of overlapping wall caps.

`AgentScripts/BuildCliffCity.cs` reproduces the scene with seed 731921. It replaces
only its generated scene and mesh folder. The earlier scenes remain available;
`RiverCityBaseline.unity` retains its original SHA-256:
`ef4f496a10f046795d4d347ffae1e8cebcc4de5a5b83640d5b3673184eb7e572`.

## Movement

Wall jumps, wall runs/climbs, vaults, ledge catches, shimmying, ring swings,
swing launches, an air dash, swimming, diving, and water leaps use scene collision.

Ledge detection checks thin lips as well as deeper surfaces. Mantling searches
for clear landing positions and checks the capsule's lift, crossing, and descent.
It can clear a roof inset or low parapet. Hold **W** or press **Space** to climb;
**S + Space** hops away; **C** drops. **Ctrl** crouches. Low pull-ups use a 1.02 m
capsule, and standing resumes only when clear. Side catches and corner shimmies
work on the platform geometry, including unmarked edges.

Wall jumps require movement away from the wall and have a shorter push. Completed
pull-ups and settled landings update the recovery point, so **R** keeps progress.

The green sticky hand fires while holding **click / F**, travels up to 10.8 m and attaches
to the first solid surface, including decorative geometry. Its pull now follows
`/Users/nate/Documents/nerf/src/grapple.js`: inward acceleration and reeling keep
sideways velocity intact. Gravity stays active; **WASD** steers the swing.
Releasing the held button lets go without replacing velocity; grips last up to
**10 seconds**. **C / right click** cancels early.

## Held grapple follow-up

- Hold click, F, or gamepad north to keep the grip; release that input to let go.
  A grip lasts ten gameplay seconds from attachment. Timeout requires a new press.
- Space, pull-ups, and blocked rope corrections no longer detach the hand.
  C/right click still cancel. Swing forces and release momentum are unchanged.
- 31 grapple checks passed, including 9.9/10-second boundaries, no automatic
  repeat after timeout, release momentum, pull-ups, and obstructed full stretch.
- Evidence: `output/grapple-hold/`. Restore point: `before-hold.tar.gz` there.
- Scoped craft check: hold feedback and controls now match the input. Read the
  craft-game-floors game-feel reference; no art, scene, package, or palette changes.
- API references: Unity [Time.timeScale](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/time/timescale)
  and Input System 1.20 [ButtonControl](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/api/UnityEngine.InputSystem.Controls.ButtonControl.html).

## Grapple swing correction

- Replaced vertical wall-walk velocity with Nerf's pull, tension damping, reeling,
  and speed limit. The 10.8 m maximum stretch remains collision constrained.
- Release and reattachment preserve momentum. Air steering stays available until
  landing; floor grips cannot levitate the player. Vines retain ladder controls.
- 20 physics checks passed, including four comparisons against the actual Nerf
  function, release ballistics, steering, obstacle collision, and timestep checks.
- 24 existing movement checks passed. On the city crossing, the hand hit the brass
  ring, swung past it, and released onto the opposite platform at y=13.24 m.
- Rebuilt and reopened the Mac app. Native input verified firing, grounded grips,
  release, recovery, and pause. No runtime errors; left paused and ready.
- Build: zero errors; Unity symbol upload returned HTTP 403. The local build
  succeeded. Native smoke evidence: `output/grapple-swing/native-check.json`.
- Evidence: `output/grapple-swing/`. Earlier sticky wall-climb checks below describe
  the superseded behavior.
- Scope: controller, hand force/cord, pose, controls, and verification. No packages
  or city geometry changed. Used the craft-game-floors character-motion/game-feel
  references as checks while retaining the existing controller and Nerf behavior.
- API references: Unity 6000.6 [CharacterController.Move](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/charactercontroller/move)
  and [Vector3.ProjectOnPlane](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/vector3/projectonplane).

## Surface traversal follow-up

- Every hand input launches a swept projectile. The first solid collision attaches;
  misses retract at 10.8 m. Decorative meshes have separate hand-only colliders.
- Pull-ups account for sloped landings, nearby clear space, raised rails, and
  initial capsule overlaps. Retreat finishes before the lift begins.
- 281 hanging vines have climb routes. Walk toward one to grab; W/S climbs,
  W/Space pulls over its top, and C drops. Placement checks the climbing path
  and landing, including round facades and projecting balconies.
- Pause retains its controls, says **Paused**, and has no flavor paragraph.
- 127 checks passed across movement, climbing, projectiles, stair pull-ups, and
  vines. Three stair probe starts inside adjacent geometry were skipped.
  This covers representative routes, not every edge in the city.
- Native build succeeded with zero errors. One warning concerned unavailable
  Unity Cloud Diagnostics symbol upload; the local app built and launched.
- Native input confirmed attachment, climbing, release, recovery, the 10.8 m
  display, and the pause menu. No runtime exceptions appeared in its log.
- Evidence: `output/traversal-surfaces/`; `ledge-final.json` supersedes the older
  roof-clearance expectations in `regression.json`.

## Climbing follow-up

- 56 affected checks passed: 23 climbing-assist, 9 roof-clearance, 24 movement.
- Actual city checks include sticky ascent onto a platform, corner shimmying,
  and pulling up after rounding the corner beneath the next platform.
- Native input verified shooting, wall ascent, reattachment, and a pull-up onto
  the wall top. The app was left paused there.
- Updated native build: 0 errors/warnings. No runtime exceptions in the playtest.
- Evidence: `output/sticky-hand/`. The earlier scene/performance results below
  describe the preceding geometry pass.

The gameplay pass used the existing controller, procedural pose, palette, and
audio source. No packages were added or city geometry changed. Reference checks:
`craft-game-floors` character-motion/game-feel and Unity CharacterController
height, Move, and Physics.CapsuleCast documentation. Verification is scoped to
movement, pose, input feedback, and the affected routes; it is not an art score.

## Verification

- 24 movement checks passed in the actual alley, crossing, and river.
- 9 focused ledge checks passed, including thin parapets, raised roof surfaces,
  forward-input climbing, blocked ceilings, and escape jumps.
- 6 stair flights passed, reaching the crown district at 73.5 m.
- No missing materials or custom shader errors. Native build: 0 errors/warnings.
- Native launch, walking, jumping onto the bench, and pause were checked through
  the game window. No runtime exceptions appeared in that session's log.
- Native M3 Pro sample: 3,364 frames, 58.99 fps average, 17.38 ms p95, at
  1920×1018. This short starting-area sample is not a full-city 1080p benchmark.

Evidence is in `output/expansion/`: `traversal-final.json`, `ledge-clearance.json`,
`routes-final.json`, `city-metrics.json`, `build-final.json`, and
`native-performance.json`. `river-final.png` shows the final geometry;
`undersides-no-shadow.png` and `undersides-fixed-no-shadow.png` compare the repair.

## Remaining art work

This is a playable geometry pass. Textures, foliage, character animation, and
architectural detail still need refinement to approach the concept images.
The tests cover representative routes, not every possible ledge in the city.
