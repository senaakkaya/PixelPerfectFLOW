using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public sealed class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Playing,
        Won,
        Lost
    }

    public GameState State { get; private set; } = GameState.Playing;

    [Header("Oyun Sistemleri")]
    [SerializeField] private BoardManager board;
    [SerializeField] private WaitingAreaManager waitingArea;
    [SerializeField] private InputController inputController;
    [SerializeField] private ShooterQueue shooterQueue;

    [Header("Sahnedeki Shooter'lar")]
    [SerializeField] private ConveyorMover[] movers;

    [Header("Sonuç Ekranı")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultTitle;
    [SerializeField] private GameObject nextLevelButton;

    private bool isLoading;

    private void Awake()
    {
        State = GameState.Playing;

        if (resultPanel != null)
            resultPanel.SetActive(false);

        if (nextLevelButton != null)
            nextLevelButton.SetActive(false);
    }

    private void OnEnable()
    {
        if (board != null)
            board.BoardCleared += HandleBoardCleared;

        if (waitingArea != null)
            waitingArea.WaitingAreaFull += HandleWaitingAreaFull;
    }

    private void Start()
    {
        if (board == null ||
            waitingArea == null ||
            inputController == null ||
            shooterQueue == null ||
            resultPanel == null ||
            resultTitle == null)
        {
            Debug.LogError(
                "GameManager: Inspector bağlantılarını kontrol et.",
                this);
        }
    }

    public void SetShooters(ConveyorMover[] generatedShooters)
    {
        movers = generatedShooters;
    }

    private void HandleBoardCleared()
    {
        Finish(GameState.Won);
    }

    private void HandleWaitingAreaFull()
    {
        Finish(GameState.Lost);
    }

    private void Finish(GameState result)
    {
        if (State != GameState.Playing)
            return;

        State = result;

        if (inputController != null)
            inputController.enabled = false;

        if (shooterQueue != null)
            shooterQueue.enabled = false;

        if (movers != null)
        {
            for (int i = 0; i < movers.Length; i++)
            {
                ConveyorMover mover = movers[i];

                if (mover == null)
                    continue;

                mover.enabled = false;

                if (mover.TryGetComponent(
                        out ShooterController shooter))
                {
                    shooter.enabled = false;
                }
            }
        }

        bool won = result == GameState.Won;

        if (resultTitle != null)
        {
            if (!won)
            {
                resultTitle.text = "Bekleme Alanı Doldu";
            }
            else if (LevelSession.CurrentLevel != null &&
                     !LevelSession.HasNextLevel)
            {
                resultTitle.text = "Tüm Bölümler Tamamlandı!";
            }
            else
            {
                resultTitle.text = "Bölüm Tamamlandı!";
            }
        }

        if (nextLevelButton != null)
        {
            nextLevelButton.SetActive(
                won && LevelSession.HasNextLevel);
        }

        if (resultPanel != null)
            resultPanel.SetActive(true);
    }

    public void RestartLevel()
    {
        if (!CanReloadScene())
            return;

        ReloadScene();
    }

    public void NextLevel()
    {
        if (State != GameState.Won || !CanReloadScene())
            return;

        if (!LevelSession.TryAdvance())
            return;

        ReloadScene();
    }

    private bool CanReloadScene()
    {
        if (isLoading)
            return false;

        if (SceneManager.GetActiveScene().buildIndex < 0)
        {
            Debug.LogError(
                "GameScene, Build Profiles içindeki sahne listesine eklenmeli.",
                this);

            return false;
        }

        return true;
    }

    private void ReloadScene()
    {
        isLoading = true;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDisable()
    {
        if (board != null)
            board.BoardCleared -= HandleBoardCleared;

        if (waitingArea != null)
            waitingArea.WaitingAreaFull -= HandleWaitingAreaFull;
    }
}