using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealSteel.Dialogue.Unity
{
    /// <summary>
    /// 대사 로그 창. 열 때 한 번 텍스트를 만들고 스크롤만 한다 (매 프레임 재생성 금지).
    /// 대사가 수천 줄 쌓여도 백로그는 링버퍼(기본 200줄)라 비용이 일정하다.
    /// </summary>
    public sealed class BacklogView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text content;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private string speakerColor = "#F2B544";

        private readonly StringBuilder _sb = new StringBuilder(4096);

        public bool IsOpen => root.activeSelf;

        private void Awake() => root.SetActive(false);

        public void Open(DialogueBacklog log)
        {
            _sb.Clear();
            for (int i = 0; i < log.Count; i++)
            {
                var item = log[i];
                if (!string.IsNullOrEmpty(item.SpeakerName))
                    _sb.Append("<color=").Append(speakerColor).Append('>').Append(item.SpeakerName).Append("</color>\n");
                _sb.Append(item.Text).Append("\n\n");
            }
            content.text = _sb.ToString();
            root.SetActive(true);
            Canvas.ForceUpdateCanvases();
            if (scroll != null) scroll.verticalNormalizedPosition = 0f; // 최신 대사가 보이게
        }

        public void Close() => root.SetActive(false);
    }
}
