using System;
using UnityEngine;

/// <summary>
/// [HUD 실시간 인터랙티브 테스터 (HUDInteractiveTester)]
/// 전투 체력(Core HP) 및 5개 파츠 내구도 UI(FighterStatusHUD, PartGaugeWidget)가
/// 실시간 수치 변경 및 이벤트에 반응하여 게이지, 수치 텍스트, 상태별 색상(녹색->노랑->빨강->회색/KO)으로
/// 정상 전환되는지 게임 뷰에서 눈으로 직접 검증하는 테스트 도구임.
/// </summary>
public class HUDInteractiveTester : MonoBehaviour
{
    [Header("테스트 대상 식별자")]
    [Tooltip("테스트할 대상 식별자 ('Player' 또는 'Enemy')")]
    [SerializeField] private string targetFighterId = "Player";

    [Header("테스트 수치 설정")]
    [SerializeField] private int coreDamageStep = 80;      // 코어 직격 시 1회 차감량
    [SerializeField] private int partDamageStep = 15;      // 파츠 피격 시 1회 차감량
    [SerializeField] private int initialMaxHp = 440;       // 기획서 예시 코어 최대 HP
    [SerializeField] private int initialPartMaxDur = 50;   // 기획서 예시 파츠 최대 내구도

    [Header("파츠 피격 시 코어 체력 연동 설정 (기획서 규칙)")]
    [Tooltip("파츠 피격 시 코어 체력도 함께 차감할지 여부")]
    [SerializeField] private bool linkCoreDamageOnPartHit = true;
    [Tooltip("머리 피격 시 코어 피해량 (치명타 부위)")]
    [SerializeField] private int headCoreDamage = 60;
    [Tooltip("팔/다리 피격 시 코어 피해량")]
    [SerializeField] private int limbCoreDamage = 35;

    [Header("화면 조작 안내 GUI 표시")]
    [SerializeField] private bool showOnScreenGUI = true;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoRunInTestScene()
    {
        var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (currentScene.name == "Battle_Test_KKH")
        {
            if (FindFirstObjectByType<HUDInteractiveTester>() == null)
            {
                var go = new GameObject("@HUD_InteractiveTester");
                go.AddComponent<HUDInteractiveTester>();
                Debug.Log("<color=cyan><b>[HUDInteractiveTester] Battle_Test_KKH 씬 감지됨 -> 인터랙티브 테스터 자동 활성화 완료!</b></color>");
            }
        }
    }
#endif

    private void Start()
    {
        InitializeTestEnvironment();
    }

    /// <summary>
    /// 허브 및 참가자 더미 스냅샷 자동 점검 및 초기 등록
    /// </summary>
    private void InitializeTestEnvironment()
    {
        var hub = CombatDataHub.Instance;
        if (hub == null)
        {
            var hubGo = new GameObject("@CombatDataHub");
            hub = hubGo.AddComponent<CombatDataHub>();
            Debug.Log("<color=cyan>[HUDInteractiveTester] CombatDataHub가 씬에 없어 자동 생성함.</color>");
        }

        // 대상 참가자의 스냅샷이 없으면 더미 데이터 자동 생성 및 등록
        if (hub.GetSnapshot(targetFighterId) == null)
        {
            bool isPlayer = targetFighterId.Equals("Player", StringComparison.OrdinalIgnoreCase);
            var snapshot = CombatantBuilder.CreateDummy(
                fighterId: targetFighterId,
                isPlayer: isPlayer,
                maxHp: initialMaxHp,
                baseDefense: 50,
                baseAtk: 20,
                leftArmAtk: 100,
                rightArmAtk: 100,
                leftLegSpd: 5f,
                rightLegSpd: 5f,
                partMaxDurability: initialPartMaxDur
            );

            if (isPlayer)
                hub.RegisterCombatants(snapshot, hub.EnemySnapshot);
            else
                hub.RegisterCombatants(hub.PlayerSnapshot, snapshot);

            Debug.Log($"<color=green>[HUDInteractiveTester] '{targetFighterId}' 더미 스냅샷 등록 완료 (Core HP: {initialMaxHp}, Part Durability: {initialPartMaxDur})</color>");
        }
    }

    private void Update()
    {
        var hub = CombatDataHub.Instance;
        if (hub == null) return;

        // 1. 숫자 1 ~ 5 키: 파츠별 내구도 차감
        if (Input.GetKeyDown(KeyCode.Alpha1)) DamagePart(BodyPart.Head);
        if (Input.GetKeyDown(KeyCode.Alpha2)) DamagePart(BodyPart.LeftArm);
        if (Input.GetKeyDown(KeyCode.Alpha3)) DamagePart(BodyPart.RightArm);
        if (Input.GetKeyDown(KeyCode.Alpha4)) DamagePart(BodyPart.LeftLeg);
        if (Input.GetKeyDown(KeyCode.Alpha5)) DamagePart(BodyPart.RightLeg);

        // 2. LeftAlt 키: 코어 체력 유효타 피격
        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            DamageCore(coreDamageStep);
        }

        // 3. K 키: 코어 즉시 파괴 (K.O 시뮬레이션)
        if (Input.GetKeyDown(KeyCode.K))
        {
            ForceCoreKO();
        }

        // 4. R 키: 전체 내구도 및 코어 풀 리셋
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetAll();
        }

        // 5. Tab 키: 화면 안내 GUI 토글
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            showOnScreenGUI = !showOnScreenGUI;
        }
    }

    public void DamagePart(BodyPart part)
    {
        var hub = CombatDataHub.Instance;
        if (hub == null) return;

        // 기획서 규칙: 파츠 피격 시 파츠 내구도와 함께 코어 체력(본체 생명력)도 함께 감소함
        int coreDmg = 0;
        if (linkCoreDamageOnPartHit)
        {
            coreDmg = (part == BodyPart.Head) ? headCoreDamage : limbCoreDamage;
        }

        hub.ApplyPartHit(targetFighterId, part, partDamageStep, coreDmg);

        int partCur = hub.GetPartDurability(targetFighterId, part);
        int partMax = hub.GetPartMaxDurability(targetFighterId, part);
        int coreCur = hub.GetCurrentHp(targetFighterId);
        int coreMax = hub.GetMaxHp(targetFighterId);

        if (coreDmg > 0)
        {
            Debug.Log($"<color=orange>[HUD Test] {targetFighterId} {part} 피격 -> 내구도 -{partDamageStep} (현재: {partCur}/{partMax}) & 코어 HP -{coreDmg} (현재: {coreCur}/{coreMax})</color>");
        }
        else
        {
            Debug.Log($"<color=orange>[HUD Test] {targetFighterId} {part} 내구도 -{partDamageStep} 차감 -> 현재: {partCur}/{partMax}</color>");
        }
    }

    public void DamageCore(int damage)
    {
        var hub = CombatDataHub.Instance;
        if (hub == null) return;

        hub.ApplyCoreDamage(targetFighterId, damage);
        int cur = hub.GetCurrentHp(targetFighterId);
        int max = hub.GetMaxHp(targetFighterId);

        Debug.Log($"<color=red>[HUD Test] {targetFighterId} 코어 직격 -{damage} 대미지 -> 현재 HP: {cur}/{max}</color>");
    }

    public void ForceCoreKO()
    {
        var hub = CombatDataHub.Instance;
        if (hub == null) return;

        int cur = hub.GetCurrentHp(targetFighterId);
        hub.ApplyCoreDamage(targetFighterId, cur);

        Debug.Log($"<color=red><b>[HUD Test] {targetFighterId} 코어 강제 파괴 (K.O) 실행됨!</b></color>");
    }

    public void ResetAll()
    {
        var hub = CombatDataHub.Instance;
        if (hub == null) return;

        hub.ResetFighter(targetFighterId);
        Debug.Log($"<color=lime><b>[HUD Test] {targetFighterId} 전신 체력 및 파츠 내구도 풀 복구 완료!</b></color>");
    }

    private void OnGUI()
    {
        if (!showOnScreenGUI) return;

        // 반투명 스타일 박스
        GUI.color = new Color(1f, 1f, 1f, 0.95f);
        GUILayout.BeginArea(new Rect(20, 20, 320, 400), "<b>[HUD 실시간 인터랙티브 테스터]</b>", GUI.skin.window);
        GUILayout.Space(6);

        GUILayout.Label($"<b>대상:</b> <color=cyan>{targetFighterId}</color> (Tab: 안내창 On/Off)", GUILayout.Height(20));
        GUILayout.Label($"<size=11><b>코어 연동 감쇄:</b> {(linkCoreDamageOnPartHit ? "<color=lime>ON (머리-" + headCoreDamage + ", 팔다리-" + limbCoreDamage + ")</color>" : "<color=gray>OFF (파츠만)</color>")}</size>");
        GUILayout.Space(4);

        GUILayout.Label("<b>[파츠 유효타 피격 (파츠 + 코어 동시 감소)]</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"[1] 머리\n-15 (코어-{headCoreDamage})", GUILayout.Height(40))) DamagePart(BodyPart.Head);
        if (GUILayout.Button($"[2] 왼팔\n-15 (코어-{limbCoreDamage})", GUILayout.Height(40))) DamagePart(BodyPart.LeftArm);
        if (GUILayout.Button($"[3] 오른팔\n-15 (코어-{limbCoreDamage})", GUILayout.Height(40))) DamagePart(BodyPart.RightArm);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"[4] 왼다리\n-15 (코어-{limbCoreDamage})", GUILayout.Height(40))) DamagePart(BodyPart.LeftLeg);
        if (GUILayout.Button($"[5] 오른다리\n-15 (코어-{limbCoreDamage})", GUILayout.Height(40))) DamagePart(BodyPart.RightLeg);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("<b>[코어 직접 피격 (가슴 직격)]</b>");
        if (GUILayout.Button($"[Space] 코어 직격 유효타 (-{coreDamageStep} HP)", GUILayout.Height(30)))
        {
            DamageCore(coreDamageStep);
        }

        GUILayout.Space(4);
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("[K] 코어 파괴 (즉시 K.O 판정)", GUILayout.Height(30)))
        {
            ForceCoreKO();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(6);
        GUI.backgroundColor = new Color(0.4f, 1f, 0.4f);
        if (GUILayout.Button("[R] 전신 체력 / 내구도 풀 리셋", GUILayout.Height(30)))
        {
            ResetAll();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.EndArea();
    }
}
