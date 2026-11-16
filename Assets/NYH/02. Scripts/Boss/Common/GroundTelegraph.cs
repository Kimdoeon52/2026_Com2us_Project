using UnityEngine;

/// <summary>
/// 바닥에 깔리는 예고 그림자 (2026-10-28, 쿄다이 "발 그림자 / 주먹 그림자 / 경기장 1/3을 덮는 그림자"). 숨겨진 채로 시작한다.
/// 2026-11-16 그림자 시스템: 흰 1픽셀을 늘린 직사각형(반듯해서 부자연스러움) → ShadowSprites.Band(양 끝이 디더링으로 흐려지는 띠)를
/// 9분할(Sliced)로 늘린다. 너비를 아무리 늘려도 끝의 흐려지는 길이는 edgeSoftness로 같다. 판정 너비는 띠 전체(흐린 끝 포함).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class GroundTelegraph : MonoBehaviour
{
    [SerializeField] private Color color = new Color(0f, 0f, 0f, 0.45f);

    [Tooltip("그림자 두께(월드). 바닥에 깔리는 납작한 띠")]
    [Min(0.01f)] [SerializeField] private float thickness = 0.5f;

    [Tooltip("양 끝이 흐려지는 길이(월드). 띠가 이보다 좁으면 줄어든다. 임시값")]
    [Min(0.01f)] [SerializeField] private float edgeSoftness = 0.35f;

    [Tooltip("깜빡임 속도(초당 횟수). 0이면 안 깜빡임")]
    [Min(0f)] [SerializeField] private float pulseRate = 3f;

    private SpriteRenderer spriteRenderer;
    private float shownAt;

    private void Awake()
    {
        EnsureSprite();
        spriteRenderer.enabled = false;
    }

    // 에디터 미리보기(쿄다이 패턴 미리보기 창)에서는 Awake가 안 돌아서 Show에서도 확인한다.
    // 띠 그림은 코드가 그린다(저장 안 됨 — 씬에 빈 참조가 남지 않게)
    private void EnsureSprite()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer.sprite != ShadowSprites.Band) spriteRenderer.sprite = ShadowSprites.Band;
        spriteRenderer.drawMode = SpriteDrawMode.Sliced;
    }

    /// <summary>바닥 groundY 위에 centerX를 중심으로 width만큼 그림자를 편다</summary>
    public void Show(float centerX, float groundY, float width)
    {
        EnsureSprite();
        transform.position = new Vector3(centerX, groundY, transform.position.z);
        // 너비·두께는 월드 값 — 부모(보스 루트)가 줄어 있어도 판정과 같은 너비로 보이게 부모 크기로 나눈다 (2026-11-04)
        Vector3 ps = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float px = Mathf.Max(0.0001f, Mathf.Abs(ps.x)), py = Mathf.Max(0.0001f, Mathf.Abs(ps.y));
        // 9분할: 끝 칸은 크기 배율만 따른다 → 배율 s로 "끝 칸 = edgeSoftness(월드)"를 맞추고, 나머지 길이는 그림 크기(size)로 늘린다.
        // 띠가 끝 둘보다 좁으면 끝을 줄인다
        float border = ShadowSprites.BandBorderPixels; // PPU 1 — 로컬 단위 그대로
        float soft = Mathf.Min(edgeSoftness, width * 0.5f);
        float s = Mathf.Max(0.0001f, soft / (border * px));
        transform.localScale = new Vector3(s, s, 1f);
        spriteRenderer.size = new Vector2(Mathf.Max(border * 2f, width / (px * s)), thickness / (py * s));
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
