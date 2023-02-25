#define USE_THRUSTER

using System;
using Unity.BossRoom.Gameplay.Configuration;
using Unity.BossRoom.Navigation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;
#if P56
using Unity.BossRoom.Gameplay.Actions;
#endif  // P56

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    public enum MovementState
    {
        Idle = 0,
        PathFollowing = 1,
        Charging = 2,
        Knockback = 3,
#if P56
        PlayerMovement = 4, // Player only
        PlayerMovement_Boost = 5, // Player only
#endif  // P56
    }

    /// <summary>
    /// Component responsible for moving a character on the server side based on inputs.
    /// </summary>
    /*[RequireComponent(typeof(NetworkCharacterState), typeof(NavMeshAgent), typeof(ServerCharacter)), RequireComponent(typeof(Rigidbody))]*/
    public class ServerCharacterMovement : NetworkBehaviour
    {
        [SerializeField]
        NavMeshAgent m_NavMeshAgent;

        [SerializeField]
        Rigidbody m_Rigidbody;

        private NavigationSystem m_NavigationSystem;

        private DynamicNavPath m_NavPath;
#if P56
        private Quaternion m_Rotation = ActionMovement.RotationNull;
        private bool m_HasLockOnTarget = false;
        public bool HasLockOnTarget { get { return m_HasLockOnTarget; } }

        // For jump
        float m_UpwardVelocity = 0f;
        bool m_IsGrounded = true;
        PositionUtil m_PositionUtil;
        const float k_MaxNavMeshDistance = 1f;
        bool m_IsOnNavmesh = true;
        Vector3 m_MovementPosition;

        float m_RotationX;
        float m_PreviousRotationX;

        Vector3 m_MovementDirection;
        Vector3 m_PreviousMovementDirection;

        bool m_IsBoost = false;
#endif  // P56

        private MovementState m_MovementState;

        MovementStatus m_PreviousState;

        [SerializeField]
        private ServerCharacter m_CharLogic;

        // when we are in charging and knockback mode, we use these additional variables
        private float m_ForcedSpeed;
        private float m_SpecialModeDurationRemaining;

        // this one is specific to knockback mode
        private Vector3 m_KnockbackVector;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool TeleportModeActivated { get; set; }

        const float k_CheatSpeed = 20;

        public bool SpeedCheatActivated { get; set; }
#endif

        void Awake()
        {
            // disable this NetworkBehavior until it is spawned
            enabled = false;

#if P56
            m_PositionUtil = new PositionUtil();
#endif  // P56
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // Only enable server component on servers
                enabled = true;

                // On the server enable navMeshAgent and initialize
                m_NavMeshAgent.enabled = true;
                m_NavigationSystem = GameObject.FindGameObjectWithTag(NavigationSystem.NavigationSystemTag).GetComponent<NavigationSystem>();
                m_NavPath = new DynamicNavPath(m_NavMeshAgent, m_NavigationSystem);
            }
        }

        /// <summary>
        /// Sets a movement target. We will path to this position, avoiding static obstacles.
        /// </summary>
        /// <param name="position">Position in world space to path to. </param>
#if !P56
        public void SetMovementTarget(Vector3 position)
#else   // !P56
        public void SetMovementTarget(ActionMovement movement)
#endif  // !P56
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TeleportModeActivated)
            {
#if !P56
                Teleport(position);
#else   // !P56
                if (!ActionMovement.IsNull(movement.Position))
                {
                    Teleport(movement.Position);
                }
#endif  // !P56
                return;
            }
#endif
#if !P56
            m_MovementState = MovementState.PathFollowing;
            m_NavPath.SetTargetPosition(position);
#else   // !P56
            m_MovementState = MovementState.PlayerMovement;

            if (ActionMovement.IsNull(movement.Position))
            {
                // if movement position is null, don't move.
                m_MovementPosition = transform.position;
            }
            else
            {
                // if movement position is not null, set new posision to movment position.
                if (m_IsGrounded && m_IsOnNavmesh)
                {
                    Vector3 groundPosition = m_PositionUtil.GetGroundPosition(movement.Position);
                    m_NavPath.SetTargetPosition(groundPosition, m_HasLockOnTarget);
                }
                else
                {
                    m_MovementPosition = movement.Position;
                }
            }

            if (ActionMovement.IsNull(movement.Rotation))
            {
                // if movement is finished (both movement position and rotation are null), set rotation to null.
                m_Rotation = (ActionMovement.IsNull(movement.Position)) ? ActionMovement.RotationNull : transform.rotation;
            }
            else
            {
                m_HasLockOnTarget = false;  // Reset lock on.
                m_Rotation = movement.Rotation;
            }

            m_RotationX = movement.RotationX;

            // For jump
#if USE_THRUSTER
            if (0f < movement.UpwardVelocity)
            {
                m_UpwardVelocity = movement.UpwardVelocity;
            }
#else   // USE_THRUSTER
            if (m_IsGrounded && 0f < movement.UpwardVelocity)
            {
                m_UpwardVelocity = movement.UpwardVelocity;
            }
             else
            {
                if (0f < m_UpwardVelocity && movement.UpwardVelocity == 0f)
                {
                    m_UpwardVelocity = 0f;
                }
            }
#endif  // USE_THRUSTER

            // For boost
            if (movement.BoostChange)
            {
                if (m_IsBoost)
                {
                    m_IsBoost = false;
                }
                else
                {
                    m_IsBoost = true;
                }
            }

            // For gear
            if (0 < movement.ChosedGear)
            {
                m_CharLogic.CurrentGear.Value = movement.ChosedGear;
            }
#endif  // !P56
        }

        public void StartForwardCharge(float speed, float duration)
        {
            m_NavPath.Clear();
            m_MovementState = MovementState.Charging;
            m_ForcedSpeed = speed;
            m_SpecialModeDurationRemaining = duration;
        }

        public void StartKnockback(Vector3 knocker, float speed, float duration)
        {
            m_NavPath.Clear();
            m_MovementState = MovementState.Knockback;
            m_KnockbackVector = transform.position - knocker;
            m_ForcedSpeed = speed;
            m_SpecialModeDurationRemaining = duration;
        }

        /// <summary>
        /// Follow the given transform until it is reached.
        /// </summary>
        /// <param name="followTransform">The transform to follow</param>
        public void FollowTransform(Transform followTransform)
        {
            m_MovementState = MovementState.PathFollowing;
            m_NavPath.FollowTransform(followTransform);
        }

#if P56
        /*
        public void LockOnTransform(Transform lockOnTransform)
        {
            if (lockOnTransform != null)
            {
                m_MovementState = MovementState.PathFollowing;
                m_NavPath.LockOnTransform(lockOnTransform);
                m_HasLockOnTarget = true;
            }
            else
            {
                m_HasLockOnTarget = false;
            }
        }
        */
#endif  // P56

        /// <summary>
        /// Returns true if the current movement-mode is unabortable (e.g. a knockback effect)
        /// </summary>
        /// <returns></returns>
        public bool IsPerformingForcedMovement()
        {
            return m_MovementState == MovementState.Knockback || m_MovementState == MovementState.Charging;
        }

        /// <summary>
        /// Returns true if the character is actively moving, false otherwise.
        /// </summary>
        /// <returns></returns>
        public bool IsMoving()
        {
            return m_MovementState != MovementState.Idle;
        }

        /// <summary>
        /// Cancels any moves that are currently in progress.
        /// </summary>
        public void CancelMove()
        {
            m_NavPath?.Clear();
            m_MovementState = MovementState.Idle;
        }

        /// <summary>
        /// Instantly moves the character to a new position. NOTE: this cancels any active movement operation!
        /// This does not notify the client that the movement occurred due to teleportation, so that needs to
        /// happen in some other way, such as with the custom action visualization in DashAttackActionFX. (Without
        /// this, the clients will animate the character moving to the new destination spot, rather than instantly
        /// appearing in the new spot.)
        /// </summary>
        /// <param name="newPosition">new coordinates the character should be at</param>
        public void Teleport(Vector3 newPosition)
        {
            CancelMove();
            if (!m_NavMeshAgent.Warp(newPosition))
            {
                // warping failed! We're off the navmesh somehow. Weird... but we can still teleport
                Debug.LogWarning($"NavMeshAgent.Warp({newPosition}) failed!", gameObject);
                transform.position = newPosition;
            }

            m_Rigidbody.position = transform.position;
            m_Rigidbody.rotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            PerformMovement();

            var currentState = GetMovementStatus(m_MovementState);
            if (m_PreviousState != currentState)
            {
                m_CharLogic.MovementStatus.Value = currentState;
                m_PreviousState = currentState;
            }
#if P56
            if (m_PreviousRotationX != m_RotationX)
            {
                m_CharLogic.RotationX.Value = m_RotationX;
                m_PreviousRotationX = m_RotationX;
            }

            if (m_PreviousMovementDirection != m_MovementDirection)
            {
                m_CharLogic.MovementDirection.Value = m_MovementDirection;
                m_PreviousMovementDirection = m_MovementDirection;
            }
#endif  // P56
        }

        public override void OnNetworkDespawn()
        {
            if (m_NavPath != null)
            {
                m_NavPath.Dispose();
            }
            if (IsServer)
            {
                // Disable server components when despawning
                enabled = false;
                m_NavMeshAgent.enabled = false;
            }
        }

        private void PerformMovement()
        {
            if (m_MovementState == MovementState.Idle)
#if P56
            {
                m_IsBoost = false;
                return;
            }
#else   // P56
                return;
#endif   // P56

            Vector3 movementVector;

            if (m_MovementState == MovementState.Charging)
            {
                // if we're done charging, stop moving
                m_SpecialModeDurationRemaining -= Time.fixedDeltaTime;
                if (m_SpecialModeDurationRemaining <= 0)
                {
                    m_MovementState = MovementState.Idle;
                    return;
                }

                var desiredMovementAmount = m_ForcedSpeed * Time.fixedDeltaTime;
                movementVector = transform.forward * desiredMovementAmount;
            }
            else if (m_MovementState == MovementState.Knockback)
            {
                m_SpecialModeDurationRemaining -= Time.fixedDeltaTime;
                if (m_SpecialModeDurationRemaining <= 0)
                {
                    m_MovementState = MovementState.Idle;
                    return;
                }

                var desiredMovementAmount = m_ForcedSpeed * Time.fixedDeltaTime;
                movementVector = m_KnockbackVector * desiredMovementAmount;
            }
            else
            {
                var desiredMovementAmount = GetBaseMovementSpeed() * Time.fixedDeltaTime;
#if !P56
                movementVector = m_NavPath.MoveAlongPath(desiredMovementAmount);
#else   // !P56
                if (m_IsBoost)
                {
                    desiredMovementAmount *= 2f;
                    m_MovementState = MovementState.PlayerMovement_Boost;
                }

                if (m_IsGrounded && m_IsOnNavmesh)
                {
                    movementVector = m_NavPath.MoveAlongPath(desiredMovementAmount);
                }
                else
                {
                    Vector3 tmpPosition = m_MovementPosition;
                    tmpPosition.y = transform.position.y;
                    movementVector = (tmpPosition - transform.position).normalized * desiredMovementAmount;
                }

                m_MovementDirection = transform.InverseTransformDirection(movementVector).normalized;
#endif  // !P56

#if !P56
                // If we didn't move stop moving.
                if (movementVector == Vector3.zero)
#else   // !P56
                // Stop moving.
                if (movementVector == Vector3.zero && ActionMovement.IsNull(m_Rotation) && m_IsGrounded)
#endif   // !P56
                {
                    m_MovementState = MovementState.Idle;
                    return;
                }
            }

#if !P56
            m_NavMeshAgent.Move(movementVector);
            transform.rotation = Quaternion.LookRotation(movementVector);
#else   // !P56

            if (0f < m_UpwardVelocity)
            {
                m_IsGrounded = false;
            }

            if (m_IsGrounded)
            {
                if (m_IsOnNavmesh)
                {
                    m_NavMeshAgent.Move(movementVector);
                }
                else
                {
                    // [TODO] perform movement on out of Navmesh.
                    m_MovementPosition = transform.position;
                }
            }
            else
            {
                // Calculate character next position by movement vector and upward velocity.
                Vector3 currentPositon = transform.position;
                Vector3 nextPosition = currentPositon + movementVector;
                nextPosition.y += m_UpwardVelocity * Time.fixedDeltaTime;

                Vector3 blockedPosition = m_PositionUtil.GetBlockedPosition(currentPositon, nextPosition, 0.5f);
                if (blockedPosition != Vector3.zero)
                {
                    nextPosition = blockedPosition;
                }

                // Get ground position by character's top position.
                Vector3 groundPosition = m_PositionUtil.GetGroundPosition(nextPosition, 0.5f);

                // verify ground position is indeed on navmesh surface
                if (NavMesh.SamplePosition(groundPosition,
                        out var hit,
                        k_MaxNavMeshDistance,
                        NavMesh.AllAreas))
                {
                    // On NavMesh.
                    m_IsOnNavmesh = true;
                }
                else
                {
                    // Off NavMesh.
                    m_IsOnNavmesh = false;
                }

                if (groundPosition.y < nextPosition.y)
                {
                    // If in air, stop NavMeshAgene.

                    if (!m_NavMeshAgent.isStopped)
                    {
                        m_NavMeshAgent.updatePosition = false;
                        m_NavMeshAgent.isStopped = true;
                    }

                    // Update character position.
                    transform.position = nextPosition;

                    // Update upward velocity by gravity.
                    m_IsGrounded = false;
                    m_UpwardVelocity += Physics.gravity.y * Time.fixedDeltaTime;

                    // Trigger "rise" animation transition.
                    m_CharLogic.serverAnimationHandler.NetworkAnimator.SetTrigger("Rise");
                }
                else
                {
                    // If upward velocity is not positive value, character was landed on ground.

                    if (m_IsOnNavmesh)
                    {
                        // If on NavMesh, start NavMeshAgent.
                        m_NavMeshAgent.updatePosition = true;
                        m_NavMeshAgent.isStopped = false;
                        m_NavMeshAgent.Warp(groundPosition);  // Warp character position.
                        m_NavPath.Clear();  // Clear path.
                    }

                    // Update character position.
                    transform.position = nextPosition;
                    m_MovementPosition = transform.position;

                    // If on the ground, stop falling.
                    m_IsGrounded = true;
                    m_UpwardVelocity = 0f;

                    // Trigger "Grounded" animation transition.
                    m_CharLogic.serverAnimationHandler.NetworkAnimator.SetTrigger("Grounded");
                }
            }

            // Change direction.
            if (m_MovementState == MovementState.Charging || m_MovementState == MovementState.PathFollowing)
            {
                transform.rotation = Quaternion.LookRotation(movementVector);
            }
#if UNITY_ANDROID
            else if (m_NavPath.TransformLockOnTarget != null)
            {
                transform.LookAt(m_NavPath.TransformLockOnTarget.position);
            }
#endif  // UNITY_ANDROID
            else
            {
                if (!ActionMovement.IsNull(m_Rotation))
                {
                    transform.rotation = Quaternion.Euler(new Vector3(0f, m_Rotation.eulerAngles.y, 0f));
                }
            }
#endif  // !P56

            // After moving adjust the position of the dynamic rigidbody.
            m_Rigidbody.position = transform.position;
            m_Rigidbody.rotation = transform.rotation;
        }

        /// <summary>
        /// Retrieves the speed for this character's class.
        /// </summary>
        private float GetBaseMovementSpeed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (SpeedCheatActivated)
            {
                return k_CheatSpeed;
            }
#endif
            CharacterClass characterClass = GameDataSource.Instance.CharacterDataByType[m_CharLogic.CharacterType];
            Assert.IsNotNull(characterClass, $"No CharacterClass data for character type {m_CharLogic.CharacterType}");
            return characterClass.Speed;
        }

        /// <summary>
        /// Determines the appropriate MovementStatus for the character. The
        /// MovementStatus is used by the client code when animating the character.
        /// </summary>
        private MovementStatus GetMovementStatus(MovementState movementState)
        {
            switch (movementState)
            {
                case MovementState.Idle:
                    return MovementStatus.Idle;
                case MovementState.Knockback:
                    return MovementStatus.Uncontrolled;
#if P56
                case MovementState.PlayerMovement_Boost:
                    return MovementStatus.Boosted;
#endif  // P56
                default:
                    return MovementStatus.Normal;
            }
        }
    }
}
