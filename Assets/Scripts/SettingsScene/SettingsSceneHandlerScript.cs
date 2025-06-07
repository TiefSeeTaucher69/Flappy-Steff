using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsSceneHandlerScript : MonoBehaviour
{
    public Dropdown fpsDropdown; // Dropdown für FPS Cap
    public const string PlayerPrefsKey = "FPSCap"; // Schlüssel für PlayerPrefs
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Gespeicherte Einstellung laden, -1 bedeutet kein Eintrag
        int savedIndex = PlayerPrefs.GetInt(PlayerPrefsKey, -1);

        if (savedIndex == -1)
        {
            // Noch nichts gespeichert, Standard auf 240 FPS (Index 4)
            savedIndex = 4;
            PlayerPrefs.SetInt(PlayerPrefsKey, savedIndex);
            PlayerPrefs.Save();
        }

        // Listener temporär entfernen, damit beim Setzen des Werts kein Event feuert
        fpsDropdown.onValueChanged.RemoveAllListeners();

        fpsDropdown.value = savedIndex;

        ApplySetting(savedIndex);

        // Listener wieder hinzufügen
        fpsDropdown.onValueChanged.AddListener(OnDropdownChanged);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SceneManager.LoadScene("EscapeScene");
        }
    }

    void OnDropdownChanged(int index)
    {
        ApplySetting(index);
        PlayerPrefs.SetInt(PlayerPrefsKey, index);
        PlayerPrefs.Save();
    }

    void ApplySetting(int index)
    {
        switch (index)
        {
            case 0:
                Application.targetFrameRate = 30;
                break;
            case 1:
                Application.targetFrameRate = 60;
                break;
            case 2:
                Application.targetFrameRate = 120;
                break;
            case 3:
                Application.targetFrameRate = 240; 
                break;
            case 4:
                Application.targetFrameRate = -1; // unbegrenzt
                break;
            default:
                Application.targetFrameRate = 240; // Fallback unbegrenzt
                break;
        }
    }
}
