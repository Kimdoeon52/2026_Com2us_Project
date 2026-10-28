// RE:AL STEEL - 나노봇 그림 모양 (스프라이트 시트 → 봇 자리)
//
// 픽셀아트 스프라이트 시트를 넣으면 프레임마다 '칠한 픽셀' 을 찾아 봇 자리로 굽는다.
//   · 기본: 어두운 픽셀(검정)을 따라가고, 밝은 픽셀(흰색)과 투명한 곳은 무시한다 ('반대로' 를 켜면 밝은 픽셀)
//   · 픽셀 하나 = 봇 하나. 봇 수 = 가장 많이 칠한 프레임의 픽셀 수
//   · 프레임이 바뀌면 남아 있는 픽셀의 봇은 그대로, 새로 생긴 픽셀은 가까운 봇이 옮겨 간다 → 봇이 흘러가며 '사이' 를 메운다
// 굽기는 편집기(부위 인스펙터)가 한다. 잘라 둔 스프라이트(Sprite Mode = Multiple)가 있으면 그 순서, 없으면 가로 × 세로 칸.
using System;
using UnityEngine;

namespace RealSteel.NanoBoss
{
    public enum NanoSpritePivot
    {
        [InspectorName("가운데")] Center,
        [InspectorName("아래 가운데 (발밑)")] BottomCenter,
    }

    [CreateAssetMenu(menuName = "RE_AL STEEL/나노봇 그림 모양", fileName = "나노봇 그림")]
    public class RSNanoSpriteShape : ScriptableObject
    {
        [Tooltip("픽셀아트 스프라이트 시트 (PNG). 가져오기 설정과 상관없이 원본 픽셀을 읽는다")]
        public Texture2D sheet;
        [Tooltip("가로 칸 수 (잘라 둔 스프라이트가 있으면 무시)")]
        [Min(1)] public int columns = 1;
        [Tooltip("세로 칸 수 (잘라 둔 스프라이트가 있으면 무시). 위 줄부터 왼쪽 → 오른쪽으로 읽는다")]
        [Min(1)] public int rows = 1;
        [Tooltip("쓸 프레임 수 (0 = 전부)")]
        [Min(0)] public int frameLimit;
        [Tooltip("이보다 어두운 픽셀을 따라간다 (0 검정 ~ 1 흰색)")]
        [Range(0.05f, 1f)] public float darkness = 0.5f;
        [Tooltip("반대로 — 밝은 픽셀을 따라간다")]
        public bool invert;
        [Tooltip("알파가 이보다 낮은 픽셀은 무시")]
        [Range(0f, 1f)] public float alphaCut = 0.5f;
        [Tooltip("그림의 어느 점을 부위 원점에 맞출까 (잘라 둔 스프라이트는 각 스프라이트의 피벗)")]
        public NanoSpritePivot pivot = NanoSpritePivot.Center;

        // ── 구운 값 (편집기가 채운다) ─────────────────────────────────
        [HideInInspector] public int frameCount, botCount;
        [HideInInspector] public int maxX, maxY, minX, minY;          // 피벗 기준 픽셀 범위 (기즈모)
        [HideInInspector] public int[] pixels = Array.Empty<int>();   // 프레임 × 봇: (x+2048) | (y+2048)<<12 | 겹침 순번<<24
        [HideInInspector] public int[] filled = Array.Empty<int>();   // 프레임마다 칠한 픽셀 수
        [HideInInspector] public string bakedKey;

        public bool Ready => frameCount > 0 && botCount > 0 && pixels != null && pixels.Length == frameCount * botCount;

        public static int Pack(int x, int y, int rank) => ((x + 2048) & 0xFFF) | (((y + 2048) & 0xFFF) << 12) | (Mathf.Min(rank, 127) << 24);

        /// <summary>프레임 f 에서 봇 k 의 자리 (부위 좌표, m). 같은 픽셀에 여럿이면 살짝 흩는다</summary>
        public Vector3 Local(int f, int k, float pixelSize)
        {
            if (!Ready) return Vector3.zero;
            f = Mathf.Clamp(f, 0, frameCount - 1);
            k = Mathf.Clamp(k, 0, botCount - 1);
            int v = pixels[f * botCount + k];
            float x = (v & 0xFFF) - 2048 + 0.5f;
            float y = ((v >> 12) & 0xFFF) - 2048 + 0.5f;
            int rank = (v >> 24) & 0x7F;
            if (rank > 0)
            {
                float a = rank * 2.39996f + k * 0.7f;
                x += Mathf.Cos(a) * 0.32f;
                y += Mathf.Sin(a) * 0.32f;
            }
            float z = ((k * 0.618034f) % 1f - 0.5f) * 0.04f;
            return new Vector3(x * pixelSize, y * pixelSize, z);
        }
    }
}
