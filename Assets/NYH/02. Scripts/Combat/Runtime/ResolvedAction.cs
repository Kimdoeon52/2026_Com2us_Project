using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "이번에 실제로 실행되는 이 행동"의 확정값 (2층 · 런타임 상태). ActionState.Begin()에서 1회 만들어지고
/// 그 행동이 끝날 때까지 안 바뀐다.
///
///     ActionData(불변 SO)  +  FrameAdjustment(보정)  →  ResolvedAction(확정)
///
/// 이게 있어야 하는 이유: 선딜·활성·후딜을 바꾸는 기술을 만들려면 SO를 고치거나(§2 위반),
/// 기술 이름으로 분기하거나(§13 위반), 변형 에셋을 복제하는(박스 좌표가 중복됨) 수밖에 없었다.
/// 확정을 한 곳에서 한 번만 하면 셋 다 안 하고도 보정을 넣을 수 있다.
///
/// 박스 창도 여기서 같이 확정된다. FrameBox가 앵커(구간)만 들고 있고 절대 프레임은 안 들고 있으므로,
/// 프레임이 보정되면 박스가 켜지는 구간도 자동으로 따라온다 — 둘이 따로 놀 수가 없다 (§4와 같은 원칙).
/// </summary>
public class ResolvedAction
{
    /// <summary>확정의 원본이 된 에셋. 데미지·가드가능 여부 등 프레임이 아닌 값은 여기서 그대로 읽는다</summary>
    public ActionData Source { get; private set; }

    public int StartupFrames { get; private set; }
    public int ActiveFrames { get; private set; }
    public int RecoveryFrames { get; private set; }

    public int TotalFrames => StartupFrames + ActiveFrames + RecoveryFrames;

    // 2026-10-09: 예전엔 Begin() 순간 박스 배열을 통째로 복사해 들고 있었다. 그러면 F9로 멈춘 채 인스펙터에서
    // 박스 좌표를 고쳐도 그 행동이 끝날 때까지 화면·판정에 반영이 안 돼서, 스프라이트 보며 박스를 맞추는 작업이 안 됐다.
    // 지금은 확정하는 건 "프레임 3개"뿐이고, 박스(좌표·종류·앵커)는 GetActiveBoxes가 부를 때마다 원본 SO에서 읽는다.
    // SO는 런타임에 아무도 안 고치므로(§2) 플레이 중 값이 바뀌는 경우는 사람이 인스펙터에서 고칠 때뿐이다

    // 호출: ActionState.Begin. 받음: 원본 에셋, 적용할 보정. 반환: 확정된 실행 정보
    public static ResolvedAction Resolve(ActionData action, FrameAdjustment adjustment)
    {
        var resolved = new ResolvedAction { Source = action };

        // 가감을 먼저 하고 배율을 나중에 곱한다 — 순서가 반대면 "선딜 -2"가 배율만큼 증폭되어
        // 같은 장비인데 빠른 기술과 느린 기술에서 효과가 달라진다
        resolved.StartupFrames = ScaleFrames(action.StartupFrames, adjustment.StartupAdd, adjustment.SpeedMultiplier);
        resolved.ActiveFrames = ScaleFrames(action.ActiveFrames, adjustment.ActiveAdd, adjustment.SpeedMultiplier);
        resolved.RecoveryFrames = ScaleFrames(action.RecoveryFrames, adjustment.RecoveryAdd, adjustment.SpeedMultiplier);

        // 원래 판정이 있던 기술이 보정·반올림 때문에 활성 0프레임이 되면 판정이 영영 안 켜진다 —
        // 에러도 안 나고 "왜 이 기술만 안 맞지"로만 보여서 진단이 어렵다. 최소 1프레임은 남긴다.
        // 원본부터 활성이 0인 기술(가드 등)은 기획 의도이므로 건드리지 않는다
        if (action.ActiveFrames > 0 && resolved.ActiveFrames <= 0)
            resolved.ActiveFrames = 1;

        return resolved;
    }

    /// <summary>
    /// 지금 이 순간 활성화된 박스. ActionState.GetActiveBoxes()를 거쳐 BoxDrawer(표시)와
    /// HitDetection(판정)이 둘 다 이 결과 하나만 본다 (§4).
    /// 박스는 매번 원본 SO에서 읽고, 켜지는 창만 이번 실행의 확정 프레임으로 계산한다 — 그래서
    /// 일시정지 중에 인스펙터에서 좌표를 바꾸면 빨간 박스가 바로 따라 움직인다.
    /// </summary>
    public IEnumerable<FrameBox> GetActiveBoxes(int globalFrame)
    {
        foreach (var source in Source.FrameBoxes)
        {
            FrameBox box = source; // 구조체 복사 — 아래에서 창을 채워도 SO 원본은 안 바뀐다(§2)
            GetWindow(box, StartupFrames, ActiveFrames, RecoveryFrames, out box.startFrame, out box.endFrame);
            if (globalFrame >= box.startFrame && globalFrame <= box.endFrame)
                yield return box;
        }
    }

    // 가감 후 배율. 음수 프레임은 의미가 없으므로 0으로 자른다
    private static int ScaleFrames(int baseFrames, int add, float speedMultiplier)
    {
        // 배율이 0 이하로 들어오면(FrameAdjustment.None을 안 쓰고 default를 쓴 경우 등) 행동 시간이
        // 0이 되어 기술이 시작하자마자 끝난다. 그런 값은 보정이 아니라 실수이므로 1배로 되돌린다
        float multiplier = speedMultiplier > 0f ? speedMultiplier : 1f;
        return Mathf.Max(0, Mathf.RoundToInt((baseFrames + add) * multiplier));
    }

    /// <summary>
    /// 박스 하나가 실제로 켜지는 절대 프레임 창. 런타임 판정과 에디터 표시가 **반드시 이 함수 하나만** 쓴다 —
    /// 에디터가 따로 계산하면 인스펙터에 적힌 창과 실제 판정이 달라지는 "거짓말하는 표시기"가 된다 (§4).
    /// </summary>
    // 호출: GetActiveBoxes(런타임) / FrameBoxDrawer·ActionDataEditor(에디터 표시). 반환: endFrame < startFrame이면 "안 켜짐"
    public static void GetWindow(FrameBox box, int startup, int active, int recovery, out int startFrame, out int endFrame)
    {
        // 아직 변환 안 한 박스는 예전처럼 절대 프레임을 그대로 쓴다 — 변환 전후로 동작이 안 바뀌게 하기 위함.
        // 이 폴백이 있어서 필드만 추가한 시점에도 게임이 멀쩡히 돌아간다
        if (box.anchor == FrameAnchor.Legacy)
        {
            startFrame = box.startFrame;
            endFrame = box.endFrame;
            return;
        }

        int total = startup + active + recovery;
        GetPhaseRange(box.anchor, startup, active, recovery, out int phaseStart, out int phaseLength);

        // 활성 0프레임짜리 기술의 히트박스처럼, 앵커한 구간 자체가 없는 경우다.
        // start > end로 만들어두면 어떤 globalFrame과도 안 겹쳐서 자연스럽게 꺼진 상태가 된다
        if (phaseLength <= 0)
        {
            startFrame = 1;
            endFrame = 0;
            return;
        }

        int begin = phaseStart + Mathf.Max(0, box.startOffset);
        // length가 0이면 "구간 끝까지" — 프레임이 늘어나면 박스도 같이 늘어나라는 뜻이라 이게 기본값이다
        int end = box.length > 0 ? begin + box.length - 1 : phaseStart + phaseLength - 1;

        // 창이 앵커한 구간 밖으로 삐져나가도 일부러 자르지 않는다 — 잽 히트박스처럼 선딜 끝에서 시작해
        // 활성까지 걸치는 박스가 실제로 있어서, 구간 경계로 자르면 기존 판정이 바뀌어버린다.
        // 창 전체가 행동 밖으로 밀려나면 begin > endFrame이 되어 자연스럽게 안 켜진다
        startFrame = begin;
        endFrame = Mathf.Min(end, total);
    }

    // 앵커가 가리키는 구간의 시작 프레임(1부터)과 길이
    private static void GetPhaseRange(FrameAnchor anchor, int startup, int active, int recovery, out int start, out int length)
    {
        switch (anchor)
        {
            case FrameAnchor.Startup:
                start = 1;
                length = startup;
                break;
            case FrameAnchor.Active:
                start = startup + 1;
                length = active;
                break;
            case FrameAnchor.Recovery:
                start = startup + active + 1;
                length = recovery;
                break;
            default: // WholeAction
                start = 1;
                length = startup + active + recovery;
                break;
        }
    }

}
