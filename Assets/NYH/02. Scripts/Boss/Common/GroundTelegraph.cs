using UnityEngine;

/// <summary>
/// 바닥에 깔리는 예고 그림자 (2026-10-28, 쿄다이 "발 그림자 / 주먹 그림자 / 경기장 1/3을 덮는 그림자").
/// 그림 파일 없이 흰 1픽셀 스프라이트를 늘려서 쓴다. 숨겨진 채로 시작한다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class GroundTelegraph : MonoBehaviour
{
    [SerializeField] private Color color = new Color(0f, 0f, 0f, 0.45f);

    [Tooltip("그림자 두께(월드). 바닥에 깔리는 납작한 띠")]
    [Min(0.01f)] [SerializeField] private float thickness = 0.5f;

    [Tooltip("깜빡임 속도(초당 횟수). 0이면 안 깜빡임")]
    [Min(0f)] [SerializeField] private float pulseRate = 3f;

    private static Sprite whiteSprite;
    private SpriteRenderer spriteRenderer;
    private float shownAt;

    private void Awake()
    {
        EnsureSprite();
        spriteRenderer.enabled = false;
    }

    // 에디터 미리보기(쿄다이 패턴 미리보기 창)에서는 Awake가 안 돌아서 Show에서도 확인한다.
    // 흰 1픽셀 그림은 저장하지 않는다(HideAndDontSave) — 씬에 빈 참조가 남지 않게
    private void EnsureSprite()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (whiteSprite == null)
        {
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        }
        spriteRenderer.sprite = whiteSprite;
    }

    /// <summary>바닥 groundY 위에 centerX를 중심으로 width만큼 그림자를 편다</summary>
    public void Show(float centerX, float groundY, float width)
    {
        EnsureSprite();
        transform.position = new Vector3(centerX, groundY, transform.position.z);
        // 너비·두께는 월드 값 — 부모(보스 루트)가 줄어 있어도 판정과 같은 너비로 보이게 부모 크기로 나눈다 (2026-11-04)
        Vector3 ps = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(width / Mathf.Max(0.0001f, Mathf.Abs(ps.x)), thickness / Mathf.Max(0.0001f, Mathf.Abs(ps.y)), 1f);
        spriteRenderer.color = color;
        spriteRenderer.enabled = true;
        shownAt = Time.time;
    }

    /// <summary>켜진 그림자의 중심 x만 옮긴다 (플레이어를 따라가는 패턴)</summary>
    public void MoveTo(float centerX)
    {
        Vector3 p = transform.position;
        transform.position = new Vector3(centerX, p.y, p.z);
    }

    public void Hide()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.enabled = false;
    }

    private void Update()
    {
        if (!spriteRenderer.enabled || pulseRate <= 0f) return;
        float t = 0.5f + 0.5f * Mathf.Sin((Time.time - shownAt) * pulseRate * Mathf.PI * 2f);
        var c = color;
        c.a = color.a * Mathf.Lerp(0.6f, 1f, t);
        spriteRenderer.color = c;
    }
}
