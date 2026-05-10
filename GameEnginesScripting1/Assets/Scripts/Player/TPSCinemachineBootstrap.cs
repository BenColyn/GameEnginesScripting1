using Cinemachine;
using StarterAssets;
using UnityEngine;

/// <summary>
/// Ensures Main Camera has CinemachineBrain and configures a third-person virtual camera
/// that follows the StarterAssets CinemachineCameraTarget (mouse look pivot).
/// </summary>
[DefaultExecutionOrder(-200)]
public class TPSCinemachineBootstrap : MonoBehaviour
{
    [SerializeField] CinemachineVirtualCamera virtualCamera;
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

        CinemachineVirtualCamera vcam = virtualCamera;
        if (vcam == null)
            vcam = FindFirstObjectByType<CinemachineVirtualCamera>();

        if (vcam == null)
        {
            var go = new GameObject("CM ThirdPerson");
            vcam = go.AddComponent<CinemachineVirtualCamera>();
            vcam.m_Priority = 10;
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
        transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
        transposer.m_FollowOffset = new Vector3(shoulderX, shoulderY, -cameraDistance);

        var composer = vcam.GetCinemachineComponent<CinemachineComposer>();
        if (composer == null)
            composer = vcam.AddCinemachineComponent<CinemachineComposer>();
        composer.m_TrackedObjectOffset = new Vector3(0f, composerYOffset, 0f);

        vcam.m_Lens.FieldOfView = 55f;

        if (addColliderExtension && vcam.GetComponent<CinemachineCollider>() == null)
            vcam.gameObject.AddComponent<CinemachineCollider>();
    }
}
