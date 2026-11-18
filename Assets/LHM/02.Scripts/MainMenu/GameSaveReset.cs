using System;
using System.IO;
using RealSteel.Cutscene;
using RealSteel.Dialogue;
using RealSteel.Tutorial;
using UnityEngine;

namespace RealSteel.MainMenu
{
    // 새 게임 = 진행 데이터 초기화. 음량·그래픽 같은 설정은 건드리지 않음
    public static class GameSaveReset
    {
        public const string DialogueSaveFileName = "dialogue_save.json";
        private const string InventorySaveFileName = "inventory.json";
        private const string InventoryResourcePath = "InventoryHost";
        private static readonly string[] CutsceneIds = { "opening" };

        private static string DialogueSavePath => Path.Combine(Application.persistentDataPath, DialogueSaveFileName);

        public static bool HasSave()
        {
            string path = DialogueSavePath;
            return File.Exists(path) || File.Exists(path + ".bak");
        }

        public static void ResetForNewGame()
        {
            new DialogueSaveSystem(DialogueSavePath).DeleteAll();
            ResetSeenFlags();
            ResetInventory();
            Debug.Log("[NewGame] 진행 데이터 초기화 완료 (대화 세이브 · 컷신/튜토리얼 본 기록 · 인벤토리). 골드는 LSH 초기화 함수 연결 전이라 유지됨");
        }

        private static void ResetSeenFlags()
        {
            foreach (string id in CutsceneIds) CutscenePlayer.ResetSeen(id);

            TutorialCatalog catalog = Resources.Load<TutorialCatalog>(TutorialCatalog.ResourcePath);
            if (catalog != null)
            {
                foreach (TutorialGuide guide in catalog.guides)
                {
                    if (guide != null) TutorialPanel.ResetSeen(guide.guideId);
                }
            }
            PlayerPrefs.Save();
        }

        // KDU InventoryHost는 씬을 넘어 살아 있어 파일만 지우면 메모리 값이 다시 저장됨
        // → 인스턴스를 즉시 없애고 파일 삭제 후 Resources 프리팹으로 다시 만들면 KDU Awake가 시작 장비로 새로 세팅
        private static void ResetInventory()
        {
            if (InventoryHost.Instance != null) UnityEngine.Object.DestroyImmediate(InventoryHost.Instance.gameObject);

            string path = Path.Combine(Application.persistentDataPath, InventorySaveFileName);
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NewGame] 인벤토리 세이브 삭제 실패 — {path} ({e.Message})");
            }

            InventoryHost prefab = Resources.Load<InventoryHost>(InventoryResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"[NewGame] Resources/{InventoryResourcePath} 프리팹이 없어 인벤토리를 다시 만들지 않았습니다.");
                return;
            }
            UnityEngine.Object.Instantiate(prefab).name = prefab.name;
        }
    }
}
