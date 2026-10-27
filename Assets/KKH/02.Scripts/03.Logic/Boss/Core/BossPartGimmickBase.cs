using UnityEngine;

namespace RealSteel.Boss.Core
{
    /// <summary>
    /// [보스 약점 부위 파괴 템플릿 클래스]
    /// 부위 내구도 차감 및 파괴 시 스킬 봉인 또는 스펙 약화 디버프를 동기화 함
    /// </summary>
    public abstract class BossPartGimmickBase : BossGimmickBase
    {
        [SerializeField] protected string gimmickID;
        [SerializeField] protected string gimmickName;
        [SerializeField] protected int maxDurability = 100;
        [SerializeField] protected string targetSkillIDToSeal = ""; //공백은 스킬 봉인 대신 스펙 약화

        public override string GimmickID => gimmickID;
        public override string GimmickName => gimmickName;

        public int CurrentDurability { get; protected set;}
        public int MaxDurability => maxDurability;
        public bool IsBroken => CurrentDurability <= 0;
        public int BreakCount { get; protected set; } = 0;

        protected override void OnAttached()
        {
            CurrentDurability = maxDurability;
            SyncToSnapshot();
        }
        public virtual bool TakePartDamage(int amount, GimmickTag tag = GimmickTag.None)
        {
            if (IsBroken)
            {
                return false;
            }
            CurrentDurability = Mathf.Max(0, CurrentDurability - amount);
            OnPartDamaged(amount);
            SyncToSnapshot();

            if (CurrentDurability <= 0)
            {
                BreakCount++;
                ExcutePartBreak();
                return true;
            }
            return false;
        }
        private void ExcutePartBreak()
        {
            // 스킬 봉인 대상이 지정된 경우에만 패턴 영구 봉인
            if (!string.IsNullOrEmpty(targetSkillIDToSeal) && OwnerBoss?.Snapshot != null)
            {
                OwnerBoss.Snapshot.SealSkill(targetSkillIDToSeal);
            }
            OnPartBroken();
            SyncToSnapshot();
        }
        private void SyncToSnapshot()
        {
            if (OwnerBoss?.Snapshot != null && OwnerBoss.Snapshot.gimmickStates.TryGetValue(GimmickID, out var state))
            {
                state.currentDurability = CurrentDurability;
                state.triggerCount = BreakCount;
            }
        }
        protected virtual void OnPartDamaged(int amount) { }
        protected abstract void OnPartBroken();
    }
}
