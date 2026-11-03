using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// Fica no objeto-pai do terminal (Terminal_Zona), que tem um Box Collider com Is Trigger:
// a zona onde o jogador tem de estar para ativar. O cubo do terminal é filho.
public class TerminalAutodestruicao : MonoBehaviour
{
    [Header("Ativação")]
    public Key tecla = Key.E;                 // tecla para ativar
    public PortaComando[] portas;             // portas da sala de comando (basta uma aberta)
    public Enemy lider;                       // líder inimigo: tem de estar morto para ativar (vazio = não verifica)

    [Header("Explosão")]
    public float tempoContagem = 60f;         // segundos até explodir
    public float raioExplosao = 40f;          // metros destruídos à volta do terminal
    public LayerMask camadasDestruir = ~0;    // o que pode ser destruído (tira o Ground)
    public GameObject efeitoExplosao;         // opcional: prefab de partículas
    public AudioClip somExplosao;             // opcional: som da explosão

    [Header("HUD")]
    public TextMeshProUGUI textoTimer;        // texto no Canvas para o aviso e a contagem
    public TextMeshProUGUI textoResultado;    // texto grande no meio do ecrã: ganhaste / perdeste

    InventarioPlayer inventario;              // o do jogador, só enquanto está dentro da zona
    bool ativado = false;
    bool terminado = false;                   // passa a verdadeiro quando já se ganhou ou perdeu
    float tempoRestante;
    
    public bool Ativado => ativado;   // o minimapa lê isto para saber se a contagem já começou

    void Start()
    {
        if (textoTimer != null) textoTimer.text = "";
        if (textoResultado != null) textoResultado.text = "";
    }

    void Update()
    {
        if (!ativado)
            VerificarAtivacao();
        else
            Contagem();
    }

    void OnTriggerEnter(Collider other)
    {
        // só o Player tem InventarioPlayer; inimigos e balas são ignorados
        InventarioPlayer inv = other.GetComponentInParent<InventarioPlayer>();
        if (inv == null) return;

        inventario = inv;
        if (!ativado && textoTimer != null)
            textoTimer.text = TextoAviso();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<InventarioPlayer>() == null) return;

        inventario = null;
        if (!ativado && textoTimer != null)
            textoTimer.text = "";
    }

    void VerificarAtivacao()
    {
        // só funciona com o jogador dentro da zona
        if (inventario == null || Keyboard.current == null) return;
        
        // atualiza o aviso a cada frame: muda sozinho quando o líder morre com o jogador na zona
        if (textoTimer != null) textoTimer.text = TextoAviso();
        
        if (!Keyboard.current[tecla].wasPressedThisFrame) return;

        if (!inventario.temCartao)
        {
            Debug.Log("Acesso negado: precisa do cartão de acesso");
            return;
        }
        if (!PortaAberta())
        {
            Debug.Log("Acesso negado: a porta da sala de comando está fechada");
            return;
        }
        if (!LiderMorto())
        {
            Debug.Log("Acesso negado: o líder ainda está vivo");
            return;
        }

        ativado = true;
        tempoRestante = tempoContagem;
        Debug.Log("Autodestruição ativada: " + tempoContagem + " s para fugir");
    }

    // pelo menos uma das portas ligadas já abriu
    bool PortaAberta()
    {
        if (portas == null || portas.Length == 0) return true;   // sem portas ligadas, não verifica
        foreach (PortaComando p in portas)
            if (p != null && p.EstaAberta) return true;
        return false;
    }

    // o líder já morreu. Também conta como morto quando já foi destruído (5 s depois de morrer)
    bool LiderMorto()
    {
        if (lider == null) return true;        // sem líder ligado, ou já destruído
        return lider.presentHealth <= 0;
    }

    void Contagem()
    {
        tempoRestante -= Time.deltaTime;

        if (textoTimer != null)
        {
            int segundos = Mathf.CeilToInt(Mathf.Max(tempoRestante, 0f));
            textoTimer.text = "Autodestruição: " + segundos + " s\nFuja para o abrigo!";
        }

        if (tempoRestante <= 0f)
            Explodir();
    }

    // chamado pelo PlayerStats quando o jogador morre
    public void JogadorMorreu()
    {
        if (!ativado || terminado) return;   // só conta durante a contagem

        terminado = true;
        enabled = false;                     // desliga o Update: a contagem pára
        if (textoTimer != null) textoTimer.text = "";
        MostrarResultado("SINAL PERDIDO\nPerdeste");
    }

    void MostrarResultado(string mensagem)
    {
        Debug.Log(mensagem);
        if (textoResultado != null) textoResultado.text = mensagem;
        BloquearCursor.FimDeJogo();   //soltar o rato
    }

    void Explodir()
    {
        terminado = true;  // a morte pela explosão já não passa pelo JogadorMorreu
        enabled = false;   // desliga o Update: a explosão só acontece uma vez
        if (textoTimer != null) textoTimer.text = "";

        if (efeitoExplosao != null)
            Instantiate(efeitoExplosao, transform.position, Quaternion.identity);
        if (somExplosao != null)
            AudioSource.PlayClipAtPoint(somExplosao, transform.position);

        // o jogador está dentro do raio? (por distância, não depende das Camadas Destruir)
        PlayerStats statsJogador = FindFirstObjectByType<PlayerStats>();
        bool jogadorApanhado = statsJogador != null &&
            Vector3.Distance(statsJogador.transform.position, transform.position) <= raioExplosao;

        // apanha todos os colliders dentro da esfera, só nas camadas escolhidas
        Collider[] atingidos = Physics.OverlapSphere(transform.position, raioExplosao, camadasDestruir);

        foreach (Collider c in atingidos)
        {
            // o Player não é destruído (levava a câmara com ele)
            if (c.GetComponentInParent<InventarioPlayer>() != null) continue;

            Destroy(c.gameObject);
        }

        if (jogadorApanhado)
        {
            statsJogador.TakeDamage(9999f);   // tira-lhe a vida toda
            MostrarResultado("SINAL PERDIDO\nPerdeste");
        }
        else
        {
            MostrarResultado("MISSÃO CUMPRIDA\nGanhaste");
        }

        Destroy(gameObject);   // a zona e o terminal (filho)
    }

    // texto no ecrã enquanto o jogador está na zona do terminal
    string TextoAviso()
    {
        if (!LiderMorto()) return "Mata o líder antes de ativar a autodestruição";
        return "[" + tecla + "] Ativar autodestruição";
    }
    
    
    // desenha na Scene o raio da explosão (vermelho)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, raioExplosao);
    }
}