using TMPro;
using UnityEngine;

// 월드 말풍선(SpriteRenderer) 안에 글자(TextMeshPro)가 넘치지 않게 맞춰주는 컴포넌트
// - 글자 영역을 말풍선 스프라이트 안쪽(여백/꼬리 제외)으로 고정하고 자동 크기 + 줄바꿈 적용
//   → 대사가 길면 글자가 작아지거나 줄이 바뀌어서 말풍선 밖으로 나가지 않음
// - 말풍선 Draw Mode가 Sliced/Tiled면 글자 길이에 맞춰 말풍선 자체를 늘림
public class SpeechBubbleFitter : MonoBehaviour
{
    // 말풍선 크기 대비 글자 여백 비율 (Diabox 200x100 기준: 좌우 18px, 위 12px, 아래 30px(꼬리))
    private const float PaddingX = 0.09f;
    private const float PaddingTop = 0.12f;
    private const float PaddingBottom = 0.3f;
    private const float MinFontSize = 0.05f;
    private const float DefaultMaxFontSize = 3f;

    private SpriteRenderer boxRenderer;
    private TextMeshPro thinkChat;
    private Vector2 baseSize; // 말풍선 기본 크기 (로컬 단위). 이보다 작아지지 않음

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

        if (IsResizable) baseSize = boxRenderer.size;
        else if (boxRenderer.sprite != null) baseSize = boxRenderer.sprite.bounds.size;
        else baseSize = new Vector2(2f, 1f);

        // 글자: 줄바꿈 + 자동 크기. 최대 크기는 원래 지정한 글자 크기
        float maxFont = thinkChat.enableAutoSizing ? thinkChat.fontSizeMax : thinkChat.fontSize;
        if (maxFont <= MinFontSize) maxFont = DefaultMaxFontSize;
        thinkChat.textWrappingMode = TextWrappingModes.Normal;
        thinkChat.overflowMode = TextOverflowModes.Overflow;
        thinkChat.enableAutoSizing = true;
        thinkChat.fontSizeMin = MinFontSize;
        thinkChat.fontSizeMax = maxFont;
        thinkChat.margin = Vector4.zero;
        thinkChat.alignment = TextAlignmentOptions.Center;

        UpdateTextArea(baseSize);
    }

    private bool IsResizable => boxRenderer.drawMode == SpriteDrawMode.Sliced || boxRenderer.drawMode == SpriteDrawMode.Tiled;

    // 말풍선 안쪽(여백 제외) 영역에 글자 RectTransform을 맞춤 (말풍선 로컬 좌표 기준)
    private void UpdateTextArea(Vector2 bubbleSize)
    {
        RectTransform textRect = thinkChat.rectTransform;
        if (textRect.parent != boxRenderer.transform) textRect.SetParent(boxRenderer.transform, true); // 말풍선 자식이어야 같이 커지고 사라짐

        Vector2 center = boxRenderer.sprite != null && !IsResizable ? (Vector2)boxRenderer.sprite.bounds.center : Vector2.zero;
        float innerWidth = bubbleSize.x * (1f - PaddingX * 2f);
        float innerHeight = bubbleSize.y * (1f - PaddingTop - PaddingBottom);
        float innerCenterY = center.y + bubbleSize.y * (PaddingBottom - PaddingTop) * 0.5f; // 꼬리 쪽 여백만큼 위로

        Vector3 textScale = textRect.localScale;
        float sx = Mathf.Approximately(textScale.x, 0f) ? 1f : textScale.x;
        float sy = Mathf.Approximately(textScale.y, 0f) ? 1f : textScale.y;

        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(innerWidth / sx, innerHeight / sy);
        textRect.localRotation = Quaternion.identity;
        textRect.localPosition = new Vector3(center.x, innerCenterY, textRect.localPosition.z);
    }

    public void GetSize(string text)
    {
        if (thinkChat == null || boxRenderer == null) return;

        thinkChat.text = text;
        if (!IsResizable) return; // Simple 말풍선은 글자가 자동으로 줄어들며 맞춰짐

        // Sliced 말풍선: 최대 글자 크기 기준으로 필요한 크기만큼 말풍선을 늘림
        float savedMin = thinkChat.fontSizeMin;
        thinkChat.enableAutoSizing = false;
        thinkChat.fontSize = thinkChat.fontSizeMax;
        Vector2 textSize = thinkChat.GetPreferredValues(text, Mathf.Infinity, Mathf.Infinity);
        thinkChat.enableAutoSizing = true;
        thinkChat.fontSizeMin = savedMin;

        Vector3 textScale = thinkChat.rectTransform.localScale;
        float width = Mathf.Max(baseSize.x, textSize.x * textScale.x / (1f - PaddingX * 2f));
        float height = Mathf.Max(baseSize.y, textSize.y * textScale.y / (1f - PaddingTop - PaddingBottom));
        boxRenderer.size = new Vector2(width, height);
        UpdateTextArea(boxRenderer.size);
    }
}
