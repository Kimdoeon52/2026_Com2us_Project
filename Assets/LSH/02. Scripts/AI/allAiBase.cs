using UnityEngine;

public abstract class AllAiBase : MonoBehaviour
{
    public abstract string NPCName { get; }
    public abstract int Gold { get; }
    public abstract bool IsReady { get; set; }

    public abstract void ReadyForAction();
    public abstract bool RaiseThink(int actionPrise, int actionRealPrise);
}
