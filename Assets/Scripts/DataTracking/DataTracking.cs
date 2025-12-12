using UnityEngine;
using System.Collections;
using Unity.XR.PXR;
using UnityEngine.XR;

namespace DataTracking
{
    /// <summary>
    /// XR设备位姿数据采集与发送
    /// 自动采集头显和手柄数据，发送到服务器
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
        [SerializeField] private string serverUrl = "https://127.0.0.1:5000/poseData";

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
        }

        private void OnDestroy()
        {
            InputDevices.deviceConnected -= OnDeviceConnected;
            InputDevices.deviceDisconnected -= OnDeviceDisconnected;
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
                Debug.Log($"[HTTP发送] #{_sendCounter}: 间隔={interval:F4}s, 频率={(1f/interval):F1}Hz");
            }
            _lastSendTime = currentTime;

            var data = BuildSendData();

            // 打印右手数据
            // if (enableDebugLog)
            // {
                LogControllerData("Right", data.right);
            // }

            string json = JsonUtility.ToJson(data, enableDebugLog);

            if (enableDebugLog)
            {
                Debug.Log($"✅ 发送VR数据JSON: {json}");
            }

            StartCoroutine(PostDataToServer(json));
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

        private IEnumerator PostDataToServer(string jsonData)
        {
            string url = uiController != null
                ? "https://localhost:5000/poseData"
                : serverUrl;

            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("❌ 服务器URL为空");
                yield break;
            }

            var request = new UnityEngine.Networking.UnityWebRequest(url, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.certificateHandler = new CustomCertificateHandler();
            request.disposeCertificateHandlerOnDispose = true;

            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                if (enableDebugLog)
                {
                    Debug.LogError($"❌ 发送失败: {request.error} (Code: {request.responseCode})");
                }
            }
            else if (enableDebugLog)
            {
                Debug.Log($"✅ 发送成功 (Code: {request.responseCode})");
            }

            request.Dispose();
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
