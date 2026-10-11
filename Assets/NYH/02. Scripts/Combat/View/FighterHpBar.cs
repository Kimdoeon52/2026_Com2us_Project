using UnityEngine;

/// <summary>
/// 화면 HP 바를 KKH CombatDataHub의 코어 HP와 연동하는 간단한 표시기 (View · UI). 판정과 무관하다.
///
/// ── 붙이는 곳 (2026-10-11) ────────────────────────────────────────────────────────
///   Battle 씬 HpCanvas 아래의 "채워지는 막대" Image 오브젝트에 직접 붙인다:
///     · PlayerHp/HpBG/HPImg   → fighterId = "Player", shrinkTowardLeft 켬
///     · EnemyHp/EHpBG/EHPImg  → fighterId = "Enemy",  shrinkTowardLeft 끔(오른쪽 끝에 붙어서 줄어듦 — 마주 보는 모양)
///   막대는 부모(HpBG 배경)를 꽉 채우도록 앵커가 0~1로 늘려져 있어야 한다(지금 씬이 그렇게 돼 있음).
///
/// ── 방식 ──────────────────────────────────────────────────────────────────────────
///   Image의 fillAmount를 쓰지 않고 RectTransform 앵커를 HP 비율만큼 줄인다.
///   fillAmount는 Image Type이 Filled이고 스프라이트가 있어야만 동작하는데, 지금 막대는 Simple + 스프라이트 없음이라
///   설정을 바꾸지 않아도 되는 앵커 방식을 골랐다.
///
///   HP는 이벤트로 받지 않고 매 프레임 CombatDataHub에 물어본다(GetCurrentHp/GetMaxHp) — 데미지가 들어오는 길이
///   근접·총알·화염·나중의 기믹으로 여러 갈래라, 이벤트를 하나라도 빠뜨리면 바가 안 줄어든다. 숫자 두 개 읽는 거라 비용 없음.
///   보이는 막대는 실제 HP를 부드럽게 따라간다(smoothSpeed) — 한 번에 툭 끊기지 않게. 0이면 즉시 반영.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class FighterHpBar : MonoBehaviour
{
    [Tooltip("CombatDataHub 조회 키. 플레이어 \"Player\" / 적 \"Enemy\" (ActionExecutor.FighterId와 같은 값)")]
    [SerializeField] private string fighterId = "Player";

    [Tooltip("켜면 왼쪽 끝을 고정하고 오른쪽부터 줄어든다(플레이어). 끄면 오른쪽 끝을 고정하고 왼쪽부터 줄어든다(적)")]
    [SerializeField] private bool shrinkTowardLeft = true;

    [Tooltip("막대가 실제 HP를 따라가는 속도(초당 비율). 0이면 즉시 반영")]
    [Min(0f)] [SerializeField] private float smoothSpeed = 3f;

    private RectTransform rect;
    private float shownRatio = 1f;

    private void Awake() => rect = GetComponent<RectTransform>();

    // 호출: Unity 매 렌더 프레임. HP 비율을 읽어 앵커를 줄인다
    private void Update()
    {
        var hub = CombatDataHub.Instance;
        // 전투가 아직 등록 전이면 꽉 찬 채로 둔다. 스냅샷 확인이 필요한 이유: 등록 안 된 id면 KKH가 현재 HP 0 / 최대 1000을
        // 돌려줘서, 그대로 쓰면 시작하자마자 바가 텅 빈 것처럼 보인다
        if (hub == null || hub.GetSnapshot(fighterId) == null) return;

        int max = hub.GetMaxHp(fighterId);
        if (max <= 0) return;
        float target = Mathf.Clamp01(hub.GetCurrentHp(fighterId) / (float)max);

        shownRatio = smoothSpeed > 0f
            ? Mathf.MoveTowards(shownRatio, target, smoothSpeed * Time.unscaledDeltaTime) // UI라 일시정지(F2) 중에도 따라간다
            : target;

        // 부모(배경)를 꽉 채우는 상태에서 한쪽 앵커만 비율만큼 당긴다. 오프셋은 0으로 둬야 앵커가 곧 크기가 된다
        if (shrinkTowardLeft)
        {
            rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
            rect.anchorMax = new Vector2(shownRatio, rect.anchorMax.y);
        }
        else
        {
            rect.anchorMin = new Vector2(1f - shownRatio, rect.anchorMin.y);
            rect.anchorMax = new Vector2(1f, rect.anchorMax.y);
        }
        rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
        rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
    }
}
