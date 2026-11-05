using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using RealSteel.Boss.JunkWheeler;

namespace RealSteel.EditorTools
{
    /// <summary>
    /// 정크 휠러 보스 탄환용 고품질 스프라이트 2종 및 탄환 프리팹을 자동 생성/연결하는 에디터 툴
    /// </summary>
    public static class JunkBulletAssetGenerator
    {
        private const string SpriteDir = "Assets/Sprites/1Boss_Robot";
        private const string SawSpritePath = SpriteDir + "/Junk_Bullet_Saw.png";
        private const string SpikeSpritePath = SpriteDir + "/Junk_Bullet_Spike.png";
        private const string PrefabDir = "Assets/KKH/04.Prefab/BOSS/First_Boss";
        private const string BulletPrefabPath = PrefabDir + "/Boss_JunkSaw_Bullet.prefab";
        private const string BossPrefabPath = PrefabDir + "/Boss1_JunkWheeler.prefab";
        private const string MuzzleFlashVfxPath = "Assets/Eric VFX Studio/Flipbook VFX - Cartoon Skill/Prefab/FX_Flipbook_FlashShotB.prefab";

        [MenuItem("Tools/RealSteel/Generate Junk Bullet Assets (스프라이트 & 프리팹 생성)", false, 30)]
        public static void GenerateAllAssets()
        {
            try
            {
                AssetDatabase.StartAssetEditing();

                // 1. 디렉토리 확인
                if (!Directory.Exists(SpriteDir)) Directory.CreateDirectory(SpriteDir);
                if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);

                // 2. 톱날 탄환 텍스처 (Junk_Bullet_Saw.png) 생성
                Texture2D sawTex = GenerateSawbladeTexture(128);
                File.WriteAllBytes(SawSpritePath, sawTex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(sawTex);

                // 3. 스파이크 탄환 텍스처 (Junk_Bullet_Spike.png) 생성
                Texture2D spikeTex = GenerateSpikeBulletTexture(128, 64);
                File.WriteAllBytes(SpikeSpritePath, spikeTex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(spikeTex);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // 4. 텍스처 임포트 설정 적용
            AssetDatabase.ImportAsset(SawSpritePath, ImportAssetOptions.ForceUpdate);
            ConfigureTextureImporter(SawSpritePath, 100f);

            AssetDatabase.ImportAsset(SpikeSpritePath, ImportAssetOptions.ForceUpdate);
            ConfigureTextureImporter(SpikeSpritePath, 100f);

            // 5. 탄환 프리팹 생성 (Boss_JunkSaw_Bullet.prefab)
            CreateOrUpdateBulletPrefab();

            // 6. 보스 프리팹(Boss1_JunkWheeler.prefab)에 자동 연결
            LinkBulletToBossPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>[JunkBulletAssetGenerator] 정크 휠러 고철 톱날 탄환 스프라이트 및 프리팹 생성/연동 완료!</color>");
            EditorUtility.DisplayDialog("완료", "정크 휠러 고철 톱날 탄환 스프라이트 2종 및 프리팹 생성이 완료되어 보스에 자동 적용되었습니다!", "확인");
        }

        private static void ConfigureTextureImporter(string path, float ppu)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Bilinear; // 회전 시 부드러운 안티앨리어싱
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        #region Procedural Sprite Generation

        /// <summary>
        /// 128x128 고품질 8날 고철 회전 톱날 텍스처 생성
        /// </summary>
        private static Texture2D GenerateSawbladeTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float cx = (size - 1) * 0.5f;
            float cy = (size - 1) * 0.5f;

            float rOuter = size * 0.44f; // 56.32
            float rInner = size * 0.30f; // 38.4
            int teethCount = 8;
            float toothAngle = (Mathf.PI * 2f) / teethCount;

            // 4개 통풍구(구멍) 좌표 사전 계산 (45도, 135도, 225도, 315도)
            float rHole = size * 0.22f; // ~28px
            float holeRadius = size * 0.042f; // ~5.4px
            Vector2[] holeCenters = new Vector2[4];
            for (int h = 0; h < 4; h++)
            {
                float a = (Mathf.PI * 0.25f) + (h * Mathf.PI * 0.5f);
                holeCenters[h] = new Vector2(cx + Mathf.Cos(a) * rHole, cy + Mathf.Sin(a) * rHole);
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float theta = Mathf.Atan2(dy, dx);
                    if (theta < 0) theta += Mathf.PI * 2f;

                    // 톱날 형상 계산
                    float localAngle = theta % toothAngle;
                    float t = localAngle / toothAngle; // 0.0 ~ 1.0

                    float rToothMax;
                    if (t <= 0.72f)
                    {
                        // 톱날 상승 구간 (완만한 곡선형 상승)
                        float prog = Mathf.Pow(t / 0.72f, 0.85f);
                        rToothMax = Mathf.Lerp(rInner, rOuter, prog);
                    }
                    else
                    {
                        // 톱날 급경사 절단면 (급격한 드롭)
                        float drop = (t - 0.72f) / 0.28f;
                        rToothMax = Mathf.Lerp(rOuter, rInner, Mathf.Clamp01(drop * 2.2f));
                    }

                    // 1. 톱날 바깥 영역 (투명 또는 외곽 열기 블룸)
                    if (r > rToothMax + 2.2f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    // 2. 외곽 안티앨리어싱 및 화염/열기 오라 (Bloom Glow)
                    if (r > rToothMax)
                    {
                        float edgeDist = (r - rToothMax) / 2.2f;
                        float alpha = Mathf.Clamp01(1f - edgeDist);
                        Color glowColor = new Color(1.0f, 0.45f, 0.05f, alpha * 0.75f);
                        tex.SetPixel(x, y, glowColor);
                        continue;
                    }

                    // 3. 통풍구(Holes) 체크
                    bool inHole = false;
                    bool inHoleBevel = false;
                    for (int h = 0; h < 4; h++)
                    {
                        float distToHole = Vector2.Distance(new Vector2(x, y), holeCenters[h]);
                        if (distToHole < holeRadius)
                        {
                            inHole = true;
                            if (distToHole > holeRadius - 1.4f) inHoleBevel = true;
                            break;
                        }
                    }

                    if (inHole)
                    {
                        if (inHoleBevel)
                        {
                            tex.SetPixel(x, y, new Color(0.12f, 0.13f, 0.15f, 0.9f));
                        }
                        else
                        {
                            tex.SetPixel(x, y, Color.clear);
                        }
                        continue;
                    }

                    // 4. 중앙 볼트 및 브론즈 허브 링
                    float rHubOuter = size * 0.135f; // ~17px
                    float rHubInner = size * 0.09f;  // ~11.5px
                    float rBolt = size * 0.08f;     // ~10px

                    if (r <= rBolt)
                    {
                        // 육각 볼트 연출
                        float hexAngle = theta % (Mathf.PI / 3f) - (Mathf.PI / 6f);
                        float hexDist = r * Mathf.Cos(hexAngle);
                        if (hexDist <= rBolt * 0.92f)
                        {
                            // 육각 볼트 표면 (메탈릭 하이라이트 & 슬롯 십자 홈)
                            bool isCrossSlot = (Mathf.Abs(dx) <= 1.4f && Mathf.Abs(dy) <= rBolt * 0.65f) ||
                                               (Mathf.Abs(dy) <= 1.4f && Mathf.Abs(dx) <= rBolt * 0.65f);
                            if (isCrossSlot)
                            {
                                tex.SetPixel(x, y, new Color(0.10f, 0.11f, 0.13f, 1f));
                            }
                            else
                            {
                                float boltLight = Mathf.Clamp01(0.45f + (dx - dy) * 0.035f);
                                Color boltColor = Color.Lerp(new Color(0.25f, 0.28f, 0.32f), new Color(0.75f, 0.80f, 0.88f), boltLight);
                                tex.SetPixel(x, y, boltColor);
                            }
                        }
                        else
                        {
                            // 볼트 외곽선
                            tex.SetPixel(x, y, new Color(0.12f, 0.13f, 0.16f, 1f));
                        }
                        continue;
                    }
                    else if (r <= rHubOuter)
                    {
                        // 브론즈/황동 보강 링 (원형 와셔)
                        float hubT = (r - rHubInner) / (rHubOuter - rHubInner);
                        float shine = Mathf.Sin(theta * 2f) * 0.15f;
                        Color brassBase = new Color(0.85f, 0.58f, 0.22f);
                        Color brassDark = new Color(0.50f, 0.32f, 0.10f);
                        Color brassCol = Color.Lerp(brassDark, brassBase, Mathf.Clamp01(hubT + shine));
                        tex.SetPixel(x, y, brassCol);
                        continue;
                    }

                    // 5. 톱날 바디 및 날 끝(Cutting Edge) 열기 그라데이션
                    float edgeRatio = Mathf.Clamp01((r - rInner) / Mathf.Max(1f, rToothMax - rInner));

                    if (edgeRatio > 0.82f)
                    {
                        // 극고열 절단날 끝 (흰색-황금색-주황색)
                        float heatT = (edgeRatio - 0.82f) / 0.18f;
                        Color hotColor = Color.Lerp(new Color(1.0f, 0.55f, 0.05f), new Color(1.0f, 0.98f, 0.85f), heatT);
                        tex.SetPixel(x, y, hotColor);
                    }
                    else if (edgeRatio > 0.45f)
                    {
                        // 가열된 붉은 메탈 전이 구역
                        float heatT = (edgeRatio - 0.45f) / 0.37f;
                        Color steelHeat = Color.Lerp(new Color(0.38f, 0.32f, 0.35f), new Color(0.95f, 0.40f, 0.08f), heatT);
                        tex.SetPixel(x, y, steelHeat);
                    }
                    else
                    {
                        // 톱날 디스크 강철 본체 (원형 선반 절삭 홈 + 녹슨 철 질감)
                        float groove = Mathf.Sin(r * 2.2f) * 0.08f;
                        float rustNoise = Mathf.Sin(x * 0.4f + y * 0.3f) * 0.06f;

                        Color steelColor = new Color(0.28f + groove, 0.30f + groove, 0.35f + groove);
                        Color rustColor = new Color(0.55f, 0.28f, 0.12f);
                        Color finalColor = Color.Lerp(steelColor, rustColor, Mathf.Clamp01(rustNoise + 0.2f));

                        // 림 경계 음영
                        if (r >= rInner - 1.5f && r <= rInner + 1.5f)
                        {
                            finalColor *= 0.75f;
                        }

                        tex.SetPixel(x, y, finalColor);
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 128x64 중장갑 고철 스파이크 미사일 / 리벳 관통탄 텍스처 생성
        /// </summary>
        private static Texture2D GenerateSpikeBulletTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float cx = (width - 1) * 0.5f;
            float cy = (height - 1) * 0.5f;
            float tipX = width * 0.92f;
            float rearX = width * 0.10f;
            float halfH = height * 0.36f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dy = Mathf.Abs(y - cy);

                    // 1. 탄두(오른쪽 X방향 진행) 원뿔형 경사
                    float maxAllowedY;
                    if (x > cx)
                    {
                        // 탄두 쐐기형
                        float t = (tipX - x) / Mathf.Max(1f, tipX - cx);
                        maxAllowedY = halfH * Mathf.Clamp01(t);
                    }
                    else if (x >= rearX)
                    {
                        // 탄체 원통형
                        maxAllowedY = halfH;
                    }
                    else
                    {
                        // 후방 추진 노즐 테이퍼
                        float t = (x - (rearX * 0.3f)) / (rearX * 0.7f);
                        maxAllowedY = halfH * Mathf.Lerp(0.6f, 1.0f, Mathf.Clamp01(t));
                    }

                    if (dy > maxAllowedY + 1.5f || x < rearX * 0.3f || x > tipX + 1.5f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    // 외곽 안티앨리어싱
                    if (dy > maxAllowedY)
                    {
                        float alpha = Mathf.Clamp01(1f - (dy - maxAllowedY) / 1.5f);
                        tex.SetPixel(x, y, new Color(1.0f, 0.45f, 0.05f, alpha * 0.7f));
                        continue;
                    }

                    // 2. 내부 텍스처링
                    if (x >= tipX - 10f)
                    {
                        // 초고열 텅스텐 관통 침 팁 (백색-금색-주황)
                        float tipHeat = (x - (tipX - 10f)) / 10f;
                        Color tipCol = Color.Lerp(new Color(1f, 0.6f, 0.1f), new Color(1f, 1f, 0.9f), tipHeat);
                        tex.SetPixel(x, y, tipCol);
                    }
                    else if (x <= rearX + 4f)
                    {
                        // 후방 추진 분사구 (그을린 강철 & 내부 점화 불꽃)
                        tex.SetPixel(x, y, new Color(0.18f, 0.19f, 0.22f));
                    }
                    else
                    {
                        // 탄체 장갑판 및 리벳
                        float vertShading = 1.0f - (dy / halfH) * 0.4f;
                        float seam = (Mathf.Abs(x - cx) <= 1.2f) ? 0.65f : 1.0f; // 중앙 용접선

                        Color bodyCol = new Color(0.35f, 0.38f, 0.44f) * vertShading * seam;
                        tex.SetPixel(x, y, bodyCol);
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        #endregion

        #region Prefab Creation & Assignment

        /// <summary>
        /// 고품질 회전 톱날 탄환 프리팹 생성 (스프라이트, 콜라이더, 회전 스크립트, 트레일 탑재)
        /// </summary>
        public static void CreateOrUpdateBulletPrefab()
        {
            var sawSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SawSpritePath);
            if (sawSprite == null)
            {
                Debug.LogError($"[JunkBulletAssetGenerator] 스프라이트를 로드할 수 없습니다: {SawSpritePath}");
                return;
            }

            // 임시 GameObject 생성하여 컴포넌트 구성
            var rootObj = new GameObject("Boss_JunkSaw_Bullet");
            rootObj.transform.localScale = new Vector3(0.48f, 0.48f, 1.0f);

            // 1. SpriteRenderer
            var sr = rootObj.AddComponent<SpriteRenderer>();
            sr.sprite = sawSprite;
            sr.color = Color.white;
            sr.sortingOrder = 20; // 보스 및 플레이어 전면 렌더링

            // 2. CircleCollider2D (탄환 피격 판정)
            var col2d = rootObj.AddComponent<CircleCollider2D>();
            col2d.isTrigger = true;
            col2d.radius = 0.25f;

            // 3. Rigidbody2D (무중력 등속 비행)
            var rb2d = rootObj.AddComponent<Rigidbody2D>();
            rb2d.gravityScale = 0f;
            rb2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 4. 회전 스크립트 (초당 3바퀴 고속 회전 연출)
            var rotator = rootObj.AddComponent<BossRotatingProjectile>();

            // 5. 트레일 렌더러 (TrailRenderer) - 탄환 후방 화염/스파크 궤적
            var trail = rootObj.AddComponent<TrailRenderer>();
            trail.time = 0.16f;
            trail.startWidth = 0.30f;
            trail.endWidth = 0.0f;
            trail.autodestruct = false;
            trail.sortingOrder = 19;

            // 트레일 색상 그라데이션 (밝은 노랑-주황 -> 붉은 화염 -> 투명)
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1.0f, 0.90f, 0.30f), 0.0f),
                    new GradientColorKey(new Color(1.0f, 0.45f, 0.05f), 0.45f),
                    new GradientColorKey(new Color(0.85f, 0.12f, 0.02f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.85f, 0.0f),
                    new GradientAlphaKey(0.60f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            trail.colorGradient = grad;

            // 트레일 머티리얼 할당 (HSFiles의 Trail 머티리얼 또는 Sprites-Default)
            var trailMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Hovl Studio/HSFiles/Materials/Trail58cg.mat");
            if (trailMat != null)
            {
                trail.material = trailMat;
            }
            else
            {
                trail.material = new Material(Shader.Find("Sprites/Default"));
            }

            // 프리팹 파일 저장
            PrefabUtility.SaveAsPrefabAsset(rootObj, BulletPrefabPath);
            UnityEngine.Object.DestroyImmediate(rootObj);

            Debug.Log($"<color=cyan>[JunkBulletAssetGenerator] 탄환 프리팹 생성 완료: {BulletPrefabPath}</color>");
        }

        /// <summary>
        /// Boss1_JunkWheeler 프리팹의 JunkWheelerBarragePatternGimmick에 생성된 탄환 프리팹 및 머즐플래시 VFX 연결
        /// </summary>
        public static void LinkBulletToBossPrefab()
        {
            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (bossPrefab == null)
            {
                Debug.LogWarning($"[JunkBulletAssetGenerator] 보스 프리팹을 찾을 수 없습니다: {BossPrefabPath}");
                return;
            }

            var barrageGimmick = bossPrefab.GetComponent<JunkWheelerBarragePatternGimmick>();
            if (barrageGimmick == null)
            {
                Debug.LogWarning("[JunkBulletAssetGenerator] 보스 프리팹에서 JunkWheelerBarragePatternGimmick을 찾을 수 없습니다.");
                return;
            }

            var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPrefabPath);
            var muzzleVfx = AssetDatabase.LoadAssetAtPath<GameObject>(MuzzleFlashVfxPath);

            var so = new SerializedObject(barrageGimmick);
            so.Update();

            var bulletProp = so.FindProperty("bulletPrefab");
            if (bulletProp != null && bulletPrefab != null)
            {
                bulletProp.objectReferenceValue = bulletPrefab;
            }

            var muzzleProp = so.FindProperty("muzzleFlashVfxPrefab");
            if (muzzleProp != null && muzzleVfx != null)
            {
                muzzleProp.objectReferenceValue = muzzleVfx;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(bossPrefab);
            PrefabUtility.SavePrefabAsset(bossPrefab);

            Debug.Log($"<color=green>[JunkBulletAssetGenerator] 보스 프리팹에 탄환({BulletPrefabPath}) 및 머즐플래시 VFX 연동 성공!</color>");
        }

        #endregion
    }
}

