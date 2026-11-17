// RE:AL STEEL - 나노봇 봇 색 (2D 알갱이)
//
// 봇 하나 = 판 위의 알갱이 하나 (칸 하나). 그림이 없으니 '봇 색' 목록이 곧 봇의 생김새다.
//   · 한 줄 = 색 + 비율. 비율은 아무 숫자나 — 합에 대한 % 로 나눈다
//   · 봇마다 색은 봇 번호로 정해져 끝까지 같다 (빨간 봇은 날아가도 · 모래가 돼도 빨간 봇)
//   · 그림 부위에서 '봇 색 = 그림 색' 을 켜면, 그림 칸에 도착한 봇이 그 칸 색으로 바뀐다
//   · 알갱이마다 밝기를 조금씩 달리해 모래 · 금속 가루 결이 생긴다 (grain)
//   · 목록이 비면 기본 금속 색 3단
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.NanoBoss
{
    [Serializable]
    public class NanoBotColor
    {
        public Color color = new Color32(84, 92, 112, 255);
        [Tooltip("비율. 아무 숫자나 — 합에 대한 % 로 나눈다. 0 = 안 씀")]
        [Min(0f)] public float ratio = 1f;
    }

    public static class RSNanoBotColors
    {
        static readonly Color32[] Default = { new Color32(84, 92, 112, 255), new Color32(66, 72, 90, 255), new Color32(110, 118, 140, 255) };
        static readonly float[] DefaultW = { 0.5f, 0.35f, 0.15f };

        static float U(int i, uint salt)
        {
            uint h = (uint)i * 2654435761u ^ (salt * 2246822519u + 0x9E3779B9u);
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }

        /// <summary>봇 번호 i 의 색 (목록 비율대로) + 알갱이 밝기 차이 (grain 0~0.3)</summary>
        public static Color32 Pick(List<NanoBotColor> list, int i, float grain)
        {
            Color32 c = Default[0];
            float u = U(i, 1);
            float sum = 0f;
            if (list != null) foreach (var e in list) if (e != null && e.ratio > 0f) sum += e.ratio;
            if (sum > 0f)
            {
                float t = u * sum;
                foreach (var e in list)
                {
                    if (e == null || e.ratio <= 0f) continue;
                    c = e.color;
                    t -= e.ratio;
                    if (t < 0f) break;
                }
            }
            else
            {
                float t = u;
                for (int k = 0; k < Default.Length; k++) { c = Default[k]; t -= DefaultW[k]; if (t < 0f) break; }
            }
            return Jitter(c, (U(i, 2) - 0.5f) * 2f * grain);
        }

        /// <summary>밝기를 k 만큼 (−1~1 배율) 바꾼다</summary>
        public static Color32 Jitter(Color32 c, float k)
        {
            float m = 1f + k;
            return new Color32((byte)Mathf.Clamp(c.r * m, 0, 255), (byte)Mathf.Clamp(c.g * m, 0, 255), (byte)Mathf.Clamp(c.b * m, 0, 255), 255);
        }

        public static float Share(List<NanoBotColor> list, int k)
        {
            float sum = 0f;
            foreach (var e in list) if (e != null && e.ratio > 0f) sum += e.ratio;
            return sum > 0f && list[k] != null ? Mathf.Max(0f, list[k].ratio) / sum : 0f;
        }
    }
}
