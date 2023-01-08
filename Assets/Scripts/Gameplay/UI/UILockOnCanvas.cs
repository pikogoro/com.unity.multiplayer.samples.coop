using System;
using UnityEngine;
using UnityEngine.Pool;

namespace Unity.BossRoom.Gameplay.UI
{
    /// <summary>
    /// </summary>
    public class UILockOnCanvas : MonoBehaviour
    {
        const int k_LockOnMax = 3;

        ObjectPool<GameObject> m_Pool;

        RectTransform m_CanvasTransform;

        [SerializeField]
        GameObject m_UILockOnPrefab;

        void Awake()
        {
            m_Pool = new ObjectPool<GameObject>(OnCreatePooledObject, OnGetFromPool, OnReleaseToPool, OnDestroyPooledObject);

            m_CanvasTransform = GetComponent<RectTransform>();
        }

        GameObject OnCreatePooledObject()
        {
            return Instantiate(m_UILockOnPrefab, m_CanvasTransform);
        }

        void OnGetFromPool(GameObject obj)
        {
            obj.SetActive(true);
        }

        void OnReleaseToPool(GameObject obj)
        {
            obj.SetActive(false);
        }

        void OnDestroyPooledObject(GameObject obj)
        {
            Destroy(obj);
        }

        public UILockOn GetUILoclOn()
        {
            if (k_LockOnMax <= m_Pool.CountActive)
            {
                return null;
            }

            return m_Pool.Get().GetComponent<UILockOn>();
        }

        public void ReleaseUILockOn(UILockOn ui)
        {
            m_Pool.Release(ui.gameObject);
        }
    }
}
