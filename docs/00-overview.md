# Overview

## What this is

A first-person action/parkour prototype. Genre signals from the code: momentum-driven movement (wall-run, wall-climb, dash, slide, ledge hang-up), a melee+ranged weapon-wheel combat system, per-limb ragdoll death, and tag-based elemental damage (Fire/Toxin/Electrification/Piercing/Crushing/Cutting/Blast/Lazer) — closest comparison is something in the Titanfall/Ghostrunner/Warframe movement-shooter space. Scene contents (a "Robot" enemy prefab, bullet/kunai prefabs, a "Runner" scene object) support this.

## Tech stack

| | |
|---|---|
| Engine | Unity **2022.3.13f1** (LTS) |
| Render pipeline | URP 14.0.9 — three quality tiers: `Performant`, `Balanced`, `HighFidelity` (own Renderer + Pipeline Asset each) |
| Input | New Input System 1.7.0 — one `.inputactions` asset (`OWL3D`), `Player` + `UI` action maps |
| UI text | TextMeshPro 3.0.6 |
| Other packages | Timeline, Visual Scripting (present but unused in the scripts read), Cinemachine not present — camera is fully hand-rolled |
| Version control | Git — repo initialized, `.gitattributes`/`.gitignore` present, active history (`git log`: "fixes", "Added new enemy", "Added 2 weapons", "Animation changes", "Added weapon action" — combat/enemy features are the most recent work) |

## Project statistics (at time of writing)

| | |
|---|---|
| C# scripts | 67 |
| Scenes | 1 (`SampleScene.unity` — the only scene, used as the actual working level despite the default name) |
| Prefabs | 15 |
| Total files under `Assets/` | 705 |
| ScriptableObject data assets (combat) | ~40 (`AttackVariant`, `DamageProfile`, `DamageTag`, `DamageEffect`) |

## Top-level `Assets/` layout

```
Assets/
├── Configs/            → PlayerMovementConfig instance (Movement.asset) — all movement tuning
├── Graphics/            → Models, prefabs (Robot enemy, bullets, hit effects, particles)
├── Input/               → OWL3D.inputactions (New Input System)
├── physics/             → (physics-related assets; not scripts)
├── Scenes/              → SampleScene.unity (the one and only scene)
├── Scripts/             → All gameplay C# + ScriptableObject combat data (see below)
├── Settings/             → URP pipeline/renderer assets (3 quality tiers) + volume profile
├── Sounds/
├── TextMesh Pro/         → TMP runtime resources (default package content)
├── Tree_Textures/
└── TutorialInfo/         → Default Unity URP template readme (not project-specific)
```

`Assets/Scripts/` subfolders:

```
Scripts/
├── Camera/                       → PlayerCamera, ArmsOffset (procedural view-bob), IRotatable
├── Editor/                       → ComplexColliderHandleEditor (custom inspector)
├── Entities/
│   ├── Behaviour/
│   │   ├── Attack/               → Weapon base classes, AttackVariant, Projectile, HitObject
│   │   ├── Health/                → DamageProfile/Tag/Packet/Calculator, elemental DamageEffects
│   │   └── ProceduralAnimations/  → Empty stub classes (unused)
│   │   └── Projectiles/Bulltets/  → PistolBullet, PistolCharged
│   └── Setup/                     → ComplexColliderHandle (ragdoll/hitbox rig builder)
├── Mechanisms/                    → Generic interactable framework (Mechanism, Propeller)
├── Player/
│   ├── Movement/                  → 10 single-responsibility movement subsystem scripts
│   ├── CollisionCheck, Input, PlayerMovement, SurfaceHandler
├── ScriptableObjects/Weapons/      → All combat data assets (AttackVariants, DamageProfiles, DamageTags)
├── Settings/                       → StartSettings (frame rate / cursor lock / FPS counter)
├── UI/                             → DebugOutput (on-screen multi-line debug text)
└── Visual/
    ├── Animations/                 → EntityAnimator, EntityAttackAnimation, HeroAnimationController, WeaponAnimationController
    ├── UI/Wheel/                   → WeaponWheelUI (radial weapon select)
    └── Particles.cs                → Particle-effect service singleton
```

## Known data issue found while documenting

Every `DamageTag` asset under `Scripts/ScriptableObjects/Weapons/DamageTags/*.asset` has its `tagName` field literally set to the string `"Ninja"` — regardless of the asset's actual name (`Fire.asset`, `Crushing.asset`, `Piercing.asset`, etc. all contain `tagName: Ninja`). This looks like a copy-paste-and-forget-to-rename mistake during asset creation. It does **not** break the damage-resistance calculation (`DamageCalculator` compares `DamageTag` object references via `List.Contains`, not the `tagName` string), but `tagName` is presumably meant for UI/debug display and is currently useless for that. See [03-combat-weapons-damage.md](03-combat-weapons-damage.md) for the full data dump.

No files were changed to document this — flagging only, per instructions.
