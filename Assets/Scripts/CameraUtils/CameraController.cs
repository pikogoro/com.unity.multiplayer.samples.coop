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

        // Variables for lerping of character's view.
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
            // Deactivate "CMCameraPrefab"
            GameObject cmCameraPrefab = GameObject.Find("CMCameraPrefab");
            cmCameraPrefab.SetActive(false);

            // Change main camera from 3rd person view to FPS view
#if !OVR
            m_CamTransform = Camera.main.gameObject.transform;
            m_CamTransform.parent = transform;
            //m_CamTransform.position = transform.position + new Vector3(0f, 1.3f, 0f);
            //m_CamTransform.position += transform.forward * 0.3f;
            m_CamTransform.localPosition = transform.position + new Vector3(0f, 1.3f, 0.3f);

            //m_LerpedPosition = m_CamTransform.position;
            //m_LerpedRotation = m_CamTransform.rotation;
            m_LerpedPosition = m_CamTransform.localPosition;
            m_LerpedRotation = m_CamTransform.localRotation;
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
            // Change character's view.
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                m_IsFPSView = !m_IsFPSView;
            }
        }

        void FixedUpdate()
        {
#if !OVR
            // Update character's pitch.
            Vector3 targetPosition;
            Quaternion targetRotation;

            if (m_IsFPSView)
            {
                // FPS
                if (m_BoneHead != null && m_BoneHead.activeSelf)
                {
                    m_BoneHead.SetActive(false);
                    //m_CamTransform.parent = null;
                    //m_LerpedPosition = m_CamTransform.position;
                    //m_LerpedRotation = m_CamTransform.rotation;
                }
                //targetRotation = Quaternion.Euler(-m_RotationX, m_RotationY, 0f);
                targetRotation = Quaternion.Euler(-m_RotationX, 0f, 0f);
                //targetPosition = transform.position + new Vector3(0.0f, 1.3f, 0f);
                //targetPosition += transform.forward * 0.3f;
                targetPosition = new Vector3(0.0f, 1.3f, 0.3f);
            }
            else
            {
                // TPS
                if (m_BoneHead != null && !m_BoneHead.activeSelf)
                {
                    m_BoneHead.SetActive(true);
                    //m_CamTransform.parent = transform;
                    //m_LerpedPosition = m_CamTransform.localPosition;
                    //m_LerpedRotation = m_CamTransform.localRotation;
                }
                //targetRotation = Quaternion.Euler(15f - m_RotationX, m_RotationY, 0f);
                targetRotation = Quaternion.Euler(15f - m_RotationX, 0f, 0f);
                //targetPosition = new Vector3(0f, 3f - m_RotationX / 30f, 3f * Math.Abs(m_RotationX) / 30f - 5f);
                targetPosition = new Vector3(0f, 2f, -3f);
            }

            // Lerp of character's view.
            m_LerpedPosition = m_PositionLerper.LerpPosition(m_LerpedPosition, targetPosition);
            m_LerpedRotation = m_RotationLerper.LerpRotation(m_LerpedRotation, targetRotation);

            //if (m_IsFPSView)
            //{
            //    m_CamTransform.position = m_LerpedPosition;
            //    m_CamTransform.rotation = m_LerpedRotation;
            //}
            //else
            //{
                m_CamTransform.localPosition = m_LerpedPosition;
                m_CamTransform.localRotation = m_LerpedRotation;
            //}
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
