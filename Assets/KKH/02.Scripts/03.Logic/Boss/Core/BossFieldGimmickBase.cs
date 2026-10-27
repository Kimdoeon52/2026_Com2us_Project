using UnityEngine;

namespace RealSteel.Boss.Core
{
    /// <summary>
    /// [전장 상호작용 기물 템플릿 클래스]
    /// </summary>
    public abstract class BossFieldGimmickBase : MonoBehaviour
    {
        public abstract string FieldGimmickID { get; }
        public bool IsInteractable { get; protected set; } = true;

        public void TriggerInteraction(GameObject actor)
        {
            if (!IsInteractable || !CanInteract(actor))
            {
                return;
            }
            ExecuteGimmickEffect(actor);
            OnPostInteract();
        }

        protected abstract bool CanInteract(GameObject actor);
        protected abstract void ExecuteGimmickEffect(GameObject actor);
        protected virtual void OnPostInteract() { }
        public virtual void ResetGimmick() { IsInteractable = true; }
    }
}