using UnityEngine;

public class FoodVisual : MonoBehaviour
{
    [SerializeField] Renderer targetRenderer;
    [SerializeField] Material rawMat;
    [SerializeField] Material cutMat;
    [SerializeField] Material readyMat;
    [SerializeField] Material burntMat;

    public void Apply(FoodItem food)
    {
        if (food == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        switch (food.state)
        {
            case FoodState.Raw: targetRenderer.sharedMaterial = rawMat; break;
            case FoodState.Cut: targetRenderer.sharedMaterial = cutMat; break;
            case FoodState.Ready: targetRenderer.sharedMaterial = readyMat; break;
            case FoodState.Burnt: targetRenderer.sharedMaterial = burntMat; break;
        }
    }
}