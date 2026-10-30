using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace MergePrototype
{
    public static class PrototypeBuilder
    {
        const string ScenePath = "Assets/Scenes/MergeBall.unity";
        static Sprite circle, square;
        static Material spriteMaterial;
        static TMP_FontAsset song, times;
        static readonly Color Ink = new Color(.16f, .22f, .23f);
        static readonly Color Paper = new Color(.97f, .95f, .91f);

        [MenuItem("Prototype/Create Scene")]
        public static void CreateScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene already exists; edit it instead of rebuilding.");
            if (TMP_Settings.instance == null) throw new InvalidOperationException("Import TMP essentials first.");
            Directory.CreateDirectory("Assets/Art");
            Directory.CreateDirectory("Assets/Prefabs");
            CreateArt();
            song = CreateFont("SimSun", "合成大球分数下一个重新开始游戏结束移动鼠标点击落球·0123456789. ");
            times = CreateFont("TimesNewRoman", "0123456789. ");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var backdrop = new GameObject("Letterbox", typeof(Camera)).GetComponent<Camera>();
            backdrop.depth = -2;
            backdrop.clearFlags = CameraClearFlags.SolidColor;
            backdrop.backgroundColor = Ink;
            backdrop.cullingMask = 0;
            backdrop.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.depth = -1;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 8;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Paper;
            camera.gameObject.AddComponent<PortraitViewport>();
            var cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            // Flat tier colors must remain unchanged by tonemapping.
            cameraData.renderPostProcessing = false;
            var light = new GameObject("Global Light").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1;

            var physics = new PhysicsMaterial2D("Ball Contact") { friction = .32f, bounciness = .12f };
            AssetDatabase.CreateAsset(physics, "Assets/Art/BallContact.physicsMaterial2D");
            Physics2D.gravity = new Vector2(0, -14);
            Physics2D.velocityIterations = 12;
            Physics2D.positionIterations = 8;
            Wall("Left Wall", new Vector2(-3.85f, 0), new Vector2(.2f, 12), physics);
            Wall("Right Wall", new Vector2(3.85f, 0), new Vector2(.2f, 12), physics);
            Wall("Floor", new Vector2(0, -6), new Vector2(7.9f, .2f), physics);

            var prefabObject = new GameObject("Ball", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(MergeBall));
            var sr = prefabObject.GetComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.sharedMaterial = spriteMaterial;
            sr.sortingOrder = 10;
            var body = prefabObject.GetComponent<Rigidbody2D>();
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearDamping = .08f;
            body.angularDamping = .15f;
            var collider = prefabObject.GetComponent<CircleCollider2D>();
            collider.radius = .5f;
            collider.sharedMaterial = physics;
            var prefab = PrefabUtility.SaveAsPrefabAsset(prefabObject, "Assets/Prefabs/Ball.prefab");
            UnityEngine.Object.DestroyImmediate(prefabObject);
            var game = new GameObject("Merge Game").AddComponent<MergeGame>();
            game.ballPrefab = prefab.GetComponent<MergeBall>();
            game.ballRoot = new GameObject("Balls").transform;
            game.gameplayCamera = camera;
            game.preview = Shape("Drop Preview", circle, Vector2.zero, Vector2.one, Color.white, 11);
            game.guide = new GameObject("Drop Guide").AddComponent<LineRenderer>();
            game.guide.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            game.guide.sharedMaterial.SetColor("_BaseColor", new Color(.84f, .84f, .79f));
            AssetDatabase.CreateAsset(game.guide.sharedMaterial, "Assets/Art/Guide.mat");
            game.guide.startWidth = game.guide.endWidth = .018f;
            game.guide.sortingOrder = -1;
            game.guide.positionCount = 2;

            var canvasObject = new GameObject("HUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 960);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            var hud = canvasObject.AddComponent<PrototypeHud>();
            hud.game = game;
            Label("Title", canvas.transform, "合成大球", -100, 425, 264, 42, 30, song, TextAlignmentOptions.MidlineLeft);
            Label("Score Label", canvas.transform, "分数", -208, 374, 50, 25, 16, song, TextAlignmentOptions.MidlineLeft);
            hud.score = Label("Score", canvas.transform, "0", -100, 373, 160, 48, 44, times, TextAlignmentOptions.MidlineLeft);
            Label("Next Label", canvas.transform, "下一个", 103, 374, 72, 25, 16, song);
            hud.nextBall = Image("Next Ball", canvas.transform, 182, 374, 28, 28, Color.white);
            hud.nextBall.sprite = circle;
            hud.restart = Button("Restart", canvas.transform, "重新开始", 173, 425, 112, 36);
            hud.countdown = Label("Danger Countdown", canvas.transform, "", 190, 279, 60, 24, 20, times);
            hud.countdown.color = new Color(.8f, .22f, .18f);
            hud.dangerDashes = new SpriteRenderer[22];
            for (int i = 0; i < hud.dangerDashes.Length; i++)
                hud.dangerDashes[i] = Shape("Danger Line", square, new Vector2(-3.58f + i * .341f, MergeGame.DangerY), new Vector2(.19f, .05f), new Color(.67f, .55f, .47f), 20);
            for (int i = 0; i < 10; i++)
            {
                float size = Mathf.Lerp(10, 39, i / 9f);
                var icon = Image("Tier " + (i + 1), canvas.transform, -210 + i * 46.5f, -400, size, size, MergeGame.Colors[i]);
                icon.sprite = circle;
            }
            Label("Controls", canvas.transform, "移动鼠标 · 点击落球", 0, -451, 400, 30, 17, song);
            hud.endPanel = Image("Game Over", canvas.transform, 0, 0, 540, 960, new Color(.08f, .14f, .15f, .65f)).gameObject;
            hud.endPanel.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var card = Image("End Card", hud.endPanel.transform, 0, 0, 410, 275, Paper);
            Label("End Title", card.transform, "游戏结束", 0, 76, 320, 44, 32, song);
            hud.finalScore = Label("Final Score", card.transform, "0", 0, 10, 340, 68, 52, times);
            hud.replay = Button("Play Again", card.transform, "重新开始", 0, -83, 210, 48);
            hud.endPanel.SetActive(false);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            PlayerSettings.companyName = "Prototype";
            PlayerSettings.productName = "合成大球";
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            QualitySettings.vSyncCount = 1;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = game.gameObject;
        }

        static void CreateArt()
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new InvalidOperationException("URP sprite shader missing.");
            spriteMaterial = new Material(shader);
            AssetDatabase.CreateAsset(spriteMaterial, "Assets/Art/Sprite.mat");
            circle = MakeSprite("Circle", true);
            square = MakeSprite("Square", false);
        }

        static Sprite MakeSprite(string name, bool isCircle)
        {
            int size = isCircle ? 256 : 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * (size * .5f));
                    texture.SetPixel(x, y, new Color(1, 1, 1, isCircle ? Mathf.Clamp01(size * .5f - distance) : 1));
                }
            texture.Apply();
            string path = "Assets/Art/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = size;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static TMP_FontAsset CreateFont(string name, string characters)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/" + name + ".ttf");
            if (source == null) throw new InvalidOperationException("Missing font: " + name);
            var font = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024);
            font.name = name;
            AssetDatabase.CreateAsset(font, "Assets/Fonts/" + name + " SDF.asset");
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            if (!font.TryAddCharacters(characters, out string missing)) throw new InvalidOperationException("Font lacks: " + missing);
            EditorUtility.SetDirty(font);
            return font;
        }

        static SpriteRenderer Shape(string name, Sprite sprite, Vector2 position, Vector2 scale, Color color, int order)
        {
            var renderer = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = spriteMaterial;
            renderer.color = color;
            renderer.sortingOrder = order;
            renderer.transform.position = position;
            renderer.transform.localScale = scale;
            return renderer;
        }

        static void Wall(string name, Vector2 position, Vector2 scale, PhysicsMaterial2D material)
        {
            var wall = Shape(name, square, position, scale, new Color(.37f, .46f, .42f), 5);
            var collider = wall.gameObject.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.sharedMaterial = material;
        }

        static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        static TextMeshProUGUI Label(string name, Transform parent, string value, float x, float y, float w, float h, int size, TMP_FontAsset font, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = Ink;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        static UnityEngine.UI.Image Image(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            var image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static UnityEngine.UI.Button Button(string name, Transform parent, string text, float x, float y, float w, float h)
        {
            var image = Image(name, parent, x, y, w, h, new Color(.85f, .88f, .82f));
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            Label("Label", image.transform, text, 0, 0, w, h, 17, song);
            return button;
        }

        [MenuItem("Prototype/Build Windows")]
        public static string BuildWindows()
        {
            Directory.CreateDirectory("Build/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = "Build/Windows/MergeBall.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build failed: " + report.summary.result);
            return Path.GetFullPath("Build/Windows/MergeBall.exe");
        }
    }
}
