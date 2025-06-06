using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SFXVolumeControl : MonoBehaviour
{
    public AudioMixer mixer;
    public Slider slider;

    void Start()
    {
        // Lade gespeicherten Wert oder setze auf 1 (100%)
        float volume;
        if (PlayerPrefs.HasKey("SFXVolume"))
            volume = PlayerPrefs.GetFloat("SFXVolume");
        else
            volume = 1f;

        slider.value = volume;
        SetVolume(volume);

        slider.onValueChanged.AddListener(SetVolume);
    }

    public void SetVolume(float value)
    {
        // Umwandlung von linearem Slider-Wert (0-1) in logarithmische Dezibel-Skala
        // Bei 0 Wert -80 dB (quasi stumm), sonst 20*log10(value)
        Debug.Log("SetVolume für SFX aufgerufen mit Wert: " + value);
        mixer.SetFloat("SFXVolume", value > 0 ? Mathf.Log10(value) * 20 : -80);

        // Speichern
        PlayerPrefs.SetFloat("SFXVolume", value);
        PlayerPrefs.Save();  // Sicherstellen, dass die Änderungen gespeichert werden
    }
}
