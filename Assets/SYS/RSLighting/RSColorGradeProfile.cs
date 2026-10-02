// RE:AL STEEL - 색감 프리셋 (시간대별 암부 · 명부 색)
//
// "몇 시엔 어떤 색감" 을 키(Key)로 적어 둔 에셋. 키 사이 시각은 자동으로 부드럽게 섞인다.
//   · 스테이지마다 다른 색감 → 이 에셋을 복제해서 바꾼 뒤 '색감' 컴포넌트에 넣는다
//   · 시각 추가 → 인스펙터의 '지금 시각에 키 추가' 버튼, 또는 목록의 + 버튼
//
// 들어 있는 값 (전부 URP 기본 후처리):
//   Split Toning               어두운 곳 · 밝은 곳을 서로 다른 색으로 물들임 (옥토패스 밤: 암부 보라 / 명부 주황)
//   Shadows Midtones Highlights 어두운 곳 · 중간 · 밝은 곳의 색과 밝기
//   White Balance              색온도 (차갑게 ↔ 따뜻하게) · 틴트 (초록 ↔ 자주)
//   Color Lookup (LUT)         포토샵에서 만든 색 보정표 (선택)
using System;
using System.Collections.Generic;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("색감 프리셋 (시간대별)", "시각마다 색감을 적어 둔 에셋. 키 사이 시각은 자동으로 섞인다.\n" +
        "· 스테이지마다 다른 색감이 필요하면 이 에셋을 복제(Ctrl+D)해서 고친다\n" +
        "· 회색(0.5) = 영향 없음. 색을 진하게 고를수록 세게 물든다")]
    [CreateAssetMenu(fileName = "RS_ColorGrade", menuName = "RE_AL STEEL/색감 프리셋 (시간대별)", order = 120)]
    public class RSColorGradeProfile : ScriptableObject
    {
        [Serializable]
        public class Key
        {
            [Tooltip("이름 (알아보기용)")]
            public string name = "새 시각";
            [Range(0f, 24f), Tooltip("이 색감이 가장 강한 시각 (0 ~ 24시)")]
            public float hour = 12f;

            [Header("어두운 곳 · 밝은 곳 물들이기 (Split Toning)")]
            [Tooltip("어두운 곳에 입힐 색. 회색(0.5) = 영향 없음. 밤엔 보라 · 파랑")]
            public Color shadowTint = Neutral;
            [Tooltip("밝은 곳에 입힐 색. 회색(0.5) = 영향 없음. 노을 · 등불엔 주황")]
            public Color highlightTint = Neutral;
            [Range(-100f, 100f), Tooltip("어느 쪽을 더 넓게 물들일지. - = 어두운 색이 넓게 / + = 밝은 색이 넓게")]
            public float balance = 0f;

            [Header("어두운 곳 · 중간 · 밝은 곳 (Shadows Midtones Highlights)")]
            [Tooltip("어두운 곳 색 (흰색 = 그대로)")]
            public Color shadows = Color.white;
            [Range(-1f, 1f), Tooltip("어두운 곳 밝기 (- 더 어둡게 / + 밝게)")]
            public float shadowsBrightness = 0f;
            [Tooltip("중간 밝기 색 (흰색 = 그대로)")]
            public Color midtones = Color.white;
            [Range(-1f, 1f), Tooltip("중간 밝기")]
            public float midtonesBrightness = 0f;
            [Tooltip("밝은 곳 색 (흰색 = 그대로)")]
            public Color highlights = Color.white;
            [Range(-1f, 1f), Tooltip("밝은 곳 밝기")]
            public float highlightsBrightness = 0f;

            [Header("색온도")]
            [Range(-100f, 100f), Tooltip("- 차갑게(파랑) / + 따뜻하게(주황)")]
            public float temperature = 0f;
            [Range(-100f, 100f), Tooltip("- 초록 / + 자주")]
            public float tint = 0f;

            [Header("LUT (선택)")]
            [Tooltip("포토샵에서 만든 색 보정표 (가로 1024 x 세로 32 띠 그림). 색감 인스펙터의 'LUT 작업 이미지' 버튼으로 만든다")]
            public Texture2D lut;
            [Range(0f, 1f), Tooltip("LUT 세기")]
            public float lutAmount = 1f;

            public Key Clone() { return (Key)MemberwiseClone(); }
        }

        public static readonly Color Neutral = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Tooltip("시각별 색감. 순서는 상관없다 (시각 순으로 알아서 정렬)")]
        public List<Key> keys = new List<Key>();

        /// <summary>인스펙터에서 값이 바뀌면 올라간다 (색감 컴포넌트가 다시 적용하는 신호)</summary>
        [NonSerialized] public int version;

        void OnValidate() { version++; }

        /// <summary>
        /// hour 시각의 색감을 계산해 into 에 담는다. 앞뒤 키를 찾아 부드럽게 섞는다 (24시 → 0시로 이어짐).
        /// LUT 는 섞을 수 없어서 앞뒤 키의 LUT · 섞는 비율을 따로 돌려준다.
        /// </summary>
        public bool Evaluate(float hour, Key into, out Key before, out Key after, out float t)
        {
            before = after = null; t = 0f;
            if (keys == null || keys.Count == 0 || into == null) return false;
            hour = Mathf.Repeat(hour, 24f);

            // 앞 = hour 이하 중 가장 늦은 키, 뒤 = hour 초과 중 가장 이른 키 (없으면 한 바퀴 돌아서)
            Key a = null, b = null;
            float da = float.MaxValue, db = float.MaxValue;
            foreach (var k in keys)
            {
                if (k == null) continue;
                float back = Mathf.Repeat(hour - k.hour, 24f);   // k 에서 hour 까지 지난 시간
                float fwd = Mathf.Repeat(k.hour - hour, 24f);    // hour 에서 k 까지 남은 시간
                if (back < da) { da = back; a = k; }
                if (fwd > 0f && fwd < db) { db = fwd; b = k; }
            }
            if (a == null) return false;
            if (b == null || b == a) { Copy(a, into); before = after = a; t = 0f; return true; }

            float span = da + db;
            t = span > 1e-4f ? da / span : 0f;
            t = t * t * (3f - 2f * t);
            Lerp(a, b, t, into);
            before = a; after = b;
            return true;
        }

        static void Copy(Key s, Key d)
        {
            d.shadowTint = s.shadowTint; d.highlightTint = s.highlightTint; d.balance = s.balance;
            d.shadows = s.shadows; d.shadowsBrightness = s.shadowsBrightness;
            d.midtones = s.midtones; d.midtonesBrightness = s.midtonesBrightness;
            d.highlights = s.highlights; d.highlightsBrightness = s.highlightsBrightness;
            d.temperature = s.temperature; d.tint = s.tint;
            d.lut = s.lut; d.lutAmount = s.lutAmount;
        }

        static void Lerp(Key a, Key b, float t, Key d)
        {
            d.shadowTint = Color.Lerp(a.shadowTint, b.shadowTint, t);
            d.highlightTint = Color.Lerp(a.highlightTint, b.highlightTint, t);
            d.balance = Mathf.Lerp(a.balance, b.balance, t);
            d.shadows = Color.Lerp(a.shadows, b.shadows, t);
            d.shadowsBrightness = Mathf.Lerp(a.shadowsBrightness, b.shadowsBrightness, t);
            d.midtones = Color.Lerp(a.midtones, b.midtones, t);
            d.midtonesBrightness = Mathf.Lerp(a.midtonesBrightness, b.midtonesBrightness, t);
            d.highlights = Color.Lerp(a.highlights, b.highlights, t);
            d.highlightsBrightness = Mathf.Lerp(a.highlightsBrightness, b.highlightsBrightness, t);
            d.temperature = Mathf.Lerp(a.temperature, b.temperature, t);
            d.tint = Mathf.Lerp(a.tint, b.tint, t);
            d.lut = t < 0.5f ? a.lut : b.lut;
            d.lutAmount = Mathf.Lerp(a.lutAmount, b.lutAmount, t);
        }

        // ─────────────────────────────────────────────────────────────
        // 기본값 (옥토패스 트래블러 레퍼런스 — 시간대 프리셋 6개와 같은 시각)
        // ─────────────────────────────────────────────────────────────

        public void FillOctopathDefaults()
        {
            keys = new List<Key>
            {
                K("새벽", 5.9f, C(0.40f, 0.44f, 0.64f), C(0.64f, 0.54f, 0.56f), -10f, -18f, 6f,
                  C(0.95f, 0.97f, 1.08f), -0.02f),
                K("아침", 8.0f, C(0.46f, 0.49f, 0.57f), C(0.62f, 0.55f, 0.44f), 0f, 6f, 0f,
                  Color.white, 0f),
                K("한낮", 12.5f, C(0.48f, 0.50f, 0.54f), C(0.54f, 0.52f, 0.47f), 0f, 2f, 0f,
                  Color.white, 0f),
                K("오후", 15.5f, C(0.46f, 0.48f, 0.56f), C(0.62f, 0.54f, 0.44f), 5f, 8f, 0f,
                  Color.white, 0f),
                K("노을", 18.4f, C(0.38f, 0.50f, 0.60f), C(0.74f, 0.52f, 0.36f), 12f, 25f, 6f,
                  C(0.97f, 0.97f, 1.05f), 0f),
                K("밤", 22.0f, C(0.44f, 0.38f, 0.64f), C(0.68f, 0.55f, 0.40f), -20f, -18f, 8f,
                  C(0.92f, 0.90f, 1.10f), -0.05f),
            };
            version++;
        }

        static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

        static Key K(string n, float h, Color sh, Color hi, float bal, float temp, float tint, Color shadows, float shB)
        {
            return new Key
            {
                name = n, hour = h, shadowTint = sh, highlightTint = hi, balance = bal,
                temperature = temp, tint = tint, shadows = shadows, shadowsBrightness = shB,
            };
        }
    }
}
