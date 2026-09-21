using UnityEngine;

/// <summary>
/// 프레임 단위 정지·전진 디버그 도구 (§9 3단계).
/// F2: Time.timeScale 토글 — deltaTime이 0이 되어 CombatClock이 자연히 멈춘다.
/// F3: 정지 상태에서 CombatClock.Tick()을 수동으로 1회 호출해 정확히 1프레임 전진한다.
/// 씬에 빈 오브젝트 하나 만들어 붙여두면 된다 (전투 로봇에 붙일 필요 없음).
/// </summary>
public class FrameStepper : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2))
        {
            Time.timeScale = Time.timeScale == 0f ? 1f : 0f;
            Debug.Log(Time.timeScale == 0f ? "[FrameStepper] 일시정지" : "[FrameStepper] 재생");
        }

        if (Input.GetKeyDown(KeyCode.F3))
        {
            if (Time.timeScale != 0f)
            {
                Debug.LogWarning("[FrameStepper] F2로 먼저 정지해야 한 프레임씩 전진할 수 있음");
                return;
            }

            CombatClock.Instance.Tick();

            // 논리(ActionState)는 위에서 이미 1틱 전진함. 그림(Animator)은 Time.timeScale=0이라
            // 자동으로는 안 움직이므로 여기서 씬의 모든 로봇을 똑같이 1틱만큼 수동으로 밀어준다.
            foreach (var view in FindObjectsByType<RobotView>(FindObjectsSortMode.None))
            {
                view.AdvanceOneTick();
            }
        }
    }
}
