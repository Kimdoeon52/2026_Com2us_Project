using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static RealSteel.BossRace.EditorTools.RsOsStyleKit;

namespace RealSteel.BossRace.EditorTools
{
    public static class RsOsRegionStyleSetup
    {
        private const string Title = "RS-OS 스타일 지역 보스 선택";
        internal const string PrefabPath = "Assets/LHM/05.Prefab/Dialogue/BossRaceCanvas.prefab";

        private static readonly Color TagOff = Hex("2E3135");
        private static readonly Color ConfirmOff = Hex("3A3D42");
        private static readonly Color LockShade = new Color(0.42f, 0.44f, 0.47f, 0.28f);

        internal static bool IsApplied() => RsOsStyleKit.IsApplied(PrefabPath);

        internal static void Setup()
        {
            Run(Title, PrefabPath,
                "- 보스 카드 · 상세 패널 · 버튼: 오른쪽 위 · 왼쪽 아래가 깎인 모서리\n" +
                "- 강조색: 금색 → RS-OS 노란색, 선택 카드 테두리 노란색\n" +
                "- 지역 태그 · 난이도 선택 버튼: 노란 사선 칩 (잠김 · 비활성은 회색 칩)\n" +
                "- 상세 패널에 노란 모서리 괄호\n",
                "DialogueTest_Prefab 씬에서 Play → Shift+Y → Shift+T → 전화 Z로 받기 → 지역 보스 선택으로 확인하세요.",
                Apply);
        }

        private static bool Apply(GameObject root, RsOsSprites sprites, StringBuilder log)
        {
            var ui = root.GetComponentInChildren<BossRaceSelectUI>(true);
            var uiSo = ui != null ? new SerializedObject(ui) : null;
            var panelObject = uiSo?.FindProperty("root").objectReferenceValue as GameObject;
            Transform layout = panelObject != null ? panelObject.transform.Find("Layout") : null;
            if (layout == null)
            {
                EditorUtility.DisplayDialog(Title, "BossRaceSelectUI 또는 Panel/Layout을 찾지 못했습니다.", "확인");
                return false;
            }

            RecolorGold(root, log);
            SetColor(panelObject.GetComponent<Image>(), Page);
            Header(layout, sprites, "RS-OS  //  GUILD HUNT  ·  BOSS RACE  ·  REGION", log);

            var cards = uiSo.FindProperty("cards");
            for (int i = 0; i < cards.arraySize; i++)
            {
                if (cards.GetArrayElementAtIndex(i).objectReferenceValue is BossRaceCard card) StyleCard(card, sprites);
            }
            log.AppendLine($"- 보스 카드 {cards.arraySize}장: 깎인 모서리, 선택 테두리 · 진행 중 표시 노란색, 지역 태그 칩");

            Transform detail = layout.Find("Detail");
            if (detail != null)
            {
                Fill(detail.GetComponent<Image>(), sprites, Panel);
                Outline(Find<Image>(detail, "Frame"), sprites, Edge);
                AddBrackets(detail);
                log.AppendLine("- 선택한 보스 패널: 깎인 모서리 + 노란 모서리 괄호");
            }

            if (uiSo.FindProperty("confirmImage").objectReferenceValue is Image confirm)
            {
                Chip(confirm, sprites);
                uiSo.FindProperty("confirmSpriteOn").objectReferenceValue = sprites.Chip;
                uiSo.FindProperty("confirmSpriteOff").objectReferenceValue = sprites.ChipOff;
                uiSo.FindProperty("confirmOn").colorValue = Color.white;
                uiSo.FindProperty("confirmOff").colorValue = Tint(ConfirmOff);
                log.AppendLine("- 난이도 선택 버튼: 노란 사선 버튼 (선택 불가 시 회색)");
            }

            uiSo.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static void StyleCard(BossRaceCard card, RsOsSprites sprites)
        {
            var so = new SerializedObject(card);
            Fill(so.FindProperty("background").objectReferenceValue as Image, sprites, Panel);
            Outline(so.FindProperty("frame").objectReferenceValue as Image, sprites, Edge);

            if (so.FindProperty("regionTag").objectReferenceValue is Image tag)
            {
                Chip(tag, sprites);
                so.FindProperty("tagSpriteActive").objectReferenceValue = sprites.Chip;
                so.FindProperty("tagSpriteInactive").objectReferenceValue = sprites.ChipOff;
            }

            if (so.FindProperty("lockOverlay").objectReferenceValue is GameObject overlay)
            {
                Fill(overlay.GetComponent<Image>(), sprites, LockShade);
            }

            so.FindProperty("backgroundNormal").colorValue = Tint(Panel);
            so.FindProperty("backgroundSelected").colorValue = Tint(PanelSelected);
            so.FindProperty("frameNormal").colorValue = Edge;
            so.FindProperty("frameSelected").colorValue = Yellow;
            so.FindProperty("frameScaleNormal").floatValue = Cut;
            so.FindProperty("frameScaleSelected").floatValue = Cut;
            so.FindProperty("accentColor").colorValue = Yellow;
            so.FindProperty("tagActive").colorValue = Color.white;
            so.FindProperty("tagInactive").colorValue = Tint(TagOff);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
