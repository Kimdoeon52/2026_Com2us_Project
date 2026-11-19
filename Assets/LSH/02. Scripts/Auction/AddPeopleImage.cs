using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class AddPeopleImage : MonoBehaviour
{
    [Header("구경꾼 배치 위치 (맵 프리팹 안의 오브젝트 위치만 사용)")]
    [SerializeField] private Transform[] spawnPointsLineOne;
    [SerializeField] private Transform[] spawnPointsLineTwo;

    [Header("구경꾼 이미지 설정")]
    [SerializeField] private Sprite[] peopleSprite;
    [SerializeField] private Vector3 peopleScale = Vector3.one;
    [SerializeField] private Vector3 spawnOffset = Vector3.zero; // 위치 미세 조정용

    [Header("무대 바라보기")]
    [Tooltip("구경꾼이 바라볼 무대 중앙. 비우면 Auction의 StagePoint를 사용")]
    [SerializeField] private Transform stagePoint;

    [Header("정렬 설정 (앞줄이 뒷줄을 가리도록)")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int lineOneSortingOrder = 11;
    [SerializeField] private int lineTwoSortingOrder = 10;

    [Header("웅성웅성 연출 설정")]
    [SerializeField] private float lessMoveTime = 0.3f;
    [SerializeField] private float maxMoveTime = 0.6f;
    [SerializeField] private float headMoveHeight = 0.2f; // 끄덕

    private readonly List<SpriteRenderer> peopleImages = new List<SpriteRenderer>(); // 생성된 구경꾼들

    private void Start()
    {
        SpawnLine(spawnPointsLineOne, lineOneSortingOrder, "LineOne");
        SpawnLine(spawnPointsLineTwo, lineTwoSortingOrder, "LineTwo");
    }

    private void SpawnLine(Transform[] points, int sortingOrder, string lineName)
    {
        if (points == null || peopleSprite == null || peopleSprite.Length == 0) return;

        Transform stage = stagePoint != null ? stagePoint : (Auction.Instance != null ? Auction.Instance.StagePoint : null);

        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) continue;

            // 맵 오브젝트는 건드리지 않고, 이 오브젝트 아래에 새 구경꾼을 생성해서 위치만 맞춘다
            var go = new GameObject($"People_{lineName}_{i}");
            go.transform.SetParent(transform, false);
            go.transform.position = points[i].position + spawnOffset;
            go.transform.rotation = Auction.FaceStage(go.transform.position, stage); // 무대 중앙을 바라봄
            go.transform.localScale = peopleScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = peopleSprite[Random.Range(0, peopleSprite.Length)]; // 랜덤 구경꾼 스프라이트
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;

            peopleImages.Add(sr);

            // 각각의 구경꾼에게 웅성거리는 애니메이션 적용
            StartTalking(sr);
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

        // 2. DOTween을 이용한 위아래 끄덕임 연출 (잠시 대기 후 시작)
        DOVirtual.DelayedCall(ranMoveTime, () =>
        {
            if (people == null) return;

            people.transform.DOLocalMoveY(yPos + headMoveHeight, selfMoveTime)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
        }).SetLink(gameObject);
    }

    private void OnDisable()
    {
        // 씬을 벗어나거나 오브젝트가 꺼질 때 도트윈 메모리 누수 방지를 위해 킬
        foreach (var image in peopleImages)
        {
            if (image != null) image.transform.DOKill();
        }
    }
}
