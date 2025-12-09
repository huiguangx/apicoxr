using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// 机器人远程遥操作视觉系统
/// 将机器人双目摄像头画面以第一人称视角显示在 VR 中
/// </summary>
public class RobotTeleoperationViewer : MonoBehaviour
{
    [Header("机器人视频流地址")]
    public string leftEyeUrl = "http://localhost:3000/left";
    public string rightEyeUrl = "http://localhost:3000/right";

    [Header("显示模式")]
    [Tooltip("大屏模式：距离眼睛 0.5 米，填充大部分视野")]
    public DisplayMode displayMode = DisplayMode.ImmersiveLargeScreen;

    [Header("自定义设置")]
    [Tooltip("视频屏幕距离眼睛的距离（米）")]
    public float distanceFromEyes = 0.5f;

    [Tooltip("屏幕宽度（米）")]
    public float screenWidth = 1.6f;

    [Tooltip("屏幕高度（米）")]
    public float screenHeight = 0.9f;

    [Header("自动启动")]
    public bool autoStart = true;

    private GameObject videoQuad;
    private Material stereoMaterial;
    private Texture2D leftEyeTexture;
    private Texture2D rightEyeTexture;
    private MjpegDecoder leftDecoder;
    private MjpegDecoder rightDecoder;
    private bool isActive = false;
    private Camera mainCamera;

    public enum DisplayMode
    {
        ImmersiveLargeScreen,  // 沉浸式大屏（推荐）
        FullView,              // 填充整个视野
        CustomDistance         // 自定义距离
    }

    void Start()
    {
        Debug.Log("=== 机器人远程遥操作视觉系统启动 ===");

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("未找到主摄像机！");
            return;
        }

        CreateVideoQuad();
        CreateStereoMaterial();

        if (autoStart)
        {
            Invoke("StartViewing", 0.5f);
        }
    }

    void CreateVideoQuad()
    {
        // 根据显示模式设置参数
        switch (displayMode)
        {
            case DisplayMode.ImmersiveLargeScreen:
                distanceFromEyes = 0.5f;
                screenWidth = 1.6f;
                screenHeight = 0.9f;
                break;
            case DisplayMode.FullView:
                distanceFromEyes = 0.3f;
                screenWidth = 1.0f;
                screenHeight = 0.6f;
                break;
        }

        // 创建 Quad 作为摄像机的子对象
        videoQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        videoQuad.name = "RobotVisionQuad";
        videoQuad.transform.SetParent(mainCamera.transform, false);

        // 设置位置：在摄像机正前方
        videoQuad.transform.localPosition = new Vector3(0, 0, distanceFromEyes);
        videoQuad.transform.localRotation = Quaternion.identity;
        videoQuad.transform.localScale = new Vector3(screenWidth, screenHeight, 1);

        // 移除碰撞体
        Destroy(videoQuad.GetComponent<Collider>());

        Debug.Log($"✓ 视频屏幕已创建：距离 {distanceFromEyes}m，尺寸 {screenWidth}x{screenHeight}m");
    }

    void CreateStereoMaterial()
    {
        // 创建立体视频 Shader 材质
        Shader stereoShader = Shader.Find("Custom/StereoVideoShader");

        if (stereoShader == null)
        {
            // 如果找不到，创建一个简单的材质
            Debug.LogWarning("未找到 StereoVideoShader，使用标准材质");
            stereoMaterial = new Material(Shader.Find("Unlit/Texture"));
        }
        else
        {
            stereoMaterial = new Material(stereoShader);
        }

        videoQuad.GetComponent<Renderer>().material = stereoMaterial;
    }

    public void StartViewing()
    {
        if (isActive)
        {
            Debug.LogWarning("视觉系统已在运行");
            return;
        }

        Debug.Log("=== 启动机器人视觉传输 ===");
        Debug.Log($"左眼: {leftEyeUrl}");
        Debug.Log($"右眼: {rightEyeUrl}");

        // 创建纹理
        leftEyeTexture = new Texture2D(2, 2);
        rightEyeTexture = new Texture2D(2, 2);

        // 设置到材质
        if (stereoMaterial.HasProperty("_LeftEyeTex"))
        {
            stereoMaterial.SetTexture("_LeftEyeTex", leftEyeTexture);
            stereoMaterial.SetTexture("_RightEyeTex", rightEyeTexture);
        }
        else
        {
            // 使用标准纹理（非立体）
            stereoMaterial.mainTexture = leftEyeTexture;
        }

        // 启动解码器
        leftDecoder = new MjpegDecoder();
        rightDecoder = new MjpegDecoder();

        leftDecoder.Connect(leftEyeUrl);
        rightDecoder.Connect(rightEyeUrl);

        StartCoroutine(UpdateLeftEye());
        StartCoroutine(UpdateRightEye());

        videoQuad.SetActive(true);
        isActive = true;

        Debug.Log("✓ 机器人视觉系统已启动");
    }

    public void StopViewing()
    {
        if (!isActive) return;

        Debug.Log("停止机器人视觉传输");

        isActive = false;
        StopAllCoroutines();

        leftDecoder?.Disconnect();
        rightDecoder?.Disconnect();
        leftDecoder = null;
        rightDecoder = null;

        if (leftEyeTexture != null) Destroy(leftEyeTexture);
        if (rightEyeTexture != null) Destroy(rightEyeTexture);

        videoQuad.SetActive(false);
    }

    IEnumerator UpdateLeftEye()
    {
        yield return new WaitForSeconds(0.5f);

        int frameCount = 0;
        float lastLogTime = Time.time;

        while (isActive && leftDecoder != null && leftDecoder.IsRunning)
        {
            byte[] jpg = leftDecoder.GetNextFrame();
            if (jpg != null && jpg.Length > 0)
            {
                try
                {
                    leftEyeTexture.LoadImage(jpg);
                    frameCount++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"左眼纹理加载失败: {e.Message}");
                }
            }

            if (Time.time - lastLogTime >= 5f)
            {
                Debug.Log($"机器人左眼 FPS: {frameCount / 5f:F1}");
                frameCount = 0;
                lastLogTime = Time.time;
            }

            yield return null;
        }
    }

    IEnumerator UpdateRightEye()
    {
        yield return new WaitForSeconds(0.5f);

        int frameCount = 0;
        float lastLogTime = Time.time;

        while (isActive && rightDecoder != null && rightDecoder.IsRunning)
        {
            byte[] jpg = rightDecoder.GetNextFrame();
            if (jpg != null && jpg.Length > 0)
            {
                try
                {
                    rightEyeTexture.LoadImage(jpg);
                    frameCount++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"右眼纹理加载失败: {e.Message}");
                }
            }

            if (Time.time - lastLogTime >= 5f)
            {
                Debug.Log($"机器人右眼 FPS: {frameCount / 5f:F1}");
                frameCount = 0;
                lastLogTime = Time.time;
            }

            yield return null;
        }
    }

    void Update()
    {
        // 空格键切换
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isActive) StopViewing();
            else StartViewing();
        }
    }

    void OnDestroy()
    {
        StopViewing();
        if (stereoMaterial != null) Destroy(stereoMaterial);
    }
}
