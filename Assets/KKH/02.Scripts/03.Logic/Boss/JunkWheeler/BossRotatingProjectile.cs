using UnityEngine;

namespace RealSteel.Boss.JunkWheeler
{
    /// <summary>
    /// 정크 휠러 고철 톱날 탄환의 고속 회전 연출 컴포넌트
    /// </summary>
    public class BossRotatingProjectile : MonoBehaviour
    {
        [Tooltip("초당 회전 각도 (음수: 시계 방향, 기본 -1080 deg/s = 초당 3바퀴)")]
        [SerializeField] private float rotateSpeed = -1080f;
        [SerializeField] private Transform visualTransform;

        private void Awake()
        {
            if (visualTransform == null)
            {
                // 자식 중 Visual 오브젝트가 있으면 우선 사용
                var child = transform.Find("Visual");
                visualTransform = child != null ? child : transform;
            }
        }

        private void Update()
        {
            if (visualTransform != null)
            {
                visualTransform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime, Space.Self);
            }
        }
    }
}

