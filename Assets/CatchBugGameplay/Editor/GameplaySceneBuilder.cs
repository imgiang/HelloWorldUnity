using System.IO;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the entire gameplay scene from scratch: Tools/Create Gameplay Scene.
///
/// Produces two scene files:
///  - Gameplay.unity: the main scene (Directional Light, Camera Rig + Main Camera, Character
///    Bootstrap, Player Input, and a SubScene reference). This is what you press Play on.
///  - GameplayEntities.unity: a SubScene containing everything that needs to be baked into ECS
///    entities (Ground, Player character + head, third-person orbit camera, player logic entity).
///    Unity.CharacterController is Entities-only, so any GameObject using it must live in a
///    SubScene - that's why the content is split across two scene files instead of one.
/// </summary>
public static class GameplaySceneBuilder
{
    private const string RootFolder = "Assets/CatchBugGameplay";
    private const string SceneFolder = RootFolder + "/Scenes";
    private const string MainScenePath = SceneFolder + "/Gameplay.unity";
    private const string SubScenePath = SceneFolder + "/GameplayEntities.unity";
    private const string InputActionsPath = RootFolder + "/Input/PlayerControls.inputactions";

    [MenuItem("Tools/Create Gameplay Scene")]
    public static void CreateGameplayScene()
    {
        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (inputActions == null)
        {
            EditorUtility.DisplayDialog(
                "Create Gameplay Scene",
                $"Could not find the input actions asset at '{InputActionsPath}'. Make sure PlayerControls.inputactions exists before running this command.",
                "OK");
            return;
        }

        if (File.Exists(MainScenePath) || File.Exists(SubScenePath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "Create Gameplay Scene",
                "Gameplay.unity and/or GameplayEntities.unity already exist and will be overwritten. Continue?",
                "Overwrite",
                "Cancel");
            if (!overwrite)
            {
                return;
            }
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EnsureFolder(SceneFolder);

        // 1. Build the SubScene content in its own additive scene, since Unity.CharacterController
        // entities can only come from baked GameObjects living inside a SubScene.
        Scene entitiesScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        Scene previousActiveScene = SceneManager.GetActiveScene();
        EditorSceneManager.SetActiveScene(entitiesScene);
        try
        {
            BuildGround();
            GameObject player = BuildPlayer();
            GameObject thirdPersonCamera = BuildThirdPersonCamera();
            BuildPlayerLogic(player, thirdPersonCamera);
        }
        finally
        {
            EditorSceneManager.SetActiveScene(previousActiveScene);
        }

        EditorSceneManager.SaveScene(entitiesScene, SubScenePath);
        SubSceneInspectorUtility.SetSceneAsSubScene(entitiesScene);

        // 2. Build the main scene (everything that stays as plain GameObjects). Switching to
        // Single mode here also closes the now-saved entitiesScene for us.
        Scene mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildDirectionalLight();
        BuildCameraRig();
        BuildBootstrapAndInput(inputActions);
        BuildSubSceneReference();

        EditorSceneManager.SetActiveScene(mainScene);
        EditorSceneManager.SaveScene(mainScene, MainScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath);
        EditorUtility.DisplayDialog(
            "Create Gameplay Scene",
            "Gameplay scene created.\n\nPress Play: WASD to move, mouse to look, Shift to sprint, Space to jump, V to switch first/third person, scroll to zoom in third person.",
            "OK");
    }

    private static void BuildGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        ground.transform.localScale = new Vector3(5f, 1f, 5f);
        // No Rigidbody -> Unity Physics bakes the MeshCollider as a static body automatically.
    }

    private static GameObject BuildPlayer()
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);

        GameObject eyes = new GameObject("Eyes");
        eyes.transform.SetParent(player.transform, worldPositionStays: false);
        eyes.transform.localPosition = new Vector3(0f, 0.6f, 0f); // world Y = 1.6
        eyes.transform.localRotation = Quaternion.identity;
        eyes.AddComponent<PlayerCharacterViewAuthoring>().Character = player;

        PlayerCharacterAuthoring characterAuthoring = player.AddComponent<PlayerCharacterAuthoring>();
        characterAuthoring.ViewEntity = eyes;

        PlayerCameraTargetAuthoring cameraTargetAuthoring = player.AddComponent<PlayerCameraTargetAuthoring>();
        cameraTargetAuthoring.Target = eyes;

        return player;
    }

    private static GameObject BuildThirdPersonCamera()
    {
        GameObject camera = new GameObject("Third Person Camera");
        camera.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, -4f), Quaternion.identity);
        camera.AddComponent<PlayerCameraAuthoring>();
        return camera;
    }

    private static void BuildPlayerLogic(GameObject player, GameObject thirdPersonCamera)
    {
        GameObject playerLogic = new GameObject("Player Logic");
        PlayerAuthoring playerAuthoring = playerLogic.AddComponent<PlayerAuthoring>();
        playerAuthoring.ControlledCharacter = player;
        playerAuthoring.ControlledCamera = thirdPersonCamera;
    }

    private static void BuildDirectionalLight()
    {
        GameObject lightGameObject = new GameObject("Directional Light", typeof(Light));
        lightGameObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light light = lightGameObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;
        light.intensity = 1f;
    }

    private static CameraRig BuildCameraRig()
    {
        GameObject rigGameObject = new GameObject("Camera Rig", typeof(CameraRig));
        rigGameObject.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, -4f), Quaternion.identity);

        GameObject mainCameraGameObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        mainCameraGameObject.tag = "MainCamera";
        mainCameraGameObject.transform.SetParent(rigGameObject.transform, worldPositionStays: false);
        mainCameraGameObject.transform.localPosition = Vector3.zero;
        mainCameraGameObject.transform.localRotation = Quaternion.identity;

        CameraRig cameraRig = rigGameObject.GetComponent<CameraRig>();
        cameraRig.AssignMainCamera(mainCameraGameObject.GetComponent<Camera>());
        return cameraRig;
    }

    private static void BuildBootstrapAndInput(InputActionAsset inputActions)
    {
        GameObject bootstrapGameObject = new GameObject("Character Bootstrap", typeof(CharacterBootstrap));

        GameObject inputGameObject = new GameObject("Player Input");
        PlayerInputReader inputReader = inputGameObject.AddComponent<PlayerInputReader>();
        inputReader.AssignInputActions(inputActions);
    }

    private static void BuildSubSceneReference()
    {
        GameObject subSceneGameObject = new GameObject("Gameplay Entities", typeof(SubScene));
        SubScene subScene = subSceneGameObject.GetComponent<SubScene>();
        subScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath);
        subScene.AutoLoadScene = true;
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
