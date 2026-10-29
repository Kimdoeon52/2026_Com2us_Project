using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace RealSteel.Dialogue
{
    [Serializable]
    public class SaveEnvelope
    {
        public string checksum;
        public SaveFile data;
    }

    public class DialogueSaveSystem
    {
        public const int CurrentVersion = 1;
        private const string ChecksumSalt = "RealSteel.Dialogue.Save";

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

            if (TryRead(mainPath, out SaveFile main, out string mainError))
            {
                return Accept(main, "본 파일", out data);
            }

            Debug.LogWarning($"[Save] 본 파일을 읽지 못했습니다 — {mainError}");

            if (TryRead(backupPath, out SaveFile backup, out string backupError))
            {
                return Accept(backup, "백업 파일", out data);
            }

            Debug.LogError($"[Save] 백업 파일도 읽지 못했습니다 — {backupError}\n파일을 확인하거나 삭제할 때까지 저장하지 않습니다.\n{mainPath}");
            saveBlocked = true;
            return false;
        }

        private bool Accept(SaveFile loaded, string source, out SaveFile data)
        {
            data = null;

            if (loaded.version > CurrentVersion)
            {
                Debug.LogError($"[Save] {source}의 버전({loaded.version})이 현재 게임({CurrentVersion})보다 높습니다. 읽지 않고, 덮어쓰지도 않습니다.");
                saveBlocked = true;
                return false;
            }

            if (loaded.version < CurrentVersion)
            {
                Migrate(loaded);
            }

            data = loaded;
            Debug.Log($"[Save] {source} 불러옴 (버전 {loaded.version}, 저장 시각 {loaded.savedAt})");
            return true;
        }

        private static void Migrate(SaveFile data)
        {
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

            SaveEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception e)
            {
                error = $"JSON 오류 ({e.Message})";
                return false;
            }

            if (envelope == null || envelope.data == null || envelope.data.version <= 0)
            {
                error = "세이브 형식이 아닌 파일 (checksum / data 없음)";
                return false;
            }

            if (string.IsNullOrEmpty(envelope.checksum))
            {
                error = "체크섬 없음";
                return false;
            }

            string expected = ComputeChecksum(envelope.data);
            if (!string.Equals(expected, envelope.checksum, StringComparison.OrdinalIgnoreCase))
            {
                error = "체크섬 불일치 (파일이 손상되었거나 수정됨)";
                return false;
            }

            data = envelope.data;
            return true;
        }

        public bool Save(SaveFile data)
        {
            if (saveBlocked)
            {
                Debug.LogWarning("[Save] 저장하지 않습니다 (기존 파일 보호)");
                return false;
            }

            data.version = CurrentVersion;
            data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            var envelope = new SaveEnvelope
            {
                checksum = ComputeChecksum(data),
                data = data
            };

            try
            {
                string directory = Path.GetDirectoryName(mainPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                File.WriteAllText(tempPath, JsonUtility.ToJson(envelope, true), new UTF8Encoding(false));

                if (File.Exists(mainPath))
                {
                    File.Replace(tempPath, mainPath, backupPath, true);
                }
                else
                {
                    File.Move(tempPath, mainPath);
                }
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
            TryDelete(mainPath);
            TryDelete(backupPath);
            TryDelete(tempPath);
            saveBlocked = false;
            Debug.Log("[Save] 세이브 파일 삭제");
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 삭제 실패 — {path} ({e.Message})");
            }
        }

        public static string ComputeChecksum(SaveFile data)
        {
            string body = ChecksumSalt + JsonUtility.ToJson(data, false);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(body));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
