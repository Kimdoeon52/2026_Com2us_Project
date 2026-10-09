using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RealSteel.Dialogue
{
    // =====================================================================
    //  JSON 데이터 모델 (Assets/Data/Dialogue/**/*.json 과 1:1 대응)
    //  - 모든 필드는 public 필드 + Newtonsoft 직렬화
    //  - JSON에 모르는 키가 있으면 로드 에러(MissingMemberHandling.Error) → 오타 즉시 발견
    // =====================================================================

    /// <summary>대사가 쓰이는 상황. 카테고리별로 연출 방식/검증 규칙이 달라진다.</summary>
    public enum DialogueCategory { MainStory, Hub, PartBreak, BossIntro, Crowd }

    /// <summary>
    /// Blocking : 입력을 잠그고 대화창 표시 (메인 스토리, 허브 NPC, 보스 진입)
    /// Bark     : 입력 잠금 없이 자막/머리 위 한 줄 (전투 중 파츠 파괴)
    /// Bubble   : 입력 잠금 없이 월드 말풍선, 무시해도 되는 비강제 대사 (관중)
    /// </summary>
    public enum PresentationMode { Blocking, Bark, Bubble }

    /// <summary>
    /// 우선순위 표준 밴드. 기획자는 숫자를 직접 고르지 않고 "의미"로 밴드를 고른다.
    /// 같은 밴드 안에서의 미세 조정만 subPriority(0~99)로 한다.
    /// </summary>
    public enum PriorityBand
    {
        Fallback = 0,     // 조건 없는 기본/반복 대사. 풀마다 최소 1개 권장 (NPC 침묵 방지)
        Ambient = 200,    // 상황 무관 잡담, 관중 함성
        Progress = 400,   // 누적 진행 반응 (n승 달성, 챕터 진입 후 첫 방문 등)
        Reactive = 600,   // 방금 일어난 일 반응 (직전 패배, 파츠 파괴, 경매 정산 결과)
        Story = 800,      // 메인 스토리 진행 대사
        Critical = 1000   // 반드시 먼저 나와야 하는 대사 (튜토리얼, 챕터 전환) - MainStory/BossIntro 전용
    }

    /// <summary>같은 대사를 다시 볼 때의 정책 (보스 재도전 피로도 대응)</summary>
    public enum RepeatPolicy { Full, ShortOnRepeat, SkipOnRepeat }

    public sealed class DialogueFile
    {
        public int schemaVersion;
        public List<DialoguePool> pools = new List<DialoguePool>();
    }

    /// <summary>
    /// 풀 = "트리거 지점" 하나. 게임 코드는 풀 ID만 알고, 어떤 대사가 나올지는 데이터가 결정한다.
    /// 예) hub.junkshop.talk, boss.wheel01.intro, combat.partBreak, crowd.arena01
    /// </summary>
    public sealed class DialoguePool
    {
        public string poolId;
        public DialogueCategory category;
        public PresentationMode mode;
        public string desc;
        public List<DialogueEntry> entries = new List<DialogueEntry>();

        [JsonIgnore] public string SourceFile { get; internal set; }
    }

    /// <summary>엔트리 = 조건을 만족하면 재생되는 대사 묶음 1개</summary>
    public sealed class DialogueEntry
    {
        /// <summary>전역 고유 문자열 ID. 세이브는 이 ID로만 기록한다(인덱스 금지).</summary>
        public string id;

        /// <summary>ID를 바꿨을 때 옛 ID를 적어두면 세이브 기록이 자동 이관된다.</summary>
        public List<string> formerIds = new List<string>();

        public PriorityBand band = PriorityBand.Ambient;

        /// <summary>같은 밴드 내 미세 조정 0~99. 밴드를 넘나드는 값은 금지.</summary>
        public int subPriority;

        /// <summary>null이면 항상 참</summary>
        public ConditionNode conditions;

        /// <summary>true면 평생 1회만 재생</summary>
        public bool once;

        /// <summary>0 = 무제한</summary>
        public int maxPlays;

        /// <summary>
        /// "에포크당 1회" 소비 규칙. Int 변수 키를 지정하면 그 값이 바뀔 때까지 다시 재생되지 않는다.
        /// 예) "battle.count" → 경기 1회당 한 번만 패배 반응 (허브를 들락날락해도 반복되지 않음)
        /// </summary>
        public string oncePer;

        /// <summary>재생 후 다시 후보가 되기까지의 시간(초, 세션 한정). 바크/말풍선 반복 방지용</summary>
        public float cooldownSec;

        public RepeatPolicy repeatPolicy = RepeatPolicy.Full;

        public List<DialogueLine> lines = new List<DialogueLine>();

        /// <summary>repeatPolicy = ShortOnRepeat일 때 두 번째부터 재생할 축약본</summary>
        public List<DialogueLine> shortLines = new List<DialogueLine>();

        /// <summary>재생 완료(스킵 포함) 시 적용. 대화 소유 변수(dlg./story.)만 쓸 수 있다.</summary>
        public List<DialogueEffect> effects = new List<DialogueEffect>();

        public string note; // 기획 메모 (런타임 미사용)

        [JsonIgnore] public DialoguePool Pool { get; internal set; }
        [JsonIgnore] public int Specificity { get; internal set; }
    }

    public sealed class DialogueLine
    {
        /// <summary>
        /// 읽음 표시(읽은 대사만 스킵)용 ID. 비우면 "{entryId}#{index}"가 자동 부여된다.
        /// 메인 스토리는 대사 삽입 시 읽음 기록이 밀리지 않도록 직접 지정할 것.
        /// </summary>
        public string lineId;
        public string speaker;
        public string portrait;
        /// <summary>문자열 테이블 키 (strings_ko.json 등)</summary>
        public string textKey;
        /// <summary>개발용 임시 텍스트. textKey가 테이블에 없을 때만 사용된다.</summary>
        public string text;
        public string voice;
        /// <summary>오토 모드 대기시간 덮어쓰기(초). 0이면 기본 공식 사용</summary>
        public float autoDelay;

        [JsonIgnore] public string ResolvedLineId { get; internal set; }
    }

    public enum EffectOp { Set, Add }

    public sealed class DialogueEffect
    {
        public EffectOp op;
        public string key;
        public JToken value;

        [JsonIgnore] internal DVal Compiled;
    }

    // ---------------------------------------------------------------------
    //  화자 / 문자열 테이블
    // ---------------------------------------------------------------------

    public sealed class SpeakerFile
    {
        public int schemaVersion;
        public List<SpeakerDef> speakers = new List<SpeakerDef>();
    }

    public sealed class SpeakerDef
    {
        public string id;
        public string nameKey;
        public List<string> portraits = new List<string>();
    }

    public sealed class StringTableFile
    {
        public int schemaVersion;
        public string lang;
        public Dictionary<string, string> strings = new Dictionary<string, string>();
    }
}
