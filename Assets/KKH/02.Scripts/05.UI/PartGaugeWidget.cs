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

    [Header("상태별 색상")]
    [SerializeField] private Color normalColor = new Color(0.1f, 0.95f, 0.2f); // 초록색 (정상)
    [SerializeField] private Color warningColor = new Color(0.95f, 0.95f, 0.2f); // 노란색 (경고)
    [SerializeField] private Color dangerColor = new Color(0.95f, 0.2f, 0.2f); // 빨간색 (위험)
    [SerializeField] private Color brokenColor = new Color(0.5f, 0.5f, 0.5f); // 회색 (파손)

    public BodyPart TargetPart => targetPart;

    /// <summary>
    /// 라벨 텍스트 지정 (예 : "머리", "몸통", "팔", "다리")
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
                partValueText.text = targetPart == BodyPart.Core ? "<color = #FF0000 >K.O</color>" : "<color = #FF3333 >파괴</color>";
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

