using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using Unity.XR.PXR;

namespace DataTracking
{
    /// <summary>
    /// 从服务器接收震动指令并触发手柄震动
    /// </summary>
    public class HapticMessageReceiver : MonoBehaviour
    {
        [Header("服务器配置")]
        [Tooltip("消息接口完整 URL (从 UIController 自动获取)")]
        [SerializeField]
        private string messageApiUrl = "https://127.0.0.1:5000/msg"; // 仅显示，实际从 UIController 获取

        [Tooltip("轮询间隔（秒）")]
        [Range(0.05f, 5f)]
        public float pollInterval = 0.1f;

        [Tooltip("启用消息接收")]
        public bool enableMessageReceiving = true;

        [Header("调试选项")]
        [Tooltip("显示详细日志")]
        public bool verboseLogging = false;

        private float lastPollTime = 0f;
        private bool isPolling = false;
        private UIController uiController;

        private void Awake()
        {
            // 获取 UIController 引用
            uiController = UnityEngine.Object.FindObjectOfType<UIController>();
            if (uiController == null)
            {
                Debug.LogWarning("⚠️ 未找到 UIController，将使用默认 messageApiUrl");
            }
        }

        private void Update()
        {
            // 更新 Inspector 显示的 URL（从 UIController 同步）
            if (uiController != null)
            {
                // messageApiUrl = "https://" + uiController.serverBaseUrl + "/msg";
                messageApiUrl = "https://localhost:5000/msg"; // 测试固定地址
            }

            if (!enableMessageReceiving) return;
            // Debug.Log("轮询目标URL: " + messageApiUrl);
            // 定期轮询消息
            if (Time.time - lastPollTime >= pollInterval && !isPolling)
            {
                lastPollTime = Time.time;
                StartCoroutine(PollMessages());
            }
        }

        /// <summary>
        /// 轮询服务器消息
        /// </summary>
        private IEnumerator PollMessages()
        {
            isPolling = true;

            // ============ 假数据测试 ============
            // 取消下面的注释来测试震动功能（不调用真实服务器）
            // 测试完成后重新注释掉即可

            // string fakeJson = "{\"id\":\"vibrate\",\"data\":{\"side\":\"right\",\"intensity\":0.8,\"duration\":0.3}}";
            // Debug.Log("📨 [假数据测试] 收到消息: " + fakeJson);
            // ProcessMessage(fakeJson);
            // isPolling = false;
            // yield break;

            // ===================================

            // 从 UIController 获取基础地址并拼接完整 URL
            string url = messageApiUrl; // 默认值
            if (uiController != null)
            {
                url = "https://" + uiController.serverBaseUrl + "/msg";
            }

            var request = new UnityWebRequest(url, "POST");
            request.downloadHandler = new DownloadHandlerBuffer();
            request.certificateHandler = new CustomCertificateHandler();
            request.disposeCertificateHandlerOnDispose = true;
            request.timeout = 2; // 2秒超时

            Debug.Log($"🌐 请求URL: {url}");
            yield return request.SendWebRequest();

            // 打印完整的响应信息
            Debug.Log($"📡 响应码: {request.responseCode}");
            Debug.Log($"📡 响应结果: {request.result}");
            Debug.Log($"📡 响应内容: {request.downloadHandler.text}");
            Debug.Log($"📡 错误信息: {request.error}");

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;

                if (verboseLogging && !string.IsNullOrEmpty(json))
                {
                    Debug.Log($"📨 收到消息: {json}");
                }

                // 解析消息
                ProcessMessage(json);
            }
            else if (request.result != UnityWebRequest.Result.InProgress)
            {
                // 只在非超时错误时记录（避免日志刷屏）
                if (verboseLogging)
                {
                    Debug.LogWarning($"⚠️ 消息接收失败 {request.error}");
                }
            }

            request.Dispose();
            isPolling = false;
        }

        /// <summary>
        /// 处理接收到的消息
        /// </summary>
        private void ProcessMessage(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                // 解析 JSON - 先尝试解析为 ServerResponse（包含 msg 数组）
                var serverResponse = JsonUtility.FromJson<ServerResponse>(json);

                if (serverResponse == null || serverResponse.msg == null || serverResponse.msg.Length == 0)
                {
                    if (verboseLogging)
                        Debug.LogWarning("⚠️ 消息解析失败：JSON 格式错误或 msg 数组为空");
                    return;
                }

                // 遍历处理每个消息
                foreach (var message in serverResponse.msg)
                {
                    if (message == null) continue;

                    // 检查是否是震动指令
                    if (message.id == "vibrate")
                    {
                        if (message.data != null)
                        {
                            TriggerVibration(message.data);
                        }
                        else
                        {
                            Debug.LogWarning("⚠️ 震动指令缺少 data 字段");
                        }
                    }
                    else
                    {
                        // 其他类型的消息可以在这里处理
                        if (verboseLogging)
                            Debug.Log($"📬 收到其他消息: {message.id}");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ 消息处理异常: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// 触发手柄震动
        /// </summary>
        private void TriggerVibration(VibrationData data)
        {
            // 参数验证
            if (data == null)
            {
                Debug.LogWarning("⚠️ 震动数据为空");
                return;
            }

            // 强度限制在 0-1
            float intensity = Mathf.Clamp01(data.intensity);

            // 持续时间限制（秒）
            float duration = Mathf.Clamp(data.duration, 0.01f, 10f);

            // 转换为毫秒
            int durationMs = (int)(duration * 1000f);

            // 确定震动类型
            PXR_Input.VibrateType vibrateType;
            string sideText;

            switch (data.side.ToLower())
            {
                case "left":
                    vibrateType = PXR_Input.VibrateType.LeftController;
                    sideText = "左手";
                    break;
                case "right":
                    vibrateType = PXR_Input.VibrateType.RightController;
                    sideText = "右手";
                    break;
                case "both":
                    vibrateType = PXR_Input.VibrateType.BothController;
                    sideText = "双手";
                    break;
                default:
                    Debug.LogWarning($"⚠️ 未知的震动方向: {data.side}，使用右手");
                    vibrateType = PXR_Input.VibrateType.RightController;
                    sideText = "右手";
                    break;
            }

            // 触发震动 - PICO Native API
            try
            {
                PXR_Input.SendHapticImpulse(
                    vibrateType,
                    intensity,
                    durationMs,
                    200  // 默认频率 200Hz（中频）
                );

                Debug.Log($"📳 {sideText}柄震动: 强度={intensity:F2}, 时长={duration:F2}秒");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ PICO 震动失败: {e.Message}");
            }

            // PCVR 兼容震动
            TriggerVibrationForPCVR(data.side, intensity, duration);
        }

        /// <summary>
        /// PCVR 模式震动支持
        /// </summary>
        private void TriggerVibrationForPCVR(string side, float intensity, float duration)
        {
            try
            {
                var xrDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();

                // 根据 side 确定设备特征
                UnityEngine.XR.InputDeviceCharacteristics characteristics =
                    UnityEngine.XR.InputDeviceCharacteristics.Controller;

                if (side.ToLower() == "left")
                {
                    characteristics |= UnityEngine.XR.InputDeviceCharacteristics.Left;
                }
                else if (side.ToLower() == "right")
                {
                    characteristics |= UnityEngine.XR.InputDeviceCharacteristics.Right;
                }
                else if (side.ToLower() == "both")
                {
                    // 两个手柄都震动
                    TriggerVibrationForPCVR("left", intensity, duration);
                    TriggerVibrationForPCVR("right", intensity, duration);
                    return;
                }

                UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(characteristics, xrDevices);

                foreach (var device in xrDevices)
                {
                    if (device.TryGetHapticCapabilities(out var capabilities) &&
                        capabilities.supportsImpulse)
                    {
                        device.SendHapticImpulse(0, intensity, duration);

                        if (verboseLogging)
                            Debug.Log($"✅ PCVR 震动已发送到: {device.name}");
                    }
                }
            }
            catch (System.Exception e)
            {
                if (verboseLogging)
                    Debug.LogWarning($"⚠️ PCVR 震动失败: {e.Message}");
            }
        }

        /// <summary>
        /// 手动触发震动（用于测试）
        /// </summary>
        [ContextMenu("测试：震动右手柄")]
        public void TestVibrateRight()
        {
            var testData = new VibrationData
            {
                side = "right",
                intensity = 0.8f,
                duration = 0.3f
            };
            TriggerVibration(testData);
        }

        [ContextMenu("测试：震动左手柄")]
        public void TestVibrateLeft()
        {
            var testData = new VibrationData
            {
                side = "left",
                intensity = 0.8f,
                duration = 0.3f
            };
            TriggerVibration(testData);
        }

        [ContextMenu("测试：震动双手柄")]
        public void TestVibrateBoth()
        {
            var testData = new VibrationData
            {
                side = "both",
                intensity = 1.0f,
                duration = 0.5f
            };
            TriggerVibration(testData);
        }
    }

    // ===== 数据结构 =====

    /// <summary>
    /// 服务器响应结构（包含 msg 数组）
    /// </summary>
    [System.Serializable]
    public class ServerResponse
    {
        public MessageWrapper[] msg;
    }

    /// <summary>
    /// 消息包装结构
    /// </summary>
    [System.Serializable]
    public class MessageWrapper
    {
        public string id;
        public VibrationData data;
    }

    /// <summary>
    /// 震动数据结构
    /// </summary>
    [System.Serializable]
    public class VibrationData
    {
        public string side;      // "left" | "right" | "both"
        public float intensity;  // 0-1
        public float duration;   // 秒
    }
}
