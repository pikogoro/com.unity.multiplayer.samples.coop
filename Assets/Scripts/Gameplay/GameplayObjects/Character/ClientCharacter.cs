using System;
using Unity.BossRoom.CameraUtils;
using Unity.BossRoom.Gameplay.UserInput;
using Unity.BossRoom.Gameplay.Configuration;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Utils;
using Unity.Netcode;
using UnityEngine;
#if P56
using System.Collections.Generic;
#endif  // P56

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    /// <summary>
    /// <see cref="ClientCharacter"/> is responsible for displaying a character on the client's screen based on state information sent by the server.
    /// </summary>
    public class ClientCharacter : NetworkBehaviour
    {
        [SerializeField]
        Animator m_ClientVisualsAnimator;

        [SerializeField]
        VisualizationConfiguration m_VisualizationConfiguration;

        /// <summary>
        /// Returns a reference to the active Animator for this visualization
        /// </summary>
        public Animator OurAnimator => m_ClientVisualsAnimator;

        /// <summary>
        /// Returns the targeting-reticule prefab for this character visualization
        /// </summary>
        public GameObject TargetReticulePrefab => m_VisualizationConfiguration.TargetReticule;

        /// <summary>
        /// Returns the Material to plug into the reticule when the selected entity is hostile
        /// </summary>
        public Material ReticuleHostileMat => m_VisualizationConfiguration.ReticuleHostileMat;

        /// <summary>
        /// Returns the Material to plug into the reticule when the selected entity is friendly
        /// </summary>
        public Material ReticuleFriendlyMat => m_VisualizationConfiguration.ReticuleFriendlyMat;

        CharacterSwap m_CharacterSwapper;

        public CharacterSwap CharacterSwap => m_CharacterSwapper;

        public bool CanPerformActions => m_ServerCharacter.CanPerformActions;

        ServerCharacter m_ServerCharacter;

        public ServerCharacter serverCharacter => m_ServerCharacter;

        ClientActionPlayer m_ClientActionViz;

        PositionLerper m_PositionLerper;

        RotationLerper m_RotationLerper;

        // this value suffices for both positional and rotational interpolations; one may have a constant value for each
        const float k_LerpTime = 0.08f;

        Vector3 m_LerpedPosition;

        Quaternion m_LerpedRotation;

        float m_CurrentSpeed;

#if P56
        Vector3 m_CurrentMovementDirection;

        float m_RotationX = 0f;
        public float RotationX
        {
            set { m_RotationX = value; }
        }

        bool m_IsDefending = false;
        bool m_IsCrouching = false;

        CharacterGearManager m_GearManager;
        ClientCharacterIKManager m_IKManager;

        Transform m_Muzzle = null;
        public Vector3 MuzzlePosition
        {
            get { return m_Muzzle.position; }  // world position
        }
        public Vector3 MuzzleLocalPosition
        {
            get { return transform.worldToLocalMatrix.MultiplyPoint(m_Muzzle.position); } // world position -> local position
        }

        // For Aiming
        const float k_AimingRaycastDistance = 1000f;
        readonly RaycastHit[] k_CachedHit = new RaycastHit[4];
        LayerMask m_AimingLayerMask;
        LayerMask m_TargetLayerMask;
        RaycastHitComparer m_RaycastHitComparer;
        Transform m_ReticleTransform = null;

        // For view rotation lerp
        RotationLerper m_RotationXLerper;
        Quaternion m_LerpedXRotation;

        // For movement animation spped lerp
        PositionLerper m_MovAnimSpeedLerper;
        Vector3 m_LerpedMovAnimSpeed;
#endif  // P56

        /// <summary>
        /// /// Server to Client RPC that broadcasts this action play to all clients.
        /// </summary>
        /// <param name="data"> Data about which action to play and its associated details. </param>
        [ClientRpc]
        public void RecvDoActionClientRPC(ActionRequestData data)
        {
            ActionRequestData data1 = data;
            m_ClientActionViz.PlayAction(ref data1);
        }

        /// <summary>
        /// This RPC is invoked on the client when the active action FXs need to be cancelled (e.g. when the character has been stunned)
        /// </summary>
        [ClientRpc]
        public void RecvCancelAllActionsClientRpc()
        {
            m_ClientActionViz.CancelAllActions();
        }

        /// <summary>
        /// This RPC is invoked on the client when active action FXs of a certain type need to be cancelled (e.g. when the Stealth action ends)
        /// </summary>
        [ClientRpc]
        public void RecvCancelActionsByPrototypeIDClientRpc(ActionID actionPrototypeID)
        {
            m_ClientActionViz.CancelAllActionsWithSamePrototypeID(actionPrototypeID);
        }

        /// <summary>
        /// Called on all clients when this character has stopped "charging up" an attack.
        /// Provides a value between 0 and 1 inclusive which indicates how "charged up" the attack ended up being.
        /// </summary>
        [ClientRpc]
        public void RecvStopChargingUpClientRpc(float percentCharged)
        {
            m_ClientActionViz.OnStoppedChargingUp(percentCharged);
        }

        void Awake()
        {
            enabled = false;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsClient || transform.parent == null)
            {
                return;
            }

            enabled = true;

            m_ClientActionViz = new ClientActionPlayer(this);

            m_ServerCharacter = GetComponentInParent<ServerCharacter>();

            m_ServerCharacter.IsStealthy.OnValueChanged += OnStealthyChanged;
            m_ServerCharacter.MovementStatus.OnValueChanged += OnMovementStatusChanged;
            OnMovementStatusChanged(MovementStatus.Normal, m_ServerCharacter.MovementStatus.Value);

#if P56
            m_ServerCharacter.MovementDirection.OnValueChanged += OnMovementDirectionChanged;
            m_ServerCharacter.CurrentGear.OnValueChanged += OnCurrentGearChanged;
            m_ServerCharacter.IsDefending.OnValueChanged += OnDefenseStateChanged;
            m_ServerCharacter.IsCrouching.OnValueChanged += OnCrouchingStateChanged;
#endif  // P56

            // sync our visualization position & rotation to the most up to date version received from server
            transform.SetPositionAndRotation(serverCharacter.physicsWrapper.Transform.position,
                serverCharacter.physicsWrapper.Transform.rotation);
            m_LerpedPosition = transform.position;
            m_LerpedRotation = transform.rotation;

            // similarly, initialize start position and rotation for smooth lerping purposes
            m_PositionLerper = new PositionLerper(serverCharacter.physicsWrapper.Transform.position, k_LerpTime);
            m_RotationLerper = new RotationLerper(serverCharacter.physicsWrapper.Transform.rotation, k_LerpTime);

#if P56
            // Setup movement animation spped lerper
            m_MovAnimSpeedLerper = new PositionLerper(Vector3.zero, k_LerpTime);

            // Setup rotation X lerper
            m_RotationXLerper = new RotationLerper(Quaternion.Euler(m_RotationX, 0f, 0f), k_LerpTime);
#endif  // P56

            if (!m_ServerCharacter.IsNpc)
            {
                name = "AvatarGraphics" + m_ServerCharacter.OwnerClientId;

                if (m_ServerCharacter.TryGetComponent(out ClientAvatarGuidHandler clientAvatarGuidHandler))
                {
                    m_ClientVisualsAnimator = clientAvatarGuidHandler.graphicsAnimator;
                }

                m_CharacterSwapper = GetComponentInChildren<CharacterSwap>();

                // ...and visualize the current char-select value that we know about
                SetAppearanceSwap();

                if (m_ServerCharacter.IsOwner)
                {
                    ActionRequestData data = new ActionRequestData { ActionID = GameDataSource.Instance.GeneralTargetActionPrototype.ActionID };
                    m_ClientActionViz.PlayAction(ref data);
                    gameObject.AddComponent<CameraController>();

                    if (m_ServerCharacter.TryGetComponent(out ClientInputSender inputSender))
                    {
                        // anticipated actions will only be played on non-host, owning clients
#if !P56
                        if (!IsServer)
#else   // !P56
                        if (!IsServer || IsHost)    // It is make to be called on Client and Host.
#endif  // !P56
                        {
                            inputSender.ActionInputEvent += OnActionInput;
                        }
                        inputSender.ClientMoveEvent += OnMoveInput;
#if P56
                        inputSender.ClientCharacter = this;

                        // Aim
                        m_AimingLayerMask = LayerMask.GetMask(new[] { "PCs", "NPCs", "Environment", "Default", "Ground" });
                        //m_TargetLayerMask = LayerMask.GetMask(new[] { "PCs", "NPCs" });
                        m_TargetLayerMask = LayerMask.GetMask(new[] { "PCs", "NPCs", "Environment", "Default", "Ground" });
                        m_RaycastHitComparer = new RaycastHitComparer();
                        GameObject go = GameObject.Find("Reticle");
                        if (go != null)
                        {
                            m_ReticleTransform = go.GetComponent<RectTransform>();
                        }
#endif  // P56
                    }
                }

#if P56
                m_ServerCharacter.RotationX.OnValueChanged += OnRotationXChanged;

                // Setup gear manager
                m_GearManager = GetComponentInChildren<CharacterGearManager>();
                m_GearManager.SetCurrentAttackType(1);

                // Setup IK manager
                m_IKManager = new ClientCharacterIKManager();
                m_IKManager.Initialize(m_GearManager, m_CharacterSwapper, transform);
                m_IKManager.SetGear(m_GearManager.GearLeftHand, m_GearManager.GearRightHand);
                m_Muzzle = m_IKManager.GearMuzzle;
                EnableIK();
#endif  // !P56
            }
        }

        public override void OnNetworkDespawn()
        {
            if (m_ServerCharacter)
            {
                m_ServerCharacter.IsStealthy.OnValueChanged -= OnStealthyChanged;
#if P56
                m_ServerCharacter.MovementDirection.OnValueChanged -= OnMovementDirectionChanged;
                m_ServerCharacter.CurrentGear.OnValueChanged -= OnCurrentGearChanged;
                m_ServerCharacter.IsDefending.OnValueChanged -= OnDefenseStateChanged;
                m_ServerCharacter.IsCrouching.OnValueChanged -= OnCrouchingStateChanged;
#endif  // P56

                if (m_ServerCharacter.TryGetComponent(out ClientInputSender sender))
                {
                    sender.ActionInputEvent -= OnActionInput;
                    sender.ClientMoveEvent -= OnMoveInput;
                }
            }

            enabled = false;
        }

        void OnActionInput(ActionRequestData data)
        {
            m_ClientActionViz.AnticipateAction(ref data);
        }

        void OnMoveInput(Vector3 position)
        {
            if (!IsAnimating())
            {
                OurAnimator.SetTrigger(m_VisualizationConfiguration.AnticipateMoveTriggerID);
            }
        }

        void OnStealthyChanged(bool oldValue, bool newValue)
        {
            SetAppearanceSwap();
        }

        void SetAppearanceSwap()
        {
            if (m_CharacterSwapper)
            {
                var specialMaterialMode = CharacterSwap.SpecialMaterialMode.None;
                if (m_ServerCharacter.IsStealthy.Value)
                {
                    if (m_ServerCharacter.IsOwner)
                    {
                        specialMaterialMode = CharacterSwap.SpecialMaterialMode.StealthySelf;
                    }
                    else
                    {
                        specialMaterialMode = CharacterSwap.SpecialMaterialMode.StealthyOther;
                    }
                }

                m_CharacterSwapper.SwapToModel(specialMaterialMode);
            }
        }

        /// <summary>
        /// Returns the value we should set the Animator's "Speed" variable, given current gameplay conditions.
        /// </summary>
        float GetVisualMovementSpeed(MovementStatus movementStatus)
        {
            if (m_ServerCharacter.NetLifeState.LifeState.Value != LifeState.Alive)
            {
                return m_VisualizationConfiguration.SpeedDead;
            }

            switch (movementStatus)
            {
                case MovementStatus.Idle:
                    return m_VisualizationConfiguration.SpeedIdle;
                case MovementStatus.Normal:
                    return m_VisualizationConfiguration.SpeedNormal;
                case MovementStatus.Uncontrolled:
                    return m_VisualizationConfiguration.SpeedUncontrolled;
                case MovementStatus.Slowed:
                    return m_VisualizationConfiguration.SpeedSlowed;
                case MovementStatus.Hasted:
                    return m_VisualizationConfiguration.SpeedHasted;
                case MovementStatus.Walking:
                    return m_VisualizationConfiguration.SpeedWalking;
#if P56
                case MovementStatus.Dashing:
                    return m_VisualizationConfiguration.SpeedDashing;
#endif  //P56
                default:
                    throw new Exception($"Unknown MovementStatus {movementStatus}");
            }
        }

        void OnMovementStatusChanged(MovementStatus previousValue, MovementStatus newValue)
        {
            if (previousValue == newValue)
            {
                return;
            }

            m_CurrentSpeed = GetVisualMovementSpeed(newValue);

            // Change IK state during dashing.
            if (newValue == MovementStatus.Dashing)
            {
                DisableIK();
            }
            else if (previousValue == MovementStatus.Dashing)
            {
                EnableIK();
            }
        }

#if P56
        void OnMovementDirectionChanged(Vector3 previousValue, Vector3 newValue)
        {
            if (previousValue != newValue)
            {
                m_CurrentMovementDirection = newValue;
            }
        }

        void OnRotationXChanged(float previousValue, float newValue)
        {
            if (previousValue != newValue && !m_ServerCharacter.IsOwner)
            {
                m_RotationX = newValue;
            }
        }

        void OnCurrentGearChanged(int previousValue, int newValue)
        {
            if (previousValue == newValue)
            {
                return;
            }

            // Change current attack type
            m_GearManager.SetCurrentAttackType(newValue);
            m_IKManager.SetGear(m_GearManager.GearLeftHand, m_GearManager.GearRightHand);
            m_Muzzle = m_IKManager.GearMuzzle;
            EnableIK();
        }

        void OnDefenseStateChanged(bool previousValue, bool newValue)
        {
            if (previousValue == newValue)
            {
                return;
            }

            if (m_GearManager.IsDefendableLeft == false && m_GearManager.IsDefendableRight == false)
            {
                m_IsDefending = false;
                return;
            }

            m_IsDefending = newValue;
            EnableIK();
        }

        void OnCrouchingStateChanged(bool previousValue, bool newValue)
        {
            if (previousValue != newValue)
            {
                m_IsCrouching = newValue;

                if (m_IsCrouching == true)
                {
                    OurAnimator.SetTrigger("Crouch");
                }
                else
                {
                    OurAnimator.SetTrigger("StandUp");
                }
            }
        }
#endif  // P56

        void Update()
        {
            // On the host, Characters are translated via ServerCharacterMovement's FixedUpdate method. To ensure that
            // the game camera tracks a GameObject moving in the Update loop and therefore eliminate any camera jitter,
            // this graphics GameObject's position is smoothed over time on the host. Clients do not need to perform any
            // positional smoothing since NetworkTransform will interpolate position updates on the root GameObject.
            if (IsHost)
            {
                // Note: a cached position (m_LerpedPosition) and rotation (m_LerpedRotation) are created and used as
                // the starting point for each interpolation since the root's position and rotation are modified in
                // FixedUpdate, thus altering this transform (being a child) in the process.
                m_LerpedPosition = m_PositionLerper.LerpPosition(m_LerpedPosition,
                    serverCharacter.physicsWrapper.Transform.position);
                m_LerpedRotation = m_RotationLerper.LerpRotation(m_LerpedRotation,
                    serverCharacter.physicsWrapper.Transform.rotation);
                transform.SetPositionAndRotation(m_LerpedPosition, m_LerpedRotation);
            }

#if P56
            if (!m_ServerCharacter.IsNpc)
            {
                Quaternion rotationX = Quaternion.Euler(new Vector3(m_RotationX, 0f, 0f));
                m_LerpedXRotation = m_RotationXLerper.LerpRotation(rotationX, m_LerpedXRotation);
                m_IKManager.OnUpdate(-m_LerpedXRotation.eulerAngles.x);
            }
#endif  // P56

            if (m_ClientVisualsAnimator)
            {
#if !P56
                // set Animator variables here
                OurAnimator.SetFloat(m_VisualizationConfiguration.SpeedVariableID, m_CurrentSpeed);
#else   // !P56
                // set Animator variables here
                Vector3 movAnimSpeed = new Vector3(m_CurrentMovementDirection.x * m_CurrentSpeed, 0f, m_CurrentMovementDirection.z * m_CurrentSpeed);
                m_LerpedMovAnimSpeed = m_MovAnimSpeedLerper.LerpPosition(m_LerpedMovAnimSpeed, movAnimSpeed);

                OurAnimator.SetFloat(m_VisualizationConfiguration.SpeedFBVariableID, m_LerpedMovAnimSpeed.z);
                OurAnimator.SetFloat(m_VisualizationConfiguration.SpeedLRVariableID, m_LerpedMovAnimSpeed.x);
#endif  // !P56
            }

            m_ClientActionViz.OnUpdate();
        }

        void OnAnimEvent(string id)
        {
            //if you are trying to figure out who calls this method, it's "magic". The Unity Animation Event system takes method names as strings,
            //and calls a method of the same name on a component on the same GameObject as the Animator. See the "attack1" Animation Clip as one
            //example of where this is configured.

            m_ClientActionViz.OnAnimEvent(id);
        }

        public bool IsAnimating()
        {
            if (OurAnimator.GetFloat(m_VisualizationConfiguration.SpeedVariableID) > 0.0) { return true; }

            for (int i = 0; i < OurAnimator.layerCount; i++)
            {
                if (OurAnimator.GetCurrentAnimatorStateInfo(i).tagHash != m_VisualizationConfiguration.BaseNodeTagID)
                {
                    //we are in an active node, not the default "nothing" node.
                    return true;
                }
            }

            return false;
        }
#if P56
        public void EnableIK()
        {
            // Don't enable IK if dead.
            if (m_ServerCharacter.NetLifeState.LifeState.Value != LifeState.Alive)
            {
                return;
            }

            if (m_IKManager != null)
            {
                if (m_IsDefending == true)
                {
                    // Left hand
                    if (m_GearManager.IsDefendableLeft == true)
                    {
                        m_IKManager.EnableIK(ClientCharacterIKManager.IKPositionType.HandLeft);
                    }
                    else
                    {
                        m_IKManager.DisableIK(ClientCharacterIKManager.IKPositionType.HandLeft);
                    }

                    // Right hand
                    if (m_GearManager.IsDefendableRight == true)
                    {
                        m_IKManager.EnableIK(ClientCharacterIKManager.IKPositionType.HandRight);
                    }
                    else
                    {
                        m_IKManager.DisableIK(ClientCharacterIKManager.IKPositionType.HandRight);
                    }
                }
                else
                {
                    // Left hand
                    if (m_GearManager.IsActiveGearLeftHand)
                    {
                        m_IKManager.EnableIK(ClientCharacterIKManager.IKPositionType.HandLeft);
                    }
                    else
                    {
                        m_IKManager.DisableIK(ClientCharacterIKManager.IKPositionType.HandLeft);
                    }

                    // Right hand
                    if (m_GearManager.IsActiveGearRightHand)
                    {
                        m_IKManager.EnableIK(ClientCharacterIKManager.IKPositionType.HandRight);
                    }
                    else
                    {
                        m_IKManager.DisableIK(ClientCharacterIKManager.IKPositionType.HandRight);
                    }
                }
            }
        }

        public void DisableIK()
        {
            if (m_IKManager != null)
            {
                m_IKManager.DisableIK(ClientCharacterIKManager.IKPositionType.HandLeft);
                m_IKManager.DisableIK(ClientCharacterIKManager.IKPositionType.HandRight);
            }
        }

        public Vector3 GetAimedPoint()
        {
            Ray ray = Camera.main.ScreenPointToRay(m_ReticleTransform.position);
            Vector3 point = ray.origin + ray.direction * k_AimingRaycastDistance;

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
                    if (k_CachedHit[i].collider.gameObject.name != "PlayerAvatar0") // except self
                    {
                        int layerTest = 1 << k_CachedHit[i].collider.gameObject.layer;
                        if ((layerTest & m_TargetLayerMask) != 0)
                        {
                            if (k_CachedHit[i].distance > 3f)
                            {
                                point = k_CachedHit[i].point;
                            }
                        }
                        break;
                    }
                }
            }

            return point;
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
