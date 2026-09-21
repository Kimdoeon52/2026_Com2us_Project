using UnityEngine;

public class AiBase : MonoBehaviour
{
    [Header("Npc 골드 데이터")]
    public Gold data;

    //람다식으로 삽입.
    public string NpcName => data != null ? data.npcName : "No Name";
    protected int NpcGold => data != null ? data.gold : 0;


    public bool IsReady { get; set; } = false;

    public virtual void ReadyForAction()
    {
        IsReady = false;
    }

    //==================================판단===================================
    public virtual bool RaiseThink(int actionPrise, int actionRealPrise)
    { //현재는 단순히 골드보유로만
        if (IsReady || data == null) return false;

        int NextPrice = actionPrise + 100;

        if(NextPrice > data.gold)
        {
            IsReady = true;
            return false;
        }
        return true;
    }

    public virtual void GetAuction(int finalPrise) //낙찰
    {
        if (data != null)
        {
            data.gold -= finalPrise;
            Debug.Log($"{NpcName} : {finalPrise} 골드로 낙찰 받음. 남은 골드 : {data.gold}");
        }
    }
}