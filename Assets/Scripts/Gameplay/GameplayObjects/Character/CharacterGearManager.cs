using System;
using Unity.BossRoom.CameraUtils;
using Unity.BossRoom.Gameplay.UserInput;
using Unity.BossRoom.Gameplay.Configuration;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Utils;
using Unity.Netcode;
using UnityEngine;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    /// <summary>
    /// </summary>
    public class CharacterGearManager : MonoBehaviour
    {
        public enum PositionType
        {
            Other,
            HandLeft,
            HandRight,
        }

        public enum GearState
        {
            None,           // none
            NoChange,       // no change
            Inactive,       // inactive
            Standby,        // stand by
            Active,         // active
        }

        [Serializable]
        public class Gear
        {
            public string m_GearName;
            public GameObject m_Gear;
            public PositionType m_PositionType;
            public bool m_IsShield;

            // Attack1
            public GameObject m_Attack1Position;
            public Vector3 m_Attack1PositionOffset;
            public Vector3 m_Attack1RotationOffset;
            public GearState m_Attack1State;
            // Attack2
            public GameObject m_Attack2Position;
            public Vector3 m_Attack2PositionOffset;
            public Vector3 m_Attack2RotationOffset;
            public GearState m_Attack2State;
            // Attack3
            public GameObject m_Attack3Position;
            public Vector3 m_Attack3PositionOffset;
            public Vector3 m_Attack3RotationOffset;
            public GearState m_Attack3State;
        }

        [SerializeField]
        public Gear[] m_Gears;

        GameObject m_GearLeftHand;
        public GameObject GearLeftHand
        {
            get { return m_GearLeftHand; }
        }

        GameObject m_GearRightHand;
        public GameObject GearRightHand
        {
            get { return m_GearRightHand; }
        }

        GameObject m_PositionGearLeftHand;
        public GameObject PositionGearLeftHand
        {
            get { return m_PositionGearLeftHand; }
        }

        GameObject m_PositionGearRightHand;
        public GameObject PositionGearRightHand
        {
            get { return m_PositionGearRightHand; }
        }

        bool m_IsActiveGearLeftHand;
        public bool IsActiveGearLeftHand
        {
            get { return m_IsActiveGearLeftHand; }
        }

        bool m_IsActiveGearRightHand;
        public bool IsActiveGearRightHand
        {
            get { return m_IsActiveGearRightHand; }
        }

        public void SetCurrentAttackType(int attackType)
        {
            for (int i = 0; i < m_Gears.Length; i++)
            {
                Gear gear = m_Gears[i];
                GameObject position = null;
                Vector3 positionOffset = Vector3.zero;
                Vector3 rotationOffset = Vector3.zero;
                GearState state = GearState.None;

                switch (attackType)
                {
                    case 1:
                        position = gear.m_Attack1Position;
                        positionOffset = gear.m_Attack1PositionOffset;
                        rotationOffset = gear.m_Attack1RotationOffset;
                        state = gear.m_Attack1State;
                        break;
                    case 2:
                        position = gear.m_Attack2Position;
                        positionOffset = gear.m_Attack2PositionOffset;
                        rotationOffset = gear.m_Attack2RotationOffset;
                        state = gear.m_Attack2State;
                        break;
                    case 3:
                        position = gear.m_Attack3Position;
                        positionOffset = gear.m_Attack3PositionOffset;
                        rotationOffset = gear.m_Attack3RotationOffset;
                        state = gear.m_Attack3State;
                        break;
                }

                switch (state)
                {
                    case GearState.Inactive:
                        gear.m_Gear.SetActive(false);
                        break;
                    case GearState.Standby:
                        gear.m_Gear.SetActive(true);
                        gear.m_Gear.transform.SetParent(position.transform);
                        gear.m_Gear.transform.localPosition = positionOffset;
                        gear.m_Gear.transform.localRotation = Quaternion.Euler(rotationOffset);
                        break;
                    case GearState.Active:
                        gear.m_Gear.SetActive(true);
                        switch (gear.m_PositionType)
                        {
                            case PositionType.HandLeft:
                                m_GearLeftHand = gear.m_Gear;
                                m_PositionGearLeftHand = position;
                                if (gear.m_IsShield)
                                {
                                    m_IsActiveGearLeftHand = false;
                                }
                                else
                                {
                                    m_IsActiveGearLeftHand = true;
                                }
                                break;
                            case PositionType.HandRight:
                                m_GearRightHand = gear.m_Gear;
                                m_PositionGearRightHand = position;
                                m_IsActiveGearRightHand = true;
                                break;
                            case PositionType.Other:
                                gear.m_Gear.transform.SetParent(position.transform);
                                gear.m_Gear.transform.localPosition = positionOffset;
                                gear.m_Gear.transform.localRotation = Quaternion.Euler(rotationOffset);
                                m_IsActiveGearLeftHand = false;
                                m_IsActiveGearRightHand = false;
                                break;
                        }
                        break;
                }
            }
        }
    }
}
