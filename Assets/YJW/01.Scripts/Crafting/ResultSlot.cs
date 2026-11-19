using UnityEngine;
using UnityEngine.UI;

public class ResultSlot : MonoBehaviour
{
    [SerializeField] private Image _icon;   // Icon 자식의 Image를 드래그해서 연결
    [Tooltip("다리 파츠 아이콘 크기 (다리 이미지가 다른 파츠보다 작게 보여서 키운다)")]
    [SerializeField] private Vector2 _legIconSize = new Vector2(150f, 150f);
    [Tooltip("다리 외 파츠 아이콘 크기")]
    [SerializeField] private Vector2 _iconSize = new Vector2(100f, 100f);

    public PartsDefinition _part;

    public void SetPart(PartsDefinition part)
    {
        bool has = part != null;
        bool isLeg = has && (part.Slot == Slot.LeftLeg || part.Slot == Slot.RightLeg);
        _icon.rectTransform.sizeDelta = isLeg ? _legIconSize : _iconSize;
        _icon.enabled = has;
        _icon.sprite = has ? part.Sprite : null;
        PartOutline.Apply(_icon, part);
        _icon.preserveAspect = true;

        // 프리팹에서 빈 칸이 하얗게 보이지 않도록 알파를 0으로 두었으므로 결과가 있을 때 다시 보이게 한다
        Color c = _icon.color;
        c.a = has ? 1f : 0f;
        _icon.color = c;
        _part = part;
    }
}
