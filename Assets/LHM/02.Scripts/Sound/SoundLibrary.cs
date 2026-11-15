using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Sound
{
    // 이름(id)으로 부르는 소리 목록. SoundManager.Sfx("ui_click") / SoundManager.Bgm("main_theme")
    // 같은 id에 클립을 여러 개 넣으면 재생할 때마다 무작위로 하나 고름 (발소리 · 타격음 등)
    [CreateAssetMenu(menuName = "RealSteel/Sound Library", fileName = "SoundLibrary")]
    public class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string id;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("재생할 때마다 이 범위에서 음높이를 무작위로 (같은 소리 반복이 덜 거슬리게). 1 · 1이면 그대로")]
            [Range(0.5f, 2f)] public float pitchMin = 1f;
            [Range(0.5f, 2f)] public float pitchMax = 1f;

            public AudioClip PickClip()
            {
                if (clips == null || clips.Length == 0) return null;
                return clips.Length == 1 ? clips[0] : clips[UnityEngine.Random.Range(0, clips.Length)];
            }

            public float PickPitch()
            {
                return Mathf.Approximately(pitchMin, pitchMax) ? pitchMin : UnityEngine.Random.Range(Mathf.Min(pitchMin, pitchMax), Mathf.Max(pitchMin, pitchMax));
            }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<string, Entry> lookup;

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryGet(string id, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(id)) return false;

            if (lookup == null)
            {
                lookup = new Dictionary<string, Entry>(StringComparer.Ordinal);
                foreach (Entry e in entries)
                {
                    if (e != null && !string.IsNullOrEmpty(e.id)) lookup[e.id] = e;
                }
            }
            return lookup.TryGetValue(id, out entry);
        }

        private void OnValidate()
        {
            lookup = null;
        }
    }
}
