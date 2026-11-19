# The Cliff Quarter

Third-person parkour sandbox in a Mediterranean fantasy cliff city.

## Play

Open the `TinyWorld` folder in Unity **6000.6.9f1**, load
`Assets/Scenes/CliffQuarter.unity`, and press **Play**. Press **Enter** at the title screen.
If you have a local Mac build, launch `Builds/The Cliff Quarter.app` directly.

- **WASD / arrows:** move · **mouse:** look
- **Shift:** sprint; run along or up nearby walls while airborne
- **Space:** jump, vault, pull up; launch from a swing
- **Away from a wall + Space:** deliberate wall jump, with a shorter push
- Approach an edge in the air to catch it. **W / Space:** climb · **A / D:** shimmy
- Platform sides and corners are grabbable. Low pull-ups automatically crouch.
- Walk toward hanging vines to grab them; **W / S:** climb; **W / Space:** pull over the top
- **Ctrl:** crouch; stand on release when there is room
- **Hold click / F:** shoot the green sticky hand at the crosshair, up to **10.8 m**; sticks to the first surface hit
- Attached: the hand reels you toward its grip; **WASD:** steer the swing
- Release the held button to let go; grips expire after **10 seconds**. Release keeps your momentum.
- **C / right click:** release or drop · From a hang: **S + Space:** hop away
- **Hold E:** catch a nearby brass ring and swing · **WASD:** pump
- **Q:** air dash, once per flight
- Moss lends you a glider during his race. Beat him to keep it. **T / D-pad Up** restarts a run.
- **Space in the air** (falling, 3 m+ clear below) opens the glider · **WASD:** steer · **Shift:** dive
  · **Space / C:** fold. Landing, water, ledges, swings, the sticky hand and dashes fold it too.
- Water: **Space:** rise / leap · **Ctrl / C:** dive · **Shift:** swim faster
- **I / D-pad Left** opens the journal. Each launch starts a fresh adventure.
- **G** talks to people and uses nearby objects. Hold the **middle mouse button** to look closer.
- **R** returns to your last settled landing. **Esc** pauses. **F1** opens the controls.
- Gamepad: sticks move/look, south jump, hold north for sticky hand, east crouch/dive,
  D-pad down release, right trigger sprint, left shoulder swing, right shoulder dash, west interact, left trigger look closer, Select controls.

The climbing alley lies ahead of the starting quay. Brass lips lead to its roof
and swing crossing. Water stairs connect the river to the quays; stair streets,
balconies, and bridges lead toward the crown gardens. The western stairs connect the
theatre, temple and palace across three ravines. At the harbour, the lower mouth of
the salt grotto climbs through the headland to a sea window.

## Edit

The earlier `RiverCity.unity` and `RiverCityBaseline.unity` scenes are preserved.

`AgentScripts/BuildQuarter.cs` rebuilds the playable quarter and its generated
meshes. The Tiny World menu rebuilds the quarter discoveries prefab. Save scene work before running it; manual edits to that generated scene
will be replaced. Traversal and glider tunables are on `WandererMotor`.
`AgentScripts/VerifyGlider.cs` checks flight in Play mode after you win the courier race.

The wanderer is modeled in `TinyWorld/Art/build_wanderer.py` (Blender 5.2), which
exports `Assets/Models/Wanderer.fbx`. Rebuild it with
`Blender --background --python Art/build_wanderer.py`, then run
`AgentScripts/Upgrade/ImportWanderer.cs` and `SetupWanderer.cs`. `WandererPose`
animates the joints procedurally; gait and lean tunables are on that component.

See `Setup/CliffCityReport.md` and the approved images in `Setup/Concepts`.
The city uses sculpted geometry and simple materials; textures and character
animation are not final.

Sound effects and ambience load from `Assets/Resources/Audio`; sources and licences
are in `Setup/AudioCredits.md`.
