# Setup report — November 14, 2026

## Installed

- Unity CLI 1.0.0; account signed in; Unity Personal active.
- Unity Editor 6000.6.9f1, Apple Silicon.
- Project: `TinyWorld`; template: `com.unity.template.urp-blank`.
- Pipeline 1.0.0, URP 17.6.0, Input System 1.20.1, uGUI 2.6.0.
- Cinemachine 3.1.7; ProBuilder 6.1.4.
- Authentication 3.9.0; Cloud Save 3.4.2; Remote Config 4.2.5; Leaderboards 2.3.5.

## Assumptions reconciled

Desktop-only, no monetization, local Git, anonymous identity, Cloud link, diagnostics,
and a visual craft baseline. All required setup skills were available.
The initial Cloud link lacked an organization; linking to the sole owner organization succeeded.
Create skipped identity and live packages before that link; this pass installed the packages
and added one non-blocking anonymous sign-in bootstrap. Play-mode sign-in passed.
Cloud Save, Remote Config, and Leaderboards are dependencies only; no player data flows yet.
Future HUD/menu stack: uGUI with TextMeshPro, not OnGUI.

## Visual baseline

`Assets/Scenes/RiverCityBaseline.unity`: three terrace levels on each riverbank,
two arched bridges, residences, domes, gardens, and a hilltop pavilion.
ProBuilder-authored arch meshes are saved as reusable mesh assets.
The scene has 1,035 renderers and 114,788 mesh triangles before batching.
This is an architectural blockout, not finished environment art or a playable parkour build.

Warm directional sun, gradient ambient lighting, distance fog, palette materials,
and a saved global Volume: ACES, Bloom 0.3 at 1.1, Vignette 0.21,
saturation +8, contrast +5. HDR and camera post-processing enabled.
No default materials, missing mesh references, or template skybox found.
Grading on/off captures differed by 14.68 mean RGB levels out of 255.

## Mac settings

M3 Pro / 14-core GPU / 36 GB RAM. PC quality tier, 1920×1080 default resolution,
Retina rendering disabled, 2× MSAA, render scale 1.0, 65 m shadows, two cascades.
60 fps is a target, not a benchmark result. No native-build/FPS test yet.

## Verification

- Scene builder compiled without diagnostics and saved through Unity APIs.
- Editor console: zero errors/warnings; no compilation failure.
- Play-mode frames advanced; anonymous identity completed successfully.
- All four post effects active; camera Volume mask and HDR checks passed.
- Fresh Editor reopen: scene, all 1,035 renderers, meshes, and Volume persisted; console clean.
- Scripts have Unity metadata. Generated directories are excluded from Git.
- Local captures and machine-check results are in the ignored `output` folder.

The first CLI-launched Editor exited without a crash trace. A controlled reopen worked.
Package installation also dropped a synchronous reply during a script reload;
the package status confirmed success. Remaining installs used asynchronous status checks.
No bug report or logs were sent to Unity.

## Remaining work

Wanderer model/animation, camera collision, movement, vaults, ledges, traversal testing,
sound, menus, saves, and native performance profiling. The full craft done gate is not met;
this pass stops at the lit setup baseline.

## Optional agent handoff — not written

With approval, add this line to the project's `AGENTS.md`:

> When building gameplay, feel, audio, shell, or live features, load the `craft-game-floors` skill; if it isn't installed, ask the user whether to run `unity skill install <client>` (ask: never run it without a yes).
