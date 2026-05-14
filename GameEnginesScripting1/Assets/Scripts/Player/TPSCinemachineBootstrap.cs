using Unity.Cinemachine;
using StarterAssets;
using UnityEngine;

#pragma warning disable CS0618 // Cinemachine 3 still ships Transposer/Composer/Collider; API marked obsolete in favor of newer pipeline components.

/// <summary>
/// Cinemachine 3 Update: Configures a third-person camera that follows the StarterAssets target.
/// </summary>
[DefaultExecutionOrder(-200)]
public class TPSCinemachineBootstrap : MonoBehaviour
{
    [Header("Virtual camera")]
    [SerializeField] CinemachineCamera virtualCamera;

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

        // Suche nach CM3 Kamera
        CinemachineCamera vcam = virtualCamera;
        if (vcam == null)
            vcam = FindFirstObjectByType<CinemachineCamera>();

        if (vcam == null)
        {
            var go = new GameObject("CM ThirdPerson");
            vcam = go.AddComponent<CinemachineCamera>();
            vcam.Priority = 10;
        }

        var player = FindFirstObjectByType<ThirdPersonController>();
        if (player == null || player.CinemachineCameraTarget == null)
            return;

        Transform target = player.CinemachineCameraTarget.transform;
        vcam.Follow = target;
        vcam.LookAt = target;

        // --- CM3 REWRITE: Komponenten sind jetzt normale MonoBehaviours ---

        // 1. Position (Ersetzt den alten Transposer)
        var follow = vcam.GetComponent<CinemachineFollow>();
        if (follow == null)
            follow = vcam.gameObject.AddComponent<CinemachineFollow>();

        follow.FollowOffset = new Vector3(shoulderX, shoulderY, -cameraDistance);
        // Hinweis: Die Standard-Einstellungen von CinemachineFollow ersetzen das alte "LockToTargetWithWorldUp" automatisch perfekt.

        // 2. Rotation (Ersetzt den alten Composer)
        var composer = vcam.GetComponent<CinemachineRotationComposer>();
        if (composer == null)
            composer = vcam.gameObject.AddComponent<CinemachineRotationComposer>();

        composer.TargetOffset = new Vector3(0f, composerYOffset, 0f);

        // 3. Linse (m_ Präfix ist weg)
        vcam.Lens.FieldOfView = fieldOfView;

        // 4. Collider
        if (addColliderExtension && vcam.GetComponent<CinemachineCollider>() == null)
            vcam.gameObject.AddComponent<CinemachineCollider>();
    }
}