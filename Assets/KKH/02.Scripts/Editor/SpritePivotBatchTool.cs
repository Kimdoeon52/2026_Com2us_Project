using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace RealSteel.EditorTools
{
    /// <summary>
    /// 여러 스프라이트 / 스프라이트 시트의 피봇(Pivot)과 메시 타입(Mesh Type)을 일괄 변경하는 에디터 툴 (Unity 6 호환)
    /// </summary>
    public class SpritePivotBatchTool : EditorWindow
    {
        public enum PivotPreset
        {
            Center,
            TopLeft,
            TopCenter,
            TopRight,
            LeftCenter,
            RightCenter,
            BottomLeft,
            BottomCenter,
            BottomRight,
            Custom
        }

        private List<Texture2D> _targetTextures = new List<Texture2D>();
        private PivotPreset _preset = PivotPreset.Center;
        private Vector2 _customPivot = new Vector2(0.5f, 0.5f);
        private bool _setMeshTypeFullRect = true;
        private Vector2 _scrollPos;

        [MenuItem("Tools/RealSteel/Sprite Pivot Batch Tool")]
        [MenuItem("Assets/2D Sprites/Open Pivot Batch Tool...", false, 20)]
        public static void OpenWindow()
        {
            var win = GetWindow<SpritePivotBatchTool>("Sprite Pivot Tool");
            win.minSize = new Vector2(380, 520);
            win.LoadFromSelection();
            win.Show();
        }

        private void OnEnable()
        {
            if (_targetTextures.Count == 0)
            {
                LoadFromSelection();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("스프라이트 피봇 일괄 변경 툴 (Unity 6)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "선택한 스프라이트 및 스프라이트 시트(Multiple)의 모든 슬라이스 프레임 피봇을 일괄 설정합니다.\n" +
                "파티클 시스템용 텍스처 시트의 왜곡 방지를 위해 Full Rect 옵션을 켤 수 있습니다.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            // 1. Pivot 설정 섹션
            DrawPivotSettings();

            EditorGUILayout.Space(8);

            // 2. 추가 옵션 섹션
            DrawOptions();

            EditorGUILayout.Space(8);

            // 3. 타겟 텍스처 목록 섹션
            DrawTargetList();

            EditorGUILayout.Space(10);

            // 4. 실행 버튼
            GUI.enabled = _targetTextures.Count > 0;
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
            if (GUILayout.Button($"선택된 {_targetTextures.Count}개 텍스처 피봇 일괄 적용", GUILayout.Height(38)))
            {
                ApplyPivotBatch();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            EditorGUILayout.Space(6);
        }

        private void DrawPivotSettings()
        {
            EditorGUILayout.LabelField("1. 피봇 프리셋 설정", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _preset = (PivotPreset)EditorGUILayout.EnumPopup("Pivot Preset", _preset);
            if (EditorGUI.EndChangeCheck())
            {
                SyncPivotWithPreset();
            }

            // 3x3 시각적 버튼 패드
            EditorGUILayout.LabelField("빠른 프리셋 선택 (3x3):", EditorStyles.miniLabel);
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.BeginHorizontal();
                DrawPresetButton("Top-Left", PivotPreset.TopLeft);
                DrawPresetButton("Top-Center", PivotPreset.TopCenter);
                DrawPresetButton("Top-Right", PivotPreset.TopRight);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                DrawPresetButton("Left", PivotPreset.LeftCenter);
                DrawPresetButton("Center", PivotPreset.Center);
                DrawPresetButton("Right", PivotPreset.RightCenter);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                DrawPresetButton("Bottom-Left", PivotPreset.BottomLeft);
                DrawPresetButton("Bottom-Center", PivotPreset.BottomCenter);
                DrawPresetButton("Bottom-Right", PivotPreset.BottomRight);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            if (_preset == PivotPreset.Custom)
            {
                _customPivot = EditorGUILayout.Vector2Field("Custom Pivot (0.0 ~ 1.0)", _customPivot);
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Vector2Field("Applied Pivot Value", _customPivot);
                EditorGUI.EndDisabledGroup();
            }
        }

        private void DrawPresetButton(string label, PivotPreset preset)
        {
            bool isCurrent = _preset == preset;
            if (isCurrent) GUI.backgroundColor = new Color(0.4f, 0.7f, 1.0f);
            if (GUILayout.Button(label, GUILayout.Height(24)))
            {
                _preset = preset;
                SyncPivotWithPreset();
            }
            if (isCurrent) GUI.backgroundColor = Color.white;
        }

        private void SyncPivotWithPreset()
        {
            switch (_preset)
            {
                case PivotPreset.Center:
                    _customPivot = new Vector2(0.5f, 0.5f);
                    break;
                case PivotPreset.TopLeft:
                    _customPivot = new Vector2(0.0f, 1.0f);
                    break;
                case PivotPreset.TopCenter:
                    _customPivot = new Vector2(0.5f, 1.0f);
                    break;
                case PivotPreset.TopRight:
                    _customPivot = new Vector2(1.0f, 1.0f);
                    break;
                case PivotPreset.LeftCenter:
                    _customPivot = new Vector2(0.0f, 0.5f);
                    break;
                case PivotPreset.RightCenter:
                    _customPivot = new Vector2(1.0f, 0.5f);
                    break;
                case PivotPreset.BottomLeft:
                    _customPivot = new Vector2(0.0f, 0.0f);
                    break;
                case PivotPreset.BottomCenter:
                    _customPivot = new Vector2(0.5f, 0.0f);
                    break;
                case PivotPreset.BottomRight:
                    _customPivot = new Vector2(1.0f, 0.0f);
                    break;
            }
        }

        private void DrawOptions()
        {
            EditorGUILayout.LabelField("2. 추가 옵션", EditorStyles.boldLabel);
            _setMeshTypeFullRect = EditorGUILayout.ToggleLeft(
                "Mesh Type을 'Full Rect'로 변경 (파티클 UV 왜곡 및 에러 방지)",
                _setMeshTypeFullRect);
        }

        private void DrawTargetList()
        {
            EditorGUILayout.LabelField($"3. 대상 텍스처 ({_targetTextures.Count}개)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("현재 선택된 에셋 불러오기"))
            {
                LoadFromSelection();
            }
            if (GUILayout.Button("목록 초기화"))
            {
                _targetTextures.Clear();
            }
            EditorGUILayout.EndHorizontal();

            // 드래그 앤 드롭 영역
            Rect dropArea = GUILayoutUtility.GetRect(0f, 32f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "여기에 텍스처/스프라이트를 드래그 앤 드롭하여 추가하세요", EditorStyles.helpBox);
            HandleDragAndDrop(dropArea);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(150));
            for (int i = 0; i < _targetTextures.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _targetTextures[i] = (Texture2D)EditorGUILayout.ObjectField(_targetTextures[i], typeof(Texture2D), false);
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    _targetTextures.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void HandleDragAndDrop(Rect dropArea)
        {
            Event evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (!dropArea.Contains(evt.mousePosition))
                    return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is Texture2D tex && !_targetTextures.Contains(tex))
                        {
                            _targetTextures.Add(tex);
                        }
                        else if (obj is Sprite spr && spr.texture != null && !_targetTextures.Contains(spr.texture))
                        {
                            _targetTextures.Add(spr.texture);
                        }
                    }
                }
                evt.Use();
            }
        }

        public void LoadFromSelection()
        {
            _targetTextures.Clear();
            foreach (var obj in Selection.objects)
            {
                if (obj is Texture2D tex && !_targetTextures.Contains(tex))
                {
                    _targetTextures.Add(tex);
                }
                else if (obj is Sprite spr && spr.texture != null && !_targetTextures.Contains(spr.texture))
                {
                    _targetTextures.Add(spr.texture);
                }
            }
        }

        private void ApplyPivotBatch()
        {
            if (_targetTextures.Count == 0) return;

            SpriteAlignment targetAlignment = GetSpriteAlignmentFromPreset(_preset);
            Vector2 targetPivot = _customPivot;

            int processedCount = 0;
            int totalSlicesUpdated = 0;

            var factory = new SpriteDataProviderFactories();
            factory.Init();

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < _targetTextures.Count; i++)
                {
                    var tex = _targetTextures[i];
                    if (tex == null) continue;

                    string path = AssetDatabase.GetAssetPath(tex);
                    EditorUtility.DisplayProgressBar("스프라이트 피봇 일괄 적용 중...", $"{tex.name} ({i + 1}/{_targetTextures.Count})", (float)i / _targetTextures.Count);

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    bool changed = false;

                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        changed = true;
                    }

                    // 1. TextureImporterSettings를 통한 단일 스프라이트 피봇 및 메시 타입 설정
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);

                    if (settings.spriteAlignment != (int)targetAlignment || settings.spritePivot != targetPivot)
                    {
                        settings.spriteAlignment = (int)targetAlignment;
                        settings.spritePivot = targetPivot;
                        changed = true;
                    }

                    if (_setMeshTypeFullRect && settings.spriteMeshType != SpriteMeshType.FullRect)
                    {
                        settings.spriteMeshType = SpriteMeshType.FullRect;
                        changed = true;
                    }

                    if (changed)
                    {
                        importer.SetTextureSettings(settings);
                    }

                    // 2. ISpriteEditorDataProvider를 통한 스프라이트 시트(Multiple) 모든 슬라이스 피봇 설정 (Unity 6 호환)
                    if (importer.spriteImportMode == SpriteImportMode.Multiple)
                    {
                        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
                        if (dataProvider != null)
                        {
                            dataProvider.InitSpriteEditorDataProvider();
                            var spriteRects = dataProvider.GetSpriteRects();
                            if (spriteRects != null && spriteRects.Length > 0)
                            {
                                for (int s = 0; s < spriteRects.Length; s++)
                                {
                                    spriteRects[s].alignment = targetAlignment;
                                    spriteRects[s].pivot = targetPivot;
                                    totalSlicesUpdated++;
                                }
                                dataProvider.SetSpriteRects(spriteRects);
                                dataProvider.Apply();
                                changed = true;
                            }
                        }
                    }
                    else
                    {
                        totalSlicesUpdated++;
                    }

                    if (changed)
                    {
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                        processedCount++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"[SpritePivotBatchTool] 피봇 변경 완료: {processedCount}개 텍스처, 총 {totalSlicesUpdated}개 스프라이트 프레임 (Pivot: {targetPivot}, FullRect: {_setMeshTypeFullRect})");
            EditorUtility.DisplayDialog("완료", $"피봇 변경이 완료되었습니다!\n텍스처: {processedCount}개\n적용된 스프라이트: {totalSlicesUpdated}개\nPivot: {targetPivot}", "확인");
        }

        private static SpriteAlignment GetSpriteAlignmentFromPreset(PivotPreset preset)
        {
            switch (preset)
            {
                case PivotPreset.Center: return SpriteAlignment.Center;
                case PivotPreset.TopLeft: return SpriteAlignment.TopLeft;
                case PivotPreset.TopCenter: return SpriteAlignment.TopCenter;
                case PivotPreset.TopRight: return SpriteAlignment.TopRight;
                case PivotPreset.LeftCenter: return SpriteAlignment.LeftCenter;
                case PivotPreset.RightCenter: return SpriteAlignment.RightCenter;
                case PivotPreset.BottomLeft: return SpriteAlignment.BottomLeft;
                case PivotPreset.BottomCenter: return SpriteAlignment.BottomCenter;
                case PivotPreset.BottomRight: return SpriteAlignment.BottomRight;
                default: return SpriteAlignment.Custom;
            }
        }

        #region Right-Click Context Menus (빠른 우클릭 메뉴)

        [MenuItem("Assets/2D Sprites/Quick Set Pivot/Center (0.5, 0.5)", false, 1)]
        private static void QuickSetPivotCenter()
        {
            ApplyQuickPivot(SpriteAlignment.Center, new Vector2(0.5f, 0.5f), false);
        }

        [MenuItem("Assets/2D Sprites/Quick Set Pivot/Bottom Center (0.5, 0.0)", false, 2)]
        private static void QuickSetPivotBottomCenter()
        {
            ApplyQuickPivot(SpriteAlignment.BottomCenter, new Vector2(0.5f, 0.0f), false);
        }

        [MenuItem("Assets/2D Sprites/Quick Set Pivot/Center + Full Rect", false, 3)]
        private static void QuickSetPivotCenterAndFullRect()
        {
            ApplyQuickPivot(SpriteAlignment.Center, new Vector2(0.5f, 0.5f), true);
        }

        [MenuItem("Assets/2D Sprites/Mesh Type/Convert to Full Rect", false, 4)]
        private static void QuickSetMeshTypeFullRect()
        {
            var textures = GetSelectedTextures();
            if (textures.Count == 0) return;

            AssetDatabase.StartAssetEditing();
            int count = 0;
            try
            {
                foreach (var tex in textures)
                {
                    string path = AssetDatabase.GetAssetPath(tex);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);

                    if (settings.spriteMeshType != SpriteMeshType.FullRect)
                    {
                        settings.spriteMeshType = SpriteMeshType.FullRect;
                        importer.SetTextureSettings(settings);
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                        count++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"[SpritePivotBatchTool] {count}개 텍스처를 Full Rect로 변경 완료했습니다.");
        }

        private static void ApplyQuickPivot(SpriteAlignment alignment, Vector2 pivot, bool setFullRect)
        {
            var textures = GetSelectedTextures();
            if (textures.Count == 0)
            {
                EditorUtility.DisplayDialog("알림", "선택된 텍스처 또는 스프라이트가 없습니다.", "확인");
                return;
            }

            var factory = new SpriteDataProviderFactories();
            factory.Init();

            AssetDatabase.StartAssetEditing();
            int totalSlices = 0;
            try
            {
                foreach (var tex in textures)
                {
                    string path = AssetDatabase.GetAssetPath(tex);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);

                    settings.spriteAlignment = (int)alignment;
                    settings.spritePivot = pivot;
                    if (setFullRect)
                    {
                        settings.spriteMeshType = SpriteMeshType.FullRect;
                    }
                    importer.SetTextureSettings(settings);

                    if (importer.spriteImportMode == SpriteImportMode.Multiple)
                    {
                        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
                        if (dataProvider != null)
                        {
                            dataProvider.InitSpriteEditorDataProvider();
                            var spriteRects = dataProvider.GetSpriteRects();
                            if (spriteRects != null && spriteRects.Length > 0)
                            {
                                for (int s = 0; s < spriteRects.Length; s++)
                                {
                                    spriteRects[s].alignment = alignment;
                                    spriteRects[s].pivot = pivot;
                                    totalSlices++;
                                }
                                dataProvider.SetSpriteRects(spriteRects);
                                dataProvider.Apply();
                            }
                        }
                    }
                    else
                    {
                        totalSlices++;
                    }

                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"[SpritePivotBatchTool] 우클릭 퀵 적용 완료: {textures.Count}개 텍스처 ({totalSlices}개 슬라이스) -> Pivot: {pivot}");
        }

        private static List<Texture2D> GetSelectedTextures()
        {
            var list = new List<Texture2D>();
            foreach (var obj in Selection.objects)
            {
                if (obj is Texture2D tex && !list.Contains(tex))
                {
                    list.Add(tex);
                }
                else if (obj is Sprite spr && spr.texture != null && !list.Contains(spr.texture))
                {
                    list.Add(spr.texture);
                }
            }
            return list;
        }

        #endregion
    }
}
