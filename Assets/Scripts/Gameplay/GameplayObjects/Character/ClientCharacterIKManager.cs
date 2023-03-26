using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Unity.BossRoom.Gameplay.GameplayObjects.Character
{
    /// <summary>
    /// </summary>
    public class ClientCharacterIKManager
    {
        public enum IKPositionType
        {
            HandLeft,
            HandRight,
        }

        CharacterGearManager m_GearManager;

        GameObject m_View = null;
        GameObject m_Eyes = null;

        // Two Bone IK Constraint
        TwoBoneIKConstraint m_LeftHandIKConstraint;
        TwoBoneIKConstraint m_RightHandIKConstraint;

        // Left hand
        GameObject m_HandLeft = null;
        Vector3 m_LeftHandIKRotationOffset;
        float m_LeftHandIKWeight = 0f;
        Transform m_LeftHandIKTarget = null;

        // Right hand
        GameObject m_HandRight = null;
        Vector3 m_RightHandIKRotationOffset;
        float m_RightHandIKWeight = 0f;
        Transform m_RightHandIKTarget = null;

        // Gear
        GameObject m_GearLeftHand = null;
        GameObject m_GearRightHand = null;
        Transform m_GearLeftHandPosition = null;
        Transform m_GearRightHandPosition = null;
        Transform m_GearMuzzle = null;
        bool m_TwoHanded = false;
        public Transform GearMuzzle
        {
            get { return m_GearMuzzle; }
        }

        //public void Initialize(CharacterSwap characterSwap, Transform transform)
        public void Initialize(CharacterGearManager gearManager, CharacterSwap characterSwap, Transform transform)
        {
            m_GearManager = gearManager;

            // View (camara)
            m_View = characterSwap.CharacterModel.view;

            // Eyes
            m_Eyes = characterSwap.CharacterModel.eyes;
 
            // Left hand
            m_HandLeft = characterSwap.CharacterModel.handLeft;
            m_LeftHandIKConstraint = characterSwap.CharacterModel.leftHandIK.GetComponent<TwoBoneIKConstraint>();
            m_LeftHandIKTarget = m_LeftHandIKConstraint.data.target;
            m_LeftHandIKRotationOffset = characterSwap.CharacterModel.leftHandIKRotationOffset;

            // Right hand
            m_HandRight = characterSwap.CharacterModel.handRight;
            m_RightHandIKConstraint = characterSwap.CharacterModel.rightHandIK.GetComponent<TwoBoneIKConstraint>();
            m_RightHandIKTarget = m_RightHandIKConstraint.data.target;
            m_RightHandIKRotationOffset = characterSwap.CharacterModel.rightHandIKRotationOffset;

            // Add rig to rig builder and rebuild.
            Rig rig = transform.GetComponentInChildren<Rig>();
            if (rig != null)
            {
                RigBuilder rigBuilder = transform.GetComponent<RigBuilder>();
                rigBuilder.layers.Clear();
                rigBuilder.layers.Add(new RigLayer(rig));
                rigBuilder.enabled = true;
                rigBuilder.Build();
            }
        }

        public void SetGear(GameObject gearLeftHand, GameObject gearRightHand)
        {
            m_GearLeftHand = gearLeftHand;
            m_GearRightHand = gearRightHand;

            if (m_GearLeftHand != null)
            {
                m_GearLeftHandPosition = m_GearLeftHand.transform.Find("grip");
                m_GearMuzzle = m_GearLeftHand.transform.Find("muzzle");
            }
            else if (m_GearRightHand != null)
            {
                m_GearLeftHandPosition = m_GearRightHand.transform.Find("foreend");
                if (m_GearLeftHandPosition != null)
                {
                    m_TwoHanded = true;
                }
                else
                {
                    m_TwoHanded = false;
                }
            }

            if (m_GearRightHand != null)
            {
                m_GearRightHandPosition = m_GearRightHand.transform.Find("grip");
                m_GearMuzzle = m_GearRightHand.transform.Find("muzzle");
            }
            else if (m_GearLeftHand != null)
            {
                m_GearRightHandPosition = m_GearLeftHand.transform.Find("foreend");
                if (m_GearRightHandPosition != null)
                {
                    m_TwoHanded = true;
                }
                else
                {
                    m_TwoHanded = false;
                }
            }
        }

        public void OnUpdate(float rotaionX)
        {
            // For crouching (change view position during crouching).
            //m_View.transform.localPosition = transform.InverseTransformPoint(m_Eyes.transform.position);
            m_View.transform.position = m_Eyes.transform.position;

            // Update IK weight.
            m_LeftHandIKConstraint.weight = m_LeftHandIKWeight;
            m_RightHandIKConstraint.weight = m_RightHandIKWeight;

            // Rotate IK position according with character's view.
            m_View.transform.localRotation = Quaternion.Euler(rotaionX, 0f, 0f);
            if (m_GearLeftHandPosition != null)
            {
                m_LeftHandIKTarget.position = m_GearLeftHandPosition.position;
                m_LeftHandIKTarget.localRotation = Quaternion.Euler(m_LeftHandIKRotationOffset) * Quaternion.Euler(-rotaionX, 0f, 0f);
            }
            if (m_GearRightHandPosition != null)
            {
                m_RightHandIKTarget.position = m_GearRightHandPosition.position;
                m_RightHandIKTarget.localRotation = Quaternion.Euler(m_RightHandIKRotationOffset) * Quaternion.Euler(-rotaionX, 0f, 0f);
            }
        }

        public void EnableIK(IKPositionType positionType)
        {
            switch (positionType)
            {
                case IKPositionType.HandLeft:
                    m_LeftHandIKWeight = 1f;
                    if (m_TwoHanded == true)
                    {
                        m_RightHandIKWeight = 1f;
                    }
                    if (m_GearLeftHand != null)
                    {
                        // Change parent and reset local position and rotation according with parent.
                        m_GearLeftHand.transform.SetParent(m_GearManager.PositionGearLeftHand.transform);
                        m_GearLeftHand.transform.localPosition = m_GearManager.PositionOffsetLeftHand;
                        m_GearLeftHand.transform.localRotation = Quaternion.Euler(m_GearManager.RotationOffsetLeftHand);
                    }
                    break;
                case IKPositionType.HandRight:
                    m_RightHandIKWeight = 1f;
                    if (m_TwoHanded == true)
                    {
                        m_LeftHandIKWeight = 1f;
                    }
                    if (m_GearRightHand != null)
                    {
                        // Change parent and reset local position and rotation according with parent.
                        m_GearRightHand.transform.SetParent(m_GearManager.PositionGearRightHand.transform);
                        m_GearRightHand.transform.localPosition = m_GearManager.PositionOffsetRightHand;
                        m_GearRightHand.transform.localRotation = Quaternion.Euler(m_GearManager.RotationOffsetRightHand);
                    }
                    break;
            }
        }

        public void DisableIK(IKPositionType positionType)
        {
            switch (positionType)
            {
                case IKPositionType.HandLeft:
                    m_LeftHandIKWeight = 0f;
                    if (m_TwoHanded == true)
                    {
                        m_RightHandIKWeight = 0f;
                    }
                    if (m_GearLeftHand != null)
                    {
                        // Change parent and reset local position and rotation according with parent.
                        m_GearLeftHand.transform.SetParent(m_HandLeft.transform);
                        m_GearLeftHand.transform.localPosition = Vector3.zero;
                        m_GearLeftHand.transform.localRotation = Quaternion.identity;
                    }
                    break;
                case IKPositionType.HandRight:
                    m_RightHandIKWeight = 0f;
                    if (m_TwoHanded == true)
                    {
                        m_LeftHandIKWeight = 0f;
                    }
                    if (m_GearRightHand != null)
                    {
                        // Change parent and reset local position and rotation according with parent.
                        m_GearRightHand.transform.SetParent(m_HandRight.transform);
                        m_GearRightHand.transform.localPosition = Vector3.zero;
                        m_GearRightHand.transform.localRotation = Quaternion.identity;
                    }
                    break;
            }
        }
    }
}
