using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public static class GlyphPrewarm
    {
        private static readonly HashSet<TMP_FontAsset> kept = new HashSet<TMP_FontAsset>();
        private static readonly Dictionary<TMP_FontAsset, HashSet<char>> tried = new Dictionary<TMP_FontAsset, HashSet<char>>();

        public static void Warm(IEnumerable<TMP_Text> texts, IEnumerable<string> sources, string label)
        {
            if (texts == null || sources == null) return;

            string chars = Unique(sources);
            if (chars.Length == 0) return;

            float started = Time.realtimeSinceStartup;
            int added = 0;
            var chains = texts.Where(t => t != null && t.font != null).Select(t => t.font).Distinct().ToList();
            foreach (TMP_FontAsset primary in chains) added += WarmChain(primary, chars);

            float ms = (Time.realtimeSinceStartup - started) * 1000f;
            if (added > 0) Debug.Log($"[GlyphPrewarm] {label}: 글자 {added}개 미리 준비 (글꼴 {chains.Count}종, {ms:0}ms)");
        }

        private static int WarmChain(TMP_FontAsset primary, string chars)
        {
            int added = 0;
            string remaining = chars;
            foreach (TMP_FontAsset font in Chain(primary))
            {
                if (string.IsNullOrEmpty(remaining)) break;

                if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                {
                    if (!tried.TryGetValue(font, out HashSet<char> done)) tried[font] = done = new HashSet<char>();
                    string request = new string(remaining.Where(c => !done.Contains(c) && !font.HasCharacter(c)).ToArray());

                    kept.Add(font);
                    if (request.Length > 0)
                    {
                        font.TryAddCharacters(request, out string missing);
                        foreach (char c in request) done.Add(c);
                        added += request.Length - (missing?.Length ?? 0);
                    }
                }

                remaining = new string(remaining.Where(c => !font.HasCharacter(c)).ToArray());
            }
            return added;
        }

        private static IEnumerable<TMP_FontAsset> Chain(TMP_FontAsset primary)
        {
            var seen = new HashSet<TMP_FontAsset>();
            var order = new List<TMP_FontAsset> { primary };
            if (primary.fallbackFontAssetTable != null) order.AddRange(primary.fallbackFontAssetTable);
            if (TMP_Settings.fallbackFontAssets != null) order.AddRange(TMP_Settings.fallbackFontAssets);

            foreach (TMP_FontAsset font in order)
            {
                if (font != null && seen.Add(font)) yield return font;
            }
        }

        private static string Unique(IEnumerable<string> sources)
        {
            var set = new HashSet<char>();
            foreach (string s in sources)
            {
                if (string.IsNullOrEmpty(s)) continue;
                foreach (char c in s)
                {
                    if (!char.IsWhiteSpace(c) && !char.IsControl(c)) set.Add(c);
                }
            }

            var builder = new StringBuilder(set.Count);
            foreach (char c in set) builder.Append(c);
            return builder.ToString();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            kept.Clear();
            tried.Clear();
        }
    }
}
