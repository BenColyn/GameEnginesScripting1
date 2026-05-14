using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // WICHTIG: Das neue System einbinden

public class RetryScript : MonoBehaviour
{
    void Update()
    {
        // Abfrage für das neue Input System
        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            Restart();
        }
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}