# Playable milestone — November 14, 2026

## Delivered

- `Assets/Scenes/RiverCity.unity`; original baseline unchanged.
- Hooded wanderer with cloak, scarf, satchel, procedural gait, idle breathing, and landing response.
- Cinemachine 3 shoulder camera: mouse/gamepad orbit, obstacle avoidance, sprint FOV, fall-recovery warp.
- Camera-relative walking/sprinting, buffered variable-height jumps, coyote time, vaults, and mantles.
- Terrace stairs, brass-edged climbing ledges, roof links, and vault benches.
- River/fall recovery to the last settled landing; manual recovery with R.
- Title, controls, pause, sensitivity, sound toggle, and quit. Settings persist locally.
- Synthesized steps/cloth/landing sounds and one reusable dust system.
- Native ARM64 release: `Builds/The River Quarter.app` (about 84 MB).
- Approved craft handoff added to root `AGENTS.md`.

## Verified

Live Play-mode probes passed walking, faster sprinting, jump ascent/landing,
bench vault, building collision, staircase ascent, river recovery, pooled effects,
all four rooftop approach landings, gait/idle variation, and pause/resume.
Camera clearance probe passed; Cinemachine obstacle avoidance enabled.
Machine scan found no default materials, untinted UI/particles, unstyled text,
or template skybox. Play-mode console and final compile: zero errors.
Final native build: zero errors/warnings. Native launch and keyboard walking
confirmed. Native HUD observed around 59–60 fps; this is not an exhaustive
benchmark. User confirmed performance is fine; further profiling stopped.

The district has 1,262 renderers and 126,968 mesh triangles before batching.
1920×1080 defaults, Retina off, 2× MSAA, SRP batching, two shadow cascades.
Telemetry storage is bounded and local; no new telemetry upload was added.

## Visual gate

Draft → final:

palette discipline: 3/5 → 3/5 — shared architecture/character/UI palette; machine scan clean.
style uniformity: 3/5 → 3/5 — consistent simple sculptural forms, not reference-image fidelity.
lighting depth: 3/5 → 3/5 — warm sun with cooler, brighter shadow fill.
silhouette: 2/5 → 3/5 — hood, tailored cloak, scarf, and satchel read at gameplay distance.
UI intentionality: 3/5 → 3/5 — palette panels, spaced type, improved status contrast.
feedback visibility: 2/5 → 3/5 — gait, landing crouch, subtle dust, and action status.

Fix passes repaired the stair/terrace connection, connected the complete roof
approach, removed overlapping ground surfaces, corrected cloak normals,
added dome collision, and increased status-text contrast.
Captures are in ignored `output/`: `gameplay-final.png`, `character-final.png`,
and `feedback-final.png`. The last stages feedback for visual inspection.

## Scope and side effects

Still an architectural gameplay blockout: no swimming, wall-running, hanging
shimmy, combat, NPCs, interiors, or finished reference-quality assets. No forced
win/end screen in this free-roam sandbox. Checkpoints are session-only.
No live-service data flows added; the existing anonymous sign-in remains optional.
Audio uses a local gain layer, not an AudioMixer; no music/ambience added.

Imported bundled TMP Essentials. No packages added this milestone. Unity
reserialized render/graphics settings during the native build and enabled its
existing Cloud connection flag. Build architecture changed to ARM64; product
name is The River Quarter. Editor test sessions stopped; auto-tick restored off.

## Guidance used

Craft references: art-direction, camera-rigs, placeholder-assets, character-motion,
game-feel, vfx, game-audio, game-shell, visual-scorecard. UI/uGUI guidance and
Unity CLI playmode-verification-loop/build-run-test were used for construction.
Pipeline command references: scripts, capture, editor lifecycle, build/compilation.
APIs checked against installed Cinemachine, TMP, and Pipeline source. Official
Unity documentation fetches returned no content; local package source supplied
API verification instead.

Builders and probes are in `TinyWorld/AgentScripts`. `BuildPlayable.Build` creates
the separate playable scene once; `FixPlayable.Apply` applies its route/look pass.
Verification scripts require a running Editor Play session and drive test input.
