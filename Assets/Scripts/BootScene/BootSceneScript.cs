using UnityEngine;
using UnityEngine.SceneManagement;

public class BootSceneScript : MonoBehaviour
{

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        QualitySettings.vSyncCount = 0; // Deaktiviere VSync, um FPS Cap zu ermöglichen

        // Auflösung laden oder Standard (native)
        int resIndex = PlayerPrefs.GetInt("ResolutionIndex", -1);
        if (resIndex != -1)
        {
            Resolution[] resolutions = Screen.resolutions;
            if (resIndex < resolutions.Length)
            {
                Resolution res = resolutions[resIndex];
                Screen.SetResolution(res.width, res.height, FullScreenMode.FullScreenWindow, res.refreshRate);
                Debug.Log("Auflösung geladen aus PlayerPrefs: " + res.width + "x" + res.height);
            }
        }
        else
        {
            // Keine gespeicherte Auflösung → native Auflösung setzen
            Resolution nativeRes = Screen.currentResolution;
            Screen.SetResolution(nativeRes.width, nativeRes.height, true);
            Debug.Log("Native Auflösung gesetzt: " + nativeRes.width + "x" + nativeRes.height);
        }

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
