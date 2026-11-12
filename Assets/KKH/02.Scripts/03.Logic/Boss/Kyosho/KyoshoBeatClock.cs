using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [쿄쇼 정박 리듬 클럭 (KyoshoBeatClock)]
    /// AudioSettings.dspTime 기반으로 프레임 드랍에 흔들리지 않는 정확한 BPM 박자/마디 타이머를 제공함.
    /// Time.timeScale == 0 일시정지 구간은 자동 동결하여 일시정지 후에도 박자가 밀리지 않음.
    /// </summary>
    public class KyoshoBeatClock : MonoBehaviour
    {
        [Header("박자 설정")]
        [SerializeField] private float bpm = 112f;
        [SerializeField] private int beatsPerBar = 4;
        [SerializeField] private bool playMetronomeTick = true;

        public float Bpm => Mathf.Max(30f, bpm);
        public int BeatsPerBar => Mathf.Max(1, beatsPerBar);
        public float BeatSec => 60f / Bpm;
        public float BarSec => BeatSec * BeatsPerBar;

        public bool IsRunning { get; private set; }
        public double SongTimeSeconds { get; private set; }
        public double TotalBeats => SongTimeSeconds / BeatSec;
        public int CurrentBeatIndex { get; private set; } = -1;
        public int CurrentBarIndex { get; private set; } = -1;
        public int BeatInBar => CurrentBeatIndex >= 0 ? (CurrentBeatIndex % BeatsPerBar) : 0;

        /// <summary>현재 박 내 진행도 (0.0 ~ 1.0)</summary>
        public float BeatProgress01
        {
            get
            {
                if (!IsRunning) return 0f;
                double b = TotalBeats;
                return Mathf.Clamp01((float)(b - Math.Floor(b)));
            }
        }

        /// <summary>매 박 정각 이벤트: (beatInBar [0..BeatsPerBar-1], totalBeatIndex)</summary>
        public event Action<int, int> OnBeat;

        /// <summary>매 마디 첫 박(강박) 이벤트: (barIndex)</summary>
        public event Action<int> OnDownbeat;

        /// <summary>매 프레임 현재 박 진행도 브로드캐스트 (0..1)</summary>
        public event Action<float> OnBeatProgress;

        private double _lastDspTime;
        private int _lastProcessedBeat = -1;

        public bool PlayMetronomeTick
        {
            get => playMetronomeTick;
            set => playMetronomeTick = value;
        }

        public void StartClock(float targetBpm, int targetBeatsPerBar = 4)
        {
            bpm = Mathf.Max(30f, targetBpm);
            beatsPerBar = Mathf.Max(1, targetBeatsPerBar);
            SongTimeSeconds = 0.0;
            CurrentBeatIndex = 0;
            CurrentBarIndex = 0;
            _lastProcessedBeat = -1;
            _lastDspTime = AudioSettings.dspTime;
            IsRunning = true;

            // 0박 첫 강박 즉시 발행
            ProcessBeatTransition(0);
        }

        public void SetBpm(float targetBpm)
        {
            double curTotalBeats = TotalBeats;
            bpm = Mathf.Max(30f, targetBpm);
            SongTimeSeconds = curTotalBeats * BeatSec;
        }

        public void StopClock()
        {
            IsRunning = false;
        }

        private void Update()
        {
            if (!IsRunning) return;

            double nowDsp = AudioSettings.dspTime;
            double delta = nowDsp - _lastDspTime;
            _lastDspTime = nowDsp;

            // 일시정지(Time.timeScale == 0) 시 클럭 동결, 그 외에는 DSP 델타 신뢰
            if (Time.timeScale <= 0f || delta <= 0.0)
            {
                return;
            }

            if (delta > 0.25)
            {
                delta = Time.unscaledDeltaTime;
            }

            SongTimeSeconds += delta * Time.timeScale;
            int newBeatIndex = Mathf.FloorToInt((float)(SongTimeSeconds / BeatSec));

            if (newBeatIndex > _lastProcessedBeat)
            {
                // 프레임 히치가 있었더라도 누락된 경계 없이 순차 발행 (최대 2박 캡)
                int start = Mathf.Max(_lastProcessedBeat + 1, newBeatIndex - 1);
                for (int b = start; b <= newBeatIndex; b++)
                {
                    ProcessBeatTransition(b);
                }
            }

            OnBeatProgress?.Invoke(BeatProgress01);
        }

        private void ProcessBeatTransition(int beatIndex)
        {
            _lastProcessedBeat = beatIndex;
            CurrentBeatIndex = beatIndex;
            int bar = beatIndex / BeatsPerBar;
            int inBar = beatIndex % BeatsPerBar;

            if (inBar == 0)
            {
                CurrentBarIndex = bar;
                OnDownbeat?.Invoke(bar);
            }

            if (playMetronomeTick)
            {
                KyoshoSfx.PlayBeatTick(inBar == 0);
            }

            OnBeat?.Invoke(inBar, beatIndex);
        }

        public float BeatsToSeconds(float beats) => beats * BeatSec;

        public double GetRemainingBeatsInBar()
        {
            double b = TotalBeats;
            double nextBarBeat = (Math.Floor(b / BeatsPerBar) + 1.0) * BeatsPerBar;
            return Math.Max(0.0, nextBarBeat - b);
        }

        /// <summary>
        /// 지정한 박(beats) 수만큼 정확히 DSP 클럭 기준으로 대기함.
        /// </summary>
        public async UniTask WaitBeatsAsync(float beats, CancellationToken token)
        {
            if (beats <= 0f) return;
            if (!IsRunning)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(beats * BeatSec), cancellationToken: token);
                return;
            }

            double targetBeat = TotalBeats + beats;
            while (IsRunning && TotalBeats < targetBeat)
            {
                token.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        /// <summary>
        /// 목표 절대 박(absoluteBeat)에 도달할 때까지 대기함 (선행 보정 Lead-Time 타이밍용).
        /// </summary>
        public async UniTask WaitUntilAbsoluteBeatAsync(double absoluteBeat, CancellationToken token)
        {
            while (IsRunning && TotalBeats < absoluteBeat)
            {
                token.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        /// <summary>
        /// 다음 박(Beat) 경계까지 대기함.
        /// </summary>
        public async UniTask<int> WaitUntilNextBeatAsync(CancellationToken token)
        {
            if (!IsRunning) return 0;
            int startBeat = CurrentBeatIndex;
            while (IsRunning && CurrentBeatIndex <= startBeat)
            {
                token.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            return CurrentBeatIndex;
        }

        /// <summary>
        /// 다음 마디 첫 박(Downbeat)까지 대기함.
        /// </summary>
        public async UniTask<int> WaitUntilNextBarAsync(CancellationToken token)
        {
            if (!IsRunning) return 0;
            int startBar = CurrentBarIndex;
            while (IsRunning && CurrentBarIndex <= startBar)
            {
                token.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            return CurrentBarIndex;
        }
    }
}
