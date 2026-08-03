using UnityEngine;

/// <summary>
/// Base for the single-purpose capability scripts an enemy is assembled from (movement, rotation,
/// attack, vision, animation). A strategy never moves a rigidbody or fires a weapon itself -- it
/// asks the relevant system to, so the same strategy works on any entity that has equivalent
/// systems attached, and a capability can be swapped by swapping the component.
///
/// Systems are discovered by <see cref="Enemy"/> via GetComponentsInChildren, so no manual wiring
/// beyond adding the component is needed.
/// </summary>
public abstract class EnemySystem : MonoBehaviour
{
    protected Enemy enemy { get; private set; }

    /// <summary>Called once by <see cref="Enemy"/> during its initialization, before any tick.</summary>
    public virtual void Initialize(Enemy owner)
    {
        enemy = owner;
    }

    /// <summary>
    /// Driven by <see cref="Enemy"/> rather than each system having its own Update, so ordering
    /// between systems (vision before strategy before movement) is explicit and stable.
    /// </summary>
    public virtual void TickSystem(float deltaTime) { }

    /// <summary>Physics-step work, for anything touching a Rigidbody.</summary>
    public virtual void FixedTickSystem(float fixedDeltaTime) { }
}
