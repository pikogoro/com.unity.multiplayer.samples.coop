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
        float m_SavedPositionY = 0f;    // Saved position y of character
        float m_UpwardVelocity = 0f;
        bool m_IsGrounded = true;
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
            m_MovementState = MovementState.PathFollowing;
#if !P56
            m_NavPath.SetTargetPosition(position);
#else   // !P56
            if (ActionMovement.IsNull(movement.Position))
            {
                m_NavPath.SetTargetPosition(transform.position, m_HasLockOnTarget);
            }
            else
            {
                m_NavPath.SetTargetPosition(movement.Position, m_HasLockOnTarget);
            }

            if (ActionMovement.IsNull(movement.Rotation))
            {
                // Set direction to null if movement is finished.
                m_Rotation = (ActionMovement.IsNull(movement.Position)) ? ActionMovement.RotationNull : transform.rotation;
            }
            else
            {
                // Reset lock on.
                m_HasLockOnTarget = false;
                m_Rotation = movement.Rotation;
            }

            // For jump
            if (m_IsGrounded && 0f < movement.UpwardVelocity)
            {
                m_UpwardVelocity = movement.UpwardVelocity;
                m_IsGrounded = false;
            }
            else
            {
                if (0f < m_UpwardVelocity && movement.UpwardVelocity == 0f)
                {
                    m_UpwardVelocity = 0f;
                }
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
        public void LockOnTransform(Transform lockOnTransform)
        {
            m_MovementState = MovementState.PathFollowing;
            m_NavPath.LockOnTransform(lockOnTransform);
            m_HasLockOnTarget = true;
        }
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
                return;

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
                movementVector = m_NavPath.MoveAlongPath(desiredMovementAmount);

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
            // Restart NavMeshAgent for movement of character.
            if (m_NavMeshAgent.isStopped)
            {
                m_NavMeshAgent.updatePosition = true;
                m_NavMeshAgent.isStopped = false;
            }
            m_NavMeshAgent.Move(movementVector);

            if (!m_IsGrounded)
            {
                // Update position y of character.
                m_SavedPositionY += m_UpwardVelocity * Time.fixedDeltaTime;

                Vector3 position = m_NavMeshAgent.transform.position;
                if (position.y < m_SavedPositionY)
                {
                    // If in the air, stop NavMeshAgent.
                    m_NavMeshAgent.updatePosition = false;
                    m_NavMeshAgent.isStopped = true;

                    // Update transform by saved position y directly.
                    position.y = m_SavedPositionY;
                    transform.position = position;

                    // Update upward velocity by gravity.
                    m_UpwardVelocity += Physics.gravity.y * Time.fixedDeltaTime;
                }
                else
                {
                    // If on the ground, reset saved position y and upward velocity.
                    m_IsGrounded = true;
                    m_SavedPositionY = 0f;
                    m_UpwardVelocity = 0f;
                }
            }

            // Change direction.
            if (m_MovementState == MovementState.Charging || m_MovementState == MovementState.Knockback || ActionMovement.IsNull(m_Rotation))
            {
                if (movementVector != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(movementVector);
                }
            }
            else if (m_NavPath.TransformLockOnTarget != null)
            {
                transform.LookAt(m_NavPath.TransformLockOnTarget.position);
            }
            else
            {
                transform.rotation = Quaternion.Euler(new Vector3(0f, m_Rotation.eulerAngles.y, 0f));
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
                default:
                    return MovementStatus.Normal;
            }
        }
    }
}
