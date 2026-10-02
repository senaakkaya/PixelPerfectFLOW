using TMPro;
using UnityEngine;

public sealed class GameHUD : MonoBehaviour
{
    [Header("Oyun")]
    [SerializeField] private BoardManager board;

    [Header("Yazılar")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text remainingText;

    private bool started;

    private void OnEnable()
    {
        if (board != null)
            board.BlockRemoved += HandleBlockRemoved;

        // Nesne sonradan tekrar açılırsa güncel bilgiyi göster.
        if (started)
            RefreshAll();
    }

    private void Start()
    {
        if (board == null ||
            levelText == null ||
            remainingText == null)
        {
            Debug.LogError(
                "GameHUD: Board ve yazı bağlantıları atanmalı.",
                this);

            enabled = false;
            return;
        }

        started = true;
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (levelText != null)
        {
            int levelNumber = LevelSession.CurrentIndex + 1;
            levelText.SetText("Bölüm {0}", levelNumber);
        }

        RefreshRemaining();
    }

    private void HandleBlockRemoved(int cellIndex)
    {
        RefreshRemaining();
    }

    private void RefreshRemaining()
    {
        if (board == null || remainingText == null)
            return;

        remainingText.SetText(
            "Kalan Küp: {0}",
            board.RemainingBlocks);
    }

    private void OnDisable()
    {
        if (board != null)
            board.BlockRemoved -= HandleBlockRemoved;
    }
}