using System;
using UnityEngine;

/// <summary>
/// 마에스트로 키 포즈 (2026-11-25 사용자 결정 B-2 — 모션 시트의 키 포즈를 그대로 쓴다).
/// 퍼펫은 정면 그림 한 장이라 몸을 비틀거나 쭉 뻗는 자세를 못 만든다 → 시트의 키 포즈 그림을 픽셀(=봇) 자리로 구워 두고,
/// 봇들이 퍼펫 자리 ↔ 포즈 자리, 포즈 ↔ 포즈 사이를 흘러가며 바뀐다.
///
/// 가상 봇 = 그림 봇 N개(퍼펫 픽셀 0..N-1) + 추가 봇(N..V-1). 포즈 그림은 퍼펫보다 픽셀이 많아서(팔·옷자락을 넓게 펼침)
/// 모자란 만큼 남는 봇을 잠깐 빌려 채운다. 추가 봇 e는 퍼펫 픽셀 extraSource[e]에서 나와 그 자리로 돌아간다(평소엔 안 보임).
/// 픽셀 대응은 빌더가 공간 순서를 지키게(가까운 픽셀끼리 가까운 자리로) 정해 두어 봇이 엉키지 않고 덩어리째 흘러간다.
/// 색은 시트 색을 쓰지 않는다 — 포즈 그림의 밝기 순서대로 퍼펫 그림의 색을 다시 칠한다(사용자: 색은 말고 모션만).
/// 좌표 = 퍼펫 그림 픽셀과 같은 공간(위쪽 0, 발밑 x = MaestroPuppetData.feetXPx).
/// MaestroPoseBaker(에디터)가 `03. Image/Boss/Maestro/Poses/*.png`를 읽어 만든다.
/// </summary>
public class MaestroPoseSet : ScriptableObject
{
    [Serializable]
    public class Pose
    {
        public string name;
        [Tooltip("이 포즈 그림의 베기 방향 (-1 = 오른쪽→왼쪽, +1 = 왼쪽→오른쪽, 0 = 방향 없음). 다른 방향으로 쓰면 좌우를 뒤집는다")]
        public int dir;
        [Tooltip("포즈 그림에 실제로 있던 픽셀 수 (나머지는 겹친 봇)")]
        public int sourcePixels;
        [HideInInspector] public Vector2[] pos = Array.Empty<Vector2>();   // 가상 봇마다 자리 (그림 픽셀, 위쪽 0)
        [HideInInspector] public Color32[] col = Array.Empty<Color32>();   // 가상 봇마다 색 (퍼펫 팔레트)
    }

    [Tooltip("그림 봇 수 (퍼펫 픽셀 수 — 다르면 다시 구워야 함)")]
    public int baseCount;
    [Tooltip("가상 봇 수 = 그림 봇 + 추가 봇")]
    public int virtualCount;
    [Tooltip("좌우를 뒤집을 때 기준 x (그림 픽셀 — 가슴 가운데)")]
    public float mirrorXPx = 53f;
    [HideInInspector] public int[] extraSource = Array.Empty<int>();   // 추가 봇 → 나오는 퍼펫 픽셀
    public Pose[] poses = Array.Empty<Pose>();

    [Header("베기 순서 (포즈 이름) — n번째 베기 = 준비/전환 포즈로 감았다가 타격 포즈로")]
    public string[] slashPrep = { "베기_준비", "베기_전환1", "베기_전환2" };
    public string[] slashHit = { "베기_1타", "베기_2타", "베기_3타" };

    public int ExtraCount => Mathf.Max(0, virtualCount - baseCount);

    public int IndexOf(string poseName)
    {
        if (poses == null) return -1;
        for (int i = 0; i < poses.Length; i++) if (poses[i] != null && poses[i].name == poseName) return i;
        return -1;
    }

    /// <summary>n번째 베기의 준비 포즈 · 타격 포즈 (목록보다 많이 베면 첫 번째를 뺀 나머지를 돌려 쓴다)</summary>
    public int SlashPose(int hit, bool strike)
    {
        var list = strike ? slashHit : slashPrep;
        if (list == null || list.Length == 0) return -1;
        int k = hit < list.Length || list.Length == 1 ? Mathf.Min(hit, list.Length - 1) : 1 + (hit - list.Length) % (list.Length - 1);
        return IndexOf(list[k]);
    }

    public bool Valid(int pixelCount) => poses != null && poses.Length > 0 && baseCount == pixelCount && virtualCount >= baseCount;
}
