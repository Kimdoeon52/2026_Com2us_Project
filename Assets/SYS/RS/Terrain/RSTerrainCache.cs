// RE:AL STEEL - RS Terrain : 절차 계산 캐시
//
// 지형을 만들 때 가장 무거운 단계는 ComputeProcedural (정점 2.6만 개 × 요소 수십 개 × 최대 3번 평가 + 노이즈).
// 그 결과(정점 XZ · 절차 높이 · 레이어 · 풀)를 .bytes 파일로 저장해 두고, 요소 · 설정 해시가 같으면 계산을 건너뛴다.
//  · 브러시 데이터(손질)는 캐시에 안 들어간다 — 원래처럼 그 위에 합친다 (Compose). 브러시 · 되돌리기 그대로 동작
//  · 메시 · 콜라이더 · 수면 · 폐자재 · 풀은 캐시 뒤에 원래대로 만든다
//  · 해시가 다르면(요소를 옮김 · 값 바꿈 · 코드 버전 바뀜) 원래대로 계산한다 → 틀려도 결과는 같고 느려질 뿐
//  · 굽기는 에디터가 씬을 저장할 때 (RSCacheBaker). 파일 이름 = 해시
using System;
using System.IO;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Terrain
{
    public partial class RSTerrain
    {
        /// <summary>절차 계산 방식(요소 Apply · Evaluate · 노이즈)을 바꾸면 올린다 → 예전 캐시는 모두 무시된다</summary>
        public const int ProcCacheVersion = 1;
        const int ProcMagic = 0x43545352;   // "RSTC"

        [NonSerialized] string builtProcHash;    // 지금 h0 · w0 · g0 · px · pz 가 어떤 해시의 결과인지 (null = 모름)
        [NonSerialized] string loadedProcHash;

        /// <summary>절차 계산에 들어가는 모든 것의 해시 (지형 설정 + 켜진 요소들의 값 · 위치)</summary>
        public string ProceduralHash()
        {
            PrepareFeatures();
            var h = RSHash.New();
            h.Add(ProcCacheVersion);
            h.Add(RSHash.StripRefs(JsonUtility.ToJson(this)));
            var w2l = transform.worldToLocalMatrix;
            h.Add(feats.Count);
            foreach (var f in feats)
            {
                h.Add(f.GetType().Name);
                h.Add(RSHash.StripRefs(JsonUtility.ToJson(f)));
                h.Add(w2l * f.transform.localToWorldMatrix);
            }
            return h.Hex;
        }

        static string CacheHashOf(TextAsset t)
        {
            if (t == null) return null;
            try
            {
                using (var r = new BinaryReader(new MemoryStream(t.bytes)))
                {
                    if (r.ReadInt32() != ProcMagic || r.ReadInt32() != ProcCacheVersion) return null;
                    return r.ReadString();
                }
            }
            catch { return null; }
        }

        bool TryLoadProcedural()
        {
            loadedProcHash = null;
            if (proceduralCache == null) return false;
            string hash = ProceduralHash();      // feats 도 여기서 준비된다
            try
            {
                using (var r = new BinaryReader(new MemoryStream(proceduralCache.bytes)))
                {
                    if (r.ReadInt32() != ProcMagic || r.ReadInt32() != ProcCacheVersion) return false;
                    if (r.ReadString() != hash) return false;
                    int _cx = r.ReadInt32(), _cz = r.ReadInt32();
                    float _csx = r.ReadSingle(), _csz = r.ReadSingle();
                    int _vx = _cx + 1, _vz = _cz + 1, n = _vx * _vz;
                    var _px = ReadFloats(r, n);
                    var _pz = ReadFloats(r, n);
                    var _h0 = ReadFloats(r, n);
                    var _g0 = ReadFloats(r, n);
                    var w = ReadFloats(r, n * 4);
                    if (_px == null || _pz == null || _h0 == null || _g0 == null || w == null) return false;
                    var _w0 = new Vector4[n];
                    for (int k = 0; k < n; k++) _w0[k] = new Vector4(w[k * 4], w[k * 4 + 1], w[k * 4 + 2], w[k * 4 + 3]);

                    cx = _cx; cz = _cz; vx = _vx; vz = _vz; csx = _csx; csz = _csz;
                    px = _px; pz = _pz; h0 = _h0; g0 = _g0; w0 = _w0;
                    hF = new float[n]; wF = new Color[n]; gF = new float[n]; wetF = new float[n];
                }
            }
            catch { return false; }
            loadedProcHash = hash;
            return true;
        }

        static float[] ReadFloats(BinaryReader r, int n)
        {
            var bytes = r.ReadBytes(n * 4);
            if (bytes.Length != n * 4) return null;
            var a = new float[n];
            Buffer.BlockCopy(bytes, 0, a, 0, bytes.Length);
            return a;
        }

        static void WriteFloats(BinaryWriter w, float[] a)
        {
            var bytes = new byte[a.Length * 4];
            Buffer.BlockCopy(a, 0, bytes, 0, bytes.Length);
            w.Write(bytes);
        }

#if UNITY_EDITOR
        /// <summary>[에디터] 캐시가 지금 요소들과 다르면 새로 굽는다 (folder = Assets/... 폴더). 바꿨으면 true</summary>
        public bool BakeProcedural(string folder)
        {
            if (!isActiveAndEnabled) return false;
            string hash = ProceduralHash();
            if (proceduralCache != null && CacheHashOf(proceduralCache) == hash) return false;
            // 지금 들고 있는 절차 결과가 이 해시의 것인지 확실하지 않으면 새로 계산
            if (h0 == null || builtProcHash != hash)
            {
                ComputeProcedural();
                builtProcHash = hash;
                SyncData();
                Compose(0, vx - 1, 0, vz - 1);
            }
            string path = folder + "/" + hash + ".bytes";
            if (!File.Exists(path))
            {
                int n = vx * vz;
                var w = new float[n * 4];
                for (int k = 0; k < n; k++) { w[k * 4] = w0[k].x; w[k * 4 + 1] = w0[k].y; w[k * 4 + 2] = w0[k].z; w[k * 4 + 3] = w0[k].w; }
                using (var ms = new MemoryStream())
                {
                    using (var bw = new BinaryWriter(ms))
                    {
                        bw.Write(ProcMagic); bw.Write(ProcCacheVersion); bw.Write(hash);
                        bw.Write(cx); bw.Write(cz); bw.Write(csx); bw.Write(csz);
                        WriteFloats(bw, px); WriteFloats(bw, pz); WriteFloats(bw, h0); WriteFloats(bw, g0); WriteFloats(bw, w);
                    }
                    File.WriteAllBytes(path, ms.ToArray());
                }
                UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
            }
            var ta = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (ta == null) return false;
            proceduralCache = ta;
            UnityEditor.EditorUtility.SetDirty(this);
            return true;
        }
#endif
    }
}
