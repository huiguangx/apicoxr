using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Unity.XR.PXR;

namespace DataTracking
{
    public class DataTracking : MonoBehaviour
    {
        [Header("XRI Default Input Actions 中的 Head Position/Rotation Action")]
        public InputActionReference deviceHeadPositionRef;
        public InputActionReference deviceHeadRotationRef;
        public InputActionReference deviceHeadVelocityRef;
        public InputActionReference deviceHeadAngularVelocityRef;

        [Header("Left Hand")]
        public InputActionReference leftPositionRef;
        public InputActionReference leftRotationRef;
        public InputActionReference leftVelocityRef;
        public InputActionReference leftAngularVelocityRef;
        public InputActionReference leftGripRef;
        public InputActionReference leftXButtonRef;  // 左手X按键
        public InputActionReference leftYButtonRef;  // 左手Y按键
        public InputActionReference leftTriggerRef;  // 左手Trigger按键
        public InputActionReference left2DAxisRef;   // 左手2D摇杆轴

        [Header("Right Hand")]
        public InputActionReference rightPositionRef;
        public InputActionReference rightRotationRef;
        public InputActionReference rightVelocityRef;
        public InputActionReference rightAngularVelocityRef;
        public InputActionReference rightAButtonRef;
        public InputActionReference rightBButtonRef;
        public InputActionReference rightGripRef;
        public InputActionReference rightTriggerRef; // 右手Trigger按键
        public InputActionReference right2DAxisRef;  // 右手2D摇杆轴

        [Header("手腕旋转映射（可选）")]
        [Tooltip("启用手腕旋转映射（解决手柄旋转轴和机器人手腕旋转轴不一致的问题）")]
        public bool enableWristRotationMapping = false;
        [Tooltip("拖入 WristRotationMapper 组件")]
        public WristRotationMapper wristRotationMapper;

        // Head
        private Vector3 _headPosition = Vector3.zero;
        private Quaternion _headRotation = Quaternion.identity;
        private Vector3 _headVelocity = Vector3.zero;
        private Vector3 _headAngularVelocity = Vector3.zero;

        // Left Hand
        private Vector3 _leftPosition = Vector3.zero;
        private Quaternion _leftRotation = Quaternion.identity;
        private Vector3 _leftVelocity = Vector3.zero;
        private Vector3 _leftAngularVelocity = Vector3.zero;

        // Right Hand
        private Vector3 _rightPosition = Vector3.zero;
        private Quaternion _rightRotation = Quaternion.identity;
        private Vector3 _rightVelocity = Vector3.zero;
        private Vector3 _rightAngularVelocity = Vector3.zero;
        // Left Buttons: 7 个按钮状态（index 2 = Grip）
        private ButtonState[] _leftButtons;
        // Right Buttons: 7 个按钮状态（index 4 = A, index 5 = B）
        private ButtonState[] _rightButtons;

        // Left Joystick Axes
        private Vector2 _left2DAxis = Vector2.zero; // 左手2D摇杆轴数据
        // Right Joystick Axes
        private Vector2 _right2DAxis = Vector2.zero; // 右手2D摇杆轴数据

        [Header("Network Settings")]
        [Tooltip("服务器完整 URL (从 UIController 自动获取)")]
        [SerializeField]
        private string serverUrl = "https://127.0.0.1:5000/poseData"; // 仅显示，实际从 UIController 获取
        // private float lastSendTime = 0f;
        public float sendInterval = 0.1f; // 发送间隔（秒）

        private UIController uiController;

        // 透视功能状态
        private bool _isSeethroughEnabled = true;
        private float _lastSendTime = -1f;  // 记录上次发送时间，初始值设为-1表示尚未发送过
        private int _sendCounter = 0;      // 发送计数器

        private void Awake()
            {
                // 视频透视
                // PXR_Manager.EnableVideoSeeThrough = true;

                // // 配置主相机以支持视频透视（必须设置！）
                // Camera mainCamera = Camera.main;
                // if (mainCamera != null)
                // {
                //     mainCamera.clearFlags = CameraClearFlags.SolidColor;
                //     Color bgColor = mainCamera.backgroundColor;
                //     bgColor.a = 0f; // Alpha 必须为 0 才能显示透视层
                //     mainCamera.backgroundColor = bgColor;
                //     Debug.Log("✅ 已配置主相机支持视频透视: ClearFlags=SolidColor, Alpha=0");
                // }
                // else
                // {
                //     Debug.LogError("❌ 未找到主相机！无法配置视频透视");
                // }

                // 初始化按钮数组
                _leftButtons = new ButtonState[7];
                _rightButtons = new ButtonState[7];
                for (int i = 0; i < 7; i++)
                {
                    _leftButtons[i] = new ButtonState();
                    _rightButtons[i] = new ButtonState();
                }

                // Head
                EnableAction(deviceHeadPositionRef);
                EnableAction(deviceHeadRotationRef);
                EnableAction(deviceHeadVelocityRef);
                EnableAction(deviceHeadAngularVelocityRef);

                // Left
                EnableAction(leftPositionRef);
                EnableAction(leftRotationRef);
                EnableAction(leftVelocityRef);
                EnableAction(leftAngularVelocityRef);
                EnableAction(leftGripRef);
                EnableAction(leftXButtonRef);  // 启用左手X按键
                EnableAction(leftYButtonRef);  // 启用左手Y按键
                EnableAction(leftTriggerRef);  // 启用左手Trigger按键
                EnableAction(left2DAxisRef);   // 启用左手2D摇杆轴

                // Right
                EnableAction(rightPositionRef);
                EnableAction(rightRotationRef);
                EnableAction(rightVelocityRef);
                EnableAction(rightAngularVelocityRef);
                EnableAction(rightAButtonRef);
                EnableAction(rightBButtonRef);
                EnableAction(rightGripRef);
                EnableAction(rightTriggerRef); // 启用右手Trigger按键
                EnableAction(right2DAxisRef);  // 启用右手2D摇杆轴

                // 获取 UIController 引用
                uiController = UnityEngine.Object.FindObjectOfType<UIController>();
                if (uiController == null)
                {
                    // Debug.LogWarning("⚠️ 未找到 UIController，将使用默认 serverUrl");
                }
            }

        private void OnEnable()
        {
            // 不再需要订阅事件，所有数据都在Update中直接读取
        }

        private void OnDisable()
        {
            // 不再需要取消订阅事件
        }

        // --- Helper Methods ---
        private void EnableAction(InputActionReference actionRef)
        {
            actionRef?.action?.Enable();
        }

        private void DisableAction(InputActionReference actionRef)
        {
            actionRef?.action?.Disable();
        }

        private void SubscribeVector3(InputActionReference actionRef, System.Action<Vector3> callback)
        {
            if (actionRef != null)
                actionRef.action.performed += ctx => callback(ctx.ReadValue<Vector3>());
        }

        private void SubscribeQuaternion(InputActionReference actionRef, System.Action<Quaternion> callback)
        {
            if (actionRef != null)
                actionRef.action.performed += ctx => callback(ctx.ReadValue<Quaternion>());
        }

        private void SubscribeVector2(InputActionReference actionRef, System.Action<Vector2> callback)
        {
            if (actionRef != null)
                actionRef.action.performed += ctx => callback(ctx.ReadValue<Vector2>());
        }

        // 左手系 Vector3 → 右手系
        private Vector3 LHtoRH_Vector3(Vector3 v)
        {
            return new Vector3(v.x, v.y, -v.z);
        }

        // 左手系 Quaternion → 右手系
        private Quaternion LHtoRH_Quaternion(Quaternion q)

        {
            return new Quaternion(-q.x, -q.y, q.z, q.w);
        }


        /// <summary>
        /// PCVR 模式震动支持
        /// </summary>
        private void TriggerHapticForPCVR(InputAction.CallbackContext ctx)
        {
            try
            {
                // 使用 Unity XR 标准 API（PCVR 兼容）
                var xrDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
                UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                    UnityEngine.XR.InputDeviceCharacteristics.Controller |
                    UnityEngine.XR.InputDeviceCharacteristics.Right,
                    xrDevices
                );

                foreach (var device in xrDevices)
                {
                    if (device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
                    {
                        device.SendHapticImpulse(0, 0.8f, 0.3f);
                        Debug.Log($"✅ PCVR 震动发送到: {device.name}");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"⚠️ PCVR 震动失败: {e.Message}");
            }
        }

        // --- Getters (直接读取实时数据) ---
        public Vector3 GetHeadPosition() => 
            IsActionEnabled(deviceHeadPositionRef) ? deviceHeadPositionRef.action.ReadValue<Vector3>() : Vector3.zero;

        public Quaternion GetHeadRotation() => 
            IsActionEnabled(deviceHeadRotationRef) ? deviceHeadRotationRef.action.ReadValue<Quaternion>() : Quaternion.identity;

        public Vector3 GetHeadVelocity() => 
            IsActionEnabled(deviceHeadVelocityRef) ? deviceHeadVelocityRef.action.ReadValue<Vector3>() : Vector3.zero;

        public Vector3 GetHeadAngularVelocity() => 
            IsActionEnabled(deviceHeadAngularVelocityRef) ? deviceHeadAngularVelocityRef.action.ReadValue<Vector3>() : Vector3.zero;

        public Vector3 GetLeftPosition() => 
            IsActionEnabled(leftPositionRef) ? leftPositionRef.action.ReadValue<Vector3>() : Vector3.zero;

        public Quaternion GetLetfRotation() => 
            IsActionEnabled(leftRotationRef) ? leftRotationRef.action.ReadValue<Quaternion>() : Quaternion.identity;

        public Vector3 GetLeftVelocity() => 
            IsActionEnabled(leftVelocityRef) ? leftVelocityRef.action.ReadValue<Vector3>() : Vector3.zero;

        public Vector3 GetLeftAngularVelocity() => 
            IsActionEnabled(leftAngularVelocityRef) ? leftAngularVelocityRef.action.ReadValue<Vector3>() : Vector3.zero;

        public Vector3 GetRightPosition() => 
            IsActionEnabled(rightPositionRef) ? rightPositionRef.action.ReadValue<Vector3>() : Vector3.zero;

        public Quaternion GetRightRotation() => 
            IsActionEnabled(rightRotationRef) ? rightRotationRef.action.ReadValue<Quaternion>() : Quaternion.identity;

        public Vector3 GetRightVelocity() => 
            IsActionEnabled(rightVelocityRef) ? rightVelocityRef.action.ReadValue<Vector3>() : Vector3.zero;

        public Vector3 GetRightAngularVelocity() => 
            IsActionEnabled(rightAngularVelocityRef) ? rightAngularVelocityRef.action.ReadValue<Vector3>() : Vector3.zero;

        private bool IsActionEnabled(InputActionReference actionRef) =>
            actionRef?.action?.enabled == true;

        private void SendVRDataToServer()
        {
            // 计算发送频率相关信息
            float currentTime = Time.time;
            _sendCounter++;
            
            // 如果不是第一次发送，则计算与上次发送的时间间隔
            if (_lastSendTime >= 0)
            {
                float timeSinceLastSend = currentTime - _lastSendTime;
                // Debug.Log($"[HTTP发送频率] #{_sendCounter}: 间隔={timeSinceLastSend:F4}s, 频率={(1f/timeSinceLastSend):F1}Hz");
            }
            
            // 更新上次发送时间
            _lastSendTime = currentTime;
            
            var data = new SendVRData();

            // Head
            data.head.position = new Vector3Data(LHtoRH_Vector3(GetHeadPosition()));
            data.head.rotation = new QuaternionData(LHtoRH_Quaternion(GetHeadRotation()));
            data.head.linearVelocity = new Vector4Data(LHtoRH_Vector3(GetHeadVelocity()));
            data.head.angularVelocity = new Vector4Data(LHtoRH_Vector3(GetHeadAngularVelocity()));

            // Left
            data.left.position = new Vector3Data(LHtoRH_Vector3(GetLeftPosition()));

            // 左手旋转：如果启用旋转映射，则应用映射
            Quaternion leftRotation = GetLetfRotation();
            if (enableWristRotationMapping && wristRotationMapper != null)
            {
                leftRotation = wristRotationMapper.MapControllerToWrist(leftRotation);
            }
            data.left.rotation = new QuaternionData(LHtoRH_Quaternion(leftRotation));

            data.left.linearVelocity = new Vector4Data(LHtoRH_Vector3(GetLeftVelocity()));
            data.left.angularVelocity = new Vector4Data(LHtoRH_Vector3(GetLeftAngularVelocity()));
            // left.button 保持默认（全 false）
            // left.axes 已在构造函数中初始化为 [0,0,0,0]

            // Right
            data.right.position = new Vector3Data(LHtoRH_Vector3(GetRightPosition()));

            // 右手旋转：如果启用旋转映射，则应用映射
            Quaternion rightRotation = GetRightRotation();
            if (enableWristRotationMapping && wristRotationMapper != null)
            {
                rightRotation = wristRotationMapper.MapControllerToWrist(rightRotation);
            }
            data.right.rotation = new QuaternionData(LHtoRH_Quaternion(rightRotation));

            data.right.linearVelocity = new Vector4Data(LHtoRH_Vector3(GetRightVelocity()));
            data.right.angularVelocity = new Vector4Data(LHtoRH_Vector3(GetRightAngularVelocity()));

            // 深拷贝按钮状态
            // Left buttons
            data.left.button = new ButtonState[_leftButtons.Length];
            for (int i = 0; i < _leftButtons.Length; i++)
            {
                var src = _leftButtons[i];
                data.left.button[i] = new ButtonState
                {
                    value = src.value,
                    pressed = src.pressed,
                    touched = src.touched
                };
            }

            // Right buttons
            data.right.button = new ButtonState[_rightButtons.Length];
            for (int i = 0; i < _rightButtons.Length; i++)
            {
                var src = _rightButtons[i];
                data.right.button[i] = new ButtonState
                {
                    value = src.value,
                    pressed = src.pressed,
                    touched = src.touched
                };
            }

            // 将左手2D摇杆轴数据填充到 axes 数组的后两位 (索引 2, 3)
            data.left.axes[2] = _left2DAxis.x;
            data.left.axes[3] = _left2DAxis.y;

            // 将右手2D摇杆轴数据填充到 axes 数组的后两位 (索引 2, 3)
            data.right.axes[2] = _right2DAxis.x;
            data.right.axes[3] = _right2DAxis.y;
            // Debug.Log("✅ 摇杆数据" + _left2DAxis.x + "," + _left2DAxis.y + "," + _right2DAxis.x + "," + _right2DAxis.y);

            data.timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            string json = JsonUtility.ToJson(data, true);
            
            // 发送到服务器
            StartCoroutine(PostDataToServer(json, _sendCounter));
        }

        private IEnumerator PostDataToServer(string jsonData, int sendNumber)
        {
            // 从 UIController 获取基础地址并拼接完整 URL
            string url = serverUrl; // 默认值
            if (uiController != null)
            {
                // url = "https://" + uiController.serverBaseUrl + "/poseData";
                url = "https://localhost:5000/poseData"; // 测试固定地址
            }
            
            // 检查URL是否有效
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("服务器URL为空");
                yield break;
            }

            var request = new UnityEngine.Networking.UnityWebRequest(url, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            // 忽略SSL证书错误（仅用于开发环境）
            request.certificateHandler = new CustomCertificateHandler();
            request.disposeCertificateHandlerOnDispose = true;

            yield return request.SendWebRequest();


            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                // Debug.LogError("发送VR数据失败. 错误信息1: " + request.error +
                //               "\n响应代码: " + request.responseCode +
                //               "\nURL: " + url);
            }
            else
            {
                // Debug.Log("成功发送VR数据到服务器. 响应代码: " + url + '-' + request.responseCode + jsonData);
            }

            request.Dispose();
        }

        void Update()
        {
            // 更新 Inspector 显示的 URL（从 UIController 同步）
            if (uiController != null)
            {
                serverUrl = "https://" + uiController.serverBaseUrl + "/poseData";
            }

            // 每帧直接读取所有输入数据
            if (IsActionEnabled(deviceHeadPositionRef))
                _headPosition = deviceHeadPositionRef.action.ReadValue<Vector3>();
            if (IsActionEnabled(deviceHeadRotationRef))
                _headRotation = deviceHeadRotationRef.action.ReadValue<Quaternion>();
            if (IsActionEnabled(deviceHeadVelocityRef))
                _headVelocity = deviceHeadVelocityRef.action.ReadValue<Vector3>();
            if (IsActionEnabled(deviceHeadAngularVelocityRef))
                _headAngularVelocity = deviceHeadAngularVelocityRef.action.ReadValue<Vector3>();
                
            if (IsActionEnabled(leftPositionRef))
                _leftPosition = leftPositionRef.action.ReadValue<Vector3>();
            if (IsActionEnabled(leftRotationRef))
                _leftRotation = leftRotationRef.action.ReadValue<Quaternion>();
            if (IsActionEnabled(leftVelocityRef))
                _leftVelocity = leftVelocityRef.action.ReadValue<Vector3>();
            if (IsActionEnabled(leftAngularVelocityRef))
                _leftAngularVelocity = leftAngularVelocityRef.action.ReadValue<Vector3>();
                
            if (IsActionEnabled(rightPositionRef))
                _rightPosition = rightPositionRef.action.ReadValue<Vector3>();
            if (IsActionEnabled(rightRotationRef))
                _rightRotation = rightRotationRef.action.ReadValue<Quaternion>();
            if (IsActionEnabled(rightVelocityRef))
                _rightVelocity = rightVelocityRef.action.ReadValue<Vector3>();
            if (IsActionEnabled(rightAngularVelocityRef))
                _rightAngularVelocity = rightAngularVelocityRef.action.ReadValue<Vector3>();
            
            // 每帧更新Trigger和Grip的值
            if (IsActionEnabled(leftTriggerRef)) {
                float currentValue = leftTriggerRef.action.ReadValue<float>();
                _leftButtons[0].value = currentValue;
                _leftButtons[0].pressed = currentValue > 0.1f;

            }
            
            if (IsActionEnabled(leftGripRef)) {
                float currentValue = leftGripRef.action.ReadValue<float>();
                _leftButtons[1].value = currentValue;
                _leftButtons[1].pressed = currentValue > 0.1f;
            }
            if (IsActionEnabled(rightTriggerRef)) {
                float currentValue = rightTriggerRef.action.ReadValue<float>();
                _rightButtons[0].value = currentValue;
                _rightButtons[0].pressed = currentValue > 0.1f;
            }
            
            if (IsActionEnabled(rightGripRef)) {
                float currentValue = rightGripRef.action.ReadValue<float>();
                _rightButtons[1].value = currentValue;
                _rightButtons[1].pressed = currentValue > 0.1f;
            }
            
            // 更新摇杆轴数据
            if (IsActionEnabled(left2DAxisRef)) {
                Vector2 newLeftAxis = left2DAxisRef.action.ReadValue<Vector2>();
                // 只有当值发生变化时才输出日志
                if (newLeftAxis != _left2DAxis) {
                    _left2DAxis = newLeftAxis;
                    Debug.Log($"左手2D摇杆轴数据更新: x={_left2DAxis.x:F3}, y={_left2DAxis.y:F3}");
                }
            }
                
            if (IsActionEnabled(right2DAxisRef)) {
                Vector2 newRightAxis = right2DAxisRef.action.ReadValue<Vector2>();
                // 只有当值发生变化时才输出日志
                if (newRightAxis != _right2DAxis) {
                    _right2DAxis = newRightAxis;
                    Debug.Log($"右手2D摇杆轴数据更新: x={_right2DAxis.x:F3}, y={_right2DAxis.y:F3}");
                }
            }
            
            // 检查并输出按钮状态变化的日志（ABXY按钮）
            CheckAndLogButtonChanges();
            
            // 每帧发送数据到服务器
                SendVRDataToServer();
            
        }

        private void CheckAndLogButtonChanges()
        {
            // 检查左手柄按钮状态变化（仅ABXY按钮）
            if (IsActionEnabled(leftXButtonRef))
            {
                float currentValue = leftXButtonRef.action.ReadValue<float>();
                bool currentlyPressed = currentValue > 0.5f;
                if (currentlyPressed != _leftButtons[4].pressed)
                {
                    _leftButtons[4].pressed = currentlyPressed;
                    _leftButtons[4].value = currentlyPressed ? 1f : 0f; // ABXY按钮只传0或1
                    Debug.Log($"左手X键{(currentlyPressed ? "按下" : "释放")}");
                }
            }

            if (IsActionEnabled(leftYButtonRef))
            {
                float currentValue = leftYButtonRef.action.ReadValue<float>();
                bool currentlyPressed = currentValue > 0.5f;
                if (currentlyPressed != _leftButtons[5].pressed)
                {
                    _leftButtons[5].pressed = currentlyPressed;
                    _leftButtons[5].value = currentlyPressed ? 1f : 0f; // ABXY按钮只传0或1
                    Debug.Log($"左手Y键{(currentlyPressed ? "按下" : "释放")}");
                }
            }

            // 检查右手柄按钮状态变化（仅ABXY按钮）
            if (IsActionEnabled(rightAButtonRef))
            {
                float currentValue = rightAButtonRef.action.ReadValue<float>();
                bool currentlyPressed = currentValue > 0.5f;
                if (currentlyPressed != _rightButtons[4].pressed)
                {
                    _rightButtons[4].pressed = currentlyPressed;
                    _rightButtons[4].value = currentlyPressed ? 1f : 0f; // ABXY按钮只传0或1
                    Debug.Log($"右手A键{(currentlyPressed ? "按下" : "释放")}");
                }
            }

            if (IsActionEnabled(rightBButtonRef))
            {
                float currentValue = rightBButtonRef.action.ReadValue<float>();
                bool currentlyPressed = currentValue > 0.5f;
                if (currentlyPressed != _rightButtons[5].pressed)
                {
                    _rightButtons[5].pressed = currentlyPressed;
                    _rightButtons[5].value = currentlyPressed ? 1f : 0f; // ABXY按钮只传0或1
                    Debug.Log($"右手B键{(currentlyPressed ? "按下" : "释放")}");
                }
            }
        }

        [ContextMenu("Test Generate JSON")]
        void TestGenerateJSON()
            {
                bool anyPressed = 
                _rightButtons[4].pressed || _rightButtons[5].pressed || _rightButtons[1].pressed || _rightButtons[0].pressed ||
                _leftButtons[1].pressed || _leftButtons[0].pressed || _leftButtons[4].pressed || _leftButtons[5].pressed;

                if (anyPressed)
                {
                    var data = new SendVRData();

                    // Head
                    data.head.position = new Vector3Data(GetHeadPosition());
                    data.head.rotation = new QuaternionData(GetHeadRotation());
                    data.head.linearVelocity = new Vector4Data(GetHeadVelocity());      // ✅ Vector4Data
                    data.head.angularVelocity = new Vector4Data(GetHeadAngularVelocity()); // ✅

                    // Left
                    data.left.position = new Vector3Data(GetLeftPosition());
                    data.left.rotation = new QuaternionData(GetLetfRotation());
                    data.left.linearVelocity = new Vector4Data(GetLeftVelocity());       // ✅
                    data.left.angularVelocity = new Vector4Data(GetLeftAngularVelocity()); // ✅
                    
                    // 设置左手摇杆轴数据到axes数组的后两位（索引2和3）
                    data.left.axes[2] = _left2DAxis.x;
                    data.left.axes[3] = _left2DAxis.y;

                    // Right
                    data.right.position = new Vector3Data(GetRightPosition());
                    data.right.rotation = new QuaternionData(GetRightRotation());
                    data.right.linearVelocity = new Vector4Data(GetRightVelocity());     // ✅
                    data.right.angularVelocity = new Vector4Data(GetRightAngularVelocity()); // ✅
                    
                    // 设置右手摇杆轴数据到axes数组的后两位（索引2和3）
                    data.right.axes[2] = _right2DAxis.x;
                    data.right.axes[3] = _right2DAxis.y;

                    // 深拷贝按钮状态
                    // Left buttons
                    data.left.button = new ButtonState[_leftButtons.Length];
                    for (int i = 0; i < _leftButtons.Length; i++)
                    {
                        var src = _leftButtons[i];
                        data.left.button[i] = new ButtonState
                        {
                            value = src.value,
                            pressed = src.pressed,
                            touched = src.touched
                        };
                    }

                    // Right buttons
                    data.right.button = new ButtonState[_rightButtons.Length];
                    for (int i = 0; i < _rightButtons.Length; i++)
                    {
                        var src = _rightButtons[i];
                        data.right.button[i] = new ButtonState
                        {
                            value = src.value,
                            pressed = src.pressed,
                            touched = src.touched
                        };
                    }

                    // axes 不需要赋值，默认就是 [0,0,0,0]，除了摇杆轴数据已在上面设置

                    data.timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    string json = JsonUtility.ToJson(data, true);
                    Debug.Log("✅ 按钮按下中 - VR 数据:\n" + json);
                }
            }
        
        /// <summary>
        /// 切换透视功能
        /// </summary>
        public void ToggleSeethrough()
        {
            _isSeethroughEnabled = !_isSeethroughEnabled;
            PXR_Manager.EnableVideoSeeThrough = _isSeethroughEnabled;
            Debug.Log($"🔄 透视已{(_isSeethroughEnabled ? "开启" : "关闭")}");
        }

        /// <summary>
        /// 获取透视状态
        /// </summary>
        public bool IsSeethroughEnabled()
        {
            return _isSeethroughEnabled;
        }
    }

    // 自定义证书处理程序，用于忽略SSL证书错误
    public class CustomCertificateHandler : UnityEngine.Networking.CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            // 在开发环境中忽略证书验证
            // 注意：在生产环境中不应忽略证书验证
            return true;
        }
    }
}