namespace RealSteel.Boss.Core
{
    /// <summary>
    /// 공격 패턴 실행 결과 상태
    /// </summary>
    public enum PatternExecutionResult
    {
        Completed,      // 정상 완료(시전 후 1.0초 확정 딜 타임 복귀)
        Interrupted,    // 저지 성공, 기믹 피격 등으로 중단됨
        Cancelled       // 보스 사망 또는 씬 전환등으로 취소됨
    }

    /// <summary>
    /// 패턴 실행 중단 사유
    /// </summary>
    public enum InterruptReason
    {
        DpsCheckStun,       // 2초 전조 중 300dps 저지 성공(3초 스턴)
        GimmickGroggy,      // 못판 착지 등 기믹 파훼(4초 그로기)
        ExplosiveStun,      // 폭발 스턴(2초 스턴)
        PhaseTransition,    // 페이즈 전환 컷신
        Manual              // 디버그 강제 중단
    }
}
