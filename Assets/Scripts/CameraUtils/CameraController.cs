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

        GameObject m_HeadGO = null;
        public GameObject HeadGO
        {
            set { m_HeadGO = value; }
        }

        float m_RotationX = 0f;

        GameObject m_EyesGO = null;
        public GameObject EyesGO
        {
            set { m_EyesGO = value;  }
        }

        Vector3 m_EyesPosition = new Vector3(0f, 1.3f, 0.3f);    // default eyes position
        public Vector3 EyesPosition
        {
            get { return m_EyesPosition; }
        }

        //Vector3 m_MuzzlePosition = new Vector3(0f, 1.3f, 0.3f);    // default eyes position
        Transform m_MuzzleTransform = null;
        public Vector3 MuzzlePosition
        {
            get { return transform.InverseTransformPoint(m_MuzzleTransform.position); }
        }

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

        // IK
        Transform m_RightHandRoot = null;
        Transform m_RightHandIKTarget = null;
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

            // Setup eyes position.
            if (m_EyesGO != null)
            {
                m_EyesPosition = m_EyesGO.transform.localPosition;
                m_EyesPosition.x = 0f;
            }

            // IK
            GameObject go = GameObject.Find("RightHandRoot");
            if (go != null)
            {
                m_RightHandRoot = go.transform;
            }

            go = GameObject.Find("RightHandIK_target");
            if (go != null)
            {
                m_RightHandIKTarget = go.transform;
            }

            go = GameObject.Find("muzzle"); // [TBD] "muzzle"
            if (go != null)
            {
                m_MuzzleTransform = go.transform;
            }

            // Set main camera for FPS view
#if !OVR
            m_CamTransform = Camera.main.gameObject.transform;
            m_CamTransform.parent = transform;
            //m_CamTransform.localPosition = new Vector3(0f, 1.3f, 0.3f);
            m_CamTransform.localPosition = m_EyesPosition;

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
                if (m_HeadGO != null && m_HeadGO.activeSelf)
                {
                    m_HeadGO.SetActive(false);
                }
                //targetPosition = new Vector3(0f, 1.3f, 0.3f);
                targetPosition = m_EyesPosition;
                targetRotation = Quaternion.Euler(-m_RotationX, m_RotationY, 0f);
            }
            else
            {
                // TPS
                if (m_HeadGO != null && !m_HeadGO.activeSelf)
                {
                    m_HeadGO.SetActive(true);
                }
                targetPosition = Quaternion.Euler(-m_RotationX, 0f, 0f) * new Vector3(0f, 3f, -3f); // [TBD] position is temporary.
                targetRotation = Quaternion.Euler(15f - m_RotationX, m_RotationY, 0f);
            }

            // Lerp of character's view.
            m_LerpedPosition = m_PositionLerper.LerpPosition(m_LerpedPosition, targetPosition);
            m_LerpedRotation = m_RotationLerper.LerpRotation(m_LerpedRotation, targetRotation);

            m_CamTransform.localPosition = m_LerpedPosition;
            m_CamTransform.rotation = m_LerpedRotation; // rotaion is not local.

            // IK
            if (m_RightHandIKTarget != null)
            {
                m_RightHandIKTarget.localPosition = Quaternion.Euler(-m_RotationX, 0f, 0f) * new Vector3(0f, 0f, 1f) + m_RightHandRoot.localPosition;
                m_RightHandIKTarget.localRotation = Quaternion.Euler(90f - m_RotationX, 0f, 0f);
            }
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
