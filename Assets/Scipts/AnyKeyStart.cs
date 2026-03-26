using UnityEngine;
using UnityEngine.SceneManagement;

public class AnyKeyStart : MonoBehaviour
{
    [Header("场景设置")]
    public string nextSceneName = "GameLevel";  // 要加载的游戏场景名称
    public bool useGameManager = true;          // 是否使用已有的 GameManager 启动游戏（可选）

    private bool hasStarted = false;             // 防止重复触发

    void Update()
    {
        // 检测是否有任意按键（键盘、手柄等）被按下，且排除鼠标按键
        if (!hasStarted && Input.anyKeyDown && !IsMouseButtonDown())
        {
            hasStarted = true;

            if (useGameManager && GameManager.Instance != null)
            {
                GameManager.Instance.StartGame(nextSceneName);
            }
            else
            {
                SceneManager.LoadScene(nextSceneName);
            }
        }
    }

    /// <summary>
    /// 检查是否有任何鼠标按键在本帧被按下
    /// </summary>
    private bool IsMouseButtonDown()
    {
        return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
    }
}