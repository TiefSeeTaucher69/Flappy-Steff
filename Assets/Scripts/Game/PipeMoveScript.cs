using UnityEngine;

public class PipeMoveScript : MonoBehaviour
{

    public float moveSpeed = 5;
    public float deadZone = -40;
    public float acceleration = 0.1f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float speed = SpeedManager.currentSpeed;
        transform.position += Vector3.left * speed * Time.deltaTime;

        if (transform.position.x < deadZone)
        {
            Debug.Log("Pipe deleted");
            Destroy(gameObject);
        }
    }
}
