using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("UI 元素 (TMP)")]
    public TextMeshProUGUI killCountText;   // 显示本次杀敌数
    public TMP_InputField nameInput;        // 昵称输入框
    public Button submitButton;             // 提交按钮

    private int currentKillCount;           // 本次杀敌数

    private void Awake()
    {
        submitButton.onClick.AddListener(OnSubmit);
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 显示结束 UI，并传入本次杀敌数
    /// </summary>
    public void Show(int killCount)
    {
        currentKillCount = killCount;
        killCountText.text = $"杀敌数：{killCount}";
        nameInput.text = "";
        gameObject.SetActive(true);
    }

    private void OnSubmit()
    {
        string playerName = nameInput.text.Trim();
        if (string.IsNullOrEmpty(playerName))
            playerName = "无名螃蟹";
        if (playerName.Length > 12)
            playerName = playerName.Substring(0, 12);

        // 保存到排行榜
        LeaderboardManager.Instance.AddScore(playerName, currentKillCount);

        gameObject.SetActive(false);

        // 可选：返回主菜单（例如通过 GameManager 返回）
        GameManager.Instance.ReturnToMainMenu();
    }
}