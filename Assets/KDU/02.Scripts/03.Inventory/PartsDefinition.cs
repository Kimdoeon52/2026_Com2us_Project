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
    RightArm,
    LeftLeg,
    RightLeg,
    Head,
    Core
}

public enum GimmickTag
{
    None,
    ObjectBreak,      // 기물 파괴: 지상 기물에 추가 피해
    RangedObject,     // 원거리 기물: 멀리 있거나 높은 기물 타격
    HighPart,         // 상단 부위: 보스 머리·어깨 높이 판정
    MidPart,          // 중단 부위: 몸통 높이 판정
    LowPart,          // 하단 부위: 다리·바닥 높이 판정
    Pierce            // 관통/찌르기 : 바닥 못판 등 관통 판정
}

// 파츠 정의. 인벤토리 배치에 필요한 최소 필드만 둔다
[CreateAssetMenu(menuName = "KDU/인벤토리/파츠 정의", fileName = "PartsDefinition")]
public class PartsDefinition : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("식별자")]
    [SerializeField] private string _id;

    [Tooltip("표시명")]
    [SerializeField] private string _displayName;

    [Tooltip("설명")]
    [SerializeField] private string _description;

    [Tooltip("장착 슬롯")]
    [SerializeField] private Slot _slot;

    [Tooltip("파츠 등급")]
    [SerializeField] private PartGrade _grade;

    [Tooltip("내구도")]
    [SerializeField] private int _durability;

    [Header("인벤토리")]
    [Tooltip("점유 모양")]
    [SerializeField] private PartsShape _shape = new PartsShape();

    [Tooltip("최대 소지 갯수")]
    [SerializeField] private int _maxCount;

    [Tooltip("파츠 이미지. 외곽 비율에 맞춰 그린다")]
    [SerializeField] private Sprite _sprite;

    [Tooltip("파괴 저항력")]
    [SerializeField] private DestructiveResistance _destructiveResistance;

    [Tooltip("가격")]
    [SerializeField] private int cost;

    [Header("전투")]
    [Tooltip("전투 패배 시 부품 영구 파괴 확률 (0.0 ~ 1.0)\n" +
             "일반(70%), 레어(45%), 에픽(20%), 전설(5%), 프로토타입(1%)")]
    [Range(0f, 1f)]
    private float _destructionResistance;

    [Tooltip("공격력 계수")]
    private int _attackPower;        // 팔 파츠: 공격력 기여치

    [Tooltip("이동속도 보너스")]
    private float _moveSpeedBonus;   // 다리 파츠: 이동속도 보정

    [Tooltip("치명타 저항력")]
    private float _critResistance;   // 머리 파츠: 치명타 저항
    private float _critDamageReduction;

    [Header("스킬 연동")]
    [Tooltip("파츠 장착 시 부여되는 고유 액티브 스킬 ID")]
    private int _activeSkillId = -1;

    [Tooltip("기믹 파훼 태그")]
    private GimmickTag _gimmickTag = GimmickTag.None;

    [Tooltip("스킬 코스트")]
    private int _skillCost = 1;

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
    public float DestructionResistance => _destructionResistance;
    public int AttackPower => _attackPower;
    public float MoveSpeedBonus => _moveSpeedBonus;
    public float CritResistance => _critResistance;
    public float CritDamageReduction => _critDamageReduction;
    public int ActiveSkillId => _activeSkillId;
    public GimmickTag GimmickTag => _gimmickTag;
    public int SkillCost => _skillCost;


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

    public string GetTooltip(bool destroyed = false)
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
        string tag = destroyed ? " <color=red>[파괴됨]</color>" : "";
        int durability = destroyed ? 0 : _durability;

        return $"이름: {_displayName}{tag}\n" +
            "------------------------------\n" +
            $"설명: {_description}\n" +
            $"등급: <color=#{hex}>{_grade}</color>\n" +
            $"최대 소지 갯수: {_maxCount}\n" +
            $"파괴저항력: {_destructiveResistance}\n" +
            $"내구도: {durability}\n";
    }
}
