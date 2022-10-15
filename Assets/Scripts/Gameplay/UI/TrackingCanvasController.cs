using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using Unity.Multiplayer.Samples.BossRoom.Visual;
using UnityEngine.SceneManagement;

namespace Unity.Multiplayer.Samples.BossRoom.Client
{
    public class TrackingCanvasController : MonoBehaviour
    {
        //[SerializeField] private Transform m_Target = null;
        private Transform m_Target = null;
        [SerializeField] private float m_UpOffset = 0f;         // default is 0m.
        [SerializeField] private float m_ForwardOffset = 1f;    // default is 1m.

        string m_PreviousSceneName;

        void Start()
        {
            if (m_Target == null)
            {
                m_Target = Camera.main.transform;
            }

            m_PreviousSceneName = SceneManager.GetActiveScene().name;
        }

        void LateUpdate()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (m_PreviousSceneName != sceneName)
            {
                m_Target = Camera.main.transform;
                m_PreviousSceneName = sceneName;
            }

            if (m_Target == null)
            {
                Debug.LogError("Target camera is null.");
                return;
            }

            transform.position = m_Target.position + m_Target.up * m_UpOffset + m_Target.forward * m_ForwardOffset;
            transform.LookAt(m_Target);
            transform.Rotate(0f, 180f, 0f);
        }
    }
}
