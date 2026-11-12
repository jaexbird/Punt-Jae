using UnityEngine;
using UnityEngine.UI;

// Declares the PlayerHealthUI class which inherits from MonoBehaviour so it can be attached to GameObjects in Unity
public class PlayerHealthUI : MonoBehaviour
{
    // [SerializeField] allows this private variable to be assigned in the Unity Inspector while keeping it hidden from other scripts
    // This holds a reference to the script handling the player's health/hurt logic
    [SerializeField] private PlayerHurtbox playerHurtbox;

    // Holds a reference to the UI Image component that acts as the visual health bar fill
    [SerializeField] private Image healthFillImage;

    // A built-in Unity method that runs automatically whenever this GameObject or script is turned on (enabled)
    private void OnEnable()
    {
        // Checks to ensure the playerHurtbox reference was actually assigned in the Inspector to prevent crashes
        if (playerHurtbox != null)
        {
            // Subscribes the UpdateFill method to the OnHealthChanged event/delegate
            // This means whenever OnHealthChanged fires in playerHurtbox, UpdateFill will automatically run
            playerHurtbox.OnHealthChanged += UpdateFill;
        }
    }

    // A built-in Unity method that runs automatically whenever this GameObject or script is turned off (disabled) or destroyed
    private void OnDisable()
    {
        // Checks if the reference exists before trying to access it
        if (playerHurtbox != null)
        {
            // Unsubscribes the UpdateFill method from the event
            // This is critical to prevent memory leaks or errors if the UI is destroyed but the player is still alive
            playerHurtbox.OnHealthChanged -= UpdateFill;
        }
    }

    // A private method that takes a float parameter (representing health percentage, usually 0.0 to 1.0)
    // This method is called via the event subscription in OnEnable
    private void UpdateFill(float healthPct)
    {
        // Safety check to ensure the UI Image reference was assigned in the Inspector
        if (healthFillImage != null)
        {
            // Smoothly sliding the bar can be done here with LeanTween or Lerp, 
            // but setting fillAmount directly gives classic snappy arcade feedback

            // Updates the 'Fill Amount' property of the Image component to match the new health percentage
            // (The Image component must have its Image Type set to 'Filled' in the Inspector for this to work)
            healthFillImage.fillAmount = healthPct;
        }
    }
}