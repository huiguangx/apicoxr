using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using DataTracking; // 添加DataTracking命名空间

/// <summary>
/// UI控制器 - 完全通过代码生成 UI，支持 XR 射线交互
/// 使用方法：在场景中创建一个空 GameObject，挂载此脚本即可
/// </summary>
public class UIController : MonoBehaviour
{
    [Header("Canvas 配置")]
    [Tooltip("UI距离相机的距离")]
    public float distanceFromCamera = 5f;

    [Tooltip("Canvas 宽度")]
    public float canvasWidth = 300f;

    [Tooltip("Canvas 高度")]
    public float canvasHeight = 300f;

    [Tooltip("Canvas 缩放（调整整体大小）")]
    public float canvasScale = 0.005f;

    [Header("Button 配置")]
    [Tooltip("按钮宽度（0 = 自动填充容器宽度）")]
    public float buttonWidth = 0f;

    [Tooltip("按钮高度")]
    public float buttonHeight = 100f;

    [Tooltip("按钮之间的间距")]
    public float buttonSpacing = 20f;

    [Header("其他配置")]
    [Tooltip("是否在启动时显示窗口")]
    public bool showOnStart = true;

    [Header("服务器配置")]
    [Tooltip("服务器基础地址 (如 192.168.1.100:5000 或 localhost:5000)")]
    public string serverBaseUrl = "127.0.0.1:5000";

    [Header("视频流配置")]
    [Tooltip("视频流服务器地址 (如 localhost:3000 或 192.168.1.100:8080)")]
    public string videoStreamBaseUrl = "localhost:5000";

    [Tooltip("视频流类型")]
    // public VideoStreamType videoStreamType = VideoStreamType.MJPEG;
    public VideoStreamType videoStreamType = VideoStreamType.WebRTC;

    [Tooltip("是否在启动时自动开启视频流")]
    public bool autoStartVideoStream = false;  // 改为默认false，避免遮挡UI

    // 内部引用
    private Canvas canvas;
    private GameObject modalWindow;
    private Text titleText;
    private Transform buttonsContainer;
    private List<Button> buttons = new List<Button>();
    private Camera mainCamera;

    // 用于检测参数变化
    private float lastCanvasWidth;
    private float lastCanvasHeight;
    private float lastCanvasScale;
    private float lastDistanceFromCamera;
    private float lastButtonWidth;
    private float lastButtonHeight;
    private float lastButtonSpacing;

    // 添加输入框相关字段
    private InputField serverUrlInputField;
    private Button confirmButton;
    private Text statusText;
    private DataTracking.DataTracking dataTracking;

    // 透视功能相关
    private Button seethroughToggleButton;
    private Text seethroughStatusText;

    // 视频流相关
    private VideoStream.StereoVideoStreamManager videoStreamManager;
    private VideoStream.StereoWebRTCStreamManager webRTCStreamManager;
    private InputField videoUrlInputField;
    private Button videoToggleButton;
    private Text videoStatusText;
    private Button streamTypeToggleButton;
    private Text streamTypeText;

    private void Awake()
    {
        mainCamera = Camera.main;
        EnsureEventSystem();
    }

    private void Start()
    {
        // 从 PlayerPrefs 加载服务器地址
        if (PlayerPrefs.HasKey("ServerBaseUrl"))
        {
            serverBaseUrl = PlayerPrefs.GetString("ServerBaseUrl");
            Debug.Log($"📥 从 PlayerPrefs 加载服务器地址: {serverBaseUrl}");
        }

        // 从 PlayerPrefs 加载视频流地址
        if (PlayerPrefs.HasKey("VideoStreamBaseUrl"))
        {
            videoStreamBaseUrl = PlayerPrefs.GetString("VideoStreamBaseUrl");
            Debug.Log($"📥 从 PlayerPrefs 加载视频流地址: {videoStreamBaseUrl}");
        }

        CreateUI();
        // 强制设置为WebRTC
        videoStreamType = VideoStreamType.MJPEG;
        // 初始化参数缓存
        lastCanvasWidth = canvasWidth;
        lastCanvasHeight = canvasHeight;
        lastCanvasScale = canvasScale;
        lastDistanceFromCamera = distanceFromCamera;
        lastButtonWidth = buttonWidth;
        lastButtonHeight = buttonHeight;
        lastButtonSpacing = buttonSpacing;

        if (!showOnStart)
        {
            HideModal();
        }

        // 获取DataTracking实例
        dataTracking = FindObjectOfType<DataTracking.DataTracking>();

        // 获取视频流管理器实例（MJPEG）
        videoStreamManager = FindObjectOfType<VideoStream.StereoVideoStreamManager>();
        if (videoStreamManager == null)
        {
            Debug.LogWarning("⚠️ 未找到 StereoVideoStreamManager 组件，MJPEG视频流功能将不可用");
        }

        // 获取WebRTC流管理器实例
        webRTCStreamManager = FindObjectOfType<VideoStream.StereoWebRTCStreamManager>();
        if (webRTCStreamManager == null)
        {
            Debug.LogWarning("⚠️ 未找到 StereoWebRTCStreamManager 组件，WebRTC视频流功能将不可用");
        }

        // 自动启动视频流
        if (autoStartVideoStream)
        {
            // 延迟1秒后自动启动视频流
            StartCoroutine(AutoStartVideoStreamDelayed());
        }

        // 初始化输入框
        InitializeServerUrlInput();
    }

    /// <summary>
    /// 延迟自动启动视频流
    /// </summary>
    private IEnumerator AutoStartVideoStreamDelayed()
    {
        yield return new WaitForSeconds(1f);
        if (videoStreamType == VideoStreamType.MJPEG)
        {
            string leftUrl = $"http://{videoStreamBaseUrl}/mjpeg/left";
            string rightUrl = $"http://{videoStreamBaseUrl}/mjpeg/right";

            Debug.Log($"🎬 自动启动MJPEG视频流\n   左眼: {leftUrl}\n   右眼: {rightUrl}");

            if (videoStreamManager != null)
            {
                videoStreamManager.StartStreaming(leftUrl, rightUrl);
            }
        }
        else if (videoStreamType == VideoStreamType.WebRTC)
        {
            Debug.Log($"🎬 自动启动WebRTC视频流\n   服务器: https://{videoStreamBaseUrl}");

            if (webRTCStreamManager != null)
            {
                webRTCStreamManager.serverUrl = $"https://{videoStreamBaseUrl}";
                webRTCStreamManager.StartStreaming();
            }
        }

        // 更新UI状态（如果UI已创建）
        if (videoToggleButton != null)
        {
            Text btnText = videoToggleButton.GetComponentInChildren<Text>();
            if (btnText != null)
                btnText.text = "关闭视频流";
        }

        if (videoStatusText != null)
        {
            videoStatusText.text = $"视频流 ({videoStreamType}): 连接中...";
            videoStatusText.color = Color.yellow;
        }
    }

    // 初始化服务器URL输入框
    private void InitializeServerUrlInput()
    {
        if (serverUrlInputField != null)
        {
            serverUrlInputField.text = serverBaseUrl;
        }
    }

    private void Update()
    {
        // 更新视频流状态显示
        UpdateVideoStreamStatus();

        // 检测 Canvas 参数变化
        if (canvas != null)
        {
            bool needUpdateCanvas = false;
            bool needUpdatePosition = false;
            bool needUpdateButtons = false;

            // 检测 Canvas 尺寸变化
            if (lastCanvasWidth != canvasWidth || lastCanvasHeight != canvasHeight)
            {
                needUpdateCanvas = true;
                lastCanvasWidth = canvasWidth;
                lastCanvasHeight = canvasHeight;
            }

            // 检测 Canvas 缩放变化
            if (lastCanvasScale != canvasScale)
            {
                canvas.transform.localScale = Vector3.one * canvasScale;
                lastCanvasScale = canvasScale;
            }

            // 检测距离变化
            if (lastDistanceFromCamera != distanceFromCamera)
            {
                needUpdatePosition = true;
                lastDistanceFromCamera = distanceFromCamera;
            }

            // 检测按钮参数变化
            if (lastButtonWidth != buttonWidth || lastButtonHeight != buttonHeight || lastButtonSpacing != buttonSpacing)
            {
                needUpdateButtons = true;
                lastButtonWidth = buttonWidth;
                lastButtonHeight = buttonHeight;
                lastButtonSpacing = buttonSpacing;
            }

            // 执行更新
            if (needUpdateCanvas)
            {
                UpdateCanvasSize();
            }

            if (needUpdatePosition)
            {
                UpdateUIPosition();
            }

            if (needUpdateButtons)
            {
                UpdateButtons();
            }
        }
    }

    /// <summary>
    /// 更新 Canvas 尺寸
    /// </summary>
    private void UpdateCanvasSize()
    {
        if (canvas != null)
        {
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(canvasWidth, canvasHeight);
        }
    }

    /// <summary>
    /// 更新 UI 位置
    /// </summary>
    private void UpdateUIPosition()
    {
        if (canvas != null && mainCamera != null)
        {
            Vector3 cameraPos = mainCamera.transform.position;
            Vector3 cameraForward = mainCamera.transform.forward;
            canvas.transform.position = cameraPos + cameraForward * distanceFromCamera;
            canvas.transform.LookAt(cameraPos);
            canvas.transform.Rotate(0, 180, 0);
        }
    }

    /// <summary>
    /// 更新所有按钮的尺寸和布局
    /// </summary>
    private void UpdateButtons()
    {
        if (buttonsContainer == null) return;

        // 更新布局组件
        VerticalLayoutGroup layout = buttonsContainer.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = buttonSpacing;
            layout.childControlWidth = (buttonWidth == 0);
            layout.childForceExpandWidth = (buttonWidth == 0);
        }

        // 更新每个按钮的尺寸
        foreach (Button btn in buttons)
        {
            if (btn != null)
            {
                RectTransform rect = btn.GetComponent<RectTransform>();
                if (buttonWidth > 0)
                {
                    rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
                }
                else
                {
                    rect.sizeDelta = new Vector2(0, buttonHeight);
                }
            }
        }
    }

    /// <summary>
    /// 确保场景中有 EventSystem
    /// </summary>
    private void EnsureEventSystem()
    {
        EventSystem eventSystem = FindObjectOfType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<StandaloneInputModule>();
        }
    }

    /// <summary>
    /// 创建整个 UI 系统
    /// </summary>
    private void CreateUI()
    {
        // 1. 创建 Canvas
        CreateCanvas();

        // 2. 创建模态窗口
        CreateModalWindow();

        // 3. 创建标题
        CreateTitle();

        // 4. 创建按钮容器
        CreateButtonsContainer();

        // 5. 添加服务器URL输入框
        CreateServerUrlInputField();

        // 6. 添加默认按钮
        AddDefaultButtons();
    }

    /// <summary>
    /// 创建 Canvas
    /// </summary>
    private void CreateCanvas()
    {
        GameObject canvasObj = new GameObject("UICanvas");
        canvasObj.transform.SetParent(transform);

        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // 设置渲染顺序，确保UI在视频流之上
        canvas.sortingOrder = 100; // 高优先级，确保在其他元素之上

        // 设置 Canvas 位置（在相机前方）
        if (mainCamera != null)
        {
            Vector3 cameraPos = mainCamera.transform.position;
            Vector3 cameraForward = mainCamera.transform.forward;
            canvasObj.transform.position = cameraPos + cameraForward * distanceFromCamera;
            canvasObj.transform.LookAt(cameraPos);
            canvasObj.transform.Rotate(0, 180, 0);
        }

        // 设置 Canvas 尺寸和缩放
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(canvasWidth, canvasHeight);
        canvasObj.transform.localScale = Vector3.one * canvasScale;

        // 添加 CanvasScaler
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;

        // 尝试添加 TrackedDeviceGraphicRaycaster（用于 XR）
        var raycasterType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
        if (raycasterType != null)
        {
            canvasObj.AddComponent(raycasterType);
        }
        else
        {
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        Debug.Log($"✅ UICanvas创建完成，SortingOrder={canvas.sortingOrder}，确保UI在视频流上方");
    }

    /// <summary>
    /// 创建模态窗口背景
    /// </summary>
    private void CreateModalWindow()
    {
        modalWindow = new GameObject("ModalWindow");
        modalWindow.transform.SetParent(canvas.transform, false);

        RectTransform rect = modalWindow.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        Image bgImage = modalWindow.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f); // 深灰色不透明
    }

    /// <summary>
    /// 创建标题栏
    /// </summary>
    private void CreateTitle()
    {
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(modalWindow.transform, false);

        RectTransform rect = titleObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = new Vector2(0, 100);
        rect.anchoredPosition = Vector2.zero;

        Image bgImage = titleObj.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);

        // 创建标题文本
        GameObject textObj = new GameObject("TitleText");
        textObj.transform.SetParent(titleObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        titleText = textObj.AddComponent<Text>();
        titleText.text = "VR UI Test Window";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 48;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.fontStyle = FontStyle.Bold;
    }

    /// <summary>
    /// 创建按钮容器
    /// </summary>
    private void CreateButtonsContainer()
    {
        if (modalWindow == null)
        {
            return;
        }

        GameObject containerObj = new GameObject("ButtonsContainer");
        containerObj.transform.SetParent(modalWindow.transform, false);

        RectTransform rect = containerObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(40, 40);   // 左、下边距
        rect.offsetMax = new Vector2(-40, -120); // 右、上边距（为标题留空间）

        buttonsContainer = containerObj.transform;

        // 添加垂直布局
        VerticalLayoutGroup layout = containerObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = buttonSpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = (buttonWidth == 0); // 如果 buttonWidth = 0，自动填充宽度
        layout.childControlHeight = false;
        layout.childForceExpandWidth = (buttonWidth == 0);
        layout.childForceExpandHeight = false;
    }

    /// <summary>
    /// 创建服务器URL输入框
    /// </summary>
    private void CreateServerUrlInputField()
    {
        if (buttonsContainer == null) return;

        // 创建输入框容器
        GameObject inputContainer = new GameObject("ServerUrlInputContainer");
        inputContainer.transform.SetParent(buttonsContainer, false);

        RectTransform containerRect = inputContainer.AddComponent<RectTransform>();

        containerRect.sizeDelta = new Vector2(500f, 120f);

        // 添加 LayoutElement 控制在垂直布局中的尺寸
        LayoutElement layoutElement = inputContainer.AddComponent<LayoutElement>();
        layoutElement.minHeight = 120;
        layoutElement.preferredHeight = 120;
        layoutElement.flexibleWidth = 1; // 自动填充宽度

        // 创建输入框
        GameObject inputFieldObj = new GameObject("ServerUrlInputField");
        inputFieldObj.transform.SetParent(inputContainer.transform, false);

        RectTransform inputRect = inputFieldObj.AddComponent<RectTransform>();
        inputRect.anchorMin = Vector2.zero;
        inputRect.anchorMax = new Vector2(0.7f, 1f);
        inputRect.pivot = new Vector2(0, 0.5f);
        inputRect.offsetMin = new Vector2(10, 10);  // 增加左右内边距
        inputRect.offsetMax = new Vector2(-10, -10);

        serverUrlInputField = inputFieldObj.AddComponent<InputField>();
        serverUrlInputField.text = "127.0.0.1:5000";

        Image inputBg = inputFieldObj.AddComponent<Image>();
        inputBg.color = new Color(0.3f, 0.3f, 0.3f, 1f); // 浅灰色背景

        serverUrlInputField.targetGraphic = inputBg;
        serverUrlInputField.placeholder = CreatePlaceholder("输入 IP:端口 (如 192.168.1.100:5000)");

        Text inputText = CreateTextComponent(inputFieldObj, "ServerUrlInputText");
        inputText.alignment = TextAnchor.MiddleLeft;
        inputText.fontSize = 32;
        serverUrlInputField.textComponent = inputText;

        // 创建确认按钮
        GameObject confirmBtnObj = new GameObject("ConfirmButton");
        confirmBtnObj.transform.SetParent(inputContainer.transform, false);

        RectTransform confirmRect = confirmBtnObj.AddComponent<RectTransform>();
        confirmRect.anchorMin = new Vector2(0.7f, 0);
        confirmRect.anchorMax = Vector2.one;
        confirmRect.pivot = new Vector2(0.5f, 0.5f);
        confirmRect.offsetMin = new Vector2(10, 10);
        confirmRect.offsetMax = new Vector2(-10, -10);

        confirmButton = confirmBtnObj.AddComponent<Button>();

        Image confirmBg = confirmBtnObj.AddComponent<Image>();
        confirmBg.color = new Color(0.2f, 0.6f, 1f, 1f);
        confirmButton.targetGraphic = confirmBg;

        Text confirmText = CreateTextComponent(confirmBtnObj, "ConfirmButtonText");
        confirmText.text = "确认";
        confirmText.fontSize = 32;
        confirmText.alignment = TextAnchor.MiddleCenter;

        confirmButton.onClick.AddListener(OnConfirmServerUrl);

        // 创建状态文本（放在容器下方）
        GameObject statusObj = new GameObject("StatusText");
        statusObj.transform.SetParent(inputContainer.transform, false);

        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 0);
        statusRect.anchorMax = new Vector2(1, 0);
        statusRect.pivot = new Vector2(0.5f, 0);
        statusRect.offsetMin = new Vector2(10, -35);
        statusRect.offsetMax = new Vector2(-10, -5);

        statusText = CreateTextComponent(statusObj, "StatusText");
        statusText.fontSize = 24;
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.color = Color.green;
        statusText.text = "";
    }

    // 创建占位符文本
    private Text CreatePlaceholder(string placeholderText)
    {
        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(serverUrlInputField.transform, false);

        RectTransform rect = placeholderObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        Text placeholder = placeholderObj.AddComponent<Text>();
        placeholder.text = placeholderText;
        placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholder.fontSize = 36;
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);

        return placeholder;
    }

    // 创建文本组件
    private Text CreateTextComponent(GameObject parent, string name)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 36;
        text.color = Color.white;

        return text;
    }

    /// <summary>
    /// 添加默认按钮
    /// </summary>
    private void AddDefaultButtons()
    {
        // 添加透视开关按钮（放在最上面）
        CreateSeethroughToggle();

        // 添加视频流控制（紧接在透视开关之后）
        CreateVideoStreamControls();

        AddButton("CONFIRM", OnConfirmClicked, new Color(0.2f, 0.6f, 1f));
        AddButton("CANCEL", OnCancelClicked, new Color(0.7f, 0.7f, 0.7f));
        AddButton("APPLY", OnApplyClicked, new Color(0.3f, 0.7f, 0.3f));
    }

    /// <summary>
    /// 动态添加按钮
    /// </summary>
    public Button AddButton(string buttonText, UnityAction onClick, Color? buttonColor = null)
    {
        if (buttonsContainer == null)
        {
            return null;
        }

        GameObject buttonObj = new GameObject($"Button_{buttonText}");
        buttonObj.transform.SetParent(buttonsContainer, false);

        RectTransform rect = buttonObj.AddComponent<RectTransform>();

        // 根据 buttonWidth 设置按钮尺寸
        if (buttonWidth > 0)
        {
            rect.sizeDelta = new Vector2(buttonWidth, buttonHeight); // 固定宽高
        }
        else
        {
            rect.sizeDelta = new Vector2(0, buttonHeight); // 只设置高度，宽度由布局控制
        }

        // 先创建 Image 组件
        Image bgImage = buttonObj.AddComponent<Image>();

        Button button = buttonObj.AddComponent<Button>();

        Color normalColor = buttonColor ?? new Color(0.2f, 0.6f, 1f);

        // 设置按钮的 targetGraphic（非常重要！）
        button.targetGraphic = bgImage;

        // 显式设置 Transition 为 ColorTint（确保 hover 效果生效）
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.4f); // hover 时变浅（混合白色）
        colors.pressedColor = normalColor * 0.7f;                              // 点击时变深
        colors.selectedColor = normalColor;
        colors.fadeDuration = 0.15f; // 平滑过渡
        button.colors = colors;

        bgImage.color = normalColor;

        // 添加 EventTrigger 来处理 hover 事件（额外的视觉反馈）
        EventTrigger trigger = buttonObj.AddComponent<EventTrigger>();

        // PointerEnter 事件
        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => OnButtonHoverEnter(buttonObj, buttonText));
        trigger.triggers.Add(enterEntry);

        // PointerExit 事件
        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => OnButtonHoverExit(buttonObj, buttonText));
        trigger.triggers.Add(exitEntry);

        // 创建按钮文本
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.text = buttonText;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 36;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.fontStyle = FontStyle.Bold;

        button.onClick.AddListener(onClick);
        buttons.Add(button);

        return button;
    }

    /// <summary>
    /// 按钮 Hover 进入事件
    /// </summary>
    private void OnButtonHoverEnter(GameObject buttonObj, string buttonText)
    {
        Debug.Log($"🎯 Hover 进入: {buttonText}");
        // 可以在这里添加额外的视觉效果，比如缩放动画
        // buttonObj.transform.localScale = Vector3.one * 1.05f;
    }

    /// <summary>
    /// 按钮 Hover 退出事件
    /// </summary>
    private void OnButtonHoverExit(GameObject buttonObj, string buttonText)
    {
        Debug.Log($"🎯 Hover 退出: {buttonText}");
        // buttonObj.transform.localScale = Vector3.one;
    }

    // 确认服务器URL按钮点击事件
    private void OnConfirmServerUrl()
    {
        if (serverUrlInputField != null)
        {
            string newBaseUrl = serverUrlInputField.text.Trim();

            if (!string.IsNullOrEmpty(newBaseUrl))
            {
                // 移除协议部分（如果用户输入了的话）
                if (newBaseUrl.StartsWith("https://"))
                {
                    newBaseUrl = newBaseUrl.Substring(8);
                }
                else if (newBaseUrl.StartsWith("http://"))
                {
                    newBaseUrl = newBaseUrl.Substring(7);
                }

                // 移除末尾的斜杠
                newBaseUrl = newBaseUrl.TrimEnd('/');

                // 验证格式（简单检查是否包含冒号）
                if (newBaseUrl.Contains(":") || newBaseUrl == "localhost")
                {
                    serverBaseUrl = newBaseUrl;

                    // 更新输入框（规范化后的 URL）
                    serverUrlInputField.text = serverBaseUrl;

                    // 保存到PlayerPrefs以便下次启动时使用
                    PlayerPrefs.SetString("ServerBaseUrl", serverBaseUrl);
                    PlayerPrefs.Save();

                    // 更新状态文本
                    if (statusText != null)
                    {
                        statusText.text = "服务器地址已更新";
                        statusText.color = Color.green;
                    }

                    Debug.Log($"✅ 服务器基础地址已更新为: {serverBaseUrl}");
                    Debug.Log($"   - VR 数据 URL: https://{serverBaseUrl}/poseData");
                    Debug.Log($"   - 消息 URL: https://{serverBaseUrl}/msg");
                }
                else
                {
                    // 格式无效
                    if (statusText != null)
                    {
                        statusText.text = "格式无效，应为 IP:端口";
                        statusText.color = Color.red;
                    }
                }
            }
            else
            {
                // URL为空
                if (statusText != null)
                {
                    statusText.text = "地址不能为空";
                    statusText.color = Color.red;
                }
            }
        }
    }

    // 验证URL格式
    private bool IsValidUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;

        // 如果URL不包含协议，则自动添加https://
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            url = "https://" + url;
        }

        try
        {
            var uri = new System.Uri(url);
            return uri.Scheme == System.Uri.UriSchemeHttp || uri.Scheme == System.Uri.UriSchemeHttps;
        }
        catch
        {
            return false;
        }
    }

    public void ShowModal(string title = "VR UI Test Window")
    {
        if (modalWindow != null)
        {
            modalWindow.SetActive(true);
            if (titleText != null)
            {
                titleText.text = title;
            }
        }
    }

    public void HideModal()
    {
        if (modalWindow != null)
        {
            modalWindow.SetActive(false);
        }
    }

    private void OnConfirmClicked()
    {
        Debug.Log("✅✅✅ BUTTON CONFIRM 按钮被点击！");
        // HideModal();
    }

    private void OnCancelClicked()
    {
        Debug.Log("❌❌❌ BUTTON CANCEL 按钮被点击！");
        // HideModal();
    }

    private void OnApplyClicked()
    {
        Debug.Log("✔️✔️✔️ BUTTON APPLY 按钮被点击！");
    }

    /// <summary>
    /// 创建透视开关UI（按钮+状态文本）
    /// </summary>
    private void CreateSeethroughToggle()
    {
        if (buttonsContainer == null) return;

        // 创建容器
        GameObject toggleContainer = new GameObject("SeethroughToggleContainer");
        toggleContainer.transform.SetParent(buttonsContainer, false);

        RectTransform containerRect = toggleContainer.AddComponent<RectTransform>();
        containerRect.sizeDelta = new Vector2(500f, 120f);

        LayoutElement layoutElement = toggleContainer.AddComponent<LayoutElement>();
        layoutElement.minHeight = 120;
        layoutElement.preferredHeight = 120;
        layoutElement.flexibleWidth = 1;

        // 创建切换按钮（左侧60%）
        GameObject btnObj = new GameObject("SeethroughToggleButton");
        btnObj.transform.SetParent(toggleContainer.transform, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = Vector2.zero;
        btnRect.anchorMax = new Vector2(0.6f, 1f);
        btnRect.pivot = new Vector2(0, 0.5f);
        btnRect.offsetMin = new Vector2(10, 10);
        btnRect.offsetMax = new Vector2(-5, -10);

        seethroughToggleButton = btnObj.AddComponent<Button>();

        Image btnBg = btnObj.AddComponent<Image>();
        btnBg.color = new Color(0.2f, 0.7f, 0.3f, 1f); // 绿色
        seethroughToggleButton.targetGraphic = btnBg;

        Text btnText = CreateTextComponent(btnObj, "ButtonText");
        btnText.text = "切换透视";
        btnText.fontSize = 32;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.fontStyle = FontStyle.Bold;

        seethroughToggleButton.onClick.AddListener(OnToggleSeethrough);

        // 创建状态文本（右侧40%）
        GameObject statusObj = new GameObject("SeethroughStatusText");
        statusObj.transform.SetParent(toggleContainer.transform, false);

        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.6f, 0);
        statusRect.anchorMax = Vector2.one;
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.offsetMin = new Vector2(5, 10);
        statusRect.offsetMax = new Vector2(-10, -10);

        // 添加背景
        Image statusBg = statusObj.AddComponent<Image>();
        statusBg.color = new Color(0.25f, 0.25f, 0.25f, 1f);

        seethroughStatusText = CreateTextComponent(statusObj, "StatusText");
        seethroughStatusText.fontSize = 28;
        seethroughStatusText.alignment = TextAnchor.MiddleCenter;
        seethroughStatusText.fontStyle = FontStyle.Bold;

        // 初始化状态文本
        UpdateSeethroughStatusText();
    }

    /// <summary>
    /// 切换透视功能
    /// </summary>
    private void OnToggleSeethrough()
    {
        if (dataTracking != null)
        {
            dataTracking.ToggleSeethrough();
            UpdateSeethroughStatusText();
        }
        else
        {
            Debug.LogError("❌ DataTracking 未找到！");
        }
    }

    /// <summary>
    /// 更新透视状态文本
    /// </summary>
    private void UpdateSeethroughStatusText()
    {
        if (seethroughStatusText == null) return;

        if (dataTracking != null)
        {
            bool isEnabled = dataTracking.IsSeethroughEnabled();
            seethroughStatusText.text = isEnabled ? "✅ 已开启" : "❌ 已关闭";
            seethroughStatusText.color = isEnabled ? Color.green : Color.red;
        }
        else
        {
            seethroughStatusText.text = "未知";
            seethroughStatusText.color = Color.gray;
        }
    }

    /// <summary>
    /// 创建视频流控制UI（输入框+开关按钮+状态）
    /// </summary>
    private void CreateVideoStreamControls()
    {
        if (buttonsContainer == null) return;

        // 创建容器
        GameObject videoContainer = new GameObject("VideoStreamContainer");
        videoContainer.transform.SetParent(buttonsContainer, false);

        RectTransform containerRect = videoContainer.AddComponent<RectTransform>();
        containerRect.sizeDelta = new Vector2(500f, 200f);

        LayoutElement layoutElement = videoContainer.AddComponent<LayoutElement>();
        layoutElement.minHeight = 200;
        layoutElement.preferredHeight = 200;
        layoutElement.flexibleWidth = 1;

        // 创建标题文本
        GameObject titleObj = new GameObject("VideoStreamTitle");
        titleObj.transform.SetParent(videoContainer.transform, false);

        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0.7f);
        titleRect.anchorMax = new Vector2(1, 1f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.offsetMin = new Vector2(10, 0);
        titleRect.offsetMax = new Vector2(-10, 0);

        Text titleText = CreateTextComponent(titleObj, "TitleText");
        titleText.text = "视频流设置";
        titleText.fontSize = 28;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = new Color(0.8f, 0.8f, 0.8f, 1f);

        // 创建输入框（上半部分，0.4-0.7）
        GameObject inputFieldObj = new GameObject("VideoUrlInputField");
        inputFieldObj.transform.SetParent(videoContainer.transform, false);

        RectTransform inputRect = inputFieldObj.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0, 0.4f);
        inputRect.anchorMax = new Vector2(0.65f, 0.7f);
        inputRect.pivot = new Vector2(0, 0.5f);
        inputRect.offsetMin = new Vector2(10, 0);
        inputRect.offsetMax = new Vector2(-5, 0);

        videoUrlInputField = inputFieldObj.AddComponent<InputField>();
        videoUrlInputField.text = videoStreamBaseUrl;

        Image inputBg = inputFieldObj.AddComponent<Image>();
        inputBg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

        videoUrlInputField.targetGraphic = inputBg;
        videoUrlInputField.placeholder = CreatePlaceholder("例: localhost:5000");

        Text inputText = CreateTextComponent(inputFieldObj, "InputText");
        inputText.alignment = TextAnchor.MiddleLeft;
        inputText.fontSize = 28;
        videoUrlInputField.textComponent = inputText;

        // 创建开关按钮（上半部分，0.65-1.0）
        GameObject toggleBtnObj = new GameObject("VideoToggleButton");
        toggleBtnObj.transform.SetParent(videoContainer.transform, false);

        RectTransform toggleRect = toggleBtnObj.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0.65f, 0.4f);
        toggleRect.anchorMax = new Vector2(1f, 0.7f);
        toggleRect.pivot = new Vector2(0.5f, 0.5f);
        toggleRect.offsetMin = new Vector2(5, 0);
        toggleRect.offsetMax = new Vector2(-10, 0);

        videoToggleButton = toggleBtnObj.AddComponent<Button>();

        Image toggleBg = toggleBtnObj.AddComponent<Image>();
        toggleBg.color = new Color(0.2f, 0.7f, 0.9f, 1f); // 青色
        videoToggleButton.targetGraphic = toggleBg;

        Text toggleText = CreateTextComponent(toggleBtnObj, "ButtonText");
        toggleText.text = "开启视频流";
        toggleText.fontSize = 28;
        toggleText.alignment = TextAnchor.MiddleCenter;
        toggleText.fontStyle = FontStyle.Bold;

        videoToggleButton.onClick.AddListener(OnVideoToggleClicked);

        // 创建状态文本（下半部分）
        GameObject statusObj = new GameObject("VideoStatusText");
        statusObj.transform.SetParent(videoContainer.transform, false);

        RectTransform statusRect = statusObj.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 0);
        statusRect.anchorMax = new Vector2(1, 0.4f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.offsetMin = new Vector2(10, 5);
        statusRect.offsetMax = new Vector2(-10, -5);

        // 添加背景
        Image statusBg = statusObj.AddComponent<Image>();
        statusBg.color = new Color(0.25f, 0.25f, 0.25f, 1f);

        videoStatusText = CreateTextComponent(statusObj, "StatusText");
        videoStatusText.fontSize = 26;
        videoStatusText.alignment = TextAnchor.MiddleCenter;
        videoStatusText.fontStyle = FontStyle.Bold;
        videoStatusText.text = "视频流: 未连接";
        videoStatusText.color = Color.gray;
    }

    /// <summary>
    /// 视频流开关按钮点击事件
    /// </summary>
    private void OnVideoToggleClicked()
    {
        bool isCurrentlyStreaming = false;

        // 检查当前流类型的管理器是否正在流式传输
        if (videoStreamType == VideoStreamType.MJPEG)
        {
            if (videoStreamManager == null)
            {
                Debug.LogError("❌ 未找到 StereoVideoStreamManager 组件");
                if (videoStatusText != null)
                {
                    videoStatusText.text = "错误: 未找到MJPEG视频流管理器";
                    videoStatusText.color = Color.red;
                }
                return;
            }
            isCurrentlyStreaming = videoStreamManager.IsStreaming;
        }
        else if (videoStreamType == VideoStreamType.WebRTC)
        {
            if (webRTCStreamManager == null)
            {
                Debug.LogError("❌ 未找到 StereoWebRTCStreamManager 组件");
                if (videoStatusText != null)
                {
                    videoStatusText.text = "错误: 未找到WebRTC视频流管理器";
                    videoStatusText.color = Color.red;
                }
                return;
            }
            isCurrentlyStreaming = webRTCStreamManager.IsStreaming;
        }

        if (isCurrentlyStreaming)
        {
            // 停止视频流
            if (videoStreamType == VideoStreamType.MJPEG && videoStreamManager != null)
            {
                videoStreamManager.StopStreaming();
            }
            else if (videoStreamType == VideoStreamType.WebRTC && webRTCStreamManager != null)
            {
                webRTCStreamManager.StopStreaming();
            }

            if (videoToggleButton != null)
            {
                Text btnText = videoToggleButton.GetComponentInChildren<Text>();
                if (btnText != null)
                    btnText.text = "开启视频流";
            }

            if (videoStatusText != null)
            {
                videoStatusText.text = $"视频流 ({videoStreamType}): 已停止";
                videoStatusText.color = Color.gray;
            }

            Debug.Log($"🛑 {videoStreamType}视频流已停止");
        }
        else
        {
            // 保存URL
            if (videoUrlInputField != null)
            {
                videoStreamBaseUrl = videoUrlInputField.text.Trim();

                // 移除协议部分
                if (videoStreamBaseUrl.StartsWith("http://"))
                    videoStreamBaseUrl = videoStreamBaseUrl.Substring(7);
                else if (videoStreamBaseUrl.StartsWith("https://"))
                    videoStreamBaseUrl = videoStreamBaseUrl.Substring(8);

                videoStreamBaseUrl = videoStreamBaseUrl.TrimEnd('/');

                // 保存到PlayerPrefs
                PlayerPrefs.SetString("VideoStreamBaseUrl", videoStreamBaseUrl);
                PlayerPrefs.Save();

                // 更新输入框
                videoUrlInputField.text = videoStreamBaseUrl;
            }

            // 启动对应类型的视频流
            if (videoStreamType == VideoStreamType.MJPEG)
            {
                // 构建MJPEG URL
                string leftUrl = $"http://{videoStreamBaseUrl}/mjpeg/left";
                string rightUrl = $"http://{videoStreamBaseUrl}/mjpeg/right";

                Debug.Log($"🎬 启动MJPEG视频流\n   左眼: {leftUrl}\n   右眼: {rightUrl}");

                // 启动视频流
                if (videoStreamManager != null)
                {
                    videoStreamManager.StartStreaming(leftUrl, rightUrl);
                }
            }
            else if (videoStreamType == VideoStreamType.WebRTC)
            {
                Debug.Log($"🎬 启动WebRTC视频流\n   服务器: https://{videoStreamBaseUrl}");

                // 启动WebRTC流
                if (webRTCStreamManager != null)
                {
                    webRTCStreamManager.serverUrl = $"https://{videoStreamBaseUrl}";
                    webRTCStreamManager.StartStreaming();
                }
            }

            if (videoToggleButton != null)
            {
                Text btnText = videoToggleButton.GetComponentInChildren<Text>();
                if (btnText != null)
                    btnText.text = "关闭视频流";
            }

            if (videoStatusText != null)
            {
                videoStatusText.text = $"视频流 ({videoStreamType}): 连接中...";
                videoStatusText.color = Color.yellow;
            }
        }
    }

    /// <summary>
    /// 更新视频流状态显示
    /// </summary>
    private void UpdateVideoStreamStatus()
    {
        if (videoStreamManager == null || videoStatusText == null)
            return;

        if (videoStreamManager.IsStreaming)
        {
            float fps = videoStreamManager.CurrentFPS;
            videoStatusText.text = $"视频流: 运行中 ({fps:F1} FPS)";
            videoStatusText.color = fps > 15f ? Color.green : Color.yellow;
        }
    }

    private void OnDestroy()
    {
        foreach (Button btn in buttons)
        {
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
            }
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmServerUrl);
        }

        if (seethroughToggleButton != null)
        {
            seethroughToggleButton.onClick.RemoveListener(OnToggleSeethrough);
        }

        if (videoToggleButton != null)
        {
            videoToggleButton.onClick.RemoveListener(OnVideoToggleClicked);
        }
    }
}

/// <summary>
/// 视频流类型枚举
/// </summary>
public enum VideoStreamType
{
    MJPEG,   // MJPEG流（HTTP multipart）
    WebRTC   // WebRTC流（P2P实时通信）
}