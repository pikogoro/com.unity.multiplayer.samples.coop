using System;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Gameplay.Configuration;
using Unity.BossRoom.Gameplay.GameplayObjects;
using Unity.BossRoom.Gameplay.GameplayObjects.Character;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
#if P56
//using Unity.Multiplayer.Samples.BossRoom.Visual;
using Unity.BossRoom.Gameplay.UI;
using Unity.BossRoom.CameraUtils;
using Unity.BossRoom.Utils;
#endif  // P56
using Action = Unity.BossRoom.Gameplay.Actions.Action;

namespace Unity.BossRoom.Gameplay.UserInput
{
    /// <summary>
    /// Captures inputs for a character on a client and sends them to the server.
    /// </summary>
    [RequireComponent(typeof(ServerCharacter))]
    public class ClientInputSender : NetworkBehaviour
    {
        const float k_MouseInputRaycastDistance = 100f;

        //The movement input rate is capped at 40ms (or 25 fps). This provides a nice balance between responsiveness and
        //upstream network conservation. This matters when holding down your mouse button to move.
        const float k_MoveSendRateSeconds = 0.04f; //25 fps.

        const float k_TargetMoveTimeout = 0.45f;  //prevent moves for this long after targeting someone (helps prevent walking to the guy you clicked).

        float m_LastSentMove;

        // Cache raycast hit array so that we can use non alloc raycasts
        readonly RaycastHit[] k_CachedHit = new RaycastHit[4];

        // This is basically a constant but layer masks cannot be created in the constructor, that's why it's assigned int Awake.
        LayerMask m_GroundLayerMask;

        LayerMask m_ActionLayerMask;

        const float k_MaxNavMeshDistance = 1f;

        RaycastHitComparer m_RaycastHitComparer;

        ServerCharacter m_ServerCharacter;

        /// <summary>
        /// This event fires at the time when an action request is sent to the server.
        /// </summary>
        public event Action<ActionRequestData> ActionInputEvent;

        /// <summary>
        /// This describes how a skill was requested. Skills requested via mouse click will do raycasts to determine their target; skills requested
        /// in other matters will use the stateful target stored in NetworkCharacterState.
        /// </summary>
        public enum SkillTriggerStyle
        {
            None,        //no skill was triggered.
            MouseClick,  //skill was triggered via mouse-click implying you should do a raycast from the mouse position to find a target.
            Keyboard,    //skill was triggered via a Keyboard press, implying target should be taken from the active target.
            KeyboardRelease, //represents a released key.
            UI,          //skill was triggered from the UI, and similar to Keyboard, target should be inferred from the active target.
            UIRelease,   //represents letting go of the mouse-button on a UI button
        }

        bool IsReleaseStyle(SkillTriggerStyle style)
        {
            return style == SkillTriggerStyle.KeyboardRelease || style == SkillTriggerStyle.UIRelease;
        }

        /// <summary>
        /// This struct essentially relays the call params of RequestAction to FixedUpdate. Recall that we may need to do raycasts
        /// as part of doing the action, and raycasts done outside of FixedUpdate can give inconsistent results (otherwise we would
        /// just expose PerformAction as a public method, and let it be called in whatever scoped it liked.
        /// </summary>
        /// <remarks>
        /// Reference: https://answers.unity.com/questions/1141633/why-does-fixedupdate-work-when-update-doesnt.html
        /// </remarks>
        struct ActionRequest
        {
            public SkillTriggerStyle TriggerStyle;
            public ActionID RequestedActionID;
            public ulong TargetId;
        }

        /// <summary>
        /// List of ActionRequests that have been received since the last FixedUpdate ran. This is a static array, to avoid allocs, and
        /// because we don't really want to let this list grow indefinitely.
        /// </summary>
        readonly ActionRequest[] m_ActionRequests = new ActionRequest[5];

        /// <summary>
        /// Number of ActionRequests that have been queued since the last FixedUpdate.
        /// </summary>
        int m_ActionRequestCount;

        BaseActionInput m_CurrentSkillInput;

        bool m_MoveRequest;

        Camera m_MainCamera;

        public event Action<Vector3> ClientMoveEvent;

        /// <summary>
        /// Convenience getter that returns our CharacterData
        /// </summary>
        CharacterClass CharacterClass => m_ServerCharacter.CharacterClass;

        [SerializeField]
        PhysicsWrapper m_PhysicsWrapper;

#if P56
        Joystick m_Joystick;
        bool m_IsMouseDown = false;
        Vector3 m_MouseDownPosition = Vector3.zero;
        CameraController m_CameraController;
        float m_LastPitch = 0f;
        ActionMovement m_LastActionMovement;
#if UNITY_ANDROID
        int m_TouchFingerId = -1;
#endif
        float m_LastRotationY = 0f;

        public float LastRotationY {
            set { m_LastRotationY = value; }
        }

        NetworkStats m_NetworkStats = null;
#if OVR
        bool m_IsMoving = false;
        float m_BaseRotationY = 0f;
        Transform m_LHandTransform = null;
        Transform m_RHandTransform = null;
        bool m_Rotated = false;
#endif  // OVR
#endif  // P56

        public override void OnNetworkSpawn()
        {
            if (!IsClient || !IsOwner)
            {
                enabled = false;
                // dont need to do anything else if not the owner
                return;
            }

            m_GroundLayerMask = LayerMask.GetMask(new[] { "Ground" });
            m_ActionLayerMask = LayerMask.GetMask(new[] { "PCs", "NPCs", "Ground" });

            m_RaycastHitComparer = new RaycastHitComparer();
        }

        void Awake()
        {
            m_ServerCharacter = GetComponent<ServerCharacter>();
            m_MainCamera = Camera.main;
        }

#if P56
        void Start()
        {
            m_CameraController = GetComponentInChildren<CameraController>();
            m_CameraController.BoneHead = GetComponentInChildren<CharacterSwap>().BoneHead;

            GameObject joystick = GameObject.Find("Joystick");
#if UNITY_STANDALONE || OVR
            // Disable the joystick if standalone or OVR.
            joystick.SetActive(false);
#endif
            m_Joystick = joystick.GetComponent<Joystick>();

            // NetworkStats is used to get RTT.
            m_NetworkStats = GetComponent<NetworkStats>();

            // Save current rotation angle y for initial value.
            m_LastRotationY = transform.rotation.eulerAngles.y;

#if OVR
            m_LHandTransform = GameObject.Find("LeftHandAnchor").transform;
            m_RHandTransform = GameObject.Find("RightHandAnchor").transform;
            m_BaseRotationY = transform.rotation.eulerAngles.y;
            m_CameraController.BaseRotationY = m_BaseRotationY;
#endif  // OVR
        }
#endif  // P56

        void FinishSkill()
        {
            m_CurrentSkillInput = null;
        }

        void SendInput(ActionRequestData action)
        {
            ActionInputEvent?.Invoke(action);
            m_ServerCharacter.RecvDoActionServerRPC(action);
        }

        void FixedUpdate()
        {
            //play all ActionRequests, in FIFO order.
            for (int i = 0; i < m_ActionRequestCount; ++i)
            {
                if (m_CurrentSkillInput != null)
                {
                    //actions requested while input is active are discarded, except for "Release" requests, which go through.
                    if (IsReleaseStyle(m_ActionRequests[i].TriggerStyle))
                    {
                        m_CurrentSkillInput.OnReleaseKey();
                    }
                }
                else if (!IsReleaseStyle(m_ActionRequests[i].TriggerStyle))
                {
                    var actionPrototype = GameDataSource.Instance.GetActionPrototypeByID(m_ActionRequests[i].RequestedActionID);
                    if (actionPrototype.Config.ActionInput != null)
                    {
                        var skillPlayer = Instantiate(actionPrototype.Config.ActionInput);
                        skillPlayer.Initiate(m_ServerCharacter, m_PhysicsWrapper.Transform.position, actionPrototype.ActionID, SendInput, FinishSkill);
                        m_CurrentSkillInput = skillPlayer;
                    }
                    else
                    {
                        PerformSkill(actionPrototype.ActionID, m_ActionRequests[i].TriggerStyle, m_ActionRequests[i].TargetId);
                    }
                }
            }

            m_ActionRequestCount = 0;

#if !P56
            if (EventSystem.current.currentSelectedGameObject != null)
            {
                return;
            }

            if (m_MoveRequest)
            {
                m_MoveRequest = false;

                if ((Time.time - m_LastSentMove) > k_MoveSendRateSeconds)
                {
                    m_LastSentMove = Time.time;

                    var ray = m_MainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);

                    var groundHits = Physics.RaycastNonAlloc(ray,
                        k_CachedHit,
                        k_MouseInputRaycastDistance,
                        m_GroundLayerMask);

                    if (groundHits > 0)
                    {
                        if (groundHits > 1)
                        {
                            // sort hits by distance
                            Array.Sort(k_CachedHit, 0, groundHits, m_RaycastHitComparer);
                        }

                        // verify point is indeed on navmesh surface
                        if (NavMesh.SamplePosition(k_CachedHit[0].point,
                                out var hit,
                                k_MaxNavMeshDistance,
                                NavMesh.AllAreas))
                        {
                            m_ServerCharacter.SendCharacterInputServerRpc(hit.position);

                            //Send our client only click request
                            ClientMoveEvent?.Invoke(hit.position);
                        }
                    }
                }
            }
#else   // !P56
            if (m_MoveRequest)
            {
                if ((Time.time - m_LastSentMove) > k_MoveSendRateSeconds)
                {
                    m_LastSentMove = Time.time;

                    ActionMovement movement = new ActionMovement();
                    Vector3 estimatedPosition;
                    if (m_Joystick.Vertical == 0f && m_Joystick.Horizontal == 0f)
                    {
                        movement.Position = ActionMovement.PositionNull;
                        estimatedPosition = transform.position;
#if OVR
                        m_IsMoving = false;
#endif  // OVR
                    }
                    else
                    {
                        float e = 1f;
                        if (!IsHost)
                        {
                            e = m_NetworkStats.RTT / Time.fixedDeltaTime;
                        }
                        movement.Position = transform.position + transform.forward * m_Joystick.Vertical * e + transform.right * m_Joystick.Horizontal * e;
                        estimatedPosition = movement.Position;
#if OVR
                        m_IsMoving = true;
#endif  // OVR
                    }

                    // Change direction of character's facing during mouse dragging.
#if !OVR
                    if (m_IsMouseDown)
#else   // !OVR
                    // Rotation by right controller stick.
                    //if (OVRInput.GetDown(OVRInput.RawButton.RThumbstickRight) || OVRInput.GetDown(OVRInput.RawButton.RThumbstickLeft))
                    if (Math.Abs(OVRInput.Get(OVRInput.RawAxis2D.RThumbstick).x) > 0.5f)
                    {
                        m_IsMoving = true;
                    }

                    if (m_IsMouseDown || m_IsMoving)
#endif  // !OVR
                    {
#if !OVR
#if UNITY_STANDALONE
                        Vector2 position = Input.mousePosition;
#elif UNITY_ANDROID

                        Vector2 position = Vector2.zero;
                        for (int i = 0; i < Input.touchCount; i++)
                        {
                            Touch touch = Input.GetTouch(i);
                            if (m_TouchFingerId == touch.fingerId)
                            {
                                position = touch.position;
                                break;
                            }
                        }
#endif
                        float yaw = (position.x - m_MouseDownPosition.x) / 60f;
                        float pitch = (position.y - m_MouseDownPosition.y) / 60f + m_LastPitch;

                        if (Math.Abs(yaw) > 30f)
                        {
                            yaw = (yaw > 0) ? 30f : -30f;
                        }
                        if (Math.Abs(pitch) > 30f)
                        {
                            pitch = (pitch > 0) ? 30f : -30f;
                        }
                        movement.Rotation = Quaternion.Euler(pitch, m_LastRotationY + yaw, 0f);
                        m_LastPitch = pitch;

                        // Save current rotaion y as last rotation y;
                        m_LastRotationY = movement.Rotation.eulerAngles.y;
#else   // !OVR
                        // Rotation by right controller stick.
                        float deltaRotationY = 0f;
                        if (!m_Rotated)
                        {
                            //if (OVRInput.GetDown(OVRInput.RawButton.RThumbstickRight))
                            if (OVRInput.Get(OVRInput.RawAxis2D.RThumbstick).x > 0.5f)
                            {
                                deltaRotationY = 30f;
                                m_Rotated = true;
                            }
                            //else if (OVRInput.GetDown(OVRInput.RawButton.RThumbstickLeft))
                            else if (OVRInput.Get(OVRInput.RawAxis2D.RThumbstick).x < -0.5f)
                            {
                                deltaRotationY = -30f;
                                m_Rotated = true;
                            }
                        }

                        if (deltaRotationY != 0f)
                        {
                            m_BaseRotationY += deltaRotationY;
                            m_CameraController.BaseRotationY = m_BaseRotationY;
                        }

                        // Rotation by left controller direction.
                        float yaw = m_LHandTransform.rotation.eulerAngles.y;
                        movement.Rotation = Quaternion.Euler(0f, yaw, 0f);
#endif  // !OVR

                    }
                    // Stop character's moving and rotation if no any input.
#if !OVR
                    else if (ActionMovement.IsNull(movement.Position) && !m_IsMouseDown)
#else   // !OVR
                    else if (ActionMovement.IsNull(movement.Position) && !m_IsMouseDown && !m_IsMoving)
#endif  // !OVR
                    {
                        movement.Rotation = ActionMovement.RotationNull;
                        m_MoveRequest = false;
                    }
                    // Anything else, not change direction of character's facing.
                    else
                    {
                        movement.Rotation = ActionMovement.RotationNull;
                    }

                    m_LastActionMovement = movement;

                    // verify point is indeed on navmesh surface
                    if (NavMesh.SamplePosition(estimatedPosition,
                            out var hit,
                            k_MaxNavMeshDistance,
                            NavMesh.AllAreas))
                    {
                        if (!ActionMovement.IsNull(movement.Position))
                        {
                            movement.Position = hit.position;
                        }
                        m_ServerCharacter.SendCharacterInputServerRpc(movement);

                        //Send our client only click request
                        ClientMoveEvent?.Invoke(hit.position);

#if !OVR
                        // Update character's pitch during mouse down.
                        if (m_IsMouseDown)
                        {
                            m_CameraController.RotationX = m_LastPitch;
                        }
#endif  // !OVR
                    }
                }
            }
#endif  // !P56
                    }

#if P56
#if UNITY_EDITOR || DEVELOPMENT_BUILD || OVR
        void OnGUI()
        {
            string text =
                    "Position: " + m_LastActionMovement.Position.ToString() + "\n" +
                    "Direction: " + m_LastActionMovement.Rotation.eulerAngles.ToString();
            DebugLogText.Log(text);
        }
#endif
#endif  // P56

        /// <summary>
        /// Perform a skill in response to some input trigger. This is the common method to which all input-driven skill plays funnel.
        /// </summary>
        /// <param name="actionID">The action you want to play. Note that "Skill1" may be overriden contextually depending on the target.</param>
        /// <param name="triggerStyle">What sort of input triggered this skill?</param>
        /// <param name="targetId">(optional) Pass in a specific networkID to target for this action</param>
        void PerformSkill(ActionID actionID, SkillTriggerStyle triggerStyle, ulong targetId = 0)
        {
            Transform hitTransform = null;

            if (targetId != 0)
            {
                // if a targetId is given, try to find the object
                NetworkObject targetNetObj;
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out targetNetObj))
                {
                    hitTransform = targetNetObj.transform;
                }
            }
            else
            {
                // otherwise try to find an object under the input position
                int numHits = 0;
                if (triggerStyle == SkillTriggerStyle.MouseClick)
                {
#if !P56 || !OVR
                    var ray = m_MainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
#else   // !P56 || !OVR
                    var ray = new Ray(m_RHandTransform.position, m_RHandTransform.forward);
#endif  // !P56 || !OVR
                    numHits = Physics.RaycastNonAlloc(ray, k_CachedHit, k_MouseInputRaycastDistance, m_ActionLayerMask);
                }

                int networkedHitIndex = -1;
#if P56
                // Choose the closest object. 
                if (numHits > 1)
                {
                    // sort hits by distance
                    Array.Sort(k_CachedHit, 0, numHits, m_RaycastHitComparer);
                }
#endif  // P56
                for (int i = 0; i < numHits; i++)
                {
                    if (k_CachedHit[i].transform.GetComponentInParent<NetworkObject>())
                    {
                        networkedHitIndex = i;
                        break;
                    }
                }

                hitTransform = networkedHitIndex >= 0 ? k_CachedHit[networkedHitIndex].transform : null;
            }

            if (GetActionRequestForTarget(hitTransform, actionID, triggerStyle, out ActionRequestData playerAction))
            {
                //Don't trigger our move logic for a while. This protects us from moving just because we clicked on them to target them.
                m_LastSentMove = Time.time + k_TargetMoveTimeout;

                SendInput(playerAction);
            }
            else if (!GameDataSource.Instance.GetActionPrototypeByID(actionID).IsGeneralTargetAction)
            {
                // clicked on nothing... perform an "untargeted" attack on the spot they clicked on.
                // (Different Actions will deal with this differently. For some, like archer arrows, this will fire an arrow
                // in the desired direction. For others, like mage's bolts, this will fire a "miss" projectile at the spot clicked on.)

                var data = new ActionRequestData();
#if !P56
                PopulateSkillRequest(k_CachedHit[0].point, actionID, ref data);
#else   // !P56
                // Set direction to the character's facing direction if target is nothing.
                PopulateSkillRequest(transform.position + transform.forward, actionID, ref data, false);
#endif  // !P56

                SendInput(data);
            }
        }

        /// <summary>
        /// When you right-click on something you will want to do contextually different things. For example you might attack an enemy,
        /// but revive a friend. You might also decide to do nothing (e.g. right-clicking on a friend who hasn't FAINTED).
        /// </summary>
        /// <param name="hit">The Transform of the entity we clicked on, or null if none.</param>
        /// <param name="actionID">The Action to build for</param>
        /// <param name="triggerStyle">How did this skill play get triggered? Mouse, Keyboard, UI etc.</param>
        /// <param name="resultData">Out parameter that will be filled with the resulting action, if any.</param>
        /// <returns>true if we should play an action, false otherwise. </returns>
        bool GetActionRequestForTarget(Transform hit, ActionID actionID, SkillTriggerStyle triggerStyle, out ActionRequestData resultData)
        {
            resultData = new ActionRequestData();

            var targetNetObj = hit != null ? hit.GetComponentInParent<NetworkObject>() : null;

            //if we can't get our target from the submitted hit transform, get it from our stateful target in our ServerCharacter.
            if (!targetNetObj && !GameDataSource.Instance.GetActionPrototypeByID(actionID).IsGeneralTargetAction)
            {
                ulong targetId = m_ServerCharacter.TargetId.Value;
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out targetNetObj);
            }

            //sanity check that this is indeed a valid target.
            if (targetNetObj == null || !ActionUtils.IsValidTarget(targetNetObj.NetworkObjectId))
            {
                return false;
            }

            if (targetNetObj.TryGetComponent<ServerCharacter>(out var serverCharacter))
            {
                //Skill1 may be contextually overridden if it was generated from a mouse-click.
                if (actionID == CharacterClass.Skill1.ActionID && triggerStyle == SkillTriggerStyle.MouseClick)
                {
                    if (!serverCharacter.IsNpc && serverCharacter.LifeState == LifeState.Fainted)
                    {
                        //right-clicked on a downed ally--change the skill play to Revive.
                        actionID = GameDataSource.Instance.ReviveActionPrototype.ActionID;
                    }
                }
            }

            Vector3 targetHitPoint;
            if (PhysicsWrapper.TryGetPhysicsWrapper(targetNetObj.NetworkObjectId, out var movementContainer))
            {
                targetHitPoint = movementContainer.Transform.position;
            }
            else
            {
                targetHitPoint = targetNetObj.transform.position;
            }

            // record our target in case this action uses that info (non-targeted attacks will ignore this)
            resultData.ActionID = actionID;
            resultData.TargetIds = new ulong[] { targetNetObj.NetworkObjectId };
#if !P56
            PopulateSkillRequest(targetHitPoint, actionID, ref resultData);
#else   // !P56
            PopulateSkillRequest(targetHitPoint, actionID, ref resultData, true);
#endif  // !P56
            return true;
        }

        /// <summary>
        /// Populates the ActionRequestData with additional information. The TargetIds of the action should already be set before calling this.
        /// </summary>
        /// <param name="hitPoint">The point in world space where the click ray hit the target.</param>
        /// <param name="actionID">The action to perform (will be stamped on the resultData)</param>
        /// <param name="resultData">The ActionRequestData to be filled out with additional information.</param>
#if !P56
        void PopulateSkillRequest(Vector3 hitPoint, ActionID actionID, ref ActionRequestData resultData)
#else   // !P56
        void PopulateSkillRequest(Vector3 hitPoint, ActionID actionID, ref ActionRequestData resultData, bool existsTarget)
#endif  // !P56
        {
            resultData.ActionID = actionID;
            var actionConfig = GameDataSource.Instance.GetActionPrototypeByID(actionID).Config;

            //most skill types should implicitly close distance. The ones that don't are explicitly set to false in the following switch.
            resultData.ShouldClose = true;

            // figure out the Direction in case we want to send it
            Vector3 offset = hitPoint - m_PhysicsWrapper.Transform.position;
#if !P56
            offset.y = 0;
#endif  // !P56
            Vector3 direction = offset.normalized;

            switch (actionConfig.Logic)
            {
                //for projectile logic, infer the direction from the click position.
                case ActionLogic.LaunchProjectile:
#if !P56
                    resultData.Direction = direction;
#else   // !P56
                    if (existsTarget)
                    {
                        resultData.Direction = direction;
                    }
                    else
                    {
#if !OVR
                        resultData.Direction = m_MainCamera.transform.forward.normalized;
#else   // !OVR
                        resultData.Direction = m_RHandTransform.forward.normalized;
#endif  // !OVR
                    }
#endif  // !P56
                    resultData.ShouldClose = false; //why? Because you could be lining up a shot, hoping to hit other people between you and your target. Moving you would be quite invasive.
                    return;
                case ActionLogic.Melee:
                    resultData.Direction = direction;
                    return;
                case ActionLogic.Target:
                    resultData.ShouldClose = false;
                    return;
                case ActionLogic.Emote:
                    resultData.CancelMovement = true;
                    return;
                case ActionLogic.RangedFXTargeted:
                    resultData.Position = hitPoint;
                    return;
                case ActionLogic.DashAttack:
                    resultData.Position = hitPoint;
                    return;
                case ActionLogic.PickUp:
                    resultData.CancelMovement = true;
                    resultData.ShouldQueue = false;
                    return;
            }
        }

        /// <summary>
        /// Request an action be performed. This will occur on the next FixedUpdate.
        /// </summary>
        /// <param name="actionID"> The action you'd like to perform. </param>
        /// <param name="triggerStyle"> What input style triggered this action. </param>
        /// <param name="targetId"> NetworkObjectId of target. </param>
        public void RequestAction(Action action, SkillTriggerStyle triggerStyle, ulong targetId = 0)
        {
            if (action == null)
            {
                return;
            }

            Assert.IsNotNull(GameDataSource.Instance.GetActionPrototypeByID(action.ActionID),
                $"Action {action.name} must be contained in the Action prototypes of GameDataSource!");

            if (m_ActionRequestCount < m_ActionRequests.Length)
            {
                m_ActionRequests[m_ActionRequestCount].RequestedActionID = action.ActionID;
                m_ActionRequests[m_ActionRequestCount].TriggerStyle = triggerStyle;
                m_ActionRequests[m_ActionRequestCount].TargetId = targetId;
                m_ActionRequestCount++;
            }
        }

        void Update()
        {
#if P56 && !OVR
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
            {
                RequestAction(CharacterClass.Skill1, SkillTriggerStyle.Keyboard);
            }
            else if (UnityEngine.Input.GetKeyUp(KeyCode.Alpha1))
            {
                RequestAction(CharacterClass.Skill1, SkillTriggerStyle.KeyboardRelease);
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
            {
                RequestAction(CharacterClass.Skill2, SkillTriggerStyle.Keyboard);
            }
            else if (UnityEngine.Input.GetKeyUp(KeyCode.Alpha2))
            {
                RequestAction(CharacterClass.Skill2, SkillTriggerStyle.KeyboardRelease);
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3))
            {
                RequestAction(CharacterClass.Skill3, SkillTriggerStyle.Keyboard);
            }
            else if (UnityEngine.Input.GetKeyUp(KeyCode.Alpha3))
            {
                RequestAction(CharacterClass.Skill3, SkillTriggerStyle.KeyboardRelease);
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5))
            {
                RequestAction(GameDataSource.Instance.Emote1ActionPrototype, SkillTriggerStyle.Keyboard);
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha6))
            {
                RequestAction(GameDataSource.Instance.Emote2ActionPrototype, SkillTriggerStyle.Keyboard);
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha7))
            {
                RequestAction(GameDataSource.Instance.Emote3ActionPrototype, SkillTriggerStyle.Keyboard);
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha8))
            {
                RequestAction(GameDataSource.Instance.Emote4ActionPrototype, SkillTriggerStyle.Keyboard);
            }
#endif  // P56 && !OVR

#if !P56
            if (!EventSystem.current.IsPointerOverGameObject() && m_CurrentSkillInput == null)
            {
                //IsPointerOverGameObject() is a simple way to determine if the mouse is over a UI element. If it is, we don't perform mouse input logic,
                //to model the button "blocking" mouse clicks from falling through and interacting with the world.

                if (UnityEngine.Input.GetMouseButtonDown(1))
                {
                    RequestAction(CharacterClass.Skill1, SkillTriggerStyle.MouseClick);
                }

                if (UnityEngine.Input.GetMouseButtonDown(0))
                {
                    RequestAction(GameDataSource.Instance.GeneralTargetActionPrototype, SkillTriggerStyle.MouseClick);
                }
                else if (UnityEngine.Input.GetMouseButton(0))
                {
                    m_MoveRequest = true;
                }
            }
#else   // !P56
#if !OVR
            // Start moving by input from Joystick.
            if (m_Joystick.Vertical != 0f || m_Joystick.Horizontal != 0f)
#else   // !OVR
            // Start moving by input from Joystick or right controller stick.
            if (m_Joystick.Vertical != 0f || m_Joystick.Horizontal != 0f ||
                //OVRInput.GetDown(OVRInput.RawButton.RThumbstickRight) || OVRInput.GetDown(OVRInput.RawButton.RThumbstickLeft))
                Math.Abs(OVRInput.Get(OVRInput.RawAxis2D.RThumbstick).x) > 0.5f)
#endif  // !OVR
            {
                m_MoveRequest = true;
            }
#if OVR
            if (Math.Abs(OVRInput.Get(OVRInput.RawAxis2D.RThumbstick).x) < 0.5f)
            {
                m_Rotated = false;
            }
#endif  // OVR

            // Ignore mouse down (or touch) if the position is over UI game object.
#if UNITY_STANDALONE
            if (!EventSystem.current.IsPointerOverGameObject() && m_CurrentSkillInput == null)
            {
                // Start rotation of character's facing by mouse button down.
                if (UnityEngine.Input.GetMouseButtonDown(0))
                {
                    RequestAction(GameDataSource.Instance.GeneralTargetActionPrototype, SkillTriggerStyle.MouseClick);
                    m_MouseDownPosition = Input.mousePosition;
                    m_IsMouseDown = true;
                    m_MoveRequest = true;
                }
                // Stop rotation of character's facing by mouse bottun up.
                if (UnityEngine.Input.GetMouseButtonUp(0))
                {
                    m_MouseDownPosition = Vector3.zero;
                    m_IsMouseDown = false;
                }
            }
#elif UNITY_ANDROID
#if !OVR
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (!EventSystem.current.IsPointerOverGameObject(touch.fingerId) && m_CurrentSkillInput == null)
                {
                    // Start rotation of character's facing by mouse button down.
                    if (touch.phase == TouchPhase.Began)
                    {
                        RequestAction(ActionType.GeneralTarget, SkillTriggerStyle.MouseClick);
                        m_MouseDownPosition = touch.position;
                        m_IsMouseDown = true;
                        m_MoveRequest = true;
                        m_TouchFingerId = touch.fingerId;
                    }
                    // Stop rotation of character's facing by mouse bottun up.
                    if (touch.phase == TouchPhase.Ended)
                    {
                        m_MouseDownPosition = Vector3.zero;
                        m_IsMouseDown = false;
                        m_TouchFingerId = -1;
                    }
                }
            }
#else   // !OVR
            // Right index trigger
            if (OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger))
            {
                RequestAction(GameDataSource.Instance.GeneralTargetActionPrototype, SkillTriggerStyle.MouseClick);
                m_IsMouseDown = true;
            }
            else if (OVRInput.GetUp(OVRInput.RawButton.RIndexTrigger))
            {
                m_IsMouseDown = false;
            }
            // Right hand trigger
            if (OVRInput.GetDown(OVRInput.RawButton.RHandTrigger))
            {
                RequestAction(CharacterClass.Skill1, SkillTriggerStyle.Keyboard);
                m_IsMouseDown = true;
            }
            else if (OVRInput.GetUp(OVRInput.RawButton.RHandTrigger))
            {
                RequestAction(CharacterClass.Skill1, SkillTriggerStyle.KeyboardRelease);
                m_IsMouseDown = false;
            }
            // A button
            if (OVRInput.GetDown(OVRInput.RawButton.A))
            {
                RequestAction(CharacterClass.Skill2, SkillTriggerStyle.Keyboard);
                m_IsMouseDown = true;
            }
            else if (OVRInput.GetUp(OVRInput.RawButton.A))
            {
                RequestAction(CharacterClass.Skill2, SkillTriggerStyle.KeyboardRelease);
                m_IsMouseDown = false;
            }
            // B button
            if (OVRInput.GetDown(OVRInput.RawButton.B))
            {
                RequestAction(CharacterClass.Skill3, SkillTriggerStyle.Keyboard);
                m_IsMouseDown = true;
            }
            else if (OVRInput.GetUp(OVRInput.RawButton.B))
            {
                RequestAction(CharacterClass.Skill3, SkillTriggerStyle.KeyboardRelease);
                m_IsMouseDown = false;
            }
#endif  // !OVR
#endif  // UNITY_STANDALONE || UNITY_ANDROID

#if !OVR
            // Set current rotation y to the last rotation y during mouse is not down.
            if (!m_IsMouseDown)
            {
                m_LastRotationY = transform.rotation.eulerAngles.y;
            }
#endif  // !OVR
#endif  // !P56
        }
    }
}
