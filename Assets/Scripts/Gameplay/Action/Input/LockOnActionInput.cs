using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.BossRoom.Gameplay.UI;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Infrastructure;
using Unity.Netcode;

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// </summary>
    public class LockOnActionInput : BaseActionInput
    {
        UILockOnCanvas m_UILockOnCanvas;

        List<UILockOn> m_UILockOnList = new List<UILockOn>();

        RectTransform m_ReticleTransform;
        const float k_LockOnRaycastDistance = 100f;
        readonly RaycastHit[] k_CachedHit = new RaycastHit[4];
        LayerMask m_LockOnLayerMask;
        LayerMask m_TargetLayerMask;
        RaycastHitComparer m_RaycastHitComparer;

        protected Vector3 m_Position = new Vector3(0f, 2f, 0f);
        protected Vector3 m_Direction = Vector3.up;

        float k_LockOnMinRange = 10f;

        bool m_IsMouseButtonUp = false;

        void Start()
        {
            m_LockOnLayerMask = LayerMask.GetMask(new[] { "PCs", "NPCs", "Environment", "Default", "Ground" });
            m_TargetLayerMask = LayerMask.GetMask(new[] { "PCs", "NPCs" });
            m_RaycastHitComparer = new RaycastHitComparer();

            m_UILockOnCanvas = GameObject.Find("LockOnSight").GetComponent<UILockOnCanvas>();
            m_ReticleTransform = GameObject.Find("Reticle").GetComponent<RectTransform>();
        }

        bool IsLockOn(Transform transform)
        {
            foreach (UILockOn ui in m_UILockOnList)
            {
                if (ui.TargetTransform == transform)
                {
                    return true;
                }
            }
            return false;
        }

        void RemoveInvalidLockOn()
        {
            for (int i = m_UILockOnList.Count - 1; i >= 0; i--)
            {
                //Ray ray = new Ray(Camera.main.ScreenToWorldPoint(m_ReticleTransform.position), m_UILockOnList[i].TargetTransform.position);
                Vector3 origin = Camera.main.ScreenToWorldPoint(m_ReticleTransform.position);
                Vector3 direction = (m_UILockOnList[i].TargetTransform.position - origin).normalized;
                Ray ray = new Ray(origin, direction);

                int hits = Physics.RaycastNonAlloc(ray,
                    k_CachedHit,
                    k_LockOnRaycastDistance,
                    m_LockOnLayerMask);

                if (hits > 0)
                {
                    if (hits > 1)
                    {
                        // sort hits by distance
                        Array.Sort(k_CachedHit, 0, hits, m_RaycastHitComparer);
                    }

                    for (int j = 0; j < hits; j++)
                    {
                        if (k_CachedHit[i].collider.gameObject.name != "PlayerAvatar0") // Except self
                        {
                            if (m_UILockOnList[i].TargetTransform == null ||
                                k_CachedHit[j].distance < k_LockOnMinRange ||
                                k_CachedHit[j].transform != m_UILockOnList[i].TargetTransform)
                            {
                                m_UILockOnCanvas.ReleaseUILockOn(m_UILockOnList[i]);
                                m_UILockOnList.RemoveAt(i);
                            }
                            break;
                        }
                    }
                }

                // TODO out of range
            }
        }

        private void Update()
        {
            // Launch
            if (Input.GetMouseButtonUp(0))
            {
                m_IsMouseButtonUp = true;
            }
        }

        void FixedUpdate()
        {
            // Remove invalid lock on
            RemoveInvalidLockOn();

            // Launch
            if (m_IsMouseButtonUp)
            {
                foreach(UILockOn ui in m_UILockOnList)
                {
                    var targetNetObj = ui.TargetTransform.GetComponentInParent<NetworkObject>();
                    var data = new ActionRequestData
                    {
                        Position = m_Position,
                        Direction = m_Direction,
                        ActionID = m_ActionPrototypeID,
                        ShouldQueue = false,
                        TargetIds = new ulong[] { targetNetObj.NetworkObjectId }
                    };
                    m_SendInput(data);
                    m_UILockOnCanvas.ReleaseUILockOn(ui);
                }
                m_UILockOnList.Clear();
                Destroy(gameObject);
                return;
            }

            // Check lock on
            Ray ray = Camera.main.ScreenPointToRay(m_ReticleTransform.position);

            int hits = Physics.RaycastNonAlloc(ray,
                k_CachedHit,
                k_LockOnRaycastDistance,
                m_LockOnLayerMask);

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
                            if (!IsLockOn(k_CachedHit[i].transform) &&
                                k_LockOnMinRange <= k_CachedHit[0].distance)
                            {
                                UILockOn ui = m_UILockOnCanvas.GetUILoclOn();
                                if (ui != null)
                                {
                                    ui.TargetTransform = k_CachedHit[i].transform;
                                    m_UILockOnList.Add(ui);
                                }
                            }
                            break;
                        }
                    }
                }
            }
        }

        public class RaycastHitComparer : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit x, RaycastHit y)
            {
                return x.distance.CompareTo(y.distance);
            }
        }
    }
}
