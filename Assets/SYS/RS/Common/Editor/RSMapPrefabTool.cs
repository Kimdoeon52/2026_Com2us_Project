// RE:AL STEEL - 맵 프리팹 만들기 (에디터)
//
// 씬 하나를 MAP_<폴더> 오브젝트 하나 아래로 묶어 SYS/Prefabs/<폴더>/MAP_<폴더>.prefab 으로 저장한다.
// 한 프리팹 안에 같이 들어가야 서로 걸린 참조(시간대 → 해 · 보조광, 배경 → 카메라 등)가 프리팹에 남는다.
//
//  · 씬에 남기는 것: 플레이어(CharacterController), 이름에 Manager 가 들어간 오브젝트,
//                    맨 위에 놓인 카메라(씬 기본 카메라), 꺼져 있는 맨 위 라이트, 저장 안 되는 숨은 오브젝트
//  · Lighting 창 설정(스카이박스 · 안개 켜기 · 거리 · 라이트맵)은 씬 설정이라 프리팹에 안 들어간다
//  · 묶기 전에 씬을 한 번 저장한다 — 저장할 때 캐시가 구워져서 캐시 메시까지 프리팹에 들어간다
//  · 되돌리기: MAP_ 오브젝트 우클릭 → Prefab → Unpack Completely 후 자식을 밖으로 빼면 원래대로
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Common.EditorTools
{
    public static class RSMapPrefabTool
    {
        // 씬 이름 → Prefabs 아래 폴더
        static readonly string[,] Maps =
        {
            { "League_Lobby",  "Lobby"   },
            { "Fight_Map",     "투우장"   },
            { "Map_Test",      "고철장"   },
            { "Home_Workshop", "Home"    },
            { "Auction_Hall",  "Auction" },
        };

        const string Menu = "Tools/RE_AL STEEL/맵 프리팹/";

        [MenuItem(Menu + "열린 씬을 맵 프리팹으로", false, 1)]
        static void CurrentScene()
        {
            var s = SceneManager.GetActiveScene();
            string folder = FolderOf(s.name);
            if (folder == null)
            {
                EditorUtility.DisplayDialog("맵 프리팹", "'" + s.name + "' 씬은 목록에 없습니다.\nRSMapPrefabTool.cs 의 Maps 표에 씬 이름과 폴더를 추가하세요.", "확인");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log(Make(s, folder));
        }

        [MenuItem(Menu + "씬 전부 (로비 · 투우장 · 고철장 · 집 · 경매장)", false, 2)]
        static void AllScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string back = SceneManager.GetActiveScene().path;
            var log = new StringBuilder("[맵 프리팹] 결과\n");
            for (int i = 0; i < Maps.GetLength(0); i++)
            {
                string p = RSPaths.Root + "/Scenes/" + Maps[i, 0] + ".unity";
                if (!File.Exists(p)) { log.AppendLine("· " + Maps[i, 0] + " — 씬 파일 없음, 건너뜀"); continue; }
                var sc = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                log.AppendLine(Make(sc, Maps[i, 1]));
            }
            if (!string.IsNullOrEmpty(back) && File.Exists(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
            Debug.Log(log.ToString());
        }

        static string FolderOf(string sceneName)
        {
            for (int i = 0; i < Maps.GetLength(0); i++) if (Maps[i, 0] == sceneName) return Maps[i, 1];
            return null;
        }

        static string Make(Scene s, string folder)
        {
            string rootName = "MAP_" + folder;
            string dir = RSPaths.Prefabs + "/" + folder;
            if (!AssetDatabase.IsValidFolder(RSPaths.Prefabs)) AssetDatabase.CreateFolder(RSPaths.Root, "Prefabs");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(RSPaths.Prefabs, folder);
            string path = dir + "/" + rootName + ".prefab";

            foreach (var r in s.GetRootGameObjects())
                if (r.name == rootName && PrefabUtility.IsAnyPrefabInstanceRoot(r))
                    return "· " + s.name + " — 이미 " + rootName + " 프리팹으로 묶여 있음, 건너뜀";
            if (File.Exists(path))
                return "· " + s.name + " — " + path + " 파일이 이미 있음 (덮어쓰지 않음). 지우거나 이름을 바꾼 뒤 다시 실행";

            // 캐시 굽기 (씬 저장 때 RSCacheBaker 가 굽는다) — 프리팹에 캐시 메시가 같이 들어가게
            EditorSceneManager.MarkSceneDirty(s);
            EditorSceneManager.SaveScene(s);

            var move = new List<GameObject>();
            var stay = new List<string>();
            foreach (var r in s.GetRootGameObjects())
            {
                if ((r.hideFlags & HideFlags.DontSave) != 0) continue;
                string why = StayReason(r);
                if (why != null) { stay.Add(r.name + "(" + why + ")"); continue; }
                move.Add(r);
            }
            if (move.Count == 0) return "· " + s.name + " — 묶을 오브젝트가 없음";

            var map = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(map, s);
            map.transform.SetSiblingIndex(0);
            foreach (var r in move) r.transform.SetParent(map.transform, true);   // 원래 순서 · 월드 위치 유지

            bool ok;
            PrefabUtility.SaveAsPrefabAssetAndConnect(map, path, InteractionMode.AutomatedAction, out ok);
            if (!ok)
            {
                foreach (var r in move) if (r != null) r.transform.SetParent(null, true);
                Object.DestroyImmediate(map);
                return "· " + s.name + " — 프리팹 저장 실패 (콘솔 오류 확인). 씬은 원래대로 되돌림";
            }

            EditorSceneManager.MarkSceneDirty(s);
            EditorSceneManager.SaveScene(s);
            return "· " + s.name + " → " + path + " (" + move.Count + "개 묶음)" +
                   (stay.Count > 0 ? "\n    씬에 남김: " + string.Join(", ", stay) : "");
        }

        static string StayReason(GameObject r)
        {
            if (r.GetComponentInChildren<CharacterController>(true) != null) return "플레이어";
            if (r.name.Contains("Manager")) return "매니저";
            if (r.GetComponent<Camera>() != null) return "씬 기본 카메라";
            if (!r.activeSelf && r.GetComponent<Light>() != null) return "꺼진 기본 라이트";
            return null;
        }
    }
}
