using Cinemachine;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Multiplayer.Samples.BossRoom.Visual
{
    public class CameraController : MonoBehaviour
    {
        private CinemachineFreeLook m_MainCamera;

        void Start()
        {
            AttachCamera();
        }

        private void AttachCamera()
        {
#if !P56
            m_MainCamera = GameObject.FindObjectOfType<CinemachineFreeLook>();
            Assert.IsNotNull(m_MainCamera, "CameraController.AttachCamera: Couldn't find gameplay freelook camera");

            if (m_MainCamera)
            {
                // camera body / aim
                m_MainCamera.Follow = transform;
                m_MainCamera.LookAt = transform;
                // default rotation / zoom
                m_MainCamera.m_Heading.m_Bias = 40f;
                m_MainCamera.m_YAxis.Value = 0.5f;
            }
#else   // P56
            // Deactivate "CMCameraPrefab"
            GameObject cmCameraPrefab = GameObject.Find("CMCameraPrefab");
            cmCameraPrefab.SetActive(false);

            // Change main camera from 3rd person view to FPS view
            Transform camTransform = Camera.main.gameObject.transform;
            camTransform.parent = transform;
            camTransform.localPosition = new Vector3(0f, 1.3f, 0.5f);
            camTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            // Hide character's head
            GameObject boneHead = GameObject.Find("Bone_Head");
            boneHead.SetActive(false);
#endif  // P56
        }
    }
}
