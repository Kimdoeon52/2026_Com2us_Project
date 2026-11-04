using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 말풍선을 텍스트 길이에 맞춰 늘려주는 컴포넌트 (줄바꿈 대신 말풍선이 옆으로 늘어남)
// Diabox 스프라이트를 런타임에 9-slice로 바꿔서 꼬리 부분은 늘어나지 않게 하고,
// 꼬리 위치를 고정한 채 오른쪽(줄이 많으면 위쪽)으로 커짐
public class SpeechBubbleFitter : MonoBehaviour
{
    // 9-slice 경계 (Diabox 200x100 기준 비율). 왼쪽 경계가 꼬리 오른쪽 끝(108px)을 덮어서 꼬리는 안 늘어남
    private const float BorderLeft = 0.56f;
    private const float BorderRight = 0.15f;
    private const float BorderTop = 0.2f;
    private const float BorderBottom = 0.32f; // 아래 테두리 + 꼬리
    private const float TailAnchorX = 0.5f; // 원래 말풍선 중심 = 이 지점이 늘어나도 제자리에 있음

    // 원본 크기(200x100) 기준 글자 여백
    private const float PaddingX = 18f;
    private const float PaddingTop = 12f;
    private const float PaddingBottom = 30f; // 꼬리 높이 포함

    private static readonly Dictionary<Sprite, Sprite> slicedCache = new Dictionary<Sprite, Sprite>(); // 같은 스프라이트는 한 번만 변환

    private RectTransform bubbleRect;
    private TextMeshProUGUI text;
    private Vector2 baseSize; // 프리팹에 잡혀 있던 기본 크기 (이보다 작아지지 않음)
    private float scale = 1f; // 원본 스프라이트 대비 크기 비율

    public static SpeechBubbleFitter Attach(Image bubble, TextMeshProUGUI text)
    {
        if (bubble == null || text == null) return null;
        if (!bubble.TryGetComponent(out SpeechBubbleFitter fitter))
        {
            fitter = bubble.gameObject.AddComponent<SpeechBubbleFitter>();
        }
        fitter.Init(bubble, text);
        return fitter;
    }

    private void Init(Image bubble, TextMeshProUGUI chatText)
    {
        bubbleRect = bubble.rectTransform;
        text = chatText;

        // 기존 자동 레이아웃(ContentSizeFitter, LayoutGroup)이 크기를 덮어쓰지 않게 끔
        DisableLayoutControllers(bubble.gameObject);
        DisableLayoutControllers(text.gameObject);

        Sprite source = bubble.sprite;
        Vector2 spriteSize = source != null ? source.rect.size : new Vector2(200f, 100f);
        float baseWidth = bubbleRect.rect.width > 1f ? bubbleRect.rect.width : spriteSize.x;
        scale = baseWidth / spriteSize.x;
        baseSize = new Vector2(baseWidth, spriteSize.y * scale); // 높이는 스프라이트 비율대로

        if (source != null)
        {
            bubble.sprite = GetSlicedSprite(source);
            bubble.type = Image.Type.Sliced;
            bubble.fillCenter = true;
            float refPpu = bubble.canvas != null ? bubble.canvas.referencePixelsPerUnit : 100f;
            bubble.pixelsPerUnitMultiplier = refPpu / source.pixelsPerUnit / scale; // 테두리를 기본 크기 비율로 그림
        }

        // 글자는 말풍선 안쪽 영역에 꽉 채우고 줄바꿈 없음
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(PaddingX * scale, PaddingBottom * scale);
        textRect.offsetMax = new Vector2(-PaddingX * scale, -PaddingTop * scale);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.horizontalAlignment = HorizontalAlignmentOptions.Center;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;

        Resize(baseSize);
    }

    public void Fit(string message) // 텍스트 넣고 크기 맞추기
    {
        text.text = message;
        Vector2 preferred = text.GetPreferredValues(message, Mathf.Infinity, Mathf.Infinity);

        float width = Mathf.Max(baseSize.x, preferred.x + PaddingX * 2f * scale);
        float height = Mathf.Max(baseSize.y, preferred.y + (PaddingTop + PaddingBottom) * scale);
        Resize(new Vector2(width, height));
    }

    private void Resize(Vector2 size)
    {
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        bubbleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);

        // 꼬리 위치(원래 중심점)가 항상 같은 자리에 오도록 피벗 이동 -> 오른쪽/위쪽으로만 커짐
        bubbleRect.pivot = new Vector2(baseSize.x * TailAnchorX / size.x, baseSize.y * 0.5f / size.y);
    }

    private static Sprite GetSlicedSprite(Sprite source)
    {
        if (source.border != Vector4.zero) return source; // 이미 9-slice가 잡힌 스프라이트는 그대로 사용
        if (slicedCache.TryGetValue(source, out Sprite cached) && cached != null) return cached;

        Rect rect = source.rect;
        Vector4 border = new Vector4(rect.width * BorderLeft, rect.height * BorderBottom, rect.width * BorderRight, rect.height * BorderTop); // (왼, 아래, 오른, 위)
        Vector2 pivot = new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height);
        Sprite sliced = Sprite.Create(source.texture, rect, pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
        sliced.name = source.name + "_Sliced";
        slicedCache[source] = sliced;
        return sliced;
    }

    private static void DisableLayoutControllers(GameObject go)
    {
        foreach (var fitter in go.GetComponents<ContentSizeFitter>()) fitter.enabled = false;
        foreach (var group in go.GetComponents<LayoutGroup>()) group.enabled = false;
    }
}
