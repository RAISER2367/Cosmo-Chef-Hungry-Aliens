using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PreferenceRow : MonoBehaviour
{
    [SerializeField] TMP_Text ingredientText;
    [SerializeField] TMP_Text stateText;
    [SerializeField] TMP_Text levelText;
    [SerializeField] Image background;

    public void Setup(string ingredientLabel, string stateLabel, PreferenceLevel level)
    {
        ingredientText.text = ingredientLabel;
        stateText.text = stateLabel;
        levelText.text = LevelLabel(level);
        if (background != null) background.color = LevelColor(level);
    }

    static string LevelLabel(PreferenceLevel level) => level switch
    {
        PreferenceLevel.Hates => "Ненавидить",
        PreferenceLevel.Dislikes => "Не любить",
        PreferenceLevel.Neutral => "Байдуже",
        PreferenceLevel.Likes => "Любить",
        PreferenceLevel.Loves => "Обожнює",
        _ => level.ToString()
    };

    static Color LevelColor(PreferenceLevel level) => level switch
    {
        PreferenceLevel.Hates => new Color(0.55f, 0.10f, 0.10f),
        PreferenceLevel.Dislikes => new Color(0.50f, 0.35f, 0.10f),
        PreferenceLevel.Neutral => new Color(0.25f, 0.25f, 0.25f),
        PreferenceLevel.Likes => new Color(0.15f, 0.40f, 0.20f),
        PreferenceLevel.Loves => new Color(0.10f, 0.55f, 0.25f),
        _ => Color.gray
    };
}