using UnityEngine;

namespace RealSteel.Boss.Test
{
    /// <summary>
    /// [보스 즉시 처치 테스트 도구 (BossInstantKillTester)]
    /// 단축키(기본 K키 또는 F12)나 화면 OnGUI 버튼, 인스펙터 컨텍스트 메뉴를 통해
    /// 씬 내의 보스를 즉시 K.O(HP 0)시키고 승리 정산 및 결과 UI 연동을 테스트함.
    /// </summary>
    public class BossInstantKillTester : MonoBehaviour
    {
        [Header("=== 타겟 보스 ===")]
        [Tooltip("비워둘 경우 씬에서 BossController를 자동으로 탐색함")]
        [SerializeField] private BossController targetBoss;

        [Header("=== 단축키 및 실행 설정 ===")]
        [Tooltip("보스를 즉시 처치할 단축키")]
        [SerializeField] private KeyCode instantKillKey = KeyCode.K;

        [Tooltip("보조 단축키 (F12)")]
        [SerializeField] private KeyCode secondaryKillKey = KeyCode.F12;

        [Tooltip("처치 시 전투 종료 및 승리 결과 UI 자동 연동 여부")]
        [SerializeField] private bool triggerBattleWin = true;

        [Tooltip("화면에 즉사 킬 버튼 OnGUI 표시")]
        [SerializeField] private bool showOnScreenButton = true;

        private void Awake()
        {
            if (targetBoss == null)
            {
                targetBoss = FindFirstObjectByType<BossController>();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(instantKillKey) || Input.GetKeyDown(secondaryKillKey))
            {
                ExecuteInstantKill();
            }
        }

        /// <summary>
        /// 보스 즉시 처치 실행
        /// </summary>
        [ContextMenu("▶ Instant Kill Boss (보스 즉시 처치)")]
        public void ExecuteInstantKill()
        {
            if (targetBoss == null)
            {
                targetBoss = FindFirstObjectByType<BossController>();
            }

            if (targetBoss == null)
            {
                Debug.LogWarning("<color=red>[BossInstantKillTester] 씬에서 BossController를 찾을 수 없습니다!</color>");
                return;
            }

            var snapshot = targetBoss.Snapshot;
            string bossName = snapshot != null ? snapshot.bossName : targetBoss.name;

            Debug.Log($"<color=red><b>[BossInstantKillTester] 치명적인 일격 발동! ➔ {bossName} 즉시 처치! (HP: 0)</b></color>");

            // 1. 보스에게 막대한 대미지 부여 (HP 0 처리)
            targetBoss.TakeDamage(999999);

            // 2. 스냅샷 강제 0 확정 (방어력/배율 무관)
            if (snapshot != null)
            {
                snapshot.currentHp = 0;
            }

            // 3. 전투 종료 및 승리 결과 UI 자동 호출 연동
            if (triggerBattleWin)
            {
                if (BattleManager.Instance != null)
                {
                    Debug.Log("<color=cyan>[BossInstantKillTester] BattleManager.EndBattle(true, 'Player') 자동 연동 호출 완료</color>");
                    BattleManager.Instance.EndBattle(true, "Player");
                }

                // 비활성화된 오브젝트까지 포함하여 BattleResultUI 검색 및 강제 활성화 보장
                var resultUI = RealSteel.UI.BattleResultUI.Instance ?? FindFirstObjectByType<RealSteel.UI.BattleResultUI>(FindObjectsInactive.Include);
                if (resultUI != null)
                {
                    resultUI.gameObject.SetActive(true);
                    var dummyResult = new BattleSettlementResult
                    {
                        PlayerWon = true,
                        GoldDelta = 150,
                        CoreExpGained = 120,
                        SummaryMessage = $"치명타 성공 — {bossName} 완전 격파!"
                    };
                    resultUI.ShowResult(dummyResult, true);
                }
                else
                {
                    Debug.LogWarning("<color=yellow>[BossInstantKillTester] 씬에서 BattleResultUI 컴포넌트를 찾지 못했습니다! Canvas에 BattleResultUI가 붙어있는지 확인해 주세요.</color>");
                }
            }
        }

        private void OnGUI()
        {
            if (!showOnScreenButton) return;

            // 화면 우측 상단 FSM 테스터 바로 아래 눈에 띄는 빨간색 킬 버튼 배치
            float width = 220;
            float height = 45;
            float x = Screen.width - width - 20;
            float y = 340;

            GUI.color = new Color(1f, 0.3f, 0.3f, 1f);
            if (GUI.Button(new Rect(x, y, width, height), $"<b>☠ [K] 보스 즉사 킬 (Kill)</b>"))
            {
                ExecuteInstantKill();
            }
            GUI.color = Color.white;
        }
    }
}

