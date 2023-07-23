using System;
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
            None,           // None.
            NoChange,       // No change gear even if attack type is changed.
            Inactive,       // Inactive (hide the gear).
            Standby,        // Stand by position (gear is stored to back or side).
            Active,         // Active position (gear is held on hand and on IK position).
        }

        [Serializable]
        public class Gear
        {
            public string m_GearName;
            public GameObject m_Gear;
            public PositionType m_PositionType;
            public bool m_IsShield;

            [Header("Attack1")]
            public GameObject m_Attack1Position;
            public Vector3 m_Attack1PositionOffset;
            public Vector3 m_Attack1RotationOffset;
            public GearState m_Attack1State;
            [Header("Attack2")]
            public GameObject m_Attack2Position;
            public Vector3 m_Attack2PositionOffset;
            public Vector3 m_Attack2RotationOffset;
            public GearState m_Attack2State;
            [Header("Attack3")]
            public GameObject m_Attack3Position;
            public Vector3 m_Attack3PositionOffset;
            public Vector3 m_Attack3RotationOffset;
            public GearState m_Attack3State;
        }

        [SerializeField]
        public Gear[] m_Gears;

        GameObject m_GearLeftHand = null;
        public GameObject GearLeftHand
        {
            get { return m_GearLeftHand; }
        }

        GameObject m_GearRightHand = null;
        public GameObject GearRightHand
        {
            get { return m_GearRightHand; }
        }

        GameObject m_PositionGearLeftHand = null;
        public GameObject PositionGearLeftHand
        {
            get { return m_PositionGearLeftHand; }
        }

        GameObject m_PositionGearRightHand = null;
        public GameObject PositionGearRightHand
        {
            get { return m_PositionGearRightHand; }
        }

        bool m_IsActiveGearLeftHand = false;
        public bool IsActiveGearLeftHand
        {
            get { return m_IsActiveGearLeftHand; }
        }

        bool m_IsActiveGearRightHand = false;
        public bool IsActiveGearRightHand
        {
            get { return m_IsActiveGearRightHand; }
        }

        Vector3 m_PositionOffsetLeftHand = Vector3.zero;
        public Vector3 PositionOffsetLeftHand
        {
            get { return m_PositionOffsetLeftHand; }
        }

        Vector3 m_PositionOffsetRightHand = Vector3.zero;
        public Vector3 PositionOffsetRightHand
        {
            get { return m_PositionOffsetRightHand; }
        }

        Vector3 m_RotationOffsetLeftHand = Vector3.zero;
        public Vector3 RotationOffsetLeftHand
        {
            get { return m_RotationOffsetLeftHand; }
        }

        Vector3 m_RotationOffsetRightHand = Vector3.zero;
        public Vector3 RotationOffsetRightHand
        {
            get { return m_RotationOffsetRightHand; }
        }

        bool m_IsDfendableLeft = false;
        public bool IsDefendableLeft
        {
            get { return m_IsDfendableLeft; }
        }

        bool m_IsDfendableRight = false;
        public bool IsDefendableRight
        {
            get { return m_IsDfendableRight; }
        }

        public void SetCurrentAttackType(int attackType)
        {
            m_IsDfendableLeft = false;
            m_IsDfendableRight = false;

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
                                m_PositionOffsetLeftHand = positionOffset;
                                m_RotationOffsetLeftHand = rotationOffset;
                                if (gear.m_IsShield == true)
                                {
                                    m_IsDfendableLeft = true;
                                }
                                else
                                {
                                    m_IsActiveGearLeftHand = true;
                                    m_IsActiveGearRightHand = false;
                                }
                                break;
                            case PositionType.HandRight:
                                m_GearRightHand = gear.m_Gear;
                                m_PositionGearRightHand = position;
                                m_PositionOffsetRightHand = positionOffset;
                                m_RotationOffsetRightHand = rotationOffset;
                                if (gear.m_IsShield == true)
                                {
                                    m_IsDfendableRight = true;
                                }
                                else
                                {
                                    m_IsActiveGearLeftHand = false;
                                    m_IsActiveGearRightHand = true;
                                }
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
