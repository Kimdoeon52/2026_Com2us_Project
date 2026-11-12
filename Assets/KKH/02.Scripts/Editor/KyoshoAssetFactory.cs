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
        private const string PrefabPath = "Assets/KKH/04.Prefab/BOSS/Third_Boss/Boss3_Kyosho.prefab";
        private const string TestScenePath = "Assets/KKH/01.Scene/Battle_Boss_Test 3.unity";

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

            // 1. 6종 음악 문양 & 3종 악기 섹션 픽셀아트 PNG 생성 및 RSNanoSpriteShape 베이킹
            var symbolShapes = new RSNanoSpriteShape[6];
            for (int i = 0; i < 6; i++)
            {
                string pngPath = $"{SpriteDir}/Symbol_{i}_{GetSymbolFileSlug(i)}.png";
                GenerateSymbolPng(pngPath, i);
                AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                symbolShapes[i] = BakeShapeAsset($"{ShapesDir}/Shape_Symbol_{i}_{GetSymbolFileSlug(i)}.asset", tex);
            }

            var stringsShape = BakeSectionPngAndShape(KyoshoSectionType.Strings, "Strings", KyoshoConstants.StringsMintColor);
            var percShape = BakeSectionPngAndShape(KyoshoSectionType.Percussion, "Percussion", KyoshoConstants.PercussionAmberColor);
            var brassShape = BakeSectionPngAndShape(KyoshoSectionType.Brass, "Brass", KyoshoConstants.BrassMagentaColor);

            // 2. RhythmTable & SfxData SO 생성
            var rhythmTable = CreateOrLoadAsset<KyoshoRhythmTableSO>($"{SoDir}/KyoshoRhythmTable.asset");
            rhythmTable.EnsureDefaults();
            for (int i = 0; i < 6; i++)
            {
                rhythmTable.variationSymbols[i].spriteShape = symbolShapes[i];
            }
            rhythmTable.stringsSectionShape = stringsShape;
            rhythmTable.percussionSectionShape = percShape;
            rhythmTable.brassSectionShape = brassShape;
            EditorUtility.SetDirty(rhythmTable);

            var sfxData = CreateOrLoadAsset<KyoshoSfxData>($"{SoDir}/KyoshoSfxData.asset");
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

        private static void WireOpenTestScene(UnityEngine.SceneManagement.Scene scene)
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

            var cam = UnityEngine.Object.FindFirstObjectByType<CombatCamera>();
            if (cam != null)
            {
                var camSo = new SerializedObject(cam);
                camSo.FindProperty("playerB").objectReferenceValue = bossGo.transform;
                camSo.FindProperty("useArenaBounds").boolValue = true;
                camSo.FindProperty("arenaMinX").floatValue = -7.75f;
                camSo.FindProperty("arenaMaxX").floatValue = 7.75f;
                camSo.FindProperty("farZ").floatValue = -23f;
                camSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var bm = UnityEngine.Object.FindFirstObjectByType<BattleManager>();
            if (bm != null)
            {
                var bmSo = new SerializedObject(bm);
                bmSo.FindProperty("enemyRobotObject").objectReferenceValue = bossGo;
                bmSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var hub = UnityEngine.Object.FindFirstObjectByType<CombatDataHub>();
            if (hub != null)
            {
                hub.BossSnapshot.bossID = KyoshoConstants.BossID;
                hub.BossSnapshot.bossName = "쿄쇼";
                EditorUtility.SetDirty(hub);
            }

            // 테스트 씬용 KyoshoGimmickTester 자동 부착 (비침투적 디버그 헬퍼)
            if (UnityEngine.Object.FindFirstObjectByType<RealSteel.Boss.Test.KyoshoGimmickTester>() == null)
            {
                var testerGo = new GameObject("KyoshoGimmickTester");
                testerGo.AddComponent<RealSteel.Boss.Test.KyoshoGimmickTester>();
            }

            EditorSceneManager.SaveScene(scene);
        }

        private static RSNanoSpriteShape BakeSectionPngAndShape(KyoshoSectionType type, string slug, Color color)
        {
            string pngPath = $"{SpriteDir}/Section_{slug}.png";
            GenerateSectionPng(pngPath, type, color);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            return BakeShapeAsset($"{ShapesDir}/Shape_Section_{slug}.asset", tex);
        }

        private static RSNanoSpriteShape BakeShapeAsset(string assetPath, Texture2D tex)
        {
            var shape = CreateOrLoadAsset<RSNanoSpriteShape>(assetPath);
            shape.sheet = tex;
            shape.columns = 1;
            shape.rows = 1;
            shape.pick = NanoSpritePick.Opaque;
            shape.alphaCut = 0.35f;
            shape.pivot = NanoSpritePivot.Center;
            RSNanoSpriteBaker.Bake(shape);
            EditorUtility.SetDirty(shape);
            return shape;
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
            const int S = 36;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float nx = (x - 17.5f) / 15f;
                    float ny = (y - 17.5f) / 15f;
                    bool on = type switch
                    {
                        KyoshoSectionType.Strings =>
                            (Mathf.Abs(nx) < 0.10f && ny > -0.85f && ny < 0.88f)
                            || (nx * nx * 2.1f + (ny + 0.25f) * (ny + 0.25f) * 1.4f < 0.34f)
                            || (nx * nx * 3.0f + (ny - 0.22f) * (ny - 0.22f) * 2.0f < 0.24f)
                            || (Mathf.Abs(ny - 0.15f - nx * 0.6f) < 0.06f && Mathf.Abs(nx) < 0.85f),
                        KyoshoSectionType.Percussion =>
                            (ny < 0.20f && ny > -0.68f && Mathf.Abs(nx) < 0.75f * (0.58f + 0.42f * (ny + 0.68f)))
                            || (Mathf.Abs(ny - 0.24f) < 0.10f && Mathf.Abs(nx) < 0.78f)
                            || (Mathf.Abs(ny - 0.55f + Mathf.Abs(nx) * 0.45f) < 0.08f && Mathf.Abs(nx) > 0.12f && Mathf.Abs(nx) < 0.65f),
                        _ =>
                            (Mathf.Abs(Vector2.Distance(new Vector2(nx, ny), Vector2.zero) - 0.52f) < 0.16f)
                            || (nx > 0.18f && Mathf.Abs(ny + 0.35f) < 0.26f * (nx + 0.2f) && nx < 0.88f)
                            || (Mathf.Abs(ny) < 0.09f && nx < 0.2f && nx > -0.75f)
                    };
                    tex.SetPixel(x, y, on ? Color.Lerp(themeColor, Color.white, (ny + 1f) * 0.2f) : Color.clear);
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

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(SoDir)) Directory.CreateDirectory(SoDir);
            if (!Directory.Exists(ShapesDir)) Directory.CreateDirectory(ShapesDir);
            if (!Directory.Exists(SpriteDir)) Directory.CreateDirectory(SpriteDir);
        }
    }
}
#endif
