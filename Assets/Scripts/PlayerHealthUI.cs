using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private PlayerHurtbox playerHurtbox;
    [SerializeField] private Image healthFillImage;

    private void OnEnable()
    {
        if (playerHurtbox != null)
        {
            playerHurtbox.OnHealthChanged += UpdateFill;
        }
    }

    private void OnDisable()
    {
        if (playerHurtbox != null)
        {
            playerHurtbox.OnHealthChanged -= UpdateFill;
        }
    }

    private void UpdateFill(float healthPct)
    {
        if (healthFillImage != null)
        {
            // Smoothly sliding the bar can be done here with LeanTween or Lerp, 
            // but setting fillAmount directly gives classic snappy arcade feedback
            healthFillImage.fillAmount = healthPct;
        }
    }
}