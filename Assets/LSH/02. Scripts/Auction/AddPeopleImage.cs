using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class AddPeopleImage : MonoBehaviour
{
    [Header("구경꾼 이미지 설정")]
    [SerializeField] private SpriteRenderer[] peopleImageLineOne;
    [SerializeField] private SpriteRenderer[] peopleImageLineTwo;
    [SerializeField] private Sprite[] peopleSprite;
    private SpriteRenderer[] peopleImages; // 모든 구경꾼 이미지 배열
    [Header("웅성웅성 연출 설정")]
    [SerializeField] private float lessMoveTime = 0.3f;
    [SerializeField] private float maxMoveTime = 0.6f;
    [SerializeField] private float headMoveHeight = 0.2f; // 끄덕

    private void Start()
    {
        peopleImages = new SpriteRenderer[peopleImageLineOne.Length + peopleImageLineTwo.Length];
        int index = 0;
        foreach (var img in peopleImageLineOne) peopleImages[index++] = img;
        foreach (var img in peopleImageLineTwo) peopleImages[index++] = img;
        for (int i = 0; i < peopleImages.Length; i++)
        {
            int ranIndex = Random.Range(0, peopleSprite.Length);
            peopleImages[i].sprite = peopleSprite[ranIndex]; // 랜덤 구경꾼들 스프라이트 지정

            // 💡 각각의 구경꾼에게 웅성거리는 애니메이션 적용
            StartTalking(peopleImages[i]);
        }
    }

    private void StartTalking(SpriteRenderer people)
    {
        if (people == null) return;

        // 1. 군중이 똑같이 움직이지 않도록 각 사람마다 움직이는 속도와 시작 타이밍을 다르게 설정
        float selfMoveTime = Random.Range(lessMoveTime, maxMoveTime);
        float ranMoveTime = Random.Range(0f, 0.4f); // 시작 타이밍 엇갈리게 하기

        // 원래 Y 위치 기준점 저장
        float yPos = people.transform.localPosition.y;

        // 2. DOTween을 이용한 위아래 끄덕임 연출
        // 잠시 대기 후 시작
        DOVirtual.DelayedCall(ranMoveTime, () =>
        {
            if (people == null) return;

            // 현재 위치에서 위로 살짝 올라갔다가(DOAnchorPosY), Yoyo 옵션으로 아래로 내려오기를 무한 반복 (-1)
            people.transform.DOLocalMoveY(yPos + headMoveHeight, selfMoveTime)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
        });
    }

    private void OnDisable()
    {
        // 씬을 벗어나거나 오브젝트가 꺼질 때 도트윈 메모리 누수 방지를 위해 킬
        if (peopleImages != null)
        {
            foreach (var image in peopleImages)
            {
                if (image != null) image.transform.DOKill();
            }
        }
    }
}
