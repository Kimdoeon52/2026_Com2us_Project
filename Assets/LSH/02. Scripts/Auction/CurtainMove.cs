using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class CurtainMove : MonoBehaviour
{
    [SerializeField] private Transform curtainLeft;
    [SerializeField] private Transform curtainRight;
    private float openTime = 2f; // "촤악" 하고 열리는 속도 감도
    private float leftFirstX;
    private float rightFirstX;
    private float moveDis= 5f;

    private void Awake()
    {
        // 게임 시작 시점에 에디터에서 배치해 둔 원래 위치를 안전하게 저장합니다.
        if (curtainLeft != null) leftFirstX = curtainLeft.localPosition.x;
        if (curtainRight != null) rightFirstX = curtainRight.localPosition.x;
    }
    public void MoveCurtain()
    {
        if (curtainLeft == null || curtainRight == null) return;
        // DOTween 시퀀스 생성 (동시 연출 제어용)
        Sequence curtainSequance = DOTween.Sequence();
        // 1. 왼쪽 커튼을 왼쪽 바깥쪽으로 이동 (- 방향)
        curtainSequance.Append(curtainLeft.DOLocalMoveX(leftFirstX - moveDis, openTime).SetEase(Ease.InOutCubic));

        // 2. 오른쪽 커튼을 동시에 오른쪽 바깥쪽으로 이동 (+ 방향) -> Join을 쓰면 동시에 실행됨
        curtainSequance.Join(curtainRight.DOLocalMoveX(rightFirstX + moveDis, openTime).SetEase(Ease.InOutCubic));

        // 3. 연출이 완전히 끝난 뒤 처리할 작업 (선택사항: 오브젝트 비활성화 등)
        curtainSequance.OnComplete(() =>
        {
            // 예: 왼쪽커튼.gameObject.SetActive(false);
            //     오른쪽커튼.gameObject.SetActive(false);
        });
    }
    public void CloseCurtain()
    {
        // 닫기 전 오브젝트가 꺼져있을 수 있으므로 활성화 보장
        curtainLeft.gameObject.SetActive(true);
        curtainRight.gameObject.SetActive(true);

        Sequence curtainCloseSequence = DOTween.Sequence();

        // 💡 무조건 0으로 보내는 대신, 처음에 저장해 둔 원래의 X 좌표로 정확히 복귀시킵니다.
        curtainCloseSequence.Append(curtainLeft.DOLocalMoveX(leftFirstX, openTime).SetEase(Ease.InOutCubic));
        curtainCloseSequence.Join(curtainRight.DOLocalMoveX(rightFirstX, openTime).SetEase(Ease.InOutCubic));

        curtainCloseSequence.OnComplete(() =>
        {
            // 닫힌 후 추가로 처리할 연출이 있다면 여기에 작성
        });
    }
}
