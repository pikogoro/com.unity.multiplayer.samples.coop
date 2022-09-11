using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Multiplayer.Samples.BossRoom.Visual;

namespace Unity.Multiplayer.Samples.BossRoom.Client
{
    public class TrackingCanvasController : MonoBehaviour
    {
        [SerializeField] private Transform m_Target = null;
        [SerializeField] private float m_UpOffset = 0f;         // default is -0.5m.
        [SerializeField] private float m_ForwardOffset = 1f;    // default is 1m.

        void Start()
        {
            if (m_Target == null)
            {
                m_Target = Camera.main.transform;
            }
        }

        void LateUpdate()
        {
            if (m_Target != null)
            {
                transform.position = m_Target.position + m_Target.up * m_UpOffset + m_Target.forward * m_ForwardOffset;
                transform.LookAt(m_Target);
                transform.Rotate(0f, 180f, 0f);
            }
        }
    }
}
