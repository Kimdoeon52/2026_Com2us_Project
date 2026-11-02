using System;
using RealSteel.Dialogue;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.BossRace
{
    public class SeasonRaceCall : MonoBehaviour
    {
        private enum Phase
        {
            Idle,
            Waiting,
            Ringing,
            Asking,
            Selecting,
            Confirming,
            Accepting,
        }

        private const float RingPeriod = 2f;

        [Header("연결")]
        [SerializeField] private DialogueManager manager;
        [SerializeField] private BossRaceSelectUI selectUI;
        [SerializeField] private DialogueChoice choice;
        [SerializeField] private MonoBehaviour progressSource;

        [Header("전화 UI")]
        [SerializeField] private GameObject ringPanel;
        [SerializeField] private RectTransform phoneIcon;
        [SerializeField] private TMP_Text answerHintText;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip ringClip;
        [SerializeField] private AudioClip pickUpClip;

        [Header("받기 키 (임시)")]
        [SerializeField] private Key answerKey = Key.Z;
        [SerializeField, Min(0f)] private float answerDelay = 0.6f;

        [Header("대사 (event_dialogues)")]
        [SerializeField] private string questionId = "season_call_question";
        [SerializeField] private string confirmId = "season_call_confirm";
        [SerializeField] private string acceptId = "season_call_accept";
        [SerializeField] private string yesLabel = "예";
        [SerializeField] private string noLabel = "아니요";

        [Header("시즌 시작 시 race.id (지역 순서)")]
        [SerializeField] private string[] raceIds = { "race1", "race2", "race3", "final" };

        private Phase phase = Phase.Idle;
        private float ringStartedAt;
        private string chosenBoss;
        private int choiceResult = -1;
        private DialogueBox subscribedBox;

        public bool IsBusy => phase != Phase.Idle;
        public string ChosenBoss => chosenBoss;
        private IBossRaceProgress Progress => progressSource as IBossRaceProgress;

        public event Action<string> SeasonStarted;

        private void Awake()
        {
            if (ringPanel != null) ringPanel.SetActive(false);
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.ignoreListenerPause = true;
            if (ringClip == null) ringClip = CreateRingClip();
            if (pickUpClip == null) pickUpClip = CreatePickUpClip();
        }

        private void Start()
        {
            if (manager == null || selectUI == null || Progress == null)
            {
                Debug.LogError("[SeasonCall] Manager, Select UI, Progress Source(IBossRaceProgress) 중 연결되지 않은 칸이 있습니다.", this);
                enabled = false;
                return;
            }

            subscribedBox = manager.Box;
            if (subscribedBox != null) subscribedBox.Closed += OnBoxClosed;
        }

        private void OnDestroy()
        {
            if (subscribedBox != null) subscribedBox.Closed -= OnBoxClosed;
        }

        private void OnDisable()
        {
            if (phase == Phase.Idle) return;

            StopRing();
            if (ringPanel != null) ringPanel.SetActive(false);
            UiStack.Remove(this);
            phase = Phase.Idle;
        }

        public bool Ring()
        {
            if (!isActiveAndEnabled)
            {
                Debug.LogWarning("[SeasonCall] 비활성 상태라 전화를 걸 수 없습니다.", this);
                return false;
            }
            if (phase != Phase.Idle)
            {
                Debug.Log($"[SeasonCall] 이미 진행 중 ({phase})");
                return false;
            }

            phase = Phase.Waiting;
            Debug.Log("[SeasonCall] 시즌 종료 — 필드로 돌아오면 길드 전화가 울립니다");
            return true;
        }

        private void Update()
        {
            if (phase == Phase.Waiting)
            {
                if (DialogueEventQueue.IsFieldIdle(manager)) StartRinging();
                return;
            }

            if (phase != Phase.Ringing) return;

            float elapsed = Time.unscaledTime - ringStartedAt;
            if (phoneIcon != null)
            {
                float t = elapsed % RingPeriod;
                bool buzzing = t < 0.4f || (t >= 0.5f && t < 0.9f);
                phoneIcon.localRotation = Quaternion.Euler(0f, 0f, buzzing ? Mathf.Sin(Time.unscaledTime * 55f) * 14f : 0f);
            }

            Keyboard keyboard = Keyboard.current;
            if (elapsed >= answerDelay && keyboard != null && keyboard[answerKey].wasPressedThisFrame) Answer();
        }

        private void StartRinging()
        {
            phase = Phase.Ringing;
            ringStartedAt = Time.unscaledTime;
            UiStack.Push(this);

            if (answerHintText != null) answerHintText.text = $"[{answerKey}] 받기";
            if (ringPanel != null) ringPanel.SetActive(true);
            if (audioSource != null && ringClip != null)
            {
                audioSource.clip = ringClip;
                audioSource.loop = true;
                audioSource.Play();
            }

            Debug.Log($"[SeasonCall] 따르릉 — 시간 정지, [{answerKey}]로 받기");
        }

        private void Answer()
        {
            StopRing();
            if (audioSource != null && pickUpClip != null) audioSource.PlayOneShot(pickUpClip);
            if (ringPanel != null) ringPanel.SetActive(false);
            if (phoneIcon != null) phoneIcon.localRotation = Quaternion.identity;

            Debug.Log("[SeasonCall] 전화 받음 → 길드 접수원");
            phase = Phase.Asking;
            if (!manager.PlayDialogue(questionId))
            {
                Debug.LogWarning($"[SeasonCall] '{questionId}' 대사를 열지 못해 바로 지역 선택으로 넘어갑니다.", this);
                OpenSelect();
            }
        }

        private void OnBoxClosed()
        {
            switch (phase)
            {
                case Phase.Asking:
                    OpenSelect();
                    break;
                case Phase.Confirming:
                    HandleConfirm();
                    break;
                case Phase.Accepting:
                    Finish();
                    break;
            }
        }

        private void OpenSelect()
        {
            phase = Phase.Selecting;
            choiceResult = -1;
            if (!selectUI.Open(Progress, OnBossChosen, null, false))
            {
                Debug.LogError("[SeasonCall] 지역 보스 선택 UI를 열지 못했습니다. 첫 지역으로 시즌을 시작합니다.", this);
                chosenBoss = selectUI.Entries.Count > 0 ? selectUI.Entries[0].bossId : null;
                Finish();
            }
        }

        private void OnBossChosen(string bossId)
        {
            chosenBoss = bossId;
            BossRaceEntry entry = FindEntry(bossId);
            DialogueBox box = manager.Box;

            if (box != null)
            {
                box.SetTextArgument("boss", entry != null ? entry.bossName : bossId);
                box.SetTextArgument("region", entry != null ? entry.regionLabel : "");
            }

            phase = Phase.Confirming;
            choiceResult = -1;
            if (box != null) box.HoldOnLastLine(ShowChoice);

            if (!manager.PlayDialogue(confirmId))
            {
                Debug.LogWarning($"[SeasonCall] '{confirmId}' 대사를 열지 못해 선택지만 띄웁니다.", this);
                if (box != null) box.CancelHold();
                ShowChoice();
            }
        }

        private void ShowChoice()
        {
            if (choice == null || !choice.Show(new[] { yesLabel, noLabel }, OnChoice, 1))
            {
                Debug.LogWarning("[SeasonCall] 예/아니요 선택지가 없어 '예'로 진행합니다.", this);
                OnChoice(0);
            }
        }

        private void OnChoice(int index)
        {
            choiceResult = index;
            if (manager.Box != null && manager.Box.IsOpen) manager.Box.Close();
            else HandleConfirm();
        }

        private void HandleConfirm()
        {
            if (choiceResult == 0)
            {
                phase = Phase.Accepting;
                StartCoroutine(PlayAfterClosed(acceptId));
                return;
            }

            Debug.Log("[SeasonCall] 아니요 → 지역 보스 선택으로 돌아감");
            OpenSelect();
        }

        private System.Collections.IEnumerator PlayAfterClosed(string dialogueId)
        {
            yield return null;

            float waited = 0f;
            DialogueBox box = manager.Box;
            while (box != null && (box.IsOpen || box.IsClosingMotion) && waited < 1f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!manager.PlayDialogue(dialogueId)) Finish();
        }

        private void Finish()
        {
            phase = Phase.Idle;
            if (manager.Box != null) manager.Box.ClearTextArguments();

            StartSeason(chosenBoss);
            UiStack.Remove(this);
            SeasonStarted?.Invoke(chosenBoss);
        }

        private void StartSeason(string bossId)
        {
            BossRaceEntry entry = FindEntry(bossId);
            int index = IndexOf(bossId);
            DialogueVariables variables = manager.Variables;

            if (variables != null)
            {
                if (index >= 0 && index < raceIds.Length) variables.Set("race.id", raceIds[index]);
                variables.Set("race.stage", 0);
                variables.Set("race.seasonFirst", "no");
                variables.Set("season.day", 1);
                variables.Set("season.endingSoon", "no");
                manager.Save();
            }

            if (progressSource is Step09_DialogueDebug debug && !string.IsNullOrEmpty(bossId)) debug.SetTargetBoss(bossId);
            Debug.Log($"[SeasonCall] 새 시즌 시작 — {(entry != null ? $"{entry.regionLabel} {entry.bossName}" : bossId)} 레이스 (race.id={(index >= 0 && index < raceIds.Length ? raceIds[index] : "-")}, 시즌 1일차)");
        }

        private void StopRing()
        {
            if (audioSource != null && audioSource.isPlaying && audioSource.clip == ringClip) audioSource.Stop();
        }

        private BossRaceEntry FindEntry(string bossId)
        {
            int index = IndexOf(bossId);
            return index >= 0 ? selectUI.Entries[index] : null;
        }

        private int IndexOf(string bossId)
        {
            if (selectUI == null || string.IsNullOrEmpty(bossId)) return -1;
            for (int i = 0; i < selectUI.Entries.Count; i++)
            {
                if (selectUI.Entries[i] != null && selectUI.Entries[i].bossId == bossId) return i;
            }
            return -1;
        }

        private static AudioClip CreateRingClip()
        {
            const int rate = 44100;
            int length = Mathf.RoundToInt(rate * RingPeriod);
            var samples = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / rate;
                float start = t < 0.45f ? 0f : 0.5f;
                float local = t - start;
                if (local < 0f || local >= 0.4f || t >= 0.9f) continue;

                float frequency = (int)(t * 30f) % 2 == 0 ? 1320f : 1660f;
                float square = Mathf.Sin(2f * Mathf.PI * frequency * t) >= 0f ? 1f : -1f;
                float envelope = Mathf.Clamp01(local / 0.005f) * Mathf.Clamp01((0.4f - local) / 0.01f);
                samples[i] = square * 0.12f * envelope;
            }

            AudioClip clip = AudioClip.Create("SeasonCallRing", length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreatePickUpClip()
        {
            const int rate = 44100;
            int length = Mathf.RoundToInt(rate * 0.12f);
            var samples = new float[length];
            var random = new System.Random(11);

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                float click = ((float)random.NextDouble() * 2f - 1f) * (1f - t) * (1f - t);
                float tone = Mathf.Sin(2f * Mathf.PI * 520f * i / rate) * (1f - t);
                samples[i] = (click * 0.25f + tone * 0.15f) * (t < 0.5f ? 1f : 0.6f);
            }

            AudioClip clip = AudioClip.Create("SeasonCallPickUp", length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
