using System;
using System.Collections.Generic;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.BossRoom.Utils;
using Unity.BossRoom.VisualEffects;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects
{
    /// <summary>
    /// Logic that handles a physics-based projectile with a collider
    /// </summary>
    public class HomingProjectile : NetworkBehaviour
    {
        bool m_Started;

        [SerializeField]
        SphereCollider m_OurCollider;

        /// <summary>
        /// The character that created us. Can be 0 to signal that we were created generically by the server.
        /// </summary>
        ulong m_SpawnerId;

        /// <summary>
        /// The data for our projectile. Indicates speed, damage, etc.
        /// </summary>
        ProjectileInfo m_ProjectileInfo;

        const int k_MaxCollisions = 4;
        //const float k_WallLingerSec = 2f; //time in seconds that arrows linger after hitting a target.
        const float k_WallLingerSec = 0.5f; //time in seconds that arrows linger after hitting a target.
        const float k_EnemyLingerSec = 0.2f; //time after hitting an enemy that we persist.
        readonly RaycastHit[] k_CachedHit = new RaycastHit[4];
        RaycastHitComparer m_RaycastHitComparer;
        Vector3 m_HitPoint = Vector3.zero;

        /// <summary>
        /// Time when we should destroy this arrow, in Time.time seconds.
        /// </summary>
        float m_DestroyAtSec;

        int m_CollisionMask;  //mask containing everything we test for while moving
        int m_BlockerMask;    //physics mask for things that block the arrow's flight.
        int m_NpcLayer;

        /// <summary>
        /// List of everyone we've hit and dealt damage to.
        /// </summary>
        /// <remarks>
        /// Note that it's possible for entries in this list to become null if they're Destroyed post-impact.
        /// But that's fine by us! We use <c>m_HitTargets.Count</c> to tell us how many total enemies we've hit,
        /// so those nulls still count as hits.
        /// </remarks>
        List<GameObject> m_HitTargets = new List<GameObject>();

        /// <summary>
        /// Are we done moving?
        /// </summary>
        bool m_IsDead;

        [SerializeField]
        [Tooltip("Explosion prefab used when projectile hits enemy. This should have a fixed duration.")]
        SpecialFXGraphic m_OnHitParticlePrefab;

        [SerializeField]
        TrailRenderer m_TrailRenderer;

        [SerializeField]
        Transform m_Visualization;

        const float k_LerpTime = 0.1f;

        PositionLerper m_PositionLerper;

        Transform m_TargetTransform;    // Target to homing

        /// <summary>
        /// Set everything up based on provided projectile information.
        /// (Note that this is called before OnNetworkSpawn(), so don't try to do any network stuff here.)
        /// </summary>
        public void Initialize(ulong creatorsNetworkObjectId, in ProjectileInfo projectileInfo, Transform target)
        {
            m_SpawnerId = creatorsNetworkObjectId;
            m_ProjectileInfo = projectileInfo;
            m_TargetTransform = target;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                m_Started = true;

                m_HitTargets = new List<GameObject>();
                m_IsDead = false;

                m_DestroyAtSec = Time.fixedTime + (m_ProjectileInfo.Range / m_ProjectileInfo.Speed_m_s);

                //m_CollisionMask = LayerMask.GetMask(new[] { "NPCs", "Default", "Environment" });
                m_CollisionMask = LayerMask.GetMask(new[] { "NPCs", "Ground", "Environment" });
                //m_BlockerMask = LayerMask.GetMask(new[] { "Default", "Environment" });
                m_BlockerMask = LayerMask.GetMask(new[] { "Ground", "Environment" });
                m_NpcLayer = LayerMask.NameToLayer("NPCs");

                m_RaycastHitComparer = new RaycastHitComparer();
            }

            if (IsClient)
            {
                m_TrailRenderer.Clear();

                m_Visualization.parent = null;

                m_PositionLerper = new PositionLerper(transform.position, k_LerpTime);
                m_Visualization.transform.rotation = transform.rotation;
            }

        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                m_Started = false;
            }


            if (IsClient)
            {
                m_TrailRenderer.Clear();
                m_Visualization.parent = transform;
            }
        }

        void FixedUpdate()
        {
            if (!m_Started || !IsServer)
            {
                return; //don't do anything before OnNetworkSpawn has run.
            }

            if (m_DestroyAtSec < Time.fixedTime)
            {
                // Time to return to the pool from whence it came.
                var networkObject = gameObject.GetComponent<NetworkObject>();
                networkObject.Despawn();
                return;
            }

            // Homing
            transform.LookAt(m_TargetTransform.position + new Vector3(0f, 1.5f, 0f));


            if (!m_IsDead)
            {
                DetectCollisions();
            }

            if (m_IsDead && m_HitPoint != Vector3.zero)
            {
                transform.position = m_HitPoint;
            }
            else
            {
                // Update projectile position after collision check.
                var displacement = transform.forward * (m_ProjectileInfo.Speed_m_s * Time.fixedDeltaTime);
                transform.position += displacement;
            }
        }

        void Update()
        {
            if (IsClient)
            {
                // One thing to note: this graphics GameObject is detached from its parent on OnNetworkSpawn. On the host,
                // the m_Parent Transform is translated via PhysicsProjectile's FixedUpdate method. On all other
                // clients, m_Parent's NetworkTransform handles syncing and interpolating the m_Parent Transform. Thus, to
                // eliminate any visual jitter on the host, this GameObject is positionally smoothed over time. On all other
                // clients, no positional smoothing is required, since m_Parent's NetworkTransform will perform
                // positional interpolation on its Update method, and so this position is simply matched 1:1 with m_Parent.

                if (IsHost)
                {
                    m_Visualization.position = m_PositionLerper.LerpPosition(m_Visualization.position,
                        transform.position);
                }
                else
                {
                    m_Visualization.position = transform.position;
                }
            }

        }

        void DetectCollisions()
        {
            // Change collision check to use sphere cast because projectile pass target if it too fast.

            float distance = m_ProjectileInfo.Speed_m_s * Time.fixedDeltaTime;
            var numHits = Physics.SphereCastNonAlloc(transform.position, m_OurCollider.radius, transform.forward, k_CachedHit, distance, m_CollisionMask);
            if (numHits > 1)
            {
                // sort hits by distance
                Array.Sort(k_CachedHit, 0, numHits, m_RaycastHitComparer);
            }

            for (int i = 0; i < numHits; i++)
            {
                int layerTest = 1 << k_CachedHit[i].collider.gameObject.layer;
                if ((layerTest & m_BlockerMask) != 0)
                {
                    //hit a wall; leave it for a couple of seconds.
                    m_ProjectileInfo.Speed_m_s = 0f;
                    m_IsDead = true;
                    m_DestroyAtSec = Time.fixedTime + k_WallLingerSec;
                    m_HitPoint = k_CachedHit[i].point;
                    return;
                }

                if (k_CachedHit[i].collider.gameObject.layer == m_NpcLayer && !m_HitTargets.Contains(k_CachedHit[i].collider.gameObject))
                {
                    m_HitTargets.Add(k_CachedHit[i].collider.gameObject);

                    if (m_HitTargets.Count >= m_ProjectileInfo.MaxVictims)
                    {
                        // we've hit all the enemies we're allowed to! So we're done
                        m_DestroyAtSec = Time.fixedTime + k_EnemyLingerSec;
                        m_IsDead = true;
                    }

                    //all NPC layer entities should have one of these.
                    var targetNetObj = k_CachedHit[i].transform.GetComponentInParent<NetworkObject>();
                    if (targetNetObj)
                    {
                        RecvHitEnemyClientRPC(targetNetObj.NetworkObjectId);

                        //retrieve the person that created us, if he's still around.
                        NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(m_SpawnerId, out var spawnerNet);
                        var spawnerObj = spawnerNet != null ? spawnerNet.GetComponent<ServerCharacter>() : null;

                        if (k_CachedHit[i].transform.TryGetComponent(out IDamageable damageable))
                        {
                            damageable.ReceiveHP(spawnerObj, -m_ProjectileInfo.Damage);

                            // Knockback
                            ServerCharacter clientCharacter = k_CachedHit[i].transform.GetComponent<ServerCharacter>();
                            if (clientCharacter != null)
                            {
                                clientCharacter.Movement.StartKnockback(k_CachedHit[i].point, 2.1f, 0.4f);
                            }
                        }
                    }

                    if (m_IsDead)
                    {
                        return; // don't keep examining collisions since we can't damage anybody else
                    }
                }
            }
        }

        [ClientRpc]
        private void RecvHitEnemyClientRPC(ulong enemyId)
        {
            //in the future we could do quite fancy things, like deparenting the Graphics Arrow and parenting it to the target.
            //For the moment we play some particles (optionally), and cause the target to animate a hit-react.

            NetworkObject targetNetObject;
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyId, out targetNetObject))
            {
                if (m_OnHitParticlePrefab)
                {
                    // show an impact graphic
                    Instantiate(m_OnHitParticlePrefab.gameObject, transform.position, transform.rotation);
                }
            }
        }
    }
}
