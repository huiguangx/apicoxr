using UnityEngine;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using Unity.XR.PXR;
using UnityEngine.XR;

namespace DataTracking
{
    /// <summary>
    /// XR设备位姿数据采集与发送
    /// 自动采集头显和手柄数据，发送到服务器
    /// 使用生产者-消费者模式：Update采集数据入队，独立线程发送HTTP请求
    /// </summary>
    public class DataTracking : MonoBehaviour
    {
        [Header("调试选项")]
        [SerializeField] private bool enableDebugLog = false;

        [Header("手腕旋转映射")]
        [Tooltip("启用手腕旋转映射（解决手柄旋转轴和机器人手腕旋转轴不一致的问题）")]
        public bool enableWristRotationMapping = false;
        [Tooltip("拖入 WristRotationMapper 组件")]
        public WristRotationMapper wristRotationMapper;

        [Header("网络设置")]
        [SerializeField] private string serverUrl = "https://localhost:5000/poseData";
        [Tooltip("发送队列最大容量（帧数）")]
        [SerializeField] private int queueMaxSize = 10;

        // XR设备引用
        private InputDevice headDevice;
        private InputDevice leftHandDevice;
        private InputDevice rightHandDevice;

        // 缓存数据
        private PoseCache headCache = new PoseCache();
        private ControllerCache leftCache = new ControllerCache();
        private ControllerCache rightCache = new ControllerCache();

        private UIController uiController;
        private bool _isSeethroughEnabled = true;
        private float _lastSendTime = -1f;
        private int _sendCounter = 0;

        // 生产者-消费者队列系统
        private ConcurrentQueue<string> sendQueue;
        private Thread sendThread;
        private volatile bool isRunning = false;
        private HttpClient httpClient;
        private int droppedFrames = 0;

        #region Unity生命周期

        private void Awake()
        {
            InitializeDevices();

            InputDevices.deviceConnected += OnDeviceConnected;
            InputDevices.deviceDisconnected += OnDeviceDisconnected;

            uiController = FindFirstObjectByType<UIController>();
            if (uiController == null && enableDebugLog)
            {
                Debug.LogWarning("⚠️ 未找到 UIController，将使用默认 serverUrl");
            }

            // 初始化生产者-消费者系统
            InitializeSendThread();
        }

        private void OnDestroy()
        {
            InputDevices.deviceConnected -= OnDeviceConnected;
            InputDevices.deviceDisconnected -= OnDeviceDisconnected;

            // 停止发送线程
            StopSendThread();
        }

        private void Update()
        {
            // 更新服务器URL
            if (uiController != null)
            {
                serverUrl = "https://" + uiController.serverBaseUrl + "/poseData";
            }

            // 确保设备有效
            if (!headDevice.isValid || !leftHandDevice.isValid || !rightHandDevice.isValid)
            {
                InitializeDevices();
            }

            // 采集并发送数据
            CollectAllDeviceData();
            SendDataToServer();
        }

        #endregion

        #region 设备管理

        private void InitializeDevices()
        {
            headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            leftHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            if (enableDebugLog)
            {
                Debug.Log($"[XR设备] 初始化: Head={headDevice.isValid}, Left={leftHandDevice.isValid}, Right={rightHandDevice.isValid}");
            }
        }

        private void OnDeviceConnected(InputDevice device)
        {
            if (enableDebugLog)
                Debug.Log($"[XR设备] 设备连接: {device.name}, Role: {device.role}");
            InitializeDevices();
        }

        private void OnDeviceDisconnected(InputDevice device)
        {
            if (enableDebugLog)
                Debug.Log($"[XR设备] 设备断开: {device.name}");
        }

        #endregion

        #region 数据采集

        private void CollectAllDeviceData()
        {
            if (headDevice.isValid)
            {
                CollectPoseData(headDevice, headCache);
            }

            if (leftHandDevice.isValid)
            {
                CollectPoseData(leftHandDevice, leftCache);
                CollectButtonData(leftHandDevice, leftCache.buttons);
                CollectJoystickData(leftHandDevice, ref leftCache.joystick);
            }

            if (rightHandDevice.isValid)
            {
                CollectPoseData(rightHandDevice, rightCache);
                CollectButtonData(rightHandDevice, rightCache.buttons);
                CollectJoystickData(rightHandDevice, ref rightCache.joystick);
            }
        }

        /// <summary>
        /// 采集设备位姿和速度数据
        /// </summary>
        private void CollectPoseData(InputDevice device, PoseCache cache)
        {
            device.TryGetFeatureValue(CommonUsages.devicePosition, out cache.position);
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out cache.rotation);
            device.TryGetFeatureValue(CommonUsages.deviceVelocity, out cache.velocity);
            device.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out cache.angularVelocity);
        }

        /// <summary>
        /// 采集手柄按钮数据
        /// </summary>
        private void CollectButtonData(InputDevice device, ButtonState[] buttons)
        {
            string deviceName = device == leftHandDevice ? "左手" : "右手";

            // 索引0: Trigger
            device.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue);
            device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerButton);
            SetButtonWithLog(buttons[0], triggerValue, triggerButton, triggerButton, $"{deviceName} Trigger");

            // 索引1: Grip
            device.TryGetFeatureValue(CommonUsages.grip, out float gripValue);
            device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripButton);
            SetButtonWithLog(buttons[1], gripValue, gripButton, gripButton, $"{deviceName} Grip");

            // 索引2: 摇杆按下
            device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool joystickClick);
            SetButtonWithLog(buttons[2], joystickClick ? 1f : 0f, joystickClick, false, $"{deviceName} 摇杆按下");

            // 索引3: 占位符
            buttons[3].Set(0f, false, false);

            // 索引4: X/A键 (Primary Button)
            device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryButton);
            device.TryGetFeatureValue(CommonUsages.primaryTouch, out bool primaryTouch);
            SetButtonWithLog(buttons[4], primaryButton ? 1f : 0f, primaryButton, primaryTouch, $"{deviceName} {(device == leftHandDevice ? "X" : "A")}键");

            // 索引5: Y/B键 (Secondary Button)
            device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondaryButton);
            device.TryGetFeatureValue(CommonUsages.secondaryTouch, out bool secondaryTouch);
            SetButtonWithLog(buttons[5], secondaryButton ? 1f : 0f, secondaryButton, secondaryTouch, $"{deviceName} {(device == leftHandDevice ? "Y" : "B")}键");

            // 索引6: 占位符
            buttons[6].Set(0f, false, false);
        }

        /// <summary>
        /// 设置按钮状态并在状态变化时打印日志
        /// </summary>
        private void SetButtonWithLog(ButtonState button, float value, bool pressed, bool touched, string buttonName)
        {
            bool stateChanged = button.pressed != pressed || button.touched != touched;

            if (stateChanged && enableDebugLog)
            {
                Debug.Log($"[按键] {buttonName}: value={value:F3}, pressed={pressed}, touched={touched}");
            }

            button.Set(value, pressed, touched);
        }

        /// <summary>
        /// 采集摇杆数据
        /// </summary>
        private void CollectJoystickData(InputDevice device, ref Vector2 joystick)
        {
            device.TryGetFeatureValue(CommonUsages.primary2DAxis, out joystick);
        }

        #endregion

        #region 数据发送

        private void SendDataToServer()
        {
            _sendCounter++;
            float currentTime = Time.time;

            if (_lastSendTime >= 0 && enableDebugLog)
            {
                float interval = currentTime - _lastSendTime;
                Debug.Log($"[入队] #{_sendCounter}: 间隔={interval:F4}s, 频率={(1f/interval):F1}Hz");
            }
            _lastSendTime = currentTime;

            var data = BuildSendData();

            // 打印右手数据
            if (enableDebugLog)
            {
                LogControllerData("Right", data.right);
            }

            string json = JsonUtility.ToJson(data, enableDebugLog);

            // 生产者：数据入队
            EnqueueData(json);
        }

        /// <summary>
        /// 生产者：将数据加入发送队列
        /// </summary>
        private void EnqueueData(string json)
        {
            if (sendQueue.Count >= queueMaxSize)
            {
                // 队列满，丢弃最旧的数据（保持实时性）
                sendQueue.TryDequeue(out _);
                droppedFrames++;

                if (enableDebugLog)
                {
                    Debug.LogWarning($"⚠️ 队列已满({queueMaxSize})，丢弃最旧数据，累计丢弃:{droppedFrames}帧");
                }
            }

            sendQueue.Enqueue(json);

            if (enableDebugLog)
            {
                Debug.Log($"✅ 数据已入队，队列长度: {sendQueue.Count}/{queueMaxSize}");
            }
        }

        /// <summary>
        /// 打印头显数据日志
        /// </summary>
        private void LogDeviceData(string deviceName, HeadInfo head)
        {
            // Debug.Log($"[{deviceName}] 位置: ({head.position.x:F3}, {head.position.y:F3}, {head.position.z:F3})");
            // Debug.Log($"[{deviceName}] 旋转: ({head.rotation.x:F3}, {head.rotation.y:F3}, {head.rotation.z:F3}, {head.rotation.w:F3})");
            // Debug.Log($"[{deviceName}] 线速度: ({head.linearVelocity.x:F3}, {head.linearVelocity.y:F3}, {head.linearVelocity.z:F3})");
            // Debug.Log($"[{deviceName}] 角速度: ({head.angularVelocity.x:F3}, {head.angularVelocity.y:F3}, {head.angularVelocity.z:F3})");
            string headString = JsonUtility.ToJson(head, true);
            Debug.Log($"✅ 发送VR数据JSON: {headString}");
        }

        /// <summary>
        /// 打印手柄数据日志
        /// </summary>
        private void LogControllerData(string deviceName, ControllerInfo controller)
        {
            // Debug.Log($"[{deviceName}] 位置: ({controller.position.x:F3}, {controller.position.y:F3}, {controller.position.z:F3})");
            // Debug.Log($"[{deviceName}] 旋转: ({controller.rotation.x:F3}, {controller.rotation.y:F3}, {controller.rotation.z:F3}, {controller.rotation.w:F3})");
            // Debug.Log($"[{deviceName}] 线速度: ({controller.linearVelocity.x:F3}, {controller.linearVelocity.y:F3}, {controller.linearVelocity.z:F3})");
            // Debug.Log($"[{deviceName}] 角速度: ({controller.angularVelocity.x:F3}, {controller.angularVelocity.y:F3}, {controller.angularVelocity.z:F3})");

            // 打印按钮状态
            string buttonStates = "";
            for (int i = 0; i < controller.button.Length; i++)
            {
                if (controller.button[i].pressed || controller.button[i].touched)
                {
                    buttonStates += $"[{i}:v={controller.button[i].value:F2},p={controller.button[i].pressed},t={controller.button[i].touched}] ";
                }
            }
            if (!string.IsNullOrEmpty(buttonStates))
            {
                Debug.Log($"[{deviceName}] 按钮: {buttonStates}");
            }

            // 打印摇杆数据
            if (controller.axes[2] != 0 || controller.axes[3] != 0)
            {
                Debug.Log($"[{deviceName}] 摇杆: X={controller.axes[2]:F3}, Y={controller.axes[3]:F3}");
            }
            string controllerString = JsonUtility.ToJson(controller, true);
            Debug.Log($"✅ 发送VR数据JSON: {controllerString}");
        }

        private SendVRData BuildSendData()
        {
            var data = new SendVRData();

            // Head
            data.head.position = new Vector3Data(ConvertVector3(headCache.position));
            data.head.rotation = new QuaternionData(ConvertQuaternion(headCache.rotation));
            data.head.linearVelocity = new Vector4Data(ConvertVector3(headCache.velocity));
            data.head.angularVelocity = new Vector4Data(ConvertVector3(headCache.angularVelocity));

            // Left
            FillControllerData(data.left, leftCache, true);

            // Right
            FillControllerData(data.right, rightCache, false);

            data.timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            return data;
        }

        private void FillControllerData(ControllerInfo info, ControllerCache cache, bool isLeft)
        {
            // 位姿
            info.position = new Vector3Data(ConvertVector3(cache.position));

            // 旋转（应用手腕映射）
            Quaternion rotation = cache.rotation;
            if (enableWristRotationMapping && wristRotationMapper != null)
            {
                rotation = wristRotationMapper.MapControllerToWrist(rotation);
            }
            info.rotation = new QuaternionData(ConvertQuaternion(rotation));

            // 速度
            info.linearVelocity = new Vector4Data(ConvertVector3(cache.velocity));
            info.angularVelocity = new Vector4Data(ConvertVector3(cache.angularVelocity));

            // 按钮
            for (int i = 0; i < 7; i++)
            {
                info.button[i] = new ButtonState
                {
                    value = cache.buttons[i].value,
                    pressed = cache.buttons[i].pressed,
                    touched = cache.buttons[i].touched
                };
            }

            // 摇杆 (左手系→右手系转换)
            info.axes[2] = cache.joystick.x;
            info.axes[3] = -cache.joystick.y;  // Y轴取反
        }

        /// <summary>
        /// 初始化发送线程系统
        /// </summary>
        private void InitializeSendThread()
        {
            sendQueue = new ConcurrentQueue<string>();

            // 配置 HttpClient（线程安全）
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true // 仅开发环境
            };
            httpClient = new HttpClient(handler)
            {
                Timeout = System.TimeSpan.FromSeconds(2)
            };

            // 启动发送线程
            isRunning = true;
            sendThread = new Thread(SendThreadLoop)
            {
                IsBackground = true,
                Name = "VR Data Send Thread"
            };
            sendThread.Start();

            if (enableDebugLog)
            {
                Debug.Log("✅ 发送线程已启动");
            }
        }

        /// <summary>
        /// 停止发送线程
        /// </summary>
        private void StopSendThread()
        {
            if (sendThread != null && sendThread.IsAlive)
            {
                isRunning = false;

                // 等待线程结束（最多1秒）
                if (!sendThread.Join(1000))
                {
                    Debug.LogWarning("⚠️ 发送线程未能在1秒内停止");
                }

                if (enableDebugLog)
                {
                    Debug.Log("🛑 发送线程已停止");
                }
            }

            httpClient?.Dispose();
        }

        /// <summary>
        /// 消费者：发送线程循环
        /// </summary>
        private void SendThreadLoop()
        {
            while (isRunning)
            {
                try
                {
                    // 从队列取数据
                    if (sendQueue.TryDequeue(out string json))
                    {
                        // 发送HTTP请求
                        SendHttpRequest(json);
                    }
                    else
                    {
                        // 队列为空，短暂休眠避免CPU占用
                        Thread.Sleep(1);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ 发送线程异常: {e.Message}");
                    Thread.Sleep(100); // 出错后等待一会儿
                }
            }
        }

        /// <summary>
        /// 消费者：执行HTTP请求（在发送线程中调用）
        /// </summary>
        private void SendHttpRequest(string jsonData)
        {
            try
            {
                string url = serverUrl;
                if (uiController != null)
                {
                    url = "https://" + uiController.serverBaseUrl + "/poseData";
                }

                if (string.IsNullOrEmpty(url))
                {
                    Debug.LogError("❌ 服务器URL为空");
                    return;
                }

                var content = new StringContent(
                    jsonData,
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                // 同步发送（在独立线程中，不会阻塞主线程）
                var response = httpClient.PostAsync(url, content).Result;

                if (response.IsSuccessStatusCode)
                {
                    // if (enableDebugLog)
                    // {
                        Debug.Log($"✅ HTTP发送成功: {response.StatusCode}");
                    // }
                }
                else
                {
                    // if (enableDebugLog)
                    // {
                        Debug.LogError($"❌ HTTP发送失败: {response.StatusCode}");
                    // }
                }
            }
            catch (System.Exception e)
            {
                if (enableDebugLog)
                {
                    Debug.LogError($"❌ HTTP请求异常: {e.Message}");
                }
            }
        }

        #endregion

        #region 坐标转换（左手系→右手系）

        private Vector3 ConvertVector3(Vector3 v)
        {
            return new Vector3(v.x, v.y, -v.z);
        }

        private Quaternion ConvertQuaternion(Quaternion q)
        {
            return new Quaternion(-q.x, -q.y, q.z, q.w);
        }

        #endregion

        #region 透视功能

        public void ToggleSeethrough()
        {
            _isSeethroughEnabled = !_isSeethroughEnabled;
            PXR_Manager.EnableVideoSeeThrough = _isSeethroughEnabled;
            Debug.Log($"🔄 透视已{(_isSeethroughEnabled ? "开启" : "关闭")}");
        }

        public bool IsSeethroughEnabled()
        {
            return _isSeethroughEnabled;
        }

        #endregion

        #region 内部数据结构

        /// <summary>
        /// 位姿数据缓存
        /// </summary>
        private class PoseCache
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity;
            public Vector3 angularVelocity;
        }

        /// <summary>
        /// 手柄数据缓存
        /// </summary>
        private class ControllerCache : PoseCache
        {
            public ButtonState[] buttons = new ButtonState[7];
            public Vector2 joystick;

            public ControllerCache()
            {
                for (int i = 0; i < 7; i++)
                {
                    buttons[i] = new ButtonState();
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// 按钮状态扩展方法
    /// </summary>
    public static class ButtonStateExtensions
    {
        public static void Set(this ButtonState btn, float value, bool pressed, bool touched)
        {
            btn.value = value;
            btn.pressed = pressed;
            btn.touched = touched;
        }
    }

    /// <summary>
    /// 自定义证书处理（开发环境用）
    /// </summary>
    public class CustomCertificateHandler : UnityEngine.Networking.CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true; // 仅开发环境使用
        }
    }
}
