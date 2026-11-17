using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Dialogue
{
    [Serializable]
    public class SpeakerVoice
    {
        public string speakerId;
        public AudioClip clip;
        [Range(0.5f, 2f)] public float pitch = 1f;
        [Range(0f, 0.3f)] public float pitchVariance = 0.05f;
    }

    public class DialogueVoice : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const float BlipLength = 0.045f;
        private const float BlipFrequency = 520f;

        [Header("연결")]
        [SerializeField] private AudioSource audioSource;

        [Header("화자별 음색")]
        [SerializeField] private List<SpeakerVoice> voices = new List<SpeakerVoice>
        {
            new SpeakerVoice { speakerId = "auctioneer", pitch = 0.85f },
            new SpeakerVoice { speakerId = "hero", pitch = 1.2f },
        };
        [SerializeField] private SpeakerVoice defaultVoice = new SpeakerVoice { speakerId = "(기본)", pitch = 1f };

        [Header("재생 규칙")]
        [SerializeField, Range(0.02f, 0.2f)] private float minInterval = 0.05f;
        [SerializeField] private bool silentNarration = true;

        private AudioClip generatedClip;
        private SpeakerVoice current;
        private float lastPlayTime = float.NegativeInfinity;

        private void Awake()
        {
            if (audioSource == null)
            {
                Debug.LogError("[DialogueVoice] Audio Source가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            audioSource.playOnAwake = false;
            audioSource.ignoreListenerPause = true;
        }

        public void SetSpeaker(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId))
            {
                current = silentNarration ? null : defaultVoice;
                return;
            }

            current = defaultVoice;
            foreach (SpeakerVoice voice in voices)
            {
                if (voice.speakerId == speakerId)
                {
                    current = voice;
                    break;
                }
            }
        }

        public void PlayCharacter(char character)
        {
            if (!enabled || current == null) return;
            if (char.IsWhiteSpace(character) || char.IsPunctuation(character) || char.IsSymbol(character)) return;

            float now = Time.unscaledTime;
            if (now - lastPlayTime < minInterval) return;
            lastPlayTime = now;

            audioSource.pitch = current.pitch + UnityEngine.Random.Range(-current.pitchVariance, current.pitchVariance);
            audioSource.PlayOneShot(current.clip != null ? current.clip : GetGeneratedClip());
        }

        private AudioClip GetGeneratedClip()
        {
            if (generatedClip != null) return generatedClip;

            int length = Mathf.RoundToInt(SampleRate * BlipLength);
            float[] samples = new float[length];

            for (int i = 0; i < length; i++)
            {
                float time = (float)i / SampleRate;
                float envelope = 1f - (float)i / length;
                float wave = Mathf.Sin(2f * Mathf.PI * BlipFrequency * time) >= 0f ? 1f : -1f;
                samples[i] = wave * 0.2f * envelope * envelope;
            }

            generatedClip = AudioClip.Create("DialogueBlip", length, 1, SampleRate, false);
            generatedClip.SetData(samples, 0);
            return generatedClip;
        }
    }
}
