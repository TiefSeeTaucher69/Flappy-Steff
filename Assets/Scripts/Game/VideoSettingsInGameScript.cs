using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VideoSettingsInGameScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public Dropdown fpsDropdown; // Dropdown für FPS Cap
    public const string PlayerPrefsKey = "FPSCap"; // Schlüssel für PlayerPrefs
    public Dropdown resolutionDropdown;
    Resolution[] resolutions;
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



        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        int currentResIndex = 0;
        var options = new List<string>();

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + " x " + resolutions[i].height + " @ " + resolutions[i].refreshRate + "Hz";
            options.Add(option);

            if (resolutions[i].width == Screen.currentResolution.width &&
                resolutions[i].height == Screen.currentResolution.height &&
                resolutions[i].refreshRate == Screen.currentResolution.refreshRate)
            {
                currentResIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = PlayerPrefs.GetInt("ResolutionIndex", currentResIndex);
        resolutionDropdown.RefreshShownValue();

        ApplyResolution(resolutionDropdown.value);

        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }


    // Update is called once per frame
    void Update()
    {
        
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

    public void OnResolutionChanged(int index)
    {
        ApplyResolution(index);
        PlayerPrefs.SetInt("ResolutionIndex", index);
        PlayerPrefs.Save();
    }

    void ApplyResolution(int index)
    {
        Resolution res = resolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreenMode, res.refreshRate);
        Debug.Log("Auflösung gesetzt auf: " + res.width + "x" + res.height);
    }
}
