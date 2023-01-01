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
            get { return m_EyesPosition; }  // local position
        }

        Transform m_MuzzleTransform = null;
        public Vector3 MuzzlePosition
        {
            get { return m_MuzzleTransform.position; }  // world position
        }
        public Vector3 MuzzleLocalPosition
        {
            get { return transform.worldToLocalMatrix.MultiplyPoint(m_MuzzleTransform.position); } // world position -> local position
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

        // For lerp of camera view.
        PositionLerper m_PositionLerper;
        RotationLerper m_RotationLerper;
        const float k_LerpTime = 0.08f;
        Vector3 m_LerpedPosition;
        Quaternion m_LerpedRotation;

        // IK
        Transform m_RightHandRoot = null;
        Transform m_RightHandIKTarget = null;
        Transform m_RightHandPivot = null;

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
#endif  // P56

        void Start()
        {
            AttachCamera();
#if P56
            m_PositionLerper = new PositionLerper(m_CamTransform.position, k_LerpTime);
            m_RotationLerper = new RotationLerper(m_CamTransform.rotation, k_LerpTime);

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
                m_RightHandPivot = go.transform.Find("RightHandPivot");
            }

            // Aiming
            go = GameObject.Find("muzzle"); // [TBD] "muzzle"
            if (go != null)
            {
                m_MuzzleTransform = go.transform;
            }

            go = GameObject.Find("Reticle");
            if (go != null) {
                m_ReticleImage = go.GetComponent<Image>();
                m_ReticleTransform = go.GetComponent<RectTransform>();
                m_ReticleOriginalPosition = m_ReticleTransform.position;
            }

            // Set main camera for FPS view
#if !OVR
            m_CamTransform = Camera.main.gameObject.transform;
            m_CamTransform.parent = transform;
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
                    //m_ReticleTransform.position = m_ReticleOriginalPosition;
                }
                targetPosition = m_EyesPosition;
                targetRotation = Quaternion.Euler(-m_RotationX, m_RotationY, 0f);
            }
            else
            {
                // TPS
                if (m_HeadGO != null && !m_HeadGO.activeSelf)
                {
                    m_HeadGO.SetActive(true);
                    //m_ReticleTransform.position = m_ReticleOriginalPosition + new Vector2(0f, 50f);
                }
                //targetPosition = Quaternion.Euler(-m_RotationX, 0f, 0f) * new Vector3(0f, 3f, -4.5f); // [TBD] under position
                targetPosition = Quaternion.Euler(-m_RotationX, 0f, 0f) * new Vector3(1f, 3f, -4.5f); // [TBD] side position
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

        private void FixedUpdate()
        {
            // IK
            if (m_RightHandIKTarget != null)
            {
                m_RightHandIKTarget.localPosition = Quaternion.Euler(-m_RotationX, 0f, 0f) * new Vector3(0f, 0f, 1f) + m_RightHandRoot.localPosition;
                m_RightHandIKTarget.localRotation = Quaternion.Euler(90f, 90f, 0f);
                m_RightHandPivot.localRotation = Quaternion.Euler(0f, -m_RotationX, 0f);
            }

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
                            m_ReticleImage.color = new Color(0f, 1f, 0f, 1f);   // Green
                            m_AimPosition = ray.origin + ray.direction * k_AimingRaycastDistance;
                        }
                        break;
                    }

                    if (i == hits)
                    {
                        m_ReticleImage.color = new Color(0f, 1f, 0f, 1f);   // Green
                        m_AimPosition = ray.origin + ray.direction * k_AimingRaycastDistance;
                    }
                }
            }
            else
            {
                m_ReticleImage.color = new Color(0f, 1f, 0f, 1f);   // Green
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
#endif  // P56
    }
    }
