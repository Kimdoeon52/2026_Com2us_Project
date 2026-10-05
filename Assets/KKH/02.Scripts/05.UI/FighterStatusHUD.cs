// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;
// using TMPro;

// /// <summary>
// /// [전투 상태 HUD (FighterStatusHUD)]
// /// CombatDataHub로부터 실시간 코어 HP 및 5개 파츠 내구도 변경 이벤트를 수신하여 UI를 갱신함.
// /// 자가 진단(Diagnostic) 및 자동 자식 탐색(Auto-Fallback) 기능이 내장되어 있어 인스펙터 바인딩 실수를 방지함.
// /// </summary>
// public class FighterStatusHUD : MonoBehaviour
// {
//     [Header("대상 캐릭터 식별자")]
//     [Tooltip("플레이어 HUD는 Player, 적군 HUD는 Enemy로 설정")]
//     [SerializeField] private string targetFighterID = "Player";

//     [Header("범용 테스트 및 프리뷰 설정")]
//     [Tooltip("CombatDataHub가 없거나 캐릭터 데이터가 미등록 상태일 때 기본 100% 정상 모의(Mock) 데이터로 자동 표시")]
//     [SerializeField] private bool useMockDataIfNoData = true;

//     [Tooltip("모의 테스트용 코어 최대 체력")]
//     [SerializeField] private int mockCoreMaxHp = 1000;

//     [Tooltip("모의 테스트용 각 부위 최대 내구도")]
//     [SerializeField] private int mockPartMaxDurability = 100;

//     [Tooltip("씬에 CombatDataHub가 발견되면 모의 스냅샷을 허브에 자동 등록하여 전투 연동")]
//     [SerializeField] private bool autoRegisterMockToHub = true;

//     [Tooltip("게임 실행 중 키보드(1~5, Space, R, K)로 단독 피격/회복 테스트 활성화")]
//     [SerializeField] private bool enableStandaloneKeyTest = true;

//     // 로컬 모의 스탯 캐시 (허브가 없는 순수 UI 씬에서도 단독 작동 지원)
//     private int localCoreHp = 1000;
//     private int localCoreMaxHp = 1000;
//     private Dictionary<BodyPart, int> localPartDurability = new Dictionary<BodyPart, int>();
//     private Dictionary<BodyPart, int> localPartMaxDurability = new Dictionary<BodyPart, int>();

//     [Header("하단 총합체력 UI")]
//     [SerializeField] private Slider totalHealthSlider; // 총합체력 슬라이더
//     [SerializeField] private TextMeshProUGUI totalHealthText; // 총합체력 텍스트

//     [Header("부위별 게이지 위젯 목록(총 6개 : core, head, leftArm, rightArm, leftLeg, rightLeg)")]
//     [SerializeField] private List<PartGaugeWidget> partGaugeWidgets = new List<PartGaugeWidget>();

//     private Dictionary<BodyPart, PartGaugeWidget> widgetLookup = new Dictionary<BodyPart, PartGaugeWidget>();
//     private bool isSubscribed = false;

// #if UNITY_EDITOR
//     [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
//     private static void AutoAttachInTestScene()
//     {
//         var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
//         if (currentScene.name == "Battle_Test_KKH")
//         {
//             if (FindFirstObjectByType<FighterStatusHUD>() == null)
//             {
//                 var hudObj = GameObject.Find("FighterStatusHUD");
//                 if (hudObj != null)
//                 {
//                     hudObj.AddComponent<FighterStatusHUD>();
//                     Debug.Log("<color=cyan><b>[FighterStatusHUD] 씬 내 'FighterStatusHUD' 게임오브젝트를 감지하여 스크립트를 자동 부착(Auto-Attach)했습니다!</b></color>");
//                 }
//                 else
//                 {
//                     Debug.LogWarning("[FighterStatusHUD] 씬에서 'FighterStatusHUD' 이름의 오브젝트를 찾을 수 없습니다.");
//                 }
//             }
//         }
//     }
// #endif

//     private void Awake()
//     {
//         InitializeWidgets();
//     }

//     /// <summary>
//     /// 위젯 수집 및 룩업 테이블 빌드 (미할당 시 자식 오브젝트 자동 탐색)
//     /// </summary>
//     private void InitializeWidgets()
//     {
//         // 1. 위젯 리스트가 비어있다면 자식 오브젝트에서 자동 수집
//         if (partGaugeWidgets == null || partGaugeWidgets.Count == 0)
//         {
//             var found = GetComponentsInChildren<PartGaugeWidget>(true);
//             partGaugeWidgets = new List<PartGaugeWidget>(found);
//             Debug.Log($"<color=yellow>[FighterStatusHUD:{targetFighterID}] 인스펙터 위젯 목록이 비어있어 자식 오브젝트에서 {found.Length}개의 위젯을 자동 수집함.</color>");
//         }

//         // 2. 하단 슬라이더/텍스트가 미할당된 경우 자동 탐색
//         if (totalHealthSlider == null)
//         {
//             totalHealthSlider = GetComponentInChildren<Slider>(true);
//             if (totalHealthSlider != null)
//                 Debug.Log($"<color=yellow>[FighterStatusHUD:{targetFighterID}] totalHealthSlider 자동 연결됨: {totalHealthSlider.gameObject.name}</color>");
//         }

//         if (totalHealthText == null && totalHealthSlider != null)
//         {
//             totalHealthText = totalHealthSlider.GetComponentInChildren<TextMeshProUGUI>(true);
//             if (totalHealthText != null)
//                 Debug.Log($"<color=yellow>[FighterStatusHUD:{targetFighterID}] totalHealthText 자동 연결됨: {totalHealthText.gameObject.name}</color>");
//         }

//         // 3. 부위별 룩업 테이블 구축 및 중복 검증
//         widgetLookup.Clear();
//         foreach (var widget in partGaugeWidgets)
//         {
//             if (widget == null) continue;

//             if (widgetLookup.ContainsKey(widget.TargetPart))
//             {
//                 Debug.LogWarning($"<color=orange>[FighterStatusHUD:{targetFighterID}] 부위 중복 감지! '{widget.TargetPart}' 부위가 '{widgetLookup[widget.TargetPart].gameObject.name}'에서 '{widget.gameObject.name}'(으)로 덮어씌워집니다. 각 위젯의 TargetPart 설정을 확인하세요.</color>");
//             }

//             widgetLookup[widget.TargetPart] = widget;
//         }

//         Debug.Log($"<color=cyan><b>[FighterStatusHUD:{targetFighterID}] 위젯 초기화 완료 (등록된 부위 수: {widgetLookup.Count}/6)</b></color>");
//         foreach (var kvp in widgetLookup)
//         {
//             Debug.Log($"   └ [{kvp.Key}] -> 오브젝트: {kvp.Value.gameObject.name}");
//         }
//     }

//     private void Start()
//     {
//         // 1. 다른 씬 또는 허브가 없을 때도 즉시 정상 UI(100%)로 보이도록 초기 모의 데이터 선적용
//         if (useMockDataIfNoData)
//         {
//             ApplyMockData();
//         }

//         // 2. CombatDataHub 싱글톤 이벤트 구독 시도
//         TrySubscribe();

//         // 3. 씬 로드 타이밍 문제로 아직 허브가 없다면 대기 코루틴 가동
//         if (!isSubscribed)
//         {
//             StartCoroutine(WaitForHubAndSubscribeCoroutine());
//         }
//     }

//     private void Update()
//     {
//         // 범용 단독 테스트 키보드 입력 지원
//         if (!enableStandaloneKeyTest) return;

//         if (Input.GetKeyDown(KeyCode.Alpha1)) HitPart(BodyPart.Head, 25);
//         else if (Input.GetKeyDown(KeyCode.Alpha2)) HitPart(BodyPart.LeftArm, 25);
//         else if (Input.GetKeyDown(KeyCode.Alpha3)) HitPart(BodyPart.RightArm, 25);
//         else if (Input.GetKeyDown(KeyCode.Alpha4)) HitPart(BodyPart.LeftLeg, 25);
//         else if (Input.GetKeyDown(KeyCode.Alpha5)) HitPart(BodyPart.RightLeg, 25);
//         else if (Input.GetKeyDown(KeyCode.LeftAlt)) HitCore(100);
//         else if (Input.GetKeyDown(KeyCode.R)) ResetAllStats();
//         else if (Input.GetKeyDown(KeyCode.K)) HitCore(9999);
//     }

//     private void OnDestroy()
//     {
//         Unsubscribe();
//     }

//     private void TrySubscribe()
//     {
//         if (isSubscribed) return;

//         var hub = CombatDataHub.Instance;
//         if (hub != null)
//         {
//             hub.OnHpChanged += HandleHpChanged;
//             hub.OnPartDurabilityChanged += HandlePartDurabilityChanged;
//             isSubscribed = true;

//             Debug.Log($"<color=green><b>[FighterStatusHUD:{targetFighterID}] CombatDataHub 이벤트 구독 성공!</b></color>");

//             // 초기 데이터 전체 동기화
//             RefreshAllData();
//         }
//     }

//     private void Unsubscribe()
//     {
//         if (!isSubscribed) return;

//         var hub = CombatDataHub.Instance;
//         if (hub != null)
//         {
//             hub.OnHpChanged -= HandleHpChanged;
//             hub.OnPartDurabilityChanged -= HandlePartDurabilityChanged;
//         }
//         isSubscribed = false;
//     }

//     /// <summary>
//     /// CombatDataHub가 런타임에 지연 생성될 경우를 대비한 자동 재구독 코루틴
//     /// </summary>
//     private IEnumerator WaitForHubAndSubscribeCoroutine()
//     {
//         float timeout = 3f;
//         float elapsed = 0f;

//         while (!isSubscribed && elapsed < timeout)
//         {
//             yield return new WaitForSeconds(0.2f);
//             elapsed += 0.2f;
//             TrySubscribe();
//         }

//         if (!isSubscribed)
//         {
//             Debug.Log($"<color=yellow>[FighterStatusHUD:{targetFighterID}] 씬 내 CombatDataHub가 없어 '단독 범용 모의 모드(Standalone Mock)'로 유지됩니다.</color>");
//             if (useMockDataIfNoData)
//             {
//                 ApplyMockData();
//             }
//         }
//     }

//     /// <summary>
//     /// 전체 수치 동기화 (CombatDataHub 연동 또는 모의 데이터 폴백)
//     /// </summary>
//     public void RefreshAllData()
//     {
//         var hub = CombatDataHub.Instance;
//         if (hub == null)
//         {
//             if (useMockDataIfNoData) ApplyMockData();
//             return;
//         }

//         var snapshot = hub.GetSnapshot(targetFighterID);
//         if (snapshot == null || snapshot.maxHp <= 0)
//         {
//             if (autoRegisterMockToHub)
//             {
//                 Debug.Log($"<color=yellow>[FighterStatusHUD:{targetFighterID}] Hub에 등록된 스냅샷이 없어 기본 100% 모의 스냅샷을 자동 등록합니다.</color>");
//                 snapshot = hub.RegisterDefaultMockIfMissing(targetFighterID, mockCoreMaxHp, mockPartMaxDurability);
//             }
//             else if (useMockDataIfNoData)
//             {
//                 ApplyMockData();
//                 return;
//             }
//             else
//             {
//                 return;
//             }
//         }

//         // 1. 코어 본체 체력 동기화
//         int currentHp = hub.GetCurrentHp(targetFighterID);
//         int maxHp = hub.GetMaxHp(targetFighterID);
//         UpdateCoreHpDisplay(currentHp, maxHp);

//         if (widgetLookup.TryGetValue(BodyPart.Core, out var coreWidget))
//         {
//             coreWidget.UpdateDurability(currentHp, maxHp);
//             coreWidget.UpdateLabelDisplay();
//         }

//         // 2. 부위별 내구도 동기화
//         foreach (var kvp in widgetLookup)
//         {
//             if (kvp.Key == BodyPart.Core) continue;

//             int curDurability = hub.GetPartDurability(targetFighterID, kvp.Key);
//             int maxDurability = hub.GetPartMaxDurability(targetFighterID, kvp.Key);
//             kvp.Value.UpdateDurability(curDurability, maxDurability);
//             kvp.Value.UpdateLabelDisplay();
//         }

//         Debug.Log($"<color=cyan>[FighterStatusHUD:{targetFighterID}] 전체 데이터 동기화 완료 (Core HP: {currentHp}/{maxHp})</color>");
//     }

//     /// <summary>
//     /// 허브가 없거나 데이터 미등록 상태일 때 100% 정상 모의(Mock) 데이터로 UI 렌더링
//     /// </summary>
//     public void ApplyMockData()
//     {
//         localCoreMaxHp = mockCoreMaxHp;
//         localCoreHp = mockCoreMaxHp;
//         UpdateCoreHpDisplay(localCoreHp, localCoreMaxHp);

//         var allParts = new[] { BodyPart.Head, BodyPart.Core, BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg };
//         foreach (var p in allParts)
//         {
//             localPartMaxDurability[p] = mockPartMaxDurability;
//             localPartDurability[p] = mockPartMaxDurability;
//         }

//         if (widgetLookup.TryGetValue(BodyPart.Core, out var coreWidget))
//         {
//             coreWidget.UpdateDurability(localCoreHp, localCoreMaxHp);
//             coreWidget.UpdateLabelDisplay();
//         }

//         foreach (var kvp in widgetLookup)
//         {
//             if (kvp.Key == BodyPart.Core) continue;
//             kvp.Value.UpdateDurability(mockPartMaxDurability, mockPartMaxDurability);
//             kvp.Value.UpdateLabelDisplay();
//         }
//     }

//     // ========================================================================
//     // 범용 테스트 인터랙션 API (허브 유무 상관없이 단독 동작 보장)
//     // ========================================================================

//     /// <summary>
//     /// 특정 부위 피격 테스트 (내구도 및 코어 체력 동반 차감)
//     /// </summary>
//     public void HitPart(BodyPart part, int partDamage = 25, int coreDamage = 20)
//     {
//         var hub = CombatDataHub.Instance;
//         if (hub != null && hub.GetSnapshot(targetFighterID) != null)
//         {
//             hub.ApplyPartHit(targetFighterID, part, partDamage, coreDamage);
//             return;
//         }

//         // 허브가 없는 단독 모드에서의 로컬 연산 처리
//         if (!localPartDurability.ContainsKey(part))
//         {
//             localPartDurability[part] = mockPartMaxDurability;
//             localPartMaxDurability[part] = mockPartMaxDurability;
//         }

//         localPartDurability[part] = Mathf.Max(0, localPartDurability[part] - partDamage);
//         localCoreHp = Mathf.Max(0, localCoreHp - coreDamage);

//         UpdateCoreHpDisplay(localCoreHp, localCoreMaxHp);
//         if (widgetLookup.TryGetValue(BodyPart.Core, out var coreW))
//         {
//             coreW.UpdateDurability(localCoreHp, localCoreMaxHp);
//         }

//         if (widgetLookup.TryGetValue(part, out var partW))
//         {
//             partW.UpdateDurability(localPartDurability[part], localPartMaxDurability[part]);
//         }
//         Debug.Log($"<color=orange>[HUD 단독 테스트] {part} 피격 (-{partDamage}) & 코어 (-{coreDamage}) -> 남은 내구도: {localPartDurability[part]}, 코어: {localCoreHp}</color>");
//     }

//     /// <summary>
//     /// 코어 본체 체력 직접 피격 테스트
//     /// </summary>
//     public void HitCore(int damage = 100)
//     {
//         var hub = CombatDataHub.Instance;
//         if (hub != null && hub.GetSnapshot(targetFighterID) != null)
//         {
//             hub.ApplyCoreDamage(targetFighterID, damage);
//             return;
//         }

//         localCoreHp = Mathf.Max(0, localCoreHp - damage);
//         UpdateCoreHpDisplay(localCoreHp, localCoreMaxHp);
//         if (widgetLookup.TryGetValue(BodyPart.Core, out var coreW))
//         {
//             coreW.UpdateDurability(localCoreHp, localCoreMaxHp);
//         }
//         Debug.Log($"<color=orange>[HUD 단독 테스트] 코어 피격 (-{damage}) -> 남은 체력: {localCoreHp}/{localCoreMaxHp}</color>");
//     }

//     /// <summary>
//     /// 모든 체력 및 파츠 내구도 100% 완전 복구
//     /// </summary>
//     public void ResetAllStats()
//     {
//         var hub = CombatDataHub.Instance;
//         if (hub != null && hub.GetSnapshot(targetFighterID) != null)
//         {
//             hub.ResetFighter(targetFighterID);
//             return;
//         }

//         ApplyMockData();
//         Debug.Log($"<color=green>[HUD 단독 테스트] 모든 체력 및 내구도 100% 복구 완료!</color>");
//     }

//     // ========================================================================
//     // 인스펙터 우클릭 컨텍스트 메뉴 (플레이 중이 아니어도 즉시 테스트 가능)
//     // ========================================================================

//     [ContextMenu("테스트: 100% 완전 복구 (Reset)")]
//     public void ContextReset() => ResetAllStats();

//     [ContextMenu("테스트: 코어 피격 (-150)")]
//     public void ContextCoreHit() => HitCore(150);

//     [ContextMenu("테스트: 머리 피격 (-30)")]
//     public void ContextHeadHit() => HitPart(BodyPart.Head, 30, 25);

//     [ContextMenu("테스트: 왼팔 피격 (-30)")]
//     public void ContextLeftArmHit() => HitPart(BodyPart.LeftArm, 30, 25);

//     [ContextMenu("테스트: 오른팔 피격 (-30)")]
//     public void ContextRightArmHit() => HitPart(BodyPart.RightArm, 30, 25);

//     [ContextMenu("테스트: 왼다리 피격 (-30)")]
//     public void ContextLeftLegHit() => HitPart(BodyPart.LeftLeg, 30, 25);

//     [ContextMenu("테스트: 오른다리 피격 (-30)")]
//     public void ContextRightLegHit() => HitPart(BodyPart.RightLeg, 30, 25);

//     [ContextMenu("테스트: 전 부위 파괴 및 K.O")]
//     public void ContextAllDestroy()
//     {
//         HitCore(9999);
//         var allParts = new[] { BodyPart.Head, BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg };
//         foreach (var p in allParts)
//         {
//             HitPart(p, 9999, 0);
//         }
//     }

//     private void HandleHpChanged(string fighterId, int currentHp, int maxHp)
//     {
//         // 대상 캐릭터 검사 (공백 무시 및 대소문자 무시)
//         if (!fighterId.Trim().Equals(targetFighterID.Trim(), StringComparison.OrdinalIgnoreCase))
//         {
//             return;
//         }

//         Debug.Log($"<color=lime>[FighterStatusHUD:{targetFighterID}] <b>OnHpChanged 수신</b> -> HP: {currentHp}/{maxHp}</color>");
//         UpdateCoreHpDisplay(currentHp, maxHp);

//         if (widgetLookup.TryGetValue(BodyPart.Core, out var coreWidget))
//         {
//             coreWidget.UpdateDurability(currentHp, maxHp);
//         }

//         if (currentHp <= 0)
//         {
//             Debug.Log($"<color=red><b>[전투 종료] {targetFighterID} 코어 체력 0 도달 (K.O)</b></color>");
//             if (coreWidget != null)
//             {
//                 coreWidget.UpdateDurability(0, maxHp);
//             }
//         }
//     }

//     private void HandlePartDurabilityChanged(string fighterId, BodyPart part, int currentHp, int maxHp)
//     {
//         // 대상 캐릭터 검사
//         if (!fighterId.Trim().Equals(targetFighterID.Trim(), StringComparison.OrdinalIgnoreCase))
//         {
//             return;
//         }

//         Debug.Log($"<color=yellow>[FighterStatusHUD:{targetFighterID}] <b>OnPartDurabilityChanged 수신</b> -> {part}: {currentHp}/{maxHp}</color>");

//         if (widgetLookup.TryGetValue(part, out var widget))
//         {
//             widget.UpdateDurability(currentHp, maxHp);
//         }
//         else
//         {
//             Debug.LogWarning($"<color=red>[FighterStatusHUD:{targetFighterID}] 수신된 '{part}' 부위에 해당하는 위젯이 widgetLookup에 등록되어 있지 않습니다!</color>");
//         }
//     }

//     private void UpdateCoreHpDisplay(int currentHp, int maxHp)
//     {
//         if (totalHealthSlider != null)
//         {
//             totalHealthSlider.maxValue = maxHp;
//             totalHealthSlider.value = currentHp;
//         }

//         if (totalHealthText != null)
//         {
//             totalHealthText.text = $"<color=#00FF44>{currentHp}</color> <color=#777777>/ {maxHp}</color>";
//         }
//     }
// }

