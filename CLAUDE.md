# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity VR application for PICO headsets that:
- Streams stereoscopic MJPEG video from a remote server for AR/passthrough experiences
- Tracks and transmits real-time VR device pose data (head and controllers) to a server
- Provides an immersive UI for configuration and control
- Supports video see-through/passthrough functionality

**Target Platform:** Android (PICO VR headsets)
**Unity Version:** 2022.3.x or later
**SDK:** PICO Unity Integration SDK 3.3.2

## Build Commands

### Building the Project

The project builds APK files for Android deployment to PICO devices. Build through Unity Editor:

```
File → Build Settings → Android → Build
```

Or use Unity command line:
```bash
# Build Android APK
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -quit -batchmode -projectPath "C:\work_project\pico_project\app\apicoxr" -buildTarget Android -executeMethod BuildScript.BuildAPK
```

The output APK will be generated at: `build_android.apk`

### Running in Unity Editor

1. Open the project in Unity Editor
2. Load scene: `Assets/Scenes/PicoXr.unity`
3. Click Play - some VR features may work in editor with XR Device Simulator

### Deploying to Device

```bash
# Install APK via ADB
adb install -r build_android.apk

# View Unity logs
adb logcat Unity:I *:S
```

## Architecture

### Core Systems

**1. VR Data Tracking (`Assets/Scripts/DataTracking/`)**
- `DataTracking.cs` - Main tracking system that collects head + controller pose/velocity/button data every frame
- `SendVRData.cs` - Data structures for serializing VR data to JSON
- `HapticMessageReceiver.cs` - Receives haptic feedback commands from server
- Sends data via HTTPS POST to `https://<serverBaseUrl>/poseData`

**2. Video Streaming (`Assets/Scripts/VideoStream/`)**
- `StereoVideoStreamManager.cs` - Manages dual-eye MJPEG video streams for AR passthrough
- `MjpegStreamHandler.cs` - Custom UnityWebRequest handler that parses MJPEG boundary markers and extracts JPEG frames
- Creates an immersive quad display parented to camera for first-person AR view
- Left/right eye streams fetched from `http://<videoStreamBaseUrl>/left` and `/right`

**3. UI System (`Assets/Scripts/UI/`)**
- `UIController.cs` - Procedurally generates all UI via code (no prefabs)
- Creates WorldSpace Canvas with XR ray interaction support
- Configurable server URLs (pose data + video stream)
- Transparent video see-through toggle
- Video stream start/stop controls

**4. Utilities (`Assets/Scripts/Utils/`)**
- `WristRotationMapper.cs` - Maps controller rotation to robot wrist rotation (coordinate system conversion)
- `ControllerPositionCalibrator.cs` - Calibration utilities
- `VRLogDisplay.cs` - On-screen debug log viewer for VR

### Key Coordinate System Details

The app converts from Unity's **left-handed coordinate system** to a **right-handed system** for the server:

```csharp
// In DataTracking.cs
Vector3 ConvertVector3(v) => new Vector3(v.x, v.y, -v.z)
Quaternion ConvertQuaternion(q) => new Quaternion(-q.x, -q.y, q.z, q.w)
```

### Data Flow

```
[PICO Headset & Controllers]
          ↓
   [XR Input System]
          ↓
   [DataTracking.cs] ← Collects pose + button data every frame
          ↓
   [JSON Serialization]
          ↓
   [HTTPS POST] → Server (https://<serverBaseUrl>/poseData)

[Video Server: http://<videoStreamBaseUrl>/left & /right]
          ↓
   [MjpegStreamHandler] ← Parses MJPEG stream
          ↓
   [StereoVideoStreamManager] ← Updates textures
          ↓
   [Custom Shader] → Renders to left/right eye
```

## Important Files

### Scripts
- `Assets/Scripts/DataTracking/DataTracking.cs` - Primary VR tracking and network transmission
- `Assets/Scripts/VideoStream/StereoVideoStreamManager.cs` - Video streaming manager
- `Assets/Scripts/VideoStream/MjpegStreamHandler.cs` - MJPEG stream parser
- `Assets/Scripts/UI/UIController.cs` - Complete UI system (WorldSpace Canvas)
- `Assets/Scripts/DataTracking/SendVRData.cs` - VR data model (JSON serialization)

### Shaders
- `Assets/Shaders/StereoVideoShader.shader` - Custom shader for stereo video rendering
  - Must be added to: `Project Settings → Graphics → Always Included Shaders`

### Scenes
- `Assets/Scenes/PicoXr.unity` - Main scene (attach scripts to appropriate GameObjects)

### Configuration
- `ProjectSettings/ProjectSettings.asset` - Android build settings, package name, SDK versions
- `Packages/manifest.json` - Unity package dependencies including XR Interaction Toolkit and PICO SDK

## Development Workflow

### Adding New Features

1. **VR Tracking Modifications:**
   - Edit `DataTracking.cs` for new sensor data
   - Update `SendVRData.cs` data model
   - Server must match JSON structure

2. **Video Stream Changes:**
   - Modify `StereoVideoStreamManager.cs` for display settings
   - Update `MjpegStreamHandler.cs` for protocol changes
   - Shader edits in `StereoVideoShader.shader`

3. **UI Additions:**
   - All UI is code-generated in `UIController.cs`
   - Use `AddButton()` method for new buttons
   - Follow existing patterns for input fields

### Important Patterns

**PlayerPrefs for Persistence:**
```csharp
// Server URLs are saved to PlayerPrefs
PlayerPrefs.SetString("ServerBaseUrl", "192.168.1.100:5000");
PlayerPrefs.SetString("VideoStreamBaseUrl", "localhost:3000");
```

**Coroutines for Network:**
```csharp
// All HTTP requests use UnityWebRequest in coroutines
StartCoroutine(PostDataToServer(jsonData));
```

**Thread-Safe Frame Updates:**
```csharp
// MjpegStreamHandler uses lock(frameLock) for cross-thread texture updates
lock (frameLock) {
    leftFrameBuffer = frameData;
    leftFrameReady = true;
}
```

## Testing

### In Unity Editor
- Test UI interactions with mouse
- Limited XR functionality without headset
- Use Unity XR Device Simulator for basic testing

### On PICO Device
1. Build APK
2. Install via ADB: `adb install -r build_android.apk`
3. Monitor logs: `adb logcat Unity:I *:S`
4. Check network connectivity to servers

### Common Issues

**Video not displaying:**
- Verify shader is in "Always Included Shaders"
- Check video URLs are correct (http, not https)
- Ensure server is streaming MJPEG with boundary marker `--boundarydonotcross`

**Pose data not sending:**
- Verify HTTPS server URL
- Check `CustomCertificateHandler` accepts server certificate
- Enable debug logs: Set `enableDebugLog = true` in DataTracking

**UI not responding to controller ray:**
- Ensure `TrackedDeviceGraphicRaycaster` is on Canvas
- Check XR Interaction Toolkit is properly configured
- Verify EventSystem exists in scene

## Network Configuration

### Server Endpoints

**Pose Data Server:**
- URL: `https://<serverBaseUrl>/poseData`
- Method: POST
- Content-Type: application/json
- Body: SendVRData JSON structure

**Video Stream Server:**
- Left Eye: `http://<videoStreamBaseUrl>/left`
- Right Eye: `http://<videoStreamBaseUrl>/right`
- Protocol: MJPEG over HTTP
- Boundary marker: `--boundarydonotcross`

### Security Notes

The project uses `CustomCertificateHandler` that accepts all certificates (returns true). This is for development only and should be replaced with proper certificate validation in production.

## XR Configuration

### Input System
- Uses Unity's new Input System
- XR controller bindings defined in project
- Button mappings:
  - Index 0: Trigger
  - Index 1: Grip
  - Index 2: Thumbstick Click
  - Index 4: X/A Button
  - Index 5: Y/B Button
  - Axes[2-3]: Thumbstick X/Y

### PICO-Specific Features
- Video see-through: `PXR_Manager.EnableVideoSeeThrough`
- Requires PICO Unity Integration SDK (local package reference)

## Performance Considerations

- Video streaming targets 30 FPS (configurable)
- Pose data sent every frame (90 Hz on PICO headsets)
- MJPEG buffer size: 1MB (expandable if needed)
- Texture format: RGB24 (can use RGB565 for better performance)

## Code Style

- Follow existing C# naming conventions
- Use XML doc comments for public methods/classes
- Organize code into regions (#region/#endregion)
- Enable debug logs via Inspector toggles, not hardcoded
