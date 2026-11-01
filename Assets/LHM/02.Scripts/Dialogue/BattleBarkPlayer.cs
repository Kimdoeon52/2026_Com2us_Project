using System.Collections.Generic;
using RealSteel.Boss.Core;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public enum BattleBarkEvent
    {
        PlayerPartBroken,
        BossPartBroken,
        BossGroggy,
    }

    public class BattleBarkPlayer : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private DialogueManager manager;
        [SerializeField] private BattleBarkSubtitle subtitle;
        [SerializeField] private string poolId = "battle_bark";

        [Header("전투 연동")]
        [SerializeField] private bool listenCombat = true;
        [SerializeField, Min(0.1f)] private float searchInterval = 1f;
        [SerializeField, Min(0f)] private float minInterval = 0.8f;

        private readonly HashSet<string> brokenBossParts = new HashSet<string>();
        private CombatDataHub hub;
        private BossControllerBase boss;
        private BossSnapshot watchedSnapshot;
        private float nextSearchAt;
        private float lastBarkAt = float.NegativeInfinity;
        private int playerBrokenCount;

        public int PlayerBrokenCount => playerBrokenCount;

        private void OnDisable()
        {
            UnbindHub();
            UnbindBoss();
        }

        private void Update()
        {
            if (!listenCombat) return;

            if (Time.unscaledTime >= nextSearchAt)
            {
                nextSearchAt = Time.unscaledTime + searchInterval;
                SearchCombat();
            }

            PollBossParts();
        }

        public void NotifyPlayerPartBroken(BodyPart part)
        {
            playerBrokenCount++;
            Bark(BattleBarkEvent.PlayerPartBroken, part.ToString(), "", -1);
        }

        public bool PlaySubtitleDialogue(string dialogueId)
        {
            if (manager == null || subtitle == null || !manager.IsReady || manager.IsFallback) return false;

            if (!manager.TryPlayLines(dialogueId, "전투 자막", out List<DialogueLine> lines)) return false;

            subtitle.ShowSequence(lines);
            Debug.Log($"[Bark] 전투 자막 {dialogueId} ({lines.Count}줄) — 끝날 때까지 바크 보류");
            return true;
        }

        public void NotifyBossPartBroken(string gimmickId)
        {
            Bark(BattleBarkEvent.BossPartBroken, "none", gimmickId, -1);
        }

        public void NotifyBossGroggy(int bossHpPercentOverride = -1)
        {
            Bark(BattleBarkEvent.BossGroggy, "none", "", bossHpPercentOverride);
        }

        public void ResetBattle()
        {
            playerBrokenCount = 0;
            lastBarkAt = float.NegativeInfinity;
            brokenBossParts.Clear();
            watchedSnapshot = null;
            if (subtitle != null) subtitle.Hide();
            Debug.Log("[Bark] 전투 기록 초기화 (파괴된 파츠 수 0)");
        }

        private bool Bark(BattleBarkEvent battleEvent, string brokenPart, string bossPartId, int bossHpOverride)
        {
            if (manager == null || subtitle == null)
            {
                Debug.LogWarning("[Bark] Manager 또는 Subtitle이 연결되지 않았습니다. 14단계 배치 메뉴를 실행하세요.", this);
                return false;
            }

            if (!manager.IsReady || manager.IsFallback) return false;

            if (manager.IsTalking)
            {
                Debug.Log($"[Bark] {battleEvent} 무시 — 대화창이 열려 있음");
                return false;
            }

            if (subtitle.IsSequencePlaying)
            {
                Debug.Log($"[Bark] {battleEvent} 무시 — 전투 자막(튜토리얼 등) 재생 중");
                return false;
            }

            float now = Time.unscaledTime;
            if (now - lastBarkAt < minInterval)
            {
                Debug.Log($"[Bark] {battleEvent} 무시 — 직전 바크 후 {minInterval}초 이내");
                return false;
            }

            DialogueVariables variables = manager.Variables;
            int hpPercent = bossHpOverride >= 0 ? bossHpOverride : BossHpPercent();

            variables.Set("battle.event", EventValue(battleEvent));
            variables.Set("battle.brokenPart", string.IsNullOrEmpty(brokenPart) ? "none" : brokenPart);
            variables.Set("battle.bossPartId", bossPartId ?? "");
            variables.Set("battle.playerBrokenCount", playerBrokenCount);
            variables.Set("battle.bossHpPercent", hpPercent);

            bool found = manager.TryBark(poolId, out DialogueLine line);
            ResetMoment(variables);

            if (!found) return false;

            lastBarkAt = now;
            subtitle.Show(line.speaker, line.text);
            Debug.Log($"[Bark] {battleEvent} (부위 {brokenPart}, 보스 부위 '{bossPartId}', 파괴 수 {playerBrokenCount}, 보스 체력 {hpPercent}%) → {(string.IsNullOrEmpty(line.speaker) ? "(내레이션)" : line.speaker)}: {line.text}");
            return true;
        }

        private static void ResetMoment(DialogueVariables variables)
        {
            variables.Set("battle.event", "none");
            variables.Set("battle.brokenPart", "none");
            variables.Set("battle.bossPartId", "");
            variables.Set("battle.playerBrokenCount", 0);
            variables.Set("battle.bossHpPercent", 100);
        }

        private static string EventValue(BattleBarkEvent battleEvent)
        {
            string name = battleEvent.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private BossSnapshot CurrentBossSnapshot()
        {
            if (boss != null && boss.Snapshot != null) return boss.Snapshot;
            return hub != null && hub.CurrentBattleMode == BattleMode.RobotVsBoss ? hub.BossSnapshot : null;
        }

        private int BossHpPercent()
        {
            BossSnapshot snapshot = CurrentBossSnapshot();
            if (snapshot == null || snapshot.maxHp <= 0) return 100;
            return Mathf.Clamp(Mathf.RoundToInt(snapshot.currentHp * 100f / snapshot.maxHp), 0, 100);
        }

        private void SearchCombat()
        {
            if (hub == null)
            {
                UnbindHub();
                CombatDataHub found = FindAnyObjectByType<CombatDataHub>();
                if (found != null) BindHub(found);
            }

            if (boss == null)
            {
                UnbindBoss();
                BossControllerBase found = FindAnyObjectByType<BossControllerBase>();
                if (found != null) BindBoss(found);
            }
        }

        private void BindHub(CombatDataHub target)
        {
            hub = target;
            hub.OnPlayerPartBroken += OnHubPlayerPartBroken;
            hub.OnBossGimmickBroken += OnHubBossGimmickBroken;
            playerBrokenCount = 0;
            Debug.Log($"[Bark] 전투 데이터 허브 연결 ({hub.name}, {hub.CurrentBattleMode})");
        }

        private void UnbindHub()
        {
            if (ReferenceEquals(hub, null)) return;

            hub.OnPlayerPartBroken -= OnHubPlayerPartBroken;
            hub.OnBossGimmickBroken -= OnHubBossGimmickBroken;
            hub = null;
        }

        private void BindBoss(BossControllerBase target)
        {
            boss = target;
            boss.OnGroggyChanged += OnBossGroggyChanged;
            watchedSnapshot = null;
            Debug.Log($"[Bark] 보스 연결 ({boss.name})");
        }

        private void UnbindBoss()
        {
            if (ReferenceEquals(boss, null)) return;

            boss.OnGroggyChanged -= OnBossGroggyChanged;
            boss = null;
        }

        private void PollBossParts()
        {
            BossSnapshot snapshot = CurrentBossSnapshot();
            if (snapshot == null || snapshot.gimmickStates == null) return;

            bool fresh = snapshot != watchedSnapshot;
            if (fresh)
            {
                watchedSnapshot = snapshot;
                brokenBossParts.Clear();
            }

            foreach (BossGimmickRuntimeState state in snapshot.gimmickStates.Values)
            {
                if (state == null || !state.isPhysicalBossPart) continue;

                if (!state.isBroken)
                {
                    brokenBossParts.Remove(state.gimmickID);
                }
                else if (brokenBossParts.Add(state.gimmickID) && !fresh)
                {
                    NotifyBossPartBroken(state.gimmickID);
                }
            }
        }

        private void OnHubPlayerPartBroken(BodyPart part)
        {
            NotifyPlayerPartBroken(part);
        }

        private void OnHubBossGimmickBroken(string gimmickId)
        {
            BossGimmickRuntimeState state = CurrentBossSnapshot()?.GetGimmick(gimmickId);
            if (state != null && !state.isPhysicalBossPart) return;

            if (brokenBossParts.Add(gimmickId)) NotifyBossPartBroken(gimmickId);
        }

        private void OnBossGroggyChanged(bool groggy, float duration)
        {
            if (groggy) NotifyBossGroggy();
        }
    }
}
