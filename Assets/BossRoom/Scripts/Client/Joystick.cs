using UnityEngine;
using UnityEngine.EventSystems;

namespace Unity.Multiplayer.Samples.BossRoom.Client
{
    public class Joystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public float Horizontal
        {
            get {
                float value = Input.GetAxis("Horizontal");
                return (value == 0) ? m_Input.x : value;
            }
        }
        public float Vertical
        {
            get {
                float value = Input.GetAxis("Vertical");
                return (value == 0) ? m_Input.y : value;
            }
        }

        [SerializeField] private RectTransform m_Background = null;
        [SerializeField] private RectTransform m_Handle = null;

        private Canvas m_Canvas;
        private Vector2 m_Input = Vector2.zero;

        protected virtual void Start()
        {
            m_Canvas = GetComponentInParent<Canvas>();
            m_Handle.anchoredPosition = Vector2.zero;
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 position = m_Background.position;
            Vector2 radius = m_Background.sizeDelta / 2;
            m_Input = (eventData.position - position) / (radius * m_Canvas.scaleFactor);
            if (m_Input.magnitude > 1)
            {
                m_Input = m_Input.normalized;
            }
            m_Handle.anchoredPosition = m_Input * radius;
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
            m_Input = Vector2.zero;
            m_Handle.anchoredPosition = Vector2.zero;
        }
    }
}
