using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using RealSteel.Boss.Core;

namespace RealSteel.Boss.JunkWheeler
{
    /// <summary>
    /// [전장 기물: 폭발 화물 상자 (ExplosiveBoxFieldGimmick)]
    /// 기획서 §6.18 및 §3.2 (7):
    /// 1. 필드에 배치되는 빨간 폭발 화물 상자.
    /// 2. 플레이어가 타격 시: 1.5초간 붉은색 점멸(퓨즈) 후 자폭 (광역 80 피해 및 2초 스턴).
    /// 3. 보스가 돌진(Charge) 중 부딪히면: 즉시 자폭하여 보스에게 150 대미지 및 2초 스턴(InterruptToStun) 유발.
    /// 4. 자폭 시 광역 범위 내 플레이어와 보스 모두에게 피해를 전파함.
    /// </summary>
    public class ExplosiveBoxFieldGimmick : BossFieldGimmickBase
    {
        public override string FieldGimmickID => "GIMMICK_EXPLOSIVE_BOX";

        [Header("폭발 파라미터")]
        [SerializeField] private int bossDamage = 150;
        [SerializeField] private int playerDamage = 80;
        [SerializeField] private float bossStunDuration = 2.0f;
        [SerializeField] private float explosionRadius = 2.5f;
        [SerializeField] private float fuseDuration = 1.5f;
        [SerializeField] private ParticleSystem explosionParticle;

        private Collider2D _collider;
        private SpriteRenderer _spriteRenderer;
        private Tweener _flashTween;
        private bool _isTriggered = false;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        protected override bool CanInteract(GameObject actor)
        {
            return !_isTriggered && IsInteractable && actor != null;
        }

        protected override void ExecuteGimmickEffect(GameObject actor)
        {
            if (_isTriggered) return;
            _isTriggered = true;
            IsInteractable = false;

            bool isBoss = actor.CompareTag("Boss") || actor.GetComponent<BossControllerBase>() != null;

            if (isBoss)
            {
                // 보스 돌진 직격: 즉시 자폭
                Debug.Log("<color=red>[ExplosiveBoxFieldGimmick] ▶ 보스 돌진 직격! 폭발 상자 즉시 자폭!</color>");
                TriggerExplosion();
            }
            else
            {
                // 플레이어 타격: 1.5초 퓨즈 후 자폭
                Debug.Log("<color=yellow>[ExplosiveBoxFieldGimmick] 플레이어 타격 감지! 1.5초 후 자폭 시퀀스 개시!</color>");
                StartFuseCountdownAsync().Forget();
            }
        }

        private async UniTaskVoid StartFuseCountdownAsync()
        {
            if (_spriteRenderer != null)
            {
                _flashTween = _spriteRenderer.DOColor(Color.red, 0.12f).SetLoops(-1, LoopType.Yoyo);
            }

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(fuseDuration), cancellationToken: this.GetCancellationTokenOnDestroy());
                TriggerExplosion();
            }
            catch (OperationCanceledException)
            {
                // 파괴 시 정상 종료
            }
        }

        private void TriggerExplosion()
        {
            if (_flashTween != null && _flashTween.IsActive())
            {
                _flashTween.Kill();
            }

            Vector2 center = transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, explosionRadius);

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                // 1. 보스 적중 처리: 150 대미지 및 2초 스턴
                var boss = hit.GetComponent<BossControllerBase>() ?? hit.GetComponentInParent<BossControllerBase>();
                if (boss != null)
                {
                    Debug.Log($"<color=red>[ExplosiveBoxFieldGimmick] 폭발로 보스에게 {bossDamage} 피해 및 {bossStunDuration}초 스턴 부여!</color>");
                    boss.TakeDamage(bossDamage);
                    boss.InterruptToStun(bossStunDuration);
                    continue;
                }

                // 2. 플레이어 적중 처리: 80 피해 (Space 무적 회피 검사)
                if (hit.CompareTag("Player"))
                {
                    bool isInvincible = false;
                    var executor = hit.GetComponent<ActionExecutor>() ?? hit.GetComponentInParent<ActionExecutor>();
                    if (executor != null && executor.State != null && executor.State.CurrentAction != null)
                    {
                        isInvincible = executor.State.CurrentAction.IsInvincibleDuringActive;
                    }

                    if (isInvincible)
                    {
                        Debug.Log("<color=cyan>[ExplosiveBoxFieldGimmick] 플레이어가 Space 무적으로 상자 폭발을 회피함!</color>");
                    }
                    else
                    {
                        var hub = CombatDataHub.Instance;
                        if (hub != null && hub.CurrentBattleMode == BattleMode.RobotVsBoss && hub.BossSnapshot != null && hub.PlayerSnapshot != null)
                        {
                            var res = hub.ProcessHit("ExplosiveBox", "Player", playerDamage);
                            Debug.Log($"<color=red>[ExplosiveBoxFieldGimmick] 플레이어가 상자 폭발에 피격당함! (원시 피해: {playerDamage} ➔ 방어력 적용 피해: {res.DamageToHp})</color>");
                        }
                        else
                        {
                            Debug.Log($"<color=red>[ExplosiveBoxFieldGimmick] 플레이어가 상자 폭발에 피격당함! (피해량: {playerDamage})</color>");
                            hub?.ApplyCoreDamage("Player", playerDamage);
                        }
                    }
                }
            }

            // 시각 연출 및 오브젝트 파괴
            if (_collider != null) _collider.enabled = false;
            if (_spriteRenderer != null) _spriteRenderer.enabled = false;

            if (explosionParticle != null)
            {
                explosionParticle.transform.SetParent(null);
                explosionParticle.Play();
                Destroy(explosionParticle.gameObject, 2.0f);
            }

            Destroy(gameObject, 0.2f);
        }

        private void OnDestroy()
        {
            if (_flashTween != null && _flashTween.IsActive())
            {
                _flashTween.Kill();
            }
        }
    }
}
