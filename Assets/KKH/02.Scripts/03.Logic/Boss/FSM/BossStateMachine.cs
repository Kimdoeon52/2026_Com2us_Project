using System;
using UnityEngine;

namespace RealSteel.Boss.FSM
{
    /// <summary>
    /// [보스 FSM 상태 머신 관리자 (BossStateMachine)]
    /// 상태 전이, 생명주기 제어 및 이벤트 통보를 담당함.
    /// </summary>
    public class BossStateMachine
    {
        public BossStateBase CurrentState { get; private set; }

        public event Action<string, string> OnStateChanged;

        public void Initialize(BossStateBase startState)
        {
            CurrentState = startState;
            CurrentState?.Enter();
            OnStateChanged?.Invoke(null, CurrentState?.StateName);
        }

        public void ChangeState(BossStateBase newState)
        {
            if (newState == null) return;

            string prevStateName = CurrentState?.StateName;
            CurrentState?.Exit();

            CurrentState = newState;
            CurrentState.Enter();

            OnStateChanged?.Invoke(prevStateName, CurrentState.StateName);
        }

        public void Update()
        {
            CurrentState?.Update();
        }

        public void OnDamageTaken(int damage)
        {
            CurrentState?.OnDamageTaken(damage);
        }
    }
}
