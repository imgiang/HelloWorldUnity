using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds an on-screen move joystick to the currently open scene: Tools/Add On-Screen Joystick.
/// Targets the already-open main (non-ECS) Gameplay scene - unlike GameplaySceneBuilder, this does
/// not create or overwrite any scene, it only adds to what's open. Camera look on touch is handled
/// separately by dragging anywhere else on screen (see PlayerInputReader's LookEnable/Look touch
/// bindings) rather than a second on-screen stick. The joystick drives a virtual Gamepad device
/// (see OnScreenControl), which is exactly what PlayerControls.inputactions' Move action is already
/// bound to, so no other input code needs to change.
/// </summary>
public static class TouchControlsBuilder
{
    private const string CanvasName = "Touch Controls";
    private static readonly Vector2 JoystickSize = new Vector2(220f, 220f);
    private static readonly Vector2 HandleSize = new Vector2(100f, 100f);
    private const float JoystickMovementRange = 70f;
    private const float JoystickEdgeMargin = 40f;

    [MenuItem("Tools/Add On-Screen Joystick")]
    public static void AddOnScreenJoystick()
    {
        if (Object.FindFirstObjectByType<Canvas>() is Canvas existingCanvas && existingCanvas.name == CanvasName)
        {
            EditorUtility.DisplayDialog(
                "Add On-Screen Joystick",
                $"A '{CanvasName}' canvas already exists in the open scene. Remove it first if you want to rebuild it.",
                "OK");
            return;
        }

        EnsureEventSystem();

        Canvas canvas = BuildCanvas();
        BuildMoveJoystick(canvas.transform);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = canvas.gameObject;

        EditorUtility.DisplayDialog(
            "Add On-Screen Joystick",
            "Added a Move joystick (bottom-left) to the scene. Drag anywhere else on screen to look around. Remember to save the scene.",
            "OK");
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemGameObject = new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystemGameObject, "Add Event System");
    }

    private static Canvas BuildCanvas()
    {
        GameObject canvasGameObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGameObject, "Add Touch Controls Canvas");

        Canvas canvas = canvasGameObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGameObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void BuildMoveJoystick(Transform canvasTransform)
    {
        GameObject background = new GameObject("Move Joystick", typeof(Image));
        background.transform.SetParent(canvasTransform, worldPositionStays: false);

        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.sprite = GetKnobSprite();
        backgroundImage.color = new Color(1f, 1f, 1f, 0.25f);

        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.sizeDelta = JoystickSize;
        backgroundRect.anchorMin = new Vector2(0f, 0f);
        backgroundRect.anchorMax = new Vector2(0f, 0f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = new Vector2(
            JoystickSize.x * 0.5f + JoystickEdgeMargin,
            JoystickSize.y * 0.5f + JoystickEdgeMargin);

        GameObject handle = new GameObject("Handle", typeof(Image));
        handle.transform.SetParent(background.transform, worldPositionStays: false);

        Image handleImage = handle.GetComponent<Image>();
        handleImage.sprite = GetKnobSprite();
        handleImage.color = new Color(1f, 1f, 1f, 0.6f);

        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = HandleSize;

        OnScreenStick stick = handle.AddComponent<OnScreenStick>();
        stick.movementRange = JoystickMovementRange;
        stick.controlPath = "<Gamepad>/leftStick";
    }

    private static Sprite GetKnobSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
    }
}
