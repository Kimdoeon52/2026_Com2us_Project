using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RealSteel.Dialogue.EditorTools;
using UnityEditor;
using UnityEngine;

namespace RealSteel.Tutorial.EditorTools
{
    // 세분화 튜토리얼 9종 + 카탈로그(Resources)를 만듦. 이미 있는 튜토리얼 파일은 덮어쓰지 않음 (직접 고친 내용 보존)
    // 카탈로그가 Resources에 있으면 TutorialDirector가 게임 시작 때 스스로 생겨서 모든 씬에서 조건을 감시함 (씬 배치 필요 없음)
    public static class TutorialCatalogSetup
    {
        private const string Title = "세분화 튜토리얼";
        private const string TopicFolder = "Assets/LHM/03.Data/Tutorial/Topics";
        internal const string CatalogPath = "Assets/LHM/Resources/RealSteel/TutorialCatalog.asset";

        private sealed class Topic
        {
            public string Id;
            public string Name;
            public TutorialTrigger Trigger;
            public string Target;
            public string Intro;
            public string When;
            public (string title, string body)[] Steps;
        }

        // 기획서 xxi 노출 지점(T번호)에 맞춘 기본값. 순서 = 동시에 걸렸을 때 나오는 순서 = 도움말 순서
        private static readonly Topic[] Topics =
        {
            new Topic
            {
                Id = "controls", Name = "조작키", Trigger = TutorialTrigger.ComponentInScene, Target = "RSSimpleCharacter",
                Intro = "tutorial_t1_controls", When = "플레이어가 있는 필드 씬 첫 입장 (T1)",
                Steps = new[]
                {
                    ("이동", "WASD로 움직이고, Shift를 누르고 있으면 달립니다. Space로 점프합니다."),
                    ("상호작용", "사람이나 물건 가까이에서 [E] 안내가 뜨면 E를 눌러 말을 걸거나 살펴봅니다."),
                    ("카메라", "마우스 오른쪽 드래그로 카메라를 돌리고, 휠로 확대 · 축소합니다. Shift + Home은 처음 각도."),
                    ("도움말", "본 안내는 F10 도움말에서 언제든 다시 볼 수 있습니다."),
                },
            },
            new Topic
            {
                Id = "junkshop", Name = "고물상", Trigger = TutorialTrigger.SceneName, Target = "JunkShop",
                Intro = "tutorial_t4_junkshop", When = "고물상 씬 첫 입장 (T4)",
                Steps = new[]
                {
                    ("고철 찾기", "고철 더미에 가까워지면 소리가 커지고 물결이 보입니다."),
                    ("채집", "E를 눌러 채집합니다. 미니게임에 성공하면 고철을 얻습니다."),
                    ("판매", "모은 고철은 고물상 아저씨에게 팔 수 있습니다."),
                },
            },
            new Topic
            {
                Id = "inventory", Name = "인벤토리", Trigger = TutorialTrigger.WindowOpened, Target = "InventoryToggle.IsOpen",
                Intro = "tutorial_t6_inventory", When = "인벤토리 첫 열기 (T6)",
                Steps = new[]
                {
                    ("가방", "주운 고철 · 파츠 · 부품이 칸에 들어갑니다. 칸 수는 정해져 있습니다."),
                    ("옮기기", "아이템을 끌어서 옮기거나 장비창에 장착합니다."),
                },
            },
            new Topic
            {
                Id = "crafting", Name = "제작", Trigger = TutorialTrigger.WindowOpened, Target = "CraftingUIManager.craftingWindow",
                Intro = "tutorial_t7_crafting", When = "제작창 첫 열기 (T7)",
                Steps = new[]
                {
                    ("부위 고르기", "위쪽 탭에서 만들 부위(팔 · 다리 · 머리)를 고릅니다."),
                    ("조합", "재료를 조합 칸에 올리면, 맞는 조합일 때 결과 칸에 파츠가 나타납니다."),
                    ("완성", "결과 칸의 파츠를 인벤토리로 끌어오면 제작이 끝납니다. 모르는 조합은 레시피북을 보세요."),
                },
            },
            new Topic
            {
                Id = "repair", Name = "수리", Trigger = TutorialTrigger.WindowOpened, Target = "FixWindowManager",
                Intro = "tutorial_t13_repair", When = "수리창 첫 열기 (T13)",
                Steps = new[]
                {
                    ("내구도", "파츠마다 내구도가 있고, 0이 되면 부서져 전투에서 쓸 수 없습니다."),
                    ("수리", "고칠 파츠를 고르고 비용 · 완료 시간을 확인한 뒤 수리합니다."),
                    ("복구", "부서진 코어는 복구 화면에서 되살립니다."),
                },
            },
            new Topic
            {
                Id = "boss_difficulty", Name = "보스 난이도 선택", Trigger = TutorialTrigger.WindowOpened, Target = "BossDifficultySelectUI.IsOpen",
                Intro = "tutorial_t9_hunt_apply", When = "토벌 신청 난이도 선택 첫 열기 (T9)",
                Steps = new[]
                {
                    ("난이도", "난이도가 높을수록 포상이 커지지만, 신청비도 올라갑니다."),
                    ("파츠 점검", "신청 전에 파츠 내구도를 확인하세요. 부서진 파츠는 전투에서 쓸 수 없습니다."),
                },
            },
            new Topic
            {
                Id = "boss_region", Name = "보스 지역 선택", Trigger = TutorialTrigger.WindowOpened, Target = "BossRaceSelectUI.IsOpen",
                Intro = "tutorial_t19_region", When = "지역 보스 선택 첫 열기 (T19)",
                Steps = new[]
                {
                    ("지역 보스", "지역마다 대상 보스가 정해져 있습니다. 이번 시즌에 도전할 지역을 고릅니다."),
                    ("다음 지역", "시즌 1위를 해야 다음 지역이 열립니다."),
                },
            },
            new Topic
            {
                Id = "battle", Name = "전투", Trigger = TutorialTrigger.ComponentInScene, Target = "BattleManager",
                Intro = "", When = "첫 전투 씬 입장 (T10 · 안내 대사는 전투 중 하단 자막으로 따로 나옴)",
                Steps = new[]
                {
                    ("이동 · 회피", "좌우로 움직이고 위로 점프합니다. Space로 피합니다."),
                    ("공격", "D는 기본기입니다. 탄이 떨어지면 잠깐 장전해야 합니다."),
                    ("파츠 기술", "Q W E R은 팔다리 파츠 기술입니다. 부서진 파츠의 기술은 쓸 수 없습니다."),
                },
            },
            new Topic
            {
                Id = "auction", Name = "경매장", Trigger = TutorialTrigger.SceneName, Target = "Auction",
                Intro = "tutorial_t15_auction", When = "경매장 씬 첫 입장 (T15)",
                Steps = new[]
                {
                    ("위탁", "월 ~ 토요일에는 물건을 경매에 맡겨 둘 수 있습니다."),
                    ("일요일 경매", "일요일에 경매가 열리면, 맡긴 물건이 낙찰가로 팔립니다."),
                },
            },
        };

        internal static bool Exists()
        {
            return AssetDatabase.LoadAssetAtPath<TutorialCatalog>(CatalogPath) != null;
        }

        internal static void Setup()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TutorialSetup.PrefabPath);
            if (prefab == null)
            {
                SetupKit.Dialog(Title, $"튜토리얼 화면 프리팹이 없습니다.\n'튜토리얼 화면' 버튼을 먼저 눌러 주세요.\n{TutorialSetup.PrefabPath}", "확인");
                return;
            }

            int existing = Topics.Count(t => AssetDatabase.LoadAssetAtPath<TutorialGuide>(GuidePath(t)) != null);
            var table = new StringBuilder();
            foreach (Topic topic in Topics) table.AppendLine($"  · {topic.Name}: {topic.When}");

            if (!SetupKit.Dialog(Title,
                    $"튜토리얼 {Topics.Length}종과 카탈로그를 만듭니다.\n{table}" +
                    "- 처음 조건이 맞을 때: 인물 안내 대사(대화창) → 화면 튜토리얼, 한 번만 (스킵해도 봤음)\n" +
                    "- F10: 도움말 (본 튜토리얼 다시 보기) / Ctrl + F10: 본 기록 초기화 (개발용)\n" +
                    $"- 튜토리얼 파일: {TopicFolder} ({(existing > 0 ? $"이미 있는 {existing}개는 그대로 둠" : "새로 만듦")})\n" +
                    $"- 카탈로그: {CatalogPath} (Resources라 씬 배치 없이 자동 동작)\n" +
                    "영상 · 이미지는 비어 있으니 각 튜토리얼 파일의 단계에 넣어 주세요.",
                    "만들기", "취소"))
            {
                return;
            }

            var log = new StringBuilder($"[{Title}]\n");
            Directory.CreateDirectory(TopicFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));

            var guides = new List<TutorialGuide>();
            foreach (Topic topic in Topics)
            {
                string path = GuidePath(topic);
                var guide = AssetDatabase.LoadAssetAtPath<TutorialGuide>(path);
                if (guide != null)
                {
                    log.AppendLine($"- {topic.Name}: 이미 있음, 그대로 둠 ({path})");
                }
                else
                {
                    guide = ScriptableObject.CreateInstance<TutorialGuide>();
                    guide.guideId = topic.Id;
                    guide.title = topic.Name;
                    guide.trigger = topic.Trigger;
                    guide.triggerTarget = topic.Target;
                    guide.introDialogueId = topic.Intro;
                    guide.steps = topic.Steps.Select(s => new TutorialStep { title = s.title, description = s.body }).ToList();
                    AssetDatabase.CreateAsset(guide, path);
                    log.AppendLine($"- {topic.Name}: 만듦 — {topic.Trigger} '{topic.Target}', 안내 대사 {(string.IsNullOrEmpty(topic.Intro) ? "없음" : topic.Intro)}");
                }
                guides.Add(guide);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<TutorialCatalog>(CatalogPath);
            bool created = catalog == null;
            if (created) catalog = ScriptableObject.CreateInstance<TutorialCatalog>();

            // 카탈로그에 직접 추가한 다른 튜토리얼은 뒤에 남김
            List<TutorialGuide> extra = catalog.guides.Where(g => g != null && !guides.Contains(g)).ToList();
            catalog.panelPrefab = prefab;
            catalog.guides = guides.Concat(extra).ToList();

            if (created) AssetDatabase.CreateAsset(catalog, CatalogPath);
            else EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            log.AppendLine($"- 카탈로그 {(created ? "만듦" : "갱신")}: {CatalogPath} (튜토리얼 {catalog.guides.Count}개{(extra.Count > 0 ? $", 직접 추가한 {extra.Count}개 유지" : "")})");
            log.AppendLine("완료. Play → 조건이 맞으면 자동으로 열림. 다시 확인하려면 Ctrl + F10으로 본 기록 초기화");
            Debug.Log(log.ToString());
            Selection.activeObject = catalog;
        }

        private static string GuidePath(Topic topic)
        {
            return $"{TopicFolder}/Tutorial_{topic.Id}.asset";
        }
    }
}
