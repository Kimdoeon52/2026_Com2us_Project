using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 맞을 때마다 머리 위에 데미지 숫자를 띄우는 연출 (View, 메이플스토리식). 판정과 무관하다.
///
/// ── 동작 (2026-10-11) ─────────────────────────────────────────────────────────────
///   · 이 로봇의 ActionExecutor.OnDamaged(근접·투사체·화염 다단히트 공통 통로)를 구독해서, 맞을 때마다 숫자 하나를 띄운다.
///   · 숫자는 위로 떠오르면서 서서히 투명해지다 사라진다.
///   · 짧은 시간 안에 연달아 맞으면(화염 다단히트 등) 숫자를 위로 한 칸씩 쌓아서 겹치지 않게 한다 — 메이플식 계단.
///     단 maxStackCount 칸까지만 쌓고 다시 맨 아래 칸으로 돌아온다 — 숫자가 뜨는 높이는 항상
///     spawnOffset ~ spawnOffset + (maxStackCount-1) × stackSpacing + riseDistance 범위 안이다(화면 밖으로 안 나감).
///   · 색: 이 로봇이 플레이어면 "내가 맞음"(보라), 아니면 "내가 때림"(주황). 회피는 "MISS", 크리티컬은 크게.
///   · 숫자 값은 KKH CombatDataHub가 계산한 HitResolutionResult.DamageToHp 그대로 — 여기서 계산하지 않는다.
///
/// ── 붙이는 곳 ───────────────────────────────────────────────────────────────────────
///   로봇 루트(ActionExecutor가 있는 오브젝트). 없으면 RobotView.EnsureEffectComponents가 자동으로 붙인다.
///   단 자동으로 붙은 건 플레이할 때마다 기본값으로 새로 생긴다 — 글자·색·위치를 바꿔서 유지하려면
///   Player·Enemy 오브젝트에 직접 Add Component로 붙여두고 거기서 값을 바꿀 것(이미 있으면 자동 부착은 건너뛴다).
///
/// ── 구현 메모 ─────────────────────────────────────────────────────────────────────
///   · 월드 공간 TextMeshPro(3D 텍스트)로 만든다 — 화면 UI 캔버스로 하면 카메라가 움직일 때마다 좌표 변환이 필요하다.
///     폰트는 TMP 기본 폰트(TMP Settings)를 쓴다. 픽셀 폰트로 바꾸려면 font 칸에 넣으면 된다.
///   · 떠오르기·투명해지기는 실제 시간(Time.deltaTime) — 순수 연출이라 틱 밖이다(§3). F2(정지) 중엔 같이 멈춘다.
///   · 숫자 오브젝트는 다 쓰면 꺼서 모아두고 재사용한다 — 화염처럼 초당 여러 번 맞으면 매번 만들고 지우는 게 아깝다.
/// </summary>
public class DamageNumberEmitter : MonoBehaviour
{
    [Header("위치·움직임 — 연출값")]
    [Tooltip("숫자가 처음 뜨는 위치(로봇 피벗 기준, 월드 단위). 보통 머리 위")]
    [SerializeField] private Vector2 spawnOffset = new Vector2(0f, 0.75f);

    [Tooltip("숫자가 떠 있는 시간(초)")]
    [Min(0.1f)] [SerializeField] private float lifetimeSeconds = 0.8f;

    [Tooltip("살아 있는 동안 위로 떠오르는 거리(월드 단위)")]
    [SerializeField] private float riseDistance = 0.35f;

    [Tooltip("연달아 맞았을 때 숫자를 위로 쌓는 간격(월드 단위)")]
    [SerializeField] private float stackSpacing = 0.14f;

    // 2026-10-11 수정: 예전엔 연달아 맞는 동안 끝없이 위로 쌓여서, 화염처럼 계속 맞으면 숫자가 화면 밖까지 올라갔다.
    // 이제 이 칸 수까지만 쌓고 다시 맨 아래 칸으로 돌아온다 → 숫자가 뜨는 높이가 항상 정해진 범위 안
    [Tooltip("연달아 맞을 때 쌓는 최대 칸 수. 다 차면 다시 맨 아래부터. 1이면 항상 같은 자리에서만 뜬다")]
    [Min(1)] [SerializeField] private int maxStackCount = 3;

    [Tooltip("이 시간(초) 안에 또 맞으면 '연달아 맞음'으로 보고 위로 쌓는다. 지나면 다시 처음 위치부터")]
    [Min(0f)] [SerializeField] private float stackWindowSeconds = 0.6f;

    [Header("글자 — {0} 자리에 데미지 숫자가 들어간다")]
    [Tooltip("데미지 표시 형식. 예: \"{0}\" → 12 / \"-{0}\" → -12 / \"{0}!\" → 12! / \"{0:N0}\" → 1,234")]
    [SerializeField] private string damageFormat = "{0}";

    [Tooltip("회피했을 때 띄울 글자")]
    [SerializeField] private string missText = "MISS";

    [Header("모양 — 연출값")]
    [Tooltip("비우면 TMP 기본 폰트")]
    [SerializeField] private TMP_FontAsset font;

    [Min(0.1f)] [SerializeField] private float fontSize = 3f;

    [Tooltip("크리티컬일 때 글자 크기 배율")]
    [Min(1f)] [SerializeField] private float criticalScale = 1.4f;

    [Tooltip("플레이어가 맞았을 때 색")]
    [SerializeField] private Color takenColor = new Color(0.75f, 0.35f, 1f);

    [Tooltip("적이 맞았을 때(= 플레이어가 때렸을 때) 색")]
    [SerializeField] private Color dealtColor = new Color(1f, 0.6f, 0.1f);

    [Tooltip("회피(MISS) 색")]
    [SerializeField] private Color missColor = new Color(0.8f, 0.8f, 0.8f);

    // 떠 있는 숫자 하나
    private class Popup
    {
        public TextMeshPro text;
        public Vector3 start;   // 처음 뜬 월드 위치 — 여기서 riseDistance만큼 올라간다
        public float age;       // 뜬 뒤 지난 시간(초)
        public Color color;
    }

    private ActionExecutor executor;
    private readonly List<Popup> active = new List<Popup>();
    private readonly Stack<TextMeshPro> pool = new Stack<TextMeshPro>();
    private int stackIndex;          // 지금 몇 번째로 쌓이는 중인지
    private float lastHitTime = -999f;

    private void Awake() => executor = GetComponent<ActionExecutor>();

    private void OnEnable()
    {
        if (executor != null) executor.OnDamaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (executor != null) executor.OnDamaged -= OnDamaged;
    }

    private void OnDestroy()
    {
        foreach (var p in active) if (p.text != null) Destroy(p.text.gameObject);
        foreach (var t in pool) if (t != null) Destroy(t.gameObject);
    }

    // 호출: ActionExecutor.OnDamaged (HitDetection이 판정을 끝낸 직후, 근접·투사체 공통)
    private void OnDamaged(HitResolutionResult result)
    {
        // 연달아 맞는 중이면 한 칸 위로, 아니면 처음 자리부터. 최대 칸 수를 넘으면 다시 맨 아래 칸으로 돌아온다(나머지 연산)
        // — 이게 없으면 계속 맞는 동안 끝없이 올라가 화면 밖으로 나간다
        stackIndex = Time.time - lastHitTime <= stackWindowSeconds ? (stackIndex + 1) % maxStackCount : 0;
        lastHitTime = Time.time;

        bool isPlayer = executor != null && executor.FighterId == "Player";
        string label;
        Color color;
        float size = fontSize;

        if (result.IsEvaded)
        {
            label = missText;
            color = missColor;
        }
        else
        {
            label = FormatDamage(result.DamageToHp);
            color = isPlayer ? takenColor : dealtColor;
            if (result.IsCritical) size *= criticalScale;
        }

        Vector3 start = transform.position + new Vector3(spawnOffset.x, spawnOffset.y + stackIndex * stackSpacing, 0f);
        Spawn(label, color, size, start);
    }

    // 형식 문자열을 잘못 넣어도(예: "{1}", 중괄호 짝 안 맞음) 게임이 멈추지 않게 숫자만 띄운다
    private string FormatDamage(int damage)
    {
        try { return string.Format(damageFormat, damage); }
        catch (System.FormatException) { return damage.ToString(); }
    }

    private void Spawn(string label, Color color, float size, Vector3 position)
    {
        TextMeshPro text = pool.Count > 0 ? pool.Pop() : CreateText();
        text.gameObject.SetActive(true);
        text.text = label;
        text.fontSize = size;
        text.color = color;
        text.transform.position = position;

        active.Add(new Popup { text = text, start = position, age = 0f, color = color });
    }

    private TextMeshPro CreateText()
    {
        // 로봇의 자식으로 두지 않는다 — 자식이면 로봇이 밀려날 때 숫자도 같이 끌려가고, 좌우 반전 때 글자가 뒤집힌다
        var go = new GameObject($"DamageNumber_{name}");
        var text = go.AddComponent<TextMeshPro>();
        if (font != null) text.font = font;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.outlineWidth = 0.25f;               // 배경 위에서도 읽히게 검은 테두리
        text.outlineColor = new Color32(0, 0, 0, 255);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        // 로봇·불·총알보다 항상 앞에 보이게. 같은 정렬 레이어에서 큰 값 = 앞
        text.sortingOrder = 100;
        return text;
    }

    // 호출: Unity 매 렌더 프레임. 위로 떠오르며(처음엔 빠르게, 끝엔 느리게) 뒤쪽 절반부터 투명해진다
    private void Update()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Popup p = active[i];
            p.age += Time.deltaTime;
            float t = p.age / lifetimeSeconds;

            if (t >= 1f)
            {
                p.text.gameObject.SetActive(false);
                pool.Push(p.text);
                active.RemoveAt(i);
                continue;
            }

            float eased = 1f - (1f - t) * (1f - t); // 처음엔 빠르게, 나중엔 천천히 떠오름
            p.text.transform.position = p.start + Vector3.up * (riseDistance * eased);

            Color c = p.color;
            c.a = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f; // 앞 절반은 또렷하게, 뒤 절반에 걸쳐 사라짐
            p.text.color = c;
        }
    }
}
