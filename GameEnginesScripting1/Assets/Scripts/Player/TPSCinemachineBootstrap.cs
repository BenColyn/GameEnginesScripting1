using Unity.Cinemachine;
using StarterAssets;
using UnityEngine;

#pragma warning disable CS0618 // Cinemachine 3 still ships Transposer/Composer/Collider; API marked obsolete in favor of newer pipeline components.

/// <summary>
/// Ensures Main Camera has CinemachineBrain and configures a third-person virtual camera
/// that follows the StarterAssets CinemachineCameraTarget (mouse look pivot).
/// </summary>
[DefaultExecutionOrder(-200)]
public class TPSCinemachineBootstrap : MonoBehaviour
{
    [Header("Virtual camera")]
    [SerializeField] CinemachineVirtualCamera virtualCamera;

    [Header("Framing")]
    [SerializeField, Range(20f, 80f)] float fieldOfView = 52f;
    [SerializeField] float shoulderX = 0.38f;
    [SerializeField] float shoulderY = 0.18f;
    [SerializeField] float cameraDistance = 4.5f;
    [SerializeField] float composerYOffset = 1.18f;

    [Header("Follow damping (Transposer)")]
    [SerializeField, Range(0f, 5f)] float transposerXDamping = 0.85f;
    [SerializeField, Range(0f, 5f)] float transposerYDamping = 0.55f;
    [SerializeField, Range(0f, 5f)] float transposerZDamping = 0.95f;

    [Header("Aim damping (Composer)")]
    [SerializeField, Range(0f, 5f)] float aimHorizontalDamping = 1.2f;
    [SerializeField, Range(0f, 5f)] float aimVerticalDamping = 0.95f;
    [SerializeField, Range(0f, 0.5f)] float aimDeadZoneWidth = 0.02f;
    [SerializeField, Range(0f, 0.5f)] float aimDeadZoneHeight = 0.02f;

    [Header("Brain")]
    [SerializeField, Range(0f, 2f)] float defaultBlendTime = 0.35f;

    [Header("Collision (CinemachineCollider)")]
    [SerializeField] bool addColliderExtension = true;
    [SerializeField, Range(0.01f, 0.5f)] float colliderCameraRadius = 0.18f;
    [SerializeField, Range(0f, 1f)] float colliderMinimumDistanceFromTarget = 0.22f;
    [SerializeField, Range(0f, 10f)] float colliderDamping = 2f;
    [SerializeField, Range(0f, 10f)] float colliderDampingWhenOccluded = 3.5f;
    [SerializeField, Range(0f, 2f)] float colliderSmoothingTime = 0.28f;

    void Awake()
    {
        if (Camera.main == null)
            return;

        var camGo = Camera.main.gameObject;
        var brain = camGo.GetComponent<CinemachineBrain>();
        if (brain == null)
            brain = camGo.AddComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, defaultBlendTime);

        CinemachineVirtualCamera vcam = virtualCamera;
        if (vcam == null)
            vcam = FindFirstObjectByType<CinemachineVirtualCamera>();

        if (vcam == null)
        {
            var go = new GameObject("CM ThirdPerson");
            vcam = go.AddComponent<CinemachineVirtualCamera>();
            vcam.Priority.Value = 10;
        }

        var player = FindFirstObjectByType<ThirdPersonController>();
        if (player == null || player.CinemachineCameraTarget == null)
            return;

        Transform target = player.CinemachineCameraTarget.transform;
        vcam.Follow = target;
        vcam.LookAt = target;

        var transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer == null)
            transposer = vcam.AddCinemachineComponent<CinemachineTransposer>();
        transposer.m_BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.LockToTargetWithWorldUp;
        transposer.m_FollowOffset = new Vector3(shoulderX, shoulderY, -cameraDistance);
        transposer.m_XDamping = transposerXDamping;
        transposer.m_YDamping = transposerYDamping;
        transposer.m_ZDamping = transposerZDamping;

        var composer = vcam.GetCinemachineComponent<CinemachineComposer>();
        if (composer == null)
            composer = vcam.AddCinemachineComponent<CinemachineComposer>();
        composer.m_TrackedObjectOffset = new Vector3(0f, composerYOffset, 0f);
        composer.m_HorizontalDamping = aimHorizontalDamping;
        composer.m_VerticalDamping = aimVerticalDamping;
        composer.m_DeadZoneWidth = aimDeadZoneWidth;
        composer.m_DeadZoneHeight = aimDeadZoneHeight;
        composer.m_LookaheadTime = 0f;
        composer.m_LookaheadSmoothing = 0f;

        vcam.m_Lens.FieldOfView = fieldOfView;

        if (addColliderExtension)
        {
            var collider = vcam.GetComponent<CinemachineCollider>();
            if (collider == null)
                collider = vcam.gameObject.AddComponent<CinemachineCollider>();
            collider.m_AvoidObstacles = true;
            collider.m_Strategy = CinemachineCollider.ResolutionStrategy.PreserveCameraDistance;
            collider.m_CameraRadius = colliderCameraRadius;
            collider.m_MinimumDistanceFromTarget = colliderMinimumDistanceFromTarget;
            collider.m_Damping = colliderDamping;
            collider.m_DampingWhenOccluded = colliderDampingWhenOccluded;
            collider.m_SmoothingTime = colliderSmoothingTime;
            collider.m_CollideAgainst = ~(1 << player.gameObject.layer);
        }
    }
}
