using UnityEngine;

/// <summary>
/// 超简单的透明化脚本（修正版）
/// 正确支持 Unlit/Transparent / URP Transparent
/// </summary>
[ExecuteInEditMode]
public class SimpleMakeTransparent : MonoBehaviour
{
    [Header("透明度设置")]
    [Range(0f, 1f)]
    [Tooltip("0 = 完全透明，1 = 完全不透明")]
    public float alpha = 0f;

    [Header("颜色设置")]
    public Color baseColor = Color.white;

    private MeshRenderer meshRenderer;
    private Material material;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    private void OnEnable()
    {
        ApplyTransparency();
    }

    private void OnValidate()
    {
        ApplyTransparency();
    }

    [ContextMenu("应用透明效果")]
    public void ApplyTransparency()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogError("未找到 MeshRenderer 组件！");
            return;
        }

        material = Application.isPlaying
            ? meshRenderer.material
            : meshRenderer.sharedMaterial;

        if (material == null)
        {
            Debug.LogError("未找到材质！");
            return;
        }

        Color c = baseColor;
        c.a = alpha;

        // ✅ 关键修复点
        if (material.HasProperty(BaseColorID))
        {
            material.SetColor(BaseColorID, c);
        }
        else
        {
            // 兼容老 Shader
            material.color = c;
        }

        Debug.Log($"✅ 透明度已应用 alpha={alpha}");
    }
}
