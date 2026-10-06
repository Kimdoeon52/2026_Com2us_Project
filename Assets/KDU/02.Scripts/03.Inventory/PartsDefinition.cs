using UnityEngine;

public enum DestructiveResistance  // 파괴 저항력
{
    VeryLow,
    Low,
    Normal
}

public enum Slot
{
    LeftArm,
    rightArm,
    LeftLeg,
    RightLeg
}

// 파츠 정의. 인벤토리 배치에 필요한 최소 필드만 둔다
[CreateAssetMenu(menuName = "KDU/인벤토리/파츠 정의", fileName = "PartsDefinition")]
public class PartsDefinition : ScriptableObject
{
    [Tooltip("식별자")]
    [SerializeField] private string _id;

    [Tooltip("표시명")]
    [SerializeField] private string _displayName;

    [Tooltip("설명")]
    [SerializeField] private string _description;

    [Tooltip("점유 모양")]
    [SerializeField] private PartsShape _shape = new PartsShape();

    [Tooltip("최대 소지 갯수")]
    [SerializeField] private int _maxCount;

    [Tooltip("파츠 이미지. 외곽 비율에 맞춰 그린다")]
    [SerializeField] private Sprite _sprite;

    [Tooltip("장착 슬롯")]
    [SerializeField] private Slot _slot;

    [Tooltip("파츠 등급")]
    [SerializeField] private PartGrade _grade;

    [Tooltip("파괴 저항력")]
    [SerializeField] private DestructiveResistance _destructiveResistance;

    [Tooltip("내구도")]
    [SerializeField] private int _durability;

    [Tooltip("가격")]
    [SerializeField] private int cost;

    public string Id => _id;
    public string DisplayName => _displayName;
    public string Description => _description;
    public PartsShape Shape => _shape;
    public int MaxCount => _maxCount;
    public Sprite Sprite => _sprite;
    public Slot Slot => _slot;
    public PartGrade Grade => _grade;
    public DestructiveResistance DestructiveResistance => _destructiveResistance;
    public int Durability => _durability;
    public int Cost => cost;

    // 외곽 크기. 렌더 크기 계산용
    public Vector2Int Size => _shape.Size;

    private void OnValidate()
    {
        _shape.Validate();
        WarnAspect();
    }

    // 비율이 어긋나면 늘어나 보인다
    private void WarnAspect()
    {
        if (_sprite == null)
            return;

        float sprite = _sprite.rect.width / _sprite.rect.height;
        float shape = (float)_shape.Size.x / _shape.Size.y;

        if (Mathf.Abs(sprite - shape) > 0.01f)
            Debug.LogWarning($"{name}: 이미지 비율이 {_shape.Size.x}x{_shape.Size.y}와 다르다.", this);
    }

    public string GetTooltip()
    {
        Color color = _grade switch
        {
            PartGrade.Rare => Color.yellow,
            PartGrade.Epic => Color.blue,
            PartGrade.Legendary => Color.magenta,
            PartGrade.Prototype => Color.cyan,
            _ => Color.white
        };

        string hex = ColorUtility.ToHtmlStringRGB(color);

        return $"이름: {_displayName}\n" +
            "------------------------------\n" +
            $"설명: {_description}\n" +
            $"등급: <color=#{hex}>{_grade}</color>\n" + 
            $"최대 소지 갯수: {_maxCount}\n" +
            $"파괴저항력: {_destructiveResistance}\n" +
            $"내구도: {_durability}\n";
    }
}
