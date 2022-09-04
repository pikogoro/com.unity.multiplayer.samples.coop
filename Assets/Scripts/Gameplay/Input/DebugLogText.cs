using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Unity.Multiplayer.Samples.BossRoom.Client
{
    public class DebugLogText : MonoBehaviour
    {
        static private DebugLogText s_Instance = null;

        static public void Log(string text)
        {
            if (s_Instance != null)
            {
                s_Instance.SetLogText(text);
            }
        }

        [SerializeField] private TextMeshProUGUI m_Text = null;

        void Awake()
        {
            s_Instance = this;
        }

        public void SetLogText(string text)
        {
            if (m_Text != null)
            {
                m_Text.text = text;
            }
        }
    }
}
