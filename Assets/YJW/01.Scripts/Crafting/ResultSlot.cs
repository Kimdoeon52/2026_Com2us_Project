using UnityEngine;
using UnityEngine.UI;

public class ResultSlot : MonoBehaviour
{
    [SerializeField] private PartsCatalog allParts;

    [SerializeField] private Image _icon;   // Icon 자식의 Image를 드래그해서 연결

    public PartsDefinition _part;

    public void SetPart(PartsDefinition part)
    {
        bool has = part != null;
        _icon.enabled = has;
        _icon.sprite = has ? part.Sprite : null;

        // 프리팹에서 빈 칸이 하얗게 보이지 않도록 알파를 0으로 두었으므로 결과가 있을 때 다시 보이게 한다
        Color c = _icon.color;
        c.a = has ? 1f : 0f;
        _icon.color = c;
        _part = part;
    }

    public PartsDefinition FindPart(string partID)
    {
        foreach(var part in allParts.Parts)
        {
            if (partID == part.Id)
                return part;
        }

        return null;
    }
}
