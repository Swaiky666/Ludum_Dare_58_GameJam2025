using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    // 场景名称常量
    public const string MAIN_MENU = "MainMenu";
    public const string GAME_SCENE = "GameScene";
    public const string COLLECTION_SCENE = "CollectionScene";

    private void Awake()
    {
        // 单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 加载主菜单场景
    public void LoadMainMenu()
    {
        SceneManager.LoadScene(MAIN_MENU);
    }

    // 加载游戏场景
    public void LoadGameScene()
    {
        SceneManager.LoadScene(GAME_SCENE);
    }

    // 加载收集界面场景
    public void LoadCollectionScene()
    {
        SceneManager.LoadScene(COLLECTION_SCENE);
    }

    // 退出游戏
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}