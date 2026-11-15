// RE:AL STEEL - 캐시용 내용 해시 (FNV-1a 64비트)
//
// 조립품 · 지형 캐시가 "저장된 결과가 지금 데이터와 같은가" 를 가릴 때 쓴다.
// 실수는 양자화해서 넣는다 (에디터 · 빌드 사이의 아주 작은 부동소수 차이로 캐시가 안 맞는 일을 줄이려고).
// 해시가 달라지면 원래대로 다시 계산할 뿐이라 틀려도 결과가 깨지지는 않는다 (느려질 뿐).
using System.Text.RegularExpressions;
using UnityEngine;

namespace RealSteel.Common
{
    public struct RSHash
    {
        ulong h;

        public static RSHash New() { return new RSHash { h = 14695981039346656037UL }; }

        public void Add(int v)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++) { h ^= (byte)(v >> (i * 8)); h *= 1099511628211UL; }
            }
        }

        public void Add(bool v) { Add(v ? 1 : 0); }

        /// <summary>실수 (q 단위로 반올림해서 넣는다)</summary>
        public void Add(float v, float q = 1e-4f)
        {
            if (float.IsNaN(v)) { Add(0x7fc00000); return; }
            long r = (long)System.Math.Round((double)v / q);
            Add((int)r); Add((int)(r >> 32));
        }

        public void Add(Vector2 v) { Add(v.x); Add(v.y); }
        public void Add(Vector3 v) { Add(v.x); Add(v.y); Add(v.z); }
        public void Add(Vector4 v) { Add(v.x); Add(v.y); Add(v.z); Add(v.w); }
        public void Add(Quaternion v) { Add(v.x, 1e-6f); Add(v.y, 1e-6f); Add(v.z, 1e-6f); Add(v.w, 1e-6f); }
        public void Add(Matrix4x4 m) { for (int i = 0; i < 16; i++) Add(m[i]); }

        public void Add(string s)
        {
            if (s == null) { Add(-1); return; }
            Add(s.Length);
            for (int i = 0; i < s.Length; i++) Add((int)s[i]);
        }

        public string Hex { get { return h.ToString("x16"); } }

        static readonly Regex Refs = new Regex("\\{\"instanceID\":-?\\d+\\}");

        /// <summary>JsonUtility 결과에서 오브젝트 참조(실행할 때마다 바뀌는 번호)를 지운다</summary>
        public static string StripRefs(string json) { return json == null ? "" : Refs.Replace(json, "0"); }
    }
}
