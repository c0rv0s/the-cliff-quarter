# The River Quarter

Third-person parkour sandbox in a Mediterranean fantasy cliff city.

## Play on this Mac

Open `Builds/The Cliff Quarter.app`, then press **Enter**.

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
- Water: **Space:** rise / leap · **Ctrl / C:** dive · **Shift:** swim faster
- **R:** recover to your last settled landing · **Esc:** pause · **F1:** hide help
- Gamepad: sticks move/look, south jump, hold north for sticky hand, east crouch/dive,
  D-pad down release, right trigger sprint, left shoulder swing, right shoulder dash.

The climbing alley lies ahead of the starting quay. Brass lips lead to its roof
and swing crossing. Water stairs connect the river to the quays; stair streets,
balconies, and bridges lead toward the crown gardens.

## Edit

Open `TinyWorld` in Unity **6000.6.9f1**, then open
`Assets/Scenes/RiverCityCliffside.unity` and press Play.
The earlier `RiverCity.unity` and `RiverCityBaseline.unity` scenes are preserved.

`AgentScripts/BuildCliffCity.cs` rebuilds only the cliff scene and its generated
meshes. Save scene work before running it; manual edits to that generated scene
will be replaced. Traversal tunables are on `WandererMotor`.

The wanderer is modeled in `TinyWorld/Art/build_wanderer.py` (Blender 5.2), which
exports `Assets/Models/Wanderer.fbx`. Rebuild it with
`Blender --background --python Art/build_wanderer.py`, then run
`AgentScripts/Upgrade/ImportWanderer.cs` and `SetupWanderer.cs`. `WandererPose`
animates the joints procedurally; gait and lean tunables are on that component.

See `Setup/CliffCityReport.md` and the approved images in `Setup/Concepts`.
The city uses sculpted geometry and simple materials; textures and character
animation are not final.
