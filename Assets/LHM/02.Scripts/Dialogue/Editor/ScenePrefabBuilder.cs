using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RealSteel.Build;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RealSteel.Dialogue.EditorTools
{
    internal static class ScenePrefabBuilder
    {
        private const string Title = "씬용 프리팹 만들기";
        private const string PlaceTitle = "씬에 대화 · NPC 배치";
        internal const string NpcFolder = "Assets/LHM/05.Prefab/NPC";
        internal const string DoorFolder = "Assets/LHM/05.Prefab/Door";
        internal const string BundleFolder = "Assets/LHM/05.Prefab/Scene";
        internal const string DoorBasePath = DoorFolder + "/Door_Base.prefab";
        private const string DataFolder = "Assets/LHM/03.Data/Dialogue";
        private const string FontPath = "Assets/LHM/04.Fonts/NanumBarunGothicBold SDF.asset";

        private static readonly string[] NonPoolJsons = { "strings_ko", "variables", "dialogue_triggers" };

        private sealed class SceneInfo
        {
            public string bundle;
            public string source;
            public string main;
            public string map;
            public (string speaker, string slot)[] slots;
        }

        private static readonly SceneInfo[] Scenes =
        {
            new SceneInfo
            {
                bundle = "LHM_Lobby", source = "Assets/Scenes/Copys_1116/League_Lobby.unity", main = "Assets/Scenes/MainScene/Main_League_Lobby.unity",
                map = "Assets/SYS/Prefabs/Lobby/MAP_Lobby.prefab", slots = new[] { ("receptionist", "SLOT_NPC_접수원") },
            },
            new SceneInfo
            {
                bundle = "LHM_Auction", source = "Assets/Scenes/Copys_1116/Auction.unity", main = "Assets/Scenes/MainScene/Main_Auction_Hall.unity",
                map = "Assets/SYS/Prefabs/Auction/MAP_Auction.prefab", slots = new[] { ("auctioneer", "SLOT_NPC_등록직원") },
            },
            new SceneInfo
            {
                bundle = "LHM_Home", source = "Assets/Scenes/Copys_1116/Home_Workshop.unity", main = "Assets/Scenes/MainScene/Main_Home_Workshop.unity",
                map = "Assets/SYS/Prefabs/Home/MAP_Home.prefab", slots = new[] { ("workbench", "SLOT_Workbench_작업대") },
            },
            new SceneInfo
            {
                bundle = "LHM_JunkShop", source = "Assets/Scenes/Copys_1116/Map_Test.unity", main = "Assets/Scenes/MainScene/Main_Junk_Shop.unity",
                map = "Assets/SYS/Prefabs/고철장/MAP_고철장.prefab", slots = new (string, string)[0],
            },
        };

        private sealed class Placement
        {
            public string name;
            public string speaker;
            public Vector3 position;
            public Quaternion rotation;
            public string json;
        }

        private sealed class SceneRecord
        {
            public SceneInfo info;
            public readonly List<Placement> npcs = new List<Placement>();
            public readonly List<Placement> doors = new List<Placement>();
        }

        internal static string NpcPath(string speakerId) => $"{NpcFolder}/NPC_{Pascal(speakerId)}.prefab";
        internal static string BundlePath(string bundle) => $"{BundleFolder}/{bundle}.prefab";

        internal static bool Exists()
        {
            return Scenes.All(s => s.slots.All(n => Load(NpcPath(n.speaker)) != null)) && Load(NpcPath("compressor")) != null && MissingPools().Length == 0;
        }

        internal static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!SetupKit.Dialog(Title,
                    "복사본 씬(Copys_1116)에 놓인 NPC · 기계 오브젝트를 겉모습째 복사해 프리팹을 만듭니다.\n" +
                    $"- 원본(읽기만): {string.Join(", ", Scenes.Select(s => Path.GetFileNameWithoutExtension(s.source)))}\n" +
                    $"- NPC: {NpcFolder} (최신 스프라이트 · 모형 · 클로즈업 점 포함)\n" +
                    $"- 씬 묶음: {BundleFolder} (맵에 자리가 없는 기계 · 문)\n" +
                    "- DialogueSystem.prefab: 빠진 대사 풀 JSON 추가\n\n" +
                    "이미 있는 프리팹은 덮어쓰지 않습니다 (다시 만들려면 지우고 실행). 원본 씬은 저장하지 않고, 작업 후 지금 씬으로 돌아옵니다.",
                    "만들기", "취소"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (!SceneManager.GetSceneAt(i).isDirty) continue;
                SetupKit.Dialog(Title, "저장되지 않은 씬이 있어 중단했습니다. 씬을 저장한 뒤 다시 실행하세요.", "확인");
                return;
            }

            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            var log = new StringBuilder($"[{Title}]\n");
            bool ok = false;
            try
            {
                ok = Run(log);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                log.AppendLine($"예외: {exception.Message}");
            }
            finally
            {
                Restore(setup);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
            SetupKit.Dialog(Title, (ok ? "완료.\n\n" : "실패했습니다. 콘솔 로그를 확인하세요.\n\n") + log, "확인");
        }

        private static bool Run(StringBuilder log)
        {
            FixSystemPools(log);
            EnsureFolder(NpcFolder);
            EnsureFolder(DoorFolder);
            EnsureFolder(BundleFolder);

            var records = new List<SceneRecord>();
            foreach (SceneInfo info in Scenes)
            {
                if (!File.Exists(info.source))
                {
                    log.AppendLine($"- 원본 씬 없음: {info.source}");
                    return false;
                }

                SceneRecord record = Read(info, log);
                if (record == null) return false;
                records.Add(record);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject doorBase = EnsureDoorBase(log);
            if (doorBase == null) return false;

            foreach (SceneRecord record in records)
            {
                if (!EnsureBundle(record, doorBase, log)) return false;
            }

            return true;
        }

        private static SceneRecord Read(SceneInfo info, StringBuilder log)
        {
            Scene scene = EditorSceneManager.OpenScene(info.source, OpenSceneMode.Single);
            var record = new SceneRecord { info = info };
            Component[] all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).ToArray();

            foreach (NpcInteractable npc in all.OfType<NpcInteractable>())
            {
                if (SaveNpcPrefab(npc, log) == null) return null;

                bool inSlot = info.slots.Any(s => s.speaker == npc.SpeakerId);
                if (!inSlot)
                {
                    record.npcs.Add(new Placement { speaker = npc.SpeakerId, position = npc.transform.position, rotation = npc.transform.rotation });
                }
            }

            foreach (DoorInteractable door in all.OfType<DoorInteractable>())
            {
                record.doors.Add(new Placement { name = door.name, position = door.transform.position, rotation = door.transform.rotation, json = EditorJsonUtility.ToJson(door) });
            }

            string missing = string.Join(", ", info.slots.Where(s => !all.OfType<NpcInteractable>().Any(n => n.SpeakerId == s.speaker)).Select(s => s.speaker));
            log.AppendLine($"- 읽음 {Path.GetFileNameWithoutExtension(info.source)}: NPC {string.Join(", ", all.OfType<NpcInteractable>().Select(n => n.SpeakerId))} / 문 {string.Join(", ", record.doors.Select(d => d.name))}" +
                           (missing.Length > 0 ? $" / 주의: {missing} 없음" : ""));
            return record;
        }

        private static GameObject SaveNpcPrefab(NpcInteractable npc, StringBuilder log)
        {
            string path = NpcPath(npc.SpeakerId);
            GameObject existing = Load(path);
            if (existing != null)
            {
                log.AppendLine($"  · 있음(유지): {path}");
                return existing;
            }

            Transform source = npc.transform;
            GameObject copy = Object.Instantiate(source.gameObject);
            try
            {
                copy.name = Path.GetFileNameWithoutExtension(path);
                copy.transform.SetParent(null, false);
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.identity;
                copy.transform.localScale = source.lossyScale;

                foreach (RSPropSlot slot in copy.GetComponents<RSPropSlot>()) Object.DestroyImmediate(slot);
                foreach (Transform generated in copy.GetComponentsInChildren<Transform>(true).Where(t => t != copy.transform && (t.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0).ToArray())
                {
                    if (generated != null) Object.DestroyImmediate(generated.gameObject);
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(copy, path);
                log.AppendLine(saved != null ? $"  · 만듦: {path} ← {source.name}" : $"  · 실패: {path}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }

        private static GameObject EnsureDoorBase(StringBuilder log)
        {
            GameObject existing = Load(DoorBasePath);
            if (existing != null) return existing;

            var go = new GameObject("Door_Base");
            var door = go.AddComponent<DoorInteractable>();
            SetReference(door, "font", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath));

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, DoorBasePath);
            Object.DestroyImmediate(go);
            log.AppendLine(saved != null ? $"- 만듦: {DoorBasePath}" : $"- 실패: {DoorBasePath}");
            return saved;
        }

        private static bool EnsureBundle(SceneRecord record, GameObject doorBase, StringBuilder log)
        {
            string path = BundlePath(record.info.bundle);
            if (record.npcs.Count == 0 && record.doors.Count == 0)
            {
                log.AppendLine($"- {record.info.bundle}: 맵 밖에 둘 것이 없어 묶음 없음");
                return true;
            }

            if (Load(path) != null)
            {
                log.AppendLine($"- 있음(유지): {path}");
                return true;
            }

            var root = new GameObject(record.info.bundle);

            foreach (Placement npc in record.npcs)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load(NpcPath(npc.speaker)));
                instance.transform.SetParent(root.transform, false);
                instance.transform.SetPositionAndRotation(npc.position, npc.rotation);
            }

            foreach (Placement door in record.doors)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(doorBase);
                instance.name = door.name;
                instance.transform.SetParent(root.transform, false);
                instance.transform.SetPositionAndRotation(door.position, door.rotation);

                var component = instance.GetComponent<DoorInteractable>();
                EditorJsonUtility.FromJsonOverwrite(door.json, component);
                SetReference(component, "player", null);
                SetReference(component, "promptTemplate", null);
                SetReference(component, "worldCamera", null);

                using var so = new SerializedObject(component);
                SerializedProperty scenePath = so.FindProperty("scenePath");
                string target = MainSceneFor(scenePath.stringValue);
                if (target != null)
                {
                    scenePath.stringValue = target;
                    so.FindProperty("sceneName").stringValue = Path.GetFileNameWithoutExtension(target);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                log.AppendLine($"  · {record.info.bundle}/{door.name} → {component.ScenePath}");
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            log.AppendLine(saved != null ? $"- 만듦: {path} (기계 · NPC {record.npcs.Count}, 문 {record.doors.Count})" : $"- 실패: {path}");
            return saved != null;
        }

        internal static bool IsPlacedInActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            SceneInfo info = FindInfo(scene, out _);
            if (info == null) return false;

            Component[] all = AllComponents(scene);
            return all.OfType<DialogueSceneBinder>().Any()
                   && info.slots.All(s => all.OfType<NpcInteractable>().Any(n => n.SpeakerId == s.speaker))
                   && (Load(BundlePath(info.bundle)) == null || FindBundle(scene, info) != null);
        }

        internal static void PlaceInActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Scene scene = SceneManager.GetActiveScene();
            SceneInfo info = FindInfo(scene, out GameObject map);
            if (info == null)
            {
                SetupKit.Dialog(PlaceTitle,
                    "이 씬에서 SYS 맵 프리팹을 찾지 못했습니다.\n" +
                    $"다음 중 하나를 씬에 먼저 놓으세요: {string.Join(", ", Scenes.Select(s => Path.GetFileNameWithoutExtension(s.map)))}\n" +
                    "(테스트 씬이면 맵 + SYS TestCharacter를 놓은 뒤 실행)", "확인");
                return;
            }

            GameObject bundlePrefab = Load(BundlePath(info.bundle));
            if (info.slots.Any(s => Load(NpcPath(s.speaker)) == null))
            {
                SetupKit.Dialog(PlaceTitle, "NPC 프리팹이 없습니다. '씬용 프리팹 만들기'를 먼저 실행하세요.", "확인");
                return;
            }

            if (!SetupKit.Dialog(PlaceTitle,
                    $"'{scene.name}' 씬 ({Path.GetFileNameWithoutExtension(info.map)})에 배치합니다.\n" +
                    "- 대화 시스템 (없을 때만)\n" +
                    string.Concat(info.slots.Select(s => $"- {Path.GetFileNameWithoutExtension(NpcPath(s.speaker))} → 맵의 {s.slot} 안 (SYS 임시 모형은 자동으로 꺼짐)\n")) +
                    (bundlePrefab != null ? $"- {info.bundle} (맵과 같은 위치)\n" : "") +
                    "\n씬은 자동 저장하지 않습니다 (Ctrl+Z로 되돌리기).",
                    "배치", "취소"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder($"[{PlaceTitle}] {scene.path}\n");
            Component[] all = AllComponents(scene);

            if (!all.OfType<DialogueSceneBinder>().Any())
            {
                var system = (GameObject)PrefabUtility.InstantiatePrefab(Load(DialoguePrefabBuilder.SystemPrefabPath), scene);
                Undo.RegisterCreatedObjectUndo(system, PlaceTitle);
                system.transform.SetSiblingIndex(0);
                log.AppendLine($"- 대화 시스템 배치: {system.name}");
            }
            else
            {
                log.AppendLine("- 대화 시스템: 이미 있음");
            }

            foreach ((string speaker, string slotName) in info.slots)
            {
                if (all.OfType<NpcInteractable>().Any(n => n.SpeakerId == speaker))
                {
                    log.AppendLine($"- {speaker}: 이미 있음");
                    continue;
                }

                RSPropSlot slot = map.GetComponentsInChildren<RSPropSlot>(true).FirstOrDefault(s => s.name == slotName);
                if (slot == null)
                {
                    log.AppendLine($"- 주의: 맵에서 {slotName}을 찾지 못해 {speaker}를 놓지 못함");
                    continue;
                }

                var npc = (GameObject)PrefabUtility.InstantiatePrefab(Load(NpcPath(speaker)), slot.transform);
                Undo.RegisterCreatedObjectUndo(npc, PlaceTitle);
                npc.transform.localPosition = Vector3.zero;
                npc.transform.localRotation = Quaternion.identity;
                slot.Refresh();
                log.AppendLine($"- {npc.name} → {slotName}");
            }

            if (bundlePrefab != null && FindBundle(scene, info) == null)
            {
                var bundle = (GameObject)PrefabUtility.InstantiatePrefab(bundlePrefab, scene);
                Undo.RegisterCreatedObjectUndo(bundle, PlaceTitle);
                bundle.transform.SetPositionAndRotation(map.transform.position, map.transform.rotation);
                log.AppendLine($"- {bundle.name} 배치 (맵 위치)");
            }

            Undo.SetCurrentGroupName(PlaceTitle);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
            SetupKit.Dialog(PlaceTitle, log.ToString(), "확인");
        }

        private static SceneInfo FindInfo(Scene scene, out GameObject map)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;

                    string asset = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                    SceneInfo info = Scenes.FirstOrDefault(s => s.map == asset);
                    if (info == null) continue;

                    map = t.gameObject;
                    return info;
                }
            }

            map = null;
            return null;
        }

        private static GameObject FindBundle(Scene scene, SceneInfo info)
        {
            string path = BundlePath(info.bundle);
            return scene.GetRootGameObjects().FirstOrDefault(r => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r) == path);
        }

        private static Component[] AllComponents(Scene scene)
        {
            return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true)).ToArray();
        }

        private static string MainSceneFor(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath)) return null;

            string name = Path.GetFileNameWithoutExtension(sourcePath);
            return Scenes.FirstOrDefault(s => Path.GetFileNameWithoutExtension(s.source) == name)?.main;
        }

        private static TextAsset[] MissingPools()
        {
            GameObject prefab = Load(DialoguePrefabBuilder.SystemPrefabPath);
            DialogueManager manager = prefab != null ? prefab.GetComponentInChildren<DialogueManager>(true) : null;
            if (manager == null) return new TextAsset[0];

            using var so = new SerializedObject(manager);
            SerializedProperty pools = so.FindProperty("poolJsons");
            var listed = new HashSet<Object>(Enumerable.Range(0, pools.arraySize).Select(i => pools.GetArrayElementAtIndex(i).objectReferenceValue));

            return AssetDatabase.FindAssets("t:TextAsset", new[] { DataFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == DataFolder && p.EndsWith(".json") && !NonPoolJsons.Contains(Path.GetFileNameWithoutExtension(p)))
                .Select(AssetDatabase.LoadAssetAtPath<TextAsset>)
                .Where(a => a != null && !listed.Contains(a))
                .ToArray();
        }

        private static void FixSystemPools(StringBuilder log)
        {
            TextAsset[] missing = MissingPools();
            if (missing.Length == 0)
            {
                log.AppendLine("- DialogueSystem.prefab: 대사 풀 빠진 것 없음");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(DialoguePrefabBuilder.SystemPrefabPath);
            try
            {
                DialogueManager manager = contents.GetComponentInChildren<DialogueManager>(true);
                using (var so = new SerializedObject(manager))
                {
                    SerializedProperty pools = so.FindProperty("poolJsons");
                    foreach (TextAsset asset in missing)
                    {
                        pools.arraySize++;
                        pools.GetArrayElementAtIndex(pools.arraySize - 1).objectReferenceValue = asset;
                    }

                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(contents, DialoguePrefabBuilder.SystemPrefabPath);
                log.AppendLine($"- DialogueSystem.prefab: 대사 풀 추가 {string.Join(", ", missing.Select(a => a.name))}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static GameObject Load(string path)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void SetReference(Object target, string field, Object value)
        {
            using var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string Pascal(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Unknown";
            return char.ToUpperInvariant(id[0]) + id.Substring(1);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        private static void Restore(SceneSetup[] setup)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SceneSetup[] saved = setup.Where(s => !string.IsNullOrEmpty(s.path)).ToArray();
            if (saved.Length == 0) return;

            if (!saved.Any(s => s.isActive)) saved[0].isActive = true;
            EditorSceneManager.RestoreSceneManagerSetup(saved);
        }
    }
}
