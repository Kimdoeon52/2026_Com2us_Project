using UnityEngine;

public enum DroneState
{
    Off,
    Orbiting,
    Attacking,
}

public class DroneUnit : MonoBehaviour
{
    [Tooltip("드론이 꺼질 때 나올 이펙트(선택). 루트에 ParticleSystem이 있어야 재생 후 자동으로 꺼진다")]
    [SerializeField] protected GameObject consumeEffectPrefab;

    private GameObject consumeEffectInstance;
    private ParticleSystem consumeEffectParticle;

    private TrailRenderer[] trails;

    // 공전 속도 버퍼 (SmoothDamp용)
    [HideInInspector] public Vector3 velocity;
    public DroneState State { get; private set; } = DroneState.Off;

    [HideInInspector] public Vector3 attackStartPos;
    [HideInInspector] public Vector2 attackLocalTarget;

    // Awake에서 이펙트까지 만든다: 허브가 전투 시작 시 드론을 미리 생성할 때 이펙트도 같이 준비되게.
    // Start는 첫 활성화(=첫 Appear, 전투 중) 때 불리므로 미리 준비하는 용도로는 늦다
    protected virtual void Awake()
    {
        trails = GetComponentsInChildren<TrailRenderer>(true);
        CreateConsumeEffect();
        gameObject.SetActive(false);
    }

    protected virtual void OnDestroy()
    {
        // 부모 없이 만든 오브젝트라 드론이 파괴돼도 같이 안 지워진다
        if (consumeEffectInstance != null) Destroy(consumeEffectInstance);
    }

    private void CreateConsumeEffect()
    {
        if (consumeEffectPrefab == null) return;

        // 드론 자식으로 두지 않는다 — 드론은 Consume 직후 꺼지므로 자식이면 이펙트도 같이 꺼진다
        consumeEffectInstance = Instantiate(consumeEffectPrefab);
        consumeEffectInstance.name = $"{name}_ConsumeFX";

        consumeEffectParticle = consumeEffectInstance.GetComponent<ParticleSystem>();
        if (consumeEffectParticle != null)
        {
            // 재생이 끝나면 스스로 SetActive(false) — 드론이 꺼져 있어도 이펙트가 알아서 정리된다
            var main = consumeEffectParticle.main;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Disable;
        }
        else
        {
            Debug.LogWarning($"[DroneUnit] {name}: 이펙트 루트에 ParticleSystem이 없어 자동으로 꺼지지 않음");
        }

        consumeEffectInstance.SetActive(false);
    }

    /// <summary>풀에서 꺼내 공전 상태로 등장</summary>
    public void Appear(Vector3 position)
    {
        transform.position = position;
        velocity = Vector3.zero;
        ClearTrails();
        State = DroneState.Orbiting;
        gameObject.SetActive(true);
    }

    /// <summary>공격 위치로 출격</summary>
    public void BeginAttack(Vector2 localTarget)
    {
        State = DroneState.Attacking;
        attackStartPos = transform.position;
        attackLocalTarget = localTarget;
    }

    /// <summary>히트 프레임 도달 → 소모 연출 후 비활성</summary>
    public void Consume(bool playEffect)
    {
        if (playEffect) PlayConsumeEffect(transform.position);
        Hide();
    }

    /// <summary>연출 없이 즉시 끄기 (실린더 리셋 등)</summary>
    public void Hide()
    {
        State = DroneState.Off;
        gameObject.SetActive(false);
    }

    private void PlayConsumeEffect(Vector3 position)
    {
        if (consumeEffectInstance == null) return;

        // 이전 재생이 아직 남아 있어도 처음부터 다시 재생되도록 껐다 켠다
        consumeEffectInstance.SetActive(false);
        consumeEffectInstance.transform.position = position;
        consumeEffectInstance.SetActive(true);

        if (consumeEffectParticle != null)
        {
            consumeEffectParticle.Clear(true);
            consumeEffectParticle.Play(true);
        }
    }

    private void ClearTrails()
    {
        if (trails == null) return;
        foreach (var t in trails) t.Clear();
    }
}