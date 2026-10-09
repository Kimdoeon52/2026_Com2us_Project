using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RealSteel.Dialogue;

namespace RealSteel.Dialogue.Tests
{
    /// <summary>Game scope 값을 딕셔너리로 흉내내는 테스트용 프로바이더</summary>
    internal sealed class FakeGame : IGameStateProvider
    {
        public readonly Dictionary<string, DVal> Values = new Dictionary<string, DVal>();
        public bool TryGet(string key, out DVal value) => Values.TryGetValue(key, out value);
    }

    public class DialogueCoreTests
    {
        // ------------------------------------------------------------ helpers
        private static string DataRoot()
        {
#if UNITY_5_3_OR_NEWER
            return Path.Combine(UnityEngine.Application.dataPath, "LHM/03.Data/Dialogue");
#else
            var env = System.Environment.GetEnvironmentVariable("DIALOGUE_DATA_ROOT"); // Unity 밖(dotnet)에서 돌릴 때
            if (!string.IsNullOrEmpty(env)) return env;
            var dir = TestContext.CurrentContext.TestDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "Assets/Data/Dialogue"))) dir = Path.GetDirectoryName(dir);
            return Path.Combine(dir, "Assets/Data/Dialogue");
#endif
        }

        private static DialogueSourceSet LoadSampleSet()
        {
            string root = DataRoot();
            var set = new DialogueSourceSet
            {
                Variables = Src(Path.Combine(root, "variables.json")),
                Speakers = Src(Path.Combine(root, "speakers.json")),
            };
            foreach (var f in Directory.GetFiles(Path.Combine(root, "Pools"), "*.json").OrderBy(x => x))
                set.DialogueFiles.Add(Src(f));
            return set;
        }

        private static DialogueSource Src(string path) => new DialogueSource(Path.GetFileName(path), File.ReadAllText(path));

        private sealed class Rig
        {
            public DialogueDatabase Db;
            public ValidationReport Report;
            public DialogueState State;
            public FakeGame Game;
            public DialogueEngine Engine;
            public DialogueSettings Settings;
        }

        private static Rig MakeRig(DialogueSettings settings = null)
        {
            var report = new ValidationReport();
            var db = DialogueDatabase.Load(LoadSampleSet(), report);
            var state = new DialogueState(db.Variables);
            var game = new FakeGame();
            var resolver = new LocalizedTextResolver(db);
            resolver.SetTable(Newtonsoft.Json.JsonConvert.DeserializeObject<StringTableFile>(
                File.ReadAllText(Path.Combine(DataRoot(), "Strings/strings_ko.json")), DialogueJson.Strict));
            settings = settings ?? new DialogueSettings { openGuardSec = 0, confirmGuardSec = 0 };
            var engine = new DialogueEngine(db, state, settings, resolver, game, new SystemRandomSource(1234));
            return new Rig { Db = db, Report = report, State = state, Game = game, Engine = engine, Settings = settings };
        }

        /// <summary>Blocking 대화를 처음부터 끝까지 Confirm으로 진행</summary>
        private static string PlayThrough(Rig rig, string poolId, double now = 0, Dictionary<string, DVal> ev = null)
        {
            var r = rig.Engine.RequestBlocking(poolId, ev, now);
            if (r.Status == BlockingRequestStatus.SkippedByRepeatPolicy) return r.Entry.id + "(skipped)";
            Assert.AreEqual(BlockingRequestStatus.Started, r.Status, $"pool {poolId}");
            r.Session.Begin();
            int guard = 0;
            while (r.Session.State != SessionState.Finished && guard++ < 100) { r.Session.Confirm(); r.Session.Confirm(); }
            Assert.AreEqual(FinishReason.Completed, r.Session.Result);
            return r.Entry.id;
        }

        // ------------------------------------------------------------ 로드/검증
        [Test]
        public void SampleData_LoadsWithoutErrors()
        {
            var rig = MakeRig();
            TestContext.WriteLine(rig.Report.ToText());
            Assert.IsFalse(rig.Report.HasErrors, rig.Report.ToText());
            Assert.AreEqual(6, rig.Db.Pools.Count);
        }

        [Test]
        public void TypoField_IsRejected()
        {
            var set = LoadSampleSet();
            set.DialogueFiles = new List<DialogueSource> { new DialogueSource("typo.json",
                "{ \"schemaVersion\":1, \"pools\":[{ \"poolId\":\"t.p\", \"category\":\"Hub\", \"mode\":\"Blocking\", \"entries\":[ { \"id\":\"t.e\", \"prioirty\":5, \"lines\":[{\"speaker\":\"player\",\"text\":\"hi\"}] } ] }] }") };
            var report = new ValidationReport();
            var db = DialogueDatabase.Load(set, report);
            Assert.IsTrue(report.Issues.Any(i => i.Severity == IssueSeverity.Error && i.Message.Contains("prioirty")), report.ToText());
            Assert.AreEqual(0, db.Pools.Count);
        }

        [Test]
        public void UnknownKey_WrongType_GameScopeEffect_AreErrors()
        {
            var set = LoadSampleSet();
            set.DialogueFiles = new List<DialogueSource> { new DialogueSource("bad.json", @"{
              ""schemaVersion"":1, ""pools"":[{ ""poolId"":""t.p"", ""category"":""Hub"", ""mode"":""Blocking"", ""entries"":[
                { ""id"":""t.a"", ""conditions"": { ""key"":""record.winz"", ""op"":""gte"", ""value"":1 }, ""lines"":[{""speaker"":""player"",""text"":""a""}] },
                { ""id"":""t.b"", ""conditions"": { ""key"":""record.wins"", ""op"":""gte"", ""value"":""many"" }, ""lines"":[{""speaker"":""player"",""text"":""b""}] },
                { ""id"":""t.c"", ""effects"": [ { ""op"":""Add"", ""key"":""player.gold"", ""value"":99999 } ], ""lines"":[{""speaker"":""player"",""text"":""c""}] },
                { ""id"":""t.d"", ""conditions"": { ""key"":""battle.lastResult"", ""op"":""eq"", ""value"":""Lsoe"" }, ""lines"":[{""speaker"":""player"",""text"":""d""}] },
                { ""id"":""t.e"", ""band"":""Fallback"", ""lines"":[{""speaker"":""player"",""text"":""e""}] }
              ]}]}") };
            var report = new ValidationReport();
            DialogueDatabase.Load(set, report);
            var errors = report.Issues.Where(i => i.Severity == IssueSeverity.Error).ToList();
            Assert.IsTrue(errors.Any(i => i.Context == "t.a" && i.Message.Contains("record.winz")));
            Assert.IsTrue(errors.Any(i => i.Context == "t.b" && i.Message.Contains("타입")));
            Assert.IsTrue(errors.Any(i => i.Context == "t.c" && i.Message.Contains("Dialogue scope")));
            Assert.IsTrue(errors.Any(i => i.Context == "t.d" && i.Message.Contains("Lsoe")));
        }

        [Test]
        public void UnreachableEntry_IsWarned()
        {
            var set = LoadSampleSet();
            set.DialogueFiles = new List<DialogueSource> { new DialogueSource("unreach.json", @"{
              ""schemaVersion"":1, ""pools"":[{ ""poolId"":""t.p"", ""category"":""Hub"", ""mode"":""Blocking"", ""entries"":[
                { ""id"":""t.always"", ""band"":""Progress"", ""lines"":[{""speaker"":""player"",""text"":""a""}] },
                { ""id"":""t.never"",  ""band"":""Ambient"", ""conditions"": { ""key"":""record.wins"", ""op"":""gte"", ""value"":1 }, ""lines"":[{""speaker"":""player"",""text"":""b""}] }
              ]}]}") };
            var report = new ValidationReport();
            DialogueDatabase.Load(set, report);
            Assert.IsTrue(report.Issues.Any(i => i.Context == "t.never" && i.Message.StartsWith("도달 불가")), report.ToText());
        }

        // ------------------------------------------------------------ 선택 규칙
        [Test]
        public void Hub_FirstMeet_ThenSpecificLoss_ThenFallbackRotation()
        {
            var rig = MakeRig();

            Assert.AreEqual("junk.first_meet", PlayThrough(rig, "hub.junkshop.talk"));
            Assert.AreEqual(1, rig.State.TryGetVar("dlg.junkTrust", out var trust) ? trust.AsNumber : -1);

            // 바퀴 보스에게 패배 → 일반 패배보다 구체적인 대사
            rig.Game.Values["battle.count"] = DVal.Int(1);
            rig.Game.Values["battle.lastResult"] = DVal.Str("Lose");
            rig.Game.Values["battle.lastOpponent"] = DVal.Str("boss_wheel01");
            Assert.AreEqual("junk.loss.boss_wheel01", PlayThrough(rig, "hub.junkshop.talk"));

            // 기본 파츠 + 다른 상대에게 패배 → basic_parts (같은 Reactive/10, 조건 2개)
            rig.Game.Values["battle.count"] = DVal.Int(2);
            rig.Game.Values["battle.lastOpponent"] = DVal.Str("other");
            rig.Game.Values["player.usingBasicParts"] = DVal.Bool(true);
            Assert.AreEqual("junk.loss.basic_parts", PlayThrough(rig, "hub.junkshop.talk"));

            // 결과 소비 후 → Fallback 2종이 번갈아
            rig.Game.Values["battle.lastResult"] = DVal.Str("None");
            var a = PlayThrough(rig, "hub.junkshop.talk");
            var b = PlayThrough(rig, "hub.junkshop.talk");
            var c = PlayThrough(rig, "hub.junkshop.talk");
            CollectionAssert.AreEquivalent(new[] { "junk.idle.a", "junk.idle.b" }, new[] { a, b });
            Assert.AreEqual(a, c, "가장 덜/오래전에 본 대사가 우선");
        }

        [Test]
        public void OncePer_ReactionConsumedPerBattle()
        {
            var rig = MakeRig();
            PlayThrough(rig, "hub.junkshop.talk"); // first_meet
            rig.Game.Values["battle.count"] = DVal.Int(1);
            rig.Game.Values["battle.lastResult"] = DVal.Str("Lose");

            Assert.AreEqual("junk.loss.generic", PlayThrough(rig, "hub.junkshop.talk"));
            // 같은 경기 결과로 다시 말 걸면 반응이 반복되지 않음
            StringAssertNotStartsWith("junk.loss", PlayThrough(rig, "hub.junkshop.talk"));
            // 다음 경기에서 또 지면 다시 반응
            rig.Game.Values["battle.count"] = DVal.Int(2);
            Assert.AreEqual("junk.loss.generic", PlayThrough(rig, "hub.junkshop.talk"));
            Assert.AreEqual(2L, rig.State.GetOncePerMark("junk.loss.generic"));
        }

        private static void StringAssertNotStartsWith(string prefix, string actual) =>
            Assert.IsFalse(actual.StartsWith(prefix), $"'{actual}'는 '{prefix}'로 시작하면 안 됨");

        [Test]
        public void Save_OldV2WithoutNewField_StillLoads()
        {
            var r = DialogueSaveSerializer.Deserialize("{\"version\":2,\"sequence\":3,\"entries\":{\"junk.loss.generic\":{\"plays\":1,\"lastSeq\":3}},\"seenLines\":[],\"vars\":{},\"futureField\":123}");
            Assert.AreEqual(SaveLoadStatus.Ok, r.Status);
            Assert.IsNull(r.Data.entries["junk.loss.generic"].oncePerMark);
        }

        [Test]
        public void SeenCondition_And_Progress()
        {
            var rig = MakeRig();
            rig.Game.Values["record.wins"] = DVal.Int(5);
            // first_meet(Story)이 먼저 나오고, 그 다음에야 wins5(seen 조건) 가능
            Assert.AreEqual("junk.first_meet", PlayThrough(rig, "hub.junkshop.talk"));
            Assert.AreEqual("junk.wins5", PlayThrough(rig, "hub.junkshop.talk"));
            Assert.AreNotEqual("junk.wins5", PlayThrough(rig, "hub.junkshop.talk"));
            Assert.AreEqual(2, (int)(rig.State.TryGetVar("dlg.junkTrust", out var t) ? t.AsNumber : 0));
        }

        [Test]
        public void GameProviderWrongType_FallsBackToDefault()
        {
            var rig = MakeRig();
            rig.Game.Values["record.wins"] = DVal.Str("5"); // 잘못된 타입
            var ctx = rig.Engine.CreateContext(null);
            Assert.IsTrue(ctx.TryGet("record.wins", out var v));
            Assert.AreEqual(VarType.Int, v.Type);
            Assert.AreEqual(0, v.AsNumber);
        }

        [Test]
        public void MainStory_EffectsAdvanceChapter_EvenWhenSkipped()
        {
            var rig = MakeRig();
            var r = rig.Engine.RequestBlocking("story.scrapyard.enter", null, 0);
            Assert.AreEqual("story.prologue", r.Entry.id);
            r.Session.Begin();
            Assert.IsFalse(r.Session.TrySkipAll(), "안 읽은 대사가 있으면 확인 필요");
            r.Session.ForceSkipAll();
            Assert.AreEqual(FinishReason.Skipped, r.Session.Result);
            Assert.IsTrue(rig.State.TryGetVar("story.chapter", out var ch));
            Assert.AreEqual(1, ch.AsNumber);
            Assert.IsNull(rig.Engine.ActiveSession);
        }

        [Test]
        public void Abort_DoesNotCommit()
        {
            var rig = MakeRig();
            var r = rig.Engine.RequestBlocking("story.scrapyard.enter", null, 0);
            r.Session.Begin();
            r.Session.Abort();
            Assert.IsFalse(rig.State.HasSeen("story.prologue"));
            Assert.AreEqual("story.prologue", rig.Engine.RequestBlocking("story.scrapyard.enter", null, 1).Entry.id);
        }

        [Test]
        public void Busy_WhileSessionActive()
        {
            var rig = MakeRig();
            var r = rig.Engine.RequestBlocking("hub.junkshop.talk", null, 0);
            r.Session.Begin();
            Assert.AreEqual(BlockingRequestStatus.Busy, rig.Engine.RequestBlocking("hub.auction.talk", null, 0).Status);
        }

        [Test]
        public void BossIntro_ShortOnRepeat_AndOneTimeHint()
        {
            var rig = MakeRig();
            var r1 = rig.Engine.RequestBlocking("boss.wheel01.intro", null, 0);
            Assert.IsFalse(r1.Session.UsingShortLines);
            Assert.AreEqual(3, r1.Session.LineCount);
            r1.Session.Begin(); r1.Session.ForceSkipAll();

            var r2 = rig.Engine.RequestBlocking("boss.wheel01.intro", null, 1);
            Assert.IsTrue(r2.Session.UsingShortLines);
            Assert.AreEqual(1, r2.Session.LineCount);
            r2.Session.Begin(); r2.Session.ForceSkipAll();

            rig.Game.Values["boss.attemptCount"] = DVal.Int(3);
            Assert.AreEqual("boss.wheel01.hint", PlayThrough(rig, "boss.wheel01.intro", 2));
            Assert.AreEqual("boss.wheel01.intro", rig.Engine.RequestBlocking("boss.wheel01.intro", null, 3).Entry.id);
        }

        [Test]
        public void PartBreak_Bark_RoutingAndCooldown()
        {
            var rig = MakeRig();
            var ev = new Dictionary<string, DVal> { ["event.partBroken"] = DVal.Str("LeftArm") };
            Assert.AreEqual("bark.part.arm", rig.Engine.RequestInstant("combat.part_break", ev, 0).id);
            // 쿨다운(4초) 중 → 같은 부위 재파괴 시 Fallback
            Assert.AreEqual("bark.part.generic", rig.Engine.RequestInstant("combat.part_break", ev, 1).id);
            // 둘 다 쿨다운 → 침묵
            Assert.IsNull(rig.Engine.RequestInstant("combat.part_break", ev, 2));
            Assert.AreEqual("bark.part.arm", rig.Engine.RequestInstant("combat.part_break", ev, 5).id);

            rig.Game.Values["player.brokenPartCount"] = DVal.Int(4);
            ev["event.partBroken"] = DVal.Str("Head");
            Assert.AreEqual("bark.part.last_standing", rig.Engine.RequestInstant("combat.part_break", ev, 20).id);
        }

        [Test]
        public void Bark_CannotBeRequestedAsBlocking()
        {
            var rig = MakeRig();
            Assert.AreEqual(BlockingRequestStatus.WrongMode, rig.Engine.RequestBlocking("combat.part_break", null, 0).Status);
            Assert.AreEqual(BlockingRequestStatus.UnknownPool, rig.Engine.RequestBlocking("nope.pool", null, 0).Status);
        }

        [Test]
        public void Crowd_ReactiveBeatsIdle_IdleVaries()
        {
            var rig = MakeRig();
            var ev = new Dictionary<string, DVal> { ["event.crowdMoment"] = DVal.Str("BossStunned") };
            Assert.AreEqual("crowd.stun", rig.Engine.RequestInstant("crowd.arena01", ev, 0).id);

            ev["event.crowdMoment"] = DVal.Str("Idle");
            var seen = new HashSet<string>();
            for (int i = 0; i < 3; i++) seen.Add(rig.Engine.RequestInstant("crowd.arena01", ev, 10 + i).id);
            Assert.AreEqual(3, seen.Count, "Idle 3종이 쿨다운으로 순환");
        }

        // ------------------------------------------------------------ 세션/편의 기능
        [Test]
        public void Session_Typewriter_Confirm_FastForward_StopsAtUnread()
        {
            var settings = new DialogueSettings { openGuardSec = 0.25f, confirmGuardSec = 0.1f, charsPerSecond = 10 };
            var rig = MakeRig(settings);
            var r = rig.Engine.RequestBlocking("boss.wheel01.intro", null, 0);
            var s = r.Session;
            s.Begin();
            Assert.AreEqual(SessionState.Revealing, s.State);

            s.Confirm(); // open guard 중 → 무시
            Assert.AreEqual(SessionState.Revealing, s.State);

            s.Tick(0.3f);
            Assert.AreEqual(3, s.VisibleChars);
            s.Confirm(); // 타이핑 즉시 완성
            Assert.AreEqual(SessionState.WaitingForInput, s.State);
            Assert.AreEqual(s.TotalChars, s.VisibleChars);
            s.Confirm(); // 다음 줄
            Assert.AreEqual(1, s.LineIndex);

            // 안 읽은 줄 → 빨리감기 불가
            s.FastForwardHeld = true;
            s.Tick(0.05f);
            Assert.AreEqual(SessionState.Revealing, s.State);
            Assert.AreEqual(1, s.LineIndex);

            // 다 읽고 다시 보면 빨리감기 가능 (ShortOnRepeat 회피 위해 Full 재생 엔트리 사용)
            s.FastForwardHeld = false;
            s.ForceSkipAll();
        }

        [Test]
        public void Session_FastForward_OnSeenLines_And_AutoMode()
        {
            var settings = new DialogueSettings { openGuardSec = 0, confirmGuardSec = 0, charsPerSecond = 10, fastForwardInterval = 0.05f, autoDelayBase = 1, autoDelayPerChar = 0 };
            var rig = MakeRig(settings);
            var r = rig.Engine.RequestBlocking("hub.junkshop.talk", null, 0); // first_meet 2줄
            r.Session.Begin();
            while (r.Session.State != SessionState.Finished) { r.Session.Confirm(); r.Session.Confirm(); }

            // 같은 줄들을 다시 보는 상황을 만들기 위해 세이브의 엔트리 기록만 지움(읽음 표시는 유지)
            rig.State.Data.entries.Remove("junk.first_meet");
            var r2 = rig.Engine.RequestBlocking("hub.junkshop.talk", null, 1);
            Assert.AreEqual("junk.first_meet", r2.Entry.id);
            var s = r2.Session;
            s.Begin();
            Assert.IsTrue(s.IsCurrentLineSeen);
            Assert.IsTrue(s.TrySkipAll(), "모두 읽은 대사는 확인 없이 스킵");

            // 오토 모드
            rig.State.Data.entries.Remove("junk.first_meet");
            var s3 = rig.Engine.RequestBlocking("hub.junkshop.talk", null, 2).Session;
            s3.AutoMode = true;
            s3.Begin();
            for (int i = 0; i < 200 && s3.State != SessionState.Finished; i++) s3.Tick(0.1f);
            Assert.AreEqual(FinishReason.Completed, s3.Result);

            // 빨리감기
            rig.State.Data.entries.Remove("junk.first_meet");
            var s4 = rig.Engine.RequestBlocking("hub.junkshop.talk", null, 3).Session;
            s4.FastForwardHeld = true;
            s4.Begin();
            for (int i = 0; i < 10 && s4.State != SessionState.Finished; i++) s4.Tick(0.05f);
            Assert.AreEqual(FinishReason.Completed, s4.Result);
        }

        [Test]
        public void Backlog_RingBuffer_KeepsNewest()
        {
            var log = new DialogueBacklog(3);
            for (int i = 0; i < 5; i++) log.Add(new BacklogItem { Text = i.ToString() });
            Assert.AreEqual(3, log.Count);
            Assert.AreEqual("2", log[0].Text);
            Assert.AreEqual("4", log[2].Text);
        }

        [Test]
        public void RichText_VisibleLength_IgnoresTags()
        {
            Assert.AreEqual(5, DialogueSession.VisibleLength("<b>코어</b>만큼은"));
        }

        // ------------------------------------------------------------ 세이브 호환성
        [Test]
        public void Save_RoundTrip()
        {
            var rig = MakeRig();
            PlayThrough(rig, "hub.junkshop.talk");
            var json = DialogueSaveSerializer.Serialize(rig.State.Data);
            var loaded = DialogueSaveSerializer.Deserialize(json);
            Assert.AreEqual(SaveLoadStatus.Ok, loaded.Status);
            Assert.AreEqual(1, loaded.Data.entries["junk.first_meet"].plays);
            Assert.IsTrue(loaded.Data.seenLines.Contains("junk.first_meet#0"));
            var st = new DialogueState(rig.Db.Variables, loaded.Data);
            Assert.IsTrue(st.TryGetVar("dlg.junkTrust", out var t) && t.AsNumber == 1);
        }

        [Test]
        public void Save_MigratesV1()
        {
            var r = DialogueSaveSerializer.Deserialize("{ \"seen\": [\"junk.first_meet\", \"story.prologue\"], \"flags\": { \"story.chapter\": 1 } }");
            Assert.AreEqual(SaveLoadStatus.Migrated, r.Status);
            Assert.AreEqual(1, r.FromVersion);
            Assert.AreEqual(1, r.Data.entries["story.prologue"].plays);
            var st = new DialogueState(MakeRig().Db.Variables, r.Data);
            Assert.IsTrue(st.TryGetVar("story.chapter", out var ch) && ch.AsNumber == 1);
        }

        [Test]
        public void Save_NewerVersion_And_Corrupt_AreReported()
        {
            Assert.AreEqual(SaveLoadStatus.NewerVersion, DialogueSaveSerializer.Deserialize("{ \"version\": 99 }").Status);
            Assert.AreEqual(SaveLoadStatus.Corrupt, DialogueSaveSerializer.Deserialize("{ \"version\": 2, ").Status);
            Assert.AreEqual(SaveLoadStatus.Ok, DialogueSaveSerializer.Deserialize("").Status);
        }

        [Test]
        public void Save_FormerIds_RemapAndOrphansKept()
        {
            var rig = MakeRig();
            rig.Db.Entries["junk.idle.a"].formerIds.Add("junk.idle_old");
            rig.State.Data.entries["junk.idle_old"] = new EntryRecord { plays = 3, lastSeq = 7 };
            rig.State.Data.entries["deleted.entry"] = new EntryRecord { plays = 1, lastSeq = 1 };

            Assert.AreEqual(1, rig.State.ApplyIdRemap(rig.Db));
            Assert.AreEqual(3, rig.State.GetPlays("junk.idle.a"));
            Assert.IsFalse(rig.State.Data.entries.ContainsKey("junk.idle_old"));
            CollectionAssert.Contains(rig.State.OrphanIds, "deleted.entry");
            Assert.IsTrue(rig.State.Data.entries.ContainsKey("deleted.entry"), "orphan 기록은 지우지 않음");
        }

        [Test]
        public void Save_VarWithChangedType_FallsBackToDefault()
        {
            var rig = MakeRig();
            rig.State.Data.vars["dlg.junkTrust"] = new Newtonsoft.Json.Linq.JValue("high"); // 예전엔 문자열이었다고 가정
            var ctx = rig.Engine.CreateContext(null);
            Assert.IsTrue(ctx.TryGet("dlg.junkTrust", out var v));
            Assert.AreEqual(0, v.AsNumber);
        }
    }
}
