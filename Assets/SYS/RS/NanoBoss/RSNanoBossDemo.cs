// RE:AL STEEL - 나노봇 보스 시험 진행 (HUD · 플레이어 더미 · 자동 공격)
//
// 시험 씬 전용. 보스의 느낌을 보려고 만든 것이지 게임 플레이어가 아니다.
//   · 플레이어 더미: 보스 반대편에서 좌우로 움직이고, 위험한 봇이 가까우면 점프한다
//   · 자동 공격: 더미가 보스의 가까운 쪽을 계속 쏜다 (질량이 깎여 페이즈가 넘어가는 걸 보려고)
//   · 마우스 클릭: 클릭한 자리를 때린다
//   · HUD: 난이도 1~5, 페이즈 · 패턴 · 질량, 패턴 직접 실행, 2페 · 3페로 건너뛰기
//   · 성능 · 밀도: 시뮬 ms · FPS, 봇 수 · 격자 간격 · 픽셀 크기를 바꿔 보며 정한다 (GTX 1060 기준 시뮬 1ms 이하 목표)
// 입력은 IMGUI 이벤트만 쓴다 (프로젝트 Input System 설정과 무관하게 동작)
using UnityEngine;

namespace RealSteel.NanoBoss
{
    [AddComponentMenu("RE_AL STEEL/나노봇 보스 시험 진행")]
    public class RSNanoBossDemo : MonoBehaviour
    {
        public RSNanoBossBrain brain;
        public RSNanoSwarm swarm;
        public Transform player;
        public LineRenderer tracer;
        public Camera cam;

        [Header("플레이어 더미")]
        public bool autoMove = true;
        public float moveSpeed = 5f;
        public float jumpSpeed = 10f;
        public float gravity = 26f;

        [Header("자동 공격")]
        public bool autoAttack = true;
        [Tooltip("초당 발사 수")]
        public float fireRate = 9f;
        public float shotRadius = 0.5f;
        public float clickRadius = 0.9f;

        int hitsTaken;
        float invuln, fireTimer, moveTimer, targetX, vy, tracerTimer, hurtFlash;
        bool grounded = true;
        Renderer playerRenderer;
        MaterialPropertyBlock mpb;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        Color playerColor = new Color(0.35f, 0.85f, 1f);
        Rect panel;
        bool showPatterns, showPerf = true;
        int pendingCount = -1;
        float fps;

        void Start()
        {
            if (!cam) cam = Camera.main;
            if (player) { playerRenderer = player.GetComponentInChildren<Renderer>(); targetX = player.position.x; }
            mpb = new MaterialPropertyBlock();
            if (tracer) tracer.enabled = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt > 0f) fps = fps <= 0f ? 1f / dt : Mathf.Lerp(fps, 1f / dt, 0.05f);
            if (!brain || !swarm || !player) return;

            MovePlayer(dt);

            // 피격 판정
            Vector3 body = player.position + Vector3.up * 0.9f;
            invuln -= dt;
            if (invuln <= 0f && swarm.DangerNear(body, 0.55f) > 0)
            {
                hitsTaken++;
                invuln = 0.6f;
                hurtFlash = 1f;
            }
            hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, dt * 3f);
            if (playerRenderer)
            {
                playerRenderer.GetPropertyBlock(mpb);
                bool blink = invuln > 0f && Mathf.Repeat(Time.time * 16f, 1f) < 0.5f;
                mpb.SetColor(BaseColor, Color.Lerp(playerColor, new Color(1f, 0.25f, 0.2f), hurtFlash) * (blink ? 0.4f : 1f));
                playerRenderer.SetPropertyBlock(mpb);
            }

            // 자동 공격
            if (autoAttack && brain.CanBeHit)
            {
                fireTimer += dt;
                float interval = 1f / Mathf.Max(0.5f, fireRate);
                while (fireTimer >= interval)
                {
                    fireTimer -= interval;
                    Vector3 gun = player.position + new Vector3(0.4f * Mathf.Sign(brain.BodyCenter.x - player.position.x), 1.2f, 0f);
                    if (brain.TryAimPoint(gun, out var p))
                    {
                        brain.Hit(p, shotRadius, (p - gun).normalized * 3f);
                        ShowTracer(gun, p);
                    }
                }
            }

            if (tracer && tracer.enabled)
            {
                tracerTimer -= dt;
                if (tracerTimer <= 0f) tracer.enabled = false;
            }
        }

        void MovePlayer(float dt)
        {
            Vector3 p = player.position;
            float floor = swarm.floorY;
            if (autoMove)
            {
                moveTimer -= dt;
                float bossX = brain.BodyCenter.x;
                float side = Mathf.Sign(p.x - bossX);
                if (side == 0f) side = -1f;
                if (moveTimer <= 0f)
                {
                    moveTimer = Random.Range(0.8f, 1.8f);
                    float near = bossX + side * 4.5f, far = bossX + side * 10f;
                    targetX = Mathf.Clamp(Random.Range(Mathf.Min(near, far), Mathf.Max(near, far)), brain.arenaMinX + 0.5f, brain.arenaMaxX - 0.5f);
                }
                p.x = Mathf.MoveTowards(p.x, targetX, moveSpeed * dt);

                // 위험한 봇이 가까우면 점프
                if (grounded && swarm.DangerNear(p + Vector3.up * 0.9f, 2.4f) > 0 && Random.value < 0.25f)
                {
                    vy = jumpSpeed;
                    grounded = false;
                }
            }
            vy -= gravity * dt;
            p.y += vy * dt;
            if (p.y <= floor) { p.y = floor; vy = 0f; grounded = true; }
            player.position = p;
        }

        void ShowTracer(Vector3 a, Vector3 b)
        {
            if (!tracer) return;
            tracer.positionCount = 2;
            tracer.SetPosition(0, a);
            tracer.SetPosition(1, b);
            tracer.enabled = true;
            tracerTimer = 0.035f;
        }

        // ─────────────────────────────────────────────────────────────
        // HUD + 마우스 클릭
        // ─────────────────────────────────────────────────────────────
        void OnGUI()
        {
            if (!brain || !swarm) return;
            var e = Event.current;

            panel = new Rect(12, 12, 330, (showPatterns ? 560 + 26 * brain.rigPatterns.Count : 300) + (showPerf ? 190 : 24));
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 10, panel.y + 8, panel.width - 20, panel.height - 16));

            GUILayout.Label("<b>나노봇 보스 시험</b>", Rich(14));
            GUILayout.BeginHorizontal();
            GUILayout.Label("난이도", GUILayout.Width(44));
            for (int d = 1; d <= 5; d++)
            {
                var name = RSNanoBossBrain.Difficulties[d - 1].name;
                bool on = brain.difficulty == d;
                if (GUILayout.Toggle(on, name, GUI.skin.button) && !on) { brain.Restart(d); hitsTaken = 0; }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("페이즈  <b>" + PhaseName(brain.Phase) + "</b>    패턴  <b>" + (string.IsNullOrEmpty(brain.CurrentPattern) ? "-" : brain.CurrentPattern) + "</b>", Rich(12));
            Rect bar = GUILayoutUtility.GetRect(10, 14, GUILayout.ExpandWidth(true));
            GUI.Box(bar, GUIContent.none);
            GUI.color = brain.Phase == BossPhase.Phase3 ? new Color(0.95f, 0.95f, 1f) : new Color(0.4f, 0.85f, 1f);
            GUI.DrawTexture(new Rect(bar.x + 1, bar.y + 1, (bar.width - 2) * Mathf.Clamp01(brain.Mass01), bar.height - 2), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.Label("질량 " + Mathf.RoundToInt(brain.Mass01 * 100f) + "%   봇 " + swarm.Alive + " / " + swarm.Count + "   플레이어 피격 " + hitsTaken, Rich(11));
            if (brain.LooksDefeated) GUILayout.Label("<b><color=#ffdd66>격파!</color></b>", Rich(16));

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            autoAttack = GUILayout.Toggle(autoAttack, " 자동 공격");
            autoMove = GUILayout.Toggle(autoMove, " 플레이어 움직임");
            GUILayout.EndHorizontal();
            fireRate = GUILayout.HorizontalSlider(fireRate, 1f, 30f);
            GUILayout.Label("초당 발사 " + fireRate.ToString("0") + "   (보스를 클릭하면 그 자리를 때림)", Rich(10));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("다시 시작")) { brain.Restart(); hitsTaken = 0; }
            if (GUILayout.Button("2페로")) brain.ForcePhase2();
            if (GUILayout.Button("3페로 (각성)")) brain.ForcePhase3();
            GUILayout.EndHorizontal();

            showPerf = GUILayout.Toggle(showPerf, " 성능 · 밀도");
            if (showPerf) PerfGUI();

            showPatterns = GUILayout.Toggle(showPatterns, " 패턴 직접 실행");
            if (showPatterns)
            {
                bool knight = brain.Phase == BossPhase.Phase3;
                var list = knight ? RSNanoBossBrain.KnightPatterns : RSNanoBossBrain.SwarmPatterns;
                GUILayout.Label(knight ? "3페 (기사)" : "1 · 2페 (군집)", Rich(11));
                foreach (var p in list)
                    if (GUILayout.Button(p)) brain.ForcePattern(p);
                if (brain.rigPatterns.Count > 0)
                {
                    GUILayout.Label("리그 패턴 (애니메이션)", Rich(11));
                    foreach (var a in brain.rigPatterns)
                        if (a != null && a.rig && a.clip && GUILayout.Button(a.Name)) brain.PlayRigPattern(a);
                }
            }
            GUILayout.EndArea();

            // 패널 밖 클릭 = 때리기
            if (e.type == EventType.MouseDown && e.button == 0 && !panel.Contains(e.mousePosition) && cam)
            {
                Vector2 sp = new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y);
                Ray ray = cam.ScreenPointToRay(sp);
                var plane = new Plane(Vector3.back, Vector3.zero);
                if (plane.Raycast(ray, out float dist))
                {
                    Vector3 hit = ray.GetPoint(dist);
                    brain.Hit(hit, clickRadius, (hit - brain.BodyCenter).normalized * 4f);
                    if (player) ShowTracer(player.position + Vector3.up * 1.2f, hit);
                }
                e.Use();
            }
        }

        void PerfGUI()
        {
            var rig = brain.ActiveRig;
            string col = swarm.SimMs <= 1f ? "#9be89b" : swarm.SimMs <= 2f ? "#ffdd66" : "#ff8877";
            GUILayout.Label($"FPS <b>{fps:0}</b>   시뮬 <b><color={col}>{swarm.SimMs:0.00}ms</color></b>   그리기 준비 {swarm.UploadMs:0.00}ms" +
                            (rig ? $"   리그 {rig.TickMs:0.00}ms" : ""), Rich(11));
            GUILayout.Label($"덩어리 격자 간격 {swarm.BlobSpace * 100f:0.0}cm (≈{swarm.BlobSpace / (swarm.botWorldSize / RSNanoBotSprites.Tile):0.0}px 판)", Rich(10));

            if (pendingCount < 0) pendingCount = swarm.botCount;
            GUILayout.BeginHorizontal();
            GUILayout.Label("봇 수", GUILayout.Width(60));
            pendingCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(pendingCount, 1000, 8000) / 250f) * 250;
            GUILayout.Label(pendingCount.ToString(), GUILayout.Width(40));
            using (new GUIEnabled(pendingCount != swarm.botCount))
                if (GUILayout.Button("적용", GUILayout.Width(44))) { swarm.botCount = pendingCount; brain.Restart(); hitsTaken = 0; }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("격자 간격", GUILayout.Width(60));
            swarm.spacing = GUILayout.HorizontalSlider(swarm.spacing, 0.06f, 0.2f);
            GUILayout.Label((swarm.spacing * 100f).ToString("0.0") + "cm", GUILayout.Width(44));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("픽셀 크기", GUILayout.Width(60));
            swarm.botWorldSize = GUILayout.HorizontalSlider(swarm.botWorldSize, 0.125f, 0.5f);
            GUILayout.Label((RSNanoBotSprites.Tile / swarm.botWorldSize).ToString("0") + "px/m", GUILayout.Width(52));
            GUILayout.EndHorizontal();
            GUILayout.Label("게임 기준 32px/m. 간격 ÷ 픽셀로 판 크기(3~6px)가 정해진다", Rich(10));

            GUILayout.BeginHorizontal();
            swarm.plates = GUILayout.Toggle(swarm.plates, " 판으로 그리기");
            swarm.castShadows = GUILayout.Toggle(swarm.castShadows, " 그림자");
            GUILayout.EndHorizontal();
        }

        struct GUIEnabled : System.IDisposable
        {
            readonly bool prev;
            public GUIEnabled(bool on) { prev = GUI.enabled; GUI.enabled = on; }
            public void Dispose() { GUI.enabled = prev; }
        }

        static string PhaseName(BossPhase p)
        {
            switch (p)
            {
                case BossPhase.Intro: return "등장";
                case BossPhase.Phase1: return "1페 (군집)";
                case BossPhase.Phase2: return "2페 (불안정)";
                case BossPhase.FakeDeath: return "…";
                case BossPhase.Awakening: return "각성";
                case BossPhase.Phase3: return "3페 (기사)";
                case BossPhase.Defeated: return "격파";
                default: return p.ToString();
            }
        }

        static GUIStyle rich;
        static GUIStyle Rich(int size)
        {
            rich ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            rich.fontSize = size;
            return rich;
        }
    }
}
