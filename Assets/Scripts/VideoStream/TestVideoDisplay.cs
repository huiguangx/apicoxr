using UnityEngine;

/// <summary>
/// 简单测试脚本：显示一个纯色Quad，验证显示系统是否工作
/// 用法：挂载到空GameObject上，运行后应该在摄像机前看到一个红色方块
/// </summary>
public class TestVideoDisplay : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("=== 测试视频显示系统 ===");

        // 查找主相机
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("❌ 找不到主相机！");
            return;
        }
        Debug.Log($"✅ 找到主相机: {mainCamera.name}");

        // 创建测试Quad
        GameObject testQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        testQuad.name = "TestQuad";
        Debug.Log("✅ 创建了Quad");

        // 作为相机子对象
        testQuad.transform.SetParent(mainCamera.transform, false);
        testQuad.transform.localPosition = new Vector3(0, 0, 0.5f); // 相机前0.5米
        testQuad.transform.localRotation = Quaternion.identity;
        testQuad.transform.localScale = Vector3.one * 0.3f; // 0.3米大小
        Debug.Log("✅ Quad已设置为相机子对象");

        // 移除碰撞体
        Collider collider = testQuad.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        // 创建红色材质
        MeshRenderer renderer = testQuad.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            Debug.LogError("❌ Quad没有MeshRenderer！");
            return;
        }

        Material testMaterial = new Material(Shader.Find("Unlit/Color"));
        testMaterial.color = Color.red;
        renderer.material = testMaterial;
        Debug.Log("✅ 应用了红色材质");

        Debug.Log("=== 如果看到红色方块，说明显示系统正常 ===");
    }
}
