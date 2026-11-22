using UnityEngine;
using TMPro;

// Carga explosiva para apanhar. Fica num objeto com um Box Collider com Is Trigger ligado
// (a malha, por exemplo um Cube achatado, é filha e sem collider).
// Ao passar por ela, soma Quantidade ao InventarioPlayer.cargas, mostra um aviso e desaparece.
// A porta principal (PortaExplosivos) gasta uma carga.
// Guia: explicacoes/Porta_Principal_Explosivos.md
public class CargaExplosiva : MonoBehaviour
{
    public int quantidade = 1;
    public float velocidadeRotacao = 45f;  // graus por segundo, só para chamar a atenção (0 = parada)
    public AudioClip somApanhar;           // opcional

    [Header("HUD")]
    public TextMeshProUGUI textoAviso;
    public string nomeTextoAviso = "RedeEletricaTexto";
    [Tooltip("Segundos com o aviso no ecrã depois de apanhar.")]
    public float tempoAviso = 2.5f;

    bool apanhada = false;
    string ultimoTexto = null;

    void Start()
    {
        if (textoAviso == null && !string.IsNullOrEmpty(nomeTextoAviso))
        {
            GameObject obj = GameObject.Find(nomeTextoAviso);
            if (obj != null) textoAviso = obj.GetComponent<TextMeshProUGUI>();
        }
    }

    void Update()
    {
        if (!apanhada && velocidadeRotacao != 0f)
            transform.Rotate(0f, velocidadeRotacao * Time.deltaTime, 0f, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        if (apanhada) return;
        // só o Player tem InventarioPlayer; inimigos e balas são ignorados
        InventarioPlayer inventario = other.GetComponentInParent<InventarioPlayer>();
        if (inventario == null) return;

        apanhada = true;
        inventario.cargas += quantidade;
        Debug.Log("Carga explosiva apanhada. Cargas: " + inventario.cargas);

        if (somApanhar != null) AudioSource.PlayClipAtPoint(somApanhar, transform.position);

        if (textoAviso != null)
        {
            ultimoTexto = "Carga explosiva apanhada (" + inventario.cargas + ")";
            textoAviso.text = ultimoTexto;
        }

        // esconde já a carga, mas só a apaga depois do aviso (para o aviso poder ser limpo)
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Destroy(gameObject, tempoAviso);
    }

    void OnDestroy()
    {
        // apaga o aviso, mas só se ainda for o desta carga (o texto é partilhado)
        if (textoAviso != null && ultimoTexto != null && textoAviso.text == ultimoTexto) textoAviso.text = "";
    }
}
