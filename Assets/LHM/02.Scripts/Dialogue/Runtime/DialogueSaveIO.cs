using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace RealSteel.Dialogue.Unity
{
    /// <summary>
    /// 대화 세이브 파일 IO. 게임 전체 세이브에 문자열로 끼워 넣는다면 이 클래스 대신
    /// DialogueService.ExportSave/ImportSave만 쓰면 된다.
    ///
    /// 안정성 규칙
    ///  - 원자적 저장: temp에 쓰고 → 기존 파일을 .bak으로 → temp를 본 파일로 교체.
    ///    저장 도중 전원이 꺼져도 "반쯤 쓰인 세이브"가 남지 않는다.
    ///  - 로드 실패(파손) 시 .bak으로 자동 복구 시도.
    ///  - 신버전 세이브(NewerVersion)는 절대 덮어쓰지 않도록 호출 측에 잠금 신호를 준다.
    /// </summary>
    public static class DialogueSaveIO
    {
        public static string DefaultPath(int slot) =>
            Path.Combine(Application.persistentDataPath, $"dialogue_slot{slot}.json");

        public static void WriteAtomic(string path, string json)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string tmp = path + ".tmp";
            string bak = path + ".bak";
            File.WriteAllText(tmp, json, new UTF8Encoding(false));

            if (File.Exists(path))
                File.Replace(tmp, path, bak, ignoreMetadataErrors: true);
            else
                File.Move(tmp, path);
        }

        public static SaveLoadResult ReadWithRecovery(string path)
        {
            var main = TryRead(path);
            if (main.Status == SaveLoadStatus.Ok || main.Status == SaveLoadStatus.Migrated || main.Status == SaveLoadStatus.NewerVersion)
                return main;

            string bak = path + ".bak";
            if (File.Exists(bak))
            {
                var backup = TryRead(bak);
                if (backup.Status == SaveLoadStatus.Ok || backup.Status == SaveLoadStatus.Migrated)
                {
                    backup.Message = "본 세이브가 손상되어 백업에서 복구했습니다. " + main.Message;
                    return backup;
                }
            }
            return main;
        }

        private static SaveLoadResult TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return DialogueSaveSerializer.Deserialize(null);
                return DialogueSaveSerializer.Deserialize(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return new SaveLoadResult { Status = SaveLoadStatus.Corrupt, Data = new DialogueSaveData(), Message = e.Message };
            }
        }
    }
}
