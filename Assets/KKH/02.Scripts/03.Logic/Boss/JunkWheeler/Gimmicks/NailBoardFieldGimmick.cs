using System;
using UnityEngine;
using RealSteel.Boss.Core;

namespace RealSteel.Boss.JunkWheeler
{
    /// <summary>
    /// [전장 기물: 고철 못판 (NailBoardFieldGimmick)]
    /// 기획서 §6.18 및 §3.2 (6), §4.1:
    /// 1. 전장 좌/우 1/3 지점에 배치되는 고철 못판 오브젝트.
    /// 2. 보스가 3단 내려찍기(Slam)로 못판 위에 착지할 때 반응.
    /// 3. 보스의 폐타이어 부위(TIRE_PART) 내구도 1 차감 및 펑크 카운트 증가.
    /// 4. 보스 컨트롤러의 InterruptToGroggy(4.0f, 1.5f)를 호출하여 4초 무력화(받는 피해 1.5배) 확정 유도.
    /// 5. 상호작용 후 1회용 소멸.
    /// </summary>
    public class NailBoardFieldGimmick : BossFieldGimmickBase
    {
        public override string FieldGimmickID => "GIMMICK_NAIL_BOARD";

        [Header("못판 파라미터")]
        [SerializeField] private float groggyDuration = 4.0f;
        [SerializeField] private float damageMultiplier = 1.5f;
        [SerializeField] private ParticleSystem breakParticle;

        private Collider2D _collider;
        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        protected override bool CanInteract(GameObject actor)
        {
            if (actor == null) return false;
            return actor.CompareTag("Boss") || actor.GetComponent<BossControllerBase>() != null;
        }

        protected override void ExecuteGimmickEffect(GameObject actor)
        {
            var boss = actor.GetComponent<BossControllerBase>() ?? actor.GetComponentInParent<BossControllerBase>();
            if (boss == null) return;

            Debug.Log("<color=magenta>[NailBoardFieldGimmick] ▶ [기믹 파훼 성공!] 보스가 고철 못판에 착지함! 타이어 펑크 및 4초 그로기 발생!</color>");

            // 1. 타이어 부위 기믹 내구도 차감
            var tireGimmick = boss.GetGimmick<JunkWheelerTirePartGimmick>("TIRE_PART");
            if (tireGimmick != null)
            {
               /* tireGimmick.TakePartDamage(1, GimmickTag.Pierce);*/
            }

            // 2. 보스 4초 그로기 (피해 1.5배) 인터럽트
            boss.InterruptToGroggy(groggyDuration, damageMultiplier);

            // 3. 1회용 소멸 처리
            IsInteractable = false;
            if (_collider != null) _collider.enabled = false;

            if (breakParticle != null)
            {
                breakParticle.transform.SetParent(null);
                breakParticle.Play();
                Destroy(breakParticle.gameObject, 2.0f);
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = false;
            }

            Destroy(gameObject, 0.5f);
        }
    }
}
