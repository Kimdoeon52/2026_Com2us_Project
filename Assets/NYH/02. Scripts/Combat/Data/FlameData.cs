using UnityEngine;

/// <summary>
/// 화염(불줄기) 연출 한 종류의 모양을 담는 데이터 (1층 · 데이터). FlameEmitter가 이것만 읽고 그린다.
///
/// 2026-10-11 추가 — 기획서1011 §6-10 "화염 방사기 패턴". 그림 없이 코드로 불을 그리는 둠(1993) 불꽃
/// 알고리즘을 옆으로 눕혀서 쓴다: 칸마다 0~36 세기를 들고, 매 스텝 왼쪽 칸 값을 조금 깎아 오른쪽으로 넘긴다.
/// 알고리즘 자체의 설명(그림 포함)은 FlameEmitter 클래스 주석에 있다. 여기는 "어떤 값을 바꾸면 불이 어떻게 변하나"만 다룬다.
///
/// ── 연결 방법 ─────────────────────────────────────────────────────────────────────
///   ActionData.flame 칸에 이 에셋을 넣으면, 그 행동의 활성 구간 동안 총구(ActionData.projectileMuzzle)에서 불이 나간다.
///   불을 그리는 FlameEmitter는 로봇에 없으면 RobotView가 자동으로 붙인다.
///
/// ── 판정과의 관계 (§4) ────────────────────────────────────────────────────────────
///   순수 연출이다 — 데미지 판정은 이 불 모양이 아니라 ActionData의 Hit박스 + hitIntervalFrames로 한다.
///   불길 길이는 매 순간 흔들리므로, length를 Hit박스 폭과 같은 숫자로 맞춰서 "보이는 것과 맞는 것"이 크게
///   어긋나지 않게만 한다. ActionDataEditor로 화염 행동을 선택하면 Scene 뷰에 length만큼 주황 점선이 그어진다.
///
/// ── 단위 ──────────────────────────────────────────────────────────────────────────
///   크기 값(length/thickness/nozzleThickness)은 월드 단위 = Hit박스 좌표와 같은 단위다.
///   칸(도트) 크기는 FlameEmitter가 로봇 스프라이트의 픽셀 크기 × pixelScale로 계산한다 —
///   그래서 로봇 PPU·스케일이 바뀌어도 불 도트와 로봇 도트 크기가 항상 맞는다.
///
/// ── 자주 쓰는 조정 ────────────────────────────────────────────────────────────────
///   · 불이 짧다/길다            → length (Hit박스 폭과 같이 바꿀 것)
///   · 위로 너무 휜다            → riseChance ↓ (0.15 → 0.08)
///   · 위아래로 더 퍼지게         → thickness ↑ + spreadChance ↑
///   · 천장에 막힌 듯 일자로 잘림  → edgeFadeStart ↓ (0.4 → 0.25)
///   · 너무 빽빽해서 덩어리 같다   → spreadChance ↓ 또는 edgeCooling ↑
///   · 도트가 너무 잘다/굵다       → pixelScale (1 = 로봇과 같은 해상도)
///   · 너무 빨리 일렁여 산만하다   → stepIntervalTicks ↑ (2 → 3)
///   · 불이 어둡게 보인다          → materialOverride에 Unlit 스프라이트 머티리얼
/// 전부 플레이 중에 바꿔도 다음 발사부터 바로 반영된다(FlameEmitter가 불을 켤 때마다 다시 읽는다).
/// </summary>
[CreateAssetMenu(menuName = "NYH/Combat/Flame Data", fileName = "NewFlameData")]
public class FlameData : ScriptableObject
{
    [Header("크기 — 월드 단위 (Hit박스 좌표와 같은 단위)")]
    // 이 거리에서 세기가 평균적으로 0이 되도록 FlameEmitter가 "한 칸당 식는 양"을 역산한다(36 ÷ 칸 수).
    // 평균이라서 실제 불끝은 매 순간 이보다 조금 짧거나 길게 흔들린다(대략 ±20%)
    [Tooltip("불길이 평균적으로 닿는 거리. Hit박스 폭과 비슷하게 맞춘다")]
    [Min(0.05f)] [SerializeField] private float length = 1.0f;

    // 텍스처의 세로 크기다. 불이 이보다 넓게 퍼질 수는 없다 — 위아래 가장자리 처리는 edgeFadeStart가 맡는다
    [Tooltip("불줄기가 퍼질 수 있는 최대 세로 폭(텍스처 높이). 총구 중심에서 위아래로 반씩")]
    [Min(0.05f)] [SerializeField] private float thickness = 0.35f;

    // 총구 열에서 불씨(세기 36)로 채우는 줄 수. thickness보다 훨씬 작게 둬야 "가늘게 나와서 퍼지는" 원뿔이 된다
    [Tooltip("총구에서 불이 나오는 세로 폭. 작을수록 가늘게 뿜어져 나와서 앞으로 갈수록 퍼진다")]
    [Min(0.01f)] [SerializeField] private float nozzleThickness = 0.08f;

    [Header("모양")]
    // 칸 크기 = 로봇 픽셀 크기 × 이 값. 칸이 커지면 칸 수가 줄어 계산도 가벼워진다(2 → 칸 수 1/4)
    [Tooltip("불 한 칸 = 로봇 스프라이트 픽셀 몇 개. 1이면 로봇과 같은 해상도, 2면 두 배로 굵은 도트")]
    [Min(1)] [SerializeField] private int pixelScale = 2;

    // 칸마다 독립적으로 굴리는 확률이라 앞으로 갈수록 누적된다 — 0.15면 50칸 가는 동안 평균 7~8칸 위로 올라간다.
    // 너무 크면 불이 위쪽 가장자리로 몰려 edgeFadeStart에 많이 먹힌다
    [Tooltip("한 칸 앞으로 갈 때 위로 한 칸 올라갈 확률. 0이면 수평, 클수록 끝이 위로 피어오른다")]
    [Range(0f, 1f)] [SerializeField] private float riseChance = 0.15f;

    // 원작 둠과 가장 다른 부분. 원작은 한 칸이 한 칸에만 값을 넘겨서, 총구가 좁은 우리 불은 구멍투성이 불티가 됐다.
    // 위·아래 이웃에도 넘겨야 빽빽한 원뿔이 된다(위·아래 각각 따로 굴림 — 0.5면 평균 1칸 더 번짐)
    [Tooltip("한 칸 앞으로 갈 때 위·아래 이웃 칸으로도 번질 확률(각각). 클수록 원뿔이 넓고 빽빽해진다")]
    [Range(0f, 1f)] [SerializeField] private float spreadChance = 0.5f;

    // 이웃으로 번진 불은 정면보다 1~이 값만큼 더 식는다 → 가운데(정면 줄)가 가장 뜨겁고 바깥이 붉다.
    // 1이면 거의 안 식어서 두툼한 덩어리, 8 이상이면 가는 심지만 남는다
    [Tooltip("이웃 칸으로 번질 때 추가로 식는 최대값(1~이 값 사이 무작위). 클수록 가장자리가 빨리 어두워져 심지가 또렷해진다")]
    [Min(1)] [SerializeField] private int edgeCooling = 4;

    // 2026-10-11 추가: 텍스처 위·아래 끝에서 불이 일자로 잘리던 문제(천장에 막힌 듯 보임) 해결용.
    // 동전 던지기 방식 — 가운데에서 이 비율만큼 벗어난 칸부터 "불이 안 붙을(뒷면)" 확률이 끝으로 갈수록 0 → 1로 서서히 오른다.
    // 예) 0.4: 가운데 40%까진 그대로 / 70% 지점에서 절반 / 끝 줄에선 전부 꺼짐 → 경계가 들쭉날쭉 성겨지며 사라진다
    [Tooltip("위·아래 가장자리에서 불이 서서히 꺼지기 시작하는 지점(가운데=0, 끝=1). 작을수록 일찍부터 성겨진다. 1이면 끄기(일자로 잘림)")]
    [Range(0f, 1f)] [SerializeField] private float edgeFadeStart = 0.4f;

    // 불 계산(+텍스처 갱신) 간격. 전투 틱(1/60초) 기준이라 주사율과 무관하다.
    // 간격이 바뀌어도 불이 뻗는 속도는 FlameEmitter.flowSpeed로 일정하게 유지된다(스텝당 전진 칸 수가 대신 늘어남)
    [Tooltip("불 계산을 몇 전투 틱마다 한 번 할지. 1 = 60fps(빠르게 일렁임), 2 = 30fps(원작 둠 느낌에 가까움)")]
    [Min(1)] [SerializeField] private int stepIntervalTicks = 2;

    [Header("색 — 왼쪽(꺼짐, 투명) → 오른쪽(가장 뜨거움)")]
    // 세기 i(0~36)를 그라데이션의 i/36 지점 색으로 칠한다. 왼쪽 끝 알파가 0이 아니면 불 없는 칸까지 네모로 칠해진다.
    // 그래픽(심예성) 쪽 색감에 맞출 때는 이 그라데이션만 바꾸면 된다 — 코드 수정 없음
    [Tooltip("세기 0~36을 이 그라데이션으로 색칠한다. 0번 칸은 알파 0이어야 배경이 비친다")]
    [SerializeField] private Gradient palette = DefaultPalette();

    // 로봇 머티리얼이 조명을 받는 종류(Lit)면 불도 같이 어두워진다. 불은 스스로 빛나야 자연스러우니 Unlit을 넣는 걸 권장
    [Tooltip("비우면 로봇과 같은 머티리얼을 쓴다. 조명에 어두워지면 Unlit 스프라이트 머티리얼을 넣는다")]
    [SerializeField] private Material materialOverride;

    // ---- 읽기 전용 접근자 — FlameEmitter는 읽기만 한다(SO에 런타임 상태를 쓰지 않는다, §2) ----
    public float Length => length;
    public float Thickness => thickness;
    public float NozzleThickness => nozzleThickness;
    public int PixelScale => pixelScale;
    public float RiseChance => riseChance;
    public float SpreadChance => spreadChance;
    public int EdgeCooling => edgeCooling;
    public float EdgeFadeStart => edgeFadeStart;
    public int StepIntervalTicks => stepIntervalTicks;
    public Gradient Palette => palette;
    public Material MaterialOverride => materialOverride;

    // 둠 불꽃 팔레트(검정 → 진빨강 → 주황 → 노랑 → 흰색)를 흉내 낸 기본값. 새 에셋을 만들면 이 값으로 시작한다.
    // 알파: 0%(꺼짐)는 투명, 8% 근처 반투명, 30% 이상 불투명 → 다 식어가는 불끝이 연기처럼 흐려진다
    private static Gradient DefaultPalette()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.12f, 0.03f, 0.03f), 0f),    // 거의 꺼짐 — 검붉은 연기
                new GradientColorKey(new Color(0.55f, 0.08f, 0.04f), 0.25f), // 진빨강
                new GradientColorKey(new Color(0.85f, 0.30f, 0.05f), 0.5f),  // 주황빨강
                new GradientColorKey(new Color(1.00f, 0.65f, 0.10f), 0.75f), // 주황노랑
                new GradientColorKey(new Color(1.00f, 0.95f, 0.70f), 1f),    // 총구 쪽 가장 뜨거운 흰노랑
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.6f, 0.08f),
                new GradientAlphaKey(1f, 0.3f),
                new GradientAlphaKey(1f, 1f),
            });
        return gradient;
    }
}
