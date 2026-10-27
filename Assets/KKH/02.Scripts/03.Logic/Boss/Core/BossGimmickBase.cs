using System;
using UnityEngine;

namespace RealSteel.Boss.Core
{
    /// <summary>
    /// [보스 기믹 최상위 추상 기본 클래스]
    /// 보스에 부착되는 패턴, 부위, 기물의 공통 인터페이스 및 수명 주기를 정의할거임
    /// </summary>
    public abstract class BossGimmickBase : MonoBehaviour
    {
        public abstract string GimmickID  { get; }
        public abstract string GimmickName { get; }
        
        public BossControllerBase OwnerBoss {get; private set;}
        public bool IsActive { get; protected set; } = true;
        
        public void Attach(BossControllerBase owner)
        {
            OwnerBoss = owner;
            IsActive = true;
            OnAttached();  
        }
        public void Detach()
        {
            OnDetached();
            IsActive = false;
            OwnerBoss = null;
        }
        public void NotifyDamageTaken(int damage)
        {
            if(!IsActive)
            {
                return;
            }
            OnDamageTaken(damage);
        }

        protected virtual void OnDamageTaken(int damage)
        { }

        protected virtual void OnDetached()
        { }
        protected virtual void OnAttached()
        { }
        public virtual void ResetGimmick()
        {
            IsActive = true;
        }
    }
}
