using UnityEngine;
using UnityEngine.UI;

public class CombinationSlot : MonoBehaviour
{
    [SerializeField] private Image _icon;

    public Sprite sprite;
    public string componentID;

    public ComponentDefinition Component { get; private set; }

    public void SetComponent(ComponentDefinition component)
    {
        Component = component;
        sprite = component != null ? component.Sprite : null;
        componentID = component != null ? component.Id : null;

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
