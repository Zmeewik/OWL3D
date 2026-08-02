# Combat, Weapons & Damage

## Weapon class hierarchy

See [01-architecture.md](01-architecture.md) §5 for the class diagram. Runtime flow for one attack:

```
Input.cs (New Input System callback, e.g. OnAttackPrimary)
  → IAttackable.OnAttackPressed(0)                     [WeaponManager]
  → WeaponManager.OnAttackPressed → CurrentWeapon.HandleInput(index, AttackInputType.Pressed)
  → WeaponBase.OnPressed → (if not chargeable) TryDoAttack → ExecuteAttack(attack)
  → [MeleeWeapon | RangedWeapon | MeleeRangedWeapon].ExecuteAttack — hit detection + damage
```

`AttackInputType`: `Pressed | Held | Released`. `WeaponBase.attacks[]` layout: index 0/1/2 = primary/secondary/tertiary press variant, index **3/4/5 = the same three slots' held/charge variant** (`HandleInput`'s `Held`/`Released` branches read `attacks[index+3]` — a hardcoded offset, not a named constant, so `attacks[]` must always be sized to hold both variants of each of the 3 slots).

### Weapon behavior variants

| Class | `kind` handled | Hit detection |
|---|---|---|
| `MeleeWeapon` | `Melee` only | `Physics.OverlapSphere` (radius) ∪ `Physics.SphereCastAll` (radius, forward, range) — deduplicated into a `HashSet<Collider>` |
| `RangedWeapon` | `Projectile`, `Ray` | Instantiates `Projectile`/`HitObject` prefab, or a single `Physics.Raycast` for hitscan |
| `MeleeRangedWeapon` | Both (dispatches on `attack.kind`) | Same melee sphere-cast code duplicated, plus same ranged code duplicated — this class is a literal merge of the other two's bodies, not composition |

Every hit path independently repeats the same damage math: charged attacks lerp from `(computed base damage) / maxChargeMultyplier` up to `damage.baseDamage` at `charged=1`; uncharged/`charged == -1` attacks use the full `DamageCalculator.CalculateDamage` result unscaled. Knockback force is lerped from 0 the same way. This block appears near-verbatim in `MeleeWeapon.ExecuteAttack`, `MeleeRangedWeapon.AttackMelee`, `MeleeRangedWeapon.AttackRanged`, `RangedWeapon.ExecuteAttack`, and `Projectile.FixedUpdate`.

### Projectiles

`Projectile` (base) does its own `Physics.RaycastAll` sweep each `FixedUpdate` between last and current position (rather than relying on trigger colliders) to avoid tunneling through thin colliders at high speed. Ignores its own collider and its `owner`'s named transform. On hit: applies `DamagePacket`, positions/parents the `HitObject` decal at the hit point, plays a `"MetalHit"` particle effect, then self-destroys.

- **`PistolBullet`** — thin override, no special behavior (`ActionOnBulletHit` calls base regardless of branch — the `if/else` is a no-op split).
- **`PistolCharged`** — grows visually (`transform.localScale`) as it launches based on `chargeFactor`, and **absorbs** other `PistolBullet`s it collides with (checked via `hit.collider.name.Contains("PistolBullet")`, string-match on GameObject name, not a layer/tag/type check), increasing its own charge/size further (`bulletIncrease` per absorption, capped at 1).

### Weapon management (`WeaponManager`)

- Holds `weapons[]` + a separate always-available `leg` weapon (kick — `OnLegHit` bypasses weapon switching entirely).
- Switching weapons: `PutAway()` on the old weapon immediately, then a coroutine delay (`timeToChange`) before `PickUp()` on the new one — so there's a hardcoded "holster" window during which no weapon is drawn.
- `IsBlocking` state gate: holding block (`OnBlockPressed`) cancels any in-progress attack input (`OnAttackCancelled`); `EntityHealth.ApplyDamage` checks `IDefender.IsBlocking` and fully nullifies non-charged damage while blocking (charged attacks still connect — block does not stop charged hits).
- `HandleCommand(string)` is a second, parallel input path: it receives string commands from `IWeaponCommand` senders (currently only `PlayerMovement`, e.g. when starting a wall-climb it broadcasts `"block_end"` + `"left_attack_cancel"` + `"right_attack_cancel"` to force-cancel active attacks/blocks) and re-dispatches them to the same `On*` methods driven by direct input — two independent trigger paths converging on one API.

## Damage pipeline data types

```
AttackVariant (ScriptableObject)  — one per attack "move": kind, cooldown, charge params, damage ref, range/radius, projectile refs, knockback
DamageProfile (ScriptableObject) — baseDamage + DamageTag[] + DamageEffect[]
DamageTag (ScriptableObject)     — tagName, color, resistancePenalty, vulnerableBonus
DamageEffect (abstract SO)       — FireEffect / ToxinEffect / ElectrificationEffect — each just a damage-per-second coroutine over a duration, only differing in menu name and constant fields (three copy-pasted classes)
DamagePacket (struct)            — computed damage + tags/effects + force vector + collision point + charged flag + optional bodyPart Rigidbody, passed at the moment of impact
```

`DamageCalculator.CalculateDamage` (static): `baseDamage × baseMultiplier(static field, global, currently 1) × Π(tag.vulnerableBonus for each tag in target.VulnerableTo) × Π(tag.resistancePenalty for each tag in target.ResistantTo)`. `EntityHealth.VulnerableTo`/`ResistantTo` are per-instance `List<DamageTag>` — set per enemy/player in the Inspector, not derived from anything automatic.

**⚠ Data bug found:** every `DamageTag` asset's `tagName` field is literally the string `"Ninja"`, regardless of the asset's own name (`Fire.asset`, `Crushing.asset`, `Piercing.asset`, `Toxin.asset`, etc. all contain `tagName: Ninja`). Confirmed by direct read of multiple files, not a grep artifact. Does not affect the actual resistance math (object-reference comparison via `List.Contains`, not string comparison) — but `tagName` is dead/wrong data for any UI or debug output that reads it. Not fixed, per instructions to leave the project untouched.

### Damage tag values (`Scripts/ScriptableObjects/Weapons/DamageTags/`)

| Asset (file name) | resistancePenalty | vulnerableBonus |
|---|---|---|
| Assault | 1 | 1 |
| Engeneer | 1 | 1 |
| Hacker | 1 | 1 |
| Ninja | 1 | 1 |
| Blast | 1.5 | 1.5 |
| Crushing | 1.5 | 1.5 |
| Cutting | 1.5 | 1.5 |
| Electrification | 1.5 | 1.5 |
| Lazer | 1.5 | 1.5 |
| Piercing | 1.5 | 1.5 |
| Toxin | 1.5 | 1.5 |
| Fire | 2 | 2 |

(`ElectrificationEffect.asset`/`FireEffect.asset`/`ToxinEffect.asset` under this same folder are actually `DamageEffect` assets, not tags — empty tag fields above reflects that, not missing data.)

### Damage profile values (base damage)

| Weapon | Normal | Charged | Notes |
|---|---|---|---|
| Melee (generic/kick) | 2 | — | `Character/Melee.asset` — the weakest hit in the game by far |
| Boxing | 10 | 30 | + "Reinforced" upgrade variant: 20 / 60 |
| Kunai (melee) | 10 | 20 | |
| Kunai (thrown) | 10 | 20 | separate profile from melee despite same numbers |
| Kunai Fantom (melee) | 20 | 40 | "Fantom" appears to be an upgraded Kunai tier |
| Kunai Fantom (thrown) | 20 | — | no separate charged profile found |
| Pistol | 20 | 40 | + "Upgraded" variant: 30 / 60 |

### Attack variant tuning (`Scripts/ScriptableObjects/Weapons/AttackVariants/`)

`kind`: `0 = Melee, 1 = Projectile, 2 = Ray` (enum ordinal as stored in YAML).

| Asset | kind | cooldown | chargeable | range | radius | knockback | projectileSpeed |
|---|---|---|---|---|---|---|---|
| Melee.asset (generic) | Melee | 0.7 | no | 4 | 0.5 | 1000 | — |
| Boxing | Melee | 0.3 | no | 4 | 0.5 | 600 | — |
| BoxingCharged | Melee | 0.3 | yes | 4 | 0.5 | 900 | — |
| BoxingUpgrade | Melee | 0.3 | no | 4 | 0.5 | 600 | — |
| BoxingUpgradeCharged | Melee | 0.3 | yes | 4 | 0.5 | 900 | — |
| KunaiMelee | Melee | 0.3 | no | 4 | 0.5 | 400 | — |
| KunaiMeleeCharged | Melee | 0.3 | yes | 4 | 0.5 | 400 | — |
| KunaiFantomMelee | Melee | 0.3 | no | 4 | 0.5 | 400 | — |
| KunaiFantomMeleeCharged | Melee | 0.3 | yes | 4 | 0.5 | 400 | — |
| KunaiRange (thrown) | Projectile | 0.5 | no | — | — | 400 | 40 |
| KunaiFantomRange | Projectile | 0.3 | no | — | — | 400 | 40 |
| Pistol | Projectile | 0.3 | no | — | — | 400 | 40 |
| PistolCharged | Projectile | 0.3 | yes | — | — | 900 | 15 |
| PistolUpgrade | Projectile | 0.3 | no | — | — | 400 | 40 |
| PistolUpgradeCharged | Projectile | 0.3 | yes | — | — | 900 | 15 |

Notable: `PistolCharged`/`PistolUpgradeCharged` have a **lower** `projectileSpeed` (15) than their uncharged counterparts (40) despite doing 2× knockback — a slower, harder-hitting charged shot, consistent with `PistolCharged.cs`'s grow-in-flight behavior needing time to visually read.

## Health, block, and death

`EntityHealth` (per-entity component): `maxHealth`, per-body-part damage multipliers (`Bodypart[]`: head ×2.0, body ×1.0, arms ×1.2, legs ×1.2 — declared as C# defaults in the field initializer, not data-driven from a ScriptableObject), `VulnerableTo`/`ResistantTo` tag lists, `OnTakeDamage`/`OnDeath`/`OnHealed` events.

`ApplyDamage` order of operations: early-out if already dead → block check (via `IDefender`, only blocks non-charged hits) → run each `DamageEffect.ApplyEffect` (fire/toxin/electrification each spin up their own `StartCoroutine` DoT loop on the *target*, independent of the packet's instantaneous damage) → subtract instantaneous damage → fire `OnTakeDamage` → play `hit_front`/`hit_back` (chosen by dot product of packet's force direction against the entity's forward) → if health ≤ 0, fire `OnDeath` and hand off to `ComplexColliderHandle` for ragdoll.

Note the **per-body-part damage multiplier is declared but never read** in the `ApplyDamage` path shown — damage always uses the flat calculated value, not `Bodypart.damageMultiplyer` scaled by which collider was actually hit. (`DamagePacket.bodyPart` carries a `Rigidbody` reference for knockback-application purposes on death, not for damage scaling.)

### Ragdoll (`ComplexColliderHandle`)

A generic recursive body-part tree (`HitboxPart`: bone transform + `BoxCollider` + `Rigidbody` + `CharacterJoint`, with a `children` list) built either by hand (`rootParts` set in Inspector) or auto-generated (`AutoGenerateBones` walks the transform hierarchy for any child with a `MeshRenderer`/`SkinnedMeshRenderer`). `ActivateRagdoll()`: disables the `Animator`(s), enables every part's collider as solid (not-trigger) + non-kinematic, disables the main capsule collider. `DeactivateRagdoll()` reverses all of it. A full `[CustomEditor]` (`ComplexColliderHandleEditor`) exposes every `[ContextMenu]` method as an Inspector button (init colliders, mass, triggers, ragdoll toggle, structure printout, standard-humanoid template, auto-bone-gen, remove-all-colliders) — this is clearly an internal rigging tool, not gameplay code.
