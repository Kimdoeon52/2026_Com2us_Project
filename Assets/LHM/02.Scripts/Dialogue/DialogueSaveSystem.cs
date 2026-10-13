using System;
using System.IO;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class DialogueSaveSystem
    {
        public const int CurrentVersion = 1;

        private readonly string mainPath;
        private readonly string backupPath;
        private readonly string tempPath;
        private bool saveBlocked;

        public string MainPath => mainPath;
        public bool IsBlocked => saveBlocked;

        public DialogueSaveSystem(string path)
        {
            mainPath = path;
            backupPath = path + ".bak";
            tempPath = path + ".tmp";
        }

        public bool TryLoad(out SaveFile data)
        {
            data = null;
            saveBlocked = false;

            if (!File.Exists(mainPath) && !File.Exists(backupPath))
            {
                Debug.Log("[Save] 세이브 파일 없음 — 새로 시작");
                return false;
            }

            if (TryRead(mainPath, out data, out string mainError))
            {
                return Accept(data, "본 파일");
            }

            Debug.LogWarning($"[Save] 본 파일을 읽지 못했습니다 — {mainError}. 백업에서 복구를 시도합니다.");

            if (TryRead(backupPath, out data, out string backupError))
            {
                return Accept(data, "백업 파일");
            }

            Debug.LogError($"[Save] 백업도 읽지 못했습니다 — {backupError}. 새로 시작하되, 파일을 확인하거나 삭제하기 전까지 저장하지 않습니다.");
            saveBlocked = true;
            data = null;
            return false;
        }

        public bool Save(SaveFile data)
        {
            if (saveBlocked)
            {
                Debug.LogError("[Save] 기존 세이브를 안전하게 읽지 못한 상태라 저장하지 않습니다 (기존 파일 보호).");
                return false;
            }

            data.version = CurrentVersion;
            data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            try
            {
                string directory = Path.GetDirectoryName(mainPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));

                if (File.Exists(mainPath))
                {
                    File.Replace(tempPath, mainPath, backupPath);
                }
                else
                {
                    File.Move(tempPath, mainPath);
                }

                Debug.Log($"[Save] 저장 완료 ({data.savedAt})");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 저장 실패 — {e.Message}");
                return false;
            }
        }

        public void DeleteAll()
        {
            DeleteIfExists(mainPath);
            DeleteIfExists(backupPath);
            DeleteIfExists(tempPath);
            saveBlocked = false;
            Debug.Log("[Save] 세이브 파일 삭제");
        }

        private bool Accept(SaveFile data, string source)
        {
            if (data.version > CurrentVersion)
            {
                Debug.LogError($"[Save] {source}의 버전({data.version})이 현재 게임({CurrentVersion})보다 높습니다. 읽지 않고, 덮어쓰지도 않습니다.");
                saveBlocked = true;
                return false;
            }

            if (data.version < CurrentVersion)
            {
                Migrate(data);
            }

            Debug.Log($"[Save] {source} 불러옴 (버전 {data.version}, 저장 시각 {data.savedAt})");
            return true;
        }

        private static void Migrate(SaveFile data)
        {
            Debug.Log($"[Save] 버전 {data.version} → {CurrentVersion} 변환");
            data.version = CurrentVersion;
        }

        private static bool TryRead(string path, out SaveFile data, out string error)
        {
            data = null;
            error = null;

            if (!File.Exists(path))
            {
                error = "파일 없음";
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<SaveFile>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                error = $"JSON 오류 ({e.Message})";
                return false;
            }

            if (data == null || data.version <= 0)
            {
                error = "버전 정보가 없는 파일";
                data = null;
                return false;
            }

            return true;
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
