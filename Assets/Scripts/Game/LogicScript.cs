using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LogicScript : MonoBehaviour
{
    public int playerScore;
    public int highScore;
    public Text scoreText;
    public GameObject gameOverScreen;
    public GameObject menuScreen;
    public LeaderboardSenderScript leaderboardSenderScript;
    private bool hasGameOverBeenHandled = false;

    [ContextMenu("Increase Score")]
    public void addScore(int scoreToAdd)
    {
        playerScore = playerScore + scoreToAdd;
        scoreText.text = playerScore.ToString();
    }

    public void restartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void gameOver()
    {
        if (hasGameOverBeenHandled) return;
        hasGameOverBeenHandled = true;

        gameOverScreen.SetActive(true);

        if (playerScore > highScore)
        {
            PlayerPrefs.SetInt("Highscore", playerScore);
            PlayerPrefs.Save();
            Debug.Log("New high score saved: " + playerScore);
            string username = PlayerPrefs.GetString("Username", "Anonymous");
            StartCoroutine(leaderboardSenderScript.SendScore(username, playerScore));
        }
        else
        {
            Debug.Log("Kein neuer Highscore. Aktueller: " + highScore);
        }
    }

    public void backtoMenu()
    {
        Debug.Log("Going to main menu");
        SceneManager.LoadScene("MainMenu");
    }

    private void Start()
    {
        highScore = PlayerPrefs.GetInt("Highscore", 0);
        leaderboardSenderScript = GameObject.Find("LeaderboardSender").GetComponent<LeaderboardSenderScript>();
    }
}
