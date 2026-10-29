using System;

namespace RealSteel.Dialogue
{
    [Serializable]
    public class EffectData
    {
        public string type;
        public float value;
        public string id;
        public bool wait;

        public override string ToString()
        {
            switch (type)
            {
                case DialogueEffectTypes.Sound:
                case DialogueEffectTypes.Bgm:
                    return $"{type}({id})";
                default:
                    return value > 0f ? $"{type}({value})" : type;
            }
        }
    }

    public static class DialogueEffectTypes
    {
        public const string Zoom = "zoom";
        public const string Shake = "shake";
        public const string FadeOut = "fadeOut";
        public const string FadeIn = "fadeIn";
        public const string Sound = "sound";
        public const string Bgm = "bgm";

        public static readonly string[] All = { Zoom, Shake, FadeOut, FadeIn, Sound, Bgm };
    }
}
