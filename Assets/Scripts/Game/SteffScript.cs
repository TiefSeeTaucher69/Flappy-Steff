using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SteffScript : MonoBehaviour
{
    public Rigidbody2D myRigitbody;
    public float flapStrength;
    public LogicScript logic;
    public bool steffIsAlive = true;
    private AudioSource hitAudioSource;

    public GameObject escapeInGameScreen;
    public GameObject settingsOnPauseScreen;
    private bool isPaused = false;
    private bool settingsManuallyOpened = false;

    void Start()
    {
        logic = GameObject.FindGameObjectsWithTag("Logic")[0].GetComponent<LogicScript>();
        hitAudioSource = GetComponent<AudioSource>();

        if (escapeInGameScreen != null)
            escapeInGameScreen.SetActive(false);

        if (settingsOnPauseScreen != null)
            settingsOnPauseScreen.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && steffIsAlive)
        {
            if (!isPaused)
            {
                PauseGame();
            }
            else if (isPaused && settingsManuallyOpened)
            {
                CloseSettingsOnPause();
            }
            else
            {
                ResumeGame();
            }
        }

        if (isPaused) return;

        if (Input.GetKeyDown(KeyCode.Space) && steffIsAlive)
        {
            myRigitbody.linearVelocity = Vector2.up * flapStrength;
        }

        Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);
        if (viewportPos.x < 0 || viewportPos.x > 1 || viewportPos.y < 0 || viewportPos.y > 1)
        {
            Debug.Log("Object has left the screen");
            logic.gameOver();
            steffIsAlive = false;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!hitAudioSource.isPlaying && steffIsAlive)
        {
            hitAudioSource.Play();
        }
        logic.gameOver();
        steffIsAlive = false;
    }

    private void PauseGame()
    {
        isPaused = true;
        settingsManuallyOpened = false;
        Time.timeScale = 0f;

        if (escapeInGameScreen != null)
            escapeInGameScreen.SetActive(true);

        if (settingsOnPauseScreen != null)
            settingsOnPauseScreen.SetActive(false);
    }

    private void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (escapeInGameScreen != null)
            escapeInGameScreen.SetActive(false);

        if (settingsOnPauseScreen != null)
            settingsOnPauseScreen.SetActive(false);
    }

    public void OpenSettingsOnPause()
    {
        settingsManuallyOpened = true;

        if (settingsOnPauseScreen != null)
            settingsOnPauseScreen.SetActive(true);

        if (escapeInGameScreen != null)
            escapeInGameScreen.SetActive(false);
    }

    public void CloseSettingsOnPause()
    {
        settingsManuallyOpened = false;

        if (settingsOnPauseScreen != null)
            settingsOnPauseScreen.SetActive(false);

        if (escapeInGameScreen != null)
            escapeInGameScreen.SetActive(true);
    }

    public void FromPauseToMenu()
    {
        ResumeGame();
        steffIsAlive = false;
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitFromPause()
    {
        Application.Quit();
        Debug.Log("Application quit requested from pause menu");
    }
}
