using UnityEngine;
using UnityEngine.InputSystem;
using RealSteel.Boss.Kyosho;

namespace RealSteel.Boss.Test
{
    /// <summary>
    /// [3보스 '쿄쇼' 통합 테스트 디버그 러너]
    /// - F1~F5: 난이도 전환 (아주 쉬움 ~ 헬)
    /// - F6: HP 60% 도달 (변주곡 ① ➔ 2페 진입 테스트)
    /// - F7: HP 30% 도달 (변주곡 ② 테스트)
    /// - F8: HP 10% 도달 (피날레 테스트)
    /// - F9: 최후의 일격 대기 중 즉시 마무리 타격 테스트
    /// - F10: 하단 박자 표시기 On/Off 토글
    /// </summary>
    public class KyoshoGimmickTester : MonoBehaviour
    {
        [SerializeField] private KyoshoBossController kyosho;
        [SerializeField] private bool showOverlay = true;

        private void Start()
        {
            if (kyosho == null) kyosho = FindFirstObjectByType<KyoshoBossController>();
        }

        private void Update()
        {
            if (kyosho == null) kyosho = FindFirstObjectByType<KyoshoBossController>();
            if (kyosho == null || kyosho.Snapshot == null) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f1Key.wasPressedThisFrame) kyosho.SetDifficulty(BossDifficulty.VeryEasy);
            else if (kb.f2Key.wasPressedThisFrame) kyosho.SetDifficulty(BossDifficulty.Easy);
            else if (kb.f3Key.wasPressedThisFrame) kyosho.SetDifficulty(BossDifficulty.Normal);
            else if (kb.f4Key.wasPressedThisFrame) kyosho.SetDifficulty(BossDifficulty.Hard);
            else if (kb.f5Key.wasPressedThisFrame) kyosho.SetDifficulty(BossDifficulty.Hell);
            else if (kb.f6Key.wasPressedThisFrame) kyosho.TakeDamage(Mathf.RoundToInt(kyosho.Snapshot.maxHp * 0.42f), true);
            else if (kb.f7Key.wasPressedThisFrame) kyosho.TakeDamage(Mathf.RoundToInt(kyosho.Snapshot.maxHp * 0.35f), true);
            else if (kb.f8Key.wasPressedThisFrame) kyosho.TakeDamage(Mathf.RoundToInt(kyosho.Snapshot.maxHp * 0.25f), true);
            else if (kb.f9Key.wasPressedThisFrame) kyosho.TakeDamage(50, true);
            else if (kb.f10Key.wasPressedThisFrame)
            {
                var hud = kyosho.GetComponent<KyoshoBeatIndicatorHUD>();
                if (hud != null) hud.ShowIndicator = !hud.ShowIndicator;
            }
        }

        private void OnGUI()
        {
            if (!showOverlay || kyosho == null || kyosho.Snapshot == null) return;

            GUILayout.BeginArea(new Rect(14, 14, 340, 150), GUI.skin.box);
            GUILayout.Label($"<b>[3보스 쿄쇼]</b> {kyosho.PhaseState} | {kyosho.SelectedDifficulty} ({kyosho.BeatClock?.Bpm:F0} BPM)");
            GUILayout.Label($"HP: {kyosho.Snapshot.currentHp} / {kyosho.Snapshot.maxHp} | 무적: {kyosho.IsInvincible} | 일격대기: {kyosho.IsWaitingFinalBlow}");
            GUILayout.Label($"상태: {kyosho.StateMachine?.CurrentState?.StateName}");
            GUILayout.Label("F1~F5: 난이도 | F6: 60%(변주①) | F7: 30%(변주②) | F8: 10%(피날레) | F9: 타격");
            GUILayout.EndArea();
        }
    }
}
