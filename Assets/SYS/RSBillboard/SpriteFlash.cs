// RE:AL STEEL - 피격 플래시 구동부
//
// BillboardPixelLit 셰이더의 _FlashAmount 를 짧게 0 → 1 → 0 으로 흔든다.
// 경직이 없는 전투 설계라 "맞았다"는 신호를 연출이 혼자 져야 한다.
// 그중 제일 값싸고 확실한 게 흰색 한 프레임이다.
//
// 머티리얼을 복제하지 않고 MaterialPropertyBlock 으로 넣으므로
// 같은 머티리얼을 쓰는 캐릭터끼리 서로 영향을 주지 않는다.
//
// 쓰는 법:  GetComponent<SpriteFlash>().Flash();

using UnityEngine;

namespace RealSteel.Presentation
{
    [DisallowMultipleComponent]
    public class SpriteFlash : MonoBehaviour
    {
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        [Tooltip("플래시가 켜졌다 꺼지는 데 걸리는 시간(초). 0.06~0.10 이 적당하다.")]
        [SerializeField] float duration = 0.08f;

        [Tooltip("가장 밝을 때의 세기. 1 이면 완전히 단색으로 덮인다.")]
        [Range(0f, 1f)]
        [SerializeField] float peak = 0.9f;

        [SerializeField] Color color = Color.white;

        /// <summary>올라갈 때는 즉시, 내려올 때는 천천히. 타격 순간이 더 또렷해진다.</summary>
        [Tooltip("켜지는 구간이 전체 시간에서 차지하는 비율")]
        [Range(0.02f, 0.5f)]
        [SerializeField] float attack = 0.12f;

        Renderer[] targets;
        MaterialPropertyBlock block;
        float t = -1f;

        void Awake()
        {
            targets = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
        }

        /// <summary>인스펙터 값으로 한 번 번쩍인다.</summary>
        public void Flash()
        {
            Flash(duration, peak, color);
        }

        /// <summary>세기를 그때그때 다르게 주고 싶을 때. 약공격은 약하게, 필살기는 세게.</summary>
        public void Flash(float seconds, float strength, Color tint)
        {
            duration = Mathf.Max(0.01f, seconds);
            peak = Mathf.Clamp01(strength);
            color = tint;
            t = 0f;
            Apply(0f);
        }

        void Update()
        {
            if (t < 0f) return;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);

            // 빠르게 켜지고 완만하게 꺼진다
            float a = k < attack
                ? k / attack
                : 1f - (k - attack) / (1f - attack);

            Apply(Mathf.Clamp01(a) * peak);

            if (k >= 1f)
            {
                t = -1f;
                Apply(0f);
            }
        }

        void Apply(float amount)
        {
            if (targets == null) return;

            for (int i = 0; i < targets.Length; i++)
            {
                var r = targets[i];
                if (r == null) continue;

                r.GetPropertyBlock(block);
                block.SetFloat(FlashAmountId, amount);
                block.SetColor(FlashColorId, color);
                r.SetPropertyBlock(block);
            }
        }

        void OnDisable()
        {
            t = -1f;
            Apply(0f);
        }
    }
}
