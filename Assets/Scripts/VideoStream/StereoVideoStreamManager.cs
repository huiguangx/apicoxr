using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace VideoStream
{
    /// <summary>
    /// 立体视频流管理器
    /// 管理双目MJPEG视频流的下载、解码和显示
    /// 支持VR第一人称沉浸式显示
    /// </summary>
    public class StereoVideoStreamManager : MonoBehaviour
    {
        #region 配置参数

        [Header("性能设置")]
        [Tooltip("目标帧率")]
        [Range(15, 60)]
        public int targetFrameRate = 30;

        [Tooltip("最大纹理尺寸")]
        public int maxTextureSize = 1920;

        [Tooltip("纹理格式")]
        public TextureFormat textureFormat = TextureFormat.RGB24;

        [Tooltip("纹理过滤模式 - Point速度快但有锯齿，Bilinear平滑但稍慢")]
        public FilterMode textureFilterMode = FilterMode.Bilinear;

        [Header("网络容错")]
        [Tooltip("连接超时时间（秒）")]
        public float connectionTimeout = 10f;

        [Tooltip("最大重试次数")]
        public int maxRetryCount = 3;

        [Tooltip("重试延迟（秒）")]
        public float retryDelay = 2f;

        [Header("显示设置")]
        [Tooltip("视频显示距离（米）- 建议1.0-3.0米")]
        [Range(0.2f, 5.0f)]
        public float displayDistance = 1.5f;

        [Tooltip("视频显示宽度（米）- 建议1.0-2.0米")]
        [Range(0.5f, 5.0f)]
        public float displayWidth = 1.6f;

        [Tooltip("视频显示高度（米）- 建议0.5-1.5米")]
        [Range(0.3f, 3.0f)]
        public float displayHeight = 0.9f;

        [Tooltip("视频透明度（1=完全不透明，0=完全透明）")]
        [Range(0f, 1f)]
        public float videoAlpha = 1.0f;

        [Tooltip("是否显示视频Quad")]
        public bool showVideo = true;

        [Header("调试选项")]
        [Tooltip("启用调试日志")]
        public bool enableDebugLog = false;

        #endregion

        #region 私有字段

        // 显示相关
        private GameObject videoQuad;
        private Material stereoMaterial;
        private Camera mainCamera;

        // 纹理
        private Texture2D leftEyeTexture;
        private Texture2D rightEyeTexture;

        // 流管理
        private Coroutine leftStreamCoroutine;
        private Coroutine rightStreamCoroutine;
        private UnityWebRequest leftRequest;
        private UnityWebRequest rightRequest;
        private bool isStreaming = false;

        // 帧缓存（双缓冲）
        private byte[] leftFrameBuffer;
        private byte[] rightFrameBuffer;
        private bool leftFrameReady = false;
        private bool rightFrameReady = false;
        private readonly object frameLock = new object();

        // URL
        private string currentLeftUrl;
        private string currentRightUrl;

        // 统计信息
        private int leftFrameCount = 0;
        private int rightFrameCount = 0;
        private float statsTimer = 0f;
        private float currentFPS = 0f;

        #endregion

        #region 公共属性

        /// <summary>
        /// 是否正在播放视频流
        /// </summary>
        public bool IsStreaming => isStreaming;

        /// <summary>
        /// 当前帧率
        /// </summary>
        public float CurrentFPS => currentFPS;

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            // 查找主相机
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[StereoVideoStreamManager] 找不到主相机！请确保场景中有标记为MainCamera的相机。");
            }
        }

        private void Start()
        {
            // 初始化纹理
            InitializeTextures();

            // 初始化材质
            InitializeMaterial();
        }

        private void Update()
        {
            // 更新纹理（主线程安全）
            UpdateTextures();

            // 更新统计信息
            UpdateStatistics();

            // 更新视频透明度
            UpdateVideoAlpha();

            // 更新视频可见性
            UpdateVideoVisibility();
        }

        private void OnDestroy()
        {
            // 停止视频流
            StopStreaming();

            // 清理资源
            CleanupResources();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 开始播放视频流
        /// </summary>
        /// <param name="leftUrl">左眼视频流URL</param>
        /// <param name="rightUrl">右眼视频流URL</param>
        public void StartStreaming(string leftUrl, string rightUrl)
        {
            if (isStreaming)
            {
                Debug.LogWarning("[StereoVideoStreamManager] 视频流已在运行中，先停止当前流");
                StopStreaming();
            }

            if (string.IsNullOrEmpty(leftUrl) || string.IsNullOrEmpty(rightUrl))
            {
                Debug.LogError("[StereoVideoStreamManager] URL不能为空");
                return;
            }

            if (mainCamera == null)
            {
                Debug.LogError("[StereoVideoStreamManager] 主相机未找到，无法启动视频流");
                return;
            }

            currentLeftUrl = leftUrl;
            currentRightUrl = rightUrl;

            Debug.Log($"[StereoVideoStreamManager] 启动视频流\nLeft: {leftUrl}\nRight: {rightUrl}");

            // 创建沉浸式显示
            CreateImmersiveDisplay();

            // 重置统计信息
            leftFrameCount = 0;
            rightFrameCount = 0;
            statsTimer = 0f;
            currentFPS = 0f;

            // 启动双目流
            isStreaming = true;
            leftStreamCoroutine = StartCoroutine(StreamEye(leftUrl, true));
            rightStreamCoroutine = StartCoroutine(StreamEye(rightUrl, false));
        }

        /// <summary>
        /// 停止播放视频流
        /// </summary>
        public void StopStreaming()
        {
            if (!isStreaming)
            {
                return;
            }

            Debug.Log("[StereoVideoStreamManager] 停止视频流");

            isStreaming = false;

            // 停止协程
            if (leftStreamCoroutine != null)
            {
                StopCoroutine(leftStreamCoroutine);
                leftStreamCoroutine = null;
            }

            if (rightStreamCoroutine != null)
            {
                StopCoroutine(rightStreamCoroutine);
                rightStreamCoroutine = null;
            }

            // 中断网络请求
            if (leftRequest != null)
            {
                leftRequest.Abort();
                leftRequest.Dispose();
                leftRequest = null;
            }

            if (rightRequest != null)
            {
                rightRequest.Abort();
                rightRequest.Dispose();
                rightRequest = null;
            }

            // 销毁显示对象
            if (videoQuad != null)
            {
                Destroy(videoQuad);
                videoQuad = null;
            }

            // 清空帧缓存
            leftFrameBuffer = null;
            rightFrameBuffer = null;
            leftFrameReady = false;
            rightFrameReady = false;
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化纹理
        /// </summary>
        private void InitializeTextures()
        {
            // 创建左眼纹理
            leftEyeTexture = new Texture2D(2, 2, textureFormat, false);
            leftEyeTexture.name = "LeftEyeTexture";
            leftEyeTexture.filterMode = textureFilterMode;
            leftEyeTexture.wrapMode = TextureWrapMode.Clamp;

            // 创建右眼纹理
            rightEyeTexture = new Texture2D(2, 2, textureFormat, false);
            rightEyeTexture.name = "RightEyeTexture";
            rightEyeTexture.filterMode = textureFilterMode;
            rightEyeTexture.wrapMode = TextureWrapMode.Clamp;

            if (enableDebugLog)
            {
                Debug.Log($"[StereoVideoStreamManager] 纹理初始化完成\n" +
                          $"  格式: {textureFormat}\n" +
                          $"  过滤模式: {textureFilterMode}");
            }
        }

        /// <summary>
        /// 初始化材质
        /// </summary>
        private void InitializeMaterial()
        {
            // 查找Shader
            Shader stereoShader = Shader.Find("Custom/StereoVideoShader");

            if (stereoShader == null)
            {
                Debug.LogError("[StereoVideoStreamManager] 找不到 Custom/StereoVideoShader！");
                Debug.LogError("请检查：1. Shader文件是否存在  2. 是否在 Project Settings > Graphics > Always Included Shaders 中添加");

                // 使用降级Shader
                stereoShader = Shader.Find("Unlit/Texture");
                if (stereoShader == null)
                {
                    Debug.LogError("[StereoVideoStreamManager] 连降级Shader都找不到，无法继续");
                    return;
                }
            }

            // 创建材质
            stereoMaterial = new Material(stereoShader);
            stereoMaterial.name = "StereoVideoMaterial";

            // 设置纹理
            stereoMaterial.SetTexture("_LeftEyeTex", leftEyeTexture);
            stereoMaterial.SetTexture("_RightEyeTex", rightEyeTexture);

            if (enableDebugLog)
            {
                Debug.Log($"[StereoVideoStreamManager] 材质初始化完成，Shader: {stereoShader.name}");
            }
        }

        #endregion

        #region 视频流下载

        /// <summary>
        /// 流式下载单眼视频
        /// </summary>
        /// <param name="url">视频流URL</param>
        /// <param name="isLeftEye">是否为左眼</param>
        private IEnumerator StreamEye(string url, bool isLeftEye)
        {
            string eyeName = isLeftEye ? "左眼" : "右眼";
            int retryCount = 0;

            while (isStreaming && retryCount < maxRetryCount)
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[StereoVideoStreamManager] 连接{eyeName}视频流: {url}");
                }

                // 创建请求
                UnityWebRequest request = new UnityWebRequest(url);
                MjpegStreamHandler handler = new MjpegStreamHandler(enableDebugLog);

                // 注册帧接收事件
                handler.OnFrameReceived += (frameData) =>
                {
                    OnFrameReceived(frameData, isLeftEye);
                };

                // 注册错误事件
                handler.OnError += (error) =>
                {
                    Debug.LogError($"[StereoVideoStreamManager] {eyeName}流处理错误: {error}");
                };

                request.downloadHandler = handler;
                request.timeout = (int)connectionTimeout;

                // 保存请求引用（用于中断）
                if (isLeftEye)
                    leftRequest = request;
                else
                    rightRequest = request;

                // 发送请求
                yield return request.SendWebRequest();

                // 检查结果
                if (request.result == UnityWebRequest.Result.Success)
                {
                    // 正常结束（通常不会到这里，因为MJPEG流是持续的）
                    Debug.Log($"[StereoVideoStreamManager] {eyeName}流正常结束");
                    retryCount = 0; // 重置重试计数
                }
                else
                {
                    // 发生错误
                    Debug.LogWarning($"[StereoVideoStreamManager] {eyeName}流连接失败: {request.error}");
                    retryCount++;

                    if (retryCount < maxRetryCount)
                    {
                        Debug.Log($"[StereoVideoStreamManager] 将在 {retryDelay} 秒后重试 ({retryCount}/{maxRetryCount})");
                        yield return new WaitForSeconds(retryDelay);
                    }
                    else
                    {
                        Debug.LogError($"[StereoVideoStreamManager] {eyeName}流达到最大重试次数，放弃连接");
                    }
                }

                // 清理请求
                request.Dispose();
                if (isLeftEye)
                    leftRequest = null;
                else
                    rightRequest = null;

                // 如果用户已停止流，退出循环
                if (!isStreaming)
                {
                    break;
                }
            }

            Debug.Log($"[StereoVideoStreamManager] {eyeName}流协程结束");
        }

        /// <summary>
        /// 当接收到新帧时的回调
        /// </summary>
        /// <param name="frameData">帧数据</param>
        /// <param name="isLeftEye">是否为左眼</param>
        private void OnFrameReceived(byte[] frameData, bool isLeftEye)
        {
            lock (frameLock)
            {
                if (isLeftEye)
                {
                    leftFrameBuffer = frameData;
                    leftFrameReady = true;
                    leftFrameCount++;
                }
                else
                {
                    rightFrameBuffer = frameData;
                    rightFrameReady = true;
                    rightFrameCount++;
                }
            }
        }

        #endregion

        #region 纹理更新

        /// <summary>
        /// 更新纹理（必须在主线程调用）
        /// </summary>
        private void UpdateTextures()
        {
            lock (frameLock)
            {
                // 更新左眼纹理
                if (leftFrameReady && leftFrameBuffer != null)
                {
                    try
                    {
                        leftEyeTexture.LoadImage(leftFrameBuffer);
                        leftFrameReady = false;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[StereoVideoStreamManager] 左眼纹理更新失败: {ex.Message}");
                    }
                }

                // 更新右眼纹理
                if (rightFrameReady && rightFrameBuffer != null)
                {
                    try
                    {
                        rightEyeTexture.LoadImage(rightFrameBuffer);
                        rightFrameReady = false;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[StereoVideoStreamManager] 右眼纹理更新失败: {ex.Message}");
                    }
                }
            }
        }

        #endregion

        #region 沉浸式显示

        /// <summary>
        /// 创建第一人称沉浸式显示
        /// </summary>
        private void CreateImmersiveDisplay()
        {
            if (videoQuad != null)
            {
                Destroy(videoQuad);
            }

            if (mainCamera == null)
            {
                Debug.LogError("[StereoVideoStreamManager] 主相机未找到，无法创建显示");
                return;
            }

            // 创建Quad
            videoQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            videoQuad.name = "ImmersiveVideoDisplay";

            // 关键：作为相机子对象，自动跟随头显
            videoQuad.transform.SetParent(mainCamera.transform, false);

            // 位置：使用可配置的距离
            videoQuad.transform.localPosition = new Vector3(0, 0, displayDistance);
            videoQuad.transform.localRotation = Quaternion.identity;

            // 尺寸：使用可配置的宽度和高度
            videoQuad.transform.localScale = new Vector3(displayWidth, displayHeight, 1f);

            // 移除碰撞体（不需要物理交互）
            Collider collider = videoQuad.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            // 应用材质
            MeshRenderer renderer = videoQuad.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = stereoMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                // 设置渲染队列，让视频在UI之前渲染（UI会显示在视频上面）
                stereoMaterial.renderQueue = 2000; // Geometry队列，UI是3000

                if (enableDebugLog)
                {
                    Debug.Log($"[StereoVideoStreamManager] 材质已应用到MeshRenderer，RenderQueue: {stereoMaterial.renderQueue}");
                }
            }
            else
            {
                Debug.LogError("[StereoVideoStreamManager] ❌ CreatePrimitive创建的Quad没有MeshRenderer！尝试手动添加...");

                // 手动添加MeshRenderer和MeshFilter
                MeshFilter meshFilter = videoQuad.GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    meshFilter = videoQuad.AddComponent<MeshFilter>();

                    // 创建Quad网格
                    Mesh mesh = new Mesh();
                    mesh.vertices = new Vector3[]
                    {
                        new Vector3(-0.5f, -0.5f, 0),
                        new Vector3(0.5f, -0.5f, 0),
                        new Vector3(-0.5f, 0.5f, 0),
                        new Vector3(0.5f, 0.5f, 0)
                    };
                    mesh.uv = new Vector2[]
                    {
                        new Vector2(0, 0),
                        new Vector2(1, 0),
                        new Vector2(0, 1),
                        new Vector2(1, 1)
                    };
                    mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
                    mesh.RecalculateNormals();

                    meshFilter.mesh = mesh;
                }

                renderer = videoQuad.AddComponent<MeshRenderer>();
                renderer.material = stereoMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                // 设置渲染队列
                stereoMaterial.renderQueue = 2000;

                Debug.Log("[StereoVideoStreamManager] ✅ 手动创建MeshRenderer成功");
            }

            if (enableDebugLog)
            {
                Debug.Log($"[StereoVideoStreamManager] 沉浸式显示创建完成\n" +
                          $"  距离: {displayDistance:F2}米\n" +
                          $"  尺寸: {displayWidth:F2}m x {displayHeight:F2}m");
            }
        }

        #endregion

        #region 可见性控制

        /// <summary>
        /// 更新视频可见性
        /// </summary>
        private void UpdateVideoVisibility()
        {
            if (videoQuad != null)
            {
                if (videoQuad.activeSelf != showVideo)
                {
                    videoQuad.SetActive(showVideo);

                    if (enableDebugLog)
                    {
                        Debug.Log($"[StereoVideoStreamManager] 视频显示: {(showVideo ? "开启" : "关闭")}");
                    }
                }
            }
        }

        #endregion

        #region 透明度控制

        /// <summary>
        /// 更新视频透明度
        /// </summary>
        private void UpdateVideoAlpha()
        {
            if (stereoMaterial != null && videoQuad != null)
            {
                Color color = stereoMaterial.color;
                if (Mathf.Abs(color.a - videoAlpha) > 0.01f)
                {
                    color.a = videoAlpha;
                    stereoMaterial.color = color;

                    // 根据透明度设置材质类型
                    if (videoAlpha < 1.0f)
                    {
                        // 半透明：需要使用Transparent渲染队列
                        stereoMaterial.renderQueue = 3000; // Transparent队列
                    }
                    else
                    {
                        // 完全不透明：使用Geometry渲染队列
                        stereoMaterial.renderQueue = 2000;
                    }
                }
            }
        }

        #endregion

        #region 统计信息

        /// <summary>
        /// 更新统计信息
        /// </summary>
        private void UpdateStatistics()
        {
            if (!isStreaming)
            {
                currentFPS = 0f;
                return;
            }

            statsTimer += Time.deltaTime;

            // 每秒更新一次FPS
            if (statsTimer >= 1f)
            {
                // 计算平均帧率（左右眼的平均值）
                currentFPS = (leftFrameCount + rightFrameCount) / 2f / statsTimer;

                if (enableDebugLog)
                {
                    Debug.Log($"[StereoVideoStreamManager] FPS: {currentFPS:F1} (左:{leftFrameCount} 右:{rightFrameCount})");
                }

                // 重置计数器
                leftFrameCount = 0;
                rightFrameCount = 0;
                statsTimer = 0f;
            }
        }

        #endregion

        #region 资源清理

        /// <summary>
        /// 清理所有资源
        /// </summary>
        private void CleanupResources()
        {
            // 清理纹理
            if (leftEyeTexture != null)
            {
                Destroy(leftEyeTexture);
                leftEyeTexture = null;
            }

            if (rightEyeTexture != null)
            {
                Destroy(rightEyeTexture);
                rightEyeTexture = null;
            }

            // 清理材质
            if (stereoMaterial != null)
            {
                Destroy(stereoMaterial);
                stereoMaterial = null;
            }

            // 清理显示对象
            if (videoQuad != null)
            {
                Destroy(videoQuad);
                videoQuad = null;
            }

            if (enableDebugLog)
            {
                Debug.Log("[StereoVideoStreamManager] 资源清理完成");
            }
        }

        #endregion
    }
}
