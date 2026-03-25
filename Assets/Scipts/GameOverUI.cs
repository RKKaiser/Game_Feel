// GameOverUI.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    [Header("UI 元素")]
    public TextMeshProUGUI survivalTimeText; // 改为 TMP
    public TMP_InputField nameInput;         // 输入框也建议使用 TMP 版本
    public Button submitButton;       // 提交按钮

    private float currentSurvivalTime;  // 本次游戏时长

    private void Start()
    {
        // 确保提交按钮监听
        submitButton.onClick.AddListener(OnSubmit);
        // 初始隐藏
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 显示结束 UI，并传入本次存活时长
    /// </summary>
    public void Show(float survivalTime)
    {
        currentSurvivalTime = survivalTime;
        survivalTimeText.text = FormatTime(survivalTime);
        nameInput.text = "";  // 清空输入框
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 提交按钮回调
    /// </summary>
    private void OnSubmit()
    {
        string playerName = nameInput.text.Trim();
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "无名勇士";  // 默认昵称
        }
        // 限制昵称长度（可选）
        if (playerName.Length > 12)
            playerName = playerName.Substring(0, 12);

        // 保存分数
        LeaderboardManager.Instance.AddScore(playerName, currentSurvivalTime);

        // 关闭面板，然后可以切换回主界面或显示其他信息
        gameObject.SetActive(false);
        // 例如：SceneManager.LoadScene("MainMenu");
    }

    private string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60);
        float remainingSeconds = seconds % 60;
        return string.Format("{0}分{1:F1}秒", minutes, remainingSeconds);
    }
}