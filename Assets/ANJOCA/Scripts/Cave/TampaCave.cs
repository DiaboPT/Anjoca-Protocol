using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using TMPro;

// Tampa da cave. Vai no objeto Tampa_Cave (a dobradiça, na borda leste do buraco), que também leva
// um Box Collider com Is Trigger: a zona onde o jogador tem de estar para a abrir.
// Com o jogador na zona, aparece no ecrã "[E] Abrir a tampa da cave". Ao carregar em E, a dobradiça
// roda aos poucos de 0 para -90 em Z e a tampa fica de pé, aberta até ao fim do nível.
// Se o jogador estiver em cima da tampa (ou junto à dobradiça), pede para sair primeiro: a tampa ao rodar
// passava por dentro dele e ele caía no buraco.
// Opcional: um NavMeshObstacle (com Carve) por cima do 2.º lanço, que impede os soldados de descerem
// enquanto a tampa está fechada. Ao abrir, desliga-se e os soldados passam a poder usar a escada.
public class TampaCave : MonoBehaviour
{
    [Header("Abrir")]
    public Key tecla = Key.E;
    [Tooltip("Rotação Z da dobradiça com a tampa aberta. Se a tampa rodar para baixo do chão, troca para 90.")]
    public float anguloAberta = -90f;
    [Tooltip("Segundos que a tampa demora a abrir.")]
    public float duracao = 1.5f;

    [Header("Peças (vazias = procura sozinho)")]
    [Tooltip("O collider da chapa (o filho Tampa). Serve para saber se o jogador está em cima dela.")]
    public Collider chapa;
    [Tooltip("A zona onde o jogador tem de estar (Box Collider com Is Trigger neste objeto).")]
    public Collider zona;
    [Tooltip("Margem à volta da chapa, em metros, onde não deixa abrir (para a tampa não passar pelo jogador).")]
    public float margemSeguranca = 0.5f;

    [Header("NavMesh (opcional, passo 8)")]
    [Tooltip("NavMeshObstacle com Carve por cima do 2.º lanço. Desliga-se ao abrir a tampa.")]
    public NavMeshObstacle obstaculo;

    [Header("Som (opcional)")]
    public AudioSource audioSource;
    public AudioClip somAbrir;

    [Header("HUD")]
    // o mesmo texto do Canvas que o quadro elétrico, o terminal e o emissor usam.
    // Vazio = procura no Start o objeto com o nome abaixo
    public TextMeshProUGUI textoAviso;
    public string nomeTextoAviso = "RedeEletricaTexto";

    [Header("Estado (ver durante o Play)")]
    public bool aberta = false;

    PlayerStats jogador;            // o jogador, só enquanto está dentro da zona
    Quaternion rotacaoFechada;
    bool aAbrir = false;
    float tempo = 0f;
    string ultimoTexto = null;      // o que esta tampa escreveu, para só apagar o que é dela

    void Awake()
    {
        if (zona == null) zona = GetComponent<Collider>();
        if (chapa == null)
        {
            Transform t = transform.Find("Tampa");
            if (t != null) chapa = t.GetComponent<Collider>();
        }
        if (chapa == null) Debug.LogWarning(name + ": não encontrei o collider da chapa (filho Tampa)");
        if (zona == null || !zona.isTrigger) Debug.LogWarning(name + ": falta o Box Collider com Is Trigger neste objeto");

        rotacaoFechada = transform.localRotation;
    }

    void Start()
    {
        if (textoAviso == null && !string.IsNullOrEmpty(nomeTextoAviso))
        {
            GameObject obj = GameObject.Find(nomeTextoAviso);
            if (obj != null) textoAviso = obj.GetComponent<TextMeshProUGUI>();
            if (textoAviso == null) Debug.LogWarning(name + ": não encontrei o texto \"" + nomeTextoAviso + "\" no Canvas");
        }
    }

    void Update()
    {
        if (aAbrir)
        {
            // roda aos poucos, a começar e a acabar devagar
            tempo += Time.deltaTime;
            float t = duracao > 0f ? Mathf.Clamp01(tempo / duracao) : 1f;
            float angulo = Mathf.SmoothStep(0f, anguloAberta, t);
            transform.localRotation = rotacaoFechada * Quaternion.Euler(0f, 0f, angulo);
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

        if (EmCimaDaTampa(jogador.transform.position))
        {
            MostrarTexto("Sai de cima da tampa para a abrir");
            return;
        }

        MostrarTexto("[" + tecla + "] Abrir a tampa da cave");

        if (Keyboard.current != null && Keyboard.current[tecla].wasPressedThisFrame)
            Abrir();
    }

    // pública para mais tarde um terminal, um cartão ou o Modo Debug a poderem abrir
    public void Abrir()
    {
        if (aberta) return;
        aberta = true;
        aAbrir = true;
        tempo = 0f;

        // os soldados passam a poder descer
        if (obstaculo != null) obstaculo.enabled = false;

        // a zona deixa de ser precisa (e não fica a rodar com a dobradiça a apanhar o jogador)
        if (zona != null) zona.enabled = false;

        if (audioSource != null && somAbrir != null) audioSource.PlayOneShot(somAbrir);

        Apagar();
    }

    // o jogador está por cima da chapa, ou a menos de margemSeguranca dela (contando com a dobradiça)?
    bool EmCimaDaTampa(Vector3 posicao)
    {
        if (chapa == null) return false;
        Bounds b = chapa.bounds;
        b.Expand(new Vector3(margemSeguranca * 2f, 0f, margemSeguranca * 2f));
        return posicao.x >= b.min.x && posicao.x <= b.max.x && posicao.z >= b.min.z && posicao.z <= b.max.z;
    }

    void OnTriggerEnter(Collider other)
    {
        // só o Player tem PlayerStats; inimigos e balas são ignorados
        PlayerStats p = other.GetComponentInParent<PlayerStats>();
        if (p == null) return;
        jogador = p;
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

    // apaga o aviso, mas só se ainda for o desta tampa (o texto é partilhado com o quadro, o terminal e o emissor)
    void Apagar()
    {
        if (textoAviso != null && ultimoTexto != null && textoAviso.text == ultimoTexto) textoAviso.text = "";
        ultimoTexto = null;
    }
}
