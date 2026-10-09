using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RealSteel.Dialogue.Unity
{
    public enum PartSlot { Head, LeftArm, RightArm, LeftLeg, RightLeg }

    /// <summary>
    /// 전투 중 바크(파츠 파괴 등). 입력을 절대 잠그지 않고 게임 시간(scaled)으로 동작한다.
    ///
    /// 스팸 방지 3단계
    ///  1) 엔트리별 cooldownSec (데이터)
    ///  2) 전역 최소 간격 globalGapSec (여러 파츠가 동시에 터져도 한 줄씩)
    ///  3) 표시 중일 때는 "더 높은 band"만 끼어들 수 있음 (Fallback이 Reactive를 덮지 않게)
    /// </summary>
    public sealed class BarkManager : MonoBehaviour
    {
        public static BarkManager Instance { get; private set; }

        [SerializeField] private string partBreakPool = "combat.part_break";
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float displaySec = 2.2f;
        [SerializeField] private float fadeSec = 0.25f;
        [SerializeField] private float globalGapSec = 0.8f;

        private DialogueEntry _current;
        private float _shownAt = -999f;
        private readonly Dictionary<string, DVal> _args = new Dictionary<string, DVal>();

        private bool IsShowing => _current != null && Time.time - _shownAt < displaySec;

        private void Awake()
        {
            Instance = this;
            if (group != null) group.alpha = 0f;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>파츠 내구도 컴포넌트가 0이 되는 순간 호출</summary>
        public bool PlayPartBreak(PartSlot part)
        {
            _args.Clear();
            _args["event.partBroken"] = DVal.Str(part.ToString());
            return Play(partBreakPool, _args);
        }

        public bool Play(string poolId, IReadOnlyDictionary<string, DVal> args)
        {
            var service = DialogueService.Instance;
            if (service == null) return false;

            if (Time.time - _shownAt < globalGapSec) return false; // 직전 바크 직후엔 무엇도 끼어들지 않음
            bool showing = IsShowing;

            // 표시 중이면 더 중요한 대사만 수락 → 거절 시 엔진에 기록이 남지 않는다
            var entry = service.RequestInstant(poolId, args,
                accept: e => !showing || (int)e.band > (int)_current.band);
            if (entry == null) return false;

            _current = entry;
            _shownAt = Time.time;
            string speaker = entry.lines[0].speaker;
            string name = string.IsNullOrEmpty(speaker) ? "" : service.Text.ResolveSpeakerName(speaker);
            string text = service.Text.ResolveLine(entry.lines[0]);
            subtitle.text = string.IsNullOrEmpty(name) ? text : $"<b>{name}</b>  {text}";
            return true;
        }

        private void Update()
        {
            if (group == null) return;
            float t = Time.time - _shownAt;
            float alpha;
            if (_current == null || t >= displaySec) alpha = 0f;
            else if (t < fadeSec) alpha = t / fadeSec;
            else if (t > displaySec - fadeSec) alpha = (displaySec - t) / fadeSec;
            else alpha = 1f;
            group.alpha = alpha;
            if (_current != null && t >= displaySec) _current = null;
        }
    }
}
