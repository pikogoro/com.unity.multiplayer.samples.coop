#if P56
using UnityEngine;
using Unity.BossRoom.Gameplay.GameplayObjects;

namespace Unity.BossRoom.Gameplay.Actions
{
    public class AutomaticActionInput : BaseActionInput
    {
        float TimeStarted { get; set; }
        float TimeRunning { get { return (Time.time - TimeStarted); } }

        ActionConfig m_Config;

        private void Start()
        {
            m_Config = GameDataSource.Instance.GetActionPrototypeByID(m_ActionPrototypeID).Config;

            LaunchProjectile();
        }

        public override void OnReleaseKey()
        {
            Destroy(gameObject);
        }

        void FixedUpdate()
        {
            //if (TimeRunning >= m_Config.ExecTimeSeconds + m_Config.ReuseTimeSeconds)
            if (TimeRunning >= m_Config.ReuseTimeSeconds)
            {
                LaunchProjectile();
            }
        }

        void LaunchProjectile()
        {
            Vector3 position = m_PlayerOwner.clientCharacter.MuzzleLocalPosition;
            Vector3 direction = m_PlayerOwner.clientCharacter.GetAimedPoint() - m_PlayerOwner.clientCharacter.MuzzlePosition;

            var data = new ActionRequestData
            {
                Position = position,
                Direction = direction,
                ActionID = m_ActionPrototypeID,
                ShouldQueue = false,
                TargetIds = null
            };
            m_SendInput(data);

            // Update interval
            TimeStarted = Time.time;
        }
    }
}
#endif // P56
