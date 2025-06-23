using UnityEngine;
using UnityEngine.UI;
using TMPro; // Required for TextMeshPro
using System; // For Action event
using System.Collections.Generic; // For List

public class UIManager : MonoBehaviour
{
    // Singleton instance. This UIManager will be per-scene.
    public static UIManager Instance { get; private set; }

    [Header("Main Panels")]
    // MissionBriefPanel được đặt ở đây vì nó cũng xuất hiện trong các Level Scene
    public GameObject missionBriefPanel; // Panel tóm tắt nhiệm vụ
    public GameObject pausePanel;       // Panel tạm dừng
    public GameObject winPanel;         // Panel chiến thắng
    public GameObject losePanel;        // Panel thất bại

    [Header("Mission Brief UI Elements")]
    public TextMeshProUGUI briefLevelNameText;
    public TextMeshProUGUI briefDescriptionText;
    public Button startMissionButton;

    [Header("Pause UI Elements")]
    public Button resumeButton;
    public Button pauseRestartButton;
    public Button pauseMainMenuButton;

    [Header("Win UI Elements")]
    public TextMeshProUGUI winScoreText; // Ví dụ: hiển thị điểm khi thắng
    public Button nextLevelButton;
    public Button winMainMenuButton;
    public Button winRestartButton; // Thêm nút restart khi thắng nếu muốn

    [Header("Lose UI Elements")]
    public TextMeshProUGUI loseReasonText; // Ví dụ: hiển thị lý do thua
    public Button tryAgainButton;
    public Button loseMainMenuButton;

    // --- Events for GameManager to subscribe to ---
    public event Action OnMissionBriefAcknowledged; // Khi người chơi chấp nhận brief
    public event Action OnResumeClicked;            // Khi nhấn nút Tiếp tục
    public event Action OnRestartLevelClicked;      // Khi nhấn nút Chơi lại
    public event Action OnMainMenuClicked;          // Khi nhấn nút Về Menu chính
    public event Action OnNextLevelClicked;         // Khi nhấn nút Màn tiếp theo (chỉ cho WinPanel)

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            // Không sử dụng DontDestroyOnLoad cho UIManager này
            // vì mỗi Scene màn chơi sẽ có UIManager riêng.
        }
    }

    void Start()
    {
        // Gán listener cho các nút Mission Brief
        startMissionButton?.onClick.AddListener(HideMissionBriefAndNotify);

        // Gán listener cho các nút Pause
        resumeButton?.onClick.AddListener(() => OnResumeClicked?.Invoke());
        pauseRestartButton?.onClick.AddListener(() => OnRestartLevelClicked?.Invoke());
        pauseMainMenuButton?.onClick.AddListener(() => OnMainMenuClicked?.Invoke());

        // Gán listener cho các nút Win
        nextLevelButton?.onClick.AddListener(() => OnNextLevelClicked?.Invoke());
        winMainMenuButton?.onClick.AddListener(() => OnMainMenuClicked?.Invoke());
        winRestartButton?.onClick.AddListener(() => OnRestartLevelClicked?.Invoke());

        // Gán listener cho các nút Lose
        tryAgainButton?.onClick.AddListener(() => OnRestartLevelClicked?.Invoke());
        loseMainMenuButton?.onClick.AddListener(() => OnMainMenuClicked?.Invoke());

        // Ẩn tất cả các panel khi khởi tạo (chỉ để lại UI gameplay nếu có)
        HideAllPanels();
    }

    /// <summary>
    /// Ẩn tất cả các panel UI chính.
    /// </summary>
    public void HideAllPanels() // Đổi thành public để GameManager có thể gọi
    {
        missionBriefPanel?.SetActive(false);
        pausePanel?.SetActive(false);
        winPanel?.SetActive(false);
        losePanel?.SetActive(false);
        // ... thêm các panel UI gameplay khác nếu bạn có (thanh máu, điểm số)
    }

    // --- Mission Brief Methods ---
    public void ShowMissionBrief(string levelName, string briefContent)
    {
        HideAllPanels();
        if (missionBriefPanel != null && briefLevelNameText != null && briefDescriptionText != null)
        {
            briefLevelNameText.text = levelName;
            briefDescriptionText.text = briefContent;
            missionBriefPanel.SetActive(true);
            // Time.timeScale sẽ được GameManager quản lý khi nó gọi hàm này
            Debug.Log("UIManager: Showing Mission Brief for " + levelName);
        }
    }

    private void HideMissionBriefAndNotify()
    {
        missionBriefPanel?.SetActive(false);
        // Time.timeScale sẽ được GameManager quản lý khi nó nhận sự kiện và chuyển trạng thái
        Debug.Log("UIManager: Mission Brief Hidden. Notifying GameManager.");
        OnMissionBriefAcknowledged?.Invoke();
    }

    // --- Pause Menu Methods ---
    public void ShowPauseMenu()
    {
        HideAllPanels();
        pausePanel?.SetActive(true);
        // Time.timeScale sẽ được GameManager quản lý khi nó gọi hàm này
        Debug.Log("UIManager: Showing Pause Menu.");
    }

    public void HidePauseMenu() // Dùng hàm này để UIManager ẩn UI mà không cần sự kiện
    {
        pausePanel?.SetActive(false);
        Debug.Log("UIManager: Hiding Pause Menu.");
    }

    // --- Win/Lose Screen Methods ---
    public void ShowWinScreen(string scoreInfo = "")
    {
        HideAllPanels();
        if (winPanel != null)
        {
            winPanel.SetActive(true);
            if (winScoreText != null) winScoreText.text = "Điểm của bạn: " + scoreInfo;
            // Time.timeScale sẽ được GameManager quản lý
            Debug.Log("UIManager: Showing Win Screen.");
        }
    }

    public void ShowLoseScreen(string reason = "")
    {
        HideAllPanels();
        if (losePanel != null)
        {
            losePanel.SetActive(true);
            if (loseReasonText != null) loseReasonText.text = reason;
            // Time.timeScale sẽ được GameManager quản lý
            Debug.Log("UIManager: Showing Lose Screen.");
        }
    }

    // --- Gameplay UI Methods (if you have them, e.g., health bar, score display) ---
    public void ShowGameplayHUD()
    {
        // Ví dụ: active/deactive HUD panel if separate
        // hudPanel?.SetActive(true);
        Debug.Log("UIManager: Showing Gameplay HUD.");
    }
}
