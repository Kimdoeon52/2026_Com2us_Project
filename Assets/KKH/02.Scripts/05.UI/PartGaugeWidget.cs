using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PartGaugeWidget : MonoBehaviour
{
    [Header("담당 부위")]
    [SerializeField] private BodyPart targetPart;

    [Header("UI 바인딩")]
    [SerializeField] private Slider durabilitySlider;
    [SerializeField] private Image durabilityFillImage;
    [SerializeField] private TextMeshProUGUI partNameText;
    [SerializeField] private TextMeshProUGUI partValueText;

    [Header("라벨 설정")]
    [Tooltip("비워둘 시 targetPart에 맞춰 'HEAD', 'CORE', 'L.ARM' 등이 자동 부여됨")]
    [SerializeField] private string customLabel = "";

    [Header("상태별 색상")]
    [SerializeField] private Color normalColor = new Color(0.1f, 0.95f, 0.2f); // 초록색 (정상)
    [SerializeField] private Color warningColor = new Color(0.95f, 0.95f, 0.2f); // 노란색 (경고)
    [SerializeField] private Color dangerColor = new Color(0.95f, 0.2f, 0.2f); // 빨간색 (위험)
    [SerializeField] private Color brokenColor = new Color(0.5f, 0.5f, 0.5f); // 회색 (파손)

    public BodyPart TargetPart => targetPart;

    private void Awake()
    {
        // 미할당 필드 자동 탐색 (Auto-Fallback)
        if (durabilitySlider == null)
            durabilitySlider = GetComponentInChildren<Slider>(true);

        if (durabilityFillImage == null && durabilitySlider != null && durabilitySlider.fillRect != null)
            durabilityFillImage = durabilitySlider.fillRect.GetComponent<Image>();

        if (partNameText == null)
        {
            var tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                if (t.gameObject.name.IndexOf("Label", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.gameObject.name.IndexOf("Name", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    partNameText = t;
                    break;
                }
            }
        }

        if (partValueText == null)
        {
            var tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                if (t.gameObject.name.IndexOf("Value", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.gameObject.name.IndexOf("HP", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    partValueText = t;
                    break;
                }
            }
        }

        UpdateLabelDisplay();
    }

    private void Start()
    {
        UpdateLabelDisplay();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (partNameText == null)
        {
            var tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                if (t.gameObject.name.IndexOf("Label", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.gameObject.name.IndexOf("Name", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    partNameText = t;
                    break;
                }
            }
        }
        UpdateLabelDisplay();
    }
#endif

    /// <summary>
    /// 라벨 표시 갱신 (커스텀 라벨 우선, 없으면 기본 부위명)
    /// </summary>
    public void UpdateLabelDisplay()
    {
        if (!string.IsNullOrEmpty(customLabel))
        {
            SetLabel(customLabel);
        }
        else
        {
            SetDefaultLabel();
        }
    }

    /// <summary>
    /// targetPart에 따른 기획서 기본 영문 부위 라벨 부여
    /// </summary>
    public void SetDefaultLabel()
    {
        string label = targetPart switch
        {
            BodyPart.Head => "HEAD",
            BodyPart.Core => "CORE",
            BodyPart.LeftArm => "L.ARM",
            BodyPart.RightArm => "R.ARM",
            BodyPart.LeftLeg => "L.LEG",
            BodyPart.RightLeg => "R.LEG",
            _ => targetPart.ToString().ToUpper()
        };
        SetLabel(label);
    }

    /// <summary>
    /// 라벨 텍스트 지정 (예 : "머리", "몸통", "팔", "다리", 부품명 등)
    /// </summary>
    public void SetLabel(string label)
    {
        if (partNameText != null)
        {
            partNameText.text = label;
        }
    }

    /// <summary>
    /// 내구도 값 업데이트
    /// </summary>
    /// <param name="cur">현재 내구도 값</param>
    /// <param name="max">최대 내구도 값</param>
    public void UpdateDurability(int cur, int max)
    {
        //1. 슬라이더 게이지 갱신
        if (durabilitySlider != null)
        {
            durabilitySlider.maxValue = max;
            durabilitySlider.value = cur;
        }

        // 2. 텍스트 표시
        if (partValueText != null)
        {
            if (cur <= 0)
            {
                partValueText.text = targetPart == BodyPart.Core ? "<color=#FF0000>K.O</color>" : "<color=#FF3333>파괴</color>";
            }
            else
            {
                partValueText.text = $"{cur}/{max}";
            }
        }
        
        // 3. 잔여 비율에 따른 동적 색상 변화
        if (durabilityFillImage != null)
        {
            float ratio = max > 0 ? (float)cur / max : 0f;
            if (cur <= 0f)
            {
                durabilityFillImage.color = brokenColor;
            }
            else if (ratio > 0.5f)
            {
                durabilityFillImage.color = normalColor;
            }
            else if (ratio > 0.25f)
            {
                durabilityFillImage.color = warningColor;
            }
            else
            {
                durabilityFillImage.color = dangerColor;
            }
        }
    }
    
}

