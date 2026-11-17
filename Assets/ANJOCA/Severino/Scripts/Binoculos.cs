using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Binóculos da torre de vigia (do José). Está no objeto Torre_Vigia › Binoculos › lente, com um Box Collider Is Trigger.
// Ao pé dos binóculos aparece "[E] Usar binóculos". Com E troca-se a câmara do jogador pela câmara dos binóculos
// e o minimapa desaparece. Com E outra vez, ou ao sair da zona, volta tudo ao normal.
// O aviso usa o mesmo texto do Canvas que a porta do cartão, a tampa da cave e o quadro elétrico (RedeEletricaTexto).
public class Binoculos : MonoBehaviour
{
    public Camera playerCamera;
    public Camera binocularCamera;
    [SerializeField] public GameObject miniMapa;

    [Header("Aviso no ecrã")]
    public Key tecla = Key.E;
    [Tooltip("Texto ao pé dos binóculos, depois de [E].")]
    public string textoUsar = "Usar binóculos";
    [Tooltip("Texto enquanto estás a ver pelos binóculos, depois de [E].")]
    public string textoLargar = "Largar binóculos";
    [Tooltip("Se ficar vazio, procura sozinho o objeto com o nome de baixo.")]
    public TextMeshProUGUI textoAviso;
    public string nomeTextoAviso = "RedeEletricaTexto";

    private bool playerNearby = false;
    private bool usingBinoculars = false;
    private string ultimoTexto = null;   // o que os binóculos escreveram, para só apagar o que é deles

    void Start()
    {
        binocularCamera.gameObject.SetActive(false);

        if (textoAviso == null && !string.IsNullOrEmpty(nomeTextoAviso))
        {
            GameObject obj = GameObject.Find(nomeTextoAviso);
            if (obj != null) textoAviso = obj.GetComponent<TextMeshProUGUI>();
            if (textoAviso == null) Debug.LogWarning(name + ": não encontrei o texto \"" + nomeTextoAviso + "\" no Canvas");
        }
    }

    void Update()
    {
        if (!playerNearby) return;

        if (Keyboard.current != null && Keyboard.current[tecla].wasPressedThisFrame)
        {
            usingBinoculars = !usingBinoculars;

            playerCamera.gameObject.SetActive(!usingBinoculars);
            binocularCamera.gameObject.SetActive(usingBinoculars);
            if (miniMapa != null) miniMapa.SetActive(!usingBinoculars);
        }

        MostrarTexto("[" + tecla + "] " + (usingBinoculars ? textoLargar : textoUsar));
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
            Apagar();

            if (usingBinoculars)
            {
                usingBinoculars = false;
                playerCamera.gameObject.SetActive(true);
                if (miniMapa != null) miniMapa.SetActive(true);
                binocularCamera.gameObject.SetActive(false);
            }
        }
    }

    void MostrarTexto(string texto)
    {
        if (textoAviso == null) return;
        if (textoAviso.text != texto) textoAviso.text = texto;
        ultimoTexto = texto;
    }

    // apaga o aviso, mas só se ainda for o dos binóculos (o texto é partilhado com outros objetos)
    void Apagar()
    {
        if (textoAviso != null && ultimoTexto != null && textoAviso.text == ultimoTexto) textoAviso.text = "";
        ultimoTexto = null;
    }
}
