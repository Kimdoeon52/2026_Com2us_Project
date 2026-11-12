using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Cutscene
{
    [Serializable]
    public class CutsceneShot
    {
        public string title;
        public string subtitle;
        public string speaker;
        public string voice;
        public string text;
        public string pin;
        public float x;
        public float y;
        public float zoom;
        public float move;
        public float hold;

        public bool HasTitle => !string.IsNullOrEmpty(title);
        public bool HasText => !string.IsNullOrEmpty(text);
        public bool HasPin => !string.IsNullOrEmpty(pin);
        public bool HasFocus => zoom > 0f;
    }

    [Serializable]
    public class CutsceneData
    {
        public string id;
        public List<CutsceneShot> shots = new List<CutsceneShot>();

        public static bool TryParse(string json, out CutsceneData data, out string error)
        {
            data = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON이 비어 있습니다.";
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<CutsceneData>(json);
            }
            catch (ArgumentException exception)
            {
                error = $"JSON 형식 오류 — {exception.Message}";
                return false;
            }

            if (data == null || string.IsNullOrEmpty(data.id))
            {
                error = "id가 없습니다.";
                return false;
            }

            if (data.shots == null || data.shots.Count == 0)
            {
                error = $"'{data.id}'에 shots가 없습니다.";
                return false;
            }

            return true;
        }

        public List<string> Check()
        {
            var problems = new List<string>();
            for (int i = 0; i < shots.Count; i++)
            {
                CutsceneShot shot = shots[i];
                string at = $"shots[{i}]";

                if (shot == null)
                {
                    problems.Add($"{at}: 비어 있음");
                    continue;
                }

                if (!shot.HasTitle && !shot.HasText && !shot.HasFocus)
                {
                    problems.Add($"{at}: title, text, zoom이 모두 없어 아무것도 하지 않음");
                }

                bool usesPoint = shot.HasFocus || shot.HasPin;
                if (usesPoint && (shot.x < 0f || shot.x > 1f || shot.y < 0f || shot.y > 1f))
                {
                    problems.Add($"{at}: 지도 좌표 x={shot.x}, y={shot.y}가 0~1 밖 — 가장자리로 맞춤");
                }

                if (shot.HasTitle && shot.HasText)
                {
                    problems.Add($"{at}: title과 text가 함께 있음 — 타이틀 카드 뒤에 자막이 이어서 나옴");
                }
            }

            return problems;
        }
    }
}
