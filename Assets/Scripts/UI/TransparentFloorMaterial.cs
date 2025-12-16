using UnityEngine;

/// <summary>
/// 自动创建透明地板（用于AR透视场景）
/// 使用方法：
/// 1. 在场景中创建空GameObject，命名为 "Floor"
/// 2. 将此脚本挂载到GameObject上
/// 3. 脚本会自动添加 Mesh、Renderer 和 Collider
/// 4. 玩家可以站在透明地板上，不会掉下去
/// </summary>
[ExecuteInEditMode] // 让脚本在编辑模式下也能运行
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
        ApplyTransparentFloor();
    }

    private void OnEnable()
    {
        // 组件启用时也执行一次
        if (Application.isPlaying || meshRenderer == null)
        {
            ApplyTransparentFloor();
        }
    }

    /// <summary>
    /// 应用透明地板效果（可在Inspector中右键调用）
    /// </summary>
    [ContextMenu("应用透明地板效果")]
    public void ApplyTransparentFloor()
    {
        // 自动添加必要的组件
        SetupFloorComponents();

        // 创建透明材质
        CreateTransparentMaterial();

        Debug.Log($"✅ 地板已设置为透明（Alpha={floorAlpha}），碰撞检测已启用");
    }

    /// <summary>
    /// 自动设置地板所需的所有组件
    /// </summary>
    private void SetupFloorComponents()
    {
        // 1. 确保有 MeshFilter（用于显示地板）
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
            // 使用Unity内置的Plane mesh
            GameObject tempPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            meshFilter.mesh = tempPlane.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempPlane);
            Debug.Log("✅ 已自动添加 MeshFilter（Plane）");
        }

        // 2. 确保有 MeshRenderer（用于渲染）
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            Debug.Log("✅ 已自动添加 MeshRenderer");
        }

        // 3. 确保有 Collider（用于碰撞检测，让玩家能站在上面）
        Collider existingCollider = GetComponent<Collider>();
        if (existingCollider == null)
        {
            // 添加 MeshCollider（与地板形状完全匹配）
            MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.convex = false; // Plane不需要凸面
            Debug.Log("✅ 已自动添加 MeshCollider（玩家现在可以站在地板上）");
        }
    }

    private void CreateTransparentMaterial()
    {
        // 使用Unity内置的透明Shader（VR场景推荐Unlit以提升性能）
        Shader transparentShader = Shader.Find("Unlit/Transparent");
        bool isStandardShader = false;

        if (transparentShader == null)
        {
            Debug.LogWarning("⚠️ 未找到 Unlit/Transparent，使用 Standard Shader");
            transparentShader = Shader.Find("Standard");
            isStandardShader = true;
        }

        if (transparentShader == null)
        {
            Debug.LogError("❌ 未找到任何可用的透明Shader！");
            return;
        }

        floorMaterial = new Material(transparentShader);

        // 设置颜色和透明度
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

        // 根据Shader类型设置透明模式
        if (isStandardShader)
        {
            // Standard Shader的透明模式设置
            floorMaterial.SetOverrideTag("RenderType", "Transparent");
            floorMaterial.SetFloat("_Mode", 3); // Transparent mode
            floorMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            floorMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            floorMaterial.SetInt("_ZWrite", 0);
            floorMaterial.DisableKeyword("_ALPHATEST_ON");
            floorMaterial.EnableKeyword("_ALPHABLEND_ON");
            floorMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            floorMaterial.renderQueue = 3000;
        }
        else
        {
            // Unlit/Transparent 已经是透明的，只需要设置render queue
            floorMaterial.renderQueue = 3000;
        }

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

    /// <summary>
    /// Inspector中修改参数时立即更新
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying && floorMaterial != null)
        {
            Color finalColor = showGridLines ? gridLineColor : Color.white;
            finalColor.a = showGridLines ? gridLineAlpha : floorAlpha;
            floorMaterial.color = finalColor;
        }
    }

    /// <summary>
    /// 清理材质防止内存泄漏
    /// </summary>
    private void OnDestroy()
    {
        if (floorMaterial != null)
        {
            Destroy(floorMaterial);
        }
    }
}
