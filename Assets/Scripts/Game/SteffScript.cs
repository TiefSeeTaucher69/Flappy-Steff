using UnityEngine;

public class SteffScript : MonoBehaviour
{

    public Rigidbody2D myRigitbody;
    public float flapStrength;
    public LogicScript logic;
    public bool steffIsAlive = true;
    private AudioSource hitAudioSource;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        logic = GameObject.FindGameObjectsWithTag("Logic")[0].GetComponent<LogicScript>();
        hitAudioSource = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && steffIsAlive)
        {
            myRigitbody.linearVelocity = Vector2.up * flapStrength;
        }
        Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);

        if (viewportPos.x < 0 || viewportPos.x > 1 ||
            viewportPos.y < 0 || viewportPos.y > 1)
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

}
