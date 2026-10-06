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
