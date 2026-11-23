using System;
using System.IO;
using System.Linq;
using RealSteel.BossRace;
using RealSteel.Dialogue;
using TMPro;
using UnityEngine;

namespace RealSteel.MainMenu
{
    public static class PauseMenuInfo
    {
        private const string Empty = "-";
        private static readonly string[] RaceIds = { "race1", "race2", "race3", "final" };

        public static void Fill(Transform root, string location)
        {
            if (root == null) return;

            Set(root, "Row_League/Value", SeasonBoss());
            Set(root, "Row_Next/Value", Empty);
            Set(root, "Row_Frame/Value", Empty);
            Set(root, "Row_Time/Value", Empty);
            Set(root, "Row_Save/Value", LastSave());
            Set(root, "SaveInfo", SaveInfo(location));
            Set(root, "TipBody", Empty);

            string version = Application.version;
            Set(root, "BuildBox/Fill/Version", ShortVersion(version));
            Set(root, "Firmware", $"Ver.  {version}");
            Set(root, "Footer", $"RE:AL STEEL   /   BUILD {version}");
        }

        private static string SeasonBoss()
        {
            DialogueManager manager = UnityEngine.Object.FindAnyObjectByType<DialogueManager>(FindObjectsInactive.Include);
            string race = manager != null && manager.Variables != null ? manager.Variables.Get("race.id") : null;
            int index = Array.IndexOf(RaceIds, race);
            if (index < 0) return "프리시즌";

            BossRaceSelectUI list = UnityEngine.Object.FindAnyObjectByType<BossRaceSelectUI>(FindObjectsInactive.Include);
            BossRaceEntry entry = list != null && index < list.Entries.Count ? list.Entries[index] : null;
            return entry != null ? $"{entry.RegionText} · {entry.bossName}" : race;
        }

        private static string LastSave()
        {
            string path = Path.Combine(Application.persistentDataPath, GameSaveReset.DialogueSaveFileName);
            if (!File.Exists(path)) return "기록 없음";
            return File.GetLastWriteTime(path).ToString("MM.dd  HH:mm");
        }

        private static string SaveInfo(string location)
        {
            TimeSystemManager clock = TimeSystemManager.Instance != null
                ? TimeSystemManager.Instance
                : UnityEngine.Object.FindAnyObjectByType<TimeSystemManager>(FindObjectsInactive.Include);
            string time = clock != null ? $"DAY {clock.day:D2}  {clock.hour:D2}:00" : "DAY --  --:--";
            return string.IsNullOrEmpty(location) ? time : $"{time}  //  {location}";
        }

        private static string ShortVersion(string version)
        {
            string[] parts = (version ?? "").Split('.');
            return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : version;
        }

        private static void Set(Transform root, string path, string value)
        {
            string[] names = path.Split('/');
            Transform found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => Matches(t, names));
            if (found != null && found.TryGetComponent(out TMP_Text text)) text.text = value;
        }

        private static bool Matches(Transform t, string[] names)
        {
            for (int i = names.Length - 1; i >= 0; i--)
            {
                if (t == null || t.name != names[i]) return false;
                t = t.parent;
            }
            return true;
        }
    }
}
