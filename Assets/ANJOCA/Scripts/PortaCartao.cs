using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using TMPro;

// Porta de correr que abre com o cartão de acesso. Feita para a porta bio-magnética F do piso 1,
// mas serve para outras portas iguais.
// Vai num objeto vazio (por exemplo Porta_F) que tem:
//  - um Box Collider com Is Trigger: a zona onde o jogador tem de estar para passar o cartão;
//  - um filho "Folha" (Cube com Box Collider): a folha da porta, que desliza para dentro da parede;
//  - opcional, um filho "Leitor" (o leitor de cartões, muda de cor) e uma Light (a luz do leitor).
// Sem o cartão: aparece "Acesso restrito..." e o leitor fica vermelho. Com o cartão: o leitor fica verde
// e aparece "[E] Passar o cartão". Ao carregar em E, a folha desliza e a porta fica aberta até ao fim do nível.
// Opcional: um NavMeshObstacle (com Carve) na folha, para os soldados não passarem com a porta fechada.
// Opcional: com o campo Cartao Lider preenchido, a porta abre com o cartão que o líder deixa cair (CartaoLider)
// em vez do cartão de acesso. É assim na porta da varanda.
// Ao abrir, desliga-se e os soldados passam a poder usar a porta.
public class PortaCartao : MonoBehaviour
{
    [Header("Abrir")]
    public Key tecla = Key.E;
    [Tooltip("Quanto a folha anda ao abrir, em metros, nos eixos deste objeto. X positivo = para leste, se o objeto não estiver rodado.")]
    public Vector3 deslocamento = new Vector3(3.05f, 0f, 0f);
    [Tooltip("Segundos que a porta demora a abrir.")]
    public float duracao = 1.2f;

    [Header("Cartão de acesso")]
    [Tooltip("Ligado: a porta só abre depois de o jogador apanhar o cartão (o temCartao do InventarioPlayer).")]
    public bool precisaCartao = true;
    [Tooltip("Texto sem o cartão.")]
    public string textoSemCartao = "Acesso restrito. Precisas do cartão de acesso";
    [Tooltip("Texto com o cartão, depois de [E].")]
    public string textoComCartao = "Passar o cartão";
    [Tooltip("Opcional. Preenchido: a porta abre com o cartão do líder (CartaoLider) em vez do cartão de acesso.")]
    public CartaoLider cartaoLider;

    [Header("Peças (vazias = procura sozinho)")]
    [Tooltip("A folha da porta (o filho Folha).")]
    public Transform folha;
    [Tooltip("A zona onde o jogador tem de estar (Box Collider com Is Trigger neste objeto).")]
    public Collider zona;
    [Tooltip("O leitor de cartões (o filho Leitor). Muda de cor.")]
    public Renderer leitor;
    [Tooltip("A luz do leitor (uma Light filha deste objeto).")]
    public Light luzLeitor;
    public Color corTrancada = new Color(1f, 0.1f, 0.1f);
    public Color corDesbloqueada = new Color(0.1f, 1f, 0.2f);

    [Header("NavMesh (opcional)")]
    [Tooltip("NavMeshObstacle com Carve na folha. Vazio = procura na folha. Desliga-se ao abrir.")]
    public NavMeshObstacle obstaculo;

    [Header("Som (opcional)")]
    public AudioSource audioSource;
    public AudioClip somAbrir;
    [Tooltip("Som quando carregas em E sem o cartão.")]
    public AudioClip somNegado;

    [Header("HUD")]
    // o mesmo texto do Canvas que o quadro elétrico, o terminal, o emissor e a tampa usam.
    // Vazio = procura no Start o objeto com o nome abaixo
    public TextMeshProUGUI textoAviso;
    public string nomeTextoAviso = "RedeEletricaTexto";

    [Header("Estado (ver durante o Play)")]
    public bool aberta = false;

    PlayerStats jogador;            // o jogador, só enquanto está dentro da zona
    InventarioPlayer inventario;    // o inventário do jogador (para ver se tem o cartão)
    Vector3 posicaoFechada;
    bool aAbrir = false;
    float tempo = 0f;
    bool corAtualDesbloqueada;
    bool corPosta = false;
    string ultimoTexto = null;      // o que esta porta escreveu, para só apagar o que é dela

    void Awake()
    {
        if (zona == null) zona = GetComponent<Collider>();
        if (folha == null) folha = transform.Find("Folha");
        if (leitor == null)
        {
            Transform t = transform.Find("Leitor");
            if (t != null) leitor = t.GetComponent<Renderer>();
        }
        if (luzLeitor == null) luzLeitor = GetComponentInChildren<Light>();
        if (obstaculo == null && folha != null) obstaculo = folha.GetComponent<NavMeshObstacle>();

        if (folha == null) Debug.LogWarning(name + ": não encontrei a folha da porta (filho Folha)");
        if (zona == null || !zona.isTrigger) Debug.LogWarning(name + ": falta o Box Collider com Is Trigger neste objeto");

        if (folha != null) posicaoFechada = folha.localPosition;
    }

    void Start()
    {
        if (textoAviso == null && !string.IsNullOrEmpty(nomeTextoAviso))
        {
            GameObject obj = GameObject.Find(nomeTextoAviso);
            if (obj != null) textoAviso = obj.GetComponent<TextMeshProUGUI>();
            if (textoAviso == null) Debug.LogWarning(name + ": não encontrei o texto \"" + nomeTextoAviso + "\" no Canvas");
        }

        inventario = FindFirstObjectByType<InventarioPlayer>();
        if (precisaCartao && cartaoLider == null && inventario == null)
            Debug.LogWarning(name + ": não encontrei o InventarioPlayer. A porta não vai abrir com o cartão");

        AtualizarCor();
    }

    void Update()
    {
        AtualizarCor();

        if (aAbrir)
        {
            // desliza aos poucos, a começar e a acabar devagar
            tempo += Time.deltaTime;
            float t = duracao > 0f ? Mathf.Clamp01(tempo / duracao) : 1f;
            if (folha != null) folha.localPosition = posicaoFechada + deslocamento * Mathf.SmoothStep(0f, 1f, t);
            if (t >= 1f) aAbrir = false;
            return;
        }

        if (aberta) return;

        // saiu da zona ou morreu (o objeto do jogador é destruído pouco depois)
        if (jogador == null || jogador.isDead)
        {
            if (jogador != null) { jogador = null; Apagar(); }
            return;
        }

        bool carregou = Keyboard.current != null && Keyboard.current[tecla].wasPressedThisFrame;

        if (!Desbloqueada())
        {
            MostrarTexto(textoSemCartao);
            if (carregou && audioSource != null && somNegado != null) audioSource.PlayOneShot(somNegado);
            return;
        }

        MostrarTexto("[" + tecla + "] " + textoComCartao);
        if (carregou) Abrir();
    }

    // a porta já abre: não precisa do cartão, ou o jogador já o apanhou
    // (o do líder, se o campo Cartao Lider estiver preenchido; senão, o cartão de acesso)
    bool Desbloqueada()
    {
        if (!precisaCartao) return true;
        if (cartaoLider != null) return cartaoLider.apanhado;
        return inventario != null && inventario.temCartao;
    }

    // leitor e luz: vermelhos sem o cartão, verdes com ele (só muda quando é preciso)
    void AtualizarCor()
    {
        bool desbloqueada = aberta || Desbloqueada();
        if (corPosta && desbloqueada == corAtualDesbloqueada) return;
        corPosta = true;
        corAtualDesbloqueada = desbloqueada;
        Color cor = desbloqueada ? corDesbloqueada : corTrancada;
        if (luzLeitor != null) luzLeitor.color = cor;
        if (leitor != null) leitor.material.color = cor;
    }

    // pública para mais tarde um terminal, o lockdown ou o Modo Debug a poderem abrir
    public void Abrir()
    {
        if (aberta) return;
        aberta = true;
        aAbrir = true;
        tempo = 0f;

        // os soldados passam a poder usar a porta
        if (obstaculo != null) obstaculo.enabled = false;

        if (audioSource != null && somAbrir != null) audioSource.PlayOneShot(somAbrir);

        Apagar();
        AtualizarCor();
    }

    void OnTriggerEnter(Collider other)
    {
        // só o Player tem PlayerStats; inimigos e balas são ignorados
        PlayerStats p = other.GetComponentInParent<PlayerStats>();
        if (p == null) return;
        jogador = p;
        if (inventario == null) inventario = other.GetComponentInParent<InventarioPlayer>();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerStats>() == null) return;
        jogador = null;
        Apagar();
    }

    void MostrarTexto(string texto)
    {
        if (textoAviso == null) return;
        if (textoAviso.text != texto) textoAviso.text = texto;
        ultimoTexto = texto;
    }

    // apaga o aviso, mas só se ainda for o desta porta (o texto é partilhado com outros objetos)
    void Apagar()
    {
        if (textoAviso != null && ultimoTexto != null && textoAviso.text == ultimoTexto) textoAviso.text = "";
        ultimoTexto = null;
    }
}
