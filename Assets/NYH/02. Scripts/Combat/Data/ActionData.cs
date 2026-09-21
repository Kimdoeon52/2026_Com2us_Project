using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// 왜 "행동마다 클래스" 대신 이 SO 하나로 잽/훅/스트레이트/.../보스 기술까지 전부 표현하는가
// ============================================================================
//
// 참고용 반례가 이미 저장소에 있다 (Assets/CSH/02.Scripts/01.Skill/SkillBase.cs 계열).
// 그쪽은 SkillBase → AttackSkillBase → DefenseSkillBase 로 스킬마다 상속 클래스를 만들고,
// 선딜/활성/후딜 프레임을 MonoBehaviour 필드에 직접 박아넣는 방식이다.
// (NYH 폴더가 아니므로 수정하지 않음 — 비교 대상으로만 참조)
//
// 이 방식을 우리 전투 실행 엔진(기본기 7개 + 스킬 5~8 + 보스 전용 다수)에 그대로 적용하면:
//
//   | 항목                     | 상속 방식 (클래스 1개 = 기술 1개)        | SO 방식 (클래스 1개 = 전체 기술) |
//   |--------------------------|------------------------------------------|-----------------------------------|
//   | 클래스 수                | 기본기5+방어2+스킬4 = 11, 보스 8체×3기술 | 1 (ActionData)                    |
//   |                          | 추가 = 총 35개↑                          |                                    |
//   | 선딜/판정/후딜 실행 코드 | 클래스마다 복붙 (또는 상속으로 억지로    | ActionExecutor 하나가 전부 처리   |
//   |                          | 우회 — 근데 C#은 다중상속 불가라 "공격  |                                    |
//   |                          | 이면서 방어"인 기술은 애초에 표현 불가) |                                    |
//   | 밸런스 표 값 변경 시     | 코드 수정 + 재컴파일                     | 인스펙터에서 값만 교체, 코드 0줄  |
//   | 새 기술 추가 시          | 새 클래스 + 새 컴파일                    | 에셋 하나 생성 (Create > Combat)  |
//   | "누구의 왼팔인지" 문제   | 해당 없음 (MonoBehaviour가 캐릭터에      | 문제 없음 — ActionData는 불변     |
//   |                          | 붙어있으므로 원래 안 겪음. 대신 위 문제를 |     데이터일 뿐, 소유는 RuntimeRobot|
//   |                          | 그대로 떠안음)                           |     (2층)이 갖는다                |
//
// 결론: 기술 개수가 늘어나는 속도(보스마다 신규 스킬)와 밸런스 표가 아직 확정되지 않아
// 자주 바뀔 것(설계 결정서 §8, §9 신뢰도 표 참조)을 감안하면 SO 방식이 유리하다.
// 상속은 오직 "무엇이 다른가"가 코드 차원에서 갈릴 때만 쓴다 — 여기선 데이터만 다르므로 해당 없음.
// (CLAUDE.md §2, §8 / 설계 결정서 §4 근거)
//
// 스킬 5~8(최상희 담당 SkillBase 계열)과의 연동은 아직 팀 합의 전이라
// 이 파일에서 직접 참조하지 않는다. 나중에 IUsable 같은 최소 인터페이스로 어댑터만 추가할 예정.
// ============================================================================

/// <summary>
/// 모든 행동(기본기, 방어, 스킬, 보스 전용 기술 포함)을 표현하는 단일 데이터 에셋.
/// 잽이든 백스핀 엘보우든 이 클래스 하나의 인스턴스(에셋)일 뿐이다.
/// 불변 데이터만 담는다 — 런타임 중 값이 깎이거나 바뀌는 상태는 절대 여기 두지 않는다 (CLAUDE.md §2).
///
/// 에셋 파일 이름은 전투 프레임표 [기술] 칸과 1:1로 같게 만든다. (예: 잽.asset, 훅.asset)
/// 수치는 전투 프레임표 v3가 최종 근거 — 여기 기본값은 전부 임시값이며 확정 전까지 TODO로 표시한다.
/// </summary>
// CreateAssetMenu를 붙이면 프로젝트 창 우클릭 메뉴에 "NYH/Combat/Action Data"가 생겨서
// 매번 스크립트로 인스턴스를 만들 필요 없이 기획/애니메이터도 직접 에셋을 만들 수 있게 된다
[CreateAssetMenu(menuName = "NYH/Combat/Action Data", fileName = "NewActionData")]
public class ActionData : ScriptableObject
{
    [Header("식별")]
    [Tooltip("프레임표 [기술] 칸과 동일하게. 로그·디버그 표시에도 이 이름을 그대로 쓴다")]
    [SerializeField] private string actionName;

    // 이 값 하나로 "부위가 파괴돼도 이 기술이 계속 나가는지"가 갈린다 — CanExecuteAction(KKH)이 이걸 읽는다
    [Tooltip("이 행동이 코어 고정인지 파츠 소속인지. 파츠 파괴 시 사용 가능 여부를 가른다 (§5)")]
    [SerializeField] private ActionSource source = ActionSource.Part;

    [Tooltip("source가 Part일 때만 의미 있음. 이 부위가 파괴되면 이 행동은 사용 불가")]
    [SerializeField] private BodyPart requiredPart;

    [Header("프레임 (60fps)")]
    // 선딜/활성/후딜 세 값이 이 클래스에서 가장 중요한 데이터다 — ActionState가 이 세 값만 보고
    // 언제 Phase를 넘길지 판단한다(선딜만큼 세면 Active로, 활성만큼 세면 Recovery로, ...)
    [Tooltip("선딜 프레임 — 순수 대기 구간. 판정 없음")]
    [Min(0)] [SerializeField] private int startupFrames; // [Min(0)]으로 음수 입력 자체를 인스펙터에서 막아둠 — 음수 프레임은 의미가 없으니까

    [Tooltip("활성 프레임 — 판정이 켜지는 구간")]
    [Min(0)] [SerializeField] private int activeFrames;

    [Tooltip("회수(후딜) 프레임 — 이 구간엔 재입력이 무시되어야 한다 (§11-4 필수 검증 항목)")]
    [Min(0)] [SerializeField] private int recoveryFrames;

    // 위닝 전용이 아니라 "무적 여부"라는 범용 플래그로 만든 이유: 나중에 다른 무적기(필살기 등)가
    // 생겨도 이름으로 분기하지 않고 이 플래그 하나로 인식되게 하기 위해서다 (§13 원칙과 동일한 이유)
    [Tooltip("위닝처럼 활성 구간 전체가 무적인 행동에 체크. 부분 무적이 필요해지면 FrameBox 쪽으로 확장")]
    [SerializeField] private bool isInvincibleDuringActive;

    [Header("판정 성질")]
    [Tooltip("false면 가드로 막을 수 없음 (백스핀 엘보우)")]
    [SerializeField] private bool isGuardable = true;

    [Tooltip("맞으면 그 자리에서 콤보가 끊기고 다운 처리 (어퍼컷, 백스핀 엘보우)")]
    [SerializeField] private bool causesKnockdown;

    // 아래 세 값은 기획이 프레임표를 아직 확정 안 해서 전부 임시값(0)이다. 코드에서 하드코딩하지 않고
    // 여기 필드로 빼둔 이유는 §2 원칙(밸런스 수치는 에셋에서) — 값이 확정되면 코드 수정 없이 인스펙터만 바꾸면 됨
    [Header("경직도 / 넉백 — 값 전부 임시 (기획 확정 전, 설계 결정서 §8·§9 신뢰도 0%)")]
    [Tooltip("경직도 게이지 누적량. 잽은 반드시 0 (기획서: 잽은 경직도 안 쌓임)")]
    [Min(0)] [SerializeField] private int staggerValue;

    [Tooltip("TODO: 임시값. 맞은 순간 양쪽 x좌표에 이 값만큼 더한다 (§6, SF2 방식 — 콤보 카운터 없음)")]
    [SerializeField] private float knockbackDistance;

    [Tooltip("TODO: 임시값. 프레임표 확정 전까지 자리만 잡아둠")]
    [Min(0)] [SerializeField] private float damage;

    [Header("연출")]
    [Tooltip("프레임표 [애니메이션 클립] 칸과 1:1로 맞춘다.")]
    [SerializeField] private string animationClipName;

    [Header("판정 박스")]
    // 이 배열 안의 각 FrameBox가 "이 행동 도중 어느 프레임에 어떤 판정 상자가 있는지"를 전부 담는다.
    // 빈 배열(new FrameBox[0])을 기본값으로 둔 이유: 아직 아무 좌표도 안 채워진 상태에서도
    // GetActiveBoxes()가 null 참조 예외 없이 그냥 빈 결과를 돌려주게 하기 위함
    [Tooltip("Hit/Hurt/Push 박스 목록. startFrame~endFrame은 ActionState.GlobalFrame 기준(1부터, endFrame 포함)")]
    [SerializeField] private FrameBox[] frameBoxes = new FrameBox[0];

    // ---- 읽기 전용 접근자 — 런타임(2층)과 시스템(3층)은 이 값을 읽기만 하고 절대 쓰지 않는다 ----
    // 프로퍼티(=>)로만 노출하고 public 필드로 안 만든 이유: 외부에서 실수로라도 값을 대입하지 못하게
    // 막기 위해서다. ActionData는 "불변 데이터"인데 public 필드였다면 누구든 런타임에 값을 바꿔버릴 수 있었을 것
    public string ActionName => actionName;
    public ActionSource Source => source;
    public BodyPart RequiredPart => requiredPart;

    // 시작 프레임
    public int StartupFrames => startupFrames;
    // 활성(공격판정이 생기는)프레임
    public int ActiveFrames => activeFrames;
    // 후딜 프레임(공격을 회수하는 프레임)
    public int RecoveryFrames => recoveryFrames;

    /// <summary>전체 프레임. 시작+활성+회수 그냥 합 — SF 원본 표기와 달리 -1 보정 없음 (§3)</summary>
    // 필드로 따로 저장하지 않고 매번 계산하는 이유: 세 프레임 값 중 하나라도 바뀌면 자동으로
    // 최신값을 반영해야 하는데, 별도 필드로 캐싱해두면 값이 바뀔 때마다 이 캐시도 같이 갱신해야 해서
    // 깜빡하면 실제 값과 어긋나는 버그가 생긴다. 계산이 워낙 가벼워서 그냥 매번 더하는 게 안전하다
    public int TotalFrames => startupFrames + activeFrames + recoveryFrames;

    public bool IsInvincibleDuringActive => isInvincibleDuringActive;
    public bool IsGuardable => isGuardable;
    public bool CausesKnockdown => causesKnockdown;

    public int StaggerValue => staggerValue;
    public float KnockbackDistance => knockbackDistance;
    public float Damage => damage;

    public string AnimationClipName => animationClipName;

    public FrameBox[] FrameBoxes => frameBoxes;

    /// <summary>
    /// 지정한 전체-타임라인 프레임(ActionState.GlobalFrame 기준)에 활성화된 박스만 반환한다.
    /// HitDetection(판정)과 BoxDrawer(표시)가 반드시 같은 이 함수를 통해서 박스를 읽는다 —
    /// 표시기가 별도 데이터를 보면 "거짓말하는 표시기"가 된다 (§4).
    /// </summary>
    public IEnumerable<FrameBox> GetActiveBoxes(int globalFrame)
    {
        // 배열 전체를 순회하면서 "지금 프레임이 이 박스의 활성 구간 안에 있는지"만 확인한다.
        // 박스 개수가 캐릭터당 많아야 십수 개 수준이라 매번 순회해도 성능 문제가 없어서,
        // 따로 프레임별 캐시(Dictionary<int, List<FrameBox>> 같은 것)를 만들지 않고 단순하게 짰다
        foreach (var box in frameBoxes)
        {
            if (globalFrame >= box.startFrame && globalFrame <= box.endFrame)
                yield return box;
        }
    }
}
