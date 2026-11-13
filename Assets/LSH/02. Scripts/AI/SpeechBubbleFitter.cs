using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 월드 말풍선(SpriteRenderer)이 글자(TextMeshPro) 길이에 맞춰 늘어나게 하는 컴포넌트
// - 말풍선 스프라이트를 런타임에 9-slice(테두리 고정)로 바꿔서 모서리는 그대로, 가운데만 늘어남
// - 대사가 길면 먼저 옆으로 늘어나고, 최대 폭(MaxWidthRatio)을 넘으면 그때부터 줄바꿈하며 위아래로 늘어남
// - 그래도 넘치면(아주 긴 대사) 글자가 자동으로 줄어듦
public class SpeechBubbleFitter : MonoBehaviour
{
    // 말풍선 기본 크기 대비 글자 여백 비율 (좌우 / 위 / 아래)
    private const float PaddingX = 0.12f;
    private const float PaddingTop = 0.14f;
    private const float PaddingBottom = 0.18f;
    private const float SliceBorderRatio = 0.25f; // 9-slice 테두리 = 스프라이트 짧은 변의 25%
    private const float MaxWidthRatio = 3f; // 기본 폭의 최대 3배까지 옆으로 늘어남. 넘으면 줄바꿈
    private const float MinFontSize = 0.05f;
    private const float DefaultMaxFontSize = 3f;
    private const float FontSizeMultiplier = 1.4f; // 원래 지정한 글자 크기의 1.4배

    private static readonly Dictionary<Sprite, Sprite> slicedCache = new Dictionary<Sprite, Sprite>(); // 같은 스프라이트는 한 번만 변환

    private SpriteRenderer boxRenderer;
    private TextMeshPro thinkChat;
    private Vector2 baseSize; // 말풍선 기본 크기 (로컬 단위). 이보다 작아지지 않음
    private float maxFontSize;
    private bool isReady;

    public static SpeechBubbleFitter Attach(SpriteRenderer bubble, TextMeshPro text)
    {
        if (bubble == null || text == null) return null;
        if (!bubble.TryGetComponent(out SpeechBubbleFitter fitter))
        {
            fitter = bubble.gameObject.AddComponent<SpeechBubbleFitter>();
        }
        fitter.Init(bubble, text);
        return fitter;
    }

    private void Init(SpriteRenderer bubble, TextMeshPro text)
    {
        boxRenderer = bubble;
        thinkChat = text;

        // 1. 말풍선을 9-slice로 (모서리 고정, 가운데만 늘어남)
        if (boxRenderer.sprite != null)
        {
            boxRenderer.sprite = GetSlicedSprite(boxRenderer.sprite);
            baseSize = boxRenderer.sprite.bounds.size; // 원본 스프라이트 크기 = 최소 크기
        }
        else
        {
            baseSize = new Vector2(2f, 1f);
        }
        boxRenderer.drawMode = SpriteDrawMode.Sliced;
        boxRenderer.size = baseSize;

        // 2. 글자 설정 (최대 크기는 처음 지정한 크기 x 배율, 한 번만 계산)
        if (!isReady)
        {
            float font = thinkChat.enableAutoSizing ? thinkChat.fontSizeMax : thinkChat.fontSize;
            if (font <= MinFontSize) font = DefaultMaxFontSize;
            maxFontSize = font * FontSizeMultiplier;
            isReady = true;
        }
        thinkChat.textWrappingMode = TextWrappingModes.Normal;
        thinkChat.overflowMode = TextOverflowModes.Overflow;
        thinkChat.margin = Vector4.zero;
        thinkChat.alignment = TextAlignmentOptions.Center;
        thinkChat.enableAutoSizing = true;
        thinkChat.fontSizeMin = MinFontSize;
        thinkChat.fontSizeMax = maxFontSize;

        UpdateTextArea(baseSize);
    }

    public void GetSize(string text)
    {
        if (thinkChat == null || boxRenderer == null) return;
        thinkChat.text = text;

        Vector3 textScale = thinkChat.rectTransform.localScale;
        float sx = Mathf.Approximately(textScale.x, 0f) ? 1f : textScale.x;
        float sy = Mathf.Approximately(textScale.y, 0f) ? 1f : textScale.y;
        // 최대 글자 크기 기준으로 필요한 크기를 잼
        thinkChat.enableAutoSizing = false;
        thinkChat.fontSize = maxFontSize;

        // 1. 한 줄로 썼을 때 폭 → 기본 폭 ~ 최대 폭 사이로
        float oneLineWidth = thinkChat.GetPreferredValues(text, Mathf.Infinity, Mathf.Infinity).x * sx;
        float maxWidth = baseSize.x * MaxWidthRatio;
        float width = Mathf.Clamp(oneLineWidth + baseSize.x * PaddingX * 2f + 0.01f, baseSize.x, maxWidth); // 0.01: 경계에서 줄바꿈 방지

        // 2. 그 폭 안에서 줄바꿈했을 때 높이
        float innerWidth = InnerWidth(width);
        float textHeight = thinkChat.GetPreferredValues(text, innerWidth / sx, Mathf.Infinity).y * sy;
        float height = Mathf.Max(baseSize.y, textHeight + baseSize.y * (PaddingTop + PaddingBottom));

        thinkChat.enableAutoSizing = true; // 그래도 넘치면 자동으로 줄어듦
        thinkChat.fontSizeMax = maxFontSize;

        boxRenderer.size = new Vector2(width, height);
        UpdateTextArea(boxRenderer.size);
    }

    // 여백은 기본 크기 기준으로 고정 (말풍선이 커져도 테두리 두께는 그대로라서)
    private float InnerWidth(float width) => Mathf.Max(0.01f, width - baseSize.x * PaddingX * 2f);
    private float InnerHeight(float height) => Mathf.Max(0.01f, height - baseSize.y * (PaddingTop + PaddingBottom));

    // 말풍선 안쪽(여백 제외) 영역에 글자 RectTransform을 맞춤 (말풍선 로컬 좌표 기준)
    private void UpdateTextArea(Vector2 bubbleSize)
    {
        RectTransform textRect = thinkChat.rectTransform;
        if (textRect.parent != boxRenderer.transform) textRect.SetParent(boxRenderer.transform, true); // 말풍선 자식이어야 같이 커지고 사라짐

        Vector3 textScale = textRect.localScale;
        float sx = Mathf.Approximately(textScale.x, 0f) ? 1f : textScale.x;
        float sy = Mathf.Approximately(textScale.y, 0f) ? 1f : textScale.y;

        // Sliced 말풍선은 피벗(가운데) 기준으로 위아래/좌우로 늘어남. 아래 여백(꼬리)만큼 글자를 살짝 위로
        float centerY = baseSize.y * (PaddingBottom - PaddingTop) * 0.5f;

        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(InnerWidth(bubbleSize.x) / sx, InnerHeight(bubbleSize.y) / sy);
        textRect.localRotation = Quaternion.identity;
        textRect.localPosition = new Vector3(0f, centerY, textRect.localPosition.z);
    }

    // 테두리(border)가 없는 스프라이트를 9-slice 가능한 복사본으로 (이미 border가 있으면 그대로)
    private static Sprite GetSlicedSprite(Sprite source)
    {
        if (source.border != Vector4.zero) return source;
        if (slicedCache.TryGetValue(source, out Sprite cached) && cached != null) return cached;

        Rect rect = source.rect;
        float b = Mathf.Min(rect.width, rect.height) * SliceBorderRatio;
        Vector2 pivot = new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height);
        Sprite sliced = Sprite.Create(source.texture, rect, pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        sliced.name = source.name + "_Sliced";
        slicedCache[source] = sliced;
        return sliced;
    }
}
