using System;
using Unity.BossRoom.CameraUtils;
using Unity.BossRoom.Gameplay.UserInput;
using Unity.BossRoom.Gameplay.Configuration;
using Unity.BossRoom.Gameplay.Actions;
using Unity.BossRoom.Utils;
using Unity.Netcode;
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

        GameObject m_View = null;

        // Two Bone IK Constraint
        TwoBoneIKConstraint m_LeftHandIKConstraint;
        TwoBoneIKConstraint m_RightHandIKConstraint;

        // Left hand
        GameObject m_HandLeft = null;
        Vector3 m_LeftHandIKRotationOffset;
        float m_LeftHandIKWeight = 0f;
        Transform m_LeftHandIKTarget = null;
        Transform m_LeftHandIKPosition = null;

        // Right hand
        GameObject m_HandRight = null;
        Vector3 m_RightHandIKRotationOffset;
        float m_RightHandIKWeight = 0f;
        Transform m_RightHandIKTarget = null;
        Transform m_RightHandIKPosition = null;

        // Gear
        GameObject m_GearLeftHand = null;
        GameObject m_GearRightHand = null;
        Transform m_GearLeftHandPosition = null;
        Transform m_GearRightHandPosition = null;
        Transform m_GearMuzzle = null;
        public Transform GearMuzzle
        {
            get { return m_GearMuzzle; }
        }

        public void Initialize(CharacterSwap characterSwap, Transform transform)
        {
            m_View = characterSwap.CharacterModel.view;
            m_LeftHandIKPosition = m_View.transform.Find("leftHandIK_position");
            m_RightHandIKPosition = m_View.transform.Find("rightHandIK_position");

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

        public void SetGearLeftHand(GameObject gear)
        {
            m_GearLeftHand = gear;
            m_GearLeftHandPosition = m_GearLeftHand.transform.Find("foreend");
            if (m_GearLeftHandPosition == null)
            {
                m_GearLeftHandPosition = m_GearLeftHand.transform.Find("grip");
            }
            //m_GearMuzzle = m_GearLeftHand.transform.Find("muzzle");
        }

        public void SetGearRightHand(GameObject gear)
        {
            m_GearRightHand = gear;
            m_GearRightHandPosition = m_GearRightHand.transform.Find("grip");
            m_GearMuzzle = m_GearRightHand.transform.Find("muzzle");
        }

        public void OnUpdate(float rotaionX)
        {
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
                    // Change parent and reset local position and rotation according with parent.
                    m_GearLeftHand.transform.SetParent(m_LeftHandIKPosition);
                    m_GearLeftHand.transform.localPosition = Vector3.zero;
                    m_GearLeftHand.transform.localRotation = Quaternion.identity;
                    break;
                case IKPositionType.HandRight:
                    m_RightHandIKWeight = 1f;
                    // Change parent and reset local position and rotation according with parent.
                    m_GearRightHand.transform.SetParent(m_RightHandIKPosition);
                    m_GearRightHand.transform.localPosition = Vector3.zero;
                    m_GearRightHand.transform.localRotation = Quaternion.identity;
                    break;
            }
        }

        public void DisableIK(IKPositionType positionType)
        {
            switch (positionType)
            {
                case IKPositionType.HandLeft:
                    m_LeftHandIKWeight = 0f;
                    // Change parent and reset local position and rotation according with parent.
                    m_GearLeftHand.transform.SetParent(m_HandLeft.transform);
                    m_GearLeftHand.transform.localPosition = Vector3.zero;
                    m_GearLeftHand.transform.localRotation = Quaternion.identity;
                    break;
                case IKPositionType.HandRight:
                    m_RightHandIKWeight = 0f;
                    // Change parent and reset local position and rotation according with parent.
                    m_GearRightHand.transform.SetParent(m_HandRight.transform);
                    m_GearRightHand.transform.localPosition = Vector3.zero;
                    m_GearRightHand.transform.localRotation = Quaternion.identity;
                    break;
            }
        }
    }
}
