using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("目标设置")]
    public Transform target; // 玩家对象

    [Header("跟随平滑度")]
    public float smoothSpeed = 5f; // 越大跟得越紧，越小越有延迟感（建议 5-10）
    
    [Header("地图边界 (世界坐标)")]
    // 这里需要填入你地图的实际边界范围
    // 例如：如果地图是从 -15 到 15，这里就填 -15 和 15
    public float mapMinX = -15f;
    public float mapMaxX = 15f;
    public float mapMinY = -15f;
    public float mapMaxY = 15f;

    [Header("调试 (可选)")]
    public bool drawGizmos = true;

    private Vector3 desiredPosition;
    private float cameraHeight;
    private float cameraWidth;

    void Start()
    {
        if (target == null)
        {
            // 自动查找标签为 "Player" 的对象
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                target = playerObj.transform;
            else
                Debug.LogError("未找到玩家对象！请设置 Target 或给玩家添加 'Player' 标签。");
        }

        // 计算摄像机自身的半高和半宽
        // orthographicSize 是摄像机高度的一半
        cameraHeight = Camera.main.orthographicSize;
        // 宽度 = 高度 * 宽高比
        cameraWidth = cameraHeight * Camera.main.aspect;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 计算理想位置：保持摄像机与玩家的相对位置（通常是中心对齐，所以偏移量为0）
        // 如果你想让摄像机稍微偏上一点，可以修改 new Vector3(target.position.x, target.position.y, ...)
        desiredPosition = new Vector3(target.position.x, target.position.y, transform.position.z);

        // 2. 平滑插值移动 (Lerp)
        // 这样摄像机不会瞬间跳过去，而是有一种“追赶”的手感
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // 3. 核心逻辑：限制边界
        // 摄像机不能超出地图，意味着：
        // 摄像机的左边缘 (pos.x - width/2) 不能小于 地图左边界 (mapMinX)
        // 摄像机的右边缘 (pos.x + width/2) 不能大于 地图右边界 (mapMaxX)
        // Y轴同理
        
        float minX = mapMinX + cameraWidth;
        float maxX = mapMaxX - cameraWidth;
        float minY = mapMinY + cameraHeight;
        float maxY = mapMaxY - cameraHeight;

        // 如果地图比摄像机视野小，防止 minX > maxX 导致出错
        // 这种情况下，摄像机应该固定在地图中心（或者不做限制，视需求而定）
        // 这里我们做一个保护：如果计算出的范围无效，就强制设为地图中心
        if (minX > maxX) { minX = maxX = (mapMinX + mapMaxX) / 2f; }
        if (minY > maxY) { minY = maxY = (mapMinY + mapMaxY) / 2f; }

        smoothedPosition.x = Mathf.Clamp(smoothedPosition.x, minX, maxX);
        smoothedPosition.y = Mathf.Clamp(smoothedPosition.y, minY, maxY);

        // 4. 应用位置
        transform.position = smoothedPosition;
    }

    // 在编辑器中绘制边界框，方便你调整 mapMin/Max 参数
    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        // 绘制地图边界 (红色线框)
        Gizmos.color = Color.red;
        Vector3 center = new Vector3((mapMinX + mapMaxX) / 2, (mapMinY + mapMaxY) / 2, 0);
        Vector3 size = new Vector3(mapMaxX - mapMinX, mapMaxY - mapMinY, 0);
        Gizmos.DrawWireCube(center, size);

        // 绘制摄像机有效移动范围 (绿色线框) - 仅在运行时可见或估算
        // 注意：这里只是示意，实际范围依赖于 orthographicSize
        if (Application.isPlaying && Camera.main != null)
        {
            float h = Camera.main.orthographicSize;
            float w = h * Camera.main.aspect;
            
            float effMinX = mapMinX + w;
            float effMaxX = mapMaxX - w;
            float effMinY = mapMinY + h;
            float effMaxY = mapMaxY - h;

            if (effMinX <= effMaxX && effMinY <= effMaxY)
            {
                Gizmos.color = Color.green;
                Vector3 effCenter = new Vector3((effMinX + effMaxX) / 2, (effMinY + effMaxY) / 2, 0);
                Vector3 effSize = new Vector3(effMaxX - effMinX, effMaxY - effMinY, 0);
                Gizmos.DrawWireCube(effCenter, effSize);
            }
        }
    }
}