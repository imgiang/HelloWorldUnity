using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds the Cannon_12 view-model, a screen-center UI crosshair, an in-world Aim Target marker, and
/// a procedurally-built set of firing VFX (muzzle flash, tracer, impact) to the currently open
/// scene: Tools/Add Cannon View. Like TouchControlsBuilder, this only adds to what's open, it does
/// not create or overwrite a scene. The cannon becomes a child of the Main Camera (so it rides
/// along at the bottom of the screen) and a Muzzle child fire point is aligned with the camera's
/// own forward direction, which is also exactly where the crosshair points and where the Aim
/// Target is placed every frame (see CannonFireSystem for the raycast that finds that point) - so
/// firing always visibly connects the muzzle to whatever the Aim Target is currently over.
/// </summary>
public static class CannonViewBuilder
{
    private const string CannonPrefabPath = "Assets/Stylish Cannon Pack/Prefabs/Cannon_12.prefab";
    private const string VfxFolder = "Assets/_Root/CatchBugGameplay/VFX";
    private const string MuzzleFlashPrefabPath = VfxFolder + "/MuzzleFlash.prefab";
    private const string ImpactPrefabPath = VfxFolder + "/ImpactHit.prefab";
    private const string TracerPrefabPath = VfxFolder + "/Tracer.prefab";
    private const string AdditiveMaterialPath = VfxFolder + "/MuzzleFlashAdditive.mat";
    private const string SmokeMaterialPath = VfxFolder + "/MuzzleFlashSmoke.mat";
    private const string TracerMaterialPath = VfxFolder + "/TracerBeam.mat";

    private const string UiFolder = "Assets/_Root/CatchBugGameplay/UI";
    private const string CrosshairTexturePath = UiFolder + "/Crosshair.png";
    private const string HudCanvasName = "HUD";

    private const float TargetViewmodelLength = 0.6f;
    private const float AimTargetWorldScale = 0.6f;
    private static readonly Vector3 CannonLocalPosition = new Vector3(0f, -0.35f, 0.6f);
    private static readonly Vector3 MuzzleLocalPosition = new Vector3(0f, -0.15f, 1.1f);

    [MenuItem("Tools/Add Cannon View")]
    public static void AddCannonView()
    {
        Camera mainCamera = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (mainCamera == null)
        {
            EditorUtility.DisplayDialog("Add Cannon View", "No Camera found in the open scene. Open Gameplay.unity first.", "OK");
            return;
        }

        if (mainCamera.transform.Find("Cannon_12") != null)
        {
            EditorUtility.DisplayDialog("Add Cannon View", "The camera already has a Cannon_12 child. Remove it first if you want to rebuild it.", "OK");
            return;
        }

        GameObject cannonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CannonPrefabPath);
        if (cannonPrefab == null)
        {
            EditorUtility.DisplayDialog("Add Cannon View", $"Could not find '{CannonPrefabPath}'.", "OK");
            return;
        }

        GameObject cannon = (GameObject)PrefabUtility.InstantiatePrefab(cannonPrefab);
        Undo.RegisterCreatedObjectUndo(cannon, "Add Cannon View");
        cannon.transform.SetParent(mainCamera.transform, worldPositionStays: false);
        cannon.transform.localPosition = CannonLocalPosition;
        cannon.transform.localRotation = Quaternion.identity;
        cannon.transform.localScale = Vector3.one * CalculateViewmodelScale(cannon);

        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(mainCamera.transform, worldPositionStays: false);
        muzzle.transform.localPosition = MuzzleLocalPosition;
        muzzle.transform.localRotation = Quaternion.identity;

        GameObject muzzleFlashPrefab = EnsureMuzzleFlashPrefab();
        GameObject impactPrefab = EnsureImpactPrefab();
        GameObject tracerPrefab = EnsureTracerPrefab();
        EnsureCrosshairUI();
        Transform aimTarget = EnsureWorldAimTarget();

        CannonView cannonView = cannon.AddComponent<CannonView>();
        SerializedObject serializedView = new SerializedObject(cannonView);
        serializedView.FindProperty("_muzzle").objectReferenceValue = muzzle.transform;
        serializedView.FindProperty("_muzzleFlashPrefab").objectReferenceValue = muzzleFlashPrefab;
        serializedView.FindProperty("_impactPrefab").objectReferenceValue = impactPrefab;
        serializedView.FindProperty("_tracerPrefab").objectReferenceValue = tracerPrefab;
        serializedView.FindProperty("_aimTarget").objectReferenceValue = aimTarget;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = cannon;

        EditorUtility.DisplayDialog(
            "Add Cannon View",
            "Added Cannon_12 as a camera view-model (bottom of screen) with a Muzzle fire point aimed " +
            "along the camera's forward direction (screen center, same place as the new UI crosshair). " +
            "An 'Aim Target' marker was also added to the scene - it follows whatever the crosshair is " +
            "over every frame, so you can see in the 3D world exactly where a shot would land.\n\n" +
            "The placement is a first guess - tweak the cannon's local position/scale and the Muzzle's " +
            "local position in the Inspector while in Play Mode to line them up visually, then save the scene.\n\n" +
            "Fire (right mouse button / gamepad right trigger) spawns the impact VFX + tracer at whatever " +
            "the Aim Target is currently over (or at max range if nothing does).",
            "OK");
    }

    private static float CalculateViewmodelScale(GameObject cannon)
    {
        Renderer[] renderers = cannon.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return 1f;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        return largestDimension > 0.001f ? TargetViewmodelLength / largestDimension : 1f;
    }

    // --- Muzzle flash (Flash + Sparks + Smoke) ---------------------------------------------

    private static GameObject EnsureMuzzleFlashPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(MuzzleFlashPrefabPath);
        if (existing != null)
        {
            return existing;
        }

        EnsureFolder(VfxFolder);

        Material additiveMaterial = CreateParticleMaterial(AdditiveMaterialPath, additive: true);
        Material smokeMaterial = CreateParticleMaterial(SmokeMaterialPath, additive: false);

        GameObject root = new GameObject("MuzzleFlash");
        SetDestroyAfter(root, 1.2f);

        BuildFlashParticles(root.transform, additiveMaterial);
        BuildSparkParticles(root.transform, additiveMaterial);
        BuildSmokeParticles(root.transform, smokeMaterial);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, MuzzleFlashPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void BuildFlashParticles(Transform parent, Material material)
    {
        GameObject go = new GameObject("Flash");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = true;
        main.startDelay = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.12f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startColor = new Color(1f, 0.75f, 0.3f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 10;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.02f;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ApplyFadeOutColor(ps);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    private static void BuildSparkParticles(Transform parent, Material material)
    {
        GameObject go = new GameObject("Sparks");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new Color(1f, 0.55f, 0.15f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0.5f;
        main.maxParticles = 30;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.02f;

        ApplyFadeOutColor(ps);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    private static void BuildSmokeParticles(Transform parent, Material material)
    {
        GameObject go = new GameObject("Smoke");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.6f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        main.startColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 10;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 4) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 2.5f));

        ApplyFadeOutColor(ps);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    // --- Impact hit (Sparks + Dust), spawned at whatever the raycast hits -------------------

    private static GameObject EnsureImpactPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefabPath);
        if (existing != null)
        {
            return existing;
        }

        EnsureFolder(VfxFolder);

        Material additiveMaterial = CreateParticleMaterial(AdditiveMaterialPath, additive: true);
        Material smokeMaterial = CreateParticleMaterial(SmokeMaterialPath, additive: false);

        GameObject root = new GameObject("ImpactHit");
        SetDestroyAfter(root, 1f);

        BuildImpactSparks(root.transform, additiveMaterial);
        BuildImpactDust(root.transform, smokeMaterial);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ImpactPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void BuildImpactSparks(Transform parent, Material material)
    {
        GameObject go = new GameObject("ImpactSparks");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new Color(1f, 0.6f, 0.2f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0.8f;
        main.maxParticles = 20;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

        // Cone points along local +Z; the whole prefab instance is rotated (see CannonView.Fire)
        // to face away from the shot direction, so sparks spread away from the hit surface.
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.03f;

        ApplyFadeOutColor(ps);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    private static void BuildImpactDust(Transform parent, Material material)
    {
        GameObject go = new GameObject("ImpactDust");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        main.startColor = new Color(0.55f, 0.5f, 0.45f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 8;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.05f;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 2f));

        ApplyFadeOutColor(ps);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    // --- Tracer: an instant beam connecting the muzzle to the hit point ---------------------

    private static GameObject EnsureTracerPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TracerPrefabPath);
        if (existing != null)
        {
            return existing;
        }

        EnsureFolder(VfxFolder);
        Material tracerMaterial = CreateBeamMaterial(TracerMaterialPath);

        // Layered like a glowing energy beam: a thin, HDR-bright core plus a wider, softer,
        // lower-alpha halo underneath. Both layers get repositioned together in CannonView.Fire.
        GameObject root = new GameObject("Tracer");
        LineRenderer core = root.AddComponent<LineRenderer>();
        ConfigureTracerLine(core, tracerMaterial, sortingOrder: 1,
            startWidth: 0.035f, endWidth: 0.015f,
            startColor: new Color(3f, 2.8f, 2f, 1f),
            endColor: new Color(2.5f, 1.2f, 0.4f, 0.9f));

        GameObject glow = new GameObject("Glow");
        glow.transform.SetParent(root.transform, false);
        LineRenderer glowLine = glow.AddComponent<LineRenderer>();
        ConfigureTracerLine(glowLine, tracerMaterial, sortingOrder: 0,
            startWidth: 0.18f, endWidth: 0.07f,
            startColor: new Color(1f, 0.6f, 0.2f, 0.35f),
            endColor: new Color(1f, 0.35f, 0.1f, 0.22f));

        SetDestroyAfter(root, 0.15f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TracerPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void ConfigureTracerLine(LineRenderer lineRenderer, Material material, int sortingOrder, float startWidth, float endWidth, Color startColor, Color endColor)
    {
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.material = material;
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;
        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
        lineRenderer.numCapVertices = 4;
        lineRenderer.sortingOrder = sortingOrder;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
    }

    // --- Shared particle helpers -------------------------------------------------------------

    private static void ApplyFadeOutColor(ParticleSystem ps)
    {
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;
    }

    private static void SetDestroyAfter(GameObject go, float seconds)
    {
        DestroyAfterSeconds destroyAfterSeconds = go.AddComponent<DestroyAfterSeconds>();
        SerializedObject serializedDestroy = new SerializedObject(destroyAfterSeconds);
        serializedDestroy.FindProperty("_seconds").floatValue = seconds;
        serializedDestroy.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material CreateParticleMaterial(string path, bool additive)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material material = new Material(shader)
        {
            name = Path.GetFileNameWithoutExtension(path),
        };

        material.SetFloat("_Surface", 1f); // Transparent
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));

        if (additive)
        {
            material.SetFloat("_Blend", 2f); // Additive
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
        }
        else
        {
            material.SetFloat("_Blend", 0f); // Alpha
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // Additive, but deliberately leaves _BaseMap unset (shader default is a solid white texture)
    // instead of the soft round particle sprite used above - a LineRenderer stretches that single
    // texture across the entire line length, so most of a multi-meter beam would render fully
    // transparent except for a small bright patch. A flat white base map lets the width curve and
    // color gradient alone define the beam's look, which stays solid along its whole length.
    private static Material CreateBeamMaterial(string path)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material material = new Material(shader)
        {
            name = Path.GetFileNameWithoutExtension(path),
        };

        material.SetFloat("_Surface", 1f); // Transparent
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetFloat("_Blend", 2f); // Additive
        material.SetFloat("_SrcBlend", (float)BlendMode.One);
        material.SetFloat("_DstBlend", (float)BlendMode.One);

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // --- Crosshair: a procedurally-drawn target reticle at screen center -------------------

    private static void EnsureCrosshairUI()
    {
        Canvas canvas = FindOrCreateHudCanvas();

        if (canvas.transform.Find("Crosshair") != null)
        {
            return;
        }

        Sprite crosshairSprite = EnsureCrosshairSprite();

        GameObject crosshair = new GameObject("Crosshair", typeof(Image));
        Undo.RegisterCreatedObjectUndo(crosshair, "Add Crosshair");
        crosshair.transform.SetParent(canvas.transform, false);

        Image image = crosshair.GetComponent<Image>();
        image.sprite = crosshairSprite;
        image.color = Color.white;
        image.raycastTarget = false;

        RectTransform rectTransform = crosshair.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(48f, 48f);
    }

    // --- World aim target: the same reticle, but placed in the 3D scene at whatever the
    // crosshair is currently over (see CannonFireSystem/CannonView.UpdateAimTarget), instead of
    // fixed on screen. Billboarded to always face the camera.

    private static Transform EnsureWorldAimTarget()
    {
        GameObject existing = GameObject.Find("Aim Target");
        if (existing != null)
        {
            return existing.transform;
        }

        Sprite crosshairSprite = EnsureCrosshairSprite();

        GameObject aimTarget = new GameObject("Aim Target", typeof(SpriteRenderer));
        Undo.RegisterCreatedObjectUndo(aimTarget, "Add Aim Target");

        SpriteRenderer spriteRenderer = aimTarget.GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = crosshairSprite;
        spriteRenderer.color = new Color(1f, 0.25f, 0.2f, 0.9f);

        aimTarget.transform.localScale = Vector3.one * AimTargetWorldScale;

        return aimTarget.transform;
    }

    private static Canvas FindOrCreateHudCanvas()
    {
        GameObject existingHud = GameObject.Find(HudCanvasName);
        if (existingHud != null && existingHud.TryGetComponent(out Canvas existingCanvas))
        {
            return existingCanvas;
        }

        GameObject canvasGameObject = new GameObject(HudCanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGameObject, "Add HUD Canvas");

        Canvas canvas = canvasGameObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGameObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static Sprite EnsureCrosshairSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(CrosshairTexturePath);
        if (existing != null)
        {
            return existing;
        }

        EnsureFolder(UiFolder);

        Texture2D texture = DrawCrosshairTexture(64);
        byte[] pngData = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);

        string fullPath = Path.Combine(Application.dataPath, CrosshairTexturePath.Substring("Assets/".Length));
        File.WriteAllBytes(fullPath, pngData);
        AssetDatabase.ImportAsset(CrosshairTexturePath);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(CrosshairTexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(CrosshairTexturePath);
    }

    private static Texture2D DrawCrosshairTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color lineColor = Color.white;

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerRadius = size * 0.42f;
        float ringThickness = size * 0.05f;
        float dotRadius = size * 0.04f;
        float tickGap = size * 0.06f;
        float tickLength = size * 0.16f;
        float tickThickness = size * 0.055f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) - center.x;
                float dy = (y + 0.5f) - center.y;
                float distanceFromCenter = Mathf.Sqrt(dx * dx + dy * dy);

                bool onRing = Mathf.Abs(distanceFromCenter - outerRadius) <= ringThickness * 0.5f;
                bool onDot = distanceFromCenter <= dotRadius;

                float tickInner = outerRadius + tickGap;
                float tickOuter = tickInner + tickLength;
                bool onVerticalTick = Mathf.Abs(dx) <= tickThickness * 0.5f && Mathf.Abs(dy) >= tickInner && Mathf.Abs(dy) <= tickOuter;
                bool onHorizontalTick = Mathf.Abs(dy) <= tickThickness * 0.5f && Mathf.Abs(dx) >= tickInner && Mathf.Abs(dx) <= tickOuter;

                texture.SetPixel(x, y, onRing || onDot || onVerticalTick || onHorizontalTick ? lineColor : clear);
            }
        }

        texture.Apply();
        return texture;
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
}
