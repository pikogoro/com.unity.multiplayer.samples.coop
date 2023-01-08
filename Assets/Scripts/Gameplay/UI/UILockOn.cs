using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.BossRoom.Gameplay.UI
{
    /// <summary>
    /// </summary>
    public class UILockOn : MonoBehaviour
    {
        private Transform m_TargetTransform;
        public Transform TargetTransform
        {
            set { m_TargetTransform = value; }
            get { return m_TargetTransform; }
        }

        RectTransform m_UIStateRectTransform;

        [Tooltip("World space vertical offset for positioning.")]
        [SerializeField]
        float m_VerticalWorldOffset;

        [Tooltip("Screen space vertical offset for positioning.")]
        [SerializeField]
        float m_VerticalScreenOffset;

        Vector3 m_VerticalOffset;

        Vector3 m_WorldPos;

        private void Start()
        {
            m_UIStateRectTransform = GetComponent<RectTransform>();
        }

        void LateUpdate()
        {
            m_WorldPos.Set(m_TargetTransform.position.x,
                m_TargetTransform.position.y + m_VerticalWorldOffset,
                m_TargetTransform.position.z);

            m_UIStateRectTransform.position = Camera.main.WorldToScreenPoint(m_WorldPos) + m_VerticalOffset;
        }
    }
}
