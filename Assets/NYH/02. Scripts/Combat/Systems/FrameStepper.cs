using UnityEngine;

/// <summary>
/// 프레임 단위 정지·전진 디버그 도구 (§9 3단계).
/// F2: Time.timeScale 토글 — deltaTime이 0이 되어 CombatClock이 자연히 멈춘다.
/// F3: 정지 상태에서 CombatClock.Tick()을 수동으로 1회 호출해 정확히 1프레임 전진한다.
/// F4: 판정 박스를 Game 뷰에도 표시할지 토글 (§9 2단계, BoxDrawer.ShowInGameView).
/// 씬에 빈 오브젝트 하나 만들어 붙여두면 된다 (전투 로봇에 붙일 필요 없음).
/// </summary>
public class FrameStepper : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            // 0이면 1로, 0이 아니면(보통 1) 0으로 — 단순 토글. timeScale=0을 만드는 것만으로
            // CombatClock.Update()의 Time.deltaTime도 같이 0이 되어 전투 로직이 저절로 멈춘다(§3) —
            // 여기서 CombatClock을 직접 멈추라고 호출할 필요가 없다는 게 이 설계의 핵심 장점
            Time.timeScale = Time.timeScale == 0f ? 1f : 0f;
            Debug.Log(Time.timeScale == 0f ? "[FrameStepper] 일시정지" : "[FrameStepper] 재생");
        }

        if (Input.GetKeyDown(KeyCode.F10))
        {
            // 정지 상태가 아니면 F10를 눌러봐야 의미가 없다 — 이미 매 프레임 자동으로 틱이 돌고 있으므로
            // "한 프레임만 전진"이라는 개념 자체가 성립하지 않는다. 그래서 먼저 F2로 멈추라고 안내만 하고 리턴
            if (Time.timeScale != 0f)
            {
                Debug.LogWarning("[FrameStepper] F9로 먼저 정지해야 한 프레임씩 전진할 수 있음");
                return;
            }

            // CombatClock이 평소 Update()에서 자동으로 부르는 것과 똑같은 함수를 여기서 수동으로 딱 1번만 부른다.
            // 이 한 줄로 모든 로봇의 ActionState.Advance()가 정확히 1프레임만큼만 전진한다
            CombatClock.Instance.Tick();

            // 논리(ActionState)는 위에서 이미 1틱 전진함. 그림(Animator)은 Time.timeScale=0이라
            // 자동으로는 안 움직이므로 여기서 씬의 모든 로봇을 똑같이 1틱만큼 수동으로 밀어준다.
            // (Animator는 Update()의 Time.deltaTime으로만 재생되는데 그게 0으로 고정돼 있어서,
            //  CombatClock.Tick()만 불러선 논리만 전진하고 화면은 그대로 멈춰있는 것처럼 보이는 문제가 있었음)
            foreach (var view in FindObjectsByType<RobotView>(FindObjectsSortMode.None))
            {
                view.AdvanceOneTick();
            }
        }

        if (Input.GetKeyDown(KeyCode.F11))
        {
            // 모든 BoxDrawer가 공유하는 static 스위치 하나만 뒤집으면 된다 — 로봇마다 따로 꺼줄 필요 없음
            BoxDrawer.ShowInGameView = !BoxDrawer.ShowInGameView;
            Debug.Log(BoxDrawer.ShowInGameView ? "[FrameStepper] 게임 뷰 판정 박스 표시 켬" : "[FrameStepper] 게임 뷰 판정 박스 표시 끔");
        }
    }
}
