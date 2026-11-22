# The Cliff Quarter

Third-person parkour sandbox in a Mediterranean fantasy cliff city.

## Play

Open the `TinyWorld` folder in Unity **6000.6.9f1**, load
`Assets/Scenes/CliffQuarter.unity`, and press **Play**. Press **Enter** at the title screen.
On Mac, open the installed **The Cliff Quarter** app from Applications.

- **WASD / arrows:** move · **mouse:** look
- **Shift:** sprint; run along or up nearby walls while airborne
- **Space:** jump, vault, pull up; launch from a swing
- **Away from a wall + Space:** deliberate wall jump, with a shorter push
- Approach an edge in the air to catch it. **W / Space:** climb · **A / D:** shimmy
- Platform sides and corners are grabbable. Low pull-ups automatically crouch.
- Walk toward hanging vines to grab them; **W / S:** climb; **W / Space:** pull over the top
- **Ctrl:** crouch; stand on release when there is room
- **Hold click / F:** shoot the green sticky hand at the crosshair, up to **12.96 m**. It sticks to the first surface hit.
- Attached: the hand reels you toward its grip; **WASD:** steer the swing
- Release the held button to let go; grips expire after **10 seconds**. Release keeps your momentum.
- **C / right click:** release or drop · From a hang: **S + Space:** hop away
- **Hold E:** catch a nearby brass ring and swing · **WASD:** pump
- **Q:** air dash, once per flight
- Moss lends you a glider during his race. Beat him to keep it. **T / D-pad Up** restarts a run.
- **Space in the air** (falling, 3 m+ clear below) opens the glider · **WASD:** steer · **Shift:** dive
  · **Space / C:** fold. Landing, water, ledges, swings, the sticky hand and dashes fold it too.
- Water: **Space:** rise / leap · **Ctrl / C:** dive · **Shift:** swim faster
- Hold **Shift + 6 + 7** together to unlock all six quest rewards for this adventure.
- Once you have the painted skiff, press **B** or click the **right stick** to call it to the nearest open water. In the skiff, hold **Shift** while rowing forward for a faster stroke.
- **I / D-pad Left** opens the journal. Each launch starts a fresh adventure.
- **G** talks to people and uses nearby objects. Hold the **middle mouse button** to look closer.
- **R** returns to your last settled landing. **Esc** pauses. **F1** opens the controls.
- Gamepad: sticks move/look, south jump, hold north for sticky hand, east crouch/dive,
  D-pad down release, right trigger sprint, left shoulder swing, right shoulder dash, west interact, left trigger look closer, Select controls.

The climbing alley lies ahead of the starting quay. Brass lips lead to its roof
and swing crossing. Water stairs connect the river to the quays; stair streets,
balconies, and bridges lead toward the crown gardens. The western stairs fork toward the Temple of the Sun and the summit palace. Cross
the royal courtyard and climb the audience stair to meet the king and his jester.
The throne faces the courtyard gate and the arched lookout over the gorge. Past either side of the audience stair, the cypress walks lead to the royal citrus gardens. The oak side door along the north walk opens onto the palace gaol, its cell gallery and the torture chamber below.

At the harbour, footbridges connect the potters’ square and fish exchange. Two
water gates lead from the east market canals into the ship basin. The chart-house
lighthouse stands offshore west of the entrance. Across the water from the
southern salt store, land inside the hooked jungle bay. Follow the pale stone
road inland to the Sun pyramid. The western branch leads to the Jaguar pyramid
past the old courts. A carved door at the base of each pyramid leads underground. Look on the west face of the Sun pyramid and the east face of the Jaguar pyramid. Nera’s sea-glass lantern illuminates the lower chambers. On the eastern uplands, the farm tracks continue beyond the stream
bridges to the outer vineyards, orchards and pastures.

## Edit

`CliffQuarter.unity` is the only scene in the Unity project. The original
baseline is archived in `Setup/Archive/`. The other prototypes are in commit
`ab1eb1c`.

The Tiny World menu rebuilds the quarter discoveries prefab. After changing the
coastline or canal layout, use **Tiny World → Bake navigable skiff water** in the
playable scene. Traversal and glider tunables are on `WandererMotor`.

The legacy `AgentScripts/BuildQuarter.cs` generator requires
`RiverCityCliffside.unity` and its `.meta` from commit `ab1eb1c`. Run the generator
in a separate checkout because it overwrites the playable scene and its meshes.
`AgentScripts/VerifyGlider.cs` checks flight in Play mode after you win the courier race.

The wanderer is modeled in `TinyWorld/Art/build_wanderer.py` (Blender 5.2), which
exports `Assets/Models/Wanderer.fbx`. Rebuild it with
`Blender --background --python Art/build_wanderer.py`. The original import and
rigging workflow is in `AgentScripts/Upgrade/` and targets the cliffside prototype.
`WandererPose` animates the joints and exposes gait and lean tunables.

See `Setup/CliffCityReport.md` and the approved images in `Setup/Concepts`.
The city uses sculpted geometry and simple materials; textures and character
animation are not final.

Sound effects and ambience load from `Assets/Resources/Audio`; sources and licences
are in `Setup/AudioCredits.md`.
