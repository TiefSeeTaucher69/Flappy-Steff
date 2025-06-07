using UnityEngine;

public class SpeedManager : MonoBehaviour
{
    public static float currentSpeed = 5f;
    public static float acceleration = 0.1f;

    void Update()
    {
        currentSpeed += acceleration * Time.deltaTime;
    }
}

