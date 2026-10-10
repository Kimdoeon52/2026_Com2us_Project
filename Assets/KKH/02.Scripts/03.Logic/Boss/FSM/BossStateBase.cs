using System.Threading;
using UnityEngine;

namespace RealSteel.Boss.FSM
{
    /// <summary>
    /// [보스 FSM 상태 기본 추상 클래스 (BossStateBase)]
    /// 모든 보스 상태(대기, 전조, 시전, 스턴, 그로기 등)의 생명주기와 인터럽트 인터페이스를 정의함.
    /// </summary>
    public abstract class BossStateBase
    {
        protected readonly BossController boss;
        protected readonly CancellationToken cancellationToken;

        public abstract string StateName { get; }

        protected BossStateBase(BossController boss, CancellationToken cancellationToken)
        {
            this.boss = boss;
            this.cancellationToken = cancellationToken;
        }

        /// <summary>상태 진입 시 1회 호출됨</summary>
        public virtual void Enter() { }

        /// <summary>매 프레임 호출됨</summary>
        public virtual void Update() { }

        /// <summary>상태 탈출 시 1회 호출됨 (정리 로직)</summary>
        public virtual void Exit() { }

        /// <summary>
        /// 상태 실행 중 보스가 대미지를 입었을 때 호출됨 (전조 중 저지 누적 대미지 집계 등에 활용)
        /// </summary>
        public virtual void OnDamageTaken(int damage) { }
    }
}
