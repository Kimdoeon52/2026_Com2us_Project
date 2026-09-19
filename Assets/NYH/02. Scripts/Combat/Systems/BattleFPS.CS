using UnityEngine;

//임시 프레임 제한용 스크립트
public class BattleFPS : MonoBehaviour
{
    public int maxFPS = 60;
    void Awake()
    {
        Application.targetFrameRate = maxFPS;
    }
}