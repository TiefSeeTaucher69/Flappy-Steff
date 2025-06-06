using UnityEngine;
using UnityEngine.SceneManagement;

public class EscapeSceneHandlerScript : MonoBehaviour
{
    public void QuitGame()
    {
        // Logic to quit the game
        Debug.Log("Game Quit");
        Application.Quit();
    }

    public void LoadSettingsScene()
    {
               // Logic to load the settings scene
        Debug.Log("Settings Scene Loaded");
        SceneManager.LoadScene("SettingsScene"); 
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
}
