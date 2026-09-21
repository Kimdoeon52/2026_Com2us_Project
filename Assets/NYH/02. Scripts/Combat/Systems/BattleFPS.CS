using UnityEngine;

// 임시 프레임 제한용 스크립트
//
// 왜 필요한가: 유니티는 기본적으로 화면 주사율에 맞춰 렌더링하려고 하는데, 모니터에 따라 이게
// 무제한으로 높게(수백 fps) 돌아갈 수 있다. CombatClock은 Time.deltaTime을 누적해서 1/60초마다
// 틱을 도는 구조(§3)라 이론적으로는 프레임 속도와 무관하게 안전해야 하지만, 실제 테스트 환경에서
// 프레임 속도를 일정하게 맞춰두면(targetFrameRate) deltaTime 값 자체가 들쭉날쭉하지 않아서
// 디버깅할 때 "지금 진짜 버그인지 프레임 속도 튐 때문인지" 헷갈리는 경우를 줄여준다.
// §3의 "초기 구현 시 허용되는 단순화"에 해당하는 임시 조치.
public class BattleFPS : MonoBehaviour
{
    public int maxFPS = 60; // 인스펙터에서 바꿀 수 있게 public — 60 고정이 기본이지만 테스트 중엔 30/120 등으로 바꿔가며 CombatClock이 정말 속도와 무관한지 검증하는 용도로도 쓸 수 있다

    // Start가 아니라 Awake인 이유: 다른 스크립트(CombatClock 등)가 Start에서 첫 틱을 돌기 전에
    // 프레임 제한이 먼저 걸려있어야, 시작하자마자 튀는 deltaTime 값을 최대한 피할 수 있다
    void Awake()
    {
        Application.targetFrameRate = maxFPS;
    }
}
