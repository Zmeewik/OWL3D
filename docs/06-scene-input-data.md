# Scene, Input & Project Data

## Scene (`Assets/Scenes/SampleScene.unity`)

The only scene in the project (530 top-level YAML objects) — despite the default "SampleScene" name (inherited from the URP template this project started from), it is the actual working/test level, not a throwaway. Notable root-level objects found while scanning names:

- **`Player`** — the player rig
- **`Runner`** — a second named entity, likely an AI/enemy test subject or the "Robot" enemy instance (`Assets/Graphics/Models/Characters/Enemies/Robots/Robot.prefab` exists but wasn't confirmed as this instance's source)
- **`Main Camera`** and **`Main Camera (1)`** — two camera objects (first-person setups commonly split the world-camera from a separate close-range weapon-viewmodel camera to avoid clipping/FOV mismatch — consistent with `PlayerCamera`'s `cameraObj`/`cameraRef` split)
- **`CameraOffset`**, **`BodyPartsOffset`**, **`BodyParts`**, **`MainCollisionControl`** — rig helper transforms matching `PlayerCamera`/`ArmsOffset`/`ComplexColliderHandle`'s serialized offset-object references
- **`PistolUpgrade`**, **`Melee`** — weapon instances placed directly in the scene (under the player rig, presumably as the initial `WeaponManager.weapons[]` entries)
- **`Wheel`**, **`DebugText`**, **`Text`** (×3) — UI: the weapon wheel and multiple on-screen debug text elements (`DebugOutput`, `DebugHPBar` instances)
- **`Muzzle`** (×2) — weapon muzzle-flash attach points
- A large number of **`Cube (N)`** objects (at least 20 distinct numbered instances found) — blockout/greybox level geometry, i.e. the level is still in whitebox form, not final art
- **`Directional Light`** — single directional light, no other scene lighting found

`Assets/Readme.asset` is the **stock Unity URP template welcome page** (links to URP docs/forums/bug-reporting) — boilerplate left over from project creation, not project-specific documentation.

## Physics layers & tags (`ProjectSettings/TagManager.asset`)

Custom layers defined (beyond Unity's built-in 0–5): `Ground` (6), `ObjectGround` (7), `Player` (8), `Weapon` (9), `Entity` (10), `BodyParts` (11), `Bullet` (12). These map directly to `LayerMask` fields seen throughout the movement/combat scripts:

- `CollisionCheck.groundLayer` / `objectLayer`, `PlayerMovement.groundLayer` → almost certainly `Ground`/`ObjectGround`
- `MeleeWeapon.hitMask` / `appliedMask` → likely `Entity`/`BodyParts` (who can be hit) vs a broader "what can receive knockback force" mask
- `Projectile.hitMask` + the explicit `LayerMask.NameToLayer("Bullet")` check in `Projectile.FixedUpdate` (used to detect same-type-bullet collisions, e.g. `PistolCharged` absorbing other pistol bullets) → the `Bullet` layer
- `EntityHealth.layerMask` (used in the post-death ragdoll-force raycast) → likely `BodyParts`

Only one custom tag defined: **`Slope`** — read directly in `PlayerSurface.HandleSlope` (`contacts[index].otherCollider.tag == "Slope"`) to distinguish a "real" slope surface from an ordinary angled wall/ground contact that merely falls in the slope angle range.

## Input (`Assets/Input/OWL3D.inputactions`) — New Input System

### `Player` action map

| Action | Type | Keyboard&Mouse | Gamepad | Notes |
|---|---|---|---|---|
| Move | Vector2 | WASD + arrow keys (composite) | Left stick / D-pad | also bound on Joystick |
| Look | Vector2 | Mouse delta (`<Pointer>/delta`) | Right stick | also Joystick hat-switch, XR controller rotation |
| Jump | Button | Space | South face button | |
| Dash | Button | Left Shift | Left shoulder | |
| Crouch | Button | C | East face button | |
| FireLeft | Button, `Tap(0.3)` | Left mouse button | Right trigger | also Joystick trigger, XR primary action |
| FireLeftHeavy | Button, `Hold(0.3)` | Left mouse button (same binding, different interaction) | Right trigger | charged/held variant of FireLeft |
| FireRight | Button, `Tap(0.3)` | Right mouse button | — | |
| FireRightHeavy | Button, `Hold(0.3)` | Right mouse button | — | |
| Kick | Button, `Hold` | V | — | maps to `IAttackable.OnLegHit` |
| Block | Button, `Hold` | Left Ctrl | — | |
| SwitchMode | Button | Middle mouse button | — | wired in `.inputactions` but no handler found in the 67 scripts read — possibly unimplemented/leftover |
| ShowOff | Button | Y | — | |
| WeaponWheel | Button | Tab | — | held to open the weapon wheel |
| HideWeapon | Button | H | — | |

Same action set is also bound for **Touch** and **XR** control schemes on several actions (Move/Look/FireLeft) even though no touch/XR-specific UI was found in the scripts read — the input asset supports more platforms than the current gameplay code visibly branches on.

### `UI` action map

Standard Unity `InputSystemUIInputModule` default map (Navigate/Submit/Cancel/Point/Click/ScrollWheel/MiddleClick/RightClick/TrackedDevicePosition/TrackedDeviceOrientation) — auto-generated boilerplate, not custom to this project.

## Render pipeline settings

Three URP quality tiers, each a `UniversalRenderPipelineAsset` + matching `UniversalRendererData`:

- `Performant` (`URP-Performant.asset` / `URP-Performant-Renderer.asset`)
- `Balanced` (`URP-Balanced.asset` / `URP-Balanced-Renderer.asset`)
- `HighFidelity` (`URP-HighFidelity.asset` / `URP-HighFidelity-Renderer.asset`)

Plus `SampleSceneProfile.asset` (a `VolumeProfile` for the scene's `Global Volume`) and `UniversalRenderPipelineGlobalSettings.asset`. No script in the 67 read explicitly switches between the three tiers at runtime — presumably a manual/build-time or platform-detection choice not present in this pass.

## Editor/IDE project files (not gameplay-relevant)

`OWL3D.sln`, `Assembly-CSharp.csproj`, `Assembly-CSharp-Editor.csproj`, `.vs/`, `.vscode/`, `.idea/`, `OWL3D.sln.DotSettings.user` — standard IDE-generated/config files, regenerated by Unity on project open. `apiCompatibilityLevel: 6` (.NET Standard 2.1) confirmed in `ProjectSettings/ProjectSettings.asset`. `activeInputHandler: 1` confirms the New Input System is exclusively active (not "Both").
