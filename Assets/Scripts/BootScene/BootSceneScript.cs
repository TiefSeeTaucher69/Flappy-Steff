using UnityEngine;
using UnityEngine.SceneManagement;

public class BootSceneScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        QualitySettings.vSyncCount = 0; // Deaktiviere VSync, um FPS Cap zu ermöglichen

        int fpsIndex = PlayerPrefs.GetInt("FPSCap", 4); // Default Index 4 (240 FPS)

        int targetFPS;
        switch (fpsIndex)
        {
            case 0: targetFPS = 30; break;
            case 1: targetFPS = 60; break;
            case 2: targetFPS = 120; break;
            case 3: targetFPS = 240; break; 
            case 4: targetFPS = -1; break;  // unbegrenzt
            default: targetFPS = 240; break;
        }

        Application.targetFrameRate = targetFPS;
        Debug.Log("FPS Cap aus PlayerPrefs gesetzt auf: " + targetFPS + " FPS");

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
