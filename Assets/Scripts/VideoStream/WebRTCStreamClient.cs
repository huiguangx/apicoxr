using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Unity.WebRTC;

namespace VideoStream
{
    /// <summary>
    /// WebRTC视频流客户端
    /// 负责与后端服务器建立WebRTC连接并接收H.264视频流
    /// </summary>
    public class WebRTCStreamClient : MonoBehaviour
    {
        #region 配置参数

        [Header("服务器配置")]
        [Tooltip("WebRTC服务器URL（如 https://localhost:5000）")]
        public string serverUrl = "https://localhost:5000";

        [Tooltip("视频源类型")]
        public VideoSourceType sourceType = VideoSourceType.SHARE_MEMORY_STEREO;

        [Tooltip("是否为左眼（false为右眼）")]
        public bool isLeftEye = true;

        [Tooltip("共享内存名称（用于SHARE_MEMORY类型）")]
        public string sharedMemoryName = "stereo_color_image_shm";

        [Header("视频配置")]
        [Tooltip("目标视频宽度")]
        public int videoWidth = 1280;

        [Tooltip("目标视频高度")]
        public int videoHeight = 720;

        [Header("调试选项")]
        [Tooltip("启用调试日志")]
        public bool enableDebugLog = false;

        #endregion

        #region 私有变量

        private RTCPeerConnection peerConnection;
        private Texture videoTexture;
        private bool isConnected = false;
        private bool isConnecting = false;

        // 事件回调
        public event Action<Texture> OnVideoTextureReady;
        public event Action<string> OnConnectionError;
        public event Action OnConnected;
        public event Action OnDisconnected;

        #endregion

        #region 公共属性

        /// <summary>
        /// 当前视频纹理
        /// </summary>
        public Texture VideoTexture => videoTexture;

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => isConnected;

        #endregion

        #region Unity生命周期

        private void OnDestroy()
        {
            Disconnect();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 开始连接WebRTC流
        /// </summary>
        public void Connect()
        {
            if (isConnecting || isConnected)
            {
                LogWarning("已经在连接中或已连接");
                return;
            }

            StartCoroutine(ConnectCoroutine());
        }

        /// <summary>
        /// 断开WebRTC连接
        /// </summary>
        public void Disconnect()
        {
            if (peerConnection != null)
            {
                peerConnection.Close();
                peerConnection.Dispose();
                peerConnection = null;
            }

            isConnected = false;
            isConnecting = false;
            videoTexture = null;

            LogInfo("WebRTC连接已断开");
            OnDisconnected?.Invoke();
        }

        #endregion

        #region WebRTC连接逻辑

        private IEnumerator ConnectCoroutine()
        {
            isConnecting = true;

            // 1. 创建PeerConnection
            var configuration = new RTCConfiguration
            {
                iceServers = new[] { new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } } }
            };

            peerConnection = new RTCPeerConnection(ref configuration);

            // 2. 设置事件监听
            SetupPeerConnectionCallbacks();

            // 3. 添加Transceiver（接收视频）
            peerConnection.AddTransceiver(TrackKind.Video, new RTCRtpTransceiverInit
            {
                direction = RTCRtpTransceiverDirection.RecvOnly
            });

            // 4. 创建Offer
            var offerOperation = peerConnection.CreateOffer();
            yield return offerOperation;

            if (offerOperation.IsError)
            {
                LogError($"创建Offer失败: {offerOperation.Error.message}");
                OnConnectionError?.Invoke(offerOperation.Error.message);
                isConnecting = false;
                yield break;
            }

            var offer = offerOperation.Desc;

            // 5. 设置本地描述
            var setLocalDescOperation = peerConnection.SetLocalDescription(ref offer);
            yield return setLocalDescOperation;

            if (setLocalDescOperation.IsError)
            {
                LogError($"设置本地描述失败: {setLocalDescOperation.Error.message}");
                OnConnectionError?.Invoke(setLocalDescOperation.Error.message);
                isConnecting = false;
                yield break;
            }

            LogInfo("本地Offer已创建，准备发送到服务器");

            // 6. 发送Offer到服务器并获取Answer
            yield return StartCoroutine(ExchangeSdpWithServer(offer.sdp));

            isConnecting = false;
        }

        private void SetupPeerConnectionCallbacks()
        {
            // ICE候选事件
            peerConnection.OnIceCandidate = candidate =>
            {
                LogInfo($"ICE Candidate: {candidate.Candidate}");
            };

            // ICE连接状态变化
            peerConnection.OnIceConnectionChange = state =>
            {
                LogInfo($"ICE连接状态: {state}");

                if (state == RTCIceConnectionState.Connected)
                {
                    isConnected = true;
                    OnConnected?.Invoke();
                }
                else if (state == RTCIceConnectionState.Disconnected || state == RTCIceConnectionState.Failed)
                {
                    isConnected = false;
                    OnDisconnected?.Invoke();
                }
            };

            // 连接状态变化
            peerConnection.OnConnectionStateChange = state =>
            {
                LogInfo($"连接状态: {state}");
            };

            // 接收视频轨道
            peerConnection.OnTrack = evt =>
            {
                LogInfo($"收到视频轨道: {evt.Track.Kind}");

                if (evt.Track is VideoStreamTrack videoTrack)
                {
                    // 获取视频纹理
                    videoTrack.OnVideoReceived += texture =>
                    {
                        videoTexture = texture;
                        OnVideoTextureReady?.Invoke(texture);
                        LogInfo($"视频纹理已准备: {texture.width}x{texture.height}");
                    };
                }
            };
        }

        private IEnumerator ExchangeSdpWithServer(string offerSdp)
        {
            // 构造请求体
            var requestData = new OfferRequestData
            {
                source = sourceType.ToString(),
                width = videoWidth,
                height = videoHeight,
                config = new OfferConfig
                {
                    left = isLeftEye,
                    shm = sharedMemoryName
                },
                sdp = offerSdp,
                type = "offer"
            };

            string jsonData = JsonUtility.ToJson(requestData);
            LogInfo($"发送Offer到服务器:\n{jsonData}");

            // 发送POST请求
            using (UnityWebRequest request = new UnityWebRequest($"{serverUrl}/offer", "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                // 忽略SSL证书验证（开发环境）
                request.certificateHandler = new CustomCertificateHandler();

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    LogError($"SDP交换失败: {request.error}");
                    OnConnectionError?.Invoke(request.error);
                    yield break;
                }

                // 解析Answer
                string responseText = request.downloadHandler.text;
                LogInfo($"收到服务器Answer:\n{responseText}");

                var answerData = JsonUtility.FromJson<AnswerResponseData>(responseText);

                // 设置远程描述
                var answer = new RTCSessionDescription
                {
                    type = RTCSdpType.Answer,
                    sdp = answerData.sdp
                };

                var setRemoteDescOperation = peerConnection.SetRemoteDescription(ref answer);
                yield return setRemoteDescOperation;

                if (setRemoteDescOperation.IsError)
                {
                    LogError($"设置远程描述失败: {setRemoteDescOperation.Error.message}");
                    OnConnectionError?.Invoke(setRemoteDescOperation.Error.message);
                }
                else
                {
                    LogInfo("WebRTC连接建立成功！");
                }
            }
        }

        #endregion

        #region 数据结构

        [Serializable]
        private class OfferRequestData
        {
            public string source;
            public int width;
            public int height;
            public OfferConfig config;
            public string sdp;
            public string type;
        }

        [Serializable]
        private class OfferConfig
        {
            public bool left;
            public string shm;
        }

        [Serializable]
        private class AnswerResponseData
        {
            public string sdp;
            public string type;
        }

        #endregion

        #region 日志辅助

        private void LogInfo(string message)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[WebRTCStreamClient {(isLeftEye ? "L" : "R")}] {message}");
            }
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"[WebRTCStreamClient {(isLeftEye ? "L" : "R")}] {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[WebRTCStreamClient {(isLeftEye ? "L" : "R")}] {message}");
        }

        #endregion
    }

    /// <summary>
    /// 视频源类型（对应后端的SourceType）
    /// </summary>
    public enum VideoSourceType
    {
        SHARE_MEMORY_STEREO,
        SHARE_MEMORY_MONO,
        ROS_STEREO,
        SHARE_MEMORY_STEREO_PROJECT
    }

    /// <summary>
    /// 自定义证书处理器（开发环境使用，生产环境需要正确验证）
    /// </summary>
    public class CustomCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true; // 开发环境：接受所有证书
        }
    }
}
