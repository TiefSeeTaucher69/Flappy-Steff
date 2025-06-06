using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class FirstOpen : MonoBehaviour
{
    public InputField usernameInput;
    public Text feedbackText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SaveUsername()
    {
        string username = usernameInput.text;

        if (!string.IsNullOrWhiteSpace(username))
        {
            PlayerPrefs.SetString("Username", username);
            PlayerPrefs.Save();
            Debug.Log("Username gespeichert: " + username);
            SceneManager.LoadScene("MainMenu"); // Jetzt erst MainMenu laden
        }
        else
        {
            feedbackText.text = "Name darf nicht leer sein.";
            Debug.LogWarning("Leerer Name.");
        }
    }
}
