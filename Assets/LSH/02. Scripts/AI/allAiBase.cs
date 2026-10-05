using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract class AllAiBase : MonoBehaviour
{
    public abstract string NPCName { get; }
    public abstract int Gold { get; }
    public abstract bool IsReady { get; set; }
    public abstract int ThinkDelay { get;} //ai마다 생각하는 시간이 다르게 설정
    public abstract int RaiseGold { get; set; } //레이즈 금액
    public abstract void ReadyForAction();
    public abstract bool RaiseThink(int actionPrise, int actionRealPrise);

    [Header("Npc 생각 나오는 곳")]
    public Image thinkImage;
    public TextMeshProUGUI npcThinkChat;

    [Header("생각 말풍선 연출 설정")]
    [SerializeField] protected CanvasGroup thinkCanvasGroup; // 말풍선 전체 투명도 제어용
    protected Sequence thinkSequence; // DOTween 트윈 중첩 방지용
    protected Vector3 originalLocalPos; // 원래 말풍선 위치 저장

    [System.Serializable]
    public struct ChatList
    {
        [TextArea(2, 5)]
        public string thinkChat;
    }

    [Header("AI 고민 대사 모음")]
    [SerializeField]
    protected List<ChatList> thinkChatList = new List<ChatList>(); // 모든 AI가 인스펙터에서 각자 설정 가능

    public virtual void Start()
    {
        RaiseGold = 100; // 초기 레이즈 금액 설정
        // CanvasGroup이 안 붙어 있으면 자동으로 가져오거나 추가
        if (thinkImage != null)
        {
            if (!thinkImage.TryGetComponent<CanvasGroup>(out thinkCanvasGroup))
            {
                thinkCanvasGroup = thinkImage.gameObject.AddComponent<CanvasGroup>();
            }
            originalLocalPos = thinkImage.transform.localPosition;

            // 초기 상태 비활성화 및 초기화
            ResetThinkBubble();
        }
    }

    public virtual string GetRandomThinkChat()
    {
        if (thinkChatList != null && thinkChatList.Count > 0)
        {
            int ranIndex = Random.Range(0, thinkChatList.Count);
            return thinkChatList[ranIndex].thinkChat;
        }
        return string.Empty; // 대사가 없을 때 빈 문자열 반환
    }

    //==================================생각 말풍선 연출===================================
    public virtual void ShowThinkChat()
    {
        if (IsReady) return;
        ShowThinkChatWithText(GetRandomThinkChat());
    }

    public virtual void ShowThinkChatWithText(string message)
    {
        if (thinkImage == null || npcThinkChat == null) return;

        // 이전 애니메이션 진행 중이면 중단
        thinkSequence?.Kill();

        // 텍스트 적용 및 초기 상태 설정
        npcThinkChat.text = message;
        thinkImage.gameObject.SetActive(true);
        thinkImage.transform.localPosition = originalLocalPos;
        thinkImage.transform.localScale = Vector3.zero; // 0 크기에서 시작
        thinkCanvasGroup.alpha = 1f;

        // DOTween 연출 시퀀스 생성
        thinkSequence = DOTween.Sequence();

        thinkSequence
            // 1. 0.3초 동안 통튀듯이 말풍선 튀어나오기 (Ease.OutBack)
            .Append(thinkImage.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack))
            // 2. 2.5초 동안 대기하면서 말풍선이 둥실둥실 살짝 올라감
            .Append(thinkImage.transform.DOLocalMoveY(originalLocalPos.y + 15f, 2.5f).SetEase(Ease.OutQuad))
            // 3. 서서히 사라지면서(Fade Out) 조금 더 위로 떠오름
            .Join(thinkCanvasGroup.DOFade(0f, 0.5f))
            .OnComplete(() =>
            {
                ResetThinkBubble();
            });
    }

    protected void ResetThinkBubble()
    {
        if (thinkImage != null)
        {
            thinkImage.transform.localPosition = originalLocalPos;
            thinkImage.transform.localScale = Vector3.zero;
            if (thinkCanvasGroup != null) thinkCanvasGroup.alpha = 0f;
            thinkImage.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        // 오브젝트 파괴 시 DOTween 메모리 정리
        thinkSequence?.Kill();
    }
}
