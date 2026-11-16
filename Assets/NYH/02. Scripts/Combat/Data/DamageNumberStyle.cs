using TMPro;
using UnityEngine;

/// <summary>
/// 데미지 숫자 공용 모양 (2026-11-16 사용자: 글꼴·색·크기를 한 곳에서 — 플레이어·적·보스 숫자 전부).
///
/// 에셋 하나를 Resources 폴더에 "DamageNumberStyle" 이름으로 두면(지금: 04. SO/Combat/Resources/DamageNumberStyle)
/// 모든 DamageNumberEmitter가 자동으로 이걸 쓴다 — 자동으로 붙는 숫자(보스·촉수·적)도 포함. 아무 데도 연결할 필요 없음.
/// 특정 대상만 다르게: 그 대상의 DamageNumberEmitter.style 칸에 다른 스타일 에셋을 넣는다.
/// 순수 연출 값 — 판정·데미지 계산과 무관. 플레이 중에 바꿔도 다음에 뜨는 숫자부터 반영된다.
/// </summary>
[CreateAssetMenu(menuName = "NYH/Combat/Damage Number Style", fileName = "DamageNumberStyle")]
public class DamageNumberStyle : ScriptableObject
{
    public const string ResourcePath = "DamageNumberStyle";

    [Header("글꼴")]
    [Tooltip("TextMeshPro 폰트 에셋(TMP_FontAsset). 비우면 TMP 기본 폰트. .ttf는 Window > TextMeshPro > Font Asset Creator로 만들어 넣는다")]
    public TMP_FontAsset font;

    [Min(0.1f)] public float fontSize = 3f;

    [Tooltip("크리티컬일 때 글자 크기 배율")]
    [Min(1f)] public float criticalScale = 1.4f;

    public FontStyles fontStyle = FontStyles.Bold;

    [Header("테두리 — 배경 위에서도 읽히게")]
    [Range(0f, 1f)] public float outlineWidth = 0.25f;
    public Color outlineColor = Color.black;

    [Header("색")]
    [Tooltip("플레이어가 맞았을 때")]
    public Color takenColor = new Color(0.75f, 0.35f, 1f);

    [Tooltip("플레이어가 때렸을 때 (적·보스·촉수가 맞음)")]
    public Color dealtColor = new Color(1f, 0.6f, 0.1f);

    [Tooltip("회피(MISS)")]
    public Color missColor = new Color(0.8f, 0.8f, 0.8f);

    [Header("글자 — {0} 자리에 데미지 숫자")]
    [Tooltip("예: \"{0}\" → 12 / \"-{0}\" → -12 / \"{0:N0}\" → 1,234")]
    public string damageFormat = "{0}";

    public string missText = "MISS";

    [Tooltip("뜬 직후 크기 배율 (튀기). 1이면 튀기 없음")]
    [Min(1f)] public float popScale = 1.4f;

    private static DamageNumberStyle shared;
    private static bool sharedLoaded;

    /// <summary>Resources의 공용 스타일. 없으면 null (그러면 각 DamageNumberEmitter의 자기 값)</summary>
    public static DamageNumberStyle Shared
    {
        get
        {
            if (!sharedLoaded || shared == null)
            {
                shared = Resources.Load<DamageNumberStyle>(ResourcePath);
                sharedLoaded = true;
            }
            return shared;
        }
    }
}
