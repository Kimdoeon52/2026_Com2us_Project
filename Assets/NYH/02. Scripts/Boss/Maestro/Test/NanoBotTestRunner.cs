// 보스3 마에스트로(월광) — 나노봇 리그 패턴 시험 재생기 (테스트 전용)
//
// NanoBot 씬에서 "나노봇 엔진으로 패턴을 만들고 조절할 수 있는가"를 확인하려고 만든 것이다. 실제 보스 컨트롤러가 아니다.
//   · 시작하면 군집(RSNanoSwarm)을 덩어리로 만든다
//   · 버튼/키(Space)로 리그 클립을 재생한다 — 삭제된 RSNanoBossBrain.RunRig 와 같은 방식(PlayableGraph로 클립 직접 재생 + rig.Begin/End)
//   · 플레이어 더미: ← → 로 움직인다. 위험 봇에 닿으면 피격 횟수만 센다
//
// ⚠️ 시간은 Time.deltaTime 기준이다. 나노봇 엔진(KKH 폴더, 수정 불가)이 LateUpdate로 돌기 때문에 시험에서는 그대로 맞췄다.
//    실제 보스로 만들 때는 판정·패턴 타이밍을 CombatClock 틱에서 세야 한다 (CLAUDE_1.md §3).
// 입력은 IMGUI 이벤트만 쓴다 (프로젝트 Input System 설정과 무관하게 동작).
using RealSteel.NanoBoss;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[AddComponentMenu("NYH/보스3/나노봇 패턴 시험 재생기")]
public class NanoBotTestRunner : MonoBehaviour
{
    [Header("연결")]
    public RSNanoSwarm swarm;
    public RSNanoRig rig;
    [Tooltip("재생할 리그 클립 (리그 자식 경로와 맞아야 한다)")]
    public AnimationClip clip;
    [Tooltip("플레이어 더미 (발밑 기준)")]
    public Transform player;

    [Header("덩어리")]
    [Tooltip("가로 : 세로 : 깊이 비율. 실제 크기는 봇 수로 정해진다")]
    public Vector3 blobShape = new Vector3(3.4f, 2.7f, 0.9f);

    [Header("재생 (시험용 값)")]
    [Range(0.25f, 2f)] public float speed = 1f;
    [Tooltip("끝나면 잠깐 쉬고 다시 재생")]
    public bool loop;
    [Min(0f)] public float loopGap = 1f;

    [Header("플레이어 더미 (시험용 값)")]
    public float moveSpeed = 5f;
    public float hitRadius = 0.55f;
    public float invulnSeconds = 0.6f;

    PlayableGraph graph;
    float playTime, playEnd, gapTimer, invuln;
    bool playing;
    int moveDir, hitsTaken;
    Vector3 bodyCenter;

    void Start()
    {
        if (!swarm) swarm = GetComponentInChildren<RSNanoSwarm>();
        if (!rig) rig = GetComponentInChildren<RSNanoRig>();
        if (!swarm) { Debug.LogWarning("[나노봇 시험] 군집이 없습니다", this); enabled = false; return; }
        BuildBlob();
    }

    void BuildBlob()
    {
        float floor = swarm.transform.position.y;
        swarm.floorFromTransform = false;
        swarm.floorY = floor;
        swarm.Blob.aspect = blobShape.x / Mathf.Max(0.01f, blobShape.y);
        swarm.Blob.radius = swarm.BlobSize(swarm.botCount, blobShape);
        bodyCenter = new Vector3(swarm.transform.position.x, floor + swarm.Blob.radius.y, swarm.transform.position.z);
        swarm.Blob.center = bodyCenter;
        swarm.Build(bodyCenter);
        swarm.ResetBotLooks();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (player && moveDir != 0)
            player.position += Vector3.right * (moveDir * moveSpeed * dt);

        invuln -= dt;
        if (player && invuln <= 0f && swarm.DangerNear(player.position + Vector3.up * 0.9f, hitRadius) > 0)
        {
            hitsTaken++;
            invuln = invulnSeconds;
            Debug.Log($"[나노봇 시험] 피격 {hitsTaken}회 (패턴 {playTime:0.00}초)");
        }

        if (playing)
        {
            playTime += dt;
            if (playTime >= playEnd) Stop();
        }
        else if (loop)
        {
            gapTimer -= dt;
            if (gapTimer <= 0f) Play();
        }
    }

    public void Play()
    {
        if (!rig || !clip) { Debug.LogWarning("[나노봇 시험] 리그나 클립이 비었습니다", this); return; }
        Stop();
        var animator = rig.GetComponent<Animator>();
        if (!animator) animator = rig.gameObject.AddComponent<Animator>();
        animator.enabled = true;

        float face = player && player.position.x < bodyCenter.x ? -1f : 1f;
        var playable = AnimationPlayableUtilities.PlayClip(animator, clip, out graph);
        playable.SetSpeed(speed);
        graph.Evaluate(0f);   // 첫 프레임 모양으로 격자 자리를 만든다
        rig.Begin(RSNanoSwarm.G_Blob, PlayerFeet, face, bodyCenter);

        playTime = 0f;
        playEnd = clip.length / Mathf.Max(0.05f, speed);
        playing = true;
        Debug.Log($"[나노봇 시험] {clip.name} 재생 ({playEnd:0.00}초, 속도 {speed:0.00})");
    }

    public void Stop()
    {
        if (graph.IsValid()) graph.Destroy();
        if (rig && rig.Active) rig.End();
        if (playing) gapTimer = loopGap;
        playing = false;
    }

    Vector3 PlayerFeet() => player ? player.position : bodyCenter + Vector3.right * 7f;

    void OnDisable() => Stop();

    void OnGUI()
    {
        var e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.LeftArrow) moveDir = -1;
            else if (e.keyCode == KeyCode.RightArrow) moveDir = 1;
            else if (e.keyCode == KeyCode.Space) Play();
        }
        else if (e.type == EventType.KeyUp && (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow))
            moveDir = 0;

        GUILayout.BeginArea(new Rect(10, 10, 260, 220), GUI.skin.box);
        GUILayout.Label($"클립: {(clip ? clip.name : "(없음)")}");
        GUILayout.Label(playing ? $"재생 중 {playTime:0.00} / {playEnd:0.00}초" : "대기");
        if (swarm) GUILayout.Label($"봇 {swarm.Alive}/{swarm.Count}  시뮬 {swarm.SimMs:0.00}ms");
        GUILayout.Label($"피격 {hitsTaken}회");
        GUILayout.Label($"속도 {speed:0.00}");
        speed = GUILayout.HorizontalSlider(speed, 0.25f, 2f);
        loop = GUILayout.Toggle(loop, "반복 재생");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("재생 (Space)")) Play();
        if (GUILayout.Button("덩어리 다시")) { Stop(); BuildBlob(); }
        GUILayout.EndHorizontal();
        GUILayout.Label("← → 플레이어 더미 이동");
        GUILayout.EndArea();
    }
}
