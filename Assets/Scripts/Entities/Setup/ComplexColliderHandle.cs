using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class ComplexColliderHandle : MonoBehaviour
{
    [Serializable]
    public class HitboxPart
    {
        public string name;
        public Transform bone;
        public BoxCollider collider;
        public Rigidbody rb;
        public CharacterJoint joint;
        public List<HitboxPart> children = new List<HitboxPart>();
    }

    [Header("Hitbox setup")]
    public Vector3 defaultColliderSize = new Vector3(0.01f, 0.01f, 0.01f);
    public bool isTriggerByDefault = true;
    public float rigidbodyMass = 1f;

    [Header("Body structure")]
    public List<HitboxPart> rootParts = new List<HitboxPart>();

    [Header("Hinge joints (knees, elbows)")]
    [Tooltip("Bones whose joint is a one-way hinge. Case-insensitive substring match on the bone name.")]
    public string[] hingeBoneKeywords = { "leg", "shin", "knee", "forearm", "elbow" };

    [Tooltip("How far the limb may bend, measured from fully straight. Anatomical range, not joint-relative.")]
    public float hingeMaxFlexion = 140f;

    [Tooltip("How far past straight the limb may go the wrong way. Keep small -- this is what stops knees " +
             "and elbows inverting.")]
    public float hingeHyperextension = 3f;

    [Tooltip("Sideways/twisting slack left on a hinge. Small values keep it a hinge rather than a ball joint.")]
    public float hingeSideSlack = 3f;

    [SerializeField] private Rigidbody mainRigidbody;
    [SerializeField] private Collider mainCollider;
    [SerializeField] private Animator[] animators;
    [SerializeField] private BoxCollider[] boxCollidersAdditional;
    [SerializeField] private Rigidbody[] rigidbodiesAdditional;

    
    [ContextMenu("Set All Colliders New Mass")]
    public void ChangeCollidersMass()
     {
         SetMassAll(rigidbodyMass);
     } 

    // Collider and Rigidbody initialization
    [ContextMenu("Initialize Colliders (With Size)")]
    public void InitializeCollidersWithSize()
    {
        foreach (var part in rootParts)
            InitializePart(part, null, true);
        Debug.Log("ComplexColliderHandle: Colliders and Rigidbody created (with size override).");
    }

    [ContextMenu("Initialize Colliders (Keep Size)")]
    public void InitializeCollidersKeepSize()
    {
        foreach (var part in rootParts)
            InitializePart(part, null, false);
        Debug.Log("ComplexColliderHandle: Colliders and Rigidbody created (keeping existing size).");
    }

    void InitializePart(HitboxPart part, HitboxPart parentPart, bool overrideSize)
    {
        if (part.bone == null) return;

        // Add BoxCollider
        if (part.collider == null)
        {
            var box = part.bone.GetComponent<BoxCollider>();
            if (box == null)
                box = part.bone.gameObject.AddComponent<BoxCollider>();

            if (overrideSize)
            {
                box.size = defaultColliderSize;
            }
            else
            {
                // Try to fit collider to mesh bounds if possible
                var mr = part.bone.GetComponent<MeshRenderer>();
                if (mr != null)
                    box.size = mr.bounds.size;
            }

            box.isTrigger = isTriggerByDefault;
            part.collider = box;
        }

        // Add Rigidbody
        if (part.rb == null)
        {
            var rb = part.bone.GetComponent<Rigidbody>();
            if (rb == null)
                rb = part.bone.gameObject.AddComponent<Rigidbody>();
            rb.mass = rigidbodyMass;
            rb.isKinematic = true; // ragdoll off by default
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // continuous
            rb.interpolation = RigidbodyInterpolation.Interpolate; // smooth movement
            part.rb = rb;
        }

        // Add CharacterJoint for ragdoll
        if (parentPart != null && part.joint == null)
        {
            var joint = part.bone.GetComponent<CharacterJoint>();
            if (joint == null)
                joint = part.bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parentPart.rb;
            joint.enableProjection = true; // prevent joints from breaking
            part.joint = joint;
        }

        // Recursively initialize children
        foreach (var child in part.children)
            InitializePart(child, part, overrideSize);
    }

    /// <summary>
    /// Turns knee and elbow joints into one-way hinges so a ragdoll can't invert them.
    ///
    /// The bend axis is measured from the rig's own rest pose rather than assumed to be a body axis:
    /// these limbs are authored with a deliberate pre-bend (~25 deg at the knees, ~30 at the elbows),
    /// and the cross product of the two bone segments gives both the exact hinge axis and its sign --
    /// rotating positively about it continues the bend the animator intended. That makes this work
    /// for left and right limbs, and for knees bending backwards while elbows bend forwards, without
    /// any per-bone sign table.
    ///
    /// Run it from the context menu after building the rig, in the rest pose. A limb posed perfectly
    /// straight is skipped: with no pre-bend there is nothing to tell the two directions apart.
    /// </summary>
    [ContextMenu("Setup Hinge Limits (knees/elbows)")]
    public void SetupHingeLimits()
    {
        var joints = GetComponentsInChildren<CharacterJoint>(true);
        int configured = 0, skipped = 0;

        // Snapshot the chain before touching anything: configuring a joint replaces the component,
        // which would invalidate entries of a live array mid-iteration.
        var hinges = new List<CharacterJoint>();
        foreach (var joint in joints)
            if (IsHingeBone(joint.gameObject.name))
                hinges.Add(joint);

        foreach (var joint in hinges)
        {
            if (!TryConfigureHinge(joint, joints))
                skipped++;
            else
                configured++;
        }

        Debug.Log($"[{name}] Hinge limits: {configured} configured, {skipped} skipped.", this);
    }

    private bool IsHingeBone(string boneName)
    {
        if (hingeBoneKeywords == null) return false;

        string lower = boneName.ToLowerInvariant();
        foreach (var keyword in hingeBoneKeywords)
            if (!string.IsNullOrEmpty(keyword) && lower.Contains(keyword.ToLowerInvariant()))
                return true;

        return false;
    }

    private bool TryConfigureHinge(CharacterJoint joint, CharacterJoint[] allJoints)
    {
        if (joint.connectedBody == null)
            return false;

        // The next bone down the chain is found through the joint graph rather than the transform
        // hierarchy, so an extra non-physics transform between bones can't break it.
        var ownBody = joint.GetComponent<Rigidbody>();
        Transform childBone = null;
        foreach (var other in allJoints)
        {
            if (other != joint && other.connectedBody == ownBody)
            {
                childBone = other.transform;
                break;
            }
        }

        if (childBone == null)
            return false;

        Vector3 parentSegment = joint.transform.position - joint.connectedBody.transform.position;
        Vector3 childSegment = childBone.position - joint.transform.position;
        if (parentSegment.sqrMagnitude < 1e-8f || childSegment.sqrMagnitude < 1e-8f)
            return false;

        parentSegment.Normalize();
        childSegment.Normalize();

        Vector3 axisWorld = Vector3.Cross(parentSegment, childSegment);
        if (axisWorld.sqrMagnitude < 1e-6f)
        {
            Debug.LogWarning($"[{name}] '{joint.gameObject.name}' is straight in the rest pose, so the " +
                             "bend direction can't be derived. Pose it slightly bent and re-run.", this);
            return false;
        }

        axisWorld.Normalize();

        // Point every hinge axis the same way across the body, then let the SIGN of the rest bend
        // about it decide which side flexion is on. Letting the axis itself flip per joint doesn't
        // work: the cross product comes out near +right at the knees but near -right at the elbows
        // (they bend opposite ways), and with the limits always written as "a little backwards, a
        // lot forwards" that silently inverted every elbow. Canonicalising the axis moves that
        // difference into the limits, where it is explicit.
        if (Vector3.Dot(axisWorld, transform.right) < 0f)
            axisWorld = -axisWorld;

        // Signed, so a limb that flexes against the canonical axis reports a negative rest bend.
        float signedRest = Vector3.SignedAngle(parentSegment, childSegment, axisWorld);
        float restBend = Mathf.Abs(signedRest);

        // A CharacterJoint freezes its zero-rotation reference when it is created, not when its
        // limits are edited. These joints were built by InitializePart in whatever pose the rig
        // happened to be in, so limits written against the *current* pose were being applied around
        // some other zero -- which is why a knee that measured correct on one leg came out inverted
        // on the other. Rebuilding the component here makes "zero == this pose" true by
        // construction, so the numbers below mean what they say.
        var go = joint.gameObject;
        var connected = joint.connectedBody;
        var anchor = joint.anchor;
        bool projection = joint.enableProjection;
        DestroyImmediate(joint);

        var hinge = go.AddComponent<CharacterJoint>();
        hinge.connectedBody = connected;
        hinge.anchor = anchor;
        hinge.enableProjection = projection;

        hinge.axis = go.transform.InverseTransformDirection(axisWorld);
        hinge.swingAxis = go.transform.InverseTransformDirection(childSegment);

        // Room to keep bending the way the limb already bends, and only a sliver the other way.
        // Which of the two twist limits is the roomy one depends on which side of the canonical
        // axis this limb flexes towards.
        float toStraight = restBend + hingeHyperextension;
        float toFlexed = Mathf.Max(0f, hingeMaxFlexion - restBend);

        var low = hinge.lowTwistLimit;
        var high = hinge.highTwistLimit;
        if (signedRest >= 0f)
        {
            low.limit = -toStraight;
            high.limit = toFlexed;
        }
        else
        {
            low.limit = -toFlexed;
            high.limit = toStraight;
        }
        hinge.lowTwistLimit = low;
        hinge.highTwistLimit = high;

        var swing1 = hinge.swing1Limit;
        swing1.limit = hingeSideSlack;
        hinge.swing1Limit = swing1;

        var swing2 = hinge.swing2Limit;
        swing2.limit = hingeSideSlack;
        hinge.swing2Limit = swing2;

        return true;
    }

    // Collider control
    public void SetEnabledAll(bool state)
    {
        foreach (var part in rootParts)
            SetEnabledRecursive(part, state);
    }

    void SetEnabledRecursive(HitboxPart part, bool state)
    {
        if (part.collider != null)
            part.collider.enabled = state;
        foreach (var child in part.children)
            SetEnabledRecursive(child, state);
    }

    // Set mass for all body parts
    public void SetMassAll(float mass)
    {
        foreach (var part in rootParts)
            SetMassRecursive(part, mass);
    }

    void SetMassRecursive(HitboxPart part, float mass)
    {
        if (part.rb != null)
            part.rb.mass = mass;
        foreach (var child in part.children)
            SetMassRecursive(child, mass);
    }

    // Trigger control
    public void SetTriggerAll(bool isTrigger)
    {
        foreach (var part in rootParts)
            SetTriggerRecursive(part, isTrigger);
    }

    void SetTriggerRecursive(HitboxPart part, bool isTrigger)
    {
        if (part.collider != null)
        {
            part.collider.isTrigger = isTrigger;
            part.rb.isKinematic = isTrigger;
        }

        foreach (var child in part.children)
            SetTriggerRecursive(child, isTrigger);
    }

    // Ragdoll control
    public void ActivateRagdoll()
    {
        // Disable Animator
        foreach (var animator in animators)
        {
            animator.enabled = false;
        }
        
        // Disable ragdoll triggers and isKinetic
        // Enable ragdoll colliders & rigidbodies
        SetEnabledAll(true);
        SetTriggerAll(false);
        
        // Disable main collider
        if (mainCollider != null)
        {
            mainCollider.enabled = false;
            mainRigidbody.isKinematic = true;
        }
        
        // Activate weapon ragdolls
        foreach (var rb in rigidbodiesAdditional)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = false;
        }
        
        foreach (var col in boxCollidersAdditional)
        {
            col.isTrigger = false;
        }
        
    }

    public void DeactivateRagdoll()
    {
        // Re-enable Animator
        foreach (var animator in animators)
        {
            animator.enabled = true;
        }
        
        // Disable ragdoll triggers and isKinetic
        // Disable ragdoll rigidbodies & set colliders to trigger
        SetEnabledAll(false);
        SetTriggerAll(true);
        
        // Re-enable main collider
        if (mainCollider != null)
        {
            mainCollider.enabled = true; 
            mainRigidbody.isKinematic = false;
        }
        
        // Deactivate weapon ragdolls
        foreach (var rb in rigidbodiesAdditional)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
        }
        
        foreach (var col in boxCollidersAdditional)
        {
            col.isTrigger = true;
        }
        
    }
    
    // Set pelvis upper side
    public void DeactivateGravity()
    {
        DeactivateGravityRecursively(false);
    }

    public void ActivateGravity()
    {
        DeactivateGravityRecursively(true);
    }
    
    private void DeactivateGravityRecursively(bool gravity)
    {
        foreach (var part in rootParts)
            SetGravityRecursive(part, gravity);
    }

    void SetGravityRecursive(HitboxPart part, bool gravity)
    {
        if (part.rb != null)
            part.rb.useGravity = gravity;
        foreach (var child in part.children)
            SetGravityRecursive(child, gravity);
    }

    void SetRagdollRecursive(HitboxPart part, bool state)
    {
        if (part.rb != null)
            part.rb.isKinematic = !state;
        if (part.collider != null)
            part.collider.isTrigger = !state;
        foreach (var child in part.children)
            SetRagdollRecursive(child, state);
    }

    // Debug structure
    public void PrintStructure()
    {
        foreach (var part in rootParts)
            PrintPart(part, 0);
    }

    void PrintPart(HitboxPart part, int depth)
    {
        string indent = new string(' ', depth * 2);
        Debug.Log($"{indent}- {part.name}");
        foreach (var child in part.children)
            PrintPart(child, depth + 1);
    }

    // Create standard humanoid structure
    [ContextMenu("Create Standard Humanoid")]
    public void CreateStandardHumanoid()
    {
        rootParts.Clear();

        // Torso
        var torsoUpper = new HitboxPart { name = "Torso_Upper", bone = new GameObject("Torso_Upper").transform };
        var torsoLower = new HitboxPart { name = "Torso_Lower", bone = new GameObject("Torso_Lower").transform };
        torsoUpper.children.Add(torsoLower);
        rootParts.Add(torsoUpper);

        // Head
        var head = new HitboxPart { name = "Head", bone = new GameObject("Head").transform };
        torsoUpper.children.Add(head);

        // Left arm
        var leftShoulder = new HitboxPart { name = "Left_Shoulder", bone = new GameObject("Left_Shoulder").transform };
        var leftForearm = new HitboxPart { name = "Left_Forearm", bone = new GameObject("Left_Forearm").transform };
        var leftHand = new HitboxPart { name = "Left_Hand", bone = new GameObject("Left_Hand").transform };
        leftShoulder.children.Add(leftForearm);
        leftForearm.children.Add(leftHand);
        torsoUpper.children.Add(leftShoulder);

        // Right arm
        var rightShoulder = new HitboxPart { name = "Right_Shoulder", bone = new GameObject("Right_Shoulder").transform };
        var rightForearm = new HitboxPart { name = "Right_Forearm", bone = new GameObject("Right_Forearm").transform };
        var rightHand = new HitboxPart { name = "Right_Hand", bone = new GameObject("Right_Hand").transform };
        rightShoulder.children.Add(rightForearm);
        rightForearm.children.Add(rightHand);
        torsoUpper.children.Add(rightShoulder);

        // Left leg
        var leftThigh = new HitboxPart { name = "Left_Thigh", bone = new GameObject("Left_Thigh").transform };
        var leftShin = new HitboxPart { name = "Left_Shin", bone = new GameObject("Left_Shin").transform };
        var leftFoot = new HitboxPart { name = "Left_Foot", bone = new GameObject("Left_Foot").transform };
        leftThigh.children.Add(leftShin);
        leftShin.children.Add(leftFoot);
        torsoLower.children.Add(leftThigh);

        // Right leg
        var rightThigh = new HitboxPart { name = "Right_Thigh", bone = new GameObject("Right_Thigh").transform };
        var rightShin = new HitboxPart { name = "Right_Shin", bone = new GameObject("Right_Shin").transform };
        var rightFoot = new HitboxPart { name = "Right_Foot", bone = new GameObject("Right_Foot").transform };
        rightThigh.children.Add(rightShin);
        rightShin.children.Add(rightFoot);
        torsoLower.children.Add(rightThigh);

        // Parent assignment
        foreach (var part in rootParts)
            SetParentRecursive(part, this.transform);

        Debug.Log("Standard Humanoid structure created.");
    }

    void SetParentRecursive(HitboxPart part, Transform parent)
    {
        if (part.bone != null)
            part.bone.SetParent(parent, false);
        foreach (var child in part.children)
            SetParentRecursive(child, part.bone);
    }

    

    // Auto bone generation
    [ContextMenu("Auto Generate Bones from Root")]
    public void AutoGenerateBones()
    {
        if (transform.childCount == 0)
        {
            Debug.LogWarning("No child objects found under this transform.");
            return;
        }

        rootParts.Clear();

        foreach (Transform child in transform)
        {
            var part = CreatePartRecursive(child);
            if (part != null)
                rootParts.Add(part);
        }

        Debug.Log($"Auto bone structure created with {CountParts(rootParts)} valid mesh parts.");
    }

    HitboxPart CreatePartRecursive(Transform current)
    {
        // Проверяем наличие меша
        bool hasMesh = current.GetComponent<MeshRenderer>() != null ||
                       current.GetComponent<SkinnedMeshRenderer>() != null;

        // Рекурсивно обрабатываем потомков
        List<HitboxPart> validChildren = new List<HitboxPart>();
        foreach (Transform child in current)
        {
            var childPart = CreatePartRecursive(child);
            if (childPart != null)
                validChildren.Add(childPart);
        }

        // Если у текущего объекта нет меша и нет валидных детей — не добавляем
        if (!hasMesh && validChildren.Count == 0)
            return null;

        // Создаём HitboxPart только если есть меш или дети с мешами
        var part = new HitboxPart
        {
            name = current.name,
            bone = current,
            children = validChildren
        };

        return part;
    }

    int CountParts(List<HitboxPart> parts)
    {
        int count = 0;
        foreach (var part in parts)
            count += CountPartRecursive(part);
        return count;
    }

    int CountPartRecursive(HitboxPart part)
    {
        int count = 1;
        foreach (var child in part.children)
            count += CountPartRecursive(child);
        return count;
    }



    // Remove all colliders from all bones
    [ContextMenu("Remove All Colliders")]
    public void RemoveAllColliders()
    {
        int removedCount = 0;
        foreach (var part in rootParts)
            removedCount += RemoveCollidersRecursive(part);

        Debug.Log($"Removed {removedCount} colliders from ragdoll structure.");
    }

    int RemoveCollidersRecursive(HitboxPart part)
    {
        int count = 0;
        if (part.bone != null)
        {
            var colliders = part.bone.GetComponents<Collider>();
            foreach (var c in colliders)
            {
                DestroyImmediate(c);
                count++;
            }
        }

        foreach (var child in part.children)
            count += RemoveCollidersRecursive(child);

        return count;
    }
}
