using UnityEngine;
/// <summary>
/// 매월 1일 경기장 NPC들의 재화, 성향 갱신 코드
/// </summary>
public class ArenaRegen : MonoBehaviour
{
    [Header("격투장 지역 설정")] // 추후 지역 추가됨에 따라서 개선 필요
    public int regionBasePrice = 500; // 현재 지역의 기준가

    [Header("NPC 오브젝트")]
    public ArenaNpcs[] arenaNpcs = new ArenaNpcs[3];

    private void OnEnable()
    {
        // TimeSystemManager의 매월 1일 정산 이벤트 구독
        TimeSystemManager.OnMonthNpcFightmoneyRestore += RegenerateNPCs;
    }

    private void OnDisable()
    {
        // 메모리 누수 방지를 위한 구독 해제
        TimeSystemManager.OnMonthNpcFightmoneyRestore -= RegenerateNPCs;
    }

    private void Start()
    {
        RegenerateNPCs();
    }

    private void RegenerateNPCs()
    {
        string[] npcNames = { "철갑의 브루투스", "날쌘돌이 잭", "냉혹한 소피아" };

        for (int i = 0; i < arenaNpcs.Length; i++)
        {
            if (arenaNpcs[i] == null) continue;

            // 랜덤 성향 (0: 공격적, 1: 평범, 2: 신중)
            NpcTendency randomTendency = (NpcTendency)Random.Range(0, 3);

            // 기준가의 4배까지 무작위 재산 부여
            int randomWealth = Random.Range(regionBasePrice, regionBasePrice * 4);

            arenaNpcs[i].InitializeNpc(npcNames[i], randomWealth, randomTendency, regionBasePrice);

            Debug.Log($"[격투장 리젠] {npcNames[i]} / 성향: {randomTendency} / 소지금: {randomWealth}G");
        }
    }
}
