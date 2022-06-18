using Cinemachine;
using UnityEngine;
using UnityEngine.Assertions;
#if P56
using System;
#endif  // P56

namespace Unity.Multiplayer.Samples.BossRoom.Visual
{
    public class CameraController : MonoBehaviour
    {
        private CinemachineFreeLook m_MainCamera;

#if P56
        bool m_IsFPSView = true;
        Transform m_CamTransform = null;
        GameObject m_BoneHead = null;
        float m_Pitch = 0f;

        // Variables for lerping of character's view.
        PositionLerper m_PositionLerper;
        RotationLerper m_RotationLerper;
        const float k_LerpTime = 0.08f;
        Vector3 m_LerpedPosition;
        Quaternion m_LerpedRotation;
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
#else   // P56
            // Deactivate "CMCameraPrefab"
            GameObject cmCameraPrefab = GameObject.Find("CMCameraPrefab");
            cmCameraPrefab.SetActive(false);

            // Change main camera from 3rd person view to FPS view
            m_CamTransform = Camera.main.gameObject.transform;
            m_CamTransform.parent = transform;
            m_CamTransform.localPosition = new Vector3(0f, 1.3f, 0.5f);
            m_CamTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            m_LerpedPosition = m_CamTransform.localPosition;
            m_LerpedRotation = m_CamTransform.localRotation;

            // Hide character's head
            m_BoneHead = GameObject.Find("Bone_Head");
            m_BoneHead.SetActive(false);
#endif  // P56
        }

#if P56
        public void SetPitch(float pitch)
        {
            m_Pitch = pitch;
        }

        void FixedUpdate()
        {
            // Change character's view.
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                m_IsFPSView = !m_IsFPSView;
            }

            // Update character's pitch.
            Vector3 targetPosition;
            Quaternion targetRotation;

            if (m_IsFPSView)
            {
                targetPosition = new Vector3(0f, 1.3f, 0.5f);
                targetRotation = Quaternion.Euler(-m_Pitch, 0f, 0f);
                m_BoneHead.SetActive(false);
            }
            else
            {
                targetPosition = new Vector3(0f, 3f - m_Pitch / 30f, 3f * Math.Abs(m_Pitch) / 30f - 5f);
                targetRotation = Quaternion.Euler(15f - m_Pitch, 0f, 0f);
                m_BoneHead.SetActive(true);
            }

            // Lerp of character's view.
            m_LerpedPosition = m_PositionLerper.LerpPosition(m_LerpedPosition, targetPosition);
            m_LerpedRotation = m_RotationLerper.LerpRotation(m_LerpedRotation, targetRotation);

            m_CamTransform.localPosition = m_LerpedPosition;
            m_CamTransform.localRotation = m_LerpedRotation;
        }
#endif  // P56
    }
}
