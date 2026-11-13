using UnityEngine;
using UnityEngine.InputSystem;

public class Binoculos : MonoBehaviour
{
    public Camera playerCamera;
    public Camera binocularCamera;
    [SerializeField] public GameObject miniMapa;

    private bool playerNearby = false;
    private bool usingBinoculars = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        binocularCamera.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (playerNearby && Keyboard.current.eKey.wasPressedThisFrame)
        {
            usingBinoculars = !usingBinoculars;

            playerCamera.gameObject.SetActive(!usingBinoculars);
            binocularCamera.gameObject.SetActive(usingBinoculars);
            miniMapa.gameObject.SetActive(!usingBinoculars);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = false;

            if (usingBinoculars)
            {
                usingBinoculars = false;
                playerCamera.gameObject.SetActive(true);
                miniMapa.gameObject.SetActive(true);
                binocularCamera.gameObject.SetActive(false);
            }
        }
    }
}
