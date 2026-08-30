using UnityEngine;

namespace DinoRush.Runtime
{
    // Builds the playable scene in code rather than from a committed .unity file
    // (docs/DECISIONS.md D12): hand-authoring Unity's YAML from a container with no editor to
    // validate it is the most reliable way to produce a project that won't open. Everything
    // here is a primitive placeholder per section 72 — prove the run is fun before making art.
    //
    // Reuses whatever camera and light the open scene already provides (the URP template's
    // SampleScene has both) and creates only what's missing, so it behaves the same whether
    // you press Play from SampleScene or from an empty one.
    public static class RunBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Build()
        {
            // The design is a 390x844 portrait layout and the camera rig is framed for it, so
            // the orientation is pinned rather than left to auto-rotate. Doing it here means it
            // also applies in the editor and Simulator, not just to a device build.
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.orientation = ScreenOrientation.Portrait;

            var root = new GameObject("DinoRush");

            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = cameraObject.AddComponent<Camera>();
            }
            // Orientation is set every frame by RunController (it looks down the track), so
            // only the lens is configured here.
            camera.backgroundColor = new Color(0.36f, 0.45f, 0.30f);
            camera.fieldOfView = 62f;

            if (Object.FindAnyObjectByType<Light>() == null)
            {
                var lightObject = new GameObject("Directional Light", typeof(Light));
                lightObject.transform.SetParent(root.transform);
                var light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform);
            // Long enough that the moving camera never reaches an edge; scaled rather than
            // tiled because it's a placeholder, not the real biome floor.
            ground.transform.localScale = new Vector3(4000f, 1f, 26f);
            Object.Destroy(ground.GetComponent<Collider>());
            ground.GetComponent<Renderer>().material.color = new Color(0.30f, 0.38f, 0.22f);

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.SetParent(root.transform);
            Object.Destroy(player.GetComponent<Collider>());
            player.GetComponent<Renderer>().material.color = new Color(0.85f, 0.72f, 0.30f);

            var obstacleRoot = new GameObject("Obstacles");
            obstacleRoot.transform.SetParent(root.transform);
            var coinRoot = new GameObject("Coins");
            coinRoot.transform.SetParent(root.transform);
            var sceneryRoot = new GameObject("Scenery");
            sceneryRoot.transform.SetParent(root.transform);

            var audio = root.AddComponent<RunAudio>();

            // Save, analytics, config, ads and progression, constructed in section 53's order.
            var services = new GameServices();

            // Built in code like everything else (D12) — canvas, safe area, and every screen.
            var ui = GameUI.Create(root.transform);

            var controller = root.AddComponent<RunController>();
            controller.Initialise(
                player.transform, camera.transform, ground.transform,
                obstacleRoot.transform, coinRoot.transform, sceneryRoot.transform,
                audio, services, ui);

            Object.DontDestroyOnLoad(root);
        }
    }
}
