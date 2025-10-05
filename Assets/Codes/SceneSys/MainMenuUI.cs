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

    // ⬇⬇⬇ 新增：把你创建的 EquippedWeaponData 资源挂到这里
    [Header("Data")]
    [SerializeField] private EquippedWeaponData equippedWeaponData; // 在Inspector里拖拽赋值

    private void Start()
    {
        ShowMainPanel();

        startButton.onClick.AddListener(OnStartGame);
        collectionButton.onClick.AddListener(OnCollection);
        settingsButton.onClick.AddListener(OnSettings);
        quitButton.onClick.AddListener(OnQuit);
        backButton.onClick.AddListener(OnBackToMain);
    }

    private void ShowMainPanel()
    {
        mainPanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    private void ShowSettingsPanel()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    private void OnStartGame()
    {
        // 1) 原有：可选的“自动收集第一个物品”
        if (CollectionManager.Instance != null)
        {
            CollectionManager.Instance.CheckAndCollectFirstItem(); // 不改你现有逻辑
        }
        else
        {
            Debug.LogWarning("CollectionManager.Instance is null! 无法检查第一个收集品");
        }

        // 2) 新增：若当前未装备武器，则用 Collectible 列表的第一个来装备
        if (equippedWeaponData != null && !equippedWeaponData.IsEquipped())
        {
            var cm = CollectionManager.Instance;
            if (cm != null)
            {
                CollectibleData first = cm.GetFirstCollectible(); // 列表第一个
                if (first != null)
                {
                    equippedWeaponData.EquipWeapon(first); // 把 Collectible 内容写入 EquippedWeaponData
                    //（可选）如果你希望开始游戏时一定“标记为已收集”，可以确保它被计入收集：
                    if (!first.isCollected)
                    {
                        cm.CollectItem(first.id);
                    }
                }
                else
                {
                    Debug.LogWarning("Collectible 列表为空，无法默认装备武器。");
                }
            }
        }

        // 3) 进入游戏场景
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadGameScene();
        }
    }

    private void OnCollection()
    {
        GameSceneManager.Instance.LoadCollectionScene();
    }

    private void OnSettings()
    {
        ShowSettingsPanel();
    }

    private void OnQuit()
    {
        GameSceneManager.Instance.QuitGame();
    }

    private void OnBackToMain()
    {
        ShowMainPanel();
    }
}
