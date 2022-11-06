using Unity.BossRoom.Gameplay.GameplayObjects;
using UnityEngine;
using UnityEngine.AI;
#if P56
using System;
#endif  // P56

namespace Unity.BossRoom.Gameplay.Actions
{
    /// <summary>
    /// This class is the first step in AoE ability. It will update the initial input visuals' position and will be in charge
    /// of tracking the user inputs. Once the ability
    /// is confirmed and the mouse is clicked, it'll send the appropriate RPC to the server, triggering the AoE serer side gameplay logic.
    /// The server side gameplay action will then trigger the client side resulting FX.
    /// This action's flow is this: (Client) AoEActionInput --> (Server) AoEAction --> (Client) AoEActionFX
    /// </summary>
    public class AoeActionInput : BaseActionInput
    {
        [SerializeField]
        GameObject m_InRangeVisualization;

        [SerializeField]
        GameObject m_OutOfRangeVisualization;

        Camera m_Camera;

        //The general action system works on MouseDown events (to support Charged Actions), but that means that if we only wait for
        //a mouse up event internally, we will fire as part of the same UI click that started the action input (meaning the user would
        //have to drag her mouse from the button to the firing location). Tracking a mouse-down mouse-up cycle means that a user can
        //click on the NavMesh separately from the mouse-click that engaged the action (which also makes the UI flow equivalent to the
        //flow from hitting a number key).
        bool m_ReceivedMouseDownEvent;

        NavMeshHit m_NavMeshHit;

        // plane that has normal pointing up on y, that is 0 distance units displaced from origin
        // this is also the same height as the NavMesh baked in-game
        static readonly Plane k_Plane = new Plane(Vector3.up, 0f);

#if P56
        const float k_GroundRaycastDistance = 100f;
        readonly RaycastHit[] k_CachedHit = new RaycastHit[4];
        LayerMask m_GroundLayerMask;
        RaycastHitComparer m_RaycastHitComparer = null;
#endif  // P56
#if P56 && OVR
        Transform m_RHandTransform = null;
#endif  // P56 && OVR

#if P56
        /// <summary>
        /// Get ground position by using raycast.
        /// </summary>
        private Vector3 GetGroundPosition(Ray ray)
        {
            Vector3 groundPosition = Vector3.zero;
            var groundHits = Physics.RaycastNonAlloc(ray, k_CachedHit, k_GroundRaycastDistance, m_GroundLayerMask);
            if (groundHits > 0)
            {
                if (groundHits > 1)
                {
                    // sort hits by distance
                    Array.Sort(k_CachedHit, 0, groundHits, m_RaycastHitComparer);
                }

                groundPosition = k_CachedHit[0].point;
            }

            return groundPosition;
        }
#endif  // P56

        void Start()
        {
            var radius = GameDataSource.Instance.GetActionPrototypeByID(m_ActionPrototypeID).Config.Radius;

            transform.localScale = new Vector3(radius * 2, radius * 2, radius * 2);
            m_Camera = Camera.main;
#if P56
            m_RaycastHitComparer = new RaycastHitComparer();
            m_GroundLayerMask = LayerMask.GetMask(new[] { "Ground" });
#endif  // P56
#if P56 && OVR
            m_RHandTransform = GameObject.Find("RightHandAnchor").transform;
#endif  // P56 && OVR
        }

        void Update()
        {
#if !P56
            if (PlaneRaycast(k_Plane, m_Camera.ScreenPointToRay(Input.mousePosition), out Vector3 pointOnPlane) &&
                NavMesh.SamplePosition(pointOnPlane, out m_NavMeshHit, 2f, NavMesh.AllAreas))
#else   // !P56
#if !OVR
            var ray = new Ray(m_Camera.transform.position, m_Camera.transform.forward);
            var groundPosition = GetGroundPosition(ray);
            if (groundPosition != Vector3.zero && NavMesh.SamplePosition(groundPosition, out m_NavMeshHit, 2f, NavMesh.AllAreas))
#else   // !OVR
            var ray = new Ray(m_RHandTransform.position, m_RHandTransform.forward);
            if (PlaneRaycast(k_Plane, ray, out Vector3 pointOnPlane) &&
                NavMesh.SamplePosition(pointOnPlane, out m_NavMeshHit, 2f, NavMesh.AllAreas))
#endif  // !OVR
#endif  // !P56
            {
                    transform.position = m_NavMeshHit.position;
            }

            float range = GameDataSource.Instance.GetActionPrototypeByID(m_ActionPrototypeID).Config.Range;
            bool isInRange = (m_Origin - transform.position).sqrMagnitude <= range * range;
            m_InRangeVisualization.SetActive(isInRange);
            m_OutOfRangeVisualization.SetActive(!isInRange);

            // wait for the player to click down and then release the mouse button before actually taking the input
#if !P56 || !OVR
            if (Input.GetMouseButtonDown(0))
#else   // !P56 || !OVR
            if (OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger))
#endif  // !P56 || !OVR
            {
                m_ReceivedMouseDownEvent = true;
            }

#if !P56 || !OVR
            if (Input.GetMouseButtonUp(0) && m_ReceivedMouseDownEvent)
#else   // !P56 || !OVR
            if (OVRInput.GetUp(OVRInput.RawButton.RIndexTrigger) && m_ReceivedMouseDownEvent)
#endif  // !P56 || !OVR
            {
                if (isInRange)
                {
                    var data = new ActionRequestData
                    {
                        Position = transform.position,
                        ActionID = m_ActionPrototypeID,
                        ShouldQueue = false,
                        TargetIds = null
                    };
                    m_SendInput(data);
                }
                Destroy(gameObject);
                return;
            }
        }

        /// <summary>
        /// Utility method to simulate a raycast to a given plane. Does not involve a Physics-based raycast.
        /// </summary>
        /// <remarks> Based on documented example here: https://docs.unity3d.com/ScriptReference/Plane.Raycast.html
        /// </remarks>
        /// <param name="plane"></param>
        /// <param name="ray"></param>
        /// <param name="pointOnPlane"></param>
        /// <returns> true if intersection point lies inside NavMesh; false otherwise </returns>
        static bool PlaneRaycast(Plane plane, Ray ray, out Vector3 pointOnPlane)
        {
            // validate that this ray intersects plane
            if (plane.Raycast(ray, out var enter))
            {
                // get the point of intersection
                pointOnPlane = ray.GetPoint(enter);
                return true;
            }
            else
            {
                pointOnPlane = Vector3.zero;
                return false;
            }
        }
    }
}
