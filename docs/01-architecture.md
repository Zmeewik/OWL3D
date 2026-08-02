# Architecture & Cross-Cutting Patterns

These patterns recur across movement, combat, and animation, and matter more than any single script when extending the code.

## 1. The stringly-typed command bus

Instead of a formal state machine or typed method calls, most subsystems talk to each other by broadcasting **string command names** through C# `Action` events, and the receiving side does a `switch` on the string:

```csharp
// PlayerMovement.cs
public static Action<string, float[]> OnLifeCameraAction;
public void OnLifeCamera(string type, float[] parameters = null) => OnLifeCameraAction?.Invoke(type, parameters);
```

```csharp
// PlayerCamera.cs / ArmsOffset.cs — both subscribe to the SAME event independently
public void ChangeLifeCameraState(string state, float[] parameters) {
    if (!stateNames.Contains(state)) return;   // unknown string → silently ignored
    ...
    switch (state) { case "dash": ... case "jump": ... case "hangup": ... }
}
```

```csharp
// WeaponManager.HandleCommand — same pattern for combat
switch(command) {
    case "left_attack": OnAttackPressed(0); break;
    case "block_start": OnBlockPressed(); break;
    ...
}
```

**Where this shows up:**
- `PlayerMovement.OnLifeCameraAction` — movement state → camera/arms procedural animation (`"dash"`, `"jump"`, `"land"`, `"wallrun"`, `"slide"`, `"hangup"`, `"fall"`, `"fallcliff"`, `"leghit"`, `"movement"`, `"none"`)
- `IWeaponCommand.OnWeaponCommand` (`Action<string>`) — `PlayerMovement` → `WeaponManager` (e.g. `"left_attack_cancel"` sent when the player starts a wall-climb, to cancel an in-progress attack)
- `IAnimationSender.OnAnimateCommand` (`Action<string, bool, float>`) — `WeaponBase`/`EntityHealth` → `EntityAttackAnimation` / `EntityAnimator` (`"left_attack_start"`, `"hit_front"`, `"block_break"`, `"idle"`, `"pick_up"`, ...)

**Practical implication:** there is no compiler check tying these strings together. `ChangeLifeCameraState`'s `stateNames` list, `EntityAttackAnimation.animationList`, and every `switch` block must independently agree on the exact string. A typo compiles fine and silently does nothing (`if (!stateNames.Contains(state)) return;`). When adding a new state/command, grep for the existing string across all four systems (movement, camera, weapon, animation) rather than assuming one central enum.

## 2. `PlayerMovement` as orchestrator, not owner

`PlayerMovement.cs` holds the `Rigidbody`, the `BodyState` enum (`Moving/Dashing/WallRunning/Sliding/InAir/SlidingGround`), and `FixedUpdate`'s per-state dispatch — but almost none of the actual logic. Each state's behavior lives in its own single-purpose `MonoBehaviour`, all wired together via public serialized references on `PlayerMovement` and called from its `FixedUpdate` switch:

```
PlayerWalking   — ground acceleration, drag, counter-movement, body rotation
PlayerCrouch    — smooth crouch/stand, collider resize, "can I stand up" raycast
PlayerDash      — impulse dash, direction from camera-relative input
PlayerSurface   — contact-normal classification → Ground/Slope/Wall/Ceiling, wall-run trigger
PlayerJump      — ground jump + wall jump
PlayerFly       — fall-time tracking, OnFly/OnLand transition logic
PlayerSlide     — slope auto-slide + ground slide-boost (two distinct "slide" behaviors, same class)
PlayerWallRun   — wall-run/wall-climb/wall-slide sub-state machine (its own nested WallState enum)
PlayerHangUp    — ledge-mantle: two-phase parabolic AddForce (up, then forward)
PlayerMomentum  — "speed points" system: named events (wallrun/jump/slide/not moving) add/subtract momentum, which lerps `currentMaxSpeed` between config's low/top speed
```

Every one of these takes `[SerializeField] PlayerMovement playerMovement` and reaches back into it (`playerMovement.rb`, `playerMovement.playerMovementConfig`, `playerMovement.currentMaxSpeed`, ...) rather than owning its own state — so they are not independently reusable, only splittable-for-readability. `SurfaceHandler` (angle-threshold classification) is a stateless helper consumed by `PlayerSurface`.

`PlayerCrouch`/`PlayerFly`/etc. do **not** implement a common interface — `PlayerMovement` calls each one's differently-named public methods directly (`playerWalking.Moving()`, `playerSlide.Slide()`, `playerWallRun.WallRun()`). There's no `IMovementState` abstraction; adding a new `BodyState` means adding a new case to `PlayerMovement.FixedUpdate`'s switch plus a new subsystem script, wired by hand.

## 3. Two near-duplicate procedural camera scripts

`PlayerCamera.cs` (drives the actual `Camera`/FOV) and `ArmsOffset.cs` (drives the weapon/arms view-model offset) are ~90% line-for-line identical: same `FeatureFlags` toggle class, same `LifeCameraState` enum, same per-state `Breath()/Movement()/OnJump()/OnDash()/OnLand()/OnSlide()/OnWallrun()/OnHangUp()/OnFall()` procedural-shake methods, both independently subscribed to `PlayerMovement.OnLifeCameraAction`. `ArmsOffset` has two states `PlayerCamera` lacks (`Fallcliff`, `Leghit`) added later — the duplication has already started to drift. Any tuning change (e.g. new shake curve) must be applied to both files by hand; there is no shared base class.

## 4. Interfaces used as thin contracts, not polymorphism

Small marker interfaces glue systems together without a shared base class:

| Interface | Implemented by | Consumed by |
|---|---|---|
| `IMovable` | `PlayerMovement` | `Input.cs` (forwards raw Input System callbacks) |
| `IRotatable` | `PlayerCamera` | `Input.cs` (`OnLook`) |
| `IAttackable` | `WeaponManager` | `Input.cs` (all attack/block/kick bindings) |
| `IButtonClick` | `WeaponManager`, `WeaponWheelUI` | `Input.cs` (`OnWeaponWheel`/`OnShowOff`/`OnHideWeapon`) |
| `IWeaponCommand` | `PlayerMovement` | `WeaponManager.Awake()` (subscribes to command events) |
| `IAnimationSender` | `WeaponBase`, `EntityHealth` | `EntityAttackAnimation.Start()` |
| `IDefender` | `WeaponManager` | `EntityHealth.ApplyDamage` (block check via `TryGetComponent<IDefender>`) |
| `IInteractable` | `Propeller` | `Mechanism.OnTriggerEnter` |

`Input.cs` is the single entry point for the New Input System — every `PlayerInput`-generated callback (`OnMove`, `OnAttackPrimary`, `OnAttackKick`, ...) looks up the relevant interface on a serialized list of target `GameObject`s and calls through it. This is the layer to look at first when adding a new input binding.

## 5. Abstract weapon hierarchy

```
WeaponBase (abstract, MonoBehaviour, IAnimationSender)
├── MeleeWeapon         — sphere-cast melee only
├── RangedWeapon         — projectile / hitscan ray only
└── MeleeRangedWeapon    — both, dispatches on AttackVariant.kind (used by hybrid weapons)
```

`WeaponBase` owns input-state machinery common to all three (press/hold/release, charge timing, cooldown, animation-command dispatch) and defers the actual hit-detection to the abstract `ExecuteAttack(AttackVariant, float charged)`. `attacks[]` is a flat array indexed positionally: `attacks[0..2]` = primary/secondary/tertiary press, `attacks[3..5]` = the *held/charged* variant of the same three — the `+3` offset is hardcoded in `HandleInput`, not expressed as a named constant.

`Projectile` follows the same shape: a base class with `protected virtual ActionOnStart/ActionOnEnd/ActionOnBulletHit` hooks, subclassed by `PistolBullet` (no-op override) and `PistolCharged` (grows in size as it absorbs same-type bullets via a `"Bullet"` layer check).

## 6. Damage pipeline

```
WeaponBase.ExecuteAttack
  → Physics.OverlapSphere/SphereCastAll (melee)  or  Raycast/Projectile (ranged)
  → DamageCalculator.CalculateDamage(DamageProfile, EntityHealth target)
        damage = baseDamage * baseMultiplier(static, global)
        × tag.vulnerableBonus   for each tag target.VulnerableTo contains
        × tag.resistancePenalty for each tag target.ResistantTo contains
  → new DamagePacket(damage, tags, effects, forceApplied, collisionPoint, charged, bodyPart)
  → EntityHealth.ApplyDamage(packet)
        - blocked? (IDefender.IsBlocking && !packet.charged) → damage nullified, OnTakeDamage(0)
        - else: run each DamageEffect.ApplyEffect(this) [Fire/Toxin/Electrification → coroutine DoT]
        - currentHealth -= damage; fire OnTakeDamage; play hit_front/hit_back anim
        - health <= 0 → OnDeath, ComplexColliderHandle.ActivateRagdoll()
```

Charge scaling: for charged attacks, `charged` is a 0..1 lerp factor between "base attack" (damage / `maxChargeMultyplier`) and full `damage.baseDamage` — computed **inline, separately, in three different files** (`MeleeWeapon`, `MeleeRangedWeapon`, `RangedWeapon`, `Projectile` all repeat the same `Mathf.Lerp(baseAttack, attack.damage.baseDamage, charged)` block rather than sharing one helper).

See [03-combat-weapons-damage.md](03-combat-weapons-damage.md) for the concrete data values.
