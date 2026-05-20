using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// UI component for an individual drone selection card.
/// Displays drone name, difficulty badge, and handles selection highlighting.
/// </summary>
public class DroneCardUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI difficultyText;
    [SerializeField] private Image border;
    [SerializeField] private Button cardButton;

    [Header("Colors")]
    [SerializeField] private Color selectedBorderColor = new Color(0.22f, 0.84f, 0.88f);
    [SerializeField] private Color defaultBorderColor = new Color(0.19f, 0.21f, 0.24f);

    public void Initialize(DroneConfig config, Action onClick)
    {
        if (nameText != null) nameText.text = config.droneName;

        if (difficultyText != null)
        {
            difficultyText.text = config.difficulty.ToString().ToUpper();
            difficultyText.color = GetDifficultyColor(config.difficulty);
        }

        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() => onClick?.Invoke());
        }
    }

    public void SetSelected(bool selected)
    {
        if (border != null)
        {
            border.color = selected ? selectedBorderColor : defaultBorderColor;
        }
    }

    public static Color GetDifficultyColor(DifficultyRating difficulty)
    {
        return difficulty switch
        {
            DifficultyRating.Easy => new Color(0.25f, 0.73f, 0.31f),
            DifficultyRating.Medium => new Color(0.82f, 0.60f, 0.13f),
            DifficultyRating.Hard => new Color(0.97f, 0.32f, 0.29f),
            _ => Color.white
        };
    }
}
