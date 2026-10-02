using TMPro;
using UnityEngine;

[RequireComponent(typeof(ShooterController))]
public sealed class ShooterAmmoLabel : MonoBehaviour
{
    [SerializeField] private Camera gameCamera;

    [Header("Yazı")]
    [SerializeField, Min(0.1f)] private float fontSize = 3f;
    [SerializeField, Min(0f)] private float screenUpOffset = 0.35f;
    [SerializeField] private Color textColor = Color.white;

    private ShooterController shooter;
    private TextMeshPro label;
    private int displayedAmmo = -1;

    private void Awake()
    {
        shooter = GetComponent<ShooterController>();
        if (gameCamera == null)
            gameCamera = Camera.main;

        if (gameCamera == null)
        {
            Debug.LogError(
                "ShooterAmmoLabel: Game Camera alanına Main Camera ata.",
                this);

            enabled = false;
            return;
        }

        if (TMP_Settings.defaultFontAsset == null)
        {
            Debug.LogError(
                "TextMeshPro Essential Resources içe aktarılmalı.",
                this);

            enabled = false;
            return;
        }

        GameObject labelObject = new GameObject(
            gameObject.name + "_AmmoLabel");

        // Bağımsız nesne: shooter'ın dönüş ve ölçeğinden etkilenmez.
        label = labelObject.AddComponent<TextMeshPro>();

        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = textColor;
        label.enableAutoSizing = false;
        label.overflowMode = TextOverflowModes.Overflow;

        label.rectTransform.sizeDelta = new Vector2(1.2f, 0.5f);

        label.SetText(string.Empty);
        label.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        displayedAmmo = -1;
    }

    private void LateUpdate()
    {
        if (label == null || gameCamera == null)
            return;

        int ammo = shooter.RemainingAmmo;

        // Yazıyı yalnızca sayı değiştiğinde güncelle.
        if (ammo != displayedAmmo)
        {
            displayedAmmo = ammo;
            label.SetText("{0:0}", ammo);
        }

        Transform cameraTransform = gameCamera.transform;

        Vector3 position =
            transform.position +
            cameraTransform.up * screenUpOffset -
            cameraTransform.forward * 0.05f;

        // Yazı ekrana dönük ve düz kalır.
        label.transform.SetPositionAndRotation(
            position,
            cameraTransform.rotation);

        if (!label.gameObject.activeSelf)
            label.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (label != null)
            label.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (label != null)
            Destroy(label.gameObject);
    }
}