using UnityEngine;
using UnityEngine.SceneManagement;

public class BootSceneScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (PlayerPrefs.HasKey("Username"))
        {
            // Username existiert → Hauptszene laden
            SceneManager.LoadScene("MainMenu");
        }
        else
        {
            // Kein Username → Eingabeszene laden
            SceneManager.LoadScene("FirstOpen");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
