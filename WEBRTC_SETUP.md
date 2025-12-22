# Unity WebRTC视频流集成文档

## 概述

本项目已成功集成Unity WebRTC包，支持通过WebRTC协议接收后端服务器的实时视频流。WebRTC相比MJPEG具有更低的延迟和更高的编码效率。

## 架构说明

### 核心组件

1. **WebRTCStreamClient.cs** - WebRTC单眼视频流客户端
   - 管理RTCPeerConnection
   - 实现SDP Offer/Answer交换
   - 接收H.264视频轨道
   - 输出视频纹理

2. **StereoWebRTCStreamManager.cs** - 双目WebRTC流管理器
   - 管理左右眼WebRTC客户端
   - 在VR空间中渲染视频
   - 提供与StereoVideoStreamManager类似的接口

3. **UIController.cs** - UI控制器（已更新）
   - 支持MJPEG和WebRTC两种流类型切换
   - 统一的视频流控制接口

### 与后端的对接

本实现完全对接后端的WebRTC服务器（`C:\work_project\pico_project\app\server`）：

**后端端点**: `POST https://<server>:5000/offer`

**请求格式**:
```json
{
  "source": "SHARE_MEMORY_STEREO",  // 或 ROS_STEREO, SHARE_MEMORY_MONO
  "width": 1280,
  "height": 720,
  "config": {
    "left": true,  // true=左眼, false=右眼
    "shm": "stereo_color_image_shm"
  },
  "sdp": "...",  // SDP Offer
  "type": "offer"
}
```

**响应格式**:
```json
{
  "sdp": "...",  // SDP Answer
  "type": "answer"
}
```

**视频编码**: H.264, 码率 3-8 Mbps, 帧率 30-60 FPS

## Unity场景配置

### 步骤1: 添加管理器组件

在Unity场景中需要添加两个管理器：

1. **添加StereoVideoStreamManager** (MJPEG)
   - 在Hierarchy中创建GameObject，命名为"MJPEG_StreamManager"
   - 添加组件: `VideoStream.StereoVideoStreamManager`
   - 配置参数（与现有配置保持一致）

2. **添加StereoWebRTCStreamManager** (WebRTC) ⭐新增
   - 在Hierarchy中创建GameObject，命名为"WebRTC_StreamManager"
   - 添加组件: `VideoStream.StereoWebRTCStreamManager`
   - 配置参数:
     - **Server Url**: `https://localhost:5000`
     - **Source Type**: `SHARE_MEMORY_STEREO` （或根据后端配置选择）
     - **Shared Memory Name**: `stereo_color_image_shm`
     - **Video Width**: `1280`
     - **Video Height**: `720`
     - **Display Distance**: `2.0`
     - **Display Width**: `3.2`
     - **Display Height**: `1.8`
     - **Enable Debug Log**: `true` (调试时启用)

### 步骤2: 配置UIController

在Canvas或UI GameObject上的UIController组件中：

1. **视频流配置**:
   - **Video Stream Base Url**: `localhost:5000`
   - **Video Stream Type**: 选择 `MJPEG` 或 `WebRTC`
   - **Auto Start Video Stream**: 根据需要勾选

2. 确保UIController能找到两个流管理器（通过FindObjectOfType自动查找）

### 步骤3: Android构建设置（重要）

由于Unity WebRTC包对Android平台有特殊要求：

1. **打开 Build Settings** (File → Build Settings)
2. **切换到 Android 平台**
3. **Player Settings**:
   - **Scripting Backend**: IL2CPP ✅
   - **Target Architectures**:
     - ✅ ARM64 (必须启用)
     - ❌ ARMv7 (必须禁用)
   - **Minimum API Level**: Android 5.0 (API level 21) 或更高

## 使用方法

### 在Unity Editor中测试

1. **启动后端服务器**:
   ```bash
   cd C:\work_project\pico_project\app\server\server
   python vr_driver_server.py
   ```

2. **确认后端配置** (`server/config/config.json`):
   - 确认使用支持WebRTC的配置（如 `1600_plane.json`）
   - 确认 `source_type` 设置正确

3. **在Unity中运行**:
   - 打开 `Assets/Scenes/PicoXr.unity`
   - 点击 Play
   - 在VR UI中输入服务器地址: `localhost:5000`
   - 点击"开启视频流"按钮

4. **查看Console日志**:
   - 确认看到 "WebRTC连接建立成功！"
   - 确认看到 "视频纹理已准备: 1280x720"

### 在PICO设备上测试

1. **构建APK** (确保已完成Android构建设置)
2. **安装到PICO设备**: `adb install -r build_android.apk`
3. **启动应用**
4. **在VR UI中输入服务器IP**: 例如 `192.168.1.100:5000`
5. **点击开启视频流**

### 切换流类型

**方法1: 在Inspector中切换** (推荐开发测试时使用)
- 在Unity Editor中，选择Canvas对象
- 在UIController组件的Inspector面板中
- 修改 `Video Stream Type` 为 `MJPEG` 或 `WebRTC`

**方法2: 运行时切换** (TODO: 可以后续添加UI按钮)
- 当前需要停止流 → 修改配置 → 重新启动流

## 性能对比

| 特性 | MJPEG | WebRTC |
|------|-------|--------|
| 延迟 | 50-100ms | 20-50ms |
| 带宽 | 较高（无自适应） | 较低（自适应码率） |
| CPU占用 | 较低 | 较高 |
| 稳定性 | 高 | 中等（依赖网络） |
| 易用性 | 高 | 中等 |
| 平台兼容性 | 广泛 | Android ARM64限制 |

## 故障排除

### 问题1: WebRTC包无法导入

**症状**: Unity Console显示WebRTC包导入错误

**解决方案**:
1. 确认Unity版本 ≥ 2022.3.x
2. 确认 `Packages/manifest.json` 中包含:
   ```json
   "com.unity.webrtc": "3.0.0-pre.7"
   ```
3. 重启Unity Editor

### 问题2: "未找到 StereoWebRTCStreamManager 组件"

**症状**: Console显示警告信息

**解决方案**:
1. 确认场景中存在包含 `StereoWebRTCStreamManager` 组件的GameObject
2. 确认该GameObject未被禁用
3. 确认WebRTCStreamClient.cs和StereoWebRTCStreamManager.cs已编译无错误

### 问题3: SDP交换失败

**症状**: Console显示 "SDP交换失败: Cannot connect to destination host"

**解决方案**:
1. 确认后端服务器已启动: `netstat -ano | findstr :5000`
2. 确认服务器URL正确（https://而不是http://）
3. 确认后端 `/offer` 端点正常:
   ```bash
   curl -k https://localhost:5000/offer
   ```
4. 检查防火墙设置

### 问题4: 视频纹理未显示

**症状**: 连接成功但看不到视频

**解决方案**:
1. 检查Console是否有 "视频纹理已准备" 日志
2. 确认Display Quad已激活且可见
3. 确认后端实际有视频数据输出
4. 检查DisplayQuad的Material是否正确

### 问题5: Android构建失败

**症状**: 构建时报错 "IL2CPP error" 或 "ARM64 not supported"

**解决方案**:
1. 确认Scripting Backend设置为IL2CPP
2. 确认只启用了ARM64，禁用ARMv7
3. 清理Build文件夹后重新构建
4. 确认NDK和SDK路径正确

### 问题6: ICE连接失败

**症状**: Console显示 "ICE连接状态: Failed"

**解决方案**:
1. 确认网络连通性
2. 如果在本地网络，确认STUN服务器可访问
3. 检查后端WebRTC配置是否正确
4. 对于复杂网络环境，可能需要配置TURN服务器

## 调试技巧

### 启用详细日志

在WebRTCStreamClient和StereoWebRTCStreamManager组件的Inspector中勾选 `Enable Debug Log`。

### 查看WebRTC统计信息

在WebRTCStreamClient.cs中可以添加统计信息收集：
```csharp
peerConnection.GetStats(OnStatsReport);

void OnStatsReport(RTCStatsReport report)
{
    foreach (var stat in report.Stats)
    {
        Debug.Log($"[WebRTC Stats] {stat.Key}: {stat.Value}");
    }
}
```

### 监控后端日志

查看后端服务器输出，确认Offer请求和Answer响应：
```
request_rtc_source: SHARE_MEMORY_STEREO, 1280, 720, {"left": true, "shm": "stereo_color_image_shm"}
Connection state is connected, pc connect num: 1
```

## 下一步优化

1. **添加UI切换按钮** - 允许运行时在MJPEG和WebRTC之间切换
2. **立体渲染改进** - 当前简化使用左眼纹理，可以实现真正的双眼独立渲染
3. **自适应码率** - 根据网络状况动态调整视频质量
4. **性能优化** - 优化纹理更新频率和内存使用
5. **错误恢复** - 实现自动重连和故障恢复机制
6. **TURN服务器支持** - 支持复杂网络环境下的连接

## 相关文件

### Unity端
- `Assets/Scripts/VideoStream/WebRTCStreamClient.cs` - WebRTC客户端核心
- `Assets/Scripts/VideoStream/StereoWebRTCStreamManager.cs` - 双目流管理器
- `Assets/Scripts/UI/UIController.cs` - UI控制器（已更新）
- `Packages/manifest.json` - Unity包配置

### 后端
- `server/server/vr_driver_server.py` - 主服务器
- `server/server/util/rtc_server/RTCOffer.py` - WebRTC Offer处理
- `server/server/util/rtc_server/RTCTrack.py` - 视频轨道实现
- `server/config/config.json` - 服务器配置

## 技术支持

遇到问题请检查:
1. Unity Console日志
2. 后端服务器输出
3. 本文档的"故障排除"部分
4. Unity WebRTC包官方文档: https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/
