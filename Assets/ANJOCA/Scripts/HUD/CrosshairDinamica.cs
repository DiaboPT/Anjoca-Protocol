using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Cruz dinâmica do ANJOCA-01: quatro traços (e um ponto) que se afastam ao andar e ao disparar
// e voltam a fechar quando o jogador está parado. Fica vermelha quando aponta a um inimigo.
// Pôr num objeto vazio dentro do Canvas (o script cria os traços sozinho, não precisa de imagens).
public class CrosshairDinamica : MonoBehaviour
{
    [Header("Aspeto (em píxeis do Canvas)")]
    public float comprimento = 10f;      // comprimento de cada traço
    public float espessura = 2f;         // grossura de cada traço
    public float aberturaMinima = 6f;    // distância do centro a cada traço, com o jogador parado
    public float aberturaMaxima = 45f;   // nunca abre mais do que isto
    public bool mostrarPonto = true;     // ponto no centro
    public float tamanhoPonto = 2f;
    public Color corNormal = new Color(1f, 1f, 1f, 0.9f);
    public Color corInimigo = new Color(1f, 0.25f, 0.2f, 1f);
    public bool contorno = true;         // contorno escuro, para se ver em fundos claros
    public Color corContorno = new Color(0f, 0f, 0f, 0.6f);

    [Header("Movimento")]
    public float velocidadeCorrer = 8f;  // a esta velocidade (m/s) a abertura por andar é total
    public float aberturaAndar = 20f;    // quanto abre a correr (somado à mínima)

    [Header("Disparo")]
    public float aberturaPorTiro = 7f;   // quanto abre em cada tiro
    public float aberturaTirosMax = 25f; // máximo que os tiros seguidos conseguem abrir
    public float recuperacaoTiro = 45f;  // píxeis por segundo a voltar a fechar depois dos tiros

    [Header("Suavidade")]
    public float velocidadeAbrir = 20f;
    public float velocidadeFechar = 8f;

    [Header("Alvo (fica vermelha)")]
    public float alcance = 100f;
    public LayerMask camadasAlvo = ~0;   // o mesmo que o Hit Layers do Weapon

    [Header("Referências (vazio = procura sozinho)")]
    public Transform jogador;
    public Camera camara;

    // mira precisa = Precisão ligada (tecla 3) + botão direito premido (o mesmo que faz o zoom no PlayerMovement):
    // fecha a cruz e ignora movimento e tiros
    SuitAbilities aprimoramentos;
    PlayerStats stats;

    RectTransform cima, baixo, esquerda, direita, ponto;
    Image[] imagens;
    float aberturaAtual;
    float aberturaTiros;
    float velocidadeSuave;
    Vector3 ultimaPosicao;

    static CrosshairDinamica instancia;

    // chamado pelo Weapon em cada tiro: CrosshairDinamica.Disparo();
    public static void Disparo()
    {
        if (instancia == null) return;
        instancia.aberturaTiros = Mathf.Min(instancia.aberturaTiros + instancia.aberturaPorTiro, instancia.aberturaTirosMax);
    }

    void Awake()
    {
        instancia = this;

        cima = CriarTraco("Cima");
        baixo = CriarTraco("Baixo");
        esquerda = CriarTraco("Esquerda");
        direita = CriarTraco("Direita");
        ponto = CriarTraco("Ponto");
        imagens = GetComponentsInChildren<Image>();
        aberturaAtual = aberturaMinima;
    }

    void Start()
    {
        if (camara == null) camara = Camera.main;
        stats = FindFirstObjectByType<PlayerStats>();
        if (jogador == null && stats != null) jogador = stats.transform;
        if (stats != null) aprimoramentos = stats.GetComponent<SuitAbilities>();
        if (jogador != null) ultimaPosicao = jogador.position;
    }

    // true só com a Precisão ligada E o botão direito premido (e o jogador vivo)
    public bool MiraPrecisa {
        get {
            if (stats != null && stats.isDead) return false;
            bool precisaoLigada = aprimoramentos != null && aprimoramentos.precisionActive;
            bool botaoDireito = Mouse.current != null && Mouse.current.rightButton.isPressed;
            return precisaoLigada && botaoDireito;
        }
    }

    RectTransform CriarTraco(string nome)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;   // não apanha cliques do rato
        if (contorno)
        {
            Outline o = go.AddComponent<Outline>();
            o.effectColor = corContorno;
            o.effectDistance = new Vector2(1f, 1f);
        }
        return go.GetComponent<RectTransform>();
    }

    void Update()
    {
        // jogador morto: esconde a cruz
        bool visivel = stats == null || !stats.isDead;
        foreach (Image img in imagens) img.enabled = visivel;
        if (!visivel) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // velocidade no chão (só X e Z), a partir de quanto o jogador andou desde o último frame
        float velocidade = 0f;
        if (jogador != null)
        {
            Vector3 delta = jogador.position - ultimaPosicao;
            delta.y = 0f;
            velocidade = delta.magnitude / dt;
            ultimaPosicao = jogador.position;
        }
        velocidadeSuave = Mathf.Lerp(velocidadeSuave, velocidade, 10f * dt);

        // os tiros vão deixando de contar com o tempo
        aberturaTiros = Mathf.MoveTowards(aberturaTiros, 0f, recuperacaoTiro * dt);

        float alvo = aberturaMinima
                   + Mathf.Clamp01(velocidadeSuave / velocidadeCorrer) * aberturaAndar
                   + aberturaTiros;

        // mira precisa: mira estável, a cruz fica fechada
        if (MiraPrecisa)
            alvo = aberturaMinima;

        alvo = Mathf.Min(alvo, aberturaMaxima);

        // abre depressa, fecha devagar
        float rapidez = alvo > aberturaAtual ? velocidadeAbrir : velocidadeFechar;
        aberturaAtual = Mathf.Lerp(aberturaAtual, alvo, rapidez * dt);

        Posicionar();
        AtualizarCor();
    }

    void Posicionar()
    {
        float centro = aberturaAtual + comprimento * 0.5f;

        cima.sizeDelta = baixo.sizeDelta = new Vector2(espessura, comprimento);
        esquerda.sizeDelta = direita.sizeDelta = new Vector2(comprimento, espessura);

        cima.anchoredPosition = new Vector2(0f, centro);
        baixo.anchoredPosition = new Vector2(0f, -centro);
        esquerda.anchoredPosition = new Vector2(-centro, 0f);
        direita.anchoredPosition = new Vector2(centro, 0f);

        ponto.gameObject.SetActive(mostrarPonto);
        ponto.sizeDelta = new Vector2(tamanhoPonto, tamanhoPonto);
        ponto.anchoredPosition = Vector2.zero;
    }

    void AtualizarCor()
    {
        bool inimigo = false;

        // apanha também triggers, porque o Sphere Collider do drone é trigger (como no Weapon).
        // as outras caixas trigger estão na layer Ignore Raycast, por isso não contam
        if (camara != null && Physics.Raycast(camara.transform.position, camara.transform.forward,
                out RaycastHit hit, alcance, camadasAlvo, QueryTriggerInteraction.Collide))
        {
            Enemy soldado = hit.collider.GetComponentInParent<Enemy>();
            if (soldado != null && soldado.presentHealth > 0f) inimigo = true;
            if (hit.collider.GetComponentInParent<EnemyDrone>() != null) inimigo = true;
        }

        Color cor = inimigo ? corInimigo : corNormal;
        foreach (Image img in imagens) img.color = cor;
    }
}
