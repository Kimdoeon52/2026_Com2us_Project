// RE:AL STEEL - 풀 눕히기 (RSFoliagePusher)
//
// 캐릭터에 붙인다. 이 캐릭터 둘레의 풀 · 꽃(RE_AL STEEL/Foliage Sprite)이 바깥으로 눕는다.
// 매 프레임 발 위치를 셰이더 전역값(_RSFoliagePush, 최대 4명)으로 넘길 뿐 — 풀 메시는 안 건드린다.
// 간단 캐릭터(RSSimpleCharacter)는 플레이할 때 자동으로 붙인다.
using System.Collections.Generic;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Terrain
{
    [RSSummary("풀 눕히기", "이 캐릭터 둘레의 풀 · 꽃이 바깥으로 눕는다 (RS 풀 · 꽃 심기). 발 위치만 셰이더로 넘기므로 가볍다. 최대 4명.\n· 눕는 정도는 풀 머티리얼의 '캐릭터가 지나가면 눕는 정도'")]
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Character/풀 눕히기")]
    public class RSFoliagePusher : MonoBehaviour
    {
        [Tooltip("풀이 눕는 반경 (m)")]
        public float radius = 0.9f;
        [Tooltip("편집 중(플레이 아닐 때)에도 눕히기 — 씬 뷰에서 확인용")]
        public bool previewInEditMode = false;

        static readonly List<RSFoliagePusher> active = new List<RSFoliagePusher>();
        static readonly Vector4[] buf = new Vector4[4];
        static int lastFrame = -1;
        static readonly int idCount = Shader.PropertyToID("_RSFoliagePushCount");
        static readonly int idPush = Shader.PropertyToID("_RSFoliagePush");

        CharacterController cc;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { active.Clear(); lastFrame = -1; }

        void OnEnable()
        {
            cc = GetComponent<CharacterController>();
            if (!active.Contains(this)) active.Add(this);
        }

        void OnDisable()
        {
            active.Remove(this);
            Upload();
        }

        void LateUpdate()
        {
            if (lastFrame == Time.frameCount && Application.isPlaying) return;
            lastFrame = Time.frameCount;
            Upload();
        }

        /// <summary>발 위치 (월드)</summary>
        public Vector3 Feet
        {
            get
            {
                if (cc == null) cc = GetComponent<CharacterController>();
                if (cc == null) return transform.position;
                Vector3 c = transform.TransformPoint(cc.center);
                return new Vector3(c.x, c.y - cc.height * 0.5f * Mathf.Abs(transform.lossyScale.y), c.z);
            }
        }

        static void Upload()
        {
            int n = 0;
            for (int i = 0; i < active.Count && n < 4; i++)
            {
                var p = active[i];
                if (p == null || !p.isActiveAndEnabled) continue;
                if (!Application.isPlaying && !p.previewInEditMode) continue;
                Vector3 f = p.Feet;
                buf[n++] = new Vector4(f.x, f.y, f.z, Mathf.Max(0.05f, p.radius));
            }
            for (int i = n; i < 4; i++) buf[i] = Vector4.zero;
            Shader.SetGlobalVectorArray(idPush, buf);
            Shader.SetGlobalFloat(idCount, n);
        }
    }
}
