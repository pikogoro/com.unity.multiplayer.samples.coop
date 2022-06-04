using Cinemachine;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Multiplayer.Samples.BossRoom.Visual
{
    public class CameraController : MonoBehaviour
    {
        private CinemachineFreeLook m_MainCamera;

#if P56
        private bool m_IsFPSView = true;
        private Transform m_CamTransform = null;
        private GameObject m_BoneHead = null;
#endif  // P56

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
            m_CamTransform = Camera.main.gameObject.transform;
            m_CamTransform.parent = transform;
            m_CamTransform.localPosition = new Vector3(0f, 1.3f, 0.5f);
            m_CamTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            // Hide character's head
            m_BoneHead = GameObject.Find("Bone_Head");
            m_BoneHead.SetActive(false);
#endif  // P56
        }

#if P56
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                m_IsFPSView = !m_IsFPSView;

                if (m_IsFPSView)
                {
                    m_CamTransform.localPosition = new Vector3(0f, 1.3f, 0.5f);
                    m_CamTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                    m_BoneHead.SetActive(false);
                }
                else
                {
                    m_CamTransform.localPosition = new Vector3(0f, 3f, -5f);
                    m_CamTransform.localRotation = Quaternion.Euler(15f, 0f, 0f);
                    m_BoneHead.SetActive(true);    
                }
            }
        }
#endif  // P56
    }
}
