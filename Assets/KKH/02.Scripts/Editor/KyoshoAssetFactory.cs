#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RealSteel.Boss.Kyosho;
using RealSteel.NanoBoss;
using RealSteel.NanoBoss.EditorTools;

namespace RealSteel.Boss.Editor
{
    /// <summary>
    /// [3보스 '쿄쇼 (Kyosho)' 에셋 · 픽셀 스프라이트 · 프리팹 · 테스트 씬 원클릭 생성/배선 툴]
    /// </summary>
    [InitializeOnLoad]
    public static class KyoshoAssetFactory
    {
        private const string SoDir = "Assets/KKH/03.SOData/Boss/Kyosho";
        private const string ShapesDir = SoDir + "/Shapes";
        private const string SpriteDir = "Assets/KKH/04.Prefab/BOSS/Third_Boss/Sprites";
        private const string MaestroSpriteDir = SpriteDir + "/Maestro";
        private const string SfxDir = "Assets/KKH/04.Prefab/BOSS/Third_Boss/Sound/SFX";
        private const string BgmDir = "Assets/KKH/04.Prefab/BOSS/Third_Boss/Sound/BGM";
        private const string PrefabPath = "Assets/KKH/04.Prefab/BOSS/Third_Boss/Boss3_Kyosho.prefab";
        private const string BgmPath = "Assets/LSH/09. Sound/FightSound/BGM/보스3.mp3";
        private const string MaestroMovesPath = "Assets/KKH/03.SOData/Boss/Kyosho/Kyosho_Maestro_Moves.asset";
        private const string TestScenePath = "Assets/KKH/01.Scene/Battle_Boss_Test 3.unity";
        private const string Battle3ScenePath = "Assets/KKH/01.Scene/Battle_Boss_Test 3.unity";

        static KyoshoAssetFactory()
        {
            EditorApplication.delayCall += EnsureAssetsGeneratedOnce;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (scene.path == TestScenePath && UnityEngine.Object.FindFirstObjectByType<KyoshoBossController>(FindObjectsInactive.Include) == null)
            {
                GenerateAssetsAndPrefab();
                WireOpenTestScene(scene);
            }
        }

        private static void EnsureAssetsGeneratedOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(PrefabPath) || AssetDatabase.LoadAssetAtPath<BossMasterData>($"{SoDir}/BossMasterData_Kyosho.asset") == null)
            {
                GenerateAssetsAndPrefab();
                if (EditorSceneManager.GetActiveScene().path == TestScenePath)
                {
                    WireOpenTestScene(EditorSceneManager.GetActiveScene());
                }
            }
        }

        [MenuItem("RealSteel/Boss/Generate Region 3 Boss (Kyosho) Assets & Scene", priority = 20)]
        public static void GenerateAll()
        {
            GenerateAssetsAndPrefab();
            WireTestScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[KyoshoAssetFactory] 3보스 '쿄쇼' 에셋, 스프라이트, 프리팹, 테스트 씬(Battle_Boss_Test 3) 일괄 구성 완료!</color>");
        }

        public static void GenerateAssetsAndPrefab()
        {
            EnsureDirectories();

            // 1. 6종 음악 문양 · 4프레임 오케스트라 본체 · 3종 악기 섹션(2프레임) 픽셀아트 PNG 생성 및 RSNanoSpriteShape 베이킹
            var symbolShapes = new RSNanoSpriteShape[6];
            for (int i = 0; i < 6; i++)
            {
                string pngPath = $"{SpriteDir}/Symbol_{i}_{GetSymbolFileSlug(i)}.png";
                GenerateSymbolPng(pngPath, i);
                AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                symbolShapes[i] = BakeShapeAsset($"{ShapesDir}/Shape_Symbol_{i}_{GetSymbolFileSlug(i)}.asset", tex);
            }

            var orchestraShape = BakeOrchestraEnsemblePngAndShape();
            var stringsShape = BakeSectionPngAndShape(KyoshoSectionType.Strings, "Strings", KyoshoConstants.StringsMintColor);
            var percShape = BakeSectionPngAndShape(KyoshoSectionType.Percussion, "Percussion", KyoshoConstants.PercussionAmberColor);
            var brassShape = BakeSectionPngAndShape(KyoshoSectionType.Brass, "Brass", KyoshoConstants.BrassMagentaColor);

            BakeMaestroSpriteSheetsAndMoves();

            // 2. RhythmTable & SfxData SO 생성
            var rhythmTable = CreateOrLoadAsset<KyoshoRhythmTableSO>($"{SoDir}/KyoshoRhythmTable.asset");
            rhythmTable.EnsureDefaults();
            for (int i = 0; i < 6; i++)
            {
                rhythmTable.variationSymbols[i].spriteShape = symbolShapes[i];
            }
            rhythmTable.orchestraBodyShape = orchestraShape;
            rhythmTable.stringsSectionShape = stringsShape;
            rhythmTable.percussionSectionShape = percShape;
            rhythmTable.brassSectionShape = brassShape;
            EditorUtility.SetDirty(rhythmTable);

            var sfxData = CreateOrLoadAsset<KyoshoSfxData>($"{SoDir}/KyoshoSfxData.asset");
            GenerateAllSfxWavsAndBind(sfxData);
            EditorUtility.SetDirty(sfxData);

            // 3. 패턴 SO 6종 생성
            var pConduct = CreateOrLoadPattern("Pattern_Kyosho_01_Conduct", KyoshoConstants.PatternConductID, "지휘 도형",
                "1페 4/4(↓←→↑) · 2페 2/4(↓↑×2) 박자 도형 지휘봉 베기. 타악 생존 시 충격파 추가.",
                0f, 0.54f, BodyPart.RightArm, 25, 190);
            var pStaccato = CreateOrLoadPattern("Pattern_Kyosho_02_Staccato", KyoshoConstants.PatternStaccatoID, "스타카토",
                "1마디 머리 위 악보 예고 ➔ 2마디 동일 리듬 직선 음표 탄 발사. 현악 생존 시 16분음표 추가.",
                0f, 0.54f, BodyPart.Head, 25, 160);
            var pBloom = CreateOrLoadPattern("Pattern_Kyosho_03_Bloom", KyoshoConstants.PatternBloomID, "개화",
                "바닥 파편 살포 ➔ 2~3박 줄기 성장(절단 가능) ➔ 4박 만개 공격.",
                0f, 0.54f, BodyPart.LeftLeg, 25, 180);
            var pFermata = CreateOrLoadPattern("Pattern_Kyosho_04_Fermata", KyoshoConstants.PatternFermataID, "페르마타",
                "상공 음표 정지(⌒) 후 일제 수직 낙하. 금관 생존 시 탄 +4발 & 수평 빔 추가.",
                0f, 0.54f, BodyPart.RightLeg, 25, 200);
            var pFinale = CreateOrLoadPattern("Pattern_Kyosho_05_Finale", KyoshoConstants.PatternFinaleID, "피날레",
                "전 봇 집결 & 쉼표 모래 안전지대 생성 ➔ 4마디 전 화면 광역 판정 ➔ 생존 시 4박 마지막 그로기.",
                0f, 0.54f, BodyPart.LeftArm, 35, 260);
            var pVariation = CreateOrLoadPattern("Pattern_Kyosho_06_Variation", KyoshoConstants.PatternVariationID, "변주곡",
                "중앙 무적 변신(도~라 3음) 기억 후 같은 순서로 문양 파괴. 실패 시 불협화음 폭발.",
                0f, 0.54f, BodyPart.Core, 0, 240);

            // 4. BossMasterData_Kyosho 생성
            var master = CreateOrLoadAsset<BossMasterData>($"{SoDir}/BossMasterData_Kyosho.asset");
            master.bossID = KyoshoConstants.BossID;
            master.bossName = KyoshoConstants.BossDisplayName;
            master.regionIndex = 3;
            master.regionBasePrice = 1300;
            master.regionLevelCap = 30;
            master.maxHp = 10000;
            master.baseDefense = 80;
            master.patternPostDelay = 0.54f;
            master.rewardGold = 650;
            master.rewardCoreExp = 312;

            master.difficultyLayers.Clear();
            foreach (BossDifficulty diff in Enum.GetValues(typeof(BossDifficulty)))
            {
                var layer = BossDifficultyLayer.GetDefault(diff);
                layer.allowPhase2 = true; // 쿄쇼는 전 난이도 1페 -> 변주곡① -> 2페 -> 피날레 진행 (§4.2)
                layer.maxAllowedPatterns = 6;
                layer.maxActiveGimmicks = diff <= BossDifficulty.Easy ? 2 : 3;
                master.difficultyLayers.Add(layer);
            }

            master.gimmickParts.Clear();
            master.gimmickParts.Add(new BossGimmickPartData
            {
                gimmickID = KyoshoConstants.SectionStringsID,
                gimmickName = "현악 섹션",
                isPhysicalBossPart = false,
                maxDurability = 800
            });
            master.gimmickParts.Add(new BossGimmickPartData
            {
                gimmickID = KyoshoConstants.SectionPercussionID,
                gimmickName = "타악 섹션",
                isPhysicalBossPart = false,
                maxDurability = 800
            });
            master.gimmickParts.Add(new BossGimmickPartData
            {
                gimmickID = KyoshoConstants.SectionBrassID,
                gimmickName = "금관 섹션",
                isPhysicalBossPart = false,
                maxDurability = 800
            });

            master.patterns.Clear();
            master.patterns.Add(pConduct);
            master.patterns.Add(pStaccato);
            master.patterns.Add(pBloom);
            master.patterns.Add(pFermata);
            master.patterns.Add(pFinale);
            master.patterns.Add(pVariation);

            master.phases.Clear();
            master.phases.Add(new BossPhaseData
            {
                phaseIndex = 1,
                hpThresholdRatio = 1.0f,
                availableSkillIDs = new System.Collections.Generic.List<string>
                {
                    KyoshoConstants.PatternConductID, KyoshoConstants.PatternStaccatoID, KyoshoConstants.PatternBloomID
                }
            });
            master.phases.Add(new BossPhaseData
            {
                phaseIndex = 2,
                hpThresholdRatio = 0.6f,
                availableSkillIDs = new System.Collections.Generic.List<string>
                {
                    KyoshoConstants.PatternConductID, KyoshoConstants.PatternStaccatoID, KyoshoConstants.PatternFermataID, KyoshoConstants.PatternFinaleID
                }
            });
            master.phases.Add(new BossPhaseData
            {
                phaseIndex = 3,
                hpThresholdRatio = KyoshoConstants.Phase3HpRatio,
                availableSkillIDs = new System.Collections.Generic.List<string>
                {
                    KyoshoConstants.PatternConductID, KyoshoConstants.PatternFermataID, KyoshoConstants.PatternFinaleID
                }
            });
            EditorUtility.SetDirty(master);

            // 5. Boss3_Kyosho.prefab 생성
            CreateOrUpdateKyoshoPrefab(master, rhythmTable, sfxData);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateOrUpdateKyoshoPrefab(BossMasterData master, KyoshoRhythmTableSO rhythmTable, KyoshoSfxData sfxData)
        {
            bool loadedFromPrefab = File.Exists(PrefabPath);
            GameObject root = loadedFromPrefab
                ? PrefabUtility.LoadPrefabContents(PrefabPath)
                : new GameObject("Boss3_Kyosho");

            root.tag = "Boss";
            var ctrl = root.GetComponent<KyoshoBossController>() ?? root.AddComponent<KyoshoBossController>();
            ctrl.EditorEnsureAllComponentsAndReferences();

            var so = new SerializedObject(ctrl);
            so.FindProperty("masterData").objectReferenceValue = master;
            so.FindProperty("rhythmTable").objectReferenceValue = rhythmTable;
            so.FindProperty("sfxData").objectReferenceValue = sfxData;
            var moves = AssetDatabase.LoadAssetAtPath<RSNanoKnightMoves>(MaestroMovesPath);
            if (moves != null)
            {
                so.FindProperty("maestroMoves").objectReferenceValue = moves;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (loadedFromPrefab)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            else if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void WireTestScene()
        {
            if (!File.Exists(TestScenePath)) return;
            var scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            WireOpenTestScene(scene);
        }

        [MenuItem("RealSteel/Boss/[2] 본 게임 3지역 보스 씬(Battle_Boss_Test 3)에 쿄쇼(Boss3_Kyosho) 배치 및 연동", priority = 21)]
        public static void WireBattle3Scene()
        {
            if (!File.Exists(Battle3ScenePath))
            {
                Debug.LogError($"[KyoshoAssetFactory] 3지역 보스 전투 씬을 찾을 수 없습니다: {Battle3ScenePath}");
                return;
            }
            GenerateAssetsAndPrefab();
            var scene = EditorSceneManager.OpenScene(Battle3ScenePath, OpenSceneMode.Single);
            WireBattle3OpenScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[KyoshoAssetFactory] 3지역 보스 씬(Battle_Boss_Test 3.unity) 에 쿄쇼(Boss3_Kyosho) 배치 및 BattleManager/Camera/Hub 연동 완료!</color>");
        }

        private static void WireOpenTestScene(UnityEngine.SceneManagement.Scene scene)
        {
            WireCombatSceneCore(scene, addTester: true);
        }

        private static void WireBattle3OpenScene(UnityEngine.SceneManagement.Scene scene)
        {
            // 기존 2보스 복제 플레이스홀더 오브젝트가 남아있으면 비활성화
            string[] placeholderNames =
            {
                "촉수 생성기", "왼손", "양손 협공", "어깨 레이저 X자", "손바닥 압착",
                "마디", "=============Object================="
            };
            foreach (var rootGo in scene.GetRootGameObjects())
            {
                foreach (string name in placeholderNames)
                {
                    if (rootGo.name == name && rootGo.activeSelf)
                    {
                        rootGo.SetActive(false);
                    }
                }
            }

            var otherBosses = UnityEngine.Object.FindObjectsByType<RealSteel.Boss.Core.BossControllerBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in otherBosses)
            {
                if (b != null && !(b is KyoshoBossController) && b.gameObject.activeSelf)
                {
                    b.gameObject.SetActive(false);
                }
            }

            WireCombatSceneCore(scene, addTester: false);
        }

        private static void WireCombatSceneCore(UnityEngine.SceneManagement.Scene scene, bool addTester)
        {
            var existingKyosho = UnityEngine.Object.FindFirstObjectByType<KyoshoBossController>(FindObjectsInactive.Include);
            GameObject bossGo;
            if (existingKyosho != null)
            {
                bossGo = existingKyosho.gameObject;
            }
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                bossGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                bossGo.transform.position = new Vector3(4.5f, 0.119f, 0f);
            }

            var ctrl = bossGo.GetComponent<KyoshoBossController>();
            ctrl?.AutoBindInspectorReferences();

            var mover = UnityEngine.Object.FindFirstObjectByType<RobotMover>();
            if (mover != null)
            {
                var moverSo = new SerializedObject(mover);
                moverSo.FindProperty("opponent").objectReferenceValue = bossGo.transform;
                var speedProp = moverSo.FindProperty("moveSpeed");
                if (speedProp != null && speedProp.floatValue < 3f) speedProp.floatValue = 3f;
                moverSo.ApplyModifiedPropertiesWithoutUndo();

                if (!addTester && mover.GetComponent("TEMP_LegSkillTester") is Behaviour legTester)
                {
                    legTester.enabled = false;
                }
            }

            var cam = UnityEngine.Object.FindFirstObjectByType<CombatCamera>();
            if (cam != null)
            {
                var camSo = new SerializedObject(cam);
                camSo.FindProperty("playerB").objectReferenceValue = bossGo.transform;
                camSo.FindProperty("minX").floatValue = -2.25f;
                camSo.FindProperty("maxX").floatValue = 2.25f;
                camSo.FindProperty("framingPadding").floatValue = 1.8f;
                camSo.FindProperty("closeZ").floatValue = -15f;
                camSo.FindProperty("farZ").floatValue = -24f;
                camSo.FindProperty("fixedY").floatValue = 2.6f;
                camSo.FindProperty("useArenaBounds").boolValue = true;
                camSo.FindProperty("arenaMinX").floatValue = -7.75f;
                camSo.FindProperty("arenaMaxX").floatValue = 7.75f;
                camSo.FindProperty("autoFrameFloor").boolValue = true;
                camSo.FindProperty("stageFloorY").floatValue = 0.12f;
                camSo.FindProperty("floorBottomViewportRatio").floatValue = 0.16f;
                camSo.FindProperty("topHudViewportReserve").floatValue = 0.12f;
                camSo.FindProperty("targetBHalfWidth").floatValue = 2.35f;
                camSo.FindProperty("minTargetTopOffsetFromFloor").floatValue = 4.3f;
                camSo.FindProperty("verticalSmoothTime").floatValue = 0.12f;
                camSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var bm = UnityEngine.Object.FindFirstObjectByType<BattleManager>();
            if (bm != null)
            {
                var bmSo = new SerializedObject(bm);
                if (mover != null) bmSo.FindProperty("playerRobotObject").objectReferenceValue = mover.gameObject;
                bmSo.FindProperty("enemyRobotObject").objectReferenceValue = bossGo;
                if (!addTester) bmSo.FindProperty("timeLimit").floatValue = 0f;
                bmSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var hub = UnityEngine.Object.FindFirstObjectByType<CombatDataHub>();
            if (hub != null)
            {
                hub.BossSnapshot.bossID = KyoshoConstants.BossID;
                hub.BossSnapshot.bossName = "쿄쇼";
                EditorUtility.SetDirty(hub);
            }

            if (!addTester && UnityEngine.Object.FindFirstObjectByType<RealSteel.UI.BattleResultUI>(FindObjectsInactive.Include) == null)
            {
                var resultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KKH/04.Prefab/BattleResultCanvas.prefab");
                if (resultPrefab != null)
                {
                    var resultGo = (GameObject)PrefabUtility.InstantiatePrefab(resultPrefab, scene);
                    resultGo.SetActive(true);
                    var resultUi = resultGo.GetComponent<RealSteel.UI.BattleResultUI>();
                    if (resultUi != null)
                    {
                        var uiSo = new SerializedObject(resultUi);
                        var lobbyProp = uiSo.FindProperty("lobbySceneName");
                        if (lobbyProp != null) lobbyProp.stringValue = "JunkShop_Map";
                        uiSo.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            var existingTester = UnityEngine.Object.FindFirstObjectByType<RealSteel.Boss.Test.KyoshoGimmickTester>(FindObjectsInactive.Include);
            if (addTester && existingTester == null)
            {
                var testerGo = new GameObject("KyoshoGimmickTester");
                testerGo.AddComponent<RealSteel.Boss.Test.KyoshoGimmickTester>();
            }
            else if (!addTester && existingTester != null)
            {
                existingTester.enabled = false;
                var tSo = new SerializedObject(existingTester);
                var overlayProp = tSo.FindProperty("showOverlay");
                if (overlayProp != null) overlayProp.boolValue = false;
                tSo.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.SaveScene(scene);
        }

        private static RSNanoSpriteShape BakeOrchestraEnsemblePngAndShape()
        {
            string pngPath = $"{SpriteDir}/Orchestra_Ensemble.png";
            GenerateOrchestraEnsemblePng(pngPath);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            return BakeShapeAsset(
                $"{ShapesDir}/Shape_Kyosho_Orchestra.asset",
                tex,
                KyoshoNanobotAttackHelper.OrchestraFrameCount,
                1);
        }

        private static RSNanoSpriteShape BakeSectionPngAndShape(KyoshoSectionType type, string slug, Color color)
        {
            string pngPath = $"{SpriteDir}/Section_{slug}.png";
            GenerateSectionPng(pngPath, type, color);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            return BakeShapeAsset(
                $"{ShapesDir}/Shape_Section_{slug}.asset",
                tex,
                KyoshoNanobotAttackHelper.SectionFrameCount,
                1);
        }

        private static RSNanoSpriteShape BakeShapeAsset(string assetPath, Texture2D tex, int columns = 1, int rows = 1)
        {
            var shape = CreateOrLoadAsset<RSNanoSpriteShape>(assetPath);
            shape.sheet = tex;
            shape.columns = Mathf.Max(1, columns);
            shape.rows = Mathf.Max(1, rows);
            shape.pick = NanoSpritePick.Opaque;
            shape.alphaCut = 0.35f;
            shape.pivot = NanoSpritePivot.Center;
            RSNanoSpriteBaker.Bake(shape);
            EditorUtility.SetDirty(shape);
            return shape;
        }

        private static void GenerateOrchestraEnsemblePng(string path)
        {
            int fw = KyoshoNanobotAttackHelper.OrchestraFrameWidth;
            int fh = KyoshoNanobotAttackHelper.OrchestraFrameHeight;
            int fc = KyoshoNanobotAttackHelper.OrchestraFrameCount;
            var tex = new Texture2D(fw * fc, fh, TextureFormat.RGBA32, false);
            for (int f = 0; f < fc; f++)
            {
                for (int y = 0; y < fh; y++)
                {
                    for (int x = 0; x < fw; x++)
                    {
                        Color32 c = KyoshoNanobotAttackHelper.SampleOrchestraEnsemblePixel(f, x, y, fw, fh);
                        tex.SetPixel(f * fw + x, y, c);
                    }
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void GenerateSymbolPng(string path, int symbolIndex)
        {
            const int S = 32;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            Color border = KyoshoConstants.SymbolBorderColors[symbolIndex];
            Color body = Color.white;

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float nx = (x - 15.5f) / 13f;
                    float ny = (y - 15.5f) / 13f;
                    bool on = symbolIndex switch
                    {
                        // 0: 𝄞 높은음자리표
                        0 => Mathf.Abs(nx) < 0.14f
                            || (ny > 0.12f && Mathf.Abs(Vector2.Distance(new Vector2(nx, ny), new Vector2(0.08f, 0.45f)) - 0.32f) < 0.14f)
                            || (ny <= 0.12f && Mathf.Abs(Vector2.Distance(new Vector2(nx, ny), new Vector2(0f, -0.22f)) - 0.40f) < 0.15f),
                        // 1: 𝄢 낮은음자리표
                        1 => (Mathf.Abs(Vector2.Distance(new Vector2(nx, ny), new Vector2(-0.18f, 0.05f)) - 0.45f) < 0.15f && nx > -0.25f)
                            || (Vector2.Distance(new Vector2(nx, ny), new Vector2(0.52f, 0.28f)) < 0.15f)
                            || (Vector2.Distance(new Vector2(nx, ny), new Vector2(0.52f, -0.22f)) < 0.15f),
                        // 2: ♯ 샵
                        2 => Mathf.Abs(nx - 0.25f) < 0.11f || Mathf.Abs(nx + 0.25f) < 0.11f
                            || (Mathf.Abs(ny - 0.22f - nx * 0.22f) < 0.12f && Mathf.Abs(nx) < 0.72f)
                            || (Mathf.Abs(ny + 0.22f - nx * 0.22f) < 0.12f && Mathf.Abs(nx) < 0.72f),
                        // 3: ♭ 플랫
                        3 => (Mathf.Abs(nx + 0.28f) < 0.12f && ny > -0.68f && ny < 0.78f)
                            || (ny < 0.18f && Mathf.Abs(Vector2.Distance(new Vector2(nx, ny), new Vector2(-0.08f, -0.22f)) - 0.36f) < 0.15f && nx > -0.28f),
                        // 4: 𝄽 쉼표
                        4 => (Mathf.Abs(ny - 0.45f + nx * 1.1f) < 0.14f && Mathf.Abs(nx) < 0.45f)
                            || (Mathf.Abs(ny + 0.05f - nx * 1.1f) < 0.14f && Mathf.Abs(nx) < 0.45f)
                            || (Mathf.Abs(ny + 0.50f + nx * 0.8f) < 0.14f && Mathf.Abs(nx) < 0.38f),
                        // 5: ⌒ 페르마타
                        _ => (ny > -0.15f && Mathf.Abs(Vector2.Distance(new Vector2(nx, ny), new Vector2(0f, -0.15f)) - 0.72f) < 0.15f)
                            || (Vector2.Distance(new Vector2(nx, ny), new Vector2(0f, -0.28f)) < 0.18f)
                    };
                    if (!on) { tex.SetPixel(x, y, Color.clear); continue; }
                    bool isRim = Mathf.Abs(nx) > 0.55f || Mathf.Abs(ny) > 0.55f;
                    tex.SetPixel(x, y, isRim ? border : Color.Lerp(body, border, 0.35f));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void GenerateSectionPng(string path, KyoshoSectionType type, Color themeColor)
        {
            int fw = KyoshoNanobotAttackHelper.SectionFrameWidth;
            int fh = KyoshoNanobotAttackHelper.SectionFrameHeight;
            int fc = KyoshoNanobotAttackHelper.SectionFrameCount;
            var tex = new Texture2D(fw * fc, fh, TextureFormat.RGBA32, false);
            for (int f = 0; f < fc; f++)
            {
                for (int y = 0; y < fh; y++)
                {
                    for (int x = 0; x < fw; x++)
                    {
                        Color32 c = KyoshoNanobotAttackHelper.SampleSectionEnsemblePixel(type, f, x, y, fw, fh);
                        tex.SetPixel(f * fw + x, y, c);
                    }
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static BossPatternData CreateOrLoadPattern(
            string fileName, string id, string patternName, string desc,
            float tellDuration, float postDelay, BodyPart targetPart, int partDamage, int baseDamage)
        {
            var p = CreateOrLoadAsset<BossPatternData>($"{SoDir}/{fileName}.asset");
            p.patternID = id;
            p.patternName = patternName;
            p.description = desc;
            p.tellDuration = tellDuration;
            p.postDelay = postDelay;
            p.hasDpsCheck = false;
            p.targetPlayerPart = targetPart;
            p.partDurabilityDamage = partDamage;
            p.baseDamage = baseDamage;
            EditorUtility.SetDirty(p);
            return p;
        }

        private static T CreateOrLoadAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, path);
            return instance;
        }

        private static string GetSymbolFileSlug(int idx)
        {
            return idx switch
            {
                0 => "TrebleClef",
                1 => "BassClef",
                2 => "Sharp",
                3 => "Flat",
                4 => "Rest",
                _ => "Fermata"
            };
        }

        private static void GenerateAllSfxWavsAndBind(KyoshoSfxData sfxData)
        {
            if (sfxData == null) return;
            if (!Directory.Exists(SfxDir)) Directory.CreateDirectory(SfxDir);
            if (!Directory.Exists(BgmDir)) Directory.CreateDirectory(BgmDir);

            var bgm = AssetDatabase.LoadAssetAtPath<AudioClip>(BgmPath);
            if (bgm != null && sfxData.bgmMain == null)
            {
                sfxData.bgmMain = bgm;
            }

            string phase2WavPath = $"{BgmDir}/bgm_kyosho_phase2.wav";
            float[] p2Samples = KyoshoSfx.SynthesizeBgmLoopSamples(2, out int p2Sr);
            if (p2Samples != null && p2Samples.Length > 0)
            {
                WritePcm16Wav(phase2WavPath, p2Samples, p2Sr);
                AssetDatabase.ImportAsset(phase2WavPath, ImportAssetOptions.ForceUpdate);
                var p2Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(phase2WavPath);
                if (p2Clip != null && (sfxData.bgmPhase2 == null || sfxData.bgmPhase2 == sfxData.bgmMain))
                {
                    sfxData.bgmPhase2 = p2Clip;
                }
            }

            string phase3WavPath = $"{BgmDir}/bgm_kyosho_phase3.wav";
            float[] p3Samples = KyoshoSfx.SynthesizeBgmLoopSamples(3, out int p3Sr);
            if (p3Samples != null && p3Samples.Length > 0)
            {
                WritePcm16Wav(phase3WavPath, p3Samples, p3Sr);
                AssetDatabase.ImportAsset(phase3WavPath, ImportAssetOptions.ForceUpdate);
                var p3Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(phase3WavPath);
                if (p3Clip != null && (sfxData.bgmPhase3 == null || sfxData.bgmPhase3 == sfxData.bgmMain || sfxData.bgmPhase3 == sfxData.bgmPhase2))
                {
                    sfxData.bgmPhase3 = p3Clip;
                }
            }

            var mappings = new (KyoshoSfxCue cue, string key, string file)[]
            {
                (sfxData.beatDownbeat, "beat_down", "sfx_beat_down.wav"),
                (sfxData.beatSub, "beat_sub", "sfx_beat_sub.wav"),
                (sfxData.conductWhoosh, "conduct_whoosh", "sfx_conduct_whoosh.wav"),
                (sfxData.conductTipFlash, "conduct_flash", "sfx_conduct_flash.wav"),
                (sfxData.percussionShockwave, "percussion_shock", "sfx_percussion_shock.wav"),
                (sfxData.staccatoCallNote, "staccato_call", "sfx_staccato_call.wav"),
                (sfxData.staccatoFire, "staccato_fire", "sfx_staccato_fire.wav"),
                (sfxData.stringsRapidFire, "strings_fire", "sfx_strings_fire.wav"),
                (sfxData.bloomSeedToss, "bloom_toss", "sfx_bloom_toss.wav"),
                (sfxData.bloomStemGrow, "bloom_grow", "sfx_bloom_grow.wav"),
                (sfxData.bloomStemCut, "bloom_cut", "sfx_bloom_cut.wav"),
                (sfxData.bloomOpenNote, "bloom_note", "sfx_bloom_note.wav"),
                (sfxData.bloomOpenTrumpet, "bloom_trumpet", "sfx_bloom_trumpet.wav"),
                (sfxData.bloomOpenCymbal, "bloom_cymbal", "sfx_bloom_cymbal.wav"),
                (sfxData.fermataSpawn, "fermata_spawn", "sfx_fermata_spawn.wav"),
                (sfxData.fermataHoldChord, "fermata_hold", "sfx_fermata_hold.wav"),
                (sfxData.fermataSlamDrop, "fermata_drop", "sfx_fermata_drop.wav"),
                (sfxData.brassBeam, "brass_beam", "sfx_brass_beam.wav"),
                (sfxData.variationCorrect, "variation_ok", "sfx_variation_ok.wav"),
                (sfxData.variationFailDissonance, "variation_fail", "sfx_variation_fail.wav"),
                (sfxData.finaleGather, "finale_gather", "sfx_finale_gather.wav"),
                (sfxData.finaleFullBlast, "finale_blast", "sfx_finale_blast.wav"),
                (sfxData.sectionBroken, "section_broken", "sfx_section_broken.wav"),
                (sfxData.encoreApplause, "encore_applause", "sfx_encore_applause.wav"),
                (sfxData.encoreAwaken, "encore_awaken", "sfx_encore_awaken.wav"),
            };

            foreach (var (cue, key, file) in mappings)
            {
                BindOrGenerateCueWav(cue, key, $"{SfxDir}/{file}");
            }

            if (sfxData.variationMelodyNotes == null || sfxData.variationMelodyNotes.Length != 6)
            {
                sfxData.variationMelodyNotes = new KyoshoSfxCue[6];
            }
            string[] solfegeSlugs = { "do", "re", "mi", "fa", "sol", "la" };
            for (int i = 0; i < 6; i++)
            {
                sfxData.variationMelodyNotes[i] ??= new KyoshoSfxCue();
                BindOrGenerateCueWav(sfxData.variationMelodyNotes[i], $"solfege_{i}", $"{SfxDir}/sfx_solfege_{i}_{solfegeSlugs[i]}.wav");
            }
        }

        private static void BindOrGenerateCueWav(KyoshoSfxCue cue, string key, string wavPath)
        {
            if (cue == null) return;
            float[] samples = KyoshoSfx.SynthesizeCueSamples(key, out int sr);
            if (samples != null && samples.Length > 0)
            {
                WritePcm16Wav(wavPath, samples, sr);
                AssetDatabase.ImportAsset(wavPath, ImportAssetOptions.ForceUpdate);
            }
            if (!cue.HasValidClip)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(wavPath);
                if (clip != null) cue.clips = new[] { clip };
            }
        }

        private static void WritePcm16Wav(string path, float[] samples, int sampleRate)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);
            int dataLen = samples.Length * 2;
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + dataLen);
            bw.Write(new[] { 'W', 'A', 'V', 'E' });
            bw.Write(new[] { 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write(sampleRate);
            bw.Write(sampleRate * 2);
            bw.Write((short)2);
            bw.Write((short)16);
            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(dataLen);
            for (int i = 0; i < samples.Length; i++)
            {
                short pcm = (short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1f, 1f) * 32767f);
                bw.Write(pcm);
            }
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(SoDir)) Directory.CreateDirectory(SoDir);
            if (!Directory.Exists(ShapesDir)) Directory.CreateDirectory(ShapesDir);
            if (!Directory.Exists(SpriteDir)) Directory.CreateDirectory(SpriteDir);
            if (!Directory.Exists(MaestroSpriteDir)) Directory.CreateDirectory(MaestroSpriteDir);
            if (!Directory.Exists(SfxDir)) Directory.CreateDirectory(SfxDir);
            if (!Directory.Exists(BgmDir)) Directory.CreateDirectory(BgmDir);
        }

        private static RSNanoKnightMoves BakeMaestroSpriteSheetsAndMoves()
        {
            if (!Directory.Exists(MaestroSpriteDir)) Directory.CreateDirectory(MaestroSpriteDir);

            var moves = CreateOrLoadAsset<RSNanoKnightMoves>(MaestroMovesPath);
            moves.cellSize = new Vector2Int(KyoshoNanobotAttackHelper.MaestroCellWidth, KyoshoNanobotAttackHelper.MaestroCellHeight);
            moves.root = new Vector2Int(KyoshoNanobotAttackHelper.MaestroRootX, KyoshoNanobotAttackHelper.MaestroRootY);
            moves.alphaCut = 0.5f;
            moves.pixelScale = 3;
            moves.darkSilhouette = false;
            moves.bladeGlow = 1.4f;
            moves.fxGlow = 2.6f;
            moves.bodySnap = 42000f;
            moves.snapSpeed = 420f;
            moves.idle = KnightMove.Guard1;

            int cellW = KyoshoNanobotAttackHelper.MaestroCellWidth;
            int cellH = KyoshoNanobotAttackHelper.MaestroCellHeight;
            foreach (KnightMove m in RSNanoKnightMoves.All)
            {
                int fc = KyoshoNanobotAttackHelper.GetMaestroFrameCount(m);
                string pngPath = $"{MaestroSpriteDir}/Maestro_{m}.png";
                var tex = new Texture2D(cellW * fc, cellH, TextureFormat.RGBA32, false);
                for (int f = 0; f < fc; f++)
                {
                    for (int y = 0; y < cellH; y++)
                    {
                        for (int x = 0; x < cellW; x++)
                        {
                            KyoshoNanobotAttackHelper.SampleMaestroFramePixel(m, f, x, y, out byte alpha, out Color32 col);
                            col.a = alpha;
                            tex.SetPixel(f * cellW + x, y, col);
                        }
                    }
                }
                tex.Apply();
                File.WriteAllBytes(pngPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(pngPath) is TextureImporter importer)
                {
                    importer.isReadable = true;
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.SaveAndReimport();
                }

                var sheetTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                var clip = moves.Get(m);
                clip.sheet = sheetTex;
                clip.fps = (m == KnightMove.Dash || m == KnightMove.Light1 || m == KnightMove.Light2) ? 18f : 14f;
            }

            RSNanoKnightBaker.Bake(moves);
            EditorUtility.SetDirty(moves);
            return moves;
        }
    }
}
#endif
