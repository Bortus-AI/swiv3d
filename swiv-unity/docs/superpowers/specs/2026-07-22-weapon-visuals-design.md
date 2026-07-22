# Weapon Visuals — Hand-Authored Prefabs

**Date:** 2026-07-22
**Status:** Approved for planning

## Problem

The player's 5 weapons (Plasma, Rockets, Homing Missiles, Napalm, Smart Bomb) already have complete gameplay logic — damage, ammo, homing, area effects, burn zones — in `WeaponDefinition.cs`, `PlayerWeapons.cs`, and `Projectile.cs`. But every projectile is a plain colored sphere built at runtime via `GameObject.CreatePrimitive(PrimitiveType.Sphere)`, with a generic `TrailRenderer`. There is no weapon-specific silhouette, no muzzle flash for special weapons, and impact effects are a single reused flash sphere regardless of weapon type. This doesn't match the visual character of the original SWIV3D's weapons. Explosions (building destruction) already received a full multi-layer VFX pass (`ExplosionUtil.SpawnBuildingBlast`); projectiles never got the equivalent treatment.

## Goals

- Give each weapon a distinct, recognizable projectile shape (rocket, missile, tracer bolt, tumbling canister).
- Add muzzle flash for the special weapons (Rockets, Homing Missiles, Napalm) — Plasma already has a dedicated `MachineGunParticles` particle system on the helicopter rig and needs no new muzzle work.
- Give each weapon a distinct impact effect instead of one generic flash.
- Do not change any gameplay tuning (damage, ammo, homing, explosion radii, burn stats) — visuals only.

## Non-goals

- Ballistics/recoil/spread changes (bullet drop, camera kick, spread cones) — explicitly out of scope per user direction.
- A visible dropped-bomb model for Smart Bomb — it stays an instant ground-shockwave effect, matching the original's screen-clearing nuke rather than a physical projectile.
- New WeaponDefinition fields for visual tuning (fin count, body ratios, etc.) — shapes are fixed per `WeaponType`, not designer-tunable via the inspector. Can be revisited later if needed.

## Architecture

Unity Editor is live and connected to this project (`Swiv3D-remastered`), so weapon bodies are **hand-authored prefabs** built in-editor via the ProBuilder MCP tools, rather than composed purely in code.

- Prefabs live at `Assets/Resources/Prefabs/Weapons/<WeaponType>.prefab` for Plasma, Rockets, HomingMissiles, and Napalm (Smart Bomb has no in-flight projectile).
- Built from ProBuilder primitives (`probuilder-create-shape`, `-extrude`, `-bevel`, `-set-face-material`) composed under one root GameObject per weapon, then saved with `assets-prefab-create`. No textures — flat/emissive materials only, consistent with the project's current no-texture-art style.
- `Projectile.CreateRuntime()` changes from `GameObject.CreatePrimitive(Sphere)` to:
  ```
  Resources.Load<GameObject>("Prefabs/Weapons/" + type) → Instantiate()
  ```
  falling back to the current primitive-sphere path if the prefab isn't found. This mirrors the existing `TryLoadDefaultClips()` pattern for audio (safe no-op if art isn't authored), so the game never breaks mid-development — this is also how the weapons will be built one at a time without ever leaving the game in a broken state.
- `Projectile.Initialize()` keeps attaching `SphereCollider` / `Rigidbody` / `TrailRenderer` on top of whatever root it receives, unchanged from today — those remain gameplay concerns layered onto whatever visual root is provided.
- Muzzle flashes and impact/explosion effects stay **procedural, code-driven particles** in a new `WeaponVisuals.cs` (sibling to `ExplosionUtil.cs`) — a burst effect isn't a "model," and this matches how `ExplosionUtil` already handles building blasts. Only the flying projectile bodies get authored prefabs.
- `ExplosionUtil.SpawnParticleBurst` is widened from `private` to `internal` so `WeaponVisuals` can reuse it for weapon-scale impact bursts instead of duplicating the particle-system setup code.

## Per-weapon shapes

| Weapon | Shape | Notes |
|---|---|---|
| Plasma | Elongated capsule/bolt (~4:1 length:width), emissive cyan | Simple — a fast tracer, not a physical object. No per-shot light (rapid fire would tank perf). |
| Rockets | Cylinder body + cone nose + 3 angled tail fins | Orange/red material, matches `projectileColor`. Classic rocket silhouette. |
| Homing Missiles | Slimmer/longer cylinder + pointed seeker-cone nose (dark tip) + smaller fins | Keeps the existing `TrailRenderer` smoke trail already in `Projectile.ApplyVisual` — missiles read with a thicker white smoke look. |
| Napalm | Squat capsule/canister with rounded caps | `Projectile.Update()` gets a small added local-axis roll so it visibly tumbles like a dropped bomb — a targeted code change alongside the prefab, not a physics overhaul. |
| Smart Bomb | No prefab | Stays an instant ground-shockwave effect; gets its impact VFX boosted instead (see below). |

## Muzzle flash

- New: on firing Rockets / Homing Missiles / Napalm, spawn a brief smoke-puff + bright flash at `firePoint` (small particle burst + quick point-light pop, ~0.15s), recoil-style, one-shot per launch. Implemented in `WeaponVisuals.SpawnMuzzleFlash(WeaponType, firePoint)`, called from `PlayerWeapons.FireProjectile()`.
- Plasma: no new work — `Helicopter/MachineGunParticles` already provides continuous muzzle VFX while firing, driven by existing `PlayerWeapons` code.
- Smart Bomb: no muzzle flash — it has no barrel-launch moment.

## Impact effects

Replaces the single generic `ExplosionUtil.SpawnFlash` call currently used for every weapon's impact in `Projectile.Explode()`:

- **Plasma**: no impact effect today (zero `explosionRadius`, so `Explode()` does nothing but destroy). Add a small spark burst (8–12 tiny particles) on hit — cosmetic only, no damage change.
- **Rockets / Homing Missiles**: scaled-down version of the building-blast layering (fireball + a few sparks + small smoke puff), sized to each weapon's own `explosionRadius`, replacing the current single flat flash sphere. Built via the newly-`internal` `ExplosionUtil.SpawnParticleBurst`.
- **Napalm**: fire-splash burst on impact; `BurnZone` (`BurnZone.cs`) gets a continuous low-rate flame particle emitter added for its lifetime, replacing the current flat pulsing translucent disc so it visibly keeps burning.
- **Smart Bomb**: `PlayerWeapons.FireSmartBomb()`'s bespoke flash+ring code is replaced with a call to the existing `ExplosionUtil.SpawnBuildingBlast()`, sized to `smartBombRadius` — reuses the proven multi-layer blast instead of writing new nuke-specific VFX.

## Data flow (unchanged)

`WeaponDefinition` stays pure tunable gameplay data — no new fields. Visual choices key off `WeaponType` directly inside `WeaponVisuals.cs`, the same way `PlayerWeapons.PlaySpecialSound` already switches on type for audio. `Projectile` and `PlayerWeapons` remain orchestration-only: they ask `WeaponVisuals` for a visual root / effect and attach gameplay components on top.

## Error handling

- Missing prefab → falls back to the existing primitive-sphere path (already how `Projectile.CreateRuntime` behaves today; no new failure mode introduced).
- Missing/failed shader lookups in new effect code follow the existing pattern already used throughout `ExplosionUtil` (`Shader.Find("Sprites/Default")` fallback chain).

## Testing / verification plan

No automated test suite covers gameplay scripts in this project; verification is manual playtesting through the connected Unity Editor MCP session (instance `Swiv3D-remastered`), consistent with existing project conventions.

- Build one weapon's prefab + code path at a time, in order: Rockets → Homing Missiles → Napalm → Plasma bolt shape → Smart Bomb impact reuse.
- After each, enter Play Mode, fire that weapon, and capture a screenshot to confirm the shape/effect reads correctly.
- Because the fallback-to-sphere path is used automatically for any prefab not yet authored, the game stays fully playable throughout — this also naturally exercises the fallback branch (early weapons run on it before their prefab exists).
- After all weapons are done: one full pass firing Plasma (hold), cycling through all 4 specials (scroll + number keys) and firing each, and triggering Smart Bomb, confirming via `console-get-logs` that no errors/warnings were introduced.
- Confirm gameplay stats are untouched: ammo counts, homing behavior, and damage numbers (`Damageable.TakeDamage` calls, `WeaponDefinition` values) are unchanged — this is a visuals-only change, so regression risk is scoped to "does it look right," not balance.
