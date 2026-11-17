using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RealSteel.Dialogue
{
    public class DialoguePortrait : MonoBehaviour
    {
        [Serializable]
        public class PortraitEntry
        {
            public string speakerId;
            public string expression = "default";
            public Sprite sprite;
        }

        private const string DefaultExpression = "default";

        [Header("연결")]
        [SerializeField] private Image portraitImage;

        [Header("초상화 목록")]
        [SerializeField] private List<PortraitEntry> portraits = new List<PortraitEntry>();

        private void Awake()
        {
            if (portraitImage == null)
            {
                Debug.LogError("[DialoguePortrait] Portrait Image가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            portraitImage.preserveAspect = true;
            Hide();
        }

        public void Show(string speakerId, string expression)
        {
            if (!enabled) return;

            if (string.IsNullOrEmpty(speakerId))
            {
                Hide();
                return;
            }

            string wanted = string.IsNullOrEmpty(expression) ? DefaultExpression : expression;
            Sprite sprite = Find(speakerId, wanted);

            if (sprite == null && wanted != DefaultExpression)
            {
                Debug.LogWarning($"[DialoguePortrait] '{speakerId}'의 표정 '{wanted}'이 없어 default를 씁니다.", this);
                sprite = Find(speakerId, DefaultExpression);
            }

            if (sprite == null)
            {
                Hide();
                return;
            }

            portraitImage.sprite = sprite;
            portraitImage.enabled = true;
        }

        public void Hide()
        {
            if (portraitImage == null) return;
            portraitImage.enabled = false;
        }

        private Sprite Find(string speakerId, string expression)
        {
            foreach (PortraitEntry entry in portraits)
            {
                if (entry != null && entry.speakerId == speakerId && entry.expression == expression) return entry.sprite;
            }
            return null;
        }
    }
}
