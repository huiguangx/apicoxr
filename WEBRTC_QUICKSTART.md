# WebRTC快速上手指南

## 5分钟快速开始

### 前提条件

- ✅ Unity 2022.3.x 或更高版本
- ✅ Python后端服务器已安装依赖
- ✅ WebRTC包已添加到项目 (`com.unity.webrtc": "3.0.0-pre.7"`)

---

## 步骤1: 启动后端服务器 (30秒)

```bash
cd C:\work_project\pico_project\app\server\server
python vr_driver_server.py
```

**预期输出**:
```
服务器配置
============================================================
HTTP 地址: http://0.0.0.0:5000
HTTPS 地址: https://0.0.0.0:5001
流模式: CAMERA
分辨率: 1280x720
帧率: 30 FPS
============================================================

左眼流:
  HTTP:  http://localhost:5000/mjpeg/left
  HTTPS: https://localhost:5001/mjpeg/left

右眼流:
  HTTP:  http://localhost:5000/mjpeg/right
  HTTPS: https://localhost:5001/mjpeg/right

按 Ctrl+C 停止服务器
```

---

## 步骤2: 配置Unity场景 (2分钟)

### 2.1 添加WebRTC流管理器

1. 在Hierarchy中右键 → Create Empty
2. 重命名为 `WebRTC_StreamManager`
3. Add Component → 输入 `StereoWebRTCStreamManager`
4. 配置参数:
   - Server Url: `https://localhost:5000`
   - Source Type: `SHARE_MEMORY_STEREO`
   - Shared Memory Name: `stereo_color_image_shm`
   - Video Width: `1280`
   - Video Height: `720`
   - Enable Debug Log: ✅

### 2.2 配置UIController

1. 选择Canvas对象（或包含UIController的GameObject）
2. 在Inspector中找到UIController组件
3. 设置:
   - Video Stream Base Url: `localhost:5000`
   - Video Stream Type: `WebRTC` ⭐
   - Auto Start Video Stream: ✅（可选）

---

## 步骤3: 运行测试 (1分钟)

### 在Unity Editor中测试

1. 点击Unity的 **Play** 按钮
2. 等待1秒（如果Auto Start启用）或点击VR UI中的"开启视频流"按钮
3. 查看Console，应该看到:
   ```
   [WebRTCStreamClient L] 本地Offer已创建，准备发送到服务器
   [WebRTCStreamClient L] WebRTC连接建立成功！
   [WebRTCStreamClient L] 收到视频轨道: Video
   [WebRTCStreamClient L] 视频纹理已准备: 1280x720
   ```

4. 如果看到视频显示 → ✅ 成功！

---

## 快速切换MJPEG/WebRTC

### 方法1: Inspector切换（开发测试推荐）

1. **停止Unity Play模式**
2. 选择Canvas对象
3. 在UIController组件中修改 `Video Stream Type`
   - `MJPEG` - 使用HTTP MJPEG流
   - `WebRTC` - 使用WebRTC流
4. **重新Play**

### 方法2: 场景配置切换（发布版本）

修改 `Assets/Scenes/PicoXr.unity` 场景文件中UIController的配置:
- 找到 `videoStreamType: 0` (MJPEG) 或 `videoStreamType: 1` (WebRTC)
- 修改数字即可切换

---

## 常见问题快速修复

### ❌ "未找到 StereoWebRTCStreamManager 组件"

**解决**: 确认场景中已添加 `WebRTC_StreamManager` GameObject并挂载了StereoWebRTCStreamManager组件

---

### ❌ "SDP交换失败: Cannot connect"

**检查清单**:
1. ✅ 后端服务器正在运行?
   ```bash
   netstat -ano | findstr :5000
   ```
2. ✅ 服务器URL是 `https://` 而不是 `http://`?
3. ✅ 防火墙未阻止5000端口?

---

### ❌ "连接成功但看不到视频"

1. 确认Console有 "视频纹理已准备" 日志
2. 确认后端有视频源（camera模式需要摄像头）
3. 检查Display Quad是否在相机前方

---

### ❌ Android构建失败

**快速修复**:
1. File → Build Settings → Player Settings
2. Other Settings:
   - Scripting Backend: **IL2CPP** ✅
   - Target Architectures: **ARM64** ✅, **ARMv7** ❌

---

## 性能对比参考

| 场景 | MJPEG | WebRTC | 推荐 |
|------|-------|--------|------|
| 本地测试 | ✅ 简单 | ✅ 低延迟 | WebRTC |
| 局域网 | ✅ 稳定 | ✅ 低延迟 | WebRTC |
| 公网 | ⚠️ 带宽高 | ✅ 自适应 | WebRTC |
| 开发调试 | ✅ 易调试 | ⚠️ 复杂 | MJPEG |

---

## 下一步

✅ 完成基础测试 → 查看 [WEBRTC_SETUP.md](./WEBRTC_SETUP.md) 了解详细配置

✅ 需要构建APK → 确认Android构建设置正确

✅ 遇到问题 → 查看 WEBRTC_SETUP.md 的"故障排除"章节

✅ 优化性能 → 调整视频分辨率、帧率、码率参数

---

## 验证清单

在提交代码或部署前，确认：

- [ ] Unity Console无错误
- [ ] 后端服务器正常运行
- [ ] WebRTC连接成功（Console有 "WebRTC连接建立成功！"）
- [ ] 视频纹理正常显示
- [ ] 延迟可接受（< 100ms）
- [ ] Android构建设置正确（IL2CPP + ARM64）

---

**🎉 恭喜！WebRTC集成完成！**
