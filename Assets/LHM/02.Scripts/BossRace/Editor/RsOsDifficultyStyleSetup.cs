using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static RealSteel.BossRace.EditorTools.RsOsStyleKit;

namespace RealSteel.BossRace.EditorTools
{
    public static class RsOsDifficultyStyleSetup
    {
        private const string Title = "RS-OS 스타일 난이도 선택";
        internal const string PrefabPath = "Assets/LHM/05.Prefab/Dialogue/DifficultyCanvas.prefab";

        private static readonly Color Illust = Hex("202326");
        private static readonly Color ChipFill = Hex("3A3D41");
        private static readonly Color PartLow = Hex("FF8A2A");
        private static readonly Color PartBroken = Hex("E8473C");

        internal static bool IsApplied() => RsOsStyleKit.IsApplied(PrefabPath);

        internal static void Setup()
        {
            Run(Title, PrefabPath,
                "- 패널 · 난이도 줄 · 버튼: 오른쪽 위 · 왼쪽 아래가 깎인 모서리\n" +
                "- 강조색: 금색 → RS-OS 노란색, 보스 · 선택 패널에 노란 모서리 괄호\n" +
                "- 지역 태그 · 레이스 시작: 노란 사선 칩\n",
                "DialogueTest_Prefab 씬에서 Play → 접수원 E → 토벌 신청으로 확인하세요.",
                Apply);
        }

        private static bool Apply(GameObject root, RsOsSprites sprites, StringBuilder log)
        {
            var ui = root.GetComponentInChildren<BossDifficultySelectUI>(true);
            var uiSo = ui != null ? new SerializedObject(ui) : null;
            var panelObject = uiSo?.FindProperty("root").objectReferenceValue as GameObject;
            Transform layout = panelObject != null ? panelObject.transform.Find("Layout") : null;
            if (layout == null)
            {
                EditorUtility.DisplayDialog(Title, "BossDifficultySelectUI 또는 Panel/Layout을 찾지 못했습니다.", "확인");
                return false;
            }

            RecolorGold(root, log);
            SetColor(panelObject.GetComponent<Image>(), Page);
            Header(layout, sprites, "RS-OS  //  GUILD HUNT  ·  BOSS RACE  ·  DIFFICULTY", log);

            Transform boss = layout.Find("Boss");
            if (boss != null)
            {
                Fill(boss.GetComponent<Image>(), sprites, Panel);
                Fill(Find<Image>(boss, "Illustration"), sprites, Illust);
                Chip(Find<Image>(boss, "RegionTag"), sprites);
                for (int i = 1; i <= 4; i++) Fill(Find<Image>(boss, $"Chip{i}"), sprites, ChipFill, 2f);
                Fill(Find<Image>(boss, "LoseBox"), sprites, Box);
                AddOutline(boss, sprites, Edge);
                AddBrackets(boss);
                log.AppendLine("- 보스 패널: 깎인 모서리 + 노란 모서리 괄호, 지역 태그 노란 칩, 부위 칩 · 패배 박스 깎인 모서리");
            }

            var rowsProperty = uiSo.FindProperty("rows");
            for (int i = 0; i < rowsProperty.arraySize; i++)
            {
                if (rowsProperty.GetArrayElementAtIndex(i).objectReferenceValue is DifficultyRow row) StyleRow(row, sprites);
            }
            log.AppendLine($"- 난이도 줄 {rowsProperty.arraySize}개: 깎인 모서리, 선택 테두리 노란색");

            Transform selected = layout.Find("Selected");
            if (selected != null)
            {
                Fill(selected.GetComponent<Image>(), sprites, Panel);
                Outline(Find<Image>(selected, "Frame"), sprites, Yellow);
                AddBrackets(selected);
                log.AppendLine("- 선택한 난이도: 깎인 모서리 + 노란 테두리 + 모서리 괄호");
            }

            Transform parts = layout.Find("Parts");
            if (parts != null)
            {
                Fill(parts.GetComponent<Image>(), sprites, Panel);
                Outline(Find<Image>(parts, "Frame"), sprites, Edge);
                for (int i = 1; i <= 5; i++) SetColor(Find<Image>(parts, $"Bar{i}"), Box);
                log.AppendLine("- 파츠 내구도: 깎인 모서리, 내구도 색 노랑 · 주황 · 빨강");
            }

            Chip(Find<Image>(layout, "StartButton"), sprites);
            log.AppendLine("- 레이스 시작: 노란 사선 버튼");

            uiSo.FindProperty("accentColor").colorValue = Yellow;
            uiSo.FindProperty("partGood").colorValue = Yellow;
            uiSo.FindProperty("partLow").colorValue = PartLow;
            uiSo.FindProperty("partBroken").colorValue = PartBroken;
            uiSo.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static void StyleRow(DifficultyRow row, RsOsSprites sprites)
        {
            var so = new SerializedObject(row);
            Fill(so.FindProperty("background").objectReferenceValue as Image, sprites, Panel);
            Outline(so.FindProperty("frame").objectReferenceValue as Image, sprites, Edge);
            so.FindProperty("backgroundNormal").colorValue = Tint(Panel);
            so.FindProperty("backgroundSelected").colorValue = Tint(PanelSelected);
            so.FindProperty("frameNormal").colorValue = Edge;
            so.FindProperty("frameSelected").colorValue = Yellow;
            so.FindProperty("frameScaleNormal").floatValue = Cut;
            so.FindProperty("frameScaleSelected").floatValue = Cut;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (so.FindProperty("stripe").objectReferenceValue is Image stripe)
            {
                RectTransform rect = stripe.rectTransform;
                rect.anchoredPosition = new Vector2(10f, -14f);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, ((RectTransform)row.transform).sizeDelta.y - 28f);
            }
        }
    }
}
