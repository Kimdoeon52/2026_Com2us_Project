using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RealSteel.BossRace.EditorTools
{
    public static class BossRaceArt
    {
        public const string Folder = "Assets/LHM/09.Asset/BossRaceUI";

        private const int IconSize = 256;
        private const float Stroke = 6f;

        public static Sprite Frame => Load("frame");
        public static Sprite Dash => Load("dash");
        public static Sprite Lock => Load("icon_lock");
        public static Sprite Wheel => Load("icon_wheel");
        public static Sprite Robot => Load("icon_robot");
        public static Sprite Box => Load("icon_box");
        public static Sprite Unknown => Load("icon_unknown");
        public static Sprite Phone => Load("icon_phone");
        public static Sprite Slant => Load("slant");
        public static Sprite Hazard => Load("hazard");
        public static Sprite Alert => Load("icon_alert");

        public static void Generate()
        {
            Directory.CreateDirectory(Folder);

            WriteFrame();
            WriteDash();
            WriteIcon("icon_wheel", WheelShapes());
            WriteIcon("icon_robot", RobotShapes());
            WriteIcon("icon_box", BoxShapes());
            WriteIcon("icon_unknown", UnknownShapes());
            WriteIcon("icon_lock", LockShapes());
            WriteIcon("icon_phone", PhoneShapes());
            WriteIcon("icon_alert", AlertShapes());
            WriteSlant();
            WriteHazard();

            Debug.Log($"[BossRace] 이미지 생성: {Folder} (frame, dash, slant, hazard, icon_wheel / robot / box / unknown / lock / phone / alert)");
        }

        private static Sprite Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");
        }

        private static List<Func<Vector2, float>> WheelShapes()
        {
            var c = new Vector2(128f, 128f);
            var shapes = new List<Func<Vector2, float>> { Ring(c, 104f, Stroke), Ring(c, 74f, Stroke), Disk(c, 22f) };
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                shapes.Add(Line(c + dir * 74f, c + dir * 104f, Stroke));
            }
            return shapes;
        }

        private static List<Func<Vector2, float>> RobotShapes()
        {
            var shapes = new List<Func<Vector2, float>>();
            shapes.AddRange(Rect(60f, 40f, 136f, 100f, Stroke));
            shapes.Add(Ring(new Vector2(128f, 90f), 28f, Stroke));
            shapes.Add(Disk(new Vector2(128f, 90f), 12f));
            shapes.Add(Line(new Vector2(100f, 140f), new Vector2(100f, 236f), Stroke));
            shapes.Add(Line(new Vector2(156f, 140f), new Vector2(156f, 236f), Stroke));
            shapes.Add(Line(new Vector2(60f, 92f), new Vector2(22f, 176f), Stroke));
            shapes.Add(Line(new Vector2(22f, 176f), new Vector2(38f, 236f), Stroke));
            shapes.Add(Line(new Vector2(196f, 92f), new Vector2(234f, 176f), Stroke));
            shapes.Add(Line(new Vector2(234f, 176f), new Vector2(218f, 236f), Stroke));
            return shapes;
        }

        private static List<Func<Vector2, float>> BoxShapes()
        {
            var shapes = new List<Func<Vector2, float>>();
            shapes.AddRange(Rect(108f, 18f, 40f, 30f, Stroke));
            shapes.AddRange(Rect(68f, 48f, 120f, 132f, Stroke));
            shapes.Add(Line(new Vector2(68f, 82f), new Vector2(188f, 82f), Stroke));
            shapes.Add(Line(new Vector2(68f, 118f), new Vector2(34f, 118f), Stroke));
            shapes.Add(Line(new Vector2(34f, 118f), new Vector2(34f, 152f), Stroke));
            shapes.Add(Line(new Vector2(188f, 118f), new Vector2(222f, 118f), Stroke));
            shapes.Add(Line(new Vector2(222f, 118f), new Vector2(222f, 152f), Stroke));
            shapes.Add(Line(new Vector2(102f, 180f), new Vector2(102f, 236f), Stroke));
            shapes.Add(Line(new Vector2(154f, 180f), new Vector2(154f, 236f), Stroke));
            return shapes;
        }

        private static List<Func<Vector2, float>> UnknownShapes()
        {
            var shapes = new List<Func<Vector2, float>>();
            var c = new Vector2(128f, 128f);
            var points = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = (-90f + 60f * i) * Mathf.Deg2Rad;
                points[i] = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 112f;
            }
            for (int i = 0; i < 6; i++) shapes.Add(Line(points[i], points[(i + 1) % 6], Stroke));

            var arcCenter = new Vector2(128f, 104f);
            shapes.Add(Arc(arcCenter, 26f, 180f, 420f, Stroke));
            var arcEnd = arcCenter + new Vector2(Mathf.Cos(60f * Mathf.Deg2Rad), Mathf.Sin(60f * Mathf.Deg2Rad)) * 26f;
            shapes.Add(Line(arcEnd, new Vector2(128f, 144f), Stroke));
            shapes.Add(Line(new Vector2(128f, 144f), new Vector2(128f, 160f), Stroke));
            shapes.Add(Disk(new Vector2(128f, 182f), 5f));
            return shapes;
        }

        private static List<Func<Vector2, float>> LockShapes()
        {
            const float w = 18f;
            var shapes = new List<Func<Vector2, float>>();
            var c = new Vector2(128f, 104f);
            shapes.Add(Arc(c, 44f, 180f, 360f, w));
            shapes.Add(Line(new Vector2(84f, 104f), new Vector2(84f, 128f), w));
            shapes.Add(Line(new Vector2(172f, 104f), new Vector2(172f, 128f), w));
            shapes.AddRange(Rect(60f, 128f, 136f, 104f, w));
            shapes.Add(Disk(new Vector2(128f, 170f), 13f));
            shapes.Add(Line(new Vector2(128f, 170f), new Vector2(128f, 200f), 12f));
            return shapes;
        }

        private static List<Func<Vector2, float>> PhoneShapes()
        {
            var shapes = new List<Func<Vector2, float>>();
            var c = new Vector2(128f, 156f);
            const float radius = 82f;
            shapes.Add(Arc(c, radius, 205f, 335f, 26f));

            foreach (float degrees in new[] { 205f, 335f })
            {
                Vector2 end = c + new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * radius;
                shapes.Add(Line(end, end + new Vector2(0f, 46f), 46f));
            }

            shapes.Add(Arc(new Vector2(128f, 156f), 118f, 235f, 305f, 9f));
            shapes.Add(Arc(new Vector2(128f, 156f), 140f, 245f, 295f, 9f));
            return shapes;
        }

        private static List<Func<Vector2, float>> AlertShapes()
        {
            var c = new Vector2(128f, 128f);
            return new List<Func<Vector2, float>>
            {
                Ring(c, 104f, 20f),
                Line(new Vector2(128f, 72f), new Vector2(128f, 140f), 24f),
                Disk(new Vector2(128f, 182f), 15f),
            };
        }

        private static void WriteSlant()
        {
            const int width = 24;
            const int height = 48;
            const float lean = 8f;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float offset = lean * y / (height - 1f);
                for (int x = 0; x < width; x++)
                {
                    float left = offset;
                    float right = width - lean + offset;
                    float coverage = Mathf.Clamp01(Mathf.Min(x + 1f - left, right - x));
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }
            Save("slant", width, height, pixels, TextureWrapMode.Clamp, Vector4.zero);
        }

        private static void WriteHazard()
        {
            const int size = 16;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool stripe = (x + y) % size < size / 2;
                    pixels[y * size + x] = stripe ? new Color32(0xF2, 0xA9, 0x00, 0xFF) : new Color32(0x14, 0x16, 0x19, 0xFF);
                }
            }
            Save("hazard", size, size, pixels, TextureWrapMode.Repeat, Vector4.zero);
        }

        private static void WriteIcon(string name, List<Func<Vector2, float>> shapes)
        {
            var pixels = new Color32[IconSize * IconSize];
            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    var p = new Vector2(x + 0.5f, IconSize - (y + 0.5f));
                    float coverage = 0f;
                    foreach (Func<Vector2, float> shape in shapes)
                    {
                        coverage = Mathf.Max(coverage, shape(p));
                        if (coverage >= 1f) break;
                    }
                    pixels[y * IconSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }

            Save(name, IconSize, IconSize, pixels, TextureWrapMode.Clamp, Vector4.zero);
        }

        private static void WriteFrame()
        {
            const int size = 16;
            const int border = 2;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < border || y < border || x >= size - border || y >= size - border;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(edge ? 255 : 0));
                }
            }
            Save("frame", size, size, pixels, TextureWrapMode.Clamp, new Vector4(4f, 4f, 4f, 4f));
        }

        private static void WriteDash()
        {
            const int width = 12;
            const int height = 2;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++) pixels[y * width + x] = new Color32(255, 255, 255, (byte)(x < 6 ? 255 : 0));
            }
            Save("dash", width, height, pixels, TextureWrapMode.Repeat, Vector4.zero);
        }

        private static void Save(string name, int width, int height, Color32[] pixels, TextureWrapMode wrap, Vector4 border)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        private static Func<Vector2, float> Line(Vector2 a, Vector2 b, float width)
        {
            return p => Coverage(width * 0.5f - SegmentDistance(p, a, b));
        }

        private static Func<Vector2, float> Ring(Vector2 center, float radius, float width)
        {
            return p => Coverage(width * 0.5f - Mathf.Abs(Vector2.Distance(p, center) - radius));
        }

        private static Func<Vector2, float> Disk(Vector2 center, float radius)
        {
            return p => Coverage(radius - Vector2.Distance(p, center));
        }

        private static Func<Vector2, float> Arc(Vector2 center, float radius, float fromDegrees, float toDegrees, float width)
        {
            Vector2 start = center + new Vector2(Mathf.Cos(fromDegrees * Mathf.Deg2Rad), Mathf.Sin(fromDegrees * Mathf.Deg2Rad)) * radius;
            Vector2 end = center + new Vector2(Mathf.Cos(toDegrees * Mathf.Deg2Rad), Mathf.Sin(toDegrees * Mathf.Deg2Rad)) * radius;
            return p =>
            {
                Vector2 d = p - center;
                float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                while (angle < fromDegrees) angle += 360f;
                float distance = angle <= toDegrees
                    ? Mathf.Abs(d.magnitude - radius)
                    : Mathf.Min(Vector2.Distance(p, start), Vector2.Distance(p, end));
                return Coverage(width * 0.5f - distance);
            };
        }

        private static IEnumerable<Func<Vector2, float>> Rect(float x, float y, float w, float h, float width)
        {
            var a = new Vector2(x, y);
            var b = new Vector2(x + w, y);
            var c = new Vector2(x + w, y + h);
            var d = new Vector2(x, y + h);
            yield return Line(a, b, width);
            yield return Line(b, c, width);
            yield return Line(c, d, width);
            yield return Line(d, a, width);
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        private static float Coverage(float signed)
        {
            return Mathf.Clamp01(signed + 0.5f);
        }
    }
}
