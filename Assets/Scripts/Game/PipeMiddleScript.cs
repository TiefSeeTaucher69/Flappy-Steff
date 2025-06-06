using UnityEngine;

public class PipeMiddleScript : MonoBehaviour
{
    public SteffScript steffReference;
    public LogicScript logic;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        logic = GameObject.FindGameObjectsWithTag("Logic")[0].GetComponent<LogicScript>();
        steffReference = GameObject.FindGameObjectWithTag("Steff").GetComponent<SteffScript>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if ((collision.gameObject.layer == 3) && steffReference.steffIsAlive == true)
        {
            logic.addScore(1);
        }
          
    }
}
