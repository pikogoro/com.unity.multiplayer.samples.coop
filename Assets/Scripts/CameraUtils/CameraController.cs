using Cinemachine;
using UnityEngine;
using UnityEngine.Assertions;
#if P56
using System;
using Unity.BossRoom.Utils;
using UnityEngine.UI;
using System.Collections.Generic;
#endif  // P56

namespace Unity.BossRoom.CameraUtils
{
    public class CameraController : MonoBehaviour
    {
#if !P56
        private CinemachineFreeLook m_MainCamera;
#else   //!P56
        private CinemachineVirtualCamera m_MainCamera;
        private CinemachineTransposer m_Transposer;

        bool m_IsFPSView = true;

        public bool IsFPSView
        {
            get { return m_IsFPSView; }
        }

#if OVR
        Transform m_CamTransform = null;
#endif  //OVR


        GameObject m_Head = null;
        public GameObject Head
        {
            set { m_Head = value; }
        }

        float m_RotationX = 0f;

        GameObject m_Eyes = null;
        public GameObject Eyes
        {
            set { m_Eyes = value;  }
        }

        GameObject m_View = null;
        public GameObject View
        {
            set { m_View = value; }
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

        Vector3 m_AimPosition;
        public Vector3 AimPosition
        {
            get { return m_AimPosition; }   // world position
        }

        // Zoom
        int m_ZoomLevel;

        // For lerp of camera view.
        PositionLerper m_PositionLerper;
        RotationLerper m_RotationLerper;
        const float k_LerpTime = 0.08f;
        Vector3 m_LerpedPosition;
        Quaternion m_LerpedRotation;

        // Aiming
        Image m_ReticleImage;
        RectTransform m_ReticleTransform;
        Vector2 m_ReticleOriginalPosition;
        const float k_AimingRaycastDistance = 100f;
        readonly RaycastHit[] k_CachedHit = new RaycastHit[4];
        LayerMask m_AimingLayerMask;
        LayerMask m_TargetLayerMask;

        Transform m_Target;
        public Transform Target { get { return m_Target; } }

        RaycastHitComparer m_RaycastHitComparer;
#if OVR
        float m_BaseRotationY = 180f;   // TBD
        public float BaseRotationY
        {
            set { m_BaseRotationY = value; }
        }
#endif  // OVR
#endif  // !P56

        void Start()
        {
            AttachCamera();
#if P56
#if OVR
            m_PositionLerper = new PositionLerper(m_CamTransform.position, k_LerpTime);
#endif  // OVR
            m_RotationLerper = new RotationLerper(m_MainCamera.transform.localRotation, k_LerpTime);

            m_AimingLayerMask = LayerMask.GetMask(new[] { "PCs", "NPCs", "Environment", "Default", "Ground" });
            //m_AimingLayerMask = LayerMask.GetMask(new[] { "NPCs", "Environment", "Default", "Ground" });
            m_TargetLayerMask = LayerMask.GetMask(new[] { "PCs", "NPCs" });
            //m_TargetLayerMask = LayerMask.GetMask(new[] { "NPCs" });
            m_RaycastHitComparer = new RaycastHitComparer();
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
            m_MainCamera = GameObject.FindObjectOfType<CinemachineVirtualCamera>();
            Assert.IsNotNull(m_MainCamera, "CameraController.AttachCamera: Couldn't find gameplay virtual camera");

            m_Transposer = m_MainCamera.GetComponentInChildren<CinemachineTransposer>();

            ZoomReset();

            GameObject go = GameObject.Find("Reticle");
            if (go != null) {
                m_ReticleImage = go.GetComponent<Image>();
                m_ReticleTransform = go.GetComponent<RectTransform>();
                m_ReticleOriginalPosition = m_ReticleTransform.position;
            }

            // Set main camera for FPS view
#if OVR
            m_CamTransform = GameObject.Find("OVRCameraRig").transform;

            if (m_BoneHead != null)
            {
                m_BoneHead.SetActive(false);
            }

            m_CamTransform.position = transform.position + new Vector3(0f, 1.3f, 0f); ;
            m_CamTransform.position += transform.forward * 0.3f;

            m_LerpedPosition = m_CamTransform.position;
#endif  // OVR
#endif  // !P56
        }

#if P56
        private void LateUpdate()   // this is LateUpdate
        {
            // Change FPS / TPS view.
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                m_IsFPSView = !m_IsFPSView;
            }

#if !OVR
            Quaternion targetRotation;

            if (m_IsFPSView)
            {
                // FPS
                if (m_Head != null && m_Head.activeSelf)
                {
                    m_Head.SetActive(false);
                    //m_ReticleTransform.position = m_ReticleOriginalPosition;
                    m_MainCamera.Follow = m_View.transform;
                    m_MainCamera.LookAt = null;
                    m_Transposer.m_FollowOffset = new Vector3(0f, 0f, 0f);
                    m_Transposer.m_XDamping = 0f;
                    m_Transposer.m_YDamping = 0f;
                    m_Transposer.m_ZDamping = 0f;
                }
                m_Transposer.m_FollowOffset = transform.InverseTransformPoint(m_Eyes.transform.position) - m_View.transform.localPosition;
                targetRotation = Quaternion.Euler(-m_RotationX, m_RotationY, 0f);
            }
            else
            {
                // TPS
                if (m_Head != null && !m_Head.activeSelf)
                {
                    m_Head.SetActive(true);
                    //m_ReticleTransform.position = m_ReticleOriginalPosition + new Vector2(0f, 50f);
                    m_MainCamera.Follow = m_View.transform;
                    m_MainCamera.LookAt = m_View.transform;
                    m_Transposer.m_FollowOffset = new Vector3(1f, 0f, 0f);
                    m_Transposer.m_XDamping = 0.1f;
                    m_Transposer.m_YDamping = 0.1f;
                    m_Transposer.m_ZDamping = 1f;
                }
                float offsetY = m_RotationX / 30f;
                float offsetZ = m_RotationX / 20f;
                m_Transposer.m_FollowOffset.y = 0.2f - offsetY;
                m_Transposer.m_FollowOffset.z = -5f + Mathf.Abs(offsetZ);
                targetRotation = Quaternion.Euler(-m_RotationX, m_RotationY, 0f);
            }

            // For crouching
            m_View.transform.localPosition = transform.InverseTransformPoint(m_Eyes.transform.position);

            // Lerp of character's pitch.
            m_LerpedRotation = m_RotationLerper.LerpRotation(m_LerpedRotation, targetRotation);
            m_MainCamera.transform.localRotation = m_LerpedRotation;
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

        private void FixedUpdate()
        {
            // Aiming
            m_Target = null;    // Reset target

            Ray ray = Camera.main.ScreenPointToRay(m_ReticleTransform.position);

            int hits = Physics.RaycastNonAlloc(ray,
                k_CachedHit,
                k_AimingRaycastDistance,
                m_AimingLayerMask);

            if (hits > 0)
            {
                if (hits > 1)
                {
                    // sort hits by distance
                    Array.Sort(k_CachedHit, 0, hits, m_RaycastHitComparer);
                }

                for (int i = 0; i < hits; i++)
                {
                    if (k_CachedHit[i].collider.gameObject.name != "PlayerAvatar0") // Except self
                    {
                        int layerTest = 1 << k_CachedHit[i].collider.gameObject.layer;
                        if ((layerTest & m_TargetLayerMask) != 0)
                        {
                            m_ReticleImage.color = new Color(1f, 0f, 0f, 1f);   // Red
                            if (k_CachedHit[i].distance < 3f)
                            {
                                m_AimPosition = ray.origin + ray.direction * k_AimingRaycastDistance;
                            }
                            else
                            {
                                m_AimPosition = k_CachedHit[i].point;

                                // Set target
                                m_Target = k_CachedHit[i].transform;
                            }
                        }
                        else
                        {
                            m_ReticleImage.color = new Color(1f, 1f, 1f, 1f);   // Green
                            m_AimPosition = ray.origin + ray.direction * k_AimingRaycastDistance;
                        }
                        break;
                    }

                    if (i == hits)
                    {
                        m_ReticleImage.color = new Color(1f, 1f, 1f, 1f);   // Green
                        m_AimPosition = ray.origin + ray.direction * k_AimingRaycastDistance;
                    }
                }
            }
            else
            {
                m_ReticleImage.color = new Color(1f, 1f, 1f, 1f);   // Green
                m_AimPosition = ray.origin + ray.direction * k_AimingRaycastDistance;
            }
        }

        public class RaycastHitComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit x, RaycastHit y)
            {
                return x.distance.CompareTo(y.distance);
            }
        }

        public void ZoomReset()
        {
            m_MainCamera.m_Lens.FieldOfView = 55f;
        }

        public void ZoomUp()
        {
            if (m_MainCamera.m_Lens.FieldOfView > 3.4375f)
            {
                m_MainCamera.m_Lens.FieldOfView /= 2f;
            }
        }

        public void ZoomDown()
        {
            if (m_MainCamera.m_Lens.FieldOfView < 27.5f)
            {
                m_MainCamera.m_Lens.FieldOfView *= 2f;
            }
        }
#endif  // P56
    }
}
