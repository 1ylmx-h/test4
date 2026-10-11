using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace QuaternionDemo
{
    /// <summary>
    /// 自包含的四元数旋转演示。运行时自动创建三维场景、uGUI、事件绑定和辅助线。
    /// </summary>
    public sealed class QuaternionRotationDemo : MonoBehaviour
    {
        private static readonly Color BackgroundColor = new Color(0.025f, 0.045f, 0.09f);
        private static readonly Color PanelColor = new Color(0.045f, 0.075f, 0.14f, 0.94f);
        private static readonly Color CyanColor = new Color(0.15f, 0.85f, 1f);
        private static readonly Color PinkColor = new Color(1f, 0.25f, 0.72f);

        private const float SlerpDurationSeconds = 2.5f;
        private const float AxisLength = 2.35f;
        private const int PathSamples = 40;

        private Transform modelRoot;
        private Camera sceneCamera;
        private TMP_FontAsset runtimeFont;
        private Material lineMaterial;

        private UnityEngine.UI.Slider axisXSlider;
        private UnityEngine.UI.Slider axisYSlider;
        private UnityEngine.UI.Slider axisZSlider;
        private UnityEngine.UI.Slider angleSlider;
        private UnityEngine.UI.Slider interpolationSlider;

        private TMP_Text chapterText;
        private TMP_Text axisText;
        private TMP_Text angleText;
        private TMP_Text interpolationText;
        private TMP_Text quaternionText;
        private TMP_Text eulerText;
        private TMP_Text magnitudeText;
        private TMP_Text stateText;
        private TMP_Text explanationText;

        private LineRenderer localXAxisLine;
        private LineRenderer localYAxisLine;
        private LineRenderer localZAxisLine;
        private LineRenderer rotationAxisLine;
        private LineRenderer directionPathLine;

        private Quaternion startRotation = Quaternion.identity;
        private Quaternion targetRotation = Quaternion.identity;
        private Quaternion currentRotation = Quaternion.identity;
        private Coroutine animationCoroutine;
        private bool suppressSliderEvents;

        public bool IsAnimating { get; private set; }
        public Quaternion CurrentRotation => currentRotation;
        public float AnimationDuration => SlerpDurationSeconds;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            CreateRuntimeFont();
            BuildWorld();
            BuildInterface();
            CreateVisualizationLines();
            RegisterSliderEvents();

            startRotation = Quaternion.identity;
            currentRotation = Quaternion.identity;
            targetRotation = BuildQuaternionFromSliders();
            ApplyRotation(currentRotation);
            SetChapter("Quaternion Rotation Lab");
            SetState("Ready - choose a preset or adjust the axis-angle controls.");
            SetExplanation("A unit quaternion represents a 3D rotation without gimbal lock.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) PlaySlerp();
            if (Input.GetKeyDown(KeyCode.R)) ResetRotation();
            if (Input.GetKeyDown(KeyCode.Alpha1)) PresetX90();
            if (Input.GetKeyDown(KeyCode.Alpha2)) PresetY90();
            if (Input.GetKeyDown(KeyCode.Alpha3)) PresetZ90();
        }

        private void OnDestroy()
        {
            UnregisterSliderEvents();
            if (lineMaterial != null) Destroy(lineMaterial);
        }

        public void PlaySlerp()
        {
            targetRotation = BuildQuaternionFromSliders();
            StartAnimation(targetRotation, "Slerp: current orientation to axis-angle target");
            SetExplanation("q(t) = Slerp(qA, qB, t). The interpolation follows the shortest arc on the unit 4D sphere.");
        }

        public void ApplyImmediately()
        {
            StopCurrentAnimation();
            startRotation = currentRotation;
            targetRotation = BuildQuaternionFromSliders();
            currentRotation = targetRotation;
            SetInterpolationWithoutNotify(1f);
            ApplyRotation(currentRotation);
            SetState("Target rotation applied immediately.");
        }

        public void ResetRotation()
        {
            StopCurrentAnimation();
            startRotation = Quaternion.identity;
            targetRotation = Quaternion.identity;
            currentRotation = Quaternion.identity;
            SetInterpolationWithoutNotify(0f);
            ApplyRotation(currentRotation);
            SetState("Identity quaternion q = (0, 0, 0, 1)");
            SetExplanation("The identity quaternion leaves every vector unchanged.");
        }

        public void PresetX90()
        {
            SetAxisAngleAndPlay(Vector3.right, 90f, "Preset: 90 degrees around X");
        }

        public void PresetY90()
        {
            SetAxisAngleAndPlay(Vector3.up, 90f, "Preset: 90 degrees around Y");
        }

        public void PresetZ90()
        {
            SetAxisAngleAndPlay(Vector3.forward, 90f, "Preset: 90 degrees around Z");
        }

        public void PresetArbitraryAxis()
        {
            SetAxisAngleAndPlay(new Vector3(1f, 1f, 0.5f), 135f, "Arbitrary axis: (1, 1, 0.5), 135 degrees");
        }

        public void ShowRotateXThenY()
        {
            SetAxisAngleWithoutNotify(new Vector3(1f, 1f, 0f), 90f);
            Quaternion rotateX = QuaternionMath.FromAxisAngle(Vector3.right, 90f);
            Quaternion rotateY = QuaternionMath.FromAxisAngle(Vector3.up, 90f);
            Quaternion combined = QuaternionMath.Multiply(rotateY, rotateX);
            StartAnimation(combined, "Non-commutativity: X first, then Y");
            SetExplanation("X then Y uses q = qY * qX. Quaternion multiplication order matters.");
        }

        public void ShowRotateYThenX()
        {
            SetAxisAngleWithoutNotify(new Vector3(1f, 1f, 0f), 90f);
            Quaternion rotateX = QuaternionMath.FromAxisAngle(Vector3.right, 90f);
            Quaternion rotateY = QuaternionMath.FromAxisAngle(Vector3.up, 90f);
            Quaternion combined = QuaternionMath.Multiply(rotateX, rotateY);
            StartAnimation(combined, "Non-commutativity: Y first, then X");
            SetExplanation("Y then X uses q = qX * qY. Its final orientation differs from X then Y.");
        }

        public void SetChapter(string value)
        {
            if (chapterText != null) chapterText.text = value;
        }

        private void SetAxisAngleAndPlay(Vector3 axis, float angle, string message)
        {
            SetAxisAngleWithoutNotify(axis, angle);
            targetRotation = QuaternionMath.FromAxisAngle(axis, angle);
            StartAnimation(targetRotation, message);
            SetExplanation("Axis-angle formula: q = [axis * sin(angle / 2), cos(angle / 2)].");
        }

        private void StartAnimation(Quaternion destination, string message)
        {
            StopCurrentAnimation();
            startRotation = currentRotation;
            targetRotation = QuaternionMath.Normalize(destination);
            UpdateWorldLines(currentRotation);
            animationCoroutine = StartCoroutine(AnimateSlerp(message));
        }

        private IEnumerator AnimateSlerp(string message)
        {
            IsAnimating = true;
            SetState(message);
            SetInterpolationWithoutNotify(0f);

            float elapsed = 0f;
            while (elapsed < SlerpDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float linearT = Mathf.Clamp01(elapsed / SlerpDurationSeconds);
                float smoothT = Mathf.SmoothStep(0f, 1f, linearT);
                currentRotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);
                SetInterpolationWithoutNotify(smoothT);
                ApplyRotation(currentRotation);
                yield return null;
            }

            currentRotation = targetRotation;
            SetInterpolationWithoutNotify(1f);
            ApplyRotation(currentRotation);
            IsAnimating = false;
            animationCoroutine = null;
        }

        private void StopCurrentAnimation()
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }
            IsAnimating = false;
        }

        private void RegisterSliderEvents()
        {
            axisXSlider.onValueChanged.AddListener(OnAxisOrAngleChanged);
            axisYSlider.onValueChanged.AddListener(OnAxisOrAngleChanged);
            axisZSlider.onValueChanged.AddListener(OnAxisOrAngleChanged);
            angleSlider.onValueChanged.AddListener(OnAxisOrAngleChanged);
            interpolationSlider.onValueChanged.AddListener(OnInterpolationChanged);
        }

        private void UnregisterSliderEvents()
        {
            if (axisXSlider != null) axisXSlider.onValueChanged.RemoveListener(OnAxisOrAngleChanged);
            if (axisYSlider != null) axisYSlider.onValueChanged.RemoveListener(OnAxisOrAngleChanged);
            if (axisZSlider != null) axisZSlider.onValueChanged.RemoveListener(OnAxisOrAngleChanged);
            if (angleSlider != null) angleSlider.onValueChanged.RemoveListener(OnAxisOrAngleChanged);
            if (interpolationSlider != null) interpolationSlider.onValueChanged.RemoveListener(OnInterpolationChanged);
        }

        private void OnAxisOrAngleChanged(float unused)
        {
            if (suppressSliderEvents) return;
            targetRotation = BuildQuaternionFromSliders();
            UpdateReadouts(currentRotation);
            UpdateWorldLines(currentRotation);
        }

        private void OnInterpolationChanged(float interpolation)
        {
            if (suppressSliderEvents || IsAnimating) return;
            currentRotation = Quaternion.Slerp(startRotation, targetRotation, interpolation);
            ApplyRotation(currentRotation);
            SetState($"Manual Slerp - t = {interpolation:F2}");
        }

        private Quaternion BuildQuaternionFromSliders()
        {
            return QuaternionMath.FromAxisAngle(ReadAxisFromSliders(), angleSlider.value);
        }

        private Vector3 ReadAxisFromSliders()
        {
            Vector3 axis = new Vector3(axisXSlider.value, axisYSlider.value, axisZSlider.value);
            return axis.sqrMagnitude < 0.0001f ? Vector3.up : axis.normalized;
        }

        private void SetAxisAngleWithoutNotify(Vector3 axis, float angle)
        {
            axis = axis.sqrMagnitude < 0.0001f ? Vector3.up : axis.normalized;
            suppressSliderEvents = true;
            axisXSlider.SetValueWithoutNotify(axis.x);
            axisYSlider.SetValueWithoutNotify(axis.y);
            axisZSlider.SetValueWithoutNotify(axis.z);
            angleSlider.SetValueWithoutNotify(angle);
            suppressSliderEvents = false;
            UpdateReadouts(currentRotation);
        }

        private void SetInterpolationWithoutNotify(float value)
        {
            suppressSliderEvents = true;
            interpolationSlider.SetValueWithoutNotify(value);
            suppressSliderEvents = false;
        }

        private void ApplyRotation(Quaternion rotation)
        {
            if (modelRoot != null) modelRoot.rotation = rotation;
            UpdateReadouts(rotation);
            UpdateWorldLines(rotation);
        }

        private void UpdateReadouts(Quaternion rotation)
        {
            Vector3 axis = ReadAxisFromSliders();
            Vector3 euler = rotation.eulerAngles;
            axisText.text = $"Axis     ({axis.x,6:F2}, {axis.y,6:F2}, {axis.z,6:F2})";
            angleText.text = $"Angle    {angleSlider.value,6:F1} deg";
            interpolationText.text = $"Slerp t  {interpolationSlider.value,6:F2}";
            quaternionText.text = $"q = ({rotation.x:F3}, {rotation.y:F3}, {rotation.z:F3}, {rotation.w:F3})";
            eulerText.text = $"Euler = ({euler.x:F1}, {euler.y:F1}, {euler.z:F1}) deg";
            magnitudeText.text = $"|q| = {QuaternionMath.Magnitude(rotation):F4}  (unit length)";
        }

        private void SetState(string value)
        {
            if (stateText != null) stateText.text = value;
        }

        private void SetExplanation(string value)
        {
            if (explanationText != null) explanationText.text = value;
        }

        private void CreateRuntimeFont()
        {
            runtimeFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (runtimeFont == null)
            {
                runtimeFont = TMP_Settings.defaultFontAsset;
            }

            if (runtimeFont == null)
            {
                throw new InvalidOperationException(
                    "TextMeshPro essential resources are missing. Run QuaternionDemoProjectBuilder.BuildAll again.");
            }
        }

        private void BuildWorld()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            sceneCamera = cameraObject.AddComponent<Camera>();
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = BackgroundColor;
            sceneCamera.fieldOfView = 46f;
            cameraObject.transform.position = new Vector3(6.2f, 4.1f, -8.2f);
            cameraObject.transform.LookAt(new Vector3(0f, 0.85f, 0f));

            GameObject lightObject = new GameObject("Directional Light");
            Light directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.color = new Color(0.88f, 0.94f, 1f);
            directionalLight.intensity = 1.05f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            GameObject fillLightObject = new GameObject("Fill Light");
            Light fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.color = new Color(0.15f, 0.55f, 1f);
            fillLight.intensity = 2.2f;
            fillLight.range = 12f;
            fillLightObject.transform.position = new Vector3(-4f, 4f, -2f);

            CreateGroundAndGrid();
            CreateAircraftModel();
        }

        private void CreateGroundAndGrid()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.06f, 0f);
            ground.transform.localScale = new Vector3(1.45f, 1f, 1.45f);
            ground.GetComponent<Renderer>().material = CreateStandardMaterial(
                "Ground Material", new Color(0.035f, 0.07f, 0.12f), 0.1f, 0.35f);

            Material gridMaterial = CreateUnlitMaterial("Grid Material", new Color(0.1f, 0.34f, 0.5f, 0.55f));
            for (int index = -7; index <= 7; index++)
            {
                float coordinate = index;
                CreateGridLine(new Vector3(coordinate, 0.005f, -7f), new Vector3(coordinate, 0.005f, 7f), gridMaterial);
                CreateGridLine(new Vector3(-7f, 0.005f, coordinate), new Vector3(7f, 0.005f, coordinate), gridMaterial);
            }
        }

        private void CreateGridLine(Vector3 start, Vector3 end, Material material)
        {
            GameObject lineObject = new GameObject("Grid Line");
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.material = material;
            line.startWidth = 0.012f;
            line.endWidth = 0.012f;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private void CreateAircraftModel()
        {
            modelRoot = new GameObject("Quaternion Aircraft").transform;
            modelRoot.position = new Vector3(0f, 1.0f, 0f);

            Material bodyMaterial = CreateStandardMaterial("Body", new Color(0.82f, 0.9f, 0.98f), 0.4f, 0.75f);
            Material wingMaterial = CreateStandardMaterial("Wings", CyanColor, 0.3f, 0.8f);
            Material noseMaterial = CreateStandardMaterial("Nose", new Color(1f, 0.35f, 0.14f), 0.2f, 0.65f);
            Material engineMaterial = CreateStandardMaterial("Engine", new Color(0.22f, 0.3f, 0.42f), 0.75f, 0.85f);

            CreatePart(PrimitiveType.Cube, "Body", modelRoot, Vector3.zero, new Vector3(0.8f, 0.48f, 2.55f), bodyMaterial);
            CreatePart(PrimitiveType.Cube, "Left Wing", modelRoot, new Vector3(-1.05f, 0f, -0.05f), new Vector3(1.65f, 0.12f, 0.9f), wingMaterial);
            CreatePart(PrimitiveType.Cube, "Right Wing", modelRoot, new Vector3(1.05f, 0f, -0.05f), new Vector3(1.65f, 0.12f, 0.9f), wingMaterial);
            CreatePart(PrimitiveType.Sphere, "Nose", modelRoot, new Vector3(0f, 0f, 1.42f), new Vector3(0.56f, 0.42f, 0.75f), noseMaterial);
            CreatePart(PrimitiveType.Cylinder, "Engine", modelRoot, new Vector3(0f, 0f, -1.42f), new Vector3(0.38f, 0.32f, 0.38f), engineMaterial, Quaternion.Euler(90f, 0f, 0f));
            CreatePart(PrimitiveType.Cube, "Tail Fin", modelRoot, new Vector3(0f, 0.48f, -0.85f), new Vector3(0.12f, 0.92f, 0.72f), wingMaterial);
        }

        private static void CreatePart(
            PrimitiveType primitiveType,
            string objectName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Quaternion? localRotation = null)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = localRotation ?? Quaternion.identity;
            part.GetComponent<Renderer>().material = material;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        private void BuildInterface()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystemObject.AddComponent<EventSystem>();
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }

            GameObject canvasObject = new GameObject("Demo Canvas", typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = sceneCamera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 10;
            UnityEngine.UI.CanvasScaler scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            RectTransform leftPanel = CreatePanel("Controls Panel", canvasRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(475f, -48f));
            RectTransform rightPanel = CreatePanel("Readout Panel", canvasRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(530f, -48f));

            RectTransform header = CreateRect("Header", canvasRect);
            header.anchorMin = new Vector2(0.27f, 0.875f);
            header.anchorMax = new Vector2(0.73f, 0.975f);
            header.offsetMin = Vector2.zero;
            header.offsetMax = Vector2.zero;
            AddImage(header.gameObject, new Color(0.035f, 0.08f, 0.15f, 0.86f), false);
            AddPanelLayout(header, 0, 0f);
            CreateText(header, "QUATERNION ROTATION LAB", 31f, FontStyles.Bold, TextAlignmentOptions.Center, CyanColor, 56f);
            chapterText = CreateText(header, "Quaternion Rotation Lab", 19f, FontStyles.Normal, TextAlignmentOptions.Center, Color.white, 38f);

            PopulateControlsPanel(leftPanel);
            PopulateReadoutPanel(rightPanel);
        }

        private void PopulateControlsPanel(RectTransform panel)
        {
            AddPanelLayout(panel, 18, 8f);
            CreateText(panel, "AXIS-ANGLE INPUT", 24f, FontStyles.Bold, TextAlignmentOptions.Left, CyanColor, 42f);
            CreateText(panel, "Adjust the rotation axis and angle, then play Slerp.", 16f, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.75f, 0.82f, 0.9f), 42f);

            axisXSlider = CreateLabeledSlider(panel, "Axis X", -1f, 1f, 0f, new Color(1f, 0.3f, 0.3f));
            axisYSlider = CreateLabeledSlider(panel, "Axis Y", -1f, 1f, 1f, new Color(0.3f, 1f, 0.42f));
            axisZSlider = CreateLabeledSlider(panel, "Axis Z", -1f, 1f, 0f, new Color(0.35f, 0.55f, 1f));
            angleSlider = CreateLabeledSlider(panel, "Angle", 0f, 360f, 120f, new Color(1f, 0.76f, 0.18f));
            interpolationSlider = CreateLabeledSlider(panel, "Interpolation", 0f, 1f, 0f, PinkColor);

            CreateButtonRow(panel,
                ("PLAY SLERP", (Action)PlaySlerp),
                ("APPLY", (Action)ApplyImmediately),
                ("RESET", (Action)ResetRotation));
            CreateButtonRow(panel,
                ("X 90", (Action)PresetX90),
                ("Y 90", (Action)PresetY90),
                ("Z 90", (Action)PresetZ90));
            CreateButtonRow(panel,
                ("ARBITRARY", (Action)PresetArbitraryAxis),
                ("X THEN Y", (Action)ShowRotateXThenY),
                ("Y THEN X", (Action)ShowRotateYThenX));

            QuaternionAutoDemo autoDemo = GetComponent<QuaternionAutoDemo>();
            CreateButtonRow(panel, ("AUTO DEMO", (Action)(() => autoDemo.PlayFullSequence())));
            CreateText(panel, "Keyboard: 1/2/3 presets  |  Space play  |  R reset", 14f, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.62f, 0.72f, 0.82f), 32f);
        }

        private void PopulateReadoutPanel(RectTransform panel)
        {
            AddPanelLayout(panel, 22, 11f);
            CreateText(panel, "LIVE ROTATION DATA", 24f, FontStyles.Bold, TextAlignmentOptions.Left, CyanColor, 42f);

            axisText = CreateReadout(panel, "Axis");
            angleText = CreateReadout(panel, "Angle");
            interpolationText = CreateReadout(panel, "Slerp t");
            quaternionText = CreateReadout(panel, "Quaternion");
            eulerText = CreateReadout(panel, "Euler");
            magnitudeText = CreateReadout(panel, "Magnitude");

            CreateSeparator(panel);
            CreateText(panel, "CORE FORMULAS", 20f, FontStyles.Bold, TextAlignmentOptions.Left, new Color(1f, 0.78f, 0.25f), 36f);
            CreateText(panel, "q = [ axis * sin(angle / 2), cos(angle / 2) ]", 17f, FontStyles.Normal, TextAlignmentOptions.Left, Color.white, 44f);
            CreateText(panel, "v' = q * (v, 0) * inverse(q)", 17f, FontStyles.Normal, TextAlignmentOptions.Left, Color.white, 36f);
            CreateText(panel, "q(t) = Slerp(qA, qB, t)", 17f, FontStyles.Normal, TextAlignmentOptions.Left, Color.white, 36f);

            CreateSeparator(panel);
            stateText = CreateText(panel, "Ready", 18f, FontStyles.Bold, TextAlignmentOptions.Left, PinkColor, 58f);
            explanationText = CreateText(panel, "Explanation", 16f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.78f, 0.86f, 0.96f), 92f);

            CreateText(panel, "AXES", 18f, FontStyles.Bold, TextAlignmentOptions.Left, Color.white, 30f);
            CreateText(panel, "X red   Y green   Z blue   rotation axis yellow", 15f, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.76f, 0.82f, 0.9f), 35f);
        }

        private TMP_Text CreateReadout(RectTransform parent, string placeholder)
        {
            RectTransform box = CreateRect(placeholder + " Box", parent);
            UnityEngine.UI.LayoutElement layout = box.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredHeight = 50f;
            AddImage(box.gameObject, new Color(0.025f, 0.045f, 0.085f, 0.92f), false);
            return CreateText(box, placeholder, 18f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, Color.white, 48f, new Vector4(13f, 5f, 10f, 5f));
        }

        private void CreateVisualizationLines()
        {
            Shader shader = FindRuntimeShader("Sprites/Default", "Unlit/Color", "UI/Default");
            lineMaterial = new Material(shader);
            localXAxisLine = CreateLine("Local X Axis", Color.red, 0.04f);
            localYAxisLine = CreateLine("Local Y Axis", Color.green, 0.04f);
            localZAxisLine = CreateLine("Local Z Axis", new Color(0.2f, 0.45f, 1f), 0.04f);
            rotationAxisLine = CreateLine("Rotation Axis", Color.yellow, 0.048f);
            directionPathLine = CreateLine("Slerp Direction Path", PinkColor, 0.03f);
        }

        private LineRenderer CreateLine(string objectName, Color color, float width)
        {
            GameObject lineObject = new GameObject(objectName);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.material = lineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 6;
            line.numCornerVertices = 4;
            line.positionCount = 2;
            return line;
        }

        private void UpdateWorldLines(Quaternion rotation)
        {
            if (modelRoot == null) return;
            Vector3 origin = modelRoot.position;
            DrawTwoPointLine(localXAxisLine, origin, origin + rotation * Vector3.right * AxisLength);
            DrawTwoPointLine(localYAxisLine, origin, origin + rotation * Vector3.up * AxisLength);
            DrawTwoPointLine(localZAxisLine, origin, origin + rotation * Vector3.forward * AxisLength);

            Vector3 axis = ReadAxisFromSliders();
            Vector3 halfAxis = axis * AxisLength * 1.1f;
            DrawTwoPointLine(rotationAxisLine, origin - halfAxis, origin + halfAxis);

            directionPathLine.positionCount = PathSamples + 1;
            for (int index = 0; index <= PathSamples; index++)
            {
                float t = index / (float)PathSamples;
                Quaternion sample = Quaternion.Slerp(startRotation, targetRotation, t);
                directionPathLine.SetPosition(index, origin + sample * Vector3.forward * AxisLength * 0.82f);
            }
        }

        private static void DrawTwoPointLine(LineRenderer line, Vector3 start, Vector3 end)
        {
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private RectTransform CreatePanel(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            RectTransform panel = CreateRect(objectName, parent);
            panel.anchorMin = anchorMin;
            panel.anchorMax = anchorMax;
            panel.pivot = pivot;
            panel.anchoredPosition = anchoredPosition;
            panel.sizeDelta = sizeDelta;
            AddImage(panel.gameObject, PanelColor, false);
            return panel;
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static UnityEngine.UI.Image AddImage(GameObject gameObject, Color color, bool raycastTarget)
        {
            UnityEngine.UI.Image image = gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static void AddPanelLayout(RectTransform panel, int padding, float spacing)
        {
            UnityEngine.UI.VerticalLayoutGroup layout = panel.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private TMP_Text CreateText(
            Transform parent,
            string value,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            Color color,
            float preferredHeight,
            Vector4? margin = null)
        {
            RectTransform rect = CreateRect("Text", parent);
            UnityEngine.UI.LayoutElement layout = rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredHeight = preferredHeight;

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = runtimeFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.enableAutoSizing = false;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            text.margin = margin ?? new Vector4(4f, 3f, 4f, 3f);
            return text;
        }

        private UnityEngine.UI.Slider CreateLabeledSlider(
            RectTransform parent,
            string label,
            float minimum,
            float maximum,
            float value,
            Color accent)
        {
            CreateText(parent, label, 16f, FontStyles.Bold, TextAlignmentOptions.Left, Color.white, 27f);
            RectTransform sliderRect = CreateRect(label + " Slider", parent);
            UnityEngine.UI.LayoutElement layout = sliderRect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredHeight = 28f;

            UnityEngine.UI.Slider slider = sliderRect.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.SetValueWithoutNotify(value);

            RectTransform background = CreateRect("Background", sliderRect);
            background.anchorMin = new Vector2(0f, 0.25f);
            background.anchorMax = new Vector2(1f, 0.75f);
            background.offsetMin = new Vector2(6f, 0f);
            background.offsetMax = new Vector2(-6f, 0f);
            AddImage(background.gameObject, new Color(0.12f, 0.17f, 0.25f), true);

            RectTransform fillArea = CreateRect("Fill Area", sliderRect);
            fillArea.anchorMin = new Vector2(0f, 0.25f);
            fillArea.anchorMax = new Vector2(1f, 0.75f);
            fillArea.offsetMin = new Vector2(8f, 0f);
            fillArea.offsetMax = new Vector2(-8f, 0f);

            RectTransform fill = CreateRect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            UnityEngine.UI.Image fillImage = AddImage(fill.gameObject, accent, false);

            RectTransform handleArea = CreateRect("Handle Slide Area", sliderRect);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);

            RectTransform handle = CreateRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(22f, 22f);
            UnityEngine.UI.Image handleImage = AddImage(handle.gameObject, Color.white, true);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            return slider;
        }

        private void CreateButtonRow(RectTransform parent, params (string label, Action action)[] buttons)
        {
            RectTransform row = CreateRect("Button Row", parent);
            UnityEngine.UI.LayoutElement rowElement = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            rowElement.preferredHeight = 48f;
            UnityEngine.UI.HorizontalLayoutGroup layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            foreach ((string label, Action action) in buttons)
            {
                RectTransform buttonRect = CreateRect(label + " Button", row);
                UnityEngine.UI.Image image = AddImage(buttonRect.gameObject, new Color(0.08f, 0.34f, 0.48f), true);
                UnityEngine.UI.Button button = buttonRect.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = image;
                UnityEngine.UI.ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.65f, 0.92f, 1f);
                colors.pressedColor = new Color(0.4f, 0.75f, 0.9f);
                button.colors = colors;
                button.onClick.AddListener(() => action());

                TMP_Text buttonText = CreateText(buttonRect, label, 14f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, 44f);
                RectTransform textRect = buttonText.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
            }
        }

        private static void CreateSeparator(RectTransform parent)
        {
            RectTransform separator = CreateRect("Separator", parent);
            UnityEngine.UI.LayoutElement layout = separator.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredHeight = 2f;
            AddImage(separator.gameObject, new Color(0.18f, 0.42f, 0.58f, 0.8f), false);
        }

        private static Material CreateStandardMaterial(string materialName, Color color, float metallic, float smoothness)
        {
            Shader shader = FindRuntimeShader("Standard", "Legacy Shaders/Diffuse", "UI/Default");
            Material material = new Material(shader) { name = materialName, color = color };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        private static Material CreateUnlitMaterial(string materialName, Color color)
        {
            Shader shader = FindRuntimeShader("Sprites/Default", "Unlit/Color", "UI/Default");
            Material material = new Material(shader) { name = materialName, color = color };
            return material;
        }

        private static Shader FindRuntimeShader(params string[] names)
        {
            foreach (string shaderName in names)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null) return shader;
            }

            throw new InvalidOperationException("No compatible runtime shader was found: " + string.Join(", ", names));
        }
    }
}
