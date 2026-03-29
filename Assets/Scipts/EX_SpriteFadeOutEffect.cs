using System.Collections;
using UnityEngine;

/// <summary>
/// 控制 Sprite 显示并淡出的效果（如升级、获得道具等）
/// 附加到带有 SpriteRenderer 组件的 GameObject 上
/// </summary>
public class EX_SpriteFadeOutEffect : MonoBehaviour
{
    [Header("组件引用")]
    [SerializeField] private SpriteRenderer spriteRenderer; // 拖拽或自动获取

    [Header("效果参数")]
    [SerializeField] private float fadeOutDuration = 1f;     // 淡出所需时间（秒）
    [SerializeField] private bool disableOnComplete = true;  // 淡出完成后是否禁用 GameObject

    private bool isPlaying = false; // 防止效果重入

    private void Awake()
    {
        // 如果没有手动指定 SpriteRenderer，尝试获取当前物体上的组件
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // 初始确保 Sprite 不可见（若希望效果开始时突然出现，此处应设为透明）
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = 0f;
            spriteRenderer.color = color;
        }
    }

    /// <summary>
    /// 公开调用接口：播放一次“显示 → 淡出”效果
    /// </summary>
    public void PlayEffect()
    {
        if (isPlaying)
        {
            Debug.LogWarning("效果正在进行中，请等待完成后再调用");
            return;
        }

        if (spriteRenderer == null)
        {
            Debug.LogError("SpriteRenderer 组件缺失，无法播放效果");
            return;
        }

        // 停止当前可能正在运行的协程，保证新效果正常开始
        StopAllCoroutines();
        StartCoroutine(FadeOutRoutine());
    }

    /// <summary>
    /// 可选：允许在运行时动态修改淡出时长
    /// </summary>
    public void SetFadeOutDuration(float duration)
    {
        fadeOutDuration = Mathf.Max(0.01f, duration);
    }

    private IEnumerator FadeOutRoutine()
    {
        isPlaying = true;

        // 1. 立即完全显示（不透明度 1）
        Color startColor = spriteRenderer.color;
        startColor.a = 1f;
        spriteRenderer.color = startColor;

        // 确保 GameObject 激活（如果之前被禁用）
        gameObject.SetActive(true);

        // 2. 记录起始颜色和结束颜色（透明）
        Color endColor = startColor;
        endColor.a = 0f;

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration; // 0→1
            spriteRenderer.color = Color.Lerp(startColor, endColor, t);
            yield return null;
        }

        // 确保最终完全透明
        spriteRenderer.color = endColor;

        // 3. 效果完成后的处理
        if (disableOnComplete)
            gameObject.SetActive(false);
        // 如果不想禁用但保持透明，可以保留 GameObject 活跃，Sprite 透明

        isPlaying = false;
    }
}