using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Dialogue.Unity
{
    /// <summary>speaker + portrait → Sprite</summary>
    [CreateAssetMenu(menuName = "RealSteel/Dialogue/Portrait Library", fileName = "PortraitLibrary")]
    public sealed class PortraitLibrary : ScriptableObject
    {
        [Serializable]
        public struct Item { public string speaker; public string portrait; public Sprite sprite; }
        public List<Item> items = new List<Item>();

        private Dictionary<(string, string), Sprite> _map;

        public Sprite Find(string speaker, string portrait)
        {
            if (_map == null)
            {
                _map = new Dictionary<(string, string), Sprite>();
                foreach (var i in items) _map[(i.speaker, i.portrait ?? "")] = i.sprite;
            }
            if (speaker == null) return null;
            if (_map.TryGetValue((speaker, portrait ?? ""), out var s)) return s;
            return _map.TryGetValue((speaker, "neutral"), out s) ? s : null;
        }
    }
}
