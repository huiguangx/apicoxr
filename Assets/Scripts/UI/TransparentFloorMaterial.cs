using UnityEngine;

/// <summary>
/// 自动将地板设置为透明材质（用于AR透视场景）
/// 使用方法：将此脚本挂载到地板GameObject上
/// </summary>
public class TransparentFloorMaterial : MonoBehaviour
{
    [Header("透明度设置")]
    [Range(0f, 1f)]
    [Tooltip("地板透明度（0=完全透明，1=完全不透明）")]
    public float floorAlpha = 0f;

    [Header("网格线设置（可选）")]
    [Tooltip("是否显示网格线用于调试")]
    public bool showGridLines = false;

    [Range(0f, 1f)]
    [Tooltip("网格线透明度")]
    public float gridLineAlpha = 0.3f;

    [Tooltip("网格线颜色")]
    public Color gridLineColor = Color.white;

    private Material floorMaterial;
    private MeshRenderer meshRenderer;

    private void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            Debug.LogError("❌ 未找到 MeshRenderer 组件！");
            return;
        }

        // 创建透明材质
        CreateTransparentMaterial();

        Debug.Log($"✅ 地板已设置为透明（Alpha={floorAlpha}）");
    }

    private void CreateTransparentMaterial()
    {
        // 使用Unity内置的透明Shader
        Shader transparentShader = Shader.Find("Unlit/Transparent");

        if (transparentShader == null)
        {
            Debug.LogWarning("⚠️ 未找到 Unlit/Transparent，使用 Standard Shader");
            transparentShader = Shader.Find("Standard");
        }

        floorMaterial = new Material(transparentShader);

        if (showGridLines)
        {
            // 如果需要网格线，使用白色半透明
            Color finalColor = gridLineColor;
            finalColor.a = gridLineAlpha;
            floorMaterial.color = finalColor;
        }
        else
        {
            // 完全透明
            Color transparent = Color.white;
            transparent.a = floorAlpha;
            floorMaterial.color = transparent;
        }

        // 设置渲染模式为透明
        floorMaterial.SetFloat("_Mode", 3); // Transparent mode
        floorMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        floorMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        floorMaterial.SetInt("_ZWrite", 0);
        floorMaterial.DisableKeyword("_ALPHATEST_ON");
        floorMaterial.EnableKeyword("_ALPHABLEND_ON");
        floorMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        floorMaterial.renderQueue = 3000;

        meshRenderer.material = floorMaterial;
    }

    private void Update()
    {
        // 实时调整透明度（Inspector中修改时生效）
        if (floorMaterial != null)
        {
            Color currentColor = floorMaterial.color;
            float targetAlpha = showGridLines ? gridLineAlpha : floorAlpha;

            if (Mathf.Abs(currentColor.a - targetAlpha) > 0.01f)
            {
                currentColor.a = targetAlpha;
                floorMaterial.color = currentColor;
            }

            if (showGridLines)
            {
                floorMaterial.color = new Color(gridLineColor.r, gridLineColor.g, gridLineColor.b, gridLineAlpha);
            }
        }
    }

    /// <summary>
    /// 完全隐藏地板（禁用渲染器）
    /// </summary>
    [ContextMenu("完全隐藏地板")]
    public void HideFloorCompletely()
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
            Debug.Log("✅ 地板渲染已完全禁用（碰撞仍保留）");
        }
    }

    /// <summary>
    /// 显示地板
    /// </summary>
    [ContextMenu("显示地板")]
    public void ShowFloor()
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = true;
            Debug.Log("✅ 地板渲染已启用");
        }
    }
}
