using System.Collections;
using System.Linq;
using RealSteel.Tutorial;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.TestTools
{
    // ======================== [TEST ONLY] 테스트 전용 ========================
    // 용도: F9 = 제작 · 수리 테스트 재료 지급 (세이브에 그대로 저장됨)
    // 위치: WorkshopTestKitSetup이 필요할 때 씬에 배치
    // 실제 게임 로직이 아님. 출시 빌드 씬/프리팹에 넣지 말 것
    // ==========================================================================
    // 제작 · 수리 영상 촬영용 테스트 지급 (Home_Workshop 1 등). Play하면 자동으로 한 번 지급, F9로 다시 지급
    //  - 제작: 모든 부품(재료)을 최대 개수까지 → 어떤 레시피든 바로 조합 가능
    //  - 수리: 5부위에 파츠 장착 + 내구도를 부위마다 다르게 깎음 / 잔해 1개 / 코어 불안정 (복구 화면용)
    //  - 골드: 수리비가 모자라지 않게 목표 금액까지 채움
    // KDU InventoryHost · LSH GlobalGold는 공개 함수만 씀 (그쪽 코드는 수정하지 않음)
    // 주의: 인벤토리 · 골드 세이브에 그대로 저장됨 (테스트용 세이브라고 생각하고 쓰기)
    public class WorkshopTestKit : MonoBehaviour
    {
        private static readonly Slot[] BodySlots = { Slot.Head, Slot.LeftArm, Slot.RightArm, Slot.LeftLeg, Slot.RightLeg };

        [Header("지급")]
        [SerializeField] private bool giveOnStart = true;
        [SerializeField] private Key reapplyKey = Key.F9;

        [Header("제작")]
        [Tooltip("모든 부품을 최대 개수까지 채움")]
        [SerializeField] private bool fillComponents = true;

        [Header("수리")]
        [SerializeField] private bool equipDamagedParts = true;
        [Tooltip("부위별 남길 내구도 비율 (머리 · 왼팔 · 오른팔 · 왼다리 · 오른다리). 1 = 멀쩡")]
        [SerializeField] private float[] durabilityRatios = { 0.8f, 0.45f, 0.2f, 0.6f, 0.05f };
        [Tooltip("복구 화면용 잔해(부서진 파츠) 1개")]
        [SerializeField] private bool addWreck = true;
        [Tooltip("복구 화면용 코어 불안정")]
        [SerializeField] private bool unstableCore = true;

        [Header("골드")]
        [Min(0)] [SerializeField] private int goldTarget = 50000;

        [Header("촬영 중 방해 줄이기")]
        [Tooltip("제작 · 수리 · 인벤토리 튜토리얼이 촬영 중에 뜨지 않게 본 것으로 처리 (Ctrl + F10으로 되돌림)")]
        [SerializeField] private bool skipWorkshopTutorials = true;
        [SerializeField] private string[] tutorialIds = { "crafting", "repair", "inventory" };

        private IEnumerator Start()
        {
            if (!giveOnStart) yield break;

            // InventoryHost는 자동 생성 · 세이브 읽기가 끝나야 그리드가 생김
            float waited = 0f;
            while ((InventoryHost.Instance == null || InventoryHost.Instance.Grid == null) && waited < 5f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Apply();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && reapplyKey != Key.None && keyboard[reapplyKey].wasPressedThisFrame) Apply();
        }

        [ContextMenu("지금 지급")]
        public void Apply()
        {
            InventoryHost host = InventoryHost.Instance;
            if (host == null || host.Grid == null)
            {
                Debug.LogWarning("[TestKit] InventoryHost가 없어 지급하지 못했습니다. 씬에 InventorySystem이 있는지 확인하세요.", this);
                return;
            }

            var log = new System.Text.StringBuilder("[TestKit] 제작 · 수리 테스트 지급\n");
            if (fillComponents) FillComponents(host, log);
            if (equipDamagedParts) EquipDamaged(host, log);
            if (addWreck) AddWreck(host, log);
            if (unstableCore && !host.Save.Core.IsUnstable)
            {
                host.Save.Core.IsUnstable = true;
                log.AppendLine("- 코어 불안정 (복구 화면)");
            }
            TopUpGold(log);
            if (skipWorkshopTutorials) SkipTutorials(log);

            host.NotifyChanged();
            log.Append($"다시 지급: {reapplyKey}");
            Debug.Log(log.ToString(), this);
        }

        private static void FillComponents(InventoryHost host, System.Text.StringBuilder log)
        {
            ComponentCatalog catalog = host.ComponentCatalog;
            if (catalog == null || catalog.Components == null)
            {
                log.AppendLine("- 부품: 부품 목록(ComponentCatalog)이 없어 건너뜀");
                return;
            }

            int kinds = 0, total = 0, noRoom = 0;
            foreach (ComponentDefinition component in catalog.Components)
            {
                if (component == null) continue;

                int room = component.RemainingCapacity(host.Save.Components.Get(component.Id));
                if (room <= 0) { kinds++; continue; }
                if (!host.CanGiveComponent(component, room)) { noRoom++; continue; }

                int added = host.GiveComponent(component, room);
                if (added > 0) { kinds++; total += added; }
            }
            log.AppendLine($"- 부품: {kinds}종을 최대 개수로 (이번에 {total}개 추가{(noRoom > 0 ? $", 인벤토리 칸이 없어 {noRoom}종 못 넣음" : "")})");
        }

        private void EquipDamaged(InventoryHost host, System.Text.StringBuilder log)
        {
            PartsCatalog catalog = host.Catalog;
            if (catalog == null || catalog.Parts == null)
            {
                log.AppendLine("- 파츠: 파츠 목록(PartsCatalog)이 없어 건너뜀");
                return;
            }

            for (int i = 0; i < BodySlots.Length; i++)
            {
                Slot slot = BodySlots[i];
                PartsDefinition equipped = host.GetEquipped(slot);
                if (equipped == null)
                {
                    // 기본 장비 → 같은 부위 아무 파츠 순으로
                    equipped = (catalog.StartingEquipment ?? new PartsDefinition[0]).FirstOrDefault(p => p != null && p.Slot == slot)
                               ?? catalog.Parts.FirstOrDefault(p => p != null && p.Slot == slot);
                    if (equipped == null)
                    {
                        log.AppendLine($"- {slot}: 이 부위 파츠가 목록에 없음");
                        continue;
                    }
                    host.Save.Equipment.Set(slot, equipped.Id);
                }

                float ratio = i < durabilityRatios.Length ? Mathf.Clamp01(durabilityRatios[i]) : 0.5f;
                int durability = Mathf.Max(1, Mathf.RoundToInt(equipped.Durability * ratio));
                host.SetDurability(slot, durability);
                log.AppendLine($"- {slot}: {equipped.Id} 장착, 내구도 {durability}/{equipped.Durability}");
            }
        }

        private static void AddWreck(InventoryHost host, System.Text.StringBuilder log)
        {
            if (host.Save.Wrecks.Count > 0)
            {
                log.AppendLine($"- 잔해: 이미 {host.Save.Wrecks.Count}개 있음");
                return;
            }

            PartsDefinition wreck = host.Catalog != null && host.Catalog.Parts != null
                ? host.Catalog.Parts.FirstOrDefault(p => p != null && p.Slot != Slot.Core)
                : null;
            if (wreck == null) return;

            host.Save.Wrecks.Add(wreck.Id);
            log.AppendLine($"- 잔해: {wreck.Id} (복구 화면)");
        }

        private void TopUpGold(System.Text.StringBuilder log)
        {
            if (goldTarget <= 0) return;
            if (!GlobalGoldWallet.IsAvailable)
            {
                log.AppendLine("- 골드: 골드 시스템(GlobalGold)이 준비되지 않아 건너뜀");
                return;
            }

            int current = GlobalGold.Instance.PlayerGold;
            if (current >= goldTarget)
            {
                log.AppendLine($"- 골드: 이미 {current:N0}G");
                return;
            }

            GlobalGoldWallet.AddPlayerGold(goldTarget - current);
            log.AppendLine($"- 골드: {current:N0}G → {GlobalGold.Instance.PlayerGold:N0}G");
        }

        private void SkipTutorials(System.Text.StringBuilder log)
        {
            int count = 0;
            foreach (string id in tutorialIds)
            {
                if (string.IsNullOrEmpty(id) || TutorialPanel.HasSeen(id)) continue;
                TutorialPanel.MarkSeen(id);
                count++;
            }
            if (count > 0) log.AppendLine($"- 튜토리얼 {count}개 본 것으로 처리 (촬영 방해 방지, Ctrl + F10으로 되돌림)");
        }
    }
}
