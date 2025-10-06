using UnityEngine;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [Header("暂停UI面板")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("暂停菜单按钮")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    [Header("设置面板按钮")]
    [SerializeField] private Button backButton;

    private bool isPaused = false;

    private void Start()
    {
        // 初始化UI状态
        pauseMenuPanel.SetActive(false);
        settingsPanel.SetActive(false);

        // 绑定按钮事件
        resumeButton.onClick.AddListener(ResumeGame);
        settingsButton.onClick.AddListener(OpenSettings);
        mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        quitButton.onClick.AddListener(QuitGame);
        backButton.onClick.AddListener(BackToPauseMenu);
    }

    private void Update()
    {
        // ⭐ 修改：如果正在选择强化，禁用ESC暂停
        if (EnhancementSelectionUI.Instance != null && EnhancementSelectionUI.Instance.IsSelecting())
        {
            return; // 强化选择期间不处理ESC
        }

        // 按ESC键切换暂停状态
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                // 如果在设置界面，先返回暂停菜单
                if (settingsPanel.activeSelf)
                {
                    BackToPauseMenu();
                }
                else
                {
                    ResumeGame();
                }
            }
            else
            {
                PauseGame();
            }
        }
    }

    // 暂停游戏
    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f; // 暂停游戏时间
        pauseMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    // 继续游戏
    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; // 恢复游戏时间
        pauseMenuPanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    // 打开设置
    private void OpenSettings()
    {
        pauseMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    // 返回暂停菜单
    private void BackToPauseMenu()
    {
        pauseMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    // 返回主菜单
    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f; // 恢复时间流速
        isPaused = false;

        // ⭐ 新增：返回主菜单时重置所有强化
        if (EnhancementManager.Instance != null)
        {
            EnhancementManager.Instance.ResetAllEnhancements();
        }

        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadMainMenu();
        }
    }

    // 退出游戏
    private void QuitGame()
    {
        Time.timeScale = 1f; // 恢复时间流速

        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.QuitGame();
        }
    }
}