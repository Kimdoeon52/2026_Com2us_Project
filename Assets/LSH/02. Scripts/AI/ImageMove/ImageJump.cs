using DG.Tweening;
using UnityEngine;

public class ImageJump : MonoBehaviour
{
    [Header("설정 값")]
    [SerializeField] private float moveDis = 0.5f; // 위로 움직일 거리
    [SerializeField] private float animeTime = 0.5f; // 한 방향으로 이동하는 시간

    private Tween tweenAnime;

    private void Start()
    {
        StartJump();
    }

    private void StartJump()
    {
        // 현재 위치 기준으로 위로 튀는 애니메이션 설정
        tweenAnime = transform.DOMoveY(transform.position.y + moveDis, animeTime)
            .SetLoops(-1, LoopType.Yoyo) // -1은 무한 반복, Yoyo는 갔다가 제자리로 돌아옴
            .SetEase(Ease.InOutSine);    // 부드러운 가감속 적용
    }

    private void OnDestroy()
    {
        // 오브젝트가 파괴될 때 트윈을 안전하게 해제 (메모리 누수 방지)
        tweenAnime?.Kill();
    }
}
