using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// 手柄位置校准工具
/// 用于修复虚拟手柄和真实手柄位置不一致的问题
/// </summary>
public class ControllerPositionCalibrator : MonoBehaviour
{
    [Header("手柄对象")]
    [Tooltip("左手控制器（拖入左手柄的 GameObject）")]
    public GameObject leftController;

    [Tooltip("右手控制器（拖入右手柄的 GameObject）")]
    public GameObject rightController;

    [Header("位置偏移调整")]
    [Tooltip("前后偏移（Z轴）- 正值向前，负值向后")]
    public float forwardOffset = 0.05f;

    [Tooltip("左右偏移（X轴）- 正值向右，负值向左")]
    public float horizontalOffset = 0f;

    [Tooltip("上下偏移（Y轴）- 正值向上，负值向下")]
    public float verticalOffset = 0f;

    [Header("旋转偏移调整")]
    [Tooltip("俯仰角偏移（X轴旋转）")]
    public float pitchOffset = 0f;

    [Tooltip("偏航角偏移（Y轴旋转）")]
    public float yawOffset = 0f;

    [Tooltip("翻滚角偏移（Z轴旋转）")]
    public float rollOffset = 0f;

    [Header("实时调整")]
    [Tooltip("启用运行时调整（可以在运行时修改参数）")]
    public bool enableRuntimeAdjustment = true;

    [Header("调试信息")]
    public bool showDebugInfo = true;

    private Transform leftHandModel;
    private Transform rightHandModel;

    void Start()
    {
        Debug.Log("=== 手柄位置校准工具启动 ===");

        // 自动查找手柄
        if (leftController == null || rightController == null)
        {
            AutoFindControllers();
        }

        // 查找手柄模型
        FindControllerModels();

        // 应用初始偏移
        ApplyOffsets();

        Debug.Log("手柄位置校准完成");
        Debug.Log($"偏移设置: 前后={forwardOffset}, 左右={horizontalOffset}, 上下={verticalOffset}");
    }

    void AutoFindControllers()
    {
        Debug.Log("自动查找手柄对象...");

        // 查找 XR Controller
        var controllers = FindObjectsOfType<XRController>();
        foreach (var controller in controllers)
        {
            if (controller.name.Contains("Left"))
            {
                leftController = controller.gameObject;
                Debug.Log($"✓ 找到左手柄: {controller.name}");
            }
            else if (controller.name.Contains("Right"))
            {
                rightController = controller.gameObject;
                Debug.Log($"✓ 找到右手柄: {controller.name}");
            }
        }

        // 如果没找到，尝试通过 ActionBasedController 查找
        if (leftController == null || rightController == null)
        {
            var actionControllers = FindObjectsOfType<ActionBasedController>();
            foreach (var controller in actionControllers)
            {
                if (controller.name.Contains("Left"))
                {
                    leftController = controller.gameObject;
                    Debug.Log($"✓ 找到左手柄: {controller.name}");
                }
                else if (controller.name.Contains("Right"))
                {
                    rightController = controller.gameObject;
                    Debug.Log($"✓ 找到右手柄: {controller.name}");
                }
            }
        }
    }

    void FindControllerModels()
    {
        // 查找手柄的视觉模型（通常是子对象）
        if (leftController != null)
        {
            // 尝试查找包含 "Model" 或 "Visual" 的子对象
            foreach (Transform child in leftController.transform)
            {
                if (child.name.Contains("Model") || child.name.Contains("Visual") || child.GetComponent<Renderer>() != null)
                {
                    leftHandModel = child;
                    Debug.Log($"✓ 找到左手柄模型: {child.name}");
                    break;
                }
            }

            // 如果没找到，使用第一个子对象
            if (leftHandModel == null && leftController.transform.childCount > 0)
            {
                leftHandModel = leftController.transform.GetChild(0);
                Debug.Log($"使用左手柄第一个子对象: {leftHandModel.name}");
            }
        }

        if (rightController != null)
        {
            foreach (Transform child in rightController.transform)
            {
                if (child.name.Contains("Model") || child.name.Contains("Visual") || child.GetComponent<Renderer>() != null)
                {
                    rightHandModel = child;
                    Debug.Log($"✓ 找到右手柄模型: {child.name}");
                    break;
                }
            }

            if (rightHandModel == null && rightController.transform.childCount > 0)
            {
                rightHandModel = rightController.transform.GetChild(0);
                Debug.Log($"使用右手柄第一个子对象: {rightHandModel.name}");
            }
        }
    }

    void ApplyOffsets()
    {
        Vector3 positionOffset = new Vector3(horizontalOffset, verticalOffset, forwardOffset);
        Vector3 rotationOffset = new Vector3(pitchOffset, yawOffset, rollOffset);

        if (leftHandModel != null)
        {
            leftHandModel.localPosition = positionOffset;
            leftHandModel.localRotation = Quaternion.Euler(rotationOffset);
            Debug.Log($"左手柄偏移已应用: {positionOffset}");
        }

        if (rightHandModel != null)
        {
            rightHandModel.localPosition = positionOffset;
            rightHandModel.localRotation = Quaternion.Euler(rotationOffset);
            Debug.Log($"右手柄偏移已应用: {positionOffset}");
        }
    }

    void Update()
    {
        if (enableRuntimeAdjustment)
        {
            ApplyOffsets();
        }

        // 键盘快捷键调整
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            // Ctrl + 方向键：调整前后位置
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                forwardOffset += 0.01f;
                Debug.Log($"前后偏移: {forwardOffset:F3}");
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                forwardOffset -= 0.01f;
                Debug.Log($"前后偏移: {forwardOffset:F3}");
            }

            // Ctrl + 左右键：调整左右位置
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                horizontalOffset -= 0.01f;
                Debug.Log($"左右偏移: {horizontalOffset:F3}");
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                horizontalOffset += 0.01f;
                Debug.Log($"左右偏移: {horizontalOffset:F3}");
            }

            // Ctrl + Page Up/Down：调整上下位置
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                verticalOffset += 0.01f;
                Debug.Log($"上下偏移: {verticalOffset:F3}");
            }
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                verticalOffset -= 0.01f;
                Debug.Log($"上下偏移: {verticalOffset:F3}");
            }

            // Ctrl + R：重置所有偏移
            if (Input.GetKeyDown(KeyCode.R))
            {
                forwardOffset = 0;
                horizontalOffset = 0;
                verticalOffset = 0;
                pitchOffset = 0;
                yawOffset = 0;
                rollOffset = 0;
                Debug.Log("所有偏移已重置");
            }
        }

        // 显示调试信息
        if (showDebugInfo && Input.GetKeyDown(KeyCode.I))
        {
            PrintDebugInfo();
        }
    }

    void PrintDebugInfo()
    {
        Debug.Log("=== 手柄位置调试信息 ===");
        Debug.Log($"位置偏移: X={horizontalOffset:F3}, Y={verticalOffset:F3}, Z={forwardOffset:F3}");
        Debug.Log($"旋转偏移: Pitch={pitchOffset:F1}°, Yaw={yawOffset:F1}°, Roll={rollOffset:F1}°");

        if (leftController != null)
        {
            Debug.Log($"左手柄位置: {leftController.transform.position}");
            Debug.Log($"左手柄旋转: {leftController.transform.rotation.eulerAngles}");
        }

        if (rightController != null)
        {
            Debug.Log($"右手柄位置: {rightController.transform.position}");
            Debug.Log($"右手柄旋转: {rightController.transform.rotation.eulerAngles}");
        }
    }

    void OnGUI()
    {
        if (showDebugInfo && (leftController != null || rightController != null))
        {
            GUILayout.BeginArea(new Rect(10, 10, 400, 300));
            GUILayout.Box("手柄位置校准工具");

            GUILayout.Label($"前后偏移 (Z): {forwardOffset:F3}m");
            GUILayout.Label($"左右偏移 (X): {horizontalOffset:F3}m");
            GUILayout.Label($"上下偏移 (Y): {verticalOffset:F3}m");

            GUILayout.Space(10);
            GUILayout.Label("快捷键:");
            GUILayout.Label("Ctrl + ↑↓ : 前后调整");
            GUILayout.Label("Ctrl + ←→ : 左右调整");
            GUILayout.Label("Ctrl + PgUp/PgDn : 上下调整");
            GUILayout.Label("Ctrl + R : 重置");
            GUILayout.Label("I : 打印调试信息");

            GUILayout.EndArea();
        }
    }
}
