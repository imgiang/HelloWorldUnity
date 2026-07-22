using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Procedurally paints a set of stylized "Bug Vacuum Cannon" VFX textures (cyan magical-vacuum
/// energy, no fire/electricity) and imports them as sprites: Tools/Generate Bug Vacuum VFX Textures.
///
/// Scope note: only shapes that are fundamentally radial gradients/rings/rays/dots are generated
/// here (Muzzle Flash, Impact Ring, Hit Sparks, Swirl Particles, Capture Bubble, Vacuum Beam).
/// Ribbons, Suction Vortex, Suction Dust (leaves/petals/stones) and Energy Trails need actual
/// illustrated/organic linework that per-pixel procedural math can't convincingly fake - those are
/// intentionally not attempted here.
/// </summary>
public static class BugVacuumVfxGenerator
{
    private const string OutputFolder = "Assets/_Root/CatchBugGameplay/VFX/BugVacuum";
    private const string TracerPrefabPath = "Assets/_Root/CatchBugGameplay/VFX/Tracer.prefab";
    private const string VacuumBeamCoreMaterialPath = OutputFolder + "/VacuumBeamCore.mat";
    private const string SparkleInkTexturePath = "Assets/Eric VFX Studio/Resource/Textures/Sparkle_Ink_001.png";
    private const string SparkleInkMaterialPath = OutputFolder + "/SparkleInkLine.mat";

    private static readonly Color CyanMain1 = HexColor("#3FE8FF");
    private static readonly Color CyanMain2 = HexColor("#00CFFF");
    private static readonly Color CyanMain3 = HexColor("#7EF7FF");
    private static readonly Color White = HexColor("#FFFFFF");
    private static readonly Color LightCyan = HexColor("#A6F4FF");
    private static readonly Color AccentGreen = HexColor("#8CFF5D");

    private static readonly string[] GeneratedFileNames =
    {
        "MuzzleFlash.png", "ImpactRing.png", "HitSparksAtlas.png",
        "SwirlParticlesAtlas.png", "CaptureBubble.png", "VacuumBeam.png",
    };

    [MenuItem("Tools/Generate Bug Vacuum VFX Textures")]
    public static void GenerateAll()
    {
        EnsureFolder(OutputFolder);

        SaveTexture(GenerateMuzzleFlash(512), "MuzzleFlash.png");
        SaveTexture(GenerateImpactRing(512), "ImpactRing.png");
        SaveTexture(GenerateHitSparksAtlas(512), "HitSparksAtlas.png");
        SaveTexture(GenerateSwirlParticlesAtlas(1024), "SwirlParticlesAtlas.png");
        SaveTexture(GenerateCaptureBubble(1024), "CaptureBubble.png");
        SaveTexture(GenerateVacuumBeam(1024, 256), "VacuumBeam.png");

        AssetDatabase.Refresh();
        ConfigureAllAsSprites();
        bool wiredTracer = ApplyVacuumBeamToTracer();

        EditorUtility.DisplayDialog(
            "Bug Vacuum VFX",
            $"Generated 6 procedural textures into {OutputFolder}:\n" +
            "MuzzleFlash, ImpactRing, HitSparksAtlas, SwirlParticlesAtlas, CaptureBubble, VacuumBeam (tiles horizontally).\n\n" +
            "Not generated (need actual illustration, not procedural math): Vacuum Ribbons, Suction Vortex, " +
            "Suction Dust (leaves/petals/stones), Energy Trails.\n\n" +
            (wiredTracer
                ? "Also applied VacuumBeam.png (Tile mode) to Tracer.prefab's Core LineRenderer, so the fired beam now uses this texture instead of a flat line."
                : $"Tracer.prefab not found at {TracerPrefabPath} - texture was not auto-applied to it."),
            "OK");
    }

    // --- 1. Muzzle Flash: glowing ring + center flash + soft radial rays + small sparks --------

    private static Texture2D GenerateMuzzleFlash(int size)
    {
        Accum[] buf = new Accum[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxR = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = (y * size) + x;
                float dx = (x + 0.5f) - center.x;
                float dy = (y + 0.5f) - center.y;
                float dist = Mathf.Sqrt((dx * dx) + (dy * dy));
                float angle = Mathf.Atan2(dy, dx);

                // Soft radial rays (12 of them), strongest mid-radius, fading toward the edge.
                float rayMask = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 6f)), 6f);
                float rayFalloff = SmoothStep(maxR * 0.9f, maxR * 0.1f, dist);
                AddGlow(buf, idx, CyanMain3, rayMask * rayFalloff * 0.5f);

                // Glowing ring.
                float ringR = maxR * 0.42f;
                float ringIntensity = Gaussian(dist - ringR, maxR * 0.07f);
                AddGlow(buf, idx, CyanMain2, ringIntensity * 0.9f);
                AddGlow(buf, idx, LightCyan, ringIntensity * 0.45f);

                // Center flash (compressed energy core).
                float core = SmoothStep(maxR * 0.32f, 0f, dist);
                AddGlow(buf, idx, White, core);
                AddGlow(buf, idx, CyanMain1, core * 0.6f);
            }
        }

        System.Random rng = new System.Random(101);
        for (int i = 0; i < 18; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
            float radius = maxR * (0.35f + ((float)rng.NextDouble() * 0.5f));
            Vector2 pos = center + (new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            float sparkSize = maxR * (0.015f + ((float)rng.NextDouble() * 0.02f));
            DrawSoftDot(buf, size, pos, sparkSize, White, 1f);
            DrawSoftDot(buf, size, pos, sparkSize * 2f, CyanMain3, 0.5f);
        }

        return BuildTexture(buf, size, size);
    }

    // --- 2. Impact Ring: bright edge + transparent center + ripples + sparks ------------------

    private static Texture2D GenerateImpactRing(int size)
    {
        Accum[] buf = new Accum[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxR = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = (y * size) + x;
                float dx = (x + 0.5f) - center.x;
                float dy = (y + 0.5f) - center.y;
                float dist = Mathf.Sqrt((dx * dx) + (dy * dy));

                float edgeR = maxR * 0.82f;
                float edgeIntensity = Gaussian(dist - edgeR, maxR * 0.05f);
                AddGlow(buf, idx, White, edgeIntensity * 0.7f);
                AddGlow(buf, idx, CyanMain2, edgeIntensity);

                for (int r = 0; r < 2; r++)
                {
                    float rippleR = maxR * (0.45f + (r * 0.18f));
                    float rippleIntensity = Gaussian(dist - rippleR, maxR * 0.035f) * (0.35f - (r * 0.1f));
                    AddGlow(buf, idx, CyanMain3, rippleIntensity);
                }
            }
        }

        System.Random rng = new System.Random(202);
        for (int i = 0; i < 14; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
            float radius = maxR * (0.78f + ((float)rng.NextDouble() * 0.12f));
            Vector2 pos = center + (new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            float s = maxR * (0.01f + ((float)rng.NextDouble() * 0.015f));
            DrawSoftDot(buf, size, pos, s, White, 1f);
            DrawSoftDot(buf, size, pos, s * 2f, CyanMain1, 0.5f);
        }

        return BuildTexture(buf, size, size);
    }

    // --- 3. Hit Sparks atlas: stars, line bursts, circles, fragments, double rings -------------

    private static Texture2D GenerateHitSparksAtlas(int size)
    {
        Accum[] buf = new Accum[size * size];
        const int gridDim = 4;
        int cell = size / gridDim;

        for (int cellIndex = 0; cellIndex < gridDim * gridDim; cellIndex++)
        {
            int cx = ((cellIndex % gridDim) * cell) + (cell / 2);
            int cy = ((cellIndex / gridDim) * cell) + (cell / 2);
            Vector2 cellCenter = new Vector2(cx, cy);
            float cellRadius = cell * 0.42f;
            int shapeType = cellIndex % 6;
            DrawHitSparkShape(buf, size, cellCenter, cellRadius, shapeType);
        }

        return BuildTexture(buf, size, size);
    }

    private static void DrawHitSparkShape(Accum[] buf, int size, Vector2 center, float radius, int shapeType)
    {
        ForEachPixelInBox(size, center, radius * 1.6f, (idx, dx, dy) =>
        {
            float dist = Mathf.Sqrt((dx * dx) + (dy * dy));
            float angle = Mathf.Atan2(dy, dx);

            switch (shapeType)
            {
                case 0: // 4-point star
                {
                    float starR = radius * (0.25f + (0.75f * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f)), 0.5f)));
                    float a = SmoothStep(starR, starR * 0.6f, dist);
                    AddGlow(buf, idx, White, a);
                    AddGlow(buf, idx, CyanMain1, a * 0.6f);
                    AddGlow(buf, idx, CyanMain3, Gaussian(dist, radius * 0.9f) * 0.25f);
                    break;
                }

                case 1: // 6-point star
                {
                    float starR = radius * (0.3f + (0.7f * Mathf.Abs(Mathf.Cos(angle * 3f))));
                    float a = SmoothStep(starR, starR * 0.55f, dist);
                    AddGlow(buf, idx, White, a);
                    AddGlow(buf, idx, CyanMain2, a * 0.6f);
                    break;
                }

                case 2: // line burst (cross)
                {
                    float lineWidth = radius * 0.14f;
                    bool onH = Mathf.Abs(dy) < lineWidth && Mathf.Abs(dx) < radius;
                    bool onV = Mathf.Abs(dx) < lineWidth && Mathf.Abs(dy) < radius;
                    float fade = 1f - Mathf.Clamp01(dist / radius);
                    if (onH || onV)
                    {
                        AddGlow(buf, idx, White, fade);
                        AddGlow(buf, idx, CyanMain1, fade * 0.6f);
                    }

                    AddGlow(buf, idx, CyanMain3, Gaussian(dist, radius * 0.5f) * 0.15f);
                    break;
                }

                case 3: // small circle + glow
                {
                    float coreA = SmoothStep(radius * 0.35f, 0f, dist);
                    float glowA = Gaussian(dist, radius * 0.5f) * 0.4f;
                    AddGlow(buf, idx, White, coreA);
                    AddGlow(buf, idx, CyanMain2, glowA);
                    break;
                }

                case 4: // tiny fragment
                {
                    float starR = radius * (0.3f + (0.7f * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(angle * 1.5f)), 2f)));
                    float a = SmoothStep(starR, starR * 0.6f, dist);
                    AddGlow(buf, idx, White, a);
                    AddGlow(buf, idx, AccentGreen, a * 0.35f);
                    break;
                }

                default: // double ring spark
                {
                    float ring1 = Gaussian(dist - (radius * 0.55f), radius * 0.08f);
                    float ring2 = Gaussian(dist - (radius * 0.3f), radius * 0.06f);
                    AddGlow(buf, idx, CyanMain2, ring1 * 0.8f);
                    AddGlow(buf, idx, White, ring2 * 0.7f);
                    break;
                }
            }
        });
    }

    // --- 4. Swirl Particles atlas: dots, sparks, streaks, fragments ---------------------------

    private static Texture2D GenerateSwirlParticlesAtlas(int size)
    {
        Accum[] buf = new Accum[size * size];
        const int gridDim = 8;
        int cell = size / gridDim;
        System.Random rng = new System.Random(404);

        for (int cellIndex = 0; cellIndex < gridDim * gridDim; cellIndex++)
        {
            int cx = ((cellIndex % gridDim) * cell) + (cell / 2);
            int cy = ((cellIndex / gridDim) * cell) + (cell / 2);
            Vector2 center = new Vector2(cx, cy);
            float cellRadius = cell * 0.38f;
            int variant = cellIndex % 4;
            DrawSwirlParticle(buf, size, center, cellRadius, variant, rng);
        }

        return BuildTexture(buf, size, size);
    }

    private static void DrawSwirlParticle(Accum[] buf, int size, Vector2 center, float radius, int variant, System.Random rng)
    {
        float streakAngle = (float)(rng.NextDouble() * Math.PI * 2.0);

        ForEachPixelInBox(size, center, radius * 2f, (idx, dx, dy) =>
        {
            float dist = Mathf.Sqrt((dx * dx) + (dy * dy));

            switch (variant)
            {
                case 0: // small dot
                {
                    float a = SmoothStep(radius * 0.5f, 0f, dist);
                    float glow = Gaussian(dist, radius * 0.7f) * 0.4f;
                    AddGlow(buf, idx, White, a);
                    AddGlow(buf, idx, CyanMain1, glow);
                    break;
                }

                case 1: // energy spark
                {
                    float angle = Mathf.Atan2(dy, dx);
                    float starR = radius * (0.2f + (0.8f * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f)), 0.6f)));
                    float a = SmoothStep(starR, starR * 0.5f, dist);
                    AddGlow(buf, idx, White, a);
                    AddGlow(buf, idx, CyanMain2, a * 0.6f);
                    break;
                }

                case 2: // tiny streak
                {
                    float cosA = Mathf.Cos(-streakAngle);
                    float sinA = Mathf.Sin(-streakAngle);
                    float rx = (dx * cosA) - (dy * sinA);
                    float ry = (dx * sinA) + (dy * cosA);
                    float lengthFalloff = SmoothStep(radius * 1.6f, 0f, Mathf.Abs(rx));
                    float widthFalloff = Gaussian(ry, radius * 0.18f);
                    float a = lengthFalloff * widthFalloff;
                    AddGlow(buf, idx, CyanMain3, a);
                    AddGlow(buf, idx, White, a * 0.5f);
                    break;
                }

                default: // glowing fragment (two overlapping soft blobs)
                {
                    float a1 = Gaussian(dist, radius * 0.35f);
                    Vector2 offset = new Vector2(radius * 0.3f, radius * 0.2f);
                    float dist2 = Vector2.Distance(new Vector2(dx, dy), offset);
                    float a2 = Gaussian(dist2, radius * 0.28f);
                    float a = Mathf.Max(a1, a2);
                    AddGlow(buf, idx, LightCyan, a);
                    AddGlow(buf, idx, White, a * 0.5f);
                    break;
                }
            }
        });
    }

    // --- 5. Capture Bubble: double outline + glow + shine + floating particles ----------------

    private static Texture2D GenerateCaptureBubble(int size)
    {
        Accum[] buf = new Accum[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxR = size / 2f;
        float outerR = maxR * 0.9f;
        float innerR = maxR * 0.82f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = (y * size) + x;
                float dx = (x + 0.5f) - center.x;
                float dy = (y + 0.5f) - center.y;
                float dist = Mathf.Sqrt((dx * dx) + (dy * dy));
                float angleDeg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

                float outerIntensity = Gaussian(dist - outerR, maxR * 0.02f);
                float innerIntensity = Gaussian(dist - innerR, maxR * 0.02f);
                AddGlow(buf, idx, White, outerIntensity * 0.8f);
                AddGlow(buf, idx, CyanMain2, outerIntensity);
                AddGlow(buf, idx, CyanMain1, innerIntensity * 0.8f);

                float fill = SmoothStep(maxR * 0.3f, outerR, dist) * (1f - SmoothStep(outerR, maxR, dist));
                AddGlow(buf, idx, CyanMain3, fill * 0.12f);

                float angleDiffDeg = Mathf.DeltaAngle(angleDeg, -125f);
                float shineAngularFalloff = Gaussian(angleDiffDeg, 20f);
                float shineRadialBand = Gaussian(dist - ((innerR + outerR) * 0.5f), maxR * 0.05f);
                AddGlow(buf, idx, White, shineAngularFalloff * shineRadialBand * 0.9f);
            }
        }

        System.Random rng = new System.Random(505);
        for (int i = 0; i < 10; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
            float radius = maxR * (0.15f + ((float)rng.NextDouble() * 0.55f));
            Vector2 pos = center + (new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            float s = maxR * (0.01f + ((float)rng.NextDouble() * 0.02f));
            DrawSoftDot(buf, size, pos, s, White, 0.9f);
            DrawSoftDot(buf, size, pos, s * 2.2f, LightCyan, 0.4f);
        }

        return BuildTexture(buf, size, size);
    }

    // --- 6. Vacuum Beam: bright core + outer glow, tileable horizontally ----------------------

    private static Texture2D GenerateVacuumBeam(int width, int height)
    {
        Accum[] buf = new Accum[width * height];
        float centerY = height / 2f;

        for (int x = 0; x < width; x++)
        {
            float t = (float)x / width;

            // Integer periods over the full width so the wobble/noise wraps seamlessly.
            float wobble = (Mathf.Sin(t * Mathf.PI * 2f * 3f) * height * 0.06f)
                + (Mathf.Sin((t * Mathf.PI * 2f * 7f) + 1.3f) * height * 0.03f);
            float coreCenterY = centerY + wobble;

            float noise = 1f + ((Mathf.Sin((t * Mathf.PI * 2f * 11f) + 0.7f) * 0.5f
                + (Mathf.Sin((t * Mathf.PI * 2f * 17f) + 2.1f) * 0.5f)) * 0.15f);

            for (int y = 0; y < height; y++)
            {
                int idx = (y * width) + x;
                float dy = (y + 0.5f) - coreCenterY;

                float core = Gaussian(dy, height * 0.06f) * noise;
                float glow = Gaussian(dy, height * 0.22f) * 0.5f * noise;

                AddGlow(buf, idx, White, core * 0.9f);
                AddGlow(buf, idx, CyanMain1, core * 0.6f);
                AddGlow(buf, idx, CyanMain2, glow);
                AddGlow(buf, idx, CyanMain3, glow * 0.6f);
            }
        }

        return BuildTexture(buf, width, height);
    }

    // --- Shared drawing/accumulation helpers ---------------------------------------------------

    private struct Accum
    {
        public float R;
        public float G;
        public float B;
        public float A;
    }

    private static void AddGlow(Accum[] buf, int idx, Color color, float intensity)
    {
        if (intensity <= 0f)
        {
            return;
        }

        buf[idx].R += color.r * intensity;
        buf[idx].G += color.g * intensity;
        buf[idx].B += color.b * intensity;
        buf[idx].A = Mathf.Max(buf[idx].A, intensity);
    }

    private static void DrawSoftDot(Accum[] buf, int size, Vector2 pos, float radius, Color color, float intensity)
    {
        ForEachPixelInBox(size, pos, radius * 3f, (idx, dx, dy) =>
        {
            float dist = Mathf.Sqrt((dx * dx) + (dy * dy));
            AddGlow(buf, idx, color, Gaussian(dist, radius) * intensity);
        });
    }

    private delegate void PixelAction(int idx, float dx, float dy);

    private static void ForEachPixelInBox(int size, Vector2 center, float halfExtent, PixelAction action)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - halfExtent));
        int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(center.x + halfExtent));
        int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - halfExtent));
        int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(center.y + halfExtent));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = (x + 0.5f) - center.x;
                float dy = (y + 0.5f) - center.y;
                action((y * size) + x, dx, dy);
            }
        }
    }

    private static float Gaussian(float x, float sigma)
    {
        if (sigma <= 0.0001f)
        {
            return 0f;
        }

        return Mathf.Exp(-(x * x) / (2f * sigma * sigma));
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - (2f * t));
    }

    private static Texture2D BuildTexture(Accum[] buf, int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[buf.Length];
        for (int i = 0; i < buf.Length; i++)
        {
            pixels[i] = new Color(
                Mathf.Clamp01(buf[i].R),
                Mathf.Clamp01(buf[i].G),
                Mathf.Clamp01(buf[i].B),
                Mathf.Clamp01(buf[i].A));
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static Color HexColor(string hex)
    {
        hex = hex.TrimStart('#');
        byte r = Convert.ToByte(hex.Substring(0, 2), 16);
        byte g = Convert.ToByte(hex.Substring(2, 2), 16);
        byte b = Convert.ToByte(hex.Substring(4, 2), 16);
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }

    // --- Asset plumbing -------------------------------------------------------------------------

    private static void SaveTexture(Texture2D texture, string fileName)
    {
        string assetPath = OutputFolder + "/" + fileName;
        byte[] png = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);

        string fullPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        File.WriteAllBytes(fullPath, png);
    }

    private static void ConfigureAllAsSprites()
    {
        foreach (string fileName in GeneratedFileNames)
        {
            string assetPath = OutputFolder + "/" + fileName;
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (importer == null)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = fileName == "VacuumBeam.png" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }

    private static void EnsureFolder(string assetFolderPath)
    {
        if (AssetDatabase.IsValidFolder(assetFolderPath))
        {
            return;
        }

        string[] parts = assetFolderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    // --- Wiring: try Sparkle_Ink_001.png (tiled) on the existing Tracer's Core LineRenderer -----
    // Unlike FX_LootDrop_Blue, this is a plain texture applied to our own LineRenderer, which
    // already tracks muzzle->hit point exactly every shot - so start/end control isn't a concern
    // here, only whether the tiled wisp shape reads well stretched along the beam.

    [MenuItem("Tools/Try Sparkle Ink Line Texture")]
    public static void TrySparkleInkLineTexture()
    {
        GameObject tracerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TracerPrefabPath);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SparkleInkTexturePath);

        if (tracerPrefab == null || texture == null)
        {
            EditorUtility.DisplayDialog(
                "Sparkle Ink Line",
                $"Could not apply - check that Tracer.prefab and '{SparkleInkTexturePath}' both exist.",
                "OK");
            return;
        }

        Material material = CreateAdditiveLineMaterial(SparkleInkMaterialPath, "SparkleInkLine", texture);
        ApplyMaterialToTracerCore(material, tileScaleX: 3f);

        EditorUtility.DisplayDialog(
            "Sparkle Ink Line",
            "Applied Sparkle_Ink_001 (tiled x3 along the beam) to Tracer.prefab's Core LineRenderer, replacing " +
            "whatever it used before. This is a wisp/comet-tail shape, not a straight bar, so it repeats as a " +
            "string of tapered wisps rather than one smooth line - if the repeat looks too sparse/dense, change " +
            "the '3f' tileScaleX in TrySparkleInkLineTexture() and rerun.\n\n" +
            "Run Tools/Generate Bug Vacuum VFX Textures again afterward if you want to switch the Core back to " +
            "the procedural VacuumBeam texture instead.",
            "OK");
    }

    private static void ApplyMaterialToTracerCore(Material material, float tileScaleX)
    {
        GameObject editablePrefab = PrefabUtility.LoadPrefabContents(TracerPrefabPath);
        try
        {
            LineRenderer core = editablePrefab.GetComponent<LineRenderer>();
            if (core == null)
            {
                return;
            }

            core.material = material;
            core.textureMode = LineTextureMode.Tile;
            core.textureScale = new Vector2(tileScaleX, 1f);

            PrefabUtility.SaveAsPrefabAsset(editablePrefab, TracerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(editablePrefab);
        }
    }

    private static Material CreateAdditiveLineMaterial(string path, string name, Texture2D texture)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material material = new Material(shader) { name = name };
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetTexture("_BaseMap", texture);

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // --- Wiring: apply VacuumBeam.png (tiled) to the existing Tracer's Core LineRenderer -------

    private static bool ApplyVacuumBeamToTracer()
    {
        GameObject tracerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TracerPrefabPath);
        if (tracerPrefab == null)
        {
            return false;
        }

        Texture2D beamTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(OutputFolder + "/VacuumBeam.png");
        if (beamTexture == null)
        {
            return false;
        }

        Material coreMaterial = CreateVacuumBeamCoreMaterial(beamTexture);

        GameObject editablePrefab = PrefabUtility.LoadPrefabContents(TracerPrefabPath);
        try
        {
            LineRenderer core = editablePrefab.GetComponent<LineRenderer>();
            if (core == null)
            {
                return false;
            }

            core.material = coreMaterial;
            core.textureMode = LineTextureMode.Tile;
            core.textureScale = new Vector2(1f, 1f);

            PrefabUtility.SaveAsPrefabAsset(editablePrefab, TracerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(editablePrefab);
        }

        return true;
    }

    private static Material CreateVacuumBeamCoreMaterial(Texture2D beamTexture)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(VacuumBeamCoreMaterialPath);
        if (existing != null)
        {
            existing.SetTexture("_BaseMap", beamTexture);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material material = new Material(shader) { name = "VacuumBeamCore" };
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetTexture("_BaseMap", beamTexture);

        AssetDatabase.CreateAsset(material, VacuumBeamCoreMaterialPath);
        return material;
    }
}
