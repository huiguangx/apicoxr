using UnityEngine;

/// <summary>
/// 手腕旋转映射工具
/// 将VR手柄旋转转换为机器人手腕旋转
/// </summary>
public class WristRotationMapper : MonoBehaviour
{
    [Header("旋转映射模式")]
    [Tooltip("选择旋转映射方式")]
    public RotationMappingMode mappingMode = RotationMappingMode.DirectWithOffset;

    [Header("旋转偏移设置")]
    [Tooltip("手腕旋转偏移（欧拉角）")]
    public Vector3 wristRotationOffset = new Vector3(0, 0, 0);

    [Tooltip("前臂旋转偏移（欧拉角）")]
    public Vector3 forearmRotationOffset = new Vector3(0, 0, 0);

    [Header("旋转轴心调整")]
    [Tooltip("手腕相对于手柄的位置偏移")]
    public Vector3 wristPivotOffset = new Vector3(0, 0, 0.1f);

    [Header("旋转限制")]
    [Tooltip("启用手腕旋转限制")]
    public bool enableRotationLimits = false;

    [Tooltip("手腕俯仰角限制（X轴）")]
    public Vector2 pitchLimit = new Vector2(-90, 90);

    [Tooltip("手腕偏航角限制（Y轴）")]
    public Vector2 yawLimit = new Vector2(-180, 180);

    [Tooltip("手腕翻滚角限制（Z轴）")]
    public Vector2 rollLimit = new Vector2(-180, 180);

    [Header("调试")]
    public bool showDebugInfo = true;

    public enum RotationMappingMode
    {
        Direct,                 // 直接映射（原始）
        DirectWithOffset,       // 直接映射 + 偏移
        ForearmToWrist,         // 前臂旋转转换为手腕旋转
        CustomMapping           // 自定义映射
    }

    /// <summary>
    /// 将VR手柄旋转转换为机器人手腕旋转
    /// </summary>
    public Quaternion MapControllerToWrist(Quaternion controllerRotation)
    {
        switch (mappingMode)
        {
            case RotationMappingMode.Direct:
                return controllerRotation;

            case RotationMappingMode.DirectWithOffset:
                return ApplyRotationOffset(controllerRotation, wristRotationOffset);

            case RotationMappingMode.ForearmToWrist:
                return ConvertForearmToWrist(controllerRotation);

            case RotationMappingMode.CustomMapping:
                return CustomRotationMapping(controllerRotation);

            default:
                return controllerRotation;
        }
    }

    /// <summary>
    /// 应用旋转偏移
    /// </summary>
    private Quaternion ApplyRotationOffset(Quaternion rotation, Vector3 offset)
    {
        Quaternion offsetQuat = Quaternion.Euler(offset);
        return rotation * offsetQuat;
    }

    /// <summary>
    /// 将前臂旋转转换为手腕旋转
    /// 核心：手柄绕前臂旋转 → 手腕自身旋转
    /// </summary>
    private Quaternion ConvertForearmToWrist(Quaternion forearmRotation)
    {
        // 1. 应用前臂偏移
        Quaternion adjustedForearm = ApplyRotationOffset(forearmRotation, forearmRotationOffset);

        // 2. 提取欧拉角
        Vector3 eulerAngles = adjustedForearm.eulerAngles;

        // 3. 转换：将前臂的Roll转换为手腕的Pitch/Yaw
        // 这里根据你的机器人具体结构调整
        Vector3 wristEuler = new Vector3(
            eulerAngles.z,  // 前臂的Roll → 手腕的Pitch
            eulerAngles.y,  // 前臂的Yaw → 手腕的Yaw
            eulerAngles.x   // 前臂的Pitch → 手腕的Roll
        );

        // 4. 应用手腕偏移
        wristEuler += wristRotationOffset;

        // 5. 应用旋转限制
        if (enableRotationLimits)
        {
            wristEuler = ApplyRotationLimits(wristEuler);
        }

        return Quaternion.Euler(wristEuler);
    }

    /// <summary>
    /// 自定义旋转映射（根据你的需求修改）
    /// </summary>
    private Quaternion CustomRotationMapping(Quaternion controllerRotation)
    {
        // 示例：只使用手柄的部分旋转分量
        Vector3 euler = controllerRotation.eulerAngles;

        // 你可以在这里自定义映射逻辑
        // 例如：只使用Y轴旋转（偏航）
        // Vector3 customEuler = new Vector3(0, euler.y, 0);

        // 或者：反转某个轴
        // euler.x = -euler.x;

        return Quaternion.Euler(euler + wristRotationOffset);
    }

    /// <summary>
    /// 应用旋转角度限制
    /// </summary>
    private Vector3 ApplyRotationLimits(Vector3 euler)
    {
        // 将角度标准化到 -180~180
        euler.x = NormalizeAngle(euler.x);
        euler.y = NormalizeAngle(euler.y);
        euler.z = NormalizeAngle(euler.z);

        // 限制范围
        euler.x = Mathf.Clamp(euler.x, pitchLimit.x, pitchLimit.y);
        euler.y = Mathf.Clamp(euler.y, yawLimit.x, yawLimit.y);
        euler.z = Mathf.Clamp(euler.z, rollLimit.x, rollLimit.y);

        return euler;
    }

    /// <summary>
    /// 标准化角度到 -180~180 范围
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }

    /// <summary>
    /// 测试函数：显示映射前后的旋转
    /// </summary>
    public void TestMapping(Quaternion inputRotation)
    {
        Quaternion outputRotation = MapControllerToWrist(inputRotation);

        Debug.Log("=== 旋转映射测试 ===");
        Debug.Log($"输入旋转（手柄）: {inputRotation.eulerAngles}");
        Debug.Log($"输出旋转（手腕）: {outputRotation.eulerAngles}");
        Debug.Log($"映射模式: {mappingMode}");
    }

    void Update()
    {
        // 调试快捷键
        if (showDebugInfo)
        {
            // Ctrl + Shift + M: 切换映射模式
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.M))
            {
                mappingMode = (RotationMappingMode)(((int)mappingMode + 1) % 4);
                Debug.Log($"切换映射模式: {mappingMode}");
            }

            // Ctrl + Shift + L: 切换旋转限制
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.L))
            {
                enableRotationLimits = !enableRotationLimits;
                Debug.Log($"旋转限制: {(enableRotationLimits ? "开启" : "关闭")}");
            }
        }
    }

    void OnGUI()
    {
        if (showDebugInfo)
        {
            GUILayout.BeginArea(new Rect(10, 320, 400, 200));
            GUILayout.Box("手腕旋转映射");

            GUILayout.Label($"映射模式: {mappingMode}");
            GUILayout.Label($"旋转限制: {(enableRotationLimits ? "开启" : "关闭")}");
            GUILayout.Label($"手腕偏移: ({wristRotationOffset.x:F1}, {wristRotationOffset.y:F1}, {wristRotationOffset.z:F1})");

            GUILayout.Space(10);
            GUILayout.Label("快捷键:");
            GUILayout.Label("Ctrl+Shift+M : 切换映射模式");
            GUILayout.Label("Ctrl+Shift+L : 切换旋转限制");

            GUILayout.EndArea();
        }
    }
}
