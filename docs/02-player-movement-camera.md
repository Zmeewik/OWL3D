# Player Movement & Camera

## Body states

`PlayerMovement.BodyState`: `Moving | Dashing | WallRunning | Sliding | InAir | SlidingGround`

`FixedUpdate` dispatches per-frame logic by `switch (currentState)`, each case delegating to the matching subsystem script (see [01-architecture.md](01-architecture.md) §2 for the full list of scripts). Every transition path:

```
Grounded, no wall contact         → Moving
Slope contact, 3+ consistent frames → Sliding      (PlayerSurface.HandleSlope, counterNormal>3 debounce)
Wall contact in air, 3+ consistent frames → WallRunning (PlayerSurface.HandleWall)
No contacts / leaves ground        → InAir          (OnFly)
Crouch+moving+fast enough, grounded → SlidingGround (PlayerMovement.OnCrouch → PlayerSlide.StartSlideGround)
Dash input                          → Dashing        (PlayerDash.OnDash, ends via Invoke("EndDash", dashTime))
```

`WallRunning` itself has a nested sub-state (`PlayerWallRun.WallState`): `Sliding | Running | Climbing | HangUp`. Entry into `Climbing` vs `Running` is decided in `WallRunStart()` by comparing the player's forward vector and move input against the wall normal (dot > 0.7 in both → climb; move input roughly toward camera-forward → horizontal run; otherwise → passive wall-slide).

## Momentum / speed system (`PlayerMomentum`)

Not a physics quantity — a **scalar accumulator** ("momentum", 0..`maxMomentum`) nudged by named events, which linearly interpolates the player's actual speed cap:

```csharp
speedPointList = { wallrun: +3, jump: +1, slide: +1, "not moving": -1, "none": -0.01 }
momentum = clamp(momentum + speedPoints[eventName], 0, maxMomentum)
currentMaxSpeed = maxLowSpeed + momentum/maxMomentum * (maxTopSpeed - maxLowSpeed)
```
So wall-running is the fastest way to build speed; standing still bleeds it off almost instantly (`"not moving"` = −1 per call vs `"none"` = −0.01 idle decay). `BuildSpeed(type)` is called from many places with ad-hoc type strings (`"jump"`, `"dash"`, `"slide"`, `"slide_ground"`, `"crouch"`, `"not moving"`, `"none"`) — same string-bus caveat as elsewhere.

## Movement subsystems — behavior notes

- **PlayerWalking** — force-based acceleration projected onto the current ground-normal plane (`Vector3.ProjectOnPlane`), so movement follows slopes. `Drag()` kills residual velocity when no input. `CounterMovement()` applies a brake force whenever horizontal speed exceeds `currentMaxSpeed × crouchMultiplier` (both grounded and airborne). `RotateBody()` snaps the rigidbody's yaw to the camera's forward every frame (no independent player-facing direction — the body always faces where the camera looks).
- **PlayerCrouch** — smooth height/collider lerp over `crouchTime`; before standing, raycasts (5 rays: center + 4 offsets) to check headroom and refuses to stand if blocked (`isStandingUp` latch, re-checked every frame via `PlayerMovement.FixedUpdate`).
- **PlayerDash** — one-shot horizontal impulse toward camera-relative move direction (defaults to camera-forward if no input), plus a small upward impulse (`dashUpForce`). Locks state to `Dashing` for `dashTime`, then normalizes velocity into `InAir`.
- **PlayerJump** — standard ground jump; wall jump variant pushes away from the wall normal and is capped by `wallrunMaxJumps` (after which vertical impulse is skipped, only horizontal push-off remains).
- **PlayerFly** — tracks `currentFallTime`/`currentDownFallTime` every frame while airborne; `OnFly()`/`OnLand()` are the two transition edges, firing different life-camera events depending on the *previous* state (`lastState == WallRunning` → `"fall"`, `lastState == Moving` → `"fallcliff"`, landing hard → `"land"` scaled by impact force above `minFallForce`).
- **PlayerSlide** — two unrelated behaviors sharing one class name: `Slide()` (continuous slope-sliding, adds force down-slope up to `maxSlideSpeed`, "sticks" the player to the surface) vs `StartSlideGround()`/`SlideGround()` (a timed speed-boost slide triggered by crouching while moving fast on flat ground, decaying from `minSlideGroundSpeed`→`maxSlideGroundSpeed`-scaled force to zero over `slideGroundTime`).
- **PlayerSurface** — classifies every physics contact by angle (`SurfaceHandler.GetSurfaceType`: Ground/Slope/Wall/Ceiling thresholds, with a *different, tighter* ground-angle threshold while crouching) and picks the "main" contact per frame (closest-to-up for ground, closest-to-90° for slope/wall). Requires **3 consecutive frames** of a consistent-angle wall/slope contact before committing to that state — a basic debounce against single-frame corner catches.
- **PlayerWallRun** — see body-states above. Horizontal run decays its push force via `SmoothStep` over `wallrunTime`; climb decays via the same over `wallClimbTime`. Both cap velocity and snap it back if the force-integration overshoots. Reference to the currently-attached wall `Transform` is tracked (`wallReferenceSaved`) so re-touching the *same* wall doesn't restart the "already ran" flag, letting a to-different-wall transition retrigger the acceleration curve from zero.
- **PlayerHangUp** — ledge mantle. `HangUpCheck()` (called from `PlayerWallRun.ClimbWallRun`) raycasts to find a ledge above; if found, computes a target landing point and drives the rigidbody through it with two hand-derived parabolic-arc `AddForce(..., ForceMode.VelocityChange)` calls (80% of `currentFinalHangUpTime` spent going up, 20% going forward) rather than a kinematic tween. `currentFinalHangUpTime` is randomized per-mantle between `minHangUpTime`/`maxHangUpTime`.

## Camera: two parallel "life camera" scripts

Both `PlayerCamera` (actual camera transform + FOV) and `ArmsOffset` (weapon/arms view-model offset) independently subscribe to `PlayerMovement.OnLifeCameraAction` and run near-identical procedural-shake state machines. See [01-architecture.md](01-architecture.md) §3 for why they're near-duplicates and what's drifted between them (`ArmsOffset` has `Fallcliff`/`Leghit` states `PlayerCamera` lacks).

Both compute a `Vector3 offsetTarget` / `Quaternion rotationTarget` per active state, then smooth toward it every `LateUpdate` via `Vector3.Lerp`/`Quaternion.Slerp` at `smoothSpeed`/`smoothRotationSpeed`. State-specific behavior (all sine/perlin-noise/SmoothDamp driven, no animation curves):

| State | Effect |
|---|---|
| `none` (idle) | `Breath()` — sine-wave vertical offset, amplitude/speed lerped by a decaying `breathMultiplyer` (builds up from recent movement, decays over `relaxTime`) |
| `movement` | Sine/cosine walk-cycle bob, amplitude scales with speed fraction (walk↔run lerp) |
| `jump` / `dash` / `land` / `fallcliff` / `leghit` | Instant angle kick on entry (e.g. dash pitches view by `-dashAngle`), then `SmoothDamp`s back to zero over a fixed time; auto-clears via a delayed coroutine (`NullStateDelayed`) |
| `slide` | Perlin-noise-driven random Z-tilt |
| `wallrun` | Directional shake (climb vs horizontal run use different amplitude sets) plus a Z-roll toward the wall, magnitude scaled by how directly the player faces along the wall (`wallrunRightDot`) |
| `hangup` | Random left/right roll direction, weighted by how "exposed" the ledge is (`hangupPercent` — 1.0 if a wall is detected in front, 0.3 otherwise) |
| `fall` | Perlin-tilt + random-circle positional shake, magnitude ramps in over `fallToMinTime`→`fallToMaxTime` |

`PlayerCamera` additionally owns FOV: `ChangeFOV(alpha)` (called from `PlayerMovement.FixedUpdate` when speed particles are active) lerps toward `savedFov + alpha * FOVOffset`, using a **faster** SmoothDamp time when *increasing* FOV (`changeFOVTime/4`) than when decreasing (`changeFOVTime`) — FOV punches in fast, eases out slow.

All per-state tuning constants (angles, shake amplitudes, times) are `[SerializeField]` on the components, not in a ScriptableObject — values are set per-instance in the scene/prefab Inspector, not centrally documented in code.

## Movement tuning values (`Assets/Configs/Movement.asset`)

The single `PlayerMovementConfig` ScriptableObject instance in use:

| Field | Value | | Field | Value |
|---|---|---|---|---|
| acceleration | 100 | | wallrunMaxForceY | 5 |
| decceleration | 30 | | wallrunTime | 1 |
| maxSpeed | 15 | | wallrunMaxJumps | 3 |
| jumpForce | 15 | | wallClimbForce | 10 |
| dashDistance | 10 | | wallClimbTime | 0.5 |
| dashUpForce | 30 | | wallClimbMaxJumps | 2 |
| dashTime | 0.3 | | wallSlideMaxSpeed | 3 |
| airControlMultiplier | 0.5 | | wallMovementMultiplier | 0.2 |
| minFallForce | 20 | | minHangUpTime | 0.3 |
| flyMaxParticleTime | 0.5 | | maxHangUpTime | 0.4 |
| maxSlideSpeed | 20 | | hangUpHeight | 0.4 |
| slideSpeed | 10 | | forwardOffset | 1 |
| maxSlideGroundSpeed | 1 | | crouchHeadOffset | 0.6 |
| minSlideGroundSpeed | 0 | | crouchMaxMultiplyer | 0.6 |
| minSlideGroundMovementSpeed | 0.3 | | crouchTime | 0.2 |
| slideGroundTime | 0.6 | | maxTopSpeed | 12 |
| wallrunForceX | 20 | | maxLowSpeed | 9 |
| wallRunForceY | 10 | | maxMomentum | 10 |
| wallrunMaxForceX | 10 | | | |

Note `maxSpeed` (15, top-level field) is distinct from and not obviously reconciled with `maxTopSpeed`/`maxLowSpeed` (12/9, used by the momentum system) — `maxSpeed` doesn't appear to be read by any of the scripts in this pass; the momentum-derived `currentMaxSpeed` (bounded by `maxLowSpeed`/`maxTopSpeed`) is what actually gates player speed.
