using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// Dispositivo eletromagnético (emissor). Enquanto está ativo, dentro da área da estática do minimapa
// (as ZonaEstatica do edifício) o fato fica com a energia a 0 e o minimapa fica em estática.
// Sabotar: manter E carregado durante uns segundos junto ao emissor. Largar a meio recomeça do zero.
// Ao sair da área, ou depois de sabotado, a energia recomeça do 0 e regenera como de costume.
// Depois de sabotado, o fato e o minimapa voltam a funcionar até ao fim do nível.
// Tem gerador próprio: não depende da rede elétrica. Sabotar um QuadroEletrico não o desliga.
// Fica no objeto-pai (Emissor_EM), que tem um Box Collider com Is Trigger: a zona onde o jogador
// tem de estar para sabotar. O cubo (mais tarde o modelo 3D) é filho.
public class EmissorEletromagnetico : MonoBehaviour
{
    [Header("Estado inicial")]
    [Tooltip("Ligado: o emissor funciona desde o início do nível. Desligado: só começa quando alguém chamar Ativar() (por exemplo, ao apanhar o cartão).")]
    public bool ativoAoIniciar = true;

    [Header("Sabotagem")]
    public Key tecla = Key.E;                 // tecla a manter carregada
    public float tempoSabotar = 5f;           // segundos com a tecla carregada

    [Header("O que se apaga ao sabotar (opcional)")]
    // objetos que se desativam: partículas, faíscas, luzes azuis à volta das bobinas...
    public GameObject[] efeitosAtivos;
    // pequena luz (por exemplo azul) no emissor: apaga-se ao sabotar
    public Light luzEstado;
    // malhas com material emissivo (bobinas, ecrãs): continuam a brilhar mesmo com a Light desligada
    public Renderer[] partesBrilhantes;
    // material sem emissão para pôr nessas malhas ao sabotar
    public Material materialDesligado;

    [Header("Sons (opcionais)")]
    // Audio Source no emissor, com Play On Awake desligado e Spatial Blend a 1 (o script escolhe o som)
    public AudioSource audioSource;
    public AudioClip somZumbido;              // em loop enquanto o emissor está ativo
    public AudioClip somSabotar;              // em loop enquanto manténs a tecla
    public AudioClip somDesligar;             // uma vez, ao acabar de sabotar

    [Header("HUD")]
    // texto no Canvas para o aviso e a percentagem (pode ser o mesmo do quadro e do terminal)
    public TextMeshProUGUI textoAviso;
    // opcional: um objeto do Canvas (por exemplo "FATO SEM ENERGIA") que pisca enquanto o campo te apanha
    public GameObject avisoCampo;

    [Header("Estado (ver durante o Play)")]
    public bool ativo;
    public bool sabotado;
    [Range(0f, 1f)] public float progresso;
    public bool jogadorNoCampo;               // o campo está a cortar a energia do fato neste momento

    public static EmissorEletromagnetico Instancia { get; private set; }

    PlayerStats alvo;                         // o jogador (para lhe cortar a energia)
    PlayerStats jogadorPerto;                 // o jogador, só enquanto está na zona de sabotar
    float tempoCarregado;

    void Awake()
    {
        Instancia = this;
    }

    void Start()
    {
        ativo = ativoAoIniciar;
        alvo = FindFirstObjectByType<PlayerStats>();

        if (EstaticaMinimapa.Instancia == null)
            Debug.LogWarning(name + ": não há EstaticaMinimapa na cena. O emissor usa a área da estática, por isso o campo não vai funcionar");

        // o texto e o aviso começam escondidos
        MostrarTexto("");
        if (avisoCampo != null) avisoCampo.SetActive(false);
    }

    void Update()
    {
        AtualizarCampo();
        AtualizarSabotagem();
        AtualizarSom();
    }

    // depois de todos os Update (regeneração, estações de energia...), volta a pôr a energia a 0
    void LateUpdate()
    {
        if (jogadorNoCampo) ManterSemEnergia();
    }

    // ---------- Campo ----------

    void AtualizarCampo()
    {
        bool funciona = ativo && !sabotado;
        EstaticaMinimapa estatica = EstaticaMinimapa.Instancia;

        // a estática do minimapa obedece ao emissor
        if (estatica != null) estatica.bloqueado = funciona;

        bool dentro = funciona && estatica != null && estatica.DentroDaZona
                      && alvo != null && !alvo.isDead;

        if (dentro && !jogadorNoCampo) EntrarNoCampo();
        else if (!dentro && jogadorNoCampo) SairDoCampo();

        if (jogadorNoCampo) ManterSemEnergia();

        if (avisoCampo != null)
        {
            // pisca 2 vezes por segundo, como o "SEM SINAL"
            bool pisca = jogadorNoCampo && Mathf.Repeat(Time.time * 2f, 1f) < 0.6f;
            if (avisoCampo.activeSelf != pisca) avisoCampo.SetActive(pisca);
        }
    }

    void EntrarNoCampo()
    {
        jogadorNoCampo = true;
        float tinha = alvo.energy;

        // desliga logo todos os aprimoramentos ligados
        SuitAbilities poderes = alvo.GetComponent<SuitAbilities>();
        if (poderes != null) poderes.CancelAll();

        alvo.energy = 0f;
        Debug.Log("Campo eletromagnético: fato sem energia (perdeu " + tinha.ToString("0") + ")");
    }

    void SairDoCampo()
    {
        jogadorNoCampo = false;

        if (alvo == null || alvo.isDead) return;
        // a energia fica a 0. Gastar 0 só recomeça a contagem do PlayerStats:
        // a regeneração arranca ao fim de Regen Delay segundos (o fato a reiniciar)
        alvo.DrainEnergy(0f);
        Debug.Log("Fora do campo eletromagnético: o fato recomeça do 0");
    }

    void ManterSemEnergia()
    {
        if (alvo != null) alvo.energy = 0f;
    }

    // Liga o emissor (por exemplo quando o jogador apanha o cartão). Não faz nada se já foi sabotado.
    public void Ativar()
    {
        if (sabotado) return;
        ativo = true;
        Debug.Log(name + ": emissor eletromagnético ativado");
    }

    // ---------- Sabotagem ----------

    void AtualizarSabotagem()
    {
        if (sabotado) return;

        // saiu da zona ou morreu (o objeto do jogador é destruído pouco depois)
        if (jogadorPerto == null || jogadorPerto.isDead)
        {
            if (tempoCarregado > 0f) Largar();
            return;
        }
        if (Keyboard.current == null) return;

        if (Keyboard.current[tecla].isPressed)
        {
            tempoCarregado += Time.deltaTime;
            progresso = Mathf.Clamp01(tempoCarregado / tempoSabotar);
            MostrarTexto("A sabotar o emissor... " + Mathf.RoundToInt(progresso * 100f) + "%");

            if (tempoCarregado >= tempoSabotar) Sabotar();
        }
        else
        {
            // largou a tecla a meio: recomeça do zero
            if (tempoCarregado > 0f) Largar();
            MostrarTexto(TextoAviso());
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // só o Player tem PlayerStats; inimigos e balas são ignorados
        PlayerStats p = other.GetComponentInParent<PlayerStats>();
        if (p == null) return;

        jogadorPerto = p;
        if (alvo == null) alvo = p;
        if (!sabotado) MostrarTexto(TextoAviso());
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerStats>() == null) return;

        jogadorPerto = null;
        Largar();
        MostrarTexto("");
    }

    // desliga o emissor até ao fim do nível. Pública para o Modo Debug (ou outro script) a poder chamar
    public void Sabotar()
    {
        if (sabotado) return;
        sabotado = true;
        ativo = false;
        progresso = 1f;
        tempoCarregado = 0f;

        // o fato reinicia do 0 e a estática sai (o mesmo que sair do campo)
        if (jogadorNoCampo) SairDoCampo();
        if (EstaticaMinimapa.Instancia != null) EstaticaMinimapa.Instancia.bloqueado = false;
        if (avisoCampo != null) avisoCampo.SetActive(false);

        foreach (GameObject efeito in efeitosAtivos)
            if (efeito != null) efeito.SetActive(false);

        if (luzEstado != null) luzEstado.enabled = false;

        if (materialDesligado != null)
            foreach (Renderer r in partesBrilhantes)
            {
                if (r == null) continue;
                // troca todos os materiais da malha (pode ter mais do que um)
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = materialDesligado;
                r.sharedMaterials = mats;
            }

        AtualizarSom();
        if (audioSource != null && somDesligar != null) audioSource.PlayOneShot(somDesligar);

        MostrarTexto("");
        Debug.Log(name + ": emissor eletromagnético sabotado. O fato recomeça do 0 e o minimapa volta");
    }

    void Largar()
    {
        tempoCarregado = 0f;
        progresso = 0f;
    }

    string TextoAviso()
    {
        return "[" + tecla + "] Manter para sabotar o emissor eletromagnético";
    }

    void MostrarTexto(string texto)
    {
        if (textoAviso != null && textoAviso.text != texto) textoAviso.text = texto;
    }

    // ---------- Som ----------

    // escolhe o loop certo: faíscas enquanto sabotas, zumbido enquanto funciona, nada depois de sabotado
    void AtualizarSom()
    {
        if (audioSource == null) return;

        AudioClip certo = null;
        if (!sabotado)
        {
            if (tempoCarregado > 0f && somSabotar != null) certo = somSabotar;
            else if (ativo) certo = somZumbido;
        }

        if (audioSource.clip == certo && (certo == null || audioSource.isPlaying)) return;

        audioSource.Stop();
        audioSource.clip = certo;
        audioSource.loop = true;
        if (certo != null) audioSource.Play();
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }
}
