using UnityEngine;
using UnityEngine.InputSystem;

public class Torre_Vigia : MonoBehaviour
{
    [SerializeField] GameObject player;
    protected bool playerInRange = false;
    private Vector3 tpPos;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        tpPos = new Vector3(transform.position.x, transform.position.y + 9.1f, transform.position.z);
    }

    // Update is called once per frame
    void Update()
    {
        if (playerInRange)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                Debug.Log("space pressed");
                player.transform.position = tpPos;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            Debug.Log("player in range of ladder");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }
}
