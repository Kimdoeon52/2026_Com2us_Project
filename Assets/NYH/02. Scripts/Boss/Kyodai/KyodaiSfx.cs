using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 쿄다이 효과음 재생기 (2026-11-05). 쿄다이 컨트롤러가 시작할 때 Bind로 효과음 묶음(KyodaiSfxData)을 걸고,
/// 손·레이저·기물·촉수 코드가 사건이 일어나는 순간 Play / Loop를 부른다. 플레이 중에만 소리가 난다(에디터 미리보기는 조용히).
///
/// 소리는 2D(거리 감쇠 없음) — 화면 안의 위치에 따라 좌우로만 나눈다. 재생용 AudioSource는 처음 쓸 때 한 오브젝트에 모아 만든다
/// (씬에 AudioListener가 없으면 그 오브젝트에 같이 붙인다).
/// 반복 소리는 Loop가 돌려준 핸들(AudioSource)을 들고 있다가 Stop으로 끈다.
/// </summary>
public static class KyodaiSfx
{
    private const int OneShotVoices = 16;

    private static KyodaiSfxData data;
    private static GameObject host;
    private static readonly List<AudioSource> voices = new List<AudioSource>();
    private static int nextVoice;

    /// <summary>지금 걸린 효과음 묶음 (없으면 null — 그러면 아무 소리도 안 난다)</summary>
    public static KyodaiSfxData D => data;

    public static void Bind(KyodaiSfxData sfx) => data = sfx;

    /// <summary>한 번 재생. pos = 소리 나는 자리(좌우 나누기용), volume = 이 재생만의 크기 배율</summary>
    public static void Play(KyodaiSfxCue cue, Vector3 pos, float pitch = 1f, float volume = 1f)
    {
        if (!Ready(cue)) return;
        AudioSource s = NextVoice();
        Setup(s, cue, pos, pitch);
        s.volume *= volume;
        s.loop = false;
        s.Play();
    }

    /// <summary>반복 재생 시작 — 돌려준 핸들로 Stop / SetPitch / Move. 칸이 비었으면 null</summary>
    public static AudioSource Loop(KyodaiSfxCue cue, Vector3 pos, float pitch = 1f)
    {
        if (!Ready(cue)) return null;
        EnsureHost();
        var s = host.AddComponent<AudioSource>();
        Setup(s, cue, pos, pitch);
        s.loop = true;
        s.time = Random.Range(0f, s.clip.length * 0.9f); // 매번 같은 자리부터 안 들리게
        s.Play();
        return s;
    }

    public static void Stop(ref AudioSource loop)
    {
        if (loop != null) Object.Destroy(loop);
        loop = null;
    }

    /// <summary>반복 소리를 움직이는 대상에 맞춰 좌우로</summary>
    public static void Move(AudioSource loop, Vector3 pos)
    {
        if (loop != null && data != null) loop.panStereo = Pan(pos);
    }

    // ───────── 배경음악 (2026-11-16 임시) ─────────
    // 보스전 시작에 페이드인, 보스를 잡으면 끊는다. 크기는 KyodaiSfxData.bgmVolume을 매 프레임 읽어서 플레이 중에 바꿔도 바로 반영.
    // 페이드는 실제 시간(일시정지·히트스톱과 무관) — 컨트롤러 Update가 TickBgm을 부른다
    private static AudioSource bgmSource;
    private static float bgmLevel;      // 0 ~ 1 (페이드 진행)
    private static float bgmTarget;     // 페이드 목표 (1 = 켜짐, 0 = 꺼짐)
    private static float bgmSeconds;    // 지금 페이드에 걸리는 시간

    /// <summary>배경음악 시작 — 0에서 bgmFadeInSeconds 동안 커진다. 이미 나오고 있으면 그대로</summary>
    public static void StartBgm()
    {
        if (!Application.isPlaying || data == null || data.bgm == null) return;
        EnsureHost();
        if (bgmSource == null)
        {
            bgmSource = host.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.spatialBlend = 0f;
            bgmSource.playOnAwake = false;
        }
        if (bgmSource.clip != data.bgm || !bgmSource.isPlaying)
        {
            bgmSource.clip = data.bgm;
            bgmLevel = 0f;
            bgmSource.volume = 0f;
            bgmSource.Play();
        }
        bgmTarget = 1f;
        bgmSeconds = data.bgmFadeInSeconds;
    }

    /// <summary>배경음악 끄기 — bgmStopSeconds 동안 줄어들고 멈춘다(0이면 바로)</summary>
    public static void StopBgm()
    {
        if (bgmSource == null) return;
        bgmTarget = 0f;
        bgmSeconds = data != null ? data.bgmStopSeconds : 0f;
        if (bgmSeconds <= 0f) Silence();
    }

    /// <summary>호출: KyodaiBossController.Update (실제 시간)</summary>
    public static void TickBgm(float unscaledDt)
    {
        if (bgmSource == null || !bgmSource.isPlaying) return;
        bgmLevel = bgmSeconds <= 0f ? bgmTarget : Mathf.MoveTowards(bgmLevel, bgmTarget, unscaledDt / bgmSeconds);
        if (bgmTarget <= 0f && bgmLevel <= 0f)
        {
            Silence();
            return;
        }
        float volume = data != null ? data.bgmVolume : 0f;
        bgmSource.volume = volume * bgmLevel * bgmLevel; // 제곱 — 귀에는 직선보다 자연스럽게 커지고 줄어든다
    }

    private static void Silence()
    {
        bgmLevel = 0f;
        if (bgmSource == null) return;
        bgmSource.volume = 0f;
        bgmSource.Stop();
    }

    private static bool Ready(KyodaiSfxCue cue) => Application.isPlaying && data != null && cue != null && !cue.IsEmpty;

    private static void Setup(AudioSource s, KyodaiSfxCue cue, Vector3 pos, float pitch)
    {
        s.clip = cue.clips[Random.Range(0, cue.clips.Length)];
        s.volume = cue.volume * data.masterVolume;
        s.pitch = pitch * (1f + Random.Range(-cue.pitchJitter, cue.pitchJitter));
        s.panStereo = Pan(pos);
        s.spatialBlend = 0f;
        s.playOnAwake = false;
    }

    // 화면 가운데 0, 양 끝 ±stereoSpread
    private static float Pan(Vector3 pos)
    {
        Camera cam = Camera.main;
        if (cam == null) return 0f;
        Vector3 v = cam.WorldToViewportPoint(pos);
        return Mathf.Clamp((v.x - 0.5f) * 2f, -1f, 1f) * data.stereoSpread;
    }

    private static AudioSource NextVoice()
    {
        EnsureHost();
        voices.RemoveAll(v => v == null);
        while (voices.Count < OneShotVoices) voices.Add(host.AddComponent<AudioSource>());
        // 쉬고 있는 목소리 먼저, 다 바쁘면 가장 오래된 것부터 덮는다
        for (int i = 0; i < voices.Count; i++)
        {
            AudioSource v = voices[(nextVoice + i) % voices.Count];
            if (!v.isPlaying) { nextVoice = (nextVoice + i + 1) % voices.Count; return v; }
        }
        AudioSource oldest = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Count;
        return oldest;
    }

    private static void EnsureHost()
    {
        if (host != null) return;
        host = new GameObject("쿄다이 효과음");
        // 전투 씬(Battle3·Battle_Boss_Test)에 AudioListener가 없다 — 없으면 아무것도 안 들리니 여기 붙인다(소리가 전부 2D라 위치 무관)
        if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
        voices.Clear();
        nextVoice = 0;
    }
}
