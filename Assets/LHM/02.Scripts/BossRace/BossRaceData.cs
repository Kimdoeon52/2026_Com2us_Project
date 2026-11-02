using System;
using UnityEngine;

namespace RealSteel.BossRace
{
    public enum BossRaceCardState
    {
        Locked,
        InProgress,
        Cleared,
    }

    [Serializable]
    public class BossRaceEntry
    {
        public string bossId = "boss.wheel";
        public string regionLabel = "지역 1";
        public string bossName = "호이루";
        public string typeLabel = "바퀴형";
        public string tags = "바퀴형 · 튀어오르기 / 돌진 / 내려찍기";
        [TextArea(2, 4)] public string description = "";
        public Sprite icon;
        public string targetParts = "";
    }

    [Serializable]
    public class DifficultyEntry
    {
        public string id = "normal";
        public string label = "보통";
        public string subtitle = "기준 난이도";
        [TextArea(2, 4)] public string description = "";
        [Range(1, 5)] public int risk = 3;
        public string scoreLabel = "[점수]";
    }

    public interface IRobotPartStatus
    {
        bool TryGetDurability(BodyPart part, out float ratio);
    }

    public interface IBossRaceProgress
    {
        bool IsRegionCleared(string bossId);
        int BestDifficulty(string bossId);
        int LosesOf(string bossId);
        int AttemptsOf(string bossId);
        int SeasonDaysLeft { get; }
        string RankText { get; }
        string ScoreText { get; }
    }

    public static class BossRaceDifficulty
    {
        public const int HintLoseCount = 3;

        public static readonly string[] Labels = { "아주 쉬움", "쉬움", "보통", "어려움", "헬" };
        public static readonly string[] Ids = { "veryEasy", "easy", "normal", "hard", "hell" };

        public static int IndexOf(string id)
        {
            return Array.IndexOf(Ids, id);
        }

        public static readonly Color[] Colors =
        {
            new Color32(0x7F, 0xD3, 0xA0, 0xFF),
            new Color32(0x9C, 0xC3, 0xFF, 0xFF),
            new Color32(0xF2, 0xA9, 0x00, 0xFF),
            new Color32(0xE8, 0x74, 0x3B, 0xFF),
            new Color32(0xD6, 0x45, 0x45, 0xFF),
        };

        public static string Label(int index)
        {
            return index >= 0 && index < Labels.Length ? Labels[index] : "없음";
        }
    }
}
