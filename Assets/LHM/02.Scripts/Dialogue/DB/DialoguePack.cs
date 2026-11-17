using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace RealSteel.Dialogue
{
    [Serializable]
    public class DialogueSourceFile
    {
        public string name;
        public string text;

        public DialogueSourceFile()
        {
        }

        public DialogueSourceFile(string name, string text)
        {
            this.name = name;
            this.text = text;
        }

        public static DialogueSourceFile From(TextAsset asset)
        {
            return asset == null ? null : new DialogueSourceFile(asset.name, asset.text);
        }
    }

    [Serializable]
    public class DialoguePackBody
    {
        public string builtAt;
        public List<DialogueSourceFile> pools = new List<DialogueSourceFile>();
        public DialogueSourceFile strings;
        public DialogueSourceFile variables;
    }

    [Serializable]
    public class DialoguePackEnvelope
    {
        public int version;
        public string checksum;
        public DialoguePackBody body;
    }

    public static class DialoguePack
    {
        public const int Version = 1;
        private const string Salt = "RealSteel.DialoguePack.v1";

        public static byte[] Write(DialoguePackBody body)
        {
            var envelope = new DialoguePackEnvelope
            {
                version = Version,
                checksum = ComputeChecksum(body),
                body = body,
            };

            byte[] json = Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope));
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionMode.Compress))
                {
                    gzip.Write(json, 0, json.Length);
                }
                return output.ToArray();
            }
        }

        public static bool TryRead(byte[] bytes, out DialoguePackBody body, out string error)
        {
            body = null;
            error = null;

            if (bytes == null || bytes.Length == 0)
            {
                error = "패킹 파일이 비어 있음";
                return false;
            }

            string json;
            try
            {
                using (var input = new MemoryStream(bytes))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var reader = new StreamReader(gzip, Encoding.UTF8))
                {
                    json = reader.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                error = $"압축 해제 실패 ({e.GetType().Name})";
                return false;
            }

            DialoguePackEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<DialoguePackEnvelope>(json);
            }
            catch (ArgumentException e)
            {
                error = $"JSON 오류 ({e.Message})";
                return false;
            }

            if (envelope == null || envelope.body == null)
            {
                error = "내용 없음";
                return false;
            }

            if (envelope.version != Version)
            {
                error = $"버전 {envelope.version} (지원: {Version})";
                return false;
            }

            if (envelope.checksum != ComputeChecksum(envelope.body))
            {
                error = "체크섬 불일치 (파일이 손상되었거나 수정됨)";
                return false;
            }

            if (envelope.body.pools == null || envelope.body.pools.Count == 0 || envelope.body.strings == null || envelope.body.variables == null)
            {
                error = "대사 풀·문자열·변수 중 빠진 항목 있음";
                return false;
            }

            body = envelope.body;
            return true;
        }

        private static string ComputeChecksum(DialoguePackBody body)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body) + Salt));
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
