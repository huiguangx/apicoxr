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
        
        [Tooltip("是否限制帧率以减少CPU负载")]
        public bool limitFrameRate = true;
        
        [Tooltip("最大帧间隔（毫秒），超过此时间才会更新纹理")]
        public int maxFrameInterval = 33; // 约30fps
        
        [Tooltip("纹理更新频率（每N帧更新一次）")]
        public int textureUpdateFrequency = 2;
        
        [Tooltip("是否在后台线程解码JPEG图像以减轻主线程负担")]
        public bool useBackgroundThreadForDecoding = false;

        [Header("网络容错")]
        [Tooltip("连接超时时间（秒）")]
        public float connectionTimeout = 30f;

        [Tooltip("最大重试次数")]
        public int maxRetryCount = 5;

        [Tooltip("重试延迟（秒）")]
        public float retryDelay = 3f;

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
        private bool leftStreamConnected = false;  // 左眼流连接状态
        private bool rightStreamConnected = false; // 右眼流连接状态
        
        // 后台线程解码相关
        private System.Threading.Thread decodingThread;
        private readonly System.Collections.Concurrent.ConcurrentQueue<FrameData> frameQueue = 
            new System.Collections.Concurrent.ConcurrentQueue<FrameData>();
        private volatile bool decodingThreadRunning = false;
        private readonly object decodingLock = new object();
        
        // 后台解码帧数据结构
        private class FrameData
        {
            public byte[] Data;
            public bool IsLeftEye;
            
            public FrameData(byte[] data, bool isLeftEye)
            {
                Data = data;
                IsLeftEye = isLeftEye;
            }
        }
        
        // 帧缓存（双缓冲）
        private byte[] leftFrameBuffer;
        private byte[] rightFrameBuffer;
        private bool leftFrameReady = false;
        private bool rightFrameReady = false;
        private readonly object frameLock = new object();
        
        // 帧率控制
        private float lastFrameTime = 0f;

        // URL
        private string currentLeftUrl;
        private string currentRightUrl;

        // 统计信息
        private int leftFrameCount = 0;
        private int rightFrameCount = 0;
        private float statsTimer = 0f;
        private float currentFPS = 0f;
        
        // 上次处理的帧时间戳，用于更准确的FPS计算
        private float lastLeftFrameTime = 0f;
        private float lastRightFrameTime = 0f;
        private const float MIN_FRAME_INTERVAL = 0.01f; // 最小帧间隔10ms，避免重复帧计入

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
            // 控制纹理更新频率，减轻主线程负担
            if (textureUpdateFrequency <= 1 || Time.frameCount % textureUpdateFrequency == 0)
            {
                UpdateTextures();
            }

            // 控制统计信息更新频率（每秒约10次）
            if (Time.frameCount % Mathf.Max(1, Application.targetFrameRate / 10) == 0)
            {
                UpdateStatistics();
            }

            // 控制视频属性更新频率（每秒约20次）
            if (Time.frameCount % Mathf.Max(1, Application.targetFrameRate / 20) == 0)
            {
                UpdateVideoAlpha();
                UpdateVideoVisibility();
            }
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

            // 重置连接状态
            leftStreamConnected = false;
            rightStreamConnected = false;

            // 创建沉浸式显示
            CreateImmersiveDisplay();

            // 重置统计信息
            leftFrameCount = 0;
            rightFrameCount = 0;
            statsTimer = 0f;
            currentFPS = 0f;

            // 启动双目流
            isStreaming = true;
            
            // 如果启用了后台线程解码，则启动解码线程
            if (useBackgroundThreadForDecoding)
            {
                StartDecodingThread();
            }
            
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
            
            // 停止后台解码线程
            StopDecodingThread();

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
                // 对于MJPEG流，禁用超时设置，因为它是持续性连接
                request.timeout = 0; // 禁用超时
                
                MjpegStreamHandler handler = new MjpegStreamHandler(enableDebugLog, targetFrameRate);

                // 注册帧接收事件
                handler.OnFrameReceived += (frameData) =>
                {
                    // 标记流已连接并接收数据
                    if (isLeftEye)
                        leftStreamConnected = true;
                    else
                        rightStreamConnected = true;
                        
                    OnFrameReceived(frameData, isLeftEye);
                };

                // 注册错误事件
                handler.OnError += (error) =>
                {
                    Debug.LogError($"[StereoVideoStreamManager] {eyeName}流处理错误: {error}");
                };

                request.downloadHandler = handler;
                // 注释掉超时设置，保持第420行的timeout=0（MJPEG是持续流，不应超时）
                // request.timeout = (int)connectionTimeout;

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
                    string errorMessage = request.error;
                    string detailedError = GetDetailedErrorMessage(request);
                    
                    // 对于MJPEG流，timeout并不总是错误，如果已经接收数据则忽略超时
                    bool isRealError = true;
                    if (request.result == UnityWebRequest.Result.ConnectionError && 
                        errorMessage.Contains("Request timeout"))
                    {
                        // 如果是因为超时，但流已经连接并接收了数据，则认为连接是稳定的
                        if ((isLeftEye && leftStreamConnected) || (!isLeftEye && rightStreamConnected))
                        {
                            Debug.Log($"[StereoVideoStreamManager] {eyeName}流连接稳定，忽略超时错误");
                            isRealError = false;
                        }
                    }
                    
                    if (isRealError)
                    {
                        Debug.LogWarning($"[StereoVideoStreamManager] {eyeName}流连接失败: {errorMessage}\n详细信息: {detailedError}");
                        retryCount++;

                        if (retryCount < maxRetryCount)
                        {
                            Debug.Log($"[StereoVideoStreamManager] 将在 {retryDelay} 秒后重试 ({retryCount}/{maxRetryCount})");
                            yield return new WaitForSeconds(retryDelay);
                        }
                        else
                        {
                            Debug.LogError($"[StereoVideoStreamManager] {eyeName}流达到最大重试次数，放弃连接\nURL: {url}\n最后一次错误: {errorMessage}\n详细信息: {detailedError}");
                        }
                    }
                    else
                    {
                        // 重置重试计数，因为我们实际上连接是成功的
                        retryCount = 0;
                        yield return new WaitForSeconds(1f); // 等待一秒后继续
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
            float currentTime = Time.time;
            
            lock (frameLock)
            {
                if (isLeftEye)
                {
                    // 检查是否为新帧（避免重复计算）
                    if (currentTime - lastLeftFrameTime >= MIN_FRAME_INTERVAL)
                    {
                        leftFrameBuffer = frameData;
                        leftFrameReady = true;
                        leftFrameCount++;
                        lastLeftFrameTime = currentTime;
                    }
                }
                else
                {
                    // 检查是否为新帧（避免重复计算）
                    if (currentTime - lastRightFrameTime >= MIN_FRAME_INTERVAL)
                    {
                        rightFrameBuffer = frameData;
                        rightFrameReady = true;
                        rightFrameCount++;
                        lastRightFrameTime = currentTime;
                    }
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
            // 帧率控制 - 限制纹理更新频率避免卡顿
            float currentTime = Time.time;
            if (limitFrameRate && (currentTime - lastFrameTime) < (maxFrameInterval / 1000f))
            {
                return; // 跳过此次更新
            }

            lock (frameLock)
            {
                bool updated = false;
                
                // 更新左眼纹理
                if (leftFrameReady && leftFrameBuffer != null)
                {
                    try
                    {
                        leftEyeTexture.LoadImage(leftFrameBuffer);
                        leftFrameReady = false;
                        if (!useBackgroundThreadForDecoding)
                        {
                            leftFrameCount++; // 只在非后台解码模式下增加计数
                        }
                        updated = true;
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
                        if (!useBackgroundThreadForDecoding)
                        {
                            rightFrameCount++; // 只在非后台解码模式下增加计数
                        }
                        updated = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[StereoVideoStreamManager] 右眼纹理更新失败: {ex.Message}");
                    }
                }
                
                // 如果有更新，记录更新时间
                if (updated)
                {
                    lastFrameTime = currentTime;
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
                    Debug.Log($"[StereoVideoStreamManager] 实际FPS: {currentFPS:F1} (左:{leftFrameCount} 右:{rightFrameCount})");
                }

                // 重置计数器
                leftFrameCount = 0;
                rightFrameCount = 0;
                statsTimer = 0f;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取详细的错误信息
        /// </summary>
        /// <param name="request">UnityWebRequest对象</param>
        /// <returns>详细的错误描述</returns>
        private string GetDetailedErrorMessage(UnityWebRequest request)
        {
            string detailedError = "";

            switch (request.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    detailedError = "连接错误 - 请检查网络连接和服务器地址是否正确";
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    detailedError = $"协议错误 - HTTP状态码: {request.responseCode}";
                    break;
                case UnityWebRequest.Result.DataProcessingError:
                    detailedError = "数据处理错误 - 返回的数据格式可能不正确";
                    break;
                case UnityWebRequest.Result.InProgress:
                    detailedError = "请求仍在进行中";
                    break;
                default:
                    detailedError = "未知错误";
                    break;
            }

            // 添加响应头信息（如果有的话）
            if (request.responseCode > 0)
            {
                detailedError += $"\n响应码: {request.responseCode}";
                
                // 尝试获取响应头
                try
                {
                    var headers = request.GetResponseHeaders();
                    if (headers != null)
                    {
                        detailedError += "\n响应头:";
                        foreach (var header in headers)
                        {
                            detailedError += $"\n  {header.Key}: {header.Value}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    detailedError += $"\n无法获取响应头: {ex.Message}";
                }
            }

            return detailedError;
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

        #region 后台解码线程

        /// <summary>
        /// 启动后台解码线程
        /// </summary>
        private void StartDecodingThread()
        {
            if (decodingThreadRunning)
                return;

            decodingThreadRunning = true;
            decodingThread = new System.Threading.Thread(DecodingThreadFunction)
            {
                Name = "MJPEG Decoding Thread",
                IsBackground = true
            };
            decodingThread.Start();
            
            Debug.Log("[StereoVideoStreamManager] 后台解码线程已启动");
        }

        /// <summary>
        /// 后台解码线程函数
        /// </summary>
        private void DecodingThreadFunction()
        {
            Debug.Log("[StereoVideoStreamManager] 后台解码线程开始运行");
            
            while (decodingThreadRunning)
            {
                // 处理队列中的帧数据
                if (frameQueue.TryDequeue(out FrameData frameData))
                {
                    try
                    {
                        // 在后台线程解码JPEG数据
                        if (frameData.IsLeftEye)
                        {
                            lock (frameLock)
                            {
                                leftFrameBuffer = frameData.Data;
                                leftFrameReady = true;
                            }
                        }
                        else
                        {
                            lock (frameLock)
                            {
                                rightFrameBuffer = frameData.Data;
                                rightFrameReady = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[StereoVideoStreamManager] 后台解码错误: {ex.Message}");
                    }
                }
                else
                {
                    // 没有待处理的帧，短暂休眠以避免占用过多CPU
                    System.Threading.Thread.Sleep(1);
                }
            }
            
            Debug.Log("[StereoVideoStreamManager] 后台解码线程已结束");
        }

        /// <summary>
        /// 停止后台解码线程
        /// </summary>
        private void StopDecodingThread()
        {
            if (!decodingThreadRunning)
                return;

            decodingThreadRunning = false;
            
            // 等待线程结束（最多等待1秒）
            if (decodingThread != null && decodingThread.IsAlive)
            {
                decodingThread.Join(1000);
            }
            
            // 清空队列
            while (frameQueue.TryDequeue(out _)) { }
            
            Debug.Log("[StereoVideoStreamManager] 后台解码线程已停止");
        }

        #endregion
    }
}