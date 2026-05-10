using StarterAssets;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// Adds Animation Rigging Multi-Aim on the chest/spine so the upper body subtly follows the camera aim (TPS-style).
/// Requires com.unity.animation.rigging.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class PlayerAdamAnimationRigging : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float aimWeight = 0.35f;
    [SerializeField] string spineBoneName = "Bip01 Spine1";
    [SerializeField] string spineBoneFallback = "Bip01 Spine";

    RigBuilder _rigBuilder;
    MultiAimConstraint _aim;
    Transform _aimTarget;
    Transform _spine;

    void Awake()
    {
        _spine = FindChildRecursive(transform, spineBoneName)
                 ?? FindChildRecursive(transform, spineBoneFallback);
        if (_spine == null)
        {
            enabled = false;
            return;
        }

        var tpc = GetComponent<ThirdPersonController>();
        if (tpc == null || tpc.CinemachineCameraTarget == null)
        {
            enabled = false;
            return;
        }

        _aimTarget = new GameObject("RigAimTargetWorld").transform;
        _aimTarget.SetParent(transform.root, false);

        _rigBuilder = GetComponent<RigBuilder>();
        if (_rigBuilder == null)
            _rigBuilder = gameObject.AddComponent<RigBuilder>();

        var rigGo = new GameObject("AnimationRig");
        rigGo.transform.SetParent(transform, false);
        var rig = rigGo.AddComponent<Rig>();
        rig.weight = 1f;

        var aimGo = new GameObject("ChestMultiAim");
        aimGo.transform.SetParent(rigGo.transform, false);
        _aim = aimGo.AddComponent<MultiAimConstraint>();
        _aim.weight = aimWeight;

        var data = _aim.data;
        data.constrainedObject = _spine;
        var sources = new WeightedTransformArray(1);
        sources.Add(new WeightedTransform(_aimTarget, 1f));
        data.sourceObjects = sources;
        data.aimAxis = MultiAimConstraintData.Axis.Z;
        data.upAxis = MultiAimConstraintData.Axis.Y;
        data.worldUpType = MultiAimConstraintData.WorldUpType.SceneUp;
        _aim.data = data;

        _rigBuilder.layers.Clear();
        _rigBuilder.layers.Add(new RigLayer(rig));

        UpdateAimTargetPosition(tpc);

        if (!_rigBuilder.Build())
            enabled = false;
    }

    void LateUpdate()
    {
        var tpc = GetComponent<ThirdPersonController>();
        if (tpc == null || Camera.main == null || _aimTarget == null)
            return;
        UpdateAimTargetPosition(tpc);
    }

    void UpdateAimTargetPosition(ThirdPersonController tpc)
    {
        Transform pivot = tpc.CinemachineCameraTarget.transform;
        _aimTarget.position = pivot.position + pivot.forward * 5f;
    }

    static Transform FindChildRecursive(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindChildRecursive(root.GetChild(i), name);
            if (f != null)
                return f;
        }
        return null;
    }
}
