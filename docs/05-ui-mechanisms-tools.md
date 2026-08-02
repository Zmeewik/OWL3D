# UI, Mechanisms & Editor Tooling

## Weapon Wheel (`WeaponWheelUI` + `PointerEnterImage`)

Radial weapon-select menu, opened/closed by holding the `WeaponWheel` input action (Tab by default). Selection is **hover-based**, not click-based: each wheel segment is an `Image` with a `PointerEnterImage` component (implements `IPointerEnterHandler`/`IPointerExitHandler`, re-exposed as plain C# `Action` events since Unity's UI events don't support unsubscribing individual closures easily otherwise). `WeaponWheelUI` subscribes an enter/exit closure per segment in `OnEnable`, tracked in a `Dictionary<PointerEnterImage, (Action enter, Action exit)>` purely so `OnDisable` can unsubscribe the *exact same delegate instances* (a closure created fresh every `OnEnable`/`OnDisable` pair would otherwise never actually unsubscribe — this dictionary is a workaround for that footgun, not a data structure the logic needs otherwise).

Flow: `WeaponWheel` button held → `WeaponWheelUI.PressButton("Wheel", true)` → `OpenWheel()` (unlocks cursor, shows wheel prefab). While open, hovering a segment sets `selectedWeapon` (no visual commit yet). Releasing the button → `ChangeWeapon(false)` → if `selectedWeapon` differs from `lastWeapon`, fires `OnWeaponChange(selectedWeapon)` (a `string`-keyed event) → `WeaponManager.SwitchWeapon(string)` looks up the matching entry in `weapons[]` by `.name` (GameObject/asset name string match) and switches to it by index.

## Debug HUD (`DebugOutput`)

Singleton, fixed-size `string[100]` output buffer addressed by line index (`Output(str, line)`), concatenated into one `TextMeshProUGUI` every `Update()`. Callers write whatever line index they like directly — `PlayerMovement.FixedUpdate` writes speed to line 1 and max-speed to line 2, `StartSettings.Update` writes FPS to line 0. There's no registry of which system owns which line; it's coordinated purely by convention across files.

## Interactable mechanisms (`Mechanism` + `IInteractable` + `Propeller`)

A tiny generic framework: `Mechanism` (attached to a trigger volume) holds an `Interaction` enum (`Trigger | Lever | Wheel` — only `Trigger`/`Lever` are actually branched on; `Wheel` has no distinct handling) and a reference `GameObject` implementing `IInteractable`. On `Start()`, it enables/disables that object's own `Collider` depending on interaction type (Trigger keeps it on; Lever explicitly turns it off — presumably meant to be activated some other way not present in this codebase). `OnTriggerEnter` just forwards to `mechanismScript.Activate(other)`.

Only one concrete `IInteractable` exists: **`Propeller`** — a launch-pad. `Activate()` zeroes the colliding object's horizontal velocity and applies an upward impulse scaled inversely by the target's mass (`goalCoefficient = 1/mass * 80`, so heavier objects get proportionally more force to compensate — net effect is a roughly mass-independent launch height). Independently, `FixedUpdate` spins a visual `rotatingHelix` transform at a constant rate regardless of whether anything has been launched — purely cosmetic, not gated by activation state.

## Custom editor tooling

**`ComplexColliderHandleEditor`** (`[CustomEditor(typeof(ComplexColliderHandle))]`) — the only custom Editor script in the project. Replaces the default Inspector for `ComplexColliderHandle` with the default fields plus a row of buttons wired straight to that component's public/`[ContextMenu]` methods: collider enable/disable, trigger toggle, ragdoll activate/deactivate, structure printout, "Create Standard Humanoid" template generator, "Auto Generate Bones from Root", mass-setter, collider initializers (with/without size override), and a "Remove All Colliders" cleanup button. This is purely an authoring-time rigging tool for setting up ragdoll hitboxes on new character models — has no effect at runtime beyond what `ComplexColliderHandle` itself does.

## Settings & bootstrap (`StartSettings`)

Runs once at scene start: hides and locks the cursor (`CursorLockMode.Locked`), forces `QualitySettings.vSyncCount = 1`, and sets `Application.targetFrameRate = 100000` (i.e., effectively "uncapped, let vsync/hardware decide" — a very large number rather than `-1`, functionally equivalent here since vsync is on). Also runs a simple polled FPS counter (`refreshRate` = 0.2s window) and pushes the result to `DebugOutput` line 0.
