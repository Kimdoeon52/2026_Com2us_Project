using System;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RealSteel.Boss.Core;
using RealSteel.Boss.FSM;
using RealSteel.Boss.JunkWheeler;

namespace RealSteel.Boss.Test
{
    /// <summary>
    /// [정크 휠러 기믹 및 난이도 레이어 통합 테스터 (JunkWheelerGimmickTester)]
    /// 기획서 §6.18, §6.10.4 및 §6.10.5 검증용 도구:
    /// 1. 5단계 난이도 실시간 전환 (아주 쉬움, 쉬움, 보통, 어려움, 헬)
    /// 2. 4대 패턴 강제 실행 (돌진, 반원 탄막, 3단 내려찍기, 회전 톱날 당구)
    /// 3. 전장 기믹 파훼 시뮬레이션 (고철 못판/Spike 솟구침 ➔ 타이어 펑크 & 4초 그로기)
    /// 4. 폭발 화물 상자 충돌 시뮬레이션 (2초 스턴)
    /// 5. 타이어 부위 내구도 차감 및 펑크 2회 누적 시 영구 약화 디버프 검증
    /// 6. 2페이즈 전환(HP 50%), 과열 가속 및 헬 배기 잔화 화염 장판 검증
    /// 7. 난이도별 승패 정산 시뮬레이션 (점수, 처치 골드, 신청비, 코어 EXP, 영구 파괴율)
    /// 8. 화면 OnGUI 및 단축키 제공
    /// </summary>
    public class JunkWheelerGimmickTester : MonoBehaviour
    {
        [Header("=== 타겟 보스 컨트롤러 ===")]
        [Tooltip("비워둘 경우 씬 내의 JunkWheelerBossController를 자동 탐색함")]
        [SerializeField] private JunkWheelerBossController targetBoss;

        [Header("=== GUI 표시 설정 ===")]
        [SerializeField] private bool showOnScreenGui = true;
        [SerializeField] private KeyCode toggleGuiKey = KeyCode.BackQuote; // ` 키로 GUI 토글

        private NailBoardFieldGimmick _sceneNailBoard;
        private ExplosiveBoxFieldGimmick _sceneExplosiveBox;
        private string _lastSettlementSummary = "";

        private void Awake()
        {
            FindComponents();
        }

        private void Start()
        {
            PrintUsageGuide();
        }

        private void Update()
        {
            // GUI 온오프 토글
            if (Input.GetKeyDown(toggleGuiKey))
            {
                showOnScreenGui = !showOnScreenGui;
            }

            if (targetBoss == null)
            {
                FindComponents();
                if (targetBoss == null) return;
            }

            // ====================================================================
            // 단축키 매핑 (HUD 1~5번 및 레거시 F1~F6 키 충돌 회피)
            // ====================================================================

            // [6/B]: 회전 톱날 당구 돌진 강제 실행 (PATTERN_BLADE_RICOCHET)
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.B))
            {
                ForceExecuteBladeRicochet();
            }

            // [7]: 돌진 패턴 강제 실행 (PATTERN_CHARGE)
            if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7))
            {
                ForceExecutePattern("PATTERN_CHARGE");
            }

            // [8]: 바이킹 순차 반원 탄막 강제 실행 (PATTERN_JUMP_BARRAGE)
            if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8))
            {
                ForceExecutePattern("PATTERN_JUMP_BARRAGE");
            }

            // [9]: 3단 내려찍기 강제 실행 (PATTERN_SLAM)
            if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9))
            {
                ForceExecutePattern("PATTERN_SLAM");
            }

            // [0]: 고철 못판 착지 기믹 파훼 시뮬레이션 (타이어 펑크 + 4초 그로기)
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
            {
                SimulateNailBoardLanding();
            }

            // [I]: 폭발 화물 상자 충돌 시뮬레이션 (2초 스턴)
            if (Input.GetKeyDown(KeyCode.I))
            {
                SimulateExplosiveBoxCollision();
            }

            // [P]: 타이어 부위 1회 펑크 (관통 태그 내구도 차감)
            if (Input.GetKeyDown(KeyCode.P))
            {
                PunctureTireOnce();
            }

            // [O]: 타이어 부위 2회 펑크 ➔ 영구 파괴 및 속도 감속 강제 적용
            if (Input.GetKeyDown(KeyCode.O))
            {
                BreakTireCompletely();
            }

            // [U]: 2페이즈 강제 전환 (체력 50% 이하 트리거)
            if (Input.GetKeyDown(KeyCode.U))
            {
                ForcePhase2Transition();
            }

            // [L]: 실시간 상세 스냅샷 및 기믹 상태 콘솔 로그 출력
            if (Input.GetKeyDown(KeyCode.L))
            {
                PrintDetailedStatus();
            }
        }

        private void FindComponents()
        {
            if (targetBoss == null)
            {
                targetBoss = FindFirstObjectByType<JunkWheelerBossController>();
            }

            _sceneNailBoard = FindFirstObjectByType<NailBoardFieldGimmick>();
            _sceneExplosiveBox = FindFirstObjectByType<ExplosiveBoxFieldGimmick>();
        }

        private void PrintUsageGuide()
        {
            Debug.Log("<color=cyan><b>[JunkWheelerGimmickTester 가이드]</b>\n" +
                      "` 키: OnGUI 창 토글 | 6: 당구 톱날 | 7: 돌진 | 8: 탄막 | 9: 찍기 | 0: 못판 착지\n" +
                      "I: 폭발 상자 | P: 타이어 1회 펑크 | O: 타이어 파괴 | U: 2페이즈 | L: 상태 출력</color>");
        }

        // ========================================================================
        // 1. 난이도 전환 제어
        // ========================================================================

        public void SetDifficulty(BossDifficulty diff)
        {
            if (targetBoss != null)
            {
                targetBoss.SetDifficulty(diff);
            }

            var bm = BattleManager.Instance;
            if (bm != null)
            {
                bm.SetBossDifficulty(diff);
            }
        }

        // ========================================================================
        // 2. 공격 패턴 강제 시전
        // ========================================================================

        [ContextMenu("[패턴] 회전 톱날 당구 돌진 강제 실행 (PATTERN_BLADE_RICOCHET)")]
        public void ForceExecuteBladeRicochet()
        {
            ForceExecutePattern(JunkWheelerRicochetBladePatternGimmick.PatternRicochetBladeID);
        }

        [ContextMenu("[패턴] 돌진 패턴 강제 실행 (PATTERN_CHARGE)")]
        public void ForceExecuteCharge()
        {
            ForceExecutePattern("PATTERN_CHARGE");
        }

        [ContextMenu("[패턴] 바이킹 탄막 패턴 강제 실행 (PATTERN_JUMP_BARRAGE)")]
        public void ForceExecuteBarrage()
        {
            ForceExecutePattern("PATTERN_JUMP_BARRAGE");
        }

        [ContextMenu("[패턴] 3단 내려찍기 패턴 강제 실행 (PATTERN_SLAM)")]
        public void ForceExecuteSlam()
        {
            ForceExecutePattern("PATTERN_SLAM");
        }

        public void ForceExecutePattern(string patternID)
        {
            if (targetBoss == null)
            {
                Debug.LogWarning("[Tester] 타겟 보스 컨트롤러가 할당되지 않았습니다.");
                return;
            }

            var gimmick = targetBoss.GetGimmick<BossPatternGimmickBase>(patternID);
            if (gimmick == null)
            {
                Debug.LogWarning($"[Tester] ID가 '{patternID}'인 패턴 기믹을 찾을 수 없습니다.");
                return;
            }

            Debug.Log($"<color=orange><b>[Tester] ▶ [패턴 강제 실행] {gimmick.GimmickName} ({patternID})</b></color>");
            var token = targetBoss.RenewStateCancellationToken();
            targetBoss.StateMachine.ChangeState(new BossPatternExecutingState(targetBoss, token, gimmick));
        }

        // ========================================================================
        // 3. 기믹 파훼 및 부위 조작
        // ========================================================================

        [ContextMenu("[기믹] 못판 착지 기믹 파훼 (타이어 펑크 + 4초 그로기)")]
        public void SimulateNailBoardLanding()
        {
            if (targetBoss == null) return;

            Debug.Log("<color=magenta><b>[Tester] ▶ [기믹 파훼] 고철 못판 착지 시뮬레이션 개시!</b></color>");

            if (_sceneNailBoard == null || _sceneNailBoard.IsConsumed)
            {
                var boards = FindObjectsByType<NailBoardFieldGimmick>(FindObjectsSortMode.None);
                _sceneNailBoard = Array.Find(boards, b => b != null && b.IsInteractable && !b.IsConsumed);
            }

            if (_sceneNailBoard != null && _sceneNailBoard.IsInteractable && !_sceneNailBoard.IsConsumed)
            {
                _sceneNailBoard.TriggerBySlam(targetBoss.gameObject);
                return;
            }

            // 씬에 못판이 없더라도 가상으로 기믹 파훼 로직 직접 검증
            var tireGimmick = targetBoss.GetGimmick<JunkWheelerTirePartGimmick>("TIRE_PART");
            if (tireGimmick != null)
            {
                tireGimmick.TakePartDamage(1, GimmickTag.Pierce);
            }

            targetBoss.InterruptToGroggy(4.0f, 1.5f);
        }

        [ContextMenu("[못판] 스파이크 솟구침 애니메이션 테스트 (TriggerEmerge)")]
        public void TestSpikeEmerge()
        {
            if (_sceneNailBoard == null) _sceneNailBoard = FindFirstObjectByType<NailBoardFieldGimmick>();
            if (_sceneNailBoard != null)
            {
                Debug.Log("<color=cyan>[Tester] 못판(스파이크) 솟구침 애니메이션 실행!</color>");
                _sceneNailBoard.TriggerEmerge();
            }
        }

        [ContextMenu("[못판] 스파이크 하강 복귀 애니메이션 테스트 (PlayRetract)")]
        public void TestSpikeRetract()
        {
            if (_sceneNailBoard == null) _sceneNailBoard = FindFirstObjectByType<NailBoardFieldGimmick>();
            if (_sceneNailBoard != null)
            {
                Debug.Log("<color=gray>[Tester] 못판(스파이크) 하강 복귀 애니메이션 실행!</color>");
                _sceneNailBoard.PlayRetractAsync().Forget();
            }
        }

        [ContextMenu("[폭발] 폭발 화물 상자 충돌 시뮬레이션 (2초 스턴)")]
        public void SimulateExplosiveBoxCollision()
        {
            if (targetBoss == null) return;

            Debug.Log("<color=red><b>[Tester] ▶ [폭발 기믹] 폭발 화물 상자 충돌 시뮬레이션 개시!</b></color>");

            if (_sceneExplosiveBox != null && _sceneExplosiveBox.IsInteractable)
            {
                _sceneExplosiveBox.TriggerInteraction(targetBoss.gameObject);
                return;
            }

            // 폴백 가상 시뮬레이션: 150 피해 및 2초 스턴
            targetBoss.TakeDamage(150);
            targetBoss.InterruptToStun(2.0f);
        }

        [ContextMenu("[타이어] 타이어 1회 펑크 (관통 피해 1)")]
        public void PunctureTireOnce()
        {
            if (targetBoss == null) return;

            var tireGimmick = targetBoss.GetGimmick<JunkWheelerTirePartGimmick>("TIRE_PART");
            if (tireGimmick == null)
            {
                Debug.LogWarning("[Tester] 타이어 부위 기믹(TIRE_PART)을 찾을 수 없습니다.");
                return;
            }

            Debug.Log("<color=yellow>[Tester] 타이어 부위에 관통(Pierce) 피해 1 차감!</color>");
            tireGimmick.TakePartDamage(1, GimmickTag.Pierce);
        }

        [ContextMenu("[타이어] 타이어 완전 파괴 (2회 펑크 및 영구 약화)")]
        public void BreakTireCompletely()
        {
            if (targetBoss == null) return;

            var tireGimmick = targetBoss.GetGimmick<JunkWheelerTirePartGimmick>("TIRE_PART");
            if (tireGimmick == null) return;

            Debug.Log("<color=red><b>[Tester] 타이어 2회 펑크 누적 ➔ 영구 파괴 디버프 강제 적용!</b></color>");
            while (!tireGimmick.IsPermanentlyBroken)
            {
                tireGimmick.TakePartDamage(1, GimmickTag.Pierce);
            }
        }

        // ========================================================================
        // 4. 페이즈 전환 및 상태 제어
        // ========================================================================

        [ContextMenu("[페이즈] 페이즈 2 전환 (HP 50% 설정)")]
        public void ForcePhase2Transition()
        {
            if (targetBoss == null || targetBoss.Snapshot == null) return;

            if (!targetBoss.Snapshot.allowPhase2)
            {
                Debug.LogWarning($"[Tester] 현재 난이도({targetBoss.Snapshot.difficulty})는 기획서상 2페이즈를 지원하지 않습니다! (어려움 또는 헬 난이도 필요)");
                return;
            }

            int targetHp = Mathf.FloorToInt(targetBoss.Snapshot.maxHp * 0.49f);
            int damage = Mathf.Max(0, targetBoss.Snapshot.currentHp - targetHp);

            Debug.Log($"<color=orange>[Tester] 체력을 50% 이하({targetHp} HP)로 감소시켜 2페이즈 전이를 트리거합니다. (피해: {damage})</color>");
            targetBoss.TakeDamage(damage);
        }

        [ContextMenu("[상태] 1.0초 확정 딜타임(Idle) 복귀")]
        public void ResetToIdle()
        {
            if (targetBoss == null) return;

            Debug.Log("<color=white>[Tester] 보스를 확정 딜타임(BossIdleState)으로 복귀시킵니다.</color>");
            var token = targetBoss.RenewStateCancellationToken();
            targetBoss.StateMachine.ChangeState(new BossIdleState(targetBoss, token, 1.0f));
        }

        // ========================================================================
        // 5. 난이도별 정산 시뮬레이션
        // ========================================================================

        public void TestSettlement(bool playerWon)
        {
            var hub = CombatDataHub.Instance;
            CombatantSnapshot player = hub?.PlayerSnapshot;
            CombatantSnapshot enemy = hub?.EnemySnapshot;
            BossMasterData bossMaster = targetBoss != null ? targetBoss.MasterData : null;
            BossDifficulty diff = targetBoss != null ? targetBoss.SelectedDifficulty : BossDifficulty.Normal;

            var res = BattleSettlementProcessor.ProcessSettlement(
                playerWon, player, enemy, 100,
                bossMaster != null ? bossMaster.regionBasePrice : 500,
                player != null ? player.coreMasterData : null,
                hub != null ? hub.GetPartMasterLookup() : null,
                bossMaster,
                diff
            );

            _lastSettlementSummary = res.SummaryMessage;
            Debug.Log($"<color=green>[Tester 정산 결과] {res.SummaryMessage}</color>");
        }

        [ContextMenu("[상태] 보스 및 기믹 전체 상세 상태 출력")]
        public void PrintDetailedStatus()
        {
            if (targetBoss == null || targetBoss.Snapshot == null)
            {
                Debug.LogWarning("[Tester] 타겟 보스 또는 스냅샷이 null입니다.");
                return;
            }

            var s = targetBoss.Snapshot;
            var tire = targetBoss.GetGimmick<JunkWheelerTirePartGimmick>("TIRE_PART");

            var sb = new StringBuilder();
            sb.AppendLine("<b><color=white>================ [정크 휠러 실시간 스냅샷] ================</color></b>");
            sb.AppendLine($"▶ 보스명: {s.bossName} ({s.bossID}) | 난이도: {s.difficulty}");
            sb.AppendLine($"▶ 코어 체력: {s.currentHp} / {s.maxHp} ({((float)s.currentHp / s.maxHp * 100f):F1}%)");
            sb.AppendLine($"▶ 공격배율: {s.attackMultiplier}x | 전조배율: {s.tellDurationMultiplier}x");
            sb.AppendLine($"▶ 허용 패턴수: {s.maxAllowedPatterns}종 | 필드 기믹수: {s.maxActiveGimmicks}개");
            sb.AppendLine($"▶ 2페이즈 허용: {s.allowPhase2} | 패턴 연계: {s.allowPatternChain} | 패시브 수: {s.passiveCount}");
            sb.AppendLine($"▶ FSM 현재 상태: {targetBoss.StateMachine?.CurrentState?.StateName ?? "None"}");
            sb.AppendLine($"▶ 무력화 상태: 그로기 = {s.isGroggy} (피해 증폭 {s.currentDamageMultiplier}배)");

            if (tire != null)
            {
                sb.AppendLine($"▶ 폐타이어 부위: 내구도 {tire.CurrentDurability}/{tire.MaxDurability} | 누적 펑크: {tire.BreakCount}/2 | 영구 파괴: {tire.IsPermanentlyBroken}");
            }

            sb.AppendLine("<b><color=white>=========================================================</color></b>");
            Debug.Log(sb.ToString());
        }

        // ========================================================================
        // 6. 화면 OnGUI 대시보드
        // ========================================================================

        private void OnGUI()
        {
            if (!showOnScreenGui) return;

            GUILayout.BeginArea(new Rect(20, 20, 310, 710), "<b>[정크 휠러 난이도 & 기믹 테스터]</b>", GUI.skin.window);
            GUILayout.Space(2);

            if (targetBoss == null)
            {
                GUILayout.Label("<color=red>보스 컨트롤러를 찾을 수 없습니다!</color>");
                if (GUILayout.Button("보스 자동 재탐색", GUILayout.Height(26))) FindComponents();
                GUILayout.EndArea();
                return;
            }

            var s = targetBoss.Snapshot;
            var curState = targetBoss.StateMachine?.CurrentState?.StateName ?? "None";
            var tire = targetBoss.GetGimmick<JunkWheelerTirePartGimmick>("TIRE_PART");

            // 1. 상태 요약 모니터
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<b>난이도:</b> <color=cyan><b>{s?.difficulty}</b></color> (Phase {s?.currentPhase ?? 1})");
            GUILayout.Label($"<b>HP:</b> {s?.currentHp ?? 0} / {s?.maxHp ?? 0} | <b>공격:</b> {s?.attackMultiplier ?? 1f:F1}x");
            GUILayout.Label($"<b>규칙:</b> 전조 {s?.tellDurationMultiplier ?? 1f:F1}x | 패턴 {s?.maxAllowedPatterns ?? 4}종 | 기믹 {s?.maxActiveGimmicks ?? 2}개");
            GUILayout.Label($"<b>페이즈2:</b> {(s?.allowPhase2 == true ? "개방" : "미지원")} | <b>패시브:</b> {s?.passiveCount ?? 0}종");
            GUILayout.Label($"<b>상태:</b> <size=10>{curState}</size>");
            if (tire != null)
            {
                string tireColor = tire.IsPermanentlyBroken ? "red" : (tire.BreakCount > 0 ? "yellow" : "white");
                GUILayout.Label($"<b>타이어:</b> <color={tireColor}>내구 {tire.CurrentDurability}/{tire.MaxDurability} (펑크 {tire.BreakCount}회)</color>");
            }
            if (s != null && s.isGroggy)
            {
                GUILayout.Label("<color=magenta><b>[그로기 취약 중 (피해 1.5배)]</b></color>");
            }
            GUILayout.EndVertical();

            // 2. 난이도 5단계 선택 버튼
            GUILayout.Space(2);
            GUILayout.Label("<b>[난이도 5단계 전환 (기획서 §6.10.4)]</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("아주쉬움", GUILayout.Height(24))) SetDifficulty(BossDifficulty.VeryEasy);
            if (GUILayout.Button("쉬움", GUILayout.Height(24))) SetDifficulty(BossDifficulty.Easy);
            if (GUILayout.Button("보통", GUILayout.Height(24))) SetDifficulty(BossDifficulty.Normal);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUI.color = new Color(1f, 0.7f, 0.4f, 1f);
            if (GUILayout.Button("어려움 (P2 과열)", GUILayout.Height(24))) SetDifficulty(BossDifficulty.Hard);
            GUI.color = new Color(1f, 0.4f, 0.4f, 1f);
            if (GUILayout.Button("헬 (배기잔화+연계)", GUILayout.Height(24))) SetDifficulty(BossDifficulty.Hell);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            // 3. 공격 패턴 강제 시전
            GUILayout.Space(2);
            GUILayout.Label("<b>[공격 패턴 강제 시전]</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("[6] 당구", GUILayout.Height(24))) ForceExecuteBladeRicochet();
            if (GUILayout.Button("[7] 돌진", GUILayout.Height(24))) ForceExecuteCharge();
            if (GUILayout.Button("[8] 탄막", GUILayout.Height(24))) ForceExecuteBarrage();
            if (GUILayout.Button("[9] 찍기", GUILayout.Height(24))) ForceExecuteSlam();
            GUILayout.EndHorizontal();

            // 4. 기믹 파훼 & 인터럽트
            GUILayout.Space(2);
            GUILayout.Label("<b>[기믹 파훼 & 스파이크 제어]</b>");
            GUI.color = new Color(1f, 0.7f, 1f, 1f);
            if (GUILayout.Button("[0] 못판 착지 (4초 그로기 & 펑크)", GUILayout.Height(26))) SimulateNailBoardLanding();
            GUI.color = new Color(1f, 0.8f, 0.6f, 1f);
            if (GUILayout.Button("[I] 폭발 상자 충돌 (2초 스턴)", GUILayout.Height(24))) SimulateExplosiveBoxCollision();
            GUI.color = Color.white;

            GUILayout.BeginHorizontal();
            GUI.color = new Color(0.4f, 1f, 0.9f, 1f);
            if (GUILayout.Button("스파이크 솟구침", GUILayout.Height(22))) TestSpikeEmerge();
            if (GUILayout.Button("스파이크 복귀", GUILayout.Height(22))) TestSpikeRetract();
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("[P] 타이어 펑크", GUILayout.Height(22))) PunctureTireOnce();
            if (GUILayout.Button("[O] 타이어 파괴", GUILayout.Height(22))) BreakTireCompletely();
            GUILayout.EndHorizontal();

            // 5. 전투 제어 및 정산 테스트
            GUILayout.Space(2);
            GUILayout.Label("<b>[전투 및 정산 시뮬레이션]</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("[U] 2페이즈 전환", GUILayout.Height(22))) ForcePhase2Transition();
            if (GUILayout.Button("[복귀] Idle 복귀", GUILayout.Height(22))) ResetToIdle();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.color = new Color(0.6f, 1f, 0.6f, 1f);
            if (GUILayout.Button("승리 정산 테스트", GUILayout.Height(24))) TestSettlement(true);
            GUI.color = new Color(1f, 0.6f, 0.6f, 1f);
            if (GUILayout.Button("패배 정산 테스트", GUILayout.Height(24))) TestSettlement(false);
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_lastSettlementSummary))
            {
                GUILayout.Label($"<size=9><color=yellow>{_lastSettlementSummary}</color></size>");
            }

            GUILayout.Space(2);
            if (GUILayout.Button("[L] 상세 스냅샷 콘솔 출력", GUILayout.Height(22))) PrintDetailedStatus();
            GUILayout.Label("<size=9><color=gray>[`] 키를 눌러 이 창을 접거나 펼 수 있습니다.</color></size>");

            GUILayout.EndArea();
        }
    }
}
