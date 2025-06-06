using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuHandlerScript : MonoBehaviour
{
    public Text highscoreText;
    public Text usernameText;
    public void StartGame()
    {
        // Logic to start the game
        SceneManager.LoadScene("GameScene"); // Replace "GameScene" with the actual name of your game scene
        Debug.Log("Game Started");
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        int highscore = PlayerPrefs.GetInt("Highscore", 0);
        highscoreText.text = highscore.ToString();
        Debug.Log("Highscore loaded: " + highscore);
        string username = PlayerPrefs.GetString("Username", "Guest");
        usernameText.text = username.ToString();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SceneManager.LoadScene("EscapeScene");
        }
    }
}
