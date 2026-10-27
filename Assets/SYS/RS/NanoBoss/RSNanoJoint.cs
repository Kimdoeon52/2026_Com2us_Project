// RE:AL STEEL - 나노봇 리그 관절 두께
//
// 줄기 부위의 관절(부위 오브젝트 자신 + 첫째 자식 체인)에 붙인다. 관절 사이는 부드럽게 이어진다.
// '두께' 는 부위의 '두께(m)' 에 곱하는 배율이고 애니메이션 창에서 키를 잡을 수 있다 (손가락 끝만 부풀리기 등).
// 씬 뷰에서 줄기 부위를 고르면 관절마다 두께 핸들이 보인다.
using UnityEngine;

namespace RealSteel.NanoBoss
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/나노봇 리그 관절")]
    public class RSNanoJoint : MonoBehaviour
    {
        [Tooltip("부위 두께에 곱하는 배율 (키를 잡는다). 0 이면 뾰족")]
        [Min(0f)] public float thickness = 1f;
    }
}
