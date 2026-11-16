using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealSteel.Dialogue.EditorTools
{
    public static class NpcSpriteSetup
    {
        private const string UndoName = "NPC 스프라이트 적용";
        private const string OutputFolder = "Assets/LHM/09.Asset/NPC";
        private const string SpriteChildName = "Sprite";
        private const string BillboardMaterialPath = "Assets/SYS/Materials/Ex_Sprite.mat";

        private struct NpcArt
        {
            public string speakerId;
            public string source;
            // 여러 장이 든 시트면 그중 쓸 스프라이트 이름 (비우면 그림 전체)
            public string spriteName;
            public float height;
        }

        private static readonly NpcArt[] Arts =
        {
            new NpcArt { speakerId = "receptionist", source = "Assets/Sprites/imsinpc/소녀.png", height = 1.6f },
            new NpcArt { speakerId = "junkdealer", source = "Assets/Sprites/imsinpc/mobcha-C.png", height = 1.9f },
            new NpcArt { speakerId = "auctioneer", source = "Assets/Sprites/NPC/NPC-PC.png", spriteName = "NPC-PC_1", height = 1.6f },
        };

        internal static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!EditorUtility.DisplayDialog(UndoName,
                    $"'{scene.name}' 씬({(string.IsNullOrEmpty(scene.path) ? "저장 안 된 씬" : scene.path)})의 NPC에 스프라이트를 적용합니다.\n" +
                    (SetupKit.IsLhmScene(scene) ? "" : "※ LHM 밖의 씬입니다. Speaker Id가 receptionist / junkdealer / auctioneer인 NPC만 바뀝니다.\n") +
                    "- 길드 접수원 ← 소녀.png / 고물상 아저씨 ← mobcha-C.png / 경매장 등록직원 ← NPC-PC.png의 NPC-PC_1\n" +
                    $"- 원본은 그대로 두고 {OutputFolder}로 복사 (투명 여백 자르기, 픽셀아트 설정, 발밑 기준점)\n" +
                    "- 필드: 캡슐은 숨기기만 하고 자식 'Sprite' 추가 (플레이어와 같은 RS 빌보드 머티리얼), [E]·클로즈업 높이를 그림에 맞춤\n" +
                    "씬은 자동 저장하지 않습니다. (Ctrl+Z로 씬 변경 되돌리기, 복사한 그림 파일은 남음)", "적용", "취소"))
            {
                return;
            }

            var log = new StringBuilder($"[NPC 스프라이트] {scene.path}\n");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            int group = Undo.GetCurrentGroup();

            Apply(log);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료. Ctrl+S로 저장 후 Play.");
            Debug.Log(log.ToString());
        }

        internal static bool IsApplied()
        {
            Scene active = SceneManager.GetActiveScene();
            NpcInteractable[] targets = Object.FindObjectsByType<NpcInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(npc => npc.gameObject.scene == active && Arts.Any(art => art.speakerId == npc.SpeakerId))
                .ToArray();
            return targets.Length > 0 && targets.All(npc => npc.transform.Find(SpriteChildName) != null);
        }

        internal static void Apply(StringBuilder log)
        {
            Directory.CreateDirectory(OutputFolder);
            for (int i = 0; i < Arts.Length; i++)
            {
                Sprite sprite = ImportArt(Arts[i], log);
                if (sprite != null) ApplyToField(Arts[i], sprite, log);
            }
        }

        // 원본이 다른 폴더로 옮겨졌으면 같은 파일 이름으로 프로젝트에서 찾음 (예: Sprites/NPC-PC.png → Sprites/NPC/NPC-PC.png)
        private static string ResolveSource(string path, StringBuilder log)
        {
            if (File.Exists(Path.GetFullPath(path))) return path;

            string fileName = Path.GetFileName(path);
            string found = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(path))
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileName(p) == fileName);
            if (found != null) log.AppendLine($"- 원본 위치가 바뀜: {path} → {found} (NpcSpriteSetup.cs의 경로도 고쳐 두세요)");
            return found ?? path;
        }

        private static Sprite ImportArt(NpcArt art, StringBuilder log)
        {
            string sourcePath = ResolveSource(art.source, log);
            string sourceFull = Path.GetFullPath(sourcePath);
            if (!File.Exists(sourceFull))
            {
                log.AppendLine($"- 실패: 원본 없음 {sourcePath}");
                return null;
            }

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(sourceFull)))
            {
                Object.DestroyImmediate(source);
                log.AppendLine($"- 실패: 그림을 읽지 못함 {sourcePath}");
                return null;
            }

            RectInt area = new RectInt(0, 0, source.width, source.height);
            if (!string.IsNullOrEmpty(art.spriteName))
            {
                Sprite part = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<Sprite>().FirstOrDefault(sp => sp.name == art.spriteName);
                if (part == null)
                {
                    Object.DestroyImmediate(source);
                    log.AppendLine($"- 실패: {sourcePath}에 '{art.spriteName}' 스프라이트가 없음");
                    return null;
                }

                // 시트의 임포트 크기가 원본과 다르면(최대 크기 제한 등) 비율을 맞춤
                float ratio = source.width / Mathf.Max(1f, part.texture.width);
                Rect rect = part.rect;
                area = new RectInt(Mathf.RoundToInt(rect.x * ratio), Mathf.RoundToInt(rect.y * ratio), Mathf.RoundToInt(rect.width * ratio), Mathf.RoundToInt(rect.height * ratio));
            }

            RectInt bounds = OpaqueBounds(source, area);
            var cropped = new Texture2D(bounds.width, bounds.height, TextureFormat.RGBA32, false);
            cropped.SetPixels(source.GetPixels(bounds.x, bounds.y, bounds.width, bounds.height));
            cropped.Apply();

            string path = $"{OutputFolder}/{art.speakerId}.png";
            File.WriteAllBytes(Path.GetFullPath(path), cropped.EncodeToPNG());
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(cropped);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = bounds.height / Mathf.Max(0.1f, art.height);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            string from = string.IsNullOrEmpty(art.spriteName) ? sourcePath : $"{sourcePath} ({art.spriteName})";
            log.AppendLine($"- {from} → {path} ({bounds.width}x{bounds.height}px로 여백 자름, 높이 {art.height}m)");
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static RectInt OpaqueBounds(Texture2D texture, RectInt area)
        {
            Color32[] pixels = texture.GetPixels32();
            int width = texture.width;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

            for (int y = Mathf.Max(0, area.yMin); y < Mathf.Min(texture.height, area.yMax); y++)
            {
                for (int x = Mathf.Max(0, area.xMin); x < Mathf.Min(width, area.xMax); x++)
                {
                    if (pixels[y * width + x].a == 0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            return maxX < 0 ? area : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        internal static Vector3 FeetOf(Transform npc)
        {
            var filter = npc.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return npc.position;

            Bounds local = filter.sharedMesh.bounds;
            Vector3 center = npc.TransformPoint(local.center);
            float lowest = float.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y,
                    (i & 4) == 0 ? local.min.z : local.max.z);
                lowest = Mathf.Min(lowest, npc.TransformPoint(corner).y);
            }
            return new Vector3(center.x, lowest, center.z);
        }

        private static void ApplyToField(NpcArt art, Sprite sprite, StringBuilder log)
        {
            NpcInteractable npc = null;
            Scene active = SceneManager.GetActiveScene();
            foreach (NpcInteractable candidate in Object.FindObjectsByType<NpcInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.SpeakerId == art.speakerId && candidate.gameObject.scene == active) npc = candidate;
            }

            if (npc == null)
            {
                log.AppendLine($"- 주의: Speaker Id '{art.speakerId}'인 NPC가 씬에 없음");
                return;
            }

            // NPC 기능이 플레이어에 잘못 붙어 있으면 플레이어 그림('Sprite' 자식)을 덮어쓰게 됨 → 건너뜀
            GameObject player = JunkShopDialogueSetup.FindPlayer(active);
            if (player != null && npc.transform.IsChildOf(player.transform))
            {
                log.AppendLine($"- 건너뜀: '{art.speakerId}' NPC 기능이 플레이어({player.name})에 붙어 있음 → 'NPC 연결' 버튼으로 NPC 오브젝트에 다시 연결한 뒤 실행하세요");
                return;
            }

            Transform root = npc.transform;
            var meshRenderer = npc.GetComponent<MeshRenderer>();
            Vector3 feet = FeetOf(root);
            if (meshRenderer != null)
            {
                if (meshRenderer.enabled)
                {
                    Undo.RecordObject(meshRenderer, UndoName);
                    meshRenderer.enabled = false;
                }
            }

            Transform child = root.Find(SpriteChildName);
            SpriteRenderer spriteRenderer;
            if (child == null)
            {
                var go = new GameObject(SpriteChildName);
                Undo.RegisterCreatedObjectUndo(go, UndoName);
                go.transform.SetParent(root, false);
                child = go.transform;
                spriteRenderer = go.AddComponent<SpriteRenderer>();
            }
            else
            {
                spriteRenderer = child.GetComponent<SpriteRenderer>();
                if (spriteRenderer == null) spriteRenderer = Undo.AddComponent<SpriteRenderer>(child.gameObject);
                Undo.RecordObject(child, UndoName);
                Undo.RecordObject(spriteRenderer, UndoName);
            }

            spriteRenderer.sprite = sprite;

            var billboardMaterial = AssetDatabase.LoadAssetAtPath<Material>(BillboardMaterialPath);
            NpcBillboard billboard = child.GetComponent<NpcBillboard>();
            if (billboardMaterial != null)
            {
                spriteRenderer.sharedMaterial = billboardMaterial;
                if (billboard != null) Undo.DestroyObjectImmediate(billboard);
                child.localRotation = Quaternion.identity;
            }
            else
            {
                if (billboard == null) Undo.AddComponent<NpcBillboard>(child.gameObject);
                log.AppendLine($"- 주의: {BillboardMaterialPath}가 없어 기본 스프라이트 머티리얼 + NpcBillboard 사용 (플레이어와 겹칠 때 앞뒤가 어긋날 수 있음)");
            }
            child.position = feet;
            Vector3 scale = root.lossyScale;
            child.localScale = new Vector3(1f / Mathf.Max(0.0001f, scale.x), 1f / Mathf.Max(0.0001f, scale.y), 1f / Mathf.Max(0.0001f, scale.z));

            float top = feet.y + art.height;
            var npcSo = new SerializedObject(npc);
            npcSo.FindProperty("promptOffset").vector3Value = new Vector3(0f, top + 0.3f - root.position.y, 0f);
            npcSo.FindProperty("closeUpFocusOffset").vector3Value = new Vector3(0f, feet.y + art.height * 0.85f - root.position.y, 0f);
            npcSo.ApplyModifiedProperties();

            log.AppendLine($"- {npc.name}: 캡슐 숨김, Sprite 추가 (발 위치 y={feet.y:0.00}), [E] 표시·클로즈업 초점 높이 조정");
        }
    }
}
