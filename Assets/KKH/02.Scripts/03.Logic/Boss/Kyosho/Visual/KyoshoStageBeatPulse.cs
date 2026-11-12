using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [경기장 배경 조명 박자 펄스 (KyoshoStageBeatPulse)]
    /// 매 박(OnBeat)마다 약한 펄스, 매 마디 첫 박(OnDownbeat)마다 강한 조명 펄스를 부여해
    /// 음소거 상태에서도 경기장 분위기만으로 박자를 직관적으로 체감할 수 있게 함.
    /// </summary>
    public class KyoshoStageBeatPulse : MonoBehaviour
    {
        [SerializeField] private float subbeatBoost = 0.14f;
        [SerializeField] private float downbeatBoost = 0.34f;
        [SerializeField] private float decaySpeed = 6.5f;

        private struct LightRecord
        {
            public Light Light;
            public float BaseIntensity;
        }

        private readonly List<LightRecord> _lights = new List<LightRecord>();
        private KyoshoBeatClock _clock;
        private float _pulse;

        public void Bind(KyoshoBeatClock clock)
        {
            Unbind();
            _clock = clock;
            CacheSceneLights();
            if (_clock != null)
            {
                _clock.OnBeat += HandleBeat;
            }
        }

        public void Unbind()
        {
            if (_clock != null)
            {
                _clock.OnBeat -= HandleBeat;
                _clock = null;
            }
            RestoreLights();
        }

        private void OnDisable() => RestoreLights();
        private void OnDestroy() => Unbind();

        private void CacheSceneLights()
        {
            _lights.Clear();
            var found = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in found)
            {
                if (l != null && l.enabled)
                {
                    _lights.Add(new LightRecord { Light = l, BaseIntensity = l.intensity });
                }
            }
        }

        private void HandleBeat(int beatInBar, int totalBeat)
        {
            _pulse = beatInBar == 0 ? 1f : 0.42f;
        }

        private void Update()
        {
            if (_lights.Count == 0 || _clock == null || !_clock.IsRunning) return;

            if (_pulse > 0f)
            {
                _pulse = Mathf.MoveTowards(_pulse, 0f, Time.deltaTime * decaySpeed);
            }

            float boost = 1f + (_pulse > 0.5f ? downbeatBoost : subbeatBoost) * _pulse;
            for (int i = 0; i < _lights.Count; i++)
            {
                var rec = _lights[i];
                if (rec.Light != null)
                {
                    rec.Light.intensity = rec.BaseIntensity * boost;
                }
            }
        }

        private void RestoreLights()
        {
            for (int i = 0; i < _lights.Count; i++)
            {
                var rec = _lights[i];
                if (rec.Light != null)
                {
                    rec.Light.intensity = rec.BaseIntensity;
                }
            }
        }
    }
}
