using Cinemachine;
using UnityEngine;
using UnityEngine.Assertions;
#if P56
using System;
using Unity.BossRoom.Utils;
#endif  // P56

namespace Unity.BossRoom.CameraUtils
{
    public class CameraController : MonoBehaviour
    {
        private CinemachineFreeLook m_MainCamera;

#if P56
        bool m_IsFPSView = true;

        public bool IsFPSView
        {
            get { return m_IsFPSView; }
        }

        Transform m_CamTransform = null;

        GameObject m_BoneHead = null;
        public GameObject BoneHead
        {
            set { m_BoneHead = value; }
        }

        float m_RotationX = 0f;

        public float RotationX
        {
            set { m_RotationX = value; }
        }

        float m_RotationY;

        public float RotationY
        {
            set { m_RotationY = value; }
        }

        // For lerp of camera view.
        PositionLerper m_PositionLerper;
        RotationLerper m_RotationLerper;
        const float k_LerpTime = 0.08f;
        Vector3 m_LerpedPosition;
        Quaternion m_LerpedRotation;
#if OVR
        float m_BaseRotationY = 180f;   // TBD
        public float BaseRotationY
        {
            set { m_BaseRotationY = value; }
        }
#endif  // OVR
#endif  // P56

        void Start()
        {
            AttachCamera();
#if P56
            m_PositionLerper = new PositionLerper(m_CamTransform.position, k_LerpTime);
            m_RotationLerper = new RotationLerper(m_CamTransform.rotation, k_LerpTime);
#endif  // P56
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
#else   // !P56
            // Inactive "CMCameraPrefab".
            GameObject cmCameraPrefab = GameObject.Find("CMCameraPrefab");
            cmCameraPrefab.SetActive(false);

            // Set main camera for FPS view
#if !OVR
            m_CamTransform = Camera.main.gameObject.transform;
            m_CamTransform.parent = transform;
            m_CamTransform.localPosition = new Vector3(0f, 1.3f, 0.3f);

            m_LerpedPosition = m_CamTransform.localPosition;
            m_LerpedRotation = m_CamTransform.rotation; // rotation is not local.
#else   // !OVR
            m_CamTransform = GameObject.Find("OVRCameraRig").transform;

            if (m_BoneHead != null)
            {
                m_BoneHead.SetActive(false);
            }

            m_CamTransform.position = transform.position + new Vector3(0f, 1.3f, 0f); ;
            m_CamTransform.position += transform.forward * 0.3f;

            m_LerpedPosition = m_CamTransform.position;
#endif  // !OVR
#endif  // !P56
        }

#if P56
        private void Update()
        {
            // Change FPS / TPS view.
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                m_IsFPSView = !m_IsFPSView;
            }

#if !OVR
            Vector3 targetPosition;
            Quaternion targetRotation;

            if (m_IsFPSView)
            {
                // FPS
                if (m_BoneHead != null && m_BoneHead.activeSelf)
                {
                    m_BoneHead.SetActive(false);
                }
                targetPosition = new Vector3(0f, 1.3f, 0.3f);
                targetRotation = Quaternion.Euler(-m_RotationX, m_RotationY, 0f);
            }
            else
            {
                // TPS
                if (m_BoneHead != null && !m_BoneHead.activeSelf)
                {
                    m_BoneHead.SetActive(true);
                }
                targetPosition = Quaternion.Euler(-m_RotationX, 0f, 0f) * new Vector3(0f, 3f, -3f);
                targetRotation = Quaternion.Euler(15f - m_RotationX, m_RotationY, 0f);
            }

            // Lerp of character's view.
            m_LerpedPosition = m_PositionLerper.LerpPosition(m_LerpedPosition, targetPosition);
            m_LerpedRotation = m_RotationLerper.LerpRotation(m_LerpedRotation, targetRotation);

            m_CamTransform.localPosition = m_LerpedPosition;
            m_CamTransform.rotation = m_LerpedRotation; // rotaion is not local.
#else   // !OVR
            // Update character's pitch.
            Vector3 targetPosition = transform.position + new Vector3(0f, 1.3f, 0f);
            targetPosition += transform.forward * 0.3f;

            // Lerp of character's view.
            m_LerpedPosition = m_PositionLerper.LerpPosition(m_LerpedPosition, targetPosition);

            m_CamTransform.position = m_LerpedPosition;

            // Rotation method for mesures to "virtual reality sickness".
            m_CamTransform.rotation = Quaternion.Euler(0f, m_BaseRotationY, 0f);
#endif  // !OVR
        }
#endif  // P56
    }
    }
