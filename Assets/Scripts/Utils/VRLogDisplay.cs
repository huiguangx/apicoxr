using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// VR日志显示器 - 自动捕获Debug.Log并显示在VR中
/// 使用方法：挂载到相机上即可，代码中正常使用Debug.Log()
/// </summary>
public class VRLogDisplay : MonoBehaviour
{
    [Header("配置")]
    [SerializeField] private int maxLines = 100;

    private Text logText;
    private Queue<string> logs = new Queue<string>();

    void Awake()
    {
        CreateUI();
        Application.logMessageReceived += HandleLog;
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void CreateUI()
    {
        // 创建Canvas
        GameObject canvasObj = new GameObject("LogCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = new Vector3(0.4f, -0.3f, 1f);  // 右下角
        canvasObj.transform.localRotation = Quaternion.identity;
        canvasObj.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.GetComponent<RectTransform>().sizeDelta = new Vector2(500, 1500);

        // 创建背景
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        var bgImage = bgObj.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0, 0, 0, 0.5f);  // 透明黑
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 创建文本
        GameObject textObj = new GameObject("LogText");
        textObj.transform.SetParent(canvasObj.transform, false);
        logText = textObj.AddComponent<Text>();
        logText.fontSize = 14;
        logText.color = Color.white;
        logText.alignment = TextAnchor.UpperLeft;
        logText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");  // 使用Unity内置字体
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);

        logText.text = "Log Ready";
    }

    void HandleLog(string message, string stackTrace, LogType type)
    {
        string colorCode = type == LogType.Error ? "red" : type == LogType.Warning ? "yellow" : "white";
        string log = $"<color={colorCode}>{message}</color>";

        logs.Enqueue(log);
        while (logs.Count > maxLines)
        {
            logs.Dequeue();
        }

        logText.text = string.Join("\n", logs);
    }
}