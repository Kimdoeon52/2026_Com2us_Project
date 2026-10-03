using UnityEngine;

public abstract class AllAiBase : MonoBehaviour
{
    public abstract string NPCName { get; }
    public abstract int Gold { get; }
    public abstract bool IsReady { get; set; }
    public abstract int ThinkDelay { get;} //ai마다 생각하는 시간이 다르게 설정
    public abstract int RaiseGold { get; set; } //레이즈 금액
    public abstract void ReadyForAction();
    public abstract bool RaiseThink(int actionPrise, int actionRealPrise);
    
}
