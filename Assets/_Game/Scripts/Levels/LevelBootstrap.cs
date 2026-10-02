using System;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class LevelBootstrap : MonoBehaviour
{
    [SerializeField] private LevelCatalog catalog;

    private void Awake()
    {
        try
        {
            LevelSession.Initialize(catalog);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);

            // Hatalı katalogla alt sistemlerin başlamasını engelle.
            gameObject.SetActive(false);
        }
    }

    [ContextMenu("Bölüm İlerlemesini Sıfırla")]
    private void ResetProgress()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning(
                "İlerlemeyi sıfırlamak için Play modundan çık.",
                this);

            return;
        }

        LevelSession.ResetProgress();

        Debug.Log("Bölüm ilerlemesi sıfırlandı.", this);
    }
}