// RE:AL STEEL - RS Terrain 브러시 데이터
//
// 손으로 깎고 칠한 결과만 따로 담는다. 요소(둔덕·길 등)를 옮기거나 지형을 다시 만들어도
// 이 데이터는 그대로 위에 얹힌다 — 절차 생성 결과 + 손질 = 최종 지형.
//
//   height[k]  : 높이 보정 (절차 높이에 더한다)
//   paint[k]   : 칠한 레이어 (R G B A, 0 = 바탕)
//   grass[k]   : 칠한 레이어의 풀 성분 (다섯 번째 레이어, 0 ~ 1). paint 와 함께 amount 만큼 덮는다
//   amount[k]  : 칠한 정도 (0 = 절차 결과 그대로, 1 = 칠한 레이어로 완전히 덮음)
//
// 지형 크기나 격자가 바뀌면 위치 기준으로 다시 샘플링한다.
using UnityEngine;

namespace RealSteel.Terrain
{
    [CreateAssetMenu(fileName = "RSTerrainData", menuName = "RE_AL STEEL/RS Terrain 브러시 데이터")]
    public class RSTerrainData : ScriptableObject
    {
        public int nx, nz;          // 정점 수
        public Vector2 size;        // 지형 크기
        public float[] height = new float[0];
        public Color[] paint  = new Color[0];
        public float[] amount = new float[0];
        public float[] grass  = new float[0];

        public bool Matches(int vx, int vz, Vector2 sz)
        {
            int n = vx * vz;
            return nx == vx && nz == vz && size == sz &&
                   height != null && height.Length == n &&
                   paint  != null && paint.Length  == n &&
                   amount != null && amount.Length == n &&
                   grass  != null && grass.Length  == n;
        }

        public bool IsEmpty
        {
            get
            {
                if (height == null || amount == null) return true;
                for (int i = 0; i < height.Length; i++) if (height[i] != 0f) return false;
                for (int i = 0; i < amount.Length; i++) if (amount[i] != 0f) return false;
                return true;
            }
        }

        /// <summary>새 격자로 맞춘다. 기존 값은 위치(지형 로컬 XZ) 기준으로 옮겨 담는다.</summary>
        public void Resize(int vx, int vz, Vector2 sz)
        {
            int n = vx * vz;
            var nh = new float[n];
            var np = new Color[n];
            var na = new float[n];
            var ng = new float[n];

            bool hasOld = nx >= 2 && nz >= 2 && size.x > 0f && size.y > 0f &&
                          height != null && height.Length == nx * nz &&
                          paint  != null && paint.Length  == nx * nz &&
                          amount != null && amount.Length == nx * nz;
            bool oldGrass = hasOld && grass != null && grass.Length == nx * nz;   // 풀 레이어 이전 에셋은 없음 → 0

            if (hasOld)
            {
                float ocx = size.x / (nx - 1), ocz = size.y / (nz - 1);
                float ncx = sz.x / Mathf.Max(1, vx - 1), ncz = sz.y / Mathf.Max(1, vz - 1);
                for (int i = 0; i < vx; i++)
                    for (int j = 0; j < vz; j++)
                    {
                        float x = -sz.x * 0.5f + i * ncx, z = -sz.y * 0.5f + j * ncz;
                        float fi = (x + size.x * 0.5f) / ocx, fj = (z + size.y * 0.5f) / ocz;
                        if (fi < 0f || fj < 0f || fi > nx - 1 || fj > nz - 1) continue;
                        int i0 = Mathf.Min(nx - 2, Mathf.FloorToInt(fi)), j0 = Mathf.Min(nz - 2, Mathf.FloorToInt(fj));
                        float u = fi - i0, v = fj - j0;
                        int a = i0 * nz + j0, b = (i0 + 1) * nz + j0, c = i0 * nz + j0 + 1, d = (i0 + 1) * nz + j0 + 1;
                        int k = i * vz + j;
                        nh[k] = Mathf.Lerp(Mathf.Lerp(height[a], height[b], u), Mathf.Lerp(height[c], height[d], u), v);
                        na[k] = Mathf.Lerp(Mathf.Lerp(amount[a], amount[b], u), Mathf.Lerp(amount[c], amount[d], u), v);
                        np[k] = Color.Lerp(Color.Lerp(paint[a], paint[b], u), Color.Lerp(paint[c], paint[d], u), v);
                        if (oldGrass) ng[k] = Mathf.Lerp(Mathf.Lerp(grass[a], grass[b], u), Mathf.Lerp(grass[c], grass[d], u), v);
                    }
            }

            nx = vx; nz = vz; size = sz;
            height = nh; paint = np; amount = na; grass = ng;
        }

        public void ClearHeight() { if (height != null) System.Array.Clear(height, 0, height.Length); }
        public void ClearPaint()  { if (amount != null) System.Array.Clear(amount, 0, amount.Length); }
    }
}
