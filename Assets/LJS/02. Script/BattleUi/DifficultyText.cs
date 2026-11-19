using TMPro;
using UnityEngine;

namespace RealSteel.BossRace
{
    public class DifficultyText : MonoBehaviour
    {
        [SerializeField] private TMP_Text difficultyText;

        private void Start()
        {
            if (difficultyText == null) difficultyText = GetComponent<TMP_Text>();
            if (difficultyText == null) return;

            difficultyText.text = PlayerPrefs.GetString("SelectedDifficultyText", "");

            string hex = PlayerPrefs.GetString("SelectedDifficultyColor", "");
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out Color parsedColor))
            {
                difficultyText.color = parsedColor;
            }
        }
    }
}