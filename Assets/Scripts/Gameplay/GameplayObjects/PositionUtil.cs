using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.BossRoom.Gameplay.Actions;
using System;
using UnityEngine.Assertions;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
    public class PositionUtil
    {
        const float k_GroundRaycastDistance = 100f;
        readonly RaycastHit[] k_CachedHit = new RaycastHit[4];
        LayerMask m_GroundLayerMask;
        RaycastHitComparer m_RaycastHitComparer;

        public PositionUtil()
        {
            m_GroundLayerMask = LayerMask.GetMask(new[] { "Ground", "Environment" });   // ground and environment
            m_RaycastHitComparer = new RaycastHitComparer();
        }

        public Vector3 GetGroundPosition(Ray ray)
        {
            Vector3 groundPosition = Vector3.zero;
            var hits = Physics.RaycastNonAlloc(ray, k_CachedHit, k_GroundRaycastDistance, m_GroundLayerMask);
            if (hits > 0)
            {
                if (hits > 1)
                {
                    // sort hits by distance
                    Array.Sort(k_CachedHit, 0, hits, m_RaycastHitComparer);
                }

                groundPosition = k_CachedHit[0].point;
            }

            return groundPosition;
        }

        public Vector3 GetGroundPosition(Vector3 position)
        {
            Vector3 groundPosition = Vector3.zero;
            var ray = new Ray(position + new Vector3(0f, 0.5f, 0f), Vector3.down);
            var hits = Physics.RaycastNonAlloc(ray, k_CachedHit, k_GroundRaycastDistance, m_GroundLayerMask);

            if (hits > 0)
            {
                if (hits > 1)
                {
                    // sort hits by distance
                    Array.Sort(k_CachedHit, 0, hits, m_RaycastHitComparer);
                }

                groundPosition = k_CachedHit[0].point;
            }

            return groundPosition;
        }

        public Vector3 GetGroundPosition(Vector3 position, float radius)
        {
            Vector3 groundPosition = Vector3.zero;
            var ray = new Ray(position + new Vector3(0f, radius, 0f), Vector3.down);
            var hits = Physics.SphereCastNonAlloc(ray, radius, k_CachedHit, k_GroundRaycastDistance, m_GroundLayerMask);

            if (hits > 0)
            {
                if (hits > 1)
                {
                    // sort hits by distance
                    Array.Sort(k_CachedHit, 0, hits, m_RaycastHitComparer);
                }

                if (k_CachedHit[0].point == Vector3.zero)
                {
                    // If the start point and target overlap, the hit point is Vector3.zero even if SphereCastNonAlloc hits. is this a bug?

                    groundPosition = position;
                }
                else
                {
                    groundPosition = k_CachedHit[0].point;
                }
            }

            return groundPosition;
        }

        public Vector3 GetBlockedPosition(Vector3 origin, Vector3 destination, float radius)
        {
            Vector3 blockedPosition = Vector3.zero;
            Vector3 delta = destination - origin;
            var ray = new Ray(origin + new Vector3(0f, radius, 0f), delta.normalized);
            var hits = Physics.SphereCastNonAlloc(ray, radius, k_CachedHit, delta.magnitude, m_GroundLayerMask);

            if (hits > 0)
            {
                if (hits > 1)
                {
                    // sort hits by distance
                    Array.Sort(k_CachedHit, 0, hits, m_RaycastHitComparer);
                }

                if (k_CachedHit[0].point == Vector3.zero)
                {
                    // If the start point and target overlap, the hit point is Vector3.zero even if SphereCastNonAlloc hits. is this a bug?

                    blockedPosition = origin + new Vector3(0f, radius, 0f);
                }
                else
                {
                    blockedPosition = k_CachedHit[0].point - delta.normalized * radius - new Vector3(0f, radius, 0f);
                }
            }

            return blockedPosition;
        }
    }
}
