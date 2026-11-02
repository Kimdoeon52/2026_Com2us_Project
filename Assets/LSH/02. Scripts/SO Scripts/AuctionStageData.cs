using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AuctionStageData", menuName = "LSH/AuctionStageData")]
public class AuctionStageData : ScriptableObject
{
    [System.Serializable]
    public class StageBossGroup
    {
        public List<BossAiBase> bosses; //해당 스테이지에 참여할 보스들
    }

    [SerializeField] private List<StageBossGroup> stages; //0번이 Stage1

    public int StageCount => stages != null ? stages.Count : 0;

    public List<BossAiBase> GetBosses(int stage) //stage는 1부터 시작. 없는 스테이지면 null
    {
        int index = stage - 1;
        if (stages == null || index < 0 || index >= stages.Count) return null;
        return stages[index].bosses;
    }
}
