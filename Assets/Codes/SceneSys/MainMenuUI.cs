using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI面板")]
    public GameObject mainPanel;
    public GameObject settingsPanel;

    [Header("主菜单按钮")]
    public Button startButton;
    public Button collectionButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("设置面板按钮")]
    public Button backButton;

    private void Start()
    {
        // 初始化UI状态
        ShowMainPanel();

        // 绑定按钮事件
        startButton.onClick.AddListener(OnStartGame);
        collectionButton.onClick.AddListener(OnCollection);
        settingsButton.onClick.AddListener(OnSettings);
        quitButton.onClick.AddListener(OnQuit);
        backButton.onClick.AddListener(OnBackToMain);
    }

    // 显示主菜单面板
    private void ShowMainPanel()
    {
        mainPanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    // 显示设置面板
    private void ShowSettingsPanel()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    // 开始游戏
    private void OnStartGame()
    {
        GameSceneManager.Instance.LoadGameScene();
    }

    // 打开收集界面
    private void OnCollection()
    {
        GameSceneManager.Instance.LoadCollectionScene();
    }

    // 打开设置
    private void OnSettings()
    {
        ShowSettingsPanel();
    }

    // 退出游戏
    private void OnQuit()
    {
        GameSceneManager.Instance.QuitGame();
    }

    // 返回主菜单
    private void OnBackToMain()
    {
        ShowMainPanel();
    }
}