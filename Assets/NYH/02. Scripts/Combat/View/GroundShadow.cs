using UnityEngine;

/// <summary>
/// 바닥 그림자 하나 (2026-11-16 그림자 시스템). 주인(로봇·쿄다이 손)이 매 프레임 Place로 "어디 위에, 얼마나 떠 있는지"만 알려주면
/// 바닥에 붙여 그리고, 높이 뜰수록 작고 옅게 만든다 — 떠 있다는 느낌의 대부분이 여기서 나온다.
///
/// 그림은 ShadowSprites.Blob(디더링으로 가장자리가 흐린 타원). 순수 연출 — 판정과 무관하고 렌더 직전(LateUpdate 등)에 부른다.
/// 크기 값은 월드 — 부모가 줄어 있어도(쿄다이 루트 0.32) 부모 크기로 나눠서 넣으므로 같은 크기로 보인다.
/// 쓰는 곳: RobotView(플레이어·일반 적 — 점프하면 줄어듦), KyodaiHand(손 — 값은 KyodaiHandFeel이 Configure로 넣음).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class GroundShadow : MonoBehaviour
{
    [Tooltip("바닥에 붙어 있을 때 그림자 크기(월드, 가로 × 세로). 임시값")]
    [SerializeField] private Vector2 size = new Vector2(0.9f, 0.22f);

    [Tooltip("가장 높이 떴을 때 크기 배율")]
    [Range(0.05f, 1f)] [SerializeField] private float minScale = 0.5f;

    [Tooltip("이 높이(월드) 이상 뜨면 가장 작고 옅다. 임시값")]
    [Min(0.1f)] [SerializeField] private float fadeHeight = 2f;

    [Range(0f, 1f)] [SerializeField] private float maxAlpha = 0.45f;
    [Range(0f, 1f)] [SerializeField] private float minAlpha = 0.15f;

    [Tooltip("바닥 위로 살짝 띄우는 높이(월드) — 바닥 그림과 겹쳐 깜빡이지 않게")]
    [SerializeField] private float lift = 0.02f;

    private SpriteRenderer spriteRenderer;

    public SpriteRenderer Renderer
    {
        get
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            return spriteRenderer;
        }
    }

    /// <summary>값을 코드에서 넣을 때 (쿄다이 손 — KyodaiHandFeel이 원본)</summary>
    public void Configure(Vector2 worldSize, float minScaleAtTop, float heightForMin, float alphaOnGround, float alphaAtTop)
    {
        size = worldSize;
        minScale = minScaleAtTop;
        fadeHeight = Mathf.Max(0.1f, heightForMin);
        maxAlpha = alphaOnGround;
        minAlpha = alphaAtTop;
    }

    /// <summary>
    /// x 위, 바닥 groundY에 그린다. height: 주인이 바닥에서 떠 있는 높이(월드).
    /// sizeMul·alphaMul: 주인 쪽 사정(손이 커짐·사라지는 중 등)을 곱한다. widthMul: 가로로만 (가로로 늘린 손바닥)
    /// </summary>
    public void Place(float x, float groundY, float height, float sizeMul = 1f, float alphaMul = 1f, float widthMul = 1f)
    {
        var r = Renderer;
        if (r.sprite != ShadowSprites.Blob) r.sprite = ShadowSprites.Blob;
        r.enabled = true;

        float k = Mathf.Clamp01(Mathf.Max(0f, height) / fadeHeight);
        float m = Mathf.Lerp(1f, minScale, k) * sizeMul;

        transform.position = new Vector3(x, groundY + lift, transform.position.z);
        transform.rotation = Quaternion.identity;
        // 스프라이트는 픽셀 1 = 1단위(PPU 1)라 그림 크기로 나눠 월드 크기에 맞추고, 부모 크기도 나눈다
        Vector2 px = r.sprite.rect.size;
        Vector3 ps = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(
            size.x * m * widthMul / (px.x * Mathf.Max(0.0001f, Mathf.Abs(ps.x))),
            size.y * m / (px.y * Mathf.Max(0.0001f, Mathf.Abs(ps.y))),
            1f);
        r.color = new Color(0f, 0f, 0f, Mathf.Lerp(maxAlpha, minAlpha, k) * alphaMul);
    }

    public void Hide()
    {
        Renderer.enabled = false;
    }

    /// <summary>주인 밑에 그림자 자식을 찾거나 만든다. 씬에서 직접 만들어 둔 자식(값 조절용)이 있으면 그걸 쓴다</summary>
    public static GroundShadow Ensure(Transform owner, string childName, int sortingOrder)
    {
        Transform t = owner.Find(childName);
        if (t == null)
        {
            t = new GameObject(childName, typeof(SpriteRenderer), typeof(GroundShadow)).transform;
            t.SetParent(owner, false);
        }
        var shadow = t.GetComponent<GroundShadow>();
        if (shadow == null) shadow = t.gameObject.AddComponent<GroundShadow>();
        shadow.Renderer.sortingOrder = sortingOrder;
        return shadow;
    }
}
