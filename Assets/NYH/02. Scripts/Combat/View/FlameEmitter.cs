using UnityEngine;

/// <summary>
/// 화염방사기 불줄기 연출 (View). 판정과 무관하다 — 데미지는 행동의 Hit박스가 낸다(§4).
///
/// ── 왜 이 방식인가 (2026-10-11) ───────────────────────────────────────────────────────
/// 기획서1011 §6-10 "화염 방사기 패턴"에 쓸 불 그림이 없었다. 그래서 그림 대신 코드로 불을 그린다.
/// 쓰는 기법은 둠(1993, PSX판)의 불꽃 알고리즘: 숫자 격자 하나 + 아주 단순한 규칙 하나로 그럴듯한 불이 나온다.
///   · 결과물이 원래 도트(픽셀)라서 픽셀아트 로봇 옆에 둬도 이질감이 없다.
///   · 70×18칸 정도라 매 스텝 계산해도 비용이 거의 없다.
///   · 끄는 연출이 공짜다 — 불씨만 끄면 총구 쪽부터 끊기고 남은 불길이 앞으로 날아가며 사라진다.
///
/// ── 알고리즘 (원작을 옆으로 눕힌 것) ─────────────────────────────────────────────────
///   격자: heat[y * cols + x], 값 0~36 = "불 세기". x = 총구에서 앞으로 몇 칸, y = 아래(0)→위.
///
///      x=0(총구)                                         x=cols-1
///   y↑ ┌───────────────────────────────────────────────┐
///      │                 . : - = + * # % @  ← 끝으로 갈수록 성겨짐(edgeFadeStart)
///      │ @@@@ % % # # * * + + = = - - : : . .   ← 앞으로 갈수록 식음(decay)
///      │ @@@@ ← 불씨(36). 가운데 nozzleRows 줄만
///      │ @@@@ % % # # * * + + = = - - : : . .
///      │                 . : - = + * # % @
///      └───────────────────────────────────────────────┘
///
///   한 "패스"(Spread) = 불이 한 칸 앞으로 나아감:
///     1. 총구 열(x=0)의 가운데 몇 줄을 36으로 채운다(꺼져 있으면 0).
///     2. 오른쪽 끝 열부터 거꾸로, 각 칸 (x, y)는 왼쪽 칸 (x-1, y)의 값을 받는다:
///        · 조금 깎인다(decay) — 평균적으로 FlameData.length 거리에서 0이 되도록 계산된 양
///        · 정면 한 칸에 놓는다(riseChance 확률로 한 칸 위 — 불끝이 위로 피어오름)
///        · 위·아래 이웃 칸에도 각각 spreadChance 확률로, edgeCooling만큼 더 식혀서 놓는다 → 원뿔처럼 넓어짐
///        · 가장자리에 가까운 칸일수록 "안 붙을" 확률이 올라간다(edgeFadeStart) → 위아래 경계가 들쭉날쭉 사라짐
///     3. 한 스텝(stepIntervalTicks 틱마다)에 이 패스를 passesPerStep번 돌린다 → 불이 flowSpeed 속도로 뻗음
///     4. 세기 0~36을 FlameData.Palette(그라데이션)로 색칠해서 텍스처에 올린다(Upload)
///
///   원작과 다른 점 두 가지 (둘 다 Python으로 같은 로직을 돌려 ASCII로 모양을 확인하고 고쳤다):
///     · 원작은 "빈 칸이면 0을 쓴다" → 우리 순서에선 위로 번진 불을 다음 줄의 0이 덮어써서 불이 아래로만 번졌다.
///       → 열을 먼저 비우고, 받은 값만 "더 뜨거운 쪽"으로 채운다(Put).
///     · 원작은 한 칸이 한 칸에만 값을 넘긴다 → 원작은 불씨가 바닥 한 줄 전체라 빽빽하지만, 우리는 총구 몇 줄뿐이라
///       흩어진 불티 몇 개가 됐다. → 위·아래 이웃에도 식힌 값을 번지게 해서 뜨거운 심지 + 넓어지는 원뿔로.
///
/// ── 언제 켜지고 꺼지나 ────────────────────────────────────────────────────────────
///   켜짐: ActionData.Flame이 있는 행동의 활성 구간 시작(OnActionActiveStart)
///   꺼짐: 후딜 시작(OnActionRecoveryStart) / 다른 행동 시작(경직으로 끊긴 경우 포함, OnActionBegin) / 사망(OnDead)
///   "꺼짐" = 불씨만 끈다. 남은 불길은 계속 계산되다가 다 날아가면 그때 렌더러를 숨긴다.
///   기술 이름으로 분기하지 않는다(§13) — 화염 기술이 몇 개로 늘어도 이 코드는 그대로다.
///
/// ── 시간·좌표 규칙 ───────────────────────────────────────────────────────────────
///   · 불 계산은 CombatClock 틱에서 한다(§3) — 주사율과 무관하게 같은 속도로 타고, F2 정지·F3 한 칸 전진에도 같이 멈추고 움직인다.
///   · 위치·좌우 반전은 렌더 프레임(LateUpdate)마다 로봇 총구를 따라간다 — 그림이라 틱 밖이어도 된다.
///   · 크기는 월드 단위(FlameData)로 받고 칸 크기는 로봇 스프라이트의 픽셀 크기에서 계산한다 → 불 도트 = 로봇 도트 크기.
///
/// ── 안 보일 때 확인 순서 ─────────────────────────────────────────────────────────
///   1. 콘솔에 "[FlameEmitter] …칸, 평균 길이…" 로그가 찍히는가? 안 찍히면 켜지지도 않은 것
///      → 이 컴포넌트가 로봇에 있는가(RobotView.EnsureEffectComponents가 자동으로 붙임) / 행동에 Flame이 연결됐는가
///   2. 로그는 찍히는데 안 보이면 → 머티리얼이 조명을 받아 어두운 것. FlameData.materialOverride에 Unlit 스프라이트 머티리얼
///   3. 불이 등 뒤로 나가면 → 로봇 Transform에 Y축 회전(-180)이 남아 있는 것. 회전은 0이어야 한다(RobotMover 경고 참고)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FlameEmitter : MonoBehaviour
{
    // 둠 원작 팔레트 단계 수. 세기는 0(꺼짐, 투명) ~ 36(가장 뜨거움, 흰색)이고, 팔레트 배열은 0~36 총 37칸이다
    private const int MaxHeat = 36;

    // 불의 "모양"은 FlameData(에셋)가 정하지만, 뻗는 속도는 이 로봇에 붙은 컴포넌트 값으로 둔다 —
    // 같은 불 모양을 쓰더라도 로봇마다 "쏘는 느낌"을 다르게 줄 수 있게
    [Tooltip("TODO: 연출값. 불이 총구에서 앞으로 뻗어 나가는 속도(월드 단위/초). 클수록 '쏘는' 느낌, 작을수록 '번지는' 느낌")]
    [Min(0.1f)] [SerializeField] private float flowSpeed = 8f;

    // ── 로봇 쪽 참조 (같은 오브젝트) ──
    private SpriteRenderer source;  // 로봇 그림 — 픽셀 크기(PPU)·머티리얼·정렬 순서를 여기서 가져온다
    private RobotMover mover;       // 바라보는 방향 — 총구 위치와 불 방향의 좌우를 정한다
    private ActionState state;      // 행동 이벤트(활성 시작/후딜 시작/새 행동/사망)를 구독하는 대상

    // ── 지금 뿜고 있는(또는 마지막으로 뿜은) 불 ──
    private FlameData data;
    private ActionData firingAction; // 총구 위치(ProjectileMuzzle)를 읽을 행동 — 플레이 중 총구를 옮기면 바로 따라간다
    private bool lit;                // 불씨가 켜져 있는가. 꺼져도 남은 불길이 다 사라질 때까지는 계속 계산한다

    // ── 시뮬레이션 격자 (가로 = 불이 뻗는 방향, 세로 = 두께) — 전부 Build()에서 FlameData로부터 계산 ──
    // 기본값(length 1 / thickness 0.35 / pixelScale 2 / PPU 100 / 스케일 1) 기준 예시: 칸 0.02 → 70×18칸, 총구 4줄
    private int cols, rows, nozzleRows;
    private float decayPerColumn;   // 한 칸 갈 때 평균적으로 줄어드는 세기. 36 ÷ (length를 칸으로 바꾼 값) → 예: 36/50 = 0.72
    private int passesPerStep;      // 한 스텝에 불이 몇 칸 나아가는지. flowSpeed × 스텝 시간 ÷ 칸 크기 → 예: 8 × (2/60) ÷ 0.02 ≈ 13
    private byte[] heat;            // 칸별 세기(0~36). 1차원 배열로 펴서 들고 있다: 인덱스 = y * cols + x
    private Color32[] pixels;       // heat를 색으로 바꾼 결과. 텍스처에 통째로 올린다
    private readonly Color32[] palette = new Color32[MaxHeat + 1]; // 세기 → 색 표. 매 칸 그라데이션을 계산하지 않으려고 미리 구워둔다
    private int tickCounter;        // stepIntervalTicks마다 한 번 계산하려고 세는 틱 수
    private int stepsSinceOut;      // 불씨를 끈 뒤 지난 스텝 수 — 남은 불이 다 날아갔는지 판단용

    // ── 화면에 그리는 오브젝트 ──
    private GameObject flameObject;   // 불 스프라이트를 들고 있는 별도 오브젝트(로봇의 자식 아님 — Build 주석 참고)
    private SpriteRenderer flameRenderer;
    private Texture2D texture;        // heat 격자를 그대로 찍는 텍스처. 1텍셀 = 1칸
    private Sprite sprite;
    // UnityEngine.Random 대신 System.Random을 쓰는 이유: 판정과 무관한 연출용 난수라 전역 시드(재현용)를 흔들지 않게 따로 둔다
    private readonly System.Random rng = new System.Random();

    private void Awake()
    {
        source = RobotView.FindRenderer(this); // 2026-10-28: 그림이 Visual 자식으로 분리돼서 루트 렌더러가 아니라 그림 렌더러를 본다
        mover = GetComponent<RobotMover>();
    }

    // OnEnable/OnDisable 짝으로 구독하는 이유는 PlayerRobotBootstrap과 같다 — 꺼졌다 켜져도 중복 구독·누락이 없게.
    // 해제 때 Instance가 아니라 Existing을 보는 이유: 씬 종료 중엔 CombatClock이 먼저 사라질 수 있는데, Instance는 그때 새로 만들어버린다
    private void OnEnable() => CombatClock.Instance.OnCombatTick += Step;

    private void OnDisable()
    {
        var clock = CombatClock.Existing;
        if (clock != null) clock.OnCombatTick -= Step;
    }

    // Start에서 구독하는 이유: ActionState는 PlayerRobotBootstrap.Awake → ActionExecutor.Init에서 만들어져서,
    // 이 컴포넌트의 Awake 시점엔 아직 없을 수 있다. Start는 모든 Awake가 끝난 뒤라 안전하다.
    // (RobotView.EnsureEffectComponents가 Awake 도중에 이 컴포넌트를 AddComponent로 붙여도 Start는 정상적으로 불린다)
    private void Start()
    {
        var executor = GetComponent<ActionExecutor>();
        state = executor != null ? executor.State : null;
        if (state == null)
        {
            Debug.LogWarning($"[FlameEmitter] {name}: ActionExecutor.State가 없음 — 불을 그리지 않음");
            return;
        }
        state.OnActionBegin += OnActionBegin;
        state.OnActionActiveStart += OnActiveStart;
        state.OnActionRecoveryStart += OnRecoveryStart;
        state.OnDead += Extinguish;
    }

    // 직접 만든 오브젝트·텍스처·스프라이트는 Unity가 알아서 안 지워준다(씬 오브젝트가 아니라 런타임 생성물) — 여기서 같이 치운다
    private void OnDestroy()
    {
        if (state != null)
        {
            state.OnActionBegin -= OnActionBegin;
            state.OnActionActiveStart -= OnActiveStart;
            state.OnActionRecoveryStart -= OnRecoveryStart;
            state.OnDead -= Extinguish;
        }
        if (flameObject != null) Destroy(flameObject);
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
    }

    // 호출: ActionState.OnActionBegin. 새 행동이 시작되면(경직으로 끊긴 경우 포함) 하던 불은 끈다.
    // ActionState.Interrupt도 내부에서 Begin을 부르므로 "맞아서 끊김"도 여기로 들어온다
    private void OnActionBegin(ActionData action) => Extinguish();

    // 호출: ActionState.OnActionActiveStart. 판정이 켜지는 순간 = 불이 나가는 순간. Flame이 없는 행동이면 아무것도 안 한다
    private void OnActiveStart(ActionData action)
    {
        if (action.Flame != null) Ignite(action);
    }

    // 호출: ActionState.OnActionRecoveryStart. 판정이 꺼지는 순간 불씨도 끈다 — 보이는 불과 맞는 구간을 맞추기 위해
    private void OnRecoveryStart(ActionData action) => Extinguish();

    // 불씨만 끈다. 렌더러는 남은 불길이 다 날아간 뒤 Step()에서 숨긴다 — 바로 숨기면 불이 툭 끊겨 보인다
    private void Extinguish() => lit = false;

    // 불을 켤 때마다 격자를 새로 만든다 — 플레이 중 인스펙터에서 FlameData를 고치면 다음 발사부터 바로 반영된다.
    // 몇 초에 한 번 일어나는 일이라 텍스처를 새로 만드는 비용은 문제 없다
    private void Ignite(ActionData action)
    {
        firingAction = action;
        data = action.Flame;
        Build();
        lit = true;
        stepsSinceOut = 0;
        flameRenderer.enabled = true;
    }

    // FlameData(월드 단위)를 칸 수로 바꾸고 텍스처·스프라이트를 만든다
    private void Build()
    {
        // 칸 크기 = 로봇 스프라이트 픽셀 하나의 월드 크기 × pixelScale.
        // 로봇 픽셀 하나 = 스케일 ÷ PPU (예: 1 ÷ 100 = 0.01). pixelScale 2면 불 한 칸 = 로봇 픽셀 2×2 = 0.02.
        // 이렇게 하면 로봇 PPU·스케일이 바뀌어도 불 도트가 항상 로봇 도트에 맞춰진다
        float ppu = source.sprite != null ? source.sprite.pixelsPerUnit : 100f;
        float robotScale = Mathf.Abs(transform.lossyScale.x);
        float cell = data.PixelScale * robotScale / ppu;

        // 평균적으로 불이 닿는 칸 수 — 여기서 세기가 0이 되도록 decay를 정한다
        int reachColumns = Mathf.Max(2, Mathf.RoundToInt(data.Length / cell));
        cols = Mathf.CeilToInt(reachColumns * 1.4f); // 흔들림으로 평균보다 멀리 튀는 불꽃이 텍스처 오른쪽 끝에서 잘리지 않게 40% 여유
        rows = Mathf.Max(3, Mathf.CeilToInt(data.Thickness / cell));
        nozzleRows = Mathf.Clamp(Mathf.RoundToInt(data.NozzleThickness / cell), 1, rows);
        decayPerColumn = (float)MaxHeat / reachColumns; // 36에서 시작해 reachColumns 칸쯤에서 0이 된다 → 평균 길이 = length

        // 한 스텝은 stepIntervalTicks 틱(예: 2틱 = 1/30초). 그동안 flowSpeed로 갈 거리를 칸 수로 바꾼 게 패스 수
        float stepSeconds = data.StepIntervalTicks * CombatClock.TICK;
        passesPerStep = Mathf.Max(1, Mathf.RoundToInt(flowSpeed * stepSeconds / cell));

        heat = new byte[cols * rows];
        pixels = new Color32[cols * rows];
        // 세기 i → 그라데이션의 i/36 지점 색. 0번은 그라데이션 왼쪽 끝(알파 0)이라 불이 없는 칸은 투명하다
        for (int i = 0; i <= MaxHeat; i++)
            palette[i] = data.Palette.Evaluate(i / (float)MaxHeat);

        if (flameObject == null)
        {
            // 로봇의 자식으로 두지 않는다 — Hit박스·총구 좌표는 로봇 스케일과 무관한 월드 단위라서, 자식으로 두면
            // 로봇 스케일이 한 번 더 곱해져 총구 위치가 어긋난다. 대신 LateUpdate에서 BoxResolver로 직접 위치를 계산한다
            flameObject = new GameObject($"Flame_{name}");
            flameRenderer = flameObject.AddComponent<SpriteRenderer>();
        }
        // 이전 발사 때 만든 텍스처·스프라이트는 버린다(크기가 바뀌었을 수 있음). Destroy 안 하면 발사할 때마다 메모리에 쌓인다
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);

        texture = new Texture2D(cols, rows, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,      // 도트가 번지지 않게(선형 보간하면 픽셀아트가 뿌옇게 된다)
            wrapMode = TextureWrapMode.Clamp,   // 가장자리 텍셀이 반대편으로 새어 나오지 않게
        };
        // 피벗 = 왼쪽 가운데(0, 0.5) → 스프라이트 원점이 총구에 붙고 오른쪽으로 뻗는다.
        // PPU를 "로봇 PPU ÷ pixelScale"로 줘서 텍셀 1개가 정확히 로봇 픽셀 pixelScale개 크기가 되게 한다
        sprite = Sprite.Create(texture, new Rect(0, 0, cols, rows), new Vector2(0f, 0.5f), ppu / data.PixelScale);

        flameRenderer.sprite = sprite;
        flameRenderer.sharedMaterial = data.MaterialOverride != null ? data.MaterialOverride : source.sharedMaterial;
        flameRenderer.sortingLayerID = source.sortingLayerID;
        flameRenderer.sortingOrder = source.sortingOrder + 1; // 로봇보다 앞 — 총구에서 나온 불이 몸에 가려지지 않게 (§10 정렬은 sortingOrder로)

        // 실제로 만들어진 크기를 남겨둔다 — "불이 Hit박스보다 길다/짧다"를 조정할 때 숫자로 대조하는 용도
        Debug.Log($"[FlameEmitter] {name}: {cols}x{rows}칸, 평균 길이 {data.Length:F2} (칸 {cell:F3}), 스텝당 {passesPerStep}칸 전진");
    }

    // 호출: CombatClock.OnCombatTick (1/60초마다). stepIntervalTicks마다 한 번 계산하고 텍스처를 갱신한다.
    // 매 틱(60fps) 계산하면 너무 빠르게 일렁여서 산만하다 — 기본 2틱(30fps)이 원작 둠의 느낌에 가깝다
    private void Step()
    {
        if (heat == null || !flameRenderer.enabled) return; // 아직 한 번도 안 켰거나, 다 꺼져서 숨긴 상태

        tickCounter++;
        if (tickCounter % data.StepIntervalTicks != 0) return;

        for (int i = 0; i < passesPerStep; i++) Spread();
        Upload();

        // 불씨가 꺼진 뒤 마지막 불꽃이 끝까지 날아가 사라질 만큼 기다렸다가 숨긴다.
        // "격자 가로 + 세로" 칸만큼 전진하면 총구에서 나온 어떤 불꽃도 격자 밖으로 나갔다고 볼 수 있다
        if (!lit && ++stepsSinceOut * passesPerStep > cols + rows)
        {
            System.Array.Clear(heat, 0, heat.Length); // 다음 발사 때 이전 불이 남아 보이지 않게
            flameRenderer.enabled = false;
        }
    }

    // 둠 불꽃의 spreadFire를 옆으로 눕힌 것. 한 번 돌면 불이 한 칸 앞으로 나아간다 (클래스 주석의 "한 패스")
    private void Spread()
    {
        // 1) 총구 열(0번 열): 가운데 nozzleRows 줄만 불씨(36). 꺼져 있으면 0 → 여기서부터 불이 끊겨 나간다
        int nozzleStart = (rows - nozzleRows) / 2;
        for (int y = 0; y < rows; y++)
            heat[y * cols] = (byte)(lit && y >= nozzleStart && y < nozzleStart + nozzleRows ? MaxHeat : 0);

        // decay가 0.72처럼 소수면 "정수 부분 + 소수 부분 확률로 1 더" 깎는다 → 평균이 정확히 decayPerColumn이 되고,
        // 칸마다 깎이는 양이 달라서 불끝이 자연스럽게 들쭉날쭉해진다
        int whole = (int)decayPerColumn;
        float fraction = decayPerColumn - whole;

        // 2) 오른쪽 끝 열부터 거꾸로 — 왼쪽 열(x-1)을 아직 이번 패스에서 안 고친 상태로 읽어야 한 패스에 정확히 한 칸만 나아간다.
        //    (왼쪽부터 돌면 방금 고친 열을 다음 열이 또 읽어서 한 패스에 불이 끝까지 순간이동한다 → 흐르는 느낌·꺼지는 연출이 사라짐)
        for (int x = cols - 1; x >= 1; x--)
        {
            // 이 열을 먼저 비우고 왼쪽 열에서 받은 값만 채운다. 원작처럼 "빈 칸이면 0을 쓴다"로 하면 위로 번진 불을
            // 다음 줄의 0이 덮어써서 불이 아래로만 번지고(2026-10-11 시뮬레이션으로 확인), 아무도 안 채운 칸엔 이전 값이 남는다
            for (int y = 0; y < rows; y++) heat[y * cols + x] = 0;

            for (int y = 0; y < rows; y++)
            {
                int src = heat[y * cols + x - 1];
                if (src == 0) continue; // 왼쪽이 꺼져 있으면 넘길 불이 없다

                int value = src - (whole + (rng.NextDouble() < fraction ? 1 : 0));
                if (value <= 0) continue; // 다 식었다 — 불끝

                // 정면 한 칸(riseChance 확률로 한 칸 위 — 텍스처 y는 위가 +) + 위·아래 이웃엔 더 식힌 값을 확률로.
                // 원작처럼 "한 칸에서 한 칸으로"만 넘기면 총구 몇 줄이 흩어지는 불티 몇 개가 되어 구멍투성이가 된다
                // (2026-10-11 시뮬레이션으로 확인) — 이웃으로 번지게 해야 뜨거운 심지 + 넓어지는 원뿔이 된다.
                // 이웃 값을 edgeCooling만큼 더 깎는 이유: 바깥쪽이 안쪽보다 빨리 식어야 "가운데가 하얗고 가장자리가 붉은" 불이 된다
                int ty = y + (rng.NextDouble() < data.RiseChance ? 1 : 0);
                Put(x, ty, value);
                if (rng.NextDouble() < data.SpreadChance) Put(x, ty + 1, value - rng.Next(1, data.EdgeCooling + 1));
                if (rng.NextDouble() < data.SpreadChance) Put(x, ty - 1, value - rng.Next(1, data.EdgeCooling + 1));
            }
        }
    }

    // 칸 (x, y)에 세기 value를 놓는다. 두 칸이 같은 자리로 오면 더 뜨거운 쪽을 남긴다. 격자 밖·0 이하는 버린다
    private void Put(int x, int y, int value)
    {
        if (y < 0 || y >= rows || value <= 0) return;

        // 위·아래 가장자리 부드럽게 꺼지기 (동전 던지기 방식):
        //   가운데에서 얼마나 벗어났는지(fromCenter, 가운데 0 ~ 끝 1)를 보고, edgeFadeStart를 넘어선 칸은
        //   "이 칸엔 불이 안 붙음(뒷면)" 확률을 0 → 1로 서서히 올린다. 끝 줄에선 확률 1이라 절대 안 닿는다.
        //   SmoothStep을 쓰는 이유: 직선으로 올리면 시작 지점에서 확률이 갑자기 꺾여 그 줄에 경계가 보인다 — 양 끝을 둥글게 이어준다.
        //   예) edgeFadeStart 0.4: 가운데 40%까진 그대로, 70% 지점에서 절반 확률, 100%(끝)에서 전부 꺼짐.
        // 이게 없으면 위로 피어오른 불이 텍스처 맨 윗줄에 꽉 차서 천장에 막힌 듯 일자로 잘린다(2026-10-11 실제 화면에서 확인)
        float half = rows * 0.5f;
        float fromCenter = Mathf.Abs(y + 0.5f - half) / half; // +0.5 = 칸의 가운데 기준으로 잰다(위아래 대칭이 되게)
        float fadeStart = data.EdgeFadeStart;
        if (fadeStart < 1f && fromCenter > fadeStart) // 1이면 끄기 = 예전처럼 일자로 잘림
        {
            float t = Mathf.SmoothStep(0f, 1f, (fromCenter - fadeStart) / (1f - fadeStart));
            if (rng.NextDouble() < t) return; // 뒷면 — 이 칸엔 안 붙는다
        }

        int index = y * cols + x;
        if (value > heat[index]) heat[index] = (byte)value;
    }

    // heat(세기) → pixels(색) → 텍스처. 한 스텝에 한 번만 GPU로 올린다(패스마다 올리면 낭비)
    private void Upload()
    {
        for (int i = 0; i < heat.Length; i++) pixels[i] = palette[heat[i]];
        texture.SetPixels32(pixels);
        texture.Apply(false); // false = 밉맵 다시 안 만듦(애초에 밉맵 없는 텍스처)
    }

    // 총구를 따라간다. 렌더 프레임마다 — 위치는 판정이 아니라 그림이라 틱 밖에서 해도 된다(§3).
    // firingAction.ProjectileMuzzle을 매번 다시 읽으므로, 플레이 중 ActionDataEditor로 총구를 끌면 불도 바로 따라온다
    private void LateUpdate()
    {
        if (flameRenderer == null || !flameRenderer.enabled || firingAction == null) return;

        bool facingRight = mover == null || mover.FacingRight;
        float scale = Mathf.Abs(transform.lossyScale.x);
        Transform t = flameObject.transform;
        // 총구 = 로봇 피벗 + 총구 로컬 좌표(왼쪽을 보면 x 반전). Hit박스와 같은 BoxResolver를 써서 "보이는 불"과 "맞는 박스"의 기준점이 같다
        t.SetPositionAndRotation(BoxResolver.ToWorldPoint(transform.position, firingAction.ProjectileMuzzle, facingRight), transform.rotation);
        // 불 텍스처는 코드로 만든 그림이라 항상 오른쪽(+x)으로 뻗는다 — 그래서 시트 그림 방향(RobotMover.ScaleSignFor)이 아니라
        // 바라보는 방향만 보고 뒤집는다. 로봇 회전은 0이어야 한다(예전 Y축 -180이 남아 있으면 불이 등 뒤로 나간다)
        t.localScale = new Vector3(facingRight ? scale : -scale, scale, scale);
    }
}
