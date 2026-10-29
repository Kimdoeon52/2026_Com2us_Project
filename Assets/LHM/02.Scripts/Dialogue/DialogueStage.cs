using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RealSteel.Dialogue
{
    public class DialogueStage : MonoBehaviour
    {
        [Serializable]
        public class BackgroundEntry
        {
            public string id;
            public Sprite sprite;
        }

        [Header("풀스크린")]
        [SerializeField] private CanvasGroup fullscreenLayer;
        [SerializeField] private Image fullscreenBackground;
        [SerializeField] private List<BackgroundEntry> backgrounds = new List<BackgroundEntry>();

        [Header("인터미션")]
        [SerializeField] private CanvasGroup intermissionLayer;
        [SerializeField] private DialogueDirector director;
        [SerializeField] private string intermissionBgmId = "bgm.intermission";
        [SerializeField, Min(1f)] private float intermissionZoom = 1.1f;
        [SerializeField, Min(0.01f)] private float intermissionZoomDuration = 2f;

        [Header("전환")]
        [SerializeField, Min(0.01f)] private float layerFadeDuration = 0.3f;

        private float fullscreenTarget;
        private float intermissionTarget;
        private string previousBgmId;
        private DialogueDisplay current = DialogueDisplay.Bottom;

        public DialogueDisplay Current => current;

        private void Awake()
        {
            PrepareLayer(fullscreenLayer);
            PrepareLayer(intermissionLayer);
        }

        private void Update()
        {
            float step = Time.unscaledDeltaTime / layerFadeDuration;
            MoveAlpha(fullscreenLayer, fullscreenTarget, step);
            MoveAlpha(intermissionLayer, intermissionTarget, step);
        }

        public void Enter(DialogueDisplay display, string backgroundId)
        {
            current = display;

            switch (display)
            {
                case DialogueDisplay.Fullscreen:
                    if (fullscreenLayer == null)
                    {
                        Debug.LogWarning("[DialogueStage] Fullscreen Layer가 연결되지 않아 하단창으로 표시합니다.", this);
                        break;
                    }
                    ShowBackground(backgroundId);
                    fullscreenTarget = 1f;
                    break;

                case DialogueDisplay.Intermission:
                    if (intermissionLayer == null)
                    {
                        Debug.LogWarning("[DialogueStage] Intermission Layer가 연결되지 않아 하단창으로 표시합니다.", this);
                    }
                    else
                    {
                        intermissionTarget = 1f;
                    }

                    if (director != null)
                    {
                        previousBgmId = director.CurrentBgmId;
                        if (!string.IsNullOrEmpty(intermissionBgmId)) director.PlayBgmById(intermissionBgmId);
                        director.ZoomTo(intermissionZoom, intermissionZoomDuration);
                    }
                    break;
            }
        }

        public void Exit()
        {
            if (current == DialogueDisplay.Intermission && director != null && !string.IsNullOrEmpty(intermissionBgmId))
            {
                director.PlayBgmById(previousBgmId);
            }

            fullscreenTarget = 0f;
            intermissionTarget = 0f;
            previousBgmId = null;
            current = DialogueDisplay.Bottom;
        }

        private void ShowBackground(string backgroundId)
        {
            if (fullscreenBackground == null) return;

            Sprite sprite = FindBackground(backgroundId);
            if (sprite == null && !string.IsNullOrEmpty(backgroundId))
            {
                Debug.LogWarning($"[DialogueStage] 배경 목록에 없는 배경 '{backgroundId}' — 검은 배경으로 표시합니다.", this);
            }

            fullscreenBackground.sprite = sprite;
            fullscreenBackground.color = sprite != null ? Color.white : Color.black;
        }

        private Sprite FindBackground(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (BackgroundEntry entry in backgrounds)
            {
                if (entry != null && entry.id == id) return entry.sprite;
            }
            return null;
        }

        private static void PrepareLayer(CanvasGroup layer)
        {
            if (layer == null) return;
            layer.alpha = 0f;
            layer.interactable = false;
            layer.blocksRaycasts = false;
        }

        private static void MoveAlpha(CanvasGroup layer, float target, float step)
        {
            if (layer == null || Mathf.Approximately(layer.alpha, target)) return;
            layer.alpha = Mathf.MoveTowards(layer.alpha, target, step);
        }
    }
}
