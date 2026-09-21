using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [임시/테스트용] RobotAssembler(§11-8, 실제 장착 파츠로 조립하는 정식 시스템)가 아직 없어서,
/// 그 대신 더미 스탯으로 CombatDataHub에 Player/Enemy를 등록해 HitDetection을 바로 테스트할 수 있게 하는 스크립트.
///
/// 정식 RobotAssembler가 만들어지면 이 스크립트는 통째로 삭제되고, 실제 장착 파츠 기반
/// CombatantBuilder.Build(...) 호출로 대체된다 — 지금은 §11-6(HitDetection)을 §11-8보다 먼저
/// 검증해보기 위한 임시 다리 역할일 뿐이다.
///
/// 씬의 "Manager" 오브젝트 아무 데나 붙여두면 된다 (로봇 본체에 붙일 필요 없음).
/// </summary>
public class DummyBattleBootstrap : MonoBehaviour
{

    // Start에서 자동 실행하는 이유: HitDetection.OnEnable이 씬의 로봇을 찾는 시점과 별개로,
    // "게임이 시작되면 일단 스탯부터 등록돼 있어야 한다"가 조건 없이 항상 성립해야 하기 때문 —
    // 타이밍을 맞추려고 다른 스크립트와 순서를 조율할 필요 없이 그냥 켜지자마자 실행한다
    private void Start()
    {
        // CombatDataHub/BattleManager는 KKH 쪽 싱글톤인데, CombatClock과 달리 씬에 없으면
        // Instance가 그냥 null을 반환한다(자동 생성 안 함) — 씬에 둘 다 미리 배치 안 해뒀으면
        // 아래에서 곧바로 NullReferenceException이 난다. KKH 파일은 못 고치니(§0), 여기서
        // 없으면 직접 만들어서 채워 넣는다 — CombatDataHubTester(KKH)도 같은 패턴을 이미 쓰고 있다
        if (CombatDataHub.Instance == null) gameObject.AddComponent<CombatDataHub>();
        if (BattleManager.Instance == null) gameObject.AddComponent<BattleManager>();

        // CombatantBuilder.CreateDummy는 실제 파츠 데이터 없이도 그럴듯한 스탯(HP/공격력/이동속도/부위
        // 내구도)을 만들어주는 테스트 전용 팩토리다 — CombatDataHubTester가 검증용으로 쓰던 것과 같은 함수
        var player = CombatantBuilder.CreateDummy(fighterId: "Player", isPlayer: true);
        var enemy = CombatantBuilder.CreateDummy(fighterId: "Enemy", isPlayer: false);

        // InitializeBattle이 CombatDataHub에 두 스냅샷을 등록해준다 — 이게 안 되어 있으면
        // ProcessHit을 아무리 불러도 "스냅샷을 찾을 수 없음" 경고만 뜨고 데미지 계산 자체가 안 된다
        BattleManager.Instance.InitializeBattle(player, enemy);

        ApplyEquipmentEffects(); // 최. 추가

        // 라이프사이클상 Ready 다음 단계로 넘겨서 "지금 전투 중"인 정상 상태로 맞춰둔다.
        // (지금 HitDetection 자체는 이 상태를 검사하지 않지만, BattleManager를 참조하는 다른 코드가
        //  Ready 상태에서 이상하게 동작하는 걸 막기 위해 정석대로 호출해둔다)
        BattleManager.Instance.StartBattle();

        Debug.Log("[DummyBattleBootstrap] 더미 스탯으로 Player/Enemy 등록 완료 — HitDetection 테스트 준비됨");
    }
    #region 최. 추가
    [Header("장비 효과 테스트 (선택)")]
    [SerializeField] private EquipmentEffectTable effectTable;
    [SerializeField] private List<PartMasterData> playerParts = new List<PartMasterData>();
    [SerializeField] private List<PartMasterData> enemyParts = new List<PartMasterData>();

    private void ApplyEquipmentEffects()
    {
        if (effectTable == null) return;

        foreach (var executor in FindObjectsByType<ActionExecutor>(FindObjectsSortMode.None))
        {
            var parts = executor.FighterId == "Enemy" ? enemyParts : playerParts;
            EquipmentEffectApplier.Apply(executor, parts, effectTable);
        }
    }

    #endregion
}
