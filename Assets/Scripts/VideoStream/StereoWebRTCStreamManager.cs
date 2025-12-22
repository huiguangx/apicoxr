using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VideoStream
{
    /// <summary>
    /// 双目WebRTC视频流管理器
    /// 管理左右眼的WebRTC视频流，并在VR空间中渲染
    /// </summary>
    public class StereoWebRTCStreamManager : MonoBehaviour
    {
        #region 配置参数

        [Header("服务器配置")]
        [Tooltip("WebRTC服务器URL（如 https://localhost:5000）")]
        public string serverUrl = "http://localhost:5000";

        [Tooltip("视频源类型")]
        public VideoSourceType sourceType = VideoSourceType.SHARE_MEMORY_STEREO;

        [Tooltip("共享内存名称")]
        public string sharedMemoryName = "stereo_color_image_shm";

        [Header("显示配置")]
        [Tooltip("显示距离（从相机到显示平面）")]
        public float displayDistance = 2.0f;

        [Tooltip("显示宽度")]
        public float displayWidth = 3.2f;

        [Tooltip("显示高度")]
        public float displayHeight = 1.8f;

        [Tooltip("透明度（0-1）")]
        [Range(0f, 1f)]
        public float alpha = 1.0f;

        [Header("视频配置")]
        [Tooltip("视频宽度")]
        public int videoWidth = 1280;

        [Tooltip("视频高度")]
        public int videoHeight = 720;

        [Header("调试选项")]
        [Tooltip("启用调试日志")]
        public bool enableDebugLog = false;

        #endregion

        #region 私有变量

        private WebRTCStreamClient leftClient;
        private WebRTCStreamClient rightClient;

        private GameObject displayQuad;
        private Material displayMaterial;
        private Camera mainCamera;

        private bool isStreaming = false;
        private Texture leftTexture;
        private Texture rightTexture;

        #endregion

        #region 公共属性

        /// <summary>
        /// 是否正在流式传输
        /// </summary>
        public bool IsStreaming => isStreaming;

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            mainCamera = Camera.main;

            // 创建WebRTC客户端组件
            CreateWebRTCClients();

            // 创建显示Quad
            CreateDisplayQuad();
        }

        private void Update()
        {
            if (isStreaming)
            {
                // 更新显示Quad位置（跟随相机）
                UpdateDisplayPosition();

                // 更新透明度
                UpdateAlpha();
            }
        }

        private void OnDestroy()
        {
            StopStreaming();

            if (displayQuad != null)
            {
                Destroy(displayQuad);
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 开始WebRTC视频流
        /// </summary>
        public void StartStreaming()
        {
            if (isStreaming)
            {
                LogWarning("已经在流式传输中");
                return;
            }

            LogInfo("开始WebRTC视频流");

            // 显示显示平面
            if (displayQuad != null)
            {
                displayQuad.SetActive(true);
            }

            // 连接左右眼客户端
            leftClient.Connect();
            rightClient.Connect();

            isStreaming = true;
        }

        /// <summary>
        /// 停止WebRTC视频流
        /// </summary>
        public void StopStreaming()
        {
            if (!isStreaming)
            {
                return;
            }

            LogInfo("停止WebRTC视频流");

            // 断开左右眼客户端
            leftClient.Disconnect();
            rightClient.Disconnect();

            // 隐藏显示平面
            if (displayQuad != null)
            {
                displayQuad.SetActive(false);
            }

            isStreaming = false;
            leftTexture = null;
            rightTexture = null;
        }

        /// <summary>
        /// 设置透明度
        /// </summary>
        public void SetAlpha(float newAlpha)
        {
            alpha = Mathf.Clamp01(newAlpha);
            UpdateAlpha();
        }

        /// <summary>
        /// 调整透明度
        /// </summary>
        public void AdjustAlpha(float delta)
        {
            SetAlpha(alpha + delta);
        }

        #endregion

        #region 初始化

        private void CreateWebRTCClients()
        {
            // 创建左眼客户端
            GameObject leftClientObj = new GameObject("WebRTC_LeftEye");
            leftClientObj.transform.SetParent(transform);
            leftClient = leftClientObj.AddComponent<WebRTCStreamClient>();
            leftClient.serverUrl = serverUrl;
            leftClient.sourceType = sourceType;
            leftClient.isLeftEye = true;
            leftClient.sharedMemoryName = sharedMemoryName;
            leftClient.videoWidth = videoWidth;
            leftClient.videoHeight = videoHeight;
            leftClient.enableDebugLog = enableDebugLog;

            // 左眼事件
            leftClient.OnVideoTextureReady += OnLeftTextureReady;
            leftClient.OnConnected += () => LogInfo("左眼WebRTC已连接");
            leftClient.OnDisconnected += () => LogInfo("左眼WebRTC已断开");
            leftClient.OnConnectionError += error => LogError($"左眼连接错误: {error}");

            // 创建右眼客户端
            GameObject rightClientObj = new GameObject("WebRTC_RightEye");
            rightClientObj.transform.SetParent(transform);
            rightClient = rightClientObj.AddComponent<WebRTCStreamClient>();
            rightClient.serverUrl = serverUrl;
            rightClient.sourceType = sourceType;
            rightClient.isLeftEye = false;
            rightClient.sharedMemoryName = sharedMemoryName;
            rightClient.videoWidth = videoWidth;
            rightClient.videoHeight = videoHeight;
            rightClient.enableDebugLog = enableDebugLog;

            // 右眼事件
            rightClient.OnVideoTextureReady += OnRightTextureReady;
            rightClient.OnConnected += () => LogInfo("右眼WebRTC已连接");
            rightClient.OnDisconnected += () => LogInfo("右眼WebRTC已断开");
            rightClient.OnConnectionError += error => LogError($"右眼连接错误: {error}");
        }

        private void CreateDisplayQuad()
        {
            // 创建显示平面
            displayQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            displayQuad.name = "WebRTC_DisplayQuad";
            displayQuad.transform.SetParent(transform);
            displayQuad.SetActive(false);

            // 移除碰撞体
            Destroy(displayQuad.GetComponent<Collider>());

            // 创建材质（使用标准着色器，支持透明）
            displayMaterial = new Material(Shader.Find("UI/Default"));
            displayMaterial.SetFloat("_Mode", 3); // 透明模式
            displayMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            displayMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            displayMaterial.SetInt("_ZWrite", 0);
            displayMaterial.DisableKeyword("_ALPHATEST_ON");
            displayMaterial.EnableKeyword("_ALPHABLEND_ON");
            displayMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            displayMaterial.renderQueue = 3000;

            displayQuad.GetComponent<Renderer>().material = displayMaterial;

            // 设置初始位置和大小
            UpdateDisplayPosition();
        }

        #endregion

        #region 纹理更新

        private void OnLeftTextureReady(Texture texture)
        {
            leftTexture = texture;
            UpdateDisplayTexture();
            LogInfo($"左眼纹理已准备: {texture.width}x{texture.height}");
        }

        private void OnRightTextureReady(Texture texture)
        {
            rightTexture = texture;
            UpdateDisplayTexture();
            LogInfo($"右眼纹理已准备: {texture.width}x{texture.height}");
        }

        private void UpdateDisplayTexture()
        {
            // 注意：这里简化处理，使用左眼纹理
            // 如果需要真正的立体渲染，需要使用自定义shader或渲染到左右眼
            if (leftTexture != null && displayMaterial != null)
            {
                displayMaterial.mainTexture = leftTexture;
            }
        }

        #endregion

        #region 显示更新

        private void UpdateDisplayPosition()
        {
            if (displayQuad == null || mainCamera == null)
            {
                return;
            }

            // 将Quad放置在相机前方
            displayQuad.transform.position = mainCamera.transform.position + mainCamera.transform.forward * displayDistance;
            displayQuad.transform.rotation = mainCamera.transform.rotation;

            // 设置大小
            displayQuad.transform.localScale = new Vector3(displayWidth, displayHeight, 1f);
        }

        private void UpdateAlpha()
        {
            if (displayMaterial != null)
            {
                Color color = displayMaterial.color;
                color.a = alpha;
                displayMaterial.color = color;
            }
        }

        #endregion

        #region 日志辅助

        private void LogInfo(string message)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[StereoWebRTCStreamManager] {message}");
            }
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"[StereoWebRTCStreamManager] {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[StereoWebRTCStreamManager] {message}");
        }

        #endregion
    }
}
