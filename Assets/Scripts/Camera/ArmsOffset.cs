using UnityEngine;

/// <summary>
/// Drives the weapon/arms view-model offset with the shared procedural "life camera" effects
/// from <see cref="ProceduralCameraEffects"/>, plus its one extra trigger: a small kick on
/// left-attack ("leg hit") animations, detected via <see cref="WeaponBase.OnAnimation"/>.
/// </summary>
public class ArmsOffset : ProceduralCameraEffects
{
    [Header("References")]
    [SerializeField] private Transform armsOffset;

    [Header("Leg hit")]
    [SerializeField] private WeaponBase weaponBase;

    protected override Transform TargetTransform => armsOffset;

    // ArmsOffset breathes a half-cycle out of phase with the camera (matches original tuning).
    protected override float BreathPhaseOffset => Mathf.PI;

    protected override void Start()
    {
        base.Start();
        weaponBase.OnAnimation += OnLeghit;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        weaponBase.OnAnimation -= OnLeghit;
    }

    // On leg hit handle
    private void OnLeghit(string anim)
    {
        if (anim.Contains("left_attack"))
            ChangeLifeCameraState(LifeCameraCue.LegHit, new float[] { 0 });
    }
}
