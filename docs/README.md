# OWL3D — Project Documentation

Reverse-engineered documentation of the OWL3D Unity project, written by reading every C# script, every ScriptableObject data asset, the scene file, the input action asset, and the project configuration. **Nothing in the project was modified** — this is a read-only pass; `docs/` sits outside `Assets/` so Unity's asset pipeline never sees it.

Generated: 2026-08-02.

## How to read this

| File | Covers |
|---|---|
| [00-overview.md](00-overview.md) | What the game is, tech stack, project stats |
| [01-architecture.md](01-architecture.md) | Cross-cutting patterns: the command-string event bus, subsystem-delegation, interfaces |
| [02-player-movement-camera.md](02-player-movement-camera.md) | First-person controller: walking, jump, dash, crouch, slide, wall-run/climb, hang-up, the two "life camera" scripts |
| [03-combat-weapons-damage.md](03-combat-weapons-damage.md) | Weapons, attack variants, damage/health/ragdoll, all tuned data values |
| [04-animation-visual.md](04-animation-visual.md) | Animation controllers, particle system, procedural animation stubs |
| [05-ui-mechanisms-tools.md](05-ui-mechanisms-tools.md) | Weapon wheel UI, debug HUD, interactable mechanisms, custom editor tooling |
| [06-scene-input-data.md](06-scene-input-data.md) | Scene hierarchy, Input System action map, ScriptableObject config values, project/render settings |

## One-paragraph summary

OWL3D is a first-person parkour/combat prototype (Unity 2022.3.13f1, URP, New Input System). The player has a momentum-based movement system (walk, jump, dash, crouch-slide, wall-run/climb, ledge hang-up) layered under a weapon system (melee/ranged/hybrid) with chargeable attacks, tag-based damage resistances, DoT effects (fire/toxin/electrification), and ragdoll death. Almost everything is wired through a **stringly-typed command bus** — `Action<string, float[]>` events carrying names like `"left_attack"`, `"wallrun"`, `"hangup"` — rather than typed method calls or a state machine.

See [01-architecture.md](01-architecture.md) for why that matters when extending the code.
