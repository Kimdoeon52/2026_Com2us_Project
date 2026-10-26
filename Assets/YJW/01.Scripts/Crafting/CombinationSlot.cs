using UnityEngine;
using UnityEngine.UI;

public class CombinationSlot : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private Image _BGImage;

    public Sprite sprite;
    public string componentID;
    public bool HasComponent = false;

    public ComponentDefinition Component { get; private set; }

    public void SetComponent(ComponentDefinition component, bool hasComp = true)
    {
        Component = component;
        sprite = component != null ? component.Sprite : null;
        componentID = component != null ? component.Id : null;
        HasComponent = component == null || hasComp;   // false면 인벤토리에서 빠지지 않은 부품 (빨간 칸). 빈 칸은 항상 true
        if (HasComponent == true)
        {
            _BGImage.color = Color.white;
            Color c = _icon.color;
            c.a = 1;
            _icon.color = c;
        }
        else
        {
            _BGImage.color = Color.red;
            Color c = _icon.color;
            c.a = 0.5f;
            _icon.color = c;
        }

        if (_icon != null)
        {
            _icon.sprite = sprite;
            _icon.enabled = sprite != null;
        }
    }
    public void Clear()
    {
        SetComponent(null);
    }
}
