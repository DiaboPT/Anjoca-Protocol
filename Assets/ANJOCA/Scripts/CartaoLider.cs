using UnityEngine;
using TMPro;

// Cartão que o líder deixa cair quando morre. Abre a porta da varanda (PortaCartao com o campo Cartao Lider).
// Vai num objeto vazio SEMPRE LIGADO (por exemplo Cartao_Lider) que tem:
//  - um Box Collider com Is Trigger: a zona onde o jogador apanha o cartão;
//  - um filho visível (o cartão) e, se quiseres, uma Light filha para se ver no chão.
// Fica escondido (sem se ver nem se apanhar) enquanto o líder está vivo. Quando o líder morre,
// aparece no chão por baixo do sítio onde ele estava, a rodar. O jogador apanha-o ao passar por cima.
public class CartaoLider : MonoBehaviour
{
    [Header("Líder")]
    [Tooltip("O Lider_Inimigo (o componente Enemy)")]
    public Enemy lider;
    [Tooltip("Metros acima do chão onde o cartão aparece")]
    public float alturaSobreChao = 0f;

    [Header("Aspeto")]
    [Tooltip("Graus por segundo a rodar, para se ver que se apanha. 0 = parado")]
    public float velocidadeRodar = 90f;

    [Header("Ao apanhar (opcional)")]
    public AudioClip somApanhar;
    [Tooltip("Texto no ecrã ao apanhar")]
    public string textoApanhar = "Apanhaste o cartão do líder. Abre a porta da varanda";
    [Tooltip("Segundos que o texto fica no ecrã")]
    public float duracaoTexto = 3f;
    // o mesmo texto do Canvas que as portas, o terminal e a tampa usam.
    // Vazio = procura no Start o objeto com o nome abaixo
    public TextMeshProUGUI textoAviso;
    public string nomeTextoAviso = "RedeEletricaTexto";

    [Header("Estado (ver durante o Play)")]
    public bool visivel = false;
    public bool apanhado = false;

    private Renderer[] renderers;
    private Light[] luzes;
    private Collider[] colliders;
    private Vector3 ultimaPosicaoLider;
    private bool liderVisto = false;     // já vimos o líder vivo pelo menos uma vez
    private string textoMostrado = null;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        luzes = GetComponentsInChildren<Light>(true);
        colliders = GetComponentsInChildren<Collider>(true);

        bool temTrigger = false;
        foreach (Collider c in colliders) if (c.isTrigger) temTrigger = true;
        if (!temTrigger) Debug.LogWarning(name + ": falta o Box Collider com Is Trigger neste objeto");

        // escondido até o líder morrer
        Mostrar(false);
    }

    private void Start()
    {
        if (lider == null) Debug.LogWarning(name + ": falta o Lider_Inimigo no campo Lider. O cartão nunca vai aparecer");

        if (textoAviso == null && !string.IsNullOrEmpty(nomeTextoAviso))
        {
            GameObject obj = GameObject.Find(nomeTextoAviso);
            if (obj != null) textoAviso = obj.GetComponent<TextMeshProUGUI>();
        }
    }

    private void Update()
    {
        if (apanhado) return;

        if (!visivel)
        {
            // enquanto o líder está vivo, guarda onde ele está
            if (lider != null && lider.presentHealth > 0f)
            {
                ultimaPosicaoLider = lider.transform.position;
                liderVisto = true;
                return;
            }

            // morreu (ou já foi destruído, 5 s depois de morrer): o cartão aparece onde ele estava
            if (liderVisto) Aparecer();
            return;
        }

        if (velocidadeRodar != 0f)
            transform.Rotate(0f, velocidadeRodar * Time.deltaTime, 0f, Space.World);
    }

    private void Aparecer()
    {
        transform.position = ChaoPorBaixo(ultimaPosicaoLider) + Vector3.up * alturaSobreChao;
        Mostrar(true);
        Debug.Log("O líder deixou cair o cartão da varanda");
    }

    // o primeiro chão por baixo do ponto, sem contar com o corpo do líder nem com triggers
    private Vector3 ChaoPorBaixo(Vector3 ponto)
    {
        Vector3 origem = ponto + Vector3.up * 0.5f;
        RaycastHit[] hits = Physics.RaycastAll(origem, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        float melhor = float.MaxValue;
        Vector3 chao = ponto;
        foreach (RaycastHit h in hits)
        {
            if (lider != null && h.collider.transform.IsChildOf(lider.transform)) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < melhor) { melhor = h.distance; chao = h.point; }
        }
        return chao;
    }

    private void Mostrar(bool sim)
    {
        visivel = sim;
        foreach (Renderer r in renderers) if (r != null) r.enabled = sim;
        foreach (Light l in luzes) if (l != null) l.enabled = sim;
        foreach (Collider c in colliders) if (c != null) c.enabled = sim;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!visivel || apanhado) return;

        // só o jogador vivo apanha (é o único com PlayerStats)
        PlayerStats stats = other.GetComponentInParent<PlayerStats>();
        if (stats == null || stats.isDead) return;

        apanhado = true;
        Mostrar(false);
        if (somApanhar != null) AudioSource.PlayClipAtPoint(somApanhar, transform.position);

        if (textoAviso != null && !string.IsNullOrEmpty(textoApanhar))
        {
            textoAviso.text = textoApanhar;
            textoMostrado = textoApanhar;
            Invoke(nameof(ApagarTexto), duracaoTexto);
        }
        Debug.Log("Apanhado: cartão do líder");
    }

    // apaga o aviso, mas só se ainda for o deste cartão (o texto é partilhado com outros objetos)
    private void ApagarTexto()
    {
        if (textoAviso != null && textoMostrado != null && textoAviso.text == textoMostrado) textoAviso.text = "";
        textoMostrado = null;
    }
}
