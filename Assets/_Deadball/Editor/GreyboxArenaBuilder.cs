using Deadball.Ball;
using Deadball.Config;
using Deadball.Fighters;
using Deadball.HUD;
using Deadball.Input;
using Deadball.Match;
using Deadball.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Deadball.Editor
{
    /// <summary>
    /// Builds the Day 1 greybox: capsules only, no art (GDD section 21).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Day 1 gate is that the game has to be fun with capsules, because if it is not, it will not
    /// be fun with Synty characters either. Generating the arena rather than hand-placing it means
    /// the layout can be thrown away and rebuilt in seconds while the feel is still being tuned,
    /// which is the only part of the schedule that actually matters.
    /// </para>
    /// <para>
    /// Everything it writes is idempotent: tuning assets and materials are reused if they already
    /// exist, so re-running the builder after a tuning pass does not undo the tuning pass.
    /// </para>
    /// </remarks>
    public static class GreyboxArenaBuilder
    {
        const string ScenePath = DeadballAssetFactory.SceneFolder + "/Arena_Greybox.unity";
        const string FighterPrefabPath = DeadballAssetFactory.PrefabFolder + "/Fighter.prefab";
        const string ActionsPath = DeadballAssetFactory.Root + "/Input/DeadballControls.inputactions";

        const float WallHeight = 2.5f;
        const float WallThickness = 0.5f;

        public static void Build()
        {
            DeadballAssetFactory.EnsureLayers();
            DeadballAssetFactory.EnsureFolder(DeadballAssetFactory.DataFolder);
            DeadballAssetFactory.EnsureFolder(DeadballAssetFactory.PrefabFolder);
            DeadballAssetFactory.EnsureFolder(DeadballAssetFactory.SceneFolder);

            DeadballAssetFactory.EnsureMatchConfig();
            DeadballAssetFactory.EnsurePalette();
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Loading the tuning assets has to happen after the new scene exists. Opening a scene
            // unloads assets nothing is referencing yet, which quietly turns a freshly created
            // ScriptableObject into a destroyed object - and assigning one of those to a serialized
            // field writes null without raising anything.
            var config = AssetDatabase.LoadAssetAtPath<MatchConfig>($"{DeadballAssetFactory.DataFolder}/MatchConfig.asset");
            var palette = AssetDatabase.LoadAssetAtPath<FighterPalette>($"{DeadballAssetFactory.DataFolder}/FighterPalette.asset");
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);

            if (config == null || palette == null || actions == null)
            {
                Debug.LogError("[Deadball] Missing a required asset: "
                    + $"config={config != null}, palette={palette != null}, actions={actions != null}.");
                return;
            }

            BuildLighting();
            ArenaReferences arena = BuildArena(config);
            BallController ball = BuildBall(config, palette);
            GameObject fighterPrefab = BuildFighterPrefab(config, palette, actions);
            FixedArenaCamera camera = BuildCamera(config, arena.transform);
            (RoundManager rounds, MatchManager match, FighterJoinManager join) =
                BuildSystems(config, arena, ball, fighterPrefab, actions);

            BuildHud(config, palette, rounds, match);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            Debug.Log($"[Deadball] Greybox arena built at {ScenePath}. Camera framed at {camera.name}.");
        }

        static void BuildLighting()
        {
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.color = new Color(1f, 0.96f, 0.9f);
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.35f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.23f, 0.28f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.12f, 0.14f);
        }

        static ArenaReferences BuildArena(MatchConfig config)
        {
            float size = config.ArenaSize;
            float half = size * 0.5f;

            var root = new GameObject("Arena");
            Material floorMaterial = DeadballAssetFactory.EnsureMaterial("GB_Floor", new Color(0.20f, 0.21f, 0.24f));
            Material wallMaterial = DeadballAssetFactory.EnsureMaterial("GB_Wall", new Color(0.31f, 0.33f, 0.38f));
            Material propMaterial = DeadballAssetFactory.EnsureMaterial("GB_Prop", new Color(0.42f, 0.38f, 0.34f));
            PhysicsMaterial bouncy = EnsureBouncyMaterial();

            Primitive("Floor", root.transform, new Vector3(0f, -0.25f, 0f),
                new Vector3(size, 0.5f, size), floorMaterial, bouncy);

            // Walls on all sides. A missed throw is not a dead ball, it is a live one ricocheting
            // somewhere neither player predicted - this single property generates most of the
            // game's chaos for free (15).
            float offset = half + WallThickness * 0.5f;
            float span = size + WallThickness * 2f;

            Primitive("Wall_North", root.transform, new Vector3(0f, WallHeight * 0.5f, offset),
                new Vector3(span, WallHeight, WallThickness), wallMaterial, bouncy);
            Primitive("Wall_South", root.transform, new Vector3(0f, WallHeight * 0.5f, -offset),
                new Vector3(span, WallHeight, WallThickness), wallMaterial, bouncy);
            Primitive("Wall_East", root.transform, new Vector3(offset, WallHeight * 0.5f, 0f),
                new Vector3(WallThickness, WallHeight, span), wallMaterial, bouncy);
            Primitive("Wall_West", root.transform, new Vector3(-offset, WallHeight * 0.5f, 0f),
                new Vector3(WallThickness, WallHeight, span), wallMaterial, bouncy);

            // Two or three solid pieces in the middle third: enough to break line of sight and let
            // you juke around a corner, not so many that the ball gets lost or the fight gets campy.
            var props = root.transform;
            Primitive("Prop_Car", props, new Vector3(-3.6f, 0.7f, 1.8f),
                new Vector3(4.2f, 1.4f, 1.8f), propMaterial, bouncy);
            Primitive("Prop_Dumpster", props, new Vector3(4.4f, 0.8f, -2.4f),
                new Vector3(2.0f, 1.6f, 1.8f), propMaterial, bouncy);
            Primitive("Prop_Pallets", props, new Vector3(0.6f, 0.5f, 5.4f),
                new Vector3(2.4f, 1.0f, 1.2f), propMaterial, bouncy);

            var centre = new GameObject("Centre");
            centre.transform.SetParent(root.transform);
            centre.transform.localPosition = Vector3.zero;

            // Opposite corners (10).
            float spawnInset = half - 2.5f;
            Transform spawnA = Marker("Spawn_P1", root.transform, new Vector3(-spawnInset, 0f, -spawnInset));
            Transform spawnB = Marker("Spawn_P2", root.transform, new Vector3(spawnInset, 0f, spawnInset));

            var references = root.AddComponent<ArenaReferences>();
            SetPrivate(references, "_centre", centre.transform);
            SetPrivate(references, "_spawnPoints", new[] { spawnA, spawnB });
            SetPrivate(references, "_config", config);

            return references;
        }

        static BallController BuildBall(MatchConfig config, FighterPalette palette)
        {
            var root = new GameObject("Ball") { layer = DeadballLayers.BallLayer };
            root.transform.position = new Vector3(0f, 0.25f, 0f);

            var body = root.AddComponent<SphereCollider>();
            body.radius = 0.25f;
            body.sharedMaterial = EnsureBouncyMaterial();

            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 0.4f;
            rigidbody.useGravity = false;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.5f;
            visual.layer = DeadballLayers.BallLayer;

            Color ballColour = palette.LooseBallColour;
            visual.GetComponent<Renderer>().sharedMaterial =
                DeadballAssetFactory.EnsureMaterial("GB_Ball", ballColour, 0.6f, ballColour * 2f);

            var trailObject = new GameObject("Trail") { layer = DeadballLayers.BallLayer };
            trailObject.transform.SetParent(root.transform, false);
            var trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = 0.2f;
            trail.widthMultiplier = 0.2f;
            trail.minVertexDistance = 0.05f;
            trail.emitting = false;
            trail.sharedMaterial = DeadballAssetFactory.EnsureUnlitMaterial("GB_Trail", Color.white, transparent: true);

            var grabObject = new GameObject("GrabTrigger") { layer = DeadballLayers.HitboxLayer };
            grabObject.transform.SetParent(root.transform, false);
            var grabCollider = grabObject.AddComponent<SphereCollider>();
            grabCollider.isTrigger = true;
            grabCollider.radius = config.PickupRadius;
            var grabTrigger = grabObject.AddComponent<BallGrabTrigger>();

            // The shadow is not a child: it lives on the floor while the ball is in the air (8.6).
            GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            shadow.name = "BallShadow";
            Object.DestroyImmediate(shadow.GetComponent<Collider>());
            shadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.GetComponent<Renderer>().sharedMaterial =
                DeadballAssetFactory.EnsureUnlitMaterial("GB_BallShadow", new Color(0f, 0f, 0f, 0.55f), transparent: true);

            var ball = root.AddComponent<BallController>();
            SetPrivate(ball, "_config", config);
            SetPrivate(ball, "_body", body);
            SetPrivate(ball, "_visual", visual.transform);
            SetPrivate(ball, "_grabTrigger", grabTrigger);

            var shadowPresenter = root.AddComponent<BallShadowPresenter>();
            SetPrivate(shadowPresenter, "_ball", ball);
            SetPrivate(shadowPresenter, "_shadow", shadow.transform);

            var visualPresenter = root.AddComponent<BallVisualPresenter>();
            SetPrivate(visualPresenter, "_ball", ball);
            SetPrivate(visualPresenter, "_renderer", visual.GetComponent<Renderer>());
            SetPrivate(visualPresenter, "_trail", trail);
            SetPrivate(visualPresenter, "_palette", palette);

            return ball;
        }

        static GameObject BuildFighterPrefab(MatchConfig config, FighterPalette palette, InputActionAsset actions)
        {
            var root = new GameObject("Fighter") { layer = DeadballLayers.FighterLayer };

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.4f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 70f;
            rigidbody.useGravity = false;
            rigidbody.freezeRotation = true;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            body.GetComponent<Renderer>().sharedMaterial =
                DeadballAssetFactory.EnsureMaterial("GB_Fighter", Color.white, 0.2f, Color.white * 0.35f);

            // A nose so facing - and therefore aim - is readable from directly above.
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "FacingMarker";
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.2f, 0.45f);
            nose.transform.localScale = new Vector3(0.18f, 0.18f, 0.5f);
            nose.GetComponent<Renderer>().sharedMaterial = body.GetComponent<Renderer>().sharedMaterial;

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = "GroundRing";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = Vector3.one * 1.6f;
            ring.GetComponent<Renderer>().sharedMaterial =
                DeadballAssetFactory.EnsureUnlitMaterial("GB_GroundRing", new Color(1f, 1f, 1f, 0.35f), transparent: true);

            Transform centre = Marker("Centre", root.transform, new Vector3(0f, 1.0f, 0f));
            Transform hand = Marker("Hand", root.transform, new Vector3(0.42f, 1.05f, 0.35f));

            var hitboxObject = new GameObject("Hitbox") { layer = DeadballLayers.HitboxLayer };
            hitboxObject.transform.SetParent(root.transform, false);
            hitboxObject.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            var hitboxCollider = hitboxObject.AddComponent<SphereCollider>();
            hitboxCollider.isTrigger = true;
            hitboxCollider.radius = 0.7f;

            var chargeRingObject = new GameObject("ChargeRing");
            chargeRingObject.transform.SetParent(root.transform, false);
            var line = chargeRingObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.numCornerVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.sharedMaterial = DeadballAssetFactory.EnsureUnlitMaterial("GB_ChargeRing", Color.white, transparent: true);

            var motor = root.AddComponent<FighterMotor>();
            var thrower = root.AddComponent<FighterThrower>();
            var catcher = root.AddComponent<FighterCatcher>();
            var knocks = root.AddComponent<FighterKnocks>();
            var fighter = root.AddComponent<Fighter>();

            SetPrivate(motor, "_config", config);
            SetPrivate(thrower, "_config", config);
            SetPrivate(thrower, "_motor", motor);
            SetPrivate(thrower, "_handAnchor", hand);
            SetPrivate(catcher, "_config", config);
            SetPrivate(knocks, "_config", config);

            SetPrivate(fighter, "_centre", centre);
            SetPrivate(fighter, "_motor", motor);
            SetPrivate(fighter, "_thrower", thrower);
            SetPrivate(fighter, "_catcher", catcher);
            SetPrivate(fighter, "_knocks", knocks);

            var hitbox = hitboxObject.AddComponent<FighterHitbox>();
            SetPrivate(hitbox, "_fighter", fighter);

            var chargeRing = chargeRingObject.AddComponent<ChargeRingPresenter>();
            SetPrivate(chargeRing, "_thrower", thrower);
            SetPrivate(chargeRing, "_catcher", catcher);

            var colours = root.AddComponent<FighterColourPresenter>();
            SetPrivate(colours, "_fighter", fighter);
            SetPrivate(colours, "_palette", palette);
            SetPrivate(colours, "_bodyRenderers", new[] { body.GetComponent<Renderer>(), nose.GetComponent<Renderer>() });
            SetPrivate(colours, "_groundRing", ring.GetComponent<Renderer>());

            var playerInput = root.AddComponent<PlayerInput>();
            playerInput.actions = actions;
            playerInput.defaultActionMap = "Fighter";
            playerInput.notificationBehavior = PlayerNotifications.InvokeUnityEvents;
            root.AddComponent<PlayerInputProvider>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, FighterPrefabPath);
            Object.DestroyImmediate(root);

            return prefab;
        }

        static FixedArenaCamera BuildCamera(MatchConfig config, Transform lookTarget)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 200f;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.10f);
            cameraObject.AddComponent<AudioListener>();

            var fixedCamera = cameraObject.AddComponent<FixedArenaCamera>();
            SetPrivate(fixedCamera, "_config", config);
            SetPrivate(fixedCamera, "_lookTarget", lookTarget);
            fixedCamera.Frame();

            return fixedCamera;
        }

        static (RoundManager, MatchManager, FighterJoinManager) BuildSystems(
            MatchConfig config, ArenaReferences arena, BallController ball,
            GameObject fighterPrefab, InputActionAsset actions)
        {
            var root = new GameObject("Systems");

            var inputManager = root.AddComponent<PlayerInputManager>();
            inputManager.playerPrefab = fighterPrefab;
            inputManager.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            inputManager.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenButtonIsPressed;

            var join = root.AddComponent<FighterJoinManager>();

            var rounds = root.AddComponent<RoundManager>();
            SetPrivate(rounds, "_config", config);
            SetPrivate(rounds, "_arena", arena);
            SetPrivate(rounds, "_ball", ball);

            var match = root.AddComponent<MatchManager>();
            SetPrivate(match, "_config", config);
            SetPrivate(match, "_rounds", rounds);
            SetPrivate(match, "_rosterSource", join);

            root.AddComponent<HitstopService>();

            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            root.AddComponent<MatchAudioCues>();

            return (rounds, match, join);
        }

        static void BuildHud(MatchConfig config, FighterPalette palette, RoundManager rounds, MatchManager match)
        {
            var canvasObject = new GameObject("HUD");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            TMP_Text timer = Label("Timer", canvasObject.transform, new Vector2(0.5f, 1f), new Vector2(0f, -70f),
                new Vector2(400f, 110f), 84f, TextAlignmentOptions.Center);
            timer.text = "60";

            var cardObject = new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup));
            cardObject.transform.SetParent(canvasObject.transform, false);
            var cardRect = (RectTransform)cardObject.transform;
            Anchor(cardRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 200f));
            TMP_Text card = Label("CardLabel", cardObject.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1400f, 200f), 110f, TextAlignmentOptions.Center);
            card.text = "ROUND 1";

            BuildFighterPanel(0, canvasObject.transform, new Vector2(0f, 1f), new Vector2(220f, -70f), config, palette, match);
            BuildFighterPanel(1, canvasObject.transform, new Vector2(1f, 1f), new Vector2(-220f, -70f), config, palette, match);

            var hud = canvasObject.AddComponent<MatchHud>();
            SetPrivate(hud, "_rounds", rounds);
            SetPrivate(hud, "_palette", palette);
            SetPrivate(hud, "_timerLabel", timer);
            SetPrivate(hud, "_cardLabel", card);
            SetPrivate(hud, "_cardGroup", cardObject.GetComponent<CanvasGroup>());
        }

        static void BuildFighterPanel(int slot, Transform parent, Vector2 anchor, Vector2 offset,
            MatchConfig config, FighterPalette palette, MatchManager match)
        {
            var panelObject = new GameObject($"Panel_P{slot + 1}", typeof(RectTransform));
            panelObject.transform.SetParent(parent, false);
            Anchor((RectTransform)panelObject.transform, anchor, offset, new Vector2(400f, 140f));

            TMP_Text name = Label("Name", panelObject.transform, new Vector2(0.5f, 1f), new Vector2(0f, -20f),
                new Vector2(380f, 48f), 40f, TextAlignmentOptions.Center);
            name.text = palette.DisplayName(slot);

            HudPipRow knocks = PipRow("KnockPips", panelObject.transform, new Vector2(0f, -70f), 34f);
            HudPipRow wins = PipRow("RoundWinPips", panelObject.transform, new Vector2(0f, -112f), 18f);

            var panel = panelObject.AddComponent<FighterHudPanel>();
            SetPrivate(panel, "_slot", slot);
            SetPrivate(panel, "_palette", palette);
            SetPrivate(panel, "_config", config);
            SetPrivate(panel, "_nameLabel", name);
            SetPrivate(panel, "_knockPips", knocks);
            SetPrivate(panel, "_roundWinPips", wins);
            SetPrivate(panel, "_match", match);
        }

        static HudPipRow PipRow(string name, Transform parent, Vector2 offset, float pipSize)
        {
            var rowObject = new GameObject(name, typeof(RectTransform));
            rowObject.transform.SetParent(parent, false);
            Anchor((RectTransform)rowObject.transform, new Vector2(0.5f, 1f), offset, new Vector2(380f, pipSize + 8f));

            var layout = rowObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Without these the layout ignores the pips' preferred size and every pip renders at the
            // default 100x100, which buries the name label underneath them.
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var templateObject = new GameObject("PipTemplate", typeof(RectTransform));
            templateObject.transform.SetParent(rowObject.transform, false);
            var template = templateObject.AddComponent<Image>();
            var element = templateObject.AddComponent<LayoutElement>();
            element.preferredWidth = pipSize;
            element.preferredHeight = pipSize;
            templateObject.SetActive(false);

            var row = rowObject.AddComponent<HudPipRow>();
            SetPrivate(row, "_template", template);
            return row;
        }

        static TMP_Text Label(string name, Transform parent, Vector2 anchor, Vector2 offset,
            Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            Anchor((RectTransform)labelObject.transform, anchor, offset, size);

            var text = labelObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        static void Anchor(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, anchor.y);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        static GameObject Primitive(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, PhysicsMaterial physics)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.layer = DeadballLayers.ArenaLayer;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            cube.GetComponent<Collider>().sharedMaterial = physics;
            return cube;
        }

        static Transform Marker(string name, Transform parent, Vector3 localPosition)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            return marker.transform;
        }

        /// <summary>
        /// Full-energy bounce, no friction. The design's 85% retention is applied by the ball in
        /// code, which keeps the number in the tuning asset instead of buried in a physics material.
        /// </summary>
        static PhysicsMaterial EnsureBouncyMaterial()
        {
            const string path = DeadballAssetFactory.MaterialFolder + "/GB_Bouncy.physicsMaterial";

            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (existing != null) return existing;

            DeadballAssetFactory.EnsureFolder(DeadballAssetFactory.MaterialFolder);

            var material = new PhysicsMaterial("GB_Bouncy")
            {
                bounciness = 1f,
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void RegisterInBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == ScenePath)) return;

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Writes a serialized private field by name.
        /// </summary>
        /// <remarks>
        /// The alternative is making every wiring field public purely so an editor script can reach
        /// it, which would leak build-time concerns into the runtime API of every component.
        /// </remarks>
        static void SetPrivate(Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Debug.LogError($"[Deadball] '{target.GetType().Name}' has no serialized field '{field}'.");
                return;
            }

            switch (value)
            {
                case Object unityObject:
                    // A destroyed Unity object compares equal to null but is not a null reference,
                    // so it would assign silently as an empty field. Catch it here instead of
                    // discovering it as a NullReferenceException at play time.
                    if (unityObject == null)
                    {
                        Debug.LogError($"[Deadball] Refusing to wire a destroyed object into "
                            + $"'{target.GetType().Name}.{field}'.");
                        return;
                    }

                    property.objectReferenceValue = unityObject;
                    break;
                case int number:
                    property.intValue = number;
                    break;
                case float number:
                    property.floatValue = number;
                    break;
                case Object[] array:
                    property.arraySize = array.Length;
                    for (int i = 0; i < array.Length; i++)
                        property.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
                    break;
                default:
                    Debug.LogError($"[Deadball] Unsupported wiring type '{value?.GetType().Name}' for '{field}'.");
                    break;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
