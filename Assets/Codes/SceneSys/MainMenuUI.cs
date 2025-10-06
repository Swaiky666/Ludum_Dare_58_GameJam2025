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

    [Header("Data")]
    [SerializeField] private EquippedWeaponData equippedWeaponData;

    private void Start()
    {
        ShowMainPanel();

        // 添加空引用检查
        if (startButton != null)
            startButton.onClick.AddListener(OnStartGame);
        else
            Debug.LogError("StartButton 未在Inspector中分配！");

        if (collectionButton != null)
            collectionButton.onClick.AddListener(OnCollection);
        else
            Debug.LogWarning("CollectionButton 未在Inspector中分配");

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettings);
        else
            Debug.LogWarning("SettingsButton 未在Inspector中分配");

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuit);
        else
            Debug.LogWarning("QuitButton 未在Inspector中分配");

        if (backButton != null)
            backButton.onClick.AddListener(OnBackToMain);
        else
            Debug.LogWarning("BackButton 未在Inspector中分配");
    }

    private void ShowMainPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void ShowSettingsPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    private void OnStartGame()
    {
        if (CollectionManager.Instance != null)
        {
            CollectionManager.Instance.CheckAndCollectFirstItem();
        }

        if (equippedWeaponData != null && !equippedWeaponData.IsEquipped())
        {
            var cm = CollectionManager.Instance;
            if (cm != null)
            {
                CollectibleData first = cm.GetFirstCollectible();
                if (first != null)
                {
                    equippedWeaponData.EquipWeapon(first);
                    if (!first.isCollected)
                    {
                        cm.CollectItem(first.id);
                    }
                }
            }
        }

        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadGameScene();
        }
    }

    private void OnCollection()
    {
        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadCollectionScene();
    }

    private void OnSettings()
    {
        ShowSettingsPanel();
    }

    private void OnQuit()
    {
        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.QuitGame();
    }

    private void OnBackToMain()
    {
        ShowMainPanel();
    }
}