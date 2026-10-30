using System;
using System.Text;
using UnityEngine;
using RealSteel.Boss.Core;
using RealSteel.Boss.FSM;
using RealSteel.Boss.JunkWheeler;

namespace RealSteel.Boss.Test
{
    /// <summary>
    /// [정크 휠러 기믹 및 신규 아키텍처 통합 테스터 (JunkWheelerGimmickTester)]
    /// 기획서 §6.18 및 리팩토링 Phase 3 검증용 도구:
    /// 1. 3대 패턴 강제 실행 (돌진, 반원 탄막, 3단 내려찍기)
    /// 2. 전장 기믹 파훼 시뮬레이션 (고철 못판 착지 ➔ 타이어 펑크 & 4초 그로기)
    /// 3. 폭발 화물 상자 충돌 시뮬레이션 (2초 스턴)
    /// 4. 타이어 부위 내구도 차감 및 펑크 2회 누적 시 영구 약화 디버프 검증
    /// 5. 2페이즈 전환(HP 50%) 및 엔진 과열 불꽃 VFX 가동 검증
    /// 6. 화면 OnGUI 및 단축키(숫자 7~0, I, O, P, U, L) 제공
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

            // [O]: 타이어 영구 파괴 (펑크 2회 누적 및 스펙 약화 즉시 발동)
            if (Input.GetKeyDown(KeyCode.O))
            {
                BreakTireCompletely();
            }

            // [U]: 체력 50% 설정 ➔ 2페이즈 전환 강제 유도
            if (Input.GetKeyDown(KeyCode.U))
            {
                ForcePhase2Transition();
            }

            // [L]: 보스 및 기믹 전체 스냅샷 상세 상태 콘솔 출력
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
            if (_sceneNailBoard == null)
            {
                _sceneNailBoard = FindFirstObjectByType<NailBoardFieldGimmick>();
            }
            if (_sceneExplosiveBox == null)
            {
                _sceneExplosiveBox = FindFirstObjectByType<ExplosiveBoxFieldGimmick>();
            }
        }

        private void PrintUsageGuide()
        {
            Debug.Log("<color=cyan><b>[JunkWheelerGimmickTester 초기화 완료]</b>\n" +
                      "▶ [6/B]: 회전 톱날 당구 돌진 강제 실행 (PATTERN_BLADE_RICOCHET)\n" +
                      "▶ [7]: 돌진 패턴 강제 실행 (PATTERN_CHARGE)\n" +
                      "▶ [8]: 바이킹 순차 탄막 강제 실행 (PATTERN_JUMP_BARRAGE)\n" +
                      "▶ [9]: 3단 내려찍기 강제 실행 (PATTERN_SLAM)\n" +
                      "▶ [0]: 못판 착지 기믹 파훼 (타이어 펑크 & 4초 그로기)\n" +
                      "▶ [I]: 폭발 상자 충돌 (2초 스턴)\n" +
                      "▶ [P]: 타이어 부위 1회 펑크 (내구도 -1, GimmickTag.Pierce)\n" +
                      "▶ [O]: 타이어 영구 파괴 (펑크 2회 ➔ 돌진 감속 50%, 탄막 감축 30%)\n" +
                      "▶ [U]: 체력 50% 설정 ➔ 2페이즈 진입 및 불꽃 VFX 가동\n" +
                      "▶ [L]: 보스 전체 스냅샷 상세 로그 출력\n" +
                      "▶ [`]: 화면 OnGUI 창 켜기/끄기 토글</color>");
        }

        // ========================================================================
        // 1. 공격 패턴 강제 실행 제어
        // ========================================================================

        [ContextMenu("▶ 패턴 4: 회전 톱날 당구 돌진 실행")]
        public void ForceExecuteBladeRicochet() => ForceExecutePattern(JunkWheelerRicochetBladePatternGimmick.PatternRicochetBladeID);

        [ContextMenu("▶ 패턴 1: 바이킹 순차 탄막 실행")]
        public void ForceExecuteBarrage() => ForceExecutePattern("PATTERN_JUMP_BARRAGE");

        [ContextMenu("▶ 패턴 2: 맵 횡단 돌진 실행")]
        public void ForceExecuteCharge() => ForceExecutePattern("PATTERN_CHARGE");

        [ContextMenu("▶ 패턴 3: 3단 내려찍기 실행")]
        public void ForceExecuteSlam() => ForceExecutePattern("PATTERN_SLAM");

        public void ForceExecutePattern(string patternId)
        {
            if (targetBoss == null) return;

            var patternGimmick = targetBoss.GetGimmick<BossPatternGimmickBase>(patternId);
            if (patternGimmick == null)
            {
                Debug.LogWarning($"<color=yellow>[Tester] '{patternId}' 기믹이 보스에 등록되어 있지 않습니다!</color>");
                return;
            }

            Debug.Log($"<color=cyan>[Tester] 패턴 강제 실행 ➔ <b>{patternGimmick.GimmickName} ({patternId})</b></color>");
            var token = targetBoss.RenewStateCancellationToken();
            targetBoss.StateMachine.ChangeState(new BossPatternExecutingState(targetBoss, token, patternGimmick));
        }

        // ========================================================================
        // 2. 기믹 파훼 및 인터럽트 시뮬레이션
        // ========================================================================

        [ContextMenu("[못판] 고철 못판 착지 시뮬레이션 (4초 그로기 & 펑크)")]
        public void SimulateNailBoardLanding()
        {
            if (targetBoss == null) return;

            Debug.Log("<color=magenta><b>[Tester] ▶ [기믹 파훼] 고철 못판 착지 시뮬레이션 개시!</b></color>");

            // 씬에 못판 오브젝트가 있으면 해당 오브젝트와 물리적 상호작용
            if (_sceneNailBoard != null && _sceneNailBoard.IsInteractable)
            {
                _sceneNailBoard.TriggerInteraction(targetBoss.gameObject);
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
        // 3. 페이즈 전환 및 상태 출력
        // ========================================================================

        [ContextMenu("[페이즈] 페이즈 2 전환 (HP 50% 설정)")]
        public void ForcePhase2Transition()
        {
            if (targetBoss == null || targetBoss.Snapshot == null) return;

            int targetHp = Mathf.FloorToInt(targetBoss.Snapshot.maxHp * 0.49f);
            int damage = Mathf.Max(0, targetBoss.Snapshot.currentHp - targetHp);

            Debug.Log($"<color=orange>[Tester] 체력을 50% 이하({targetHp} HP)로 감소시켜 2페이즈 전이를 트리거합니다. (피해: {damage})</color>");
            targetBoss.TakeDamage(damage);
        }

        [ContextMenu("[상태] 1.0초 확정 딜타임(Idle) 복귀")]
        public void ResetToIdle()
        {
            if (targetBoss == null) return;

            Debug.Log("<color=white>[Tester] 보스를 1.0초 확정 딜타임(BossIdleState)으로 복귀시킵니다.</color>");
            var token = targetBoss.RenewStateCancellationToken();
            targetBoss.StateMachine.ChangeState(new BossIdleState(targetBoss, token, 1.0f));
        }

        [ContextMenu("[전조] 돌진 전조 점멸 수동 테스트 (OnTellStarted)")]
        public void TestChargeTellStart()
        {
            if (targetBoss == null) return;
            var charge = targetBoss.GetGimmick<JunkWheelerChargePatternGimmick>("PATTERN_CHARGE");
            if (charge != null)
            {
                Debug.Log("<color=red>[Tester] 돌진 전조 점멸 및 진동 수동 시작 (OnTellStarted)</color>");
                charge.OnTellStarted();
            }
        }

        [ContextMenu("[전조] 전조 점멸 수동 종료 (OnTellEnded)")]
        public void TestChargeTellEnd()
        {
            if (targetBoss == null) return;
            var charge = targetBoss.GetGimmick<JunkWheelerChargePatternGimmick>("PATTERN_CHARGE");
            if (charge != null)
            {
                Debug.Log("<color=cyan>[Tester] 전조 점멸 및 진동 수동 종료 (OnTellEnded)</color>");
                charge.OnTellEnded();
            }
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
            sb.AppendLine($"▶ 보스명: {s.bossName} ({s.bossID})");
            sb.AppendLine($"▶ 코어 체력: {s.currentHp} / {s.maxHp} ({((float)s.currentHp / s.maxHp * 100f):F1}%)");
            sb.AppendLine($"▶ 기본 방어력: {s.baseDefense}");
            sb.AppendLine($"▶ 현재 페이즈: Phase {s.currentPhase}");
            sb.AppendLine($"▶ FSM 현재 상태: {targetBoss.StateMachine?.CurrentState?.StateName ?? "None"}");
            sb.AppendLine($"▶ 무력화 상태: 그로기 = {s.isGroggy} (피해 증폭 {s.currentDamageMultiplier}배)");

            if (tire != null)
            {
                sb.AppendLine($"▶ 폐타이어 부위: 내구도 {tire.CurrentDurability}/{tire.MaxDurability} | 누적 펑크: {tire.BreakCount}/2 | 영구 파괴: {tire.IsPermanentlyBroken}");
            }

            sb.AppendLine($"▶ 영구 봉인된 스킬 목록: {(s.sealedSkills.Count > 0 ? string.Join(", ", s.sealedSkills) : "없음 (스펙 약화 모드)")}");
            sb.AppendLine("<b><color=white>=========================================================</color></b>");

            Debug.Log(sb.ToString());
        }

        // ========================================================================
        // 4. 화면 OnGUI 대시보드
        // ========================================================================

        private void OnGUI()
        {
            if (!showOnScreenGui) return;

            // 화면 좌측 상단 컨트롤 패널 배치 (BossFSMTester와 충돌 방지)
            GUILayout.BeginArea(new Rect(20, 20, 280, 560), "<b>[정크 휠러 기믹 테스터]</b>", GUI.skin.window);
            GUILayout.Space(4);

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

            // 상태 요약 모니터
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<b>HP:</b> {s?.currentHp ?? 0} / {s?.maxHp ?? 0} (Phase {s?.currentPhase ?? 1})");
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

            GUILayout.Space(4);
            GUILayout.Label("<b>[공격 패턴 강제 시전]</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("[6] 당구", GUILayout.Height(25))) ForceExecuteBladeRicochet();
            if (GUILayout.Button("[7] 돌진", GUILayout.Height(25))) ForceExecuteCharge();
            if (GUILayout.Button("[8] 탄막", GUILayout.Height(25))) ForceExecuteBarrage();
            if (GUILayout.Button("[9] 찍기", GUILayout.Height(25))) ForceExecuteSlam();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("<b>[기믹 파훼 & 인터럽트]</b>");
            GUI.color = new Color(1f, 0.7f, 1f, 1f);
            if (GUILayout.Button("[0] 못판 착지 (4초 그로기 & 펑크)", GUILayout.Height(28))) SimulateNailBoardLanding();
            GUI.color = new Color(1f, 0.8f, 0.6f, 1f);
            if (GUILayout.Button("[I] 폭발 상자 충돌 (2초 스턴)", GUILayout.Height(26))) SimulateExplosiveBoxCollision();
            GUI.color = Color.white;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("[P] 타이어 1회 펑크", GUILayout.Height(24))) PunctureTireOnce();
            if (GUILayout.Button("[O] 타이어 완전 파괴", GUILayout.Height(24))) BreakTireCompletely();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("<b>[전투 및 상태 제어]</b>");
            if (GUILayout.Button("[U] 2페이즈 전환 (HP 50%)", GUILayout.Height(24))) ForcePhase2Transition();
            if (GUILayout.Button("[복귀] 확정 딜타임(Idle) 복귀", GUILayout.Height(24))) ResetToIdle();
            if (GUILayout.Button("[L] 상세 스냅샷 콘솔 출력", GUILayout.Height(24))) PrintDetailedStatus();

            GUILayout.Space(4);
            GUILayout.Label("<b>[전조 점멸(Tell Flash) 제어]</b>");
            GUILayout.BeginHorizontal();
            GUI.color = new Color(1f, 0.4f, 0.4f, 1f);
            if (GUILayout.Button("전조 시작", GUILayout.Height(24))) TestChargeTellStart();
            GUI.color = new Color(0.6f, 0.9f, 1f, 1f);
            if (GUILayout.Button("점멸 종료", GUILayout.Height(24))) TestChargeTellEnd();
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("<size=9><color=gray>[`] 키를 눌러 이 창을 접거나 펼 수 있습니다.</color></size>");

            GUILayout.EndArea();
        }
    }
}

