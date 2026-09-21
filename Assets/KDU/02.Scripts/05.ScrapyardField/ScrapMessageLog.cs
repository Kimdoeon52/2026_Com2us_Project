using TMPro;
using UnityEngine;

// 화면에 잠깐 떴다 사라지는 안내 문구. 무엇을 띄울지는 호출한 쪽이 정한다
public class ScrapMessageLog : MonoBehaviour
{
    [Tooltip("문구를 그릴 텍스트")]
    [SerializeField] private TMP_Text _label;

    [Tooltip("표시 시간(초)")]
    [Min(0.1f)]
    [SerializeField] private float _duration = 2f;

    [Tooltip("사라질 때 걸리는 시간(초)")]
    [Min(0f)]
    [SerializeField] private float _fadeDuration = 0.4f;

    [Tooltip("획득 성공 문구 색")]
    [SerializeField] private Color _normalColor = Color.white;

    [Tooltip("거리 부족 등 실패 문구 색")]
    [SerializeField] private Color _warningColor = new Color(1f, 0.3f, 0.25f);

    private float _remaining;

    private void Awake() => SetAlpha(0f);

    public void Show(string message) => Display(message, _normalColor);

    public void ShowWarning(string message) => Display(message, _warningColor);

    private void Display(string message, Color color)
    {
        if (_label == null)
            return;

        _label.text = message;
        _label.color = color;
        _remaining = _duration + _fadeDuration;
        SetAlpha(1f);
    }

    private void Update()
    {
        if (_remaining <= 0f)
            return;

        _remaining -= Time.deltaTime;

        if (_remaining <= 0f)
        {
            SetAlpha(0f);
            return;
        }

        if (_fadeDuration > 0f && _remaining < _fadeDuration)
            SetAlpha(_remaining / _fadeDuration);
    }

    private void SetAlpha(float alpha)
    {
        if (_label == null)
            return;

        Color color = _label.color;
        color.a = Mathf.Clamp01(alpha);
        _label.color = color;
    }
}
