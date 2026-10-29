using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NpcNameData", menuName = "LJS/NpcNameData")]
public class NpcNameData : ScriptableObject
{
    [Header("NPC 이름 목록 (최소 100개 이상 작성)")]
    public List<string> npcNames = new List<string>();
}