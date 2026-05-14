using Unity.Cinemachine;
using StarterAssets;
using UnityEngine;

/// <summary>
/// Cinemachine 3 Update: Configures a third-person camera that follows the StarterAssets target.
/// </summary>
[DefaultExecutionOrder(-200)]
public class TPSCinemachineBootstrap : MonoBehaviour
{
    // CM3 nutzt jetzt "CinemachineCamera" statt "CinemachineVirtualCamera"
    [SerializeField] CinemachineCamera virtualCamera;
    [SerializeField] float shoulderX = 0.35f;
    [SerializeField] float shoulderY = 0.15f;
    [SerializeField] float cameraDistance = 4f;
    [SerializeField] float composerYOffset = 1.15f;
    [SerializeField] bool addColliderExtension = true;

    void Awake()
    {
        if (Camera.main == null)
            return;

        var camGo = Camera.main.gameObject;
        if (camGo.GetComponent<CinemachineBrain>() == null)
            camGo.AddComponent<CinemachineBrain>();

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
        vcam.Lens.FieldOfView = 55f;

        // 4. Collider
        if (addColliderExtension && vcam.GetComponent<CinemachineCollider>() == null)
            vcam.gameObject.AddComponent<CinemachineCollider>();
    }
}