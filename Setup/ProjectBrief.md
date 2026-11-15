# Tiny World

Single-player, third-person parkour sandbox in a Mediterranean fantasy cliff city.
A turquoise river connects stacked terraces, bridges, markets, and gardens.
The player is a cloaked wanderer exploring freely, without combat or monetization.

## Art direction

- Style: sculpted, sun-warmed architecture; clear silhouettes and restrained detail.
- Palette: limestone `#F0DFBF`, sandstone `#BC956F`, terracotta `#B66547`, water `#268F98`, olive `#63774C`, shadow `#354E63`, brass `#DAAE57`, sky `#8FC4DF`.
- Mood: low warm sun, cool open-sky shadows, distant haze.

## Original playable route

Riverside → market stairs → rooftops → bridge → courtyard.
Start with running, sprinting, jumping, camera collision, vaults, and ledge climbs.
The initial compact district is preserved in RiverCity.unity.

## Mac target

- Apple M3 Pro, 14-core GPU, 36 GB unified memory.
- Target: 60 fps at 1920×1080, measured in a native Mac build.
- URP, fixed daylight, bounded shadows, modular geometry, simple water.
- No crowd simulation, real-time GI, or full-resolution Retina requirement at baseline.

## Setup milestone

Install Unity; create and open a URP project; verify a lit architectural baseline.
The setup scene is not a playable parkour build. Movement is the next milestone.

## Current direction

`RiverCityCliffside.unity` expands the sandbox around the approved concepts in
`Concepts/`. Continuous eroded cliff faces replace stepped terrain. Narrow
streets, pocket plazas, stacked buildings, bridges, and gardens create vertical
routes. Preserve the palette; prioritize scale and organic variety over a craft
checklist. See `CliffCityReport.md` for validation and limits.
