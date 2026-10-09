using System;
using TMPro;
using UnityEngine;

namespace RealSteel.Dialogue.Unity
{
    /// <summary>관중 머리 위 월드 스페이스 말풍선 1개. 풀링되어 재사용된다.</summary>
    public sealed class CrowdBubble : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.2f, 0f);
        [SerializeField] private float fadeSec = 0.2f;

        private Transform _anchor;
        private float _born, _life;
        private Action<CrowdBubble> _release;

        public Transform Anchor => _anchor;

        public void Show(Transform anchor, string text, float lifeSec, Action<CrowdBubble> release)
        {
            _anchor = anchor;
            _life = lifeSec;
            _born = Time.time;
            _release = release;
            label.text = text;
            transform.position = anchor.position + offset;
            gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (_anchor == null) { Finish(); return; }
            transform.position = _anchor.position + offset;

            float t = Time.time - _born;
            if (t >= _life) { Finish(); return; }
            group.alpha = t < fadeSec ? t / fadeSec : (t > _life - fadeSec ? (_life - t) / fadeSec : 1f);
        }

        private void Finish()
        {
            var r = _release;
            _release = null;
            _anchor = null;
            gameObject.SetActive(false);
            r?.Invoke(this);
        }
    }
}
