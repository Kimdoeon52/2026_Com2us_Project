using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RealSteel.Cutscene.EditorTools
{
    internal static class CutsceneArt
    {
        internal const string Folder = "Assets/LHM/09.Asset/Cutscene";
        internal const string MapPath = Folder + "/opening_map_placeholder.png";
        internal const string ShadePath = Folder + "/shade_gradient.png";

        private const int MapWidth = 1920;
        private const int MapHeight = 1080;
        private const int ShadeHeight = 256;
        private const int GridStep = 48;
        private const int GridMajorStep = 240;
        private const int BlockCount = 160;
        private const int BlockSeed = 7;

        private static readonly Color Ground = new Color32(0x0B, 0x10, 0x16, 0xFF);
        private static readonly Color GridColor = new Color32(0x8F, 0xB3, 0xD9, 0xFF);
        private static readonly Color RiverColor = new Color32(0x16, 0x32, 0x4A, 0xFF);
        private static readonly Color BlockColor = new Color32(0x1E, 0x2A, 0x36, 0xFF);
        private static readonly Color RoadColor = new Color32(0x3A, 0x4A, 0x5C, 0xFF);
        private static readonly Color ShadeColor = new Color32(0x0D, 0x0E, 0x10, 0xFF);

        private static readonly (float x, float y, float radius, Color color)[] Places =
        {
            (0.58f, 0.18f, 110f, new Color32(0xE0, 0x5A, 0x4F, 0xFF)),
            (0.50f, 0.45f, 120f, new Color32(0xFF, 0xE5, 0x00, 0xFF)),
            (0.22f, 0.72f, 90f, new Color32(0xF2, 0xA9, 0x00, 0xFF)),
            (0.80f, 0.55f, 120f, new Color32(0x3F, 0xC1, 0xC9, 0xFF)),
            (0.82f, 0.24f, 110f, new Color32(0x5B, 0x8D, 0xEF, 0xFF)),
            (0.70f, 0.82f, 110f, new Color32(0x9B, 0x6B, 0xDF, 0xFF)),
        };

        private static readonly (int from, int to)[] Roads =
        {
            (1, 0), (1, 2), (1, 3), (3, 4), (1, 4), (1, 5), (3, 5), (2, 5),
        };

        internal static void EnsureImages(StringBuilder log)
        {
            EnsureFolder(Folder);

            if (AssetDatabase.LoadAssetAtPath<Sprite>(MapPath) == null)
            {
                WriteSprite(MapPath, DrawMap(), 2048);
                log.AppendLine($"- 임시 지도 만듦: {MapPath}");
            }
            else
            {
                log.AppendLine($"- 지도 있음, 그대로 둠: {MapPath}");
            }

            if (AssetDatabase.LoadAssetAtPath<Sprite>(ShadePath) == null)
            {
                WriteSprite(ShadePath, DrawShade(), 256);
                log.AppendLine($"- 어둡게 덮는 그라데이션 만듦: {ShadePath}");
            }
        }

        internal static Sprite LoadMap()
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(MapPath);
        }

        internal static Sprite LoadShade()
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(ShadePath);
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Texture2D DrawMap()
        {
            var pixels = new Color[MapWidth * MapHeight];
            FillGround(pixels);
            DrawGrid(pixels);
            DrawRiver(pixels);
            DrawBlocks(pixels);
            foreach ((int from, int to) in Roads) DrawSegment(pixels, ToPixel(Places[from].x, Places[from].y), ToPixel(Places[to].x, Places[to].y), 5f, RoadColor, 0.9f);
            foreach ((float x, float y, float radius, Color color) in Places) DrawPlace(pixels, ToPixel(x, y), radius, color);

            var texture = new Texture2D(MapWidth, MapHeight, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D DrawShade()
        {
            var texture = new Texture2D(4, ShadeHeight, TextureFormat.RGBA32, false);
            for (int y = 0; y < ShadeHeight; y++)
            {
                float alpha = Mathf.Pow(1f - (float)y / (ShadeHeight - 1), 1.6f) * 0.92f;
                Color color = new Color(ShadeColor.r, ShadeColor.g, ShadeColor.b, alpha);
                for (int x = 0; x < 4; x++) texture.SetPixel(x, y, color);
            }
            texture.Apply();
            return texture;
        }

        private static void FillGround(Color[] pixels)
        {
            var center = new Vector2(MapWidth * 0.5f, MapHeight * 0.5f);
            float reach = center.magnitude;
            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    float edge = Vector2.Distance(new Vector2(x, y), center) / reach;
                    pixels[y * MapWidth + x] = Color.Lerp(Ground, Color.black, edge * edge * 0.6f);
                }
            }
        }

        private static void DrawGrid(Color[] pixels)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    bool major = x % GridMajorStep == 0 || y % GridMajorStep == 0;
                    bool minor = x % GridStep == 0 || y % GridStep == 0;
                    if (major) Blend(pixels, x, y, GridColor, 0.12f);
                    else if (minor) Blend(pixels, x, y, GridColor, 0.05f);
                }
            }
        }

        private static void DrawRiver(Color[] pixels)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                float center = MapWidth * (0.36f + 0.04f * Mathf.Sin((float)y / MapHeight * Mathf.PI * 2.2f));
                for (int x = Mathf.Max(0, (int)center - 16); x <= Mathf.Min(MapWidth - 1, (int)center + 16); x++)
                {
                    float distance = Mathf.Abs(x - center);
                    Blend(pixels, x, y, distance > 13f ? GridColor : RiverColor, distance > 13f ? 0.25f : 0.95f);
                }
            }
        }

        private static void DrawBlocks(Color[] pixels)
        {
            var random = new System.Random(BlockSeed);
            for (int i = 0; i < BlockCount; i++)
            {
                double angle = random.NextDouble() * Mathf.PI * 2f;
                double reach = System.Math.Sqrt(random.NextDouble());
                int cx = (int)(MapWidth * (0.52f + 0.2f * reach * System.Math.Cos(angle)));
                int cy = (int)(MapHeight * (0.52f + 0.24f * reach * System.Math.Sin(angle)));
                int w = random.Next(14, 46);
                int h = random.Next(14, 46);
                FillRect(pixels, cx - w / 2, cy - h / 2, w, h, BlockColor, 0.8f);
            }
        }

        private static void DrawPlace(Color[] pixels, Vector2 center, float radius, Color color)
        {
            int minX = Mathf.Max(0, (int)(center.x - radius - 2));
            int maxX = Mathf.Min(MapWidth - 1, (int)(center.x + radius + 2));
            int minY = Mathf.Max(0, (int)(center.y - radius - 2));
            int maxY = Mathf.Min(MapHeight - 1, (int)(center.y + radius + 2));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance <= 8f) Blend(pixels, x, y, color, 1f);
                    else if (distance > radius - 3f && distance <= radius) Blend(pixels, x, y, color, 0.7f);
                    else if (distance <= radius) Blend(pixels, x, y, color, 0.12f);
                }
            }
        }

        private static void DrawSegment(Color[] pixels, Vector2 from, Vector2 to, float thickness, Color color, float alpha)
        {
            int minX = Mathf.Max(0, (int)(Mathf.Min(from.x, to.x) - thickness));
            int maxX = Mathf.Min(MapWidth - 1, (int)(Mathf.Max(from.x, to.x) + thickness));
            int minY = Mathf.Max(0, (int)(Mathf.Min(from.y, to.y) - thickness));
            int maxY = Mathf.Min(MapHeight - 1, (int)(Mathf.Max(from.y, to.y) + thickness));
            Vector2 line = to - from;
            float lengthSquared = Mathf.Max(0.0001f, line.sqrMagnitude);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var point = new Vector2(x, y);
                    float t = Mathf.Clamp01(Vector2.Dot(point - from, line) / lengthSquared);
                    if (Vector2.Distance(point, from + line * t) <= thickness * 0.5f) Blend(pixels, x, y, color, alpha);
                }
            }
        }

        private static void FillRect(Color[] pixels, int left, int bottom, int width, int height, Color color, float alpha)
        {
            for (int y = Mathf.Max(0, bottom); y < Mathf.Min(MapHeight, bottom + height); y++)
            {
                for (int x = Mathf.Max(0, left); x < Mathf.Min(MapWidth, left + width); x++) Blend(pixels, x, y, color, alpha);
            }
        }

        private static void Blend(Color[] pixels, int x, int y, Color color, float alpha)
        {
            int index = y * MapWidth + x;
            pixels[index] = Color.Lerp(pixels[index], new Color(color.r, color.g, color.b, 1f), alpha);
        }

        private static Vector2 ToPixel(float x, float y)
        {
            return new Vector2(x * MapWidth, (1f - y) * MapHeight);
        }

        private static void WriteSprite(string assetPath, Texture2D texture, int maxSize)
        {
            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
