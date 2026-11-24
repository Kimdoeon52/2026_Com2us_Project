using System;
using UnityEngine;

/// <summary>
/// 마에스트로 퍼펫 데이터 (2026-11-24) — 참고 그림 픽셀 하나 = 봇 하나, 픽셀마다 붙은 뼈(또는 천).
/// MaestroLookBuilder가 `03. Image/Boss/Maestro/마에스트로_전체.png`(원본 시트에서 칸 단위로 뽑은 93×150px)를 읽어 규칙으로 나눠 채운다.
/// 좌표는 그림 픽셀, 위쪽이 0 (그림판 좌표). MaestroLook이 미터·월드로 바꾼다.
/// 뼈·천의 느낌 값(스프링·바람 등)은 여기서 조절 — 다시 빌드해도 이 에셋 값은 유지된다(픽셀 목록만 갱신).
/// </summary>
[CreateAssetMenu(fileName = "Maestro_PuppetData", menuName = "NYH/Boss/Maestro Puppet Data")]
public class MaestroPuppetData : ScriptableObject
{
    public enum PixelKind : byte { Normal, Drip, Sparkle, Glow }

    [Serializable]
    public class Bone
    {
        public string name;
        [Tooltip("부모 뼈 번호 (-1 = 발밑 원점). 부모가 항상 앞 번호")]
        public int parent = -1;
        [Tooltip("관절 자리 (그림 픽셀, 위쪽 0)")]
        public Vector2 pivotPx;
        [Header("스프링 (2차 동역학)")]
        [Tooltip("진동수 (Hz) — 클수록 빨리 따라간다")]
        [Min(0.1f)] public float frequency = 2f;
        [Tooltip("감쇠 — 1 미만이면 살짝 넘쳤다 돌아온다 (무게감), 1 이상이면 넘치지 않음")]
        [Min(0.05f)] public float damping = 0.55f;
        [Tooltip("반응 — 0 = 천천히 출발, 1 = 즉시, 1 초과 = 예비 동작처럼 반대로 살짝 갔다 옴")]
        public float response = 0.4f;
        [Tooltip("부모가 빨리 돌 때 반대로 늦게 따라오는 정도 (팔로스루, 초)")]
        [Min(0f)] public float followThrough;
    }

    [Serializable]
    public class Cloth
    {
        public string name;
        [Tooltip("붙은 뼈 번호 (-1 = 발밑 원점)")]
        public int parentBone = -1;
        [Tooltip("천 뼈대 선 — 뿌리(붙는 곳)부터 끝까지 (그림 픽셀, 위쪽 0)")]
        public Vector2[] spinePx = Array.Empty<Vector2>();
        [Header("느낌")]
        [Tooltip("원래 모양으로 돌아가려는 힘 — 뿌리 쪽 (1/s²)")]
        [Min(0f)] public float stiffnessRoot = 90f;
        [Tooltip("원래 모양으로 돌아가려는 힘 — 끝 쪽. 작을수록 끝이 자유롭게 펄럭인다")]
        [Min(0f)] public float stiffnessTip = 8f;
        [Tooltip("중력 (m/s²) — 천이 아래로 처지는 정도")]
        public float gravity = 1.5f;
        [Tooltip("바람 세기 (m/s²) — 가만히 있어도 펄럭임")]
        [Min(0f)] public float wind = 2.5f;
        [Tooltip("바람 빠르기")]
        [Min(0f)] public float windSpeed = 0.9f;
        [Tooltip("공기 저항 (0~1, 프레임마다 속도를 깎는 비율)")]
        [Range(0f, 0.5f)] public float drag = 0.06f;
    }

    [Header("그림")]
    public float artWidthPx = 93f;
    public float artHeightPx = 150f;
    [Tooltip("발밑 가운데 x (그림 픽셀)")]
    public float feetXPx = 46f;

    [Header("뼈 (부모가 앞 번호)")]
    public Bone[] bones = Array.Empty<Bone>();

    [Header("천 (끈·소매·자락)")]
    public Cloth[] cloths = Array.Empty<Cloth>();

    // ── 픽셀 (빌더가 채움) ──
    [HideInInspector] public Vector2[] pixPos = Array.Empty<Vector2>();     // 그림 픽셀 (위쪽 0)
    [HideInInspector] public Color32[] pixColor = Array.Empty<Color32>();
    [HideInInspector] public short[] pixOwner = Array.Empty<short>();       // ≥0 뼈 번호, -1 = 발밑 원점(하체), ≤ -2 = 천 (-2 - 천 번호)
    [HideInInspector] public byte[] pixKind = Array.Empty<byte>();

    public int PixelCount => pixPos != null ? pixPos.Length : 0;

    public int BoneIndex(string boneName)
    {
        for (int i = 0; i < bones.Length; i++) if (bones[i].name == boneName) return i;
        return -1;
    }
}
