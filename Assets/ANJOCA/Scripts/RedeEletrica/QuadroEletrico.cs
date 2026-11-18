using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Serialization;

// Quadro elétrico de uma ala. Fica no objeto-pai (Quadro_Eletrico), que tem um Box Collider com Is Trigger:
// a zona onde o jogador tem de estar para sabotar. O cubo (mais tarde o modelo 3D) é filho.
// Sabotar: manter E carregado durante uns segundos. Largar a meio recomeça do zero.
// Ao acabar, a ala fica sem energia até ao fim do nível: apaga as luzes, desliga as câmaras e as estações de energia.
// As zonas escuras (ZonaSemEnergia) apontam para este quadro: os soldados que lá estiverem
// acendem a lanterna e passam a ver menos (Enemy.cs). Os drones não são afetados (têm bateria própria).
public class QuadroEletrico : MonoBehaviour
{
    [Header("Sabotagem")]
    public Key tecla = Key.E;                 // tecla a manter carregada
    public float tempoSabotar = 4f;           // segundos com a tecla carregada
    public float energiaDreno = 30f;          // energia que o fato recebe ao acabar (0 = nenhuma)

    [Header("O que este quadro alimenta")]
    // objetos que se desativam no corte: os pais com as luzes da ala, os holofotes, a luz de cada estação...
    public GameObject[] luzesDaAla;
    // objetos que se ativam no corte: as luzes de emergência (vermelhas e fracas). Começam desativados
    public GameObject[] luzesEmergencia;
    // câmaras de vigilância da ala: ficam desligadas até ao fim do nível
    public CamaraVigilancia[] camaras;
    // o trigger (Box Collider com Is Trigger) de cada estação de energia da ala: sem ele, não carrega.
    // FormerlySerializedAs: o campo chamava-se "terminais"; assim não perdes o que já tinhas arrastado
    [FormerlySerializedAs("terminais")]
    public Collider[] estacoesDeEnergia;
    // opcional: Volume do HDRP com a exposição para a ala às escuras. Começa desativado
    public GameObject volumeEscuro;

    [Header("Lâmpadas que brilham (opcional)")]
    // malhas com material emissivo (lâmpadas, ecrãs): continuam a brilhar mesmo com a Light desligada
    public Renderer[] lampadas;
    // material sem emissão para pôr nessas malhas no corte
    public Material materialApagado;

    [Header("Luz do próprio quadro (opcional)")]
    // pequena luz (por exemplo verde) no quadro: apaga-se no corte
    public Light luzEstado;

    [Header("Sons (opcionais)")]
    public AudioSource audioSource;
    public AudioClip somSabotar;              // toca em loop enquanto manténs a tecla (faíscas)
    public AudioClip somCorte;                // a energia a ir abaixo

    [Header("HUD")]
    // texto no Canvas para o aviso e a percentagem. Vazio = procura no Start o objeto com o nome abaixo
    public TextMeshProUGUI textoAviso;
    // nome do objeto de texto no Canvas (o mesmo para todos os quadros, por isso o prefab não precisa de ligação)
    public string nomeTextoAviso = "RedeEletricaTexto";

    [Header("Estado (ver durante o Play)")]
    public bool sabotado = false;
    [Range(0f, 1f)] public float progresso = 0f;

    PlayerStats jogador;                      // o do jogador, só enquanto está dentro da zona
    float tempoCarregado = 0f;

    void Start()
    {
        // o prefab não guarda ligações à cena: se o Texto Aviso vier vazio, procura-o pelo nome.
        // GameObject.Find só encontra objetos ativos, por isso o RedeEletricaTexto tem de estar ativo
        if (textoAviso == null && !string.IsNullOrEmpty(nomeTextoAviso))
        {
            GameObject obj = GameObject.Find(nomeTextoAviso);
            if (obj != null) textoAviso = obj.GetComponent<TextMeshProUGUI>();
            if (textoAviso == null) Debug.LogWarning(name + ": não encontrei o texto \"" + nomeTextoAviso + "\" no Canvas");
        }

        // o texto começa vazio (senão aparecia o "New Text" do TextMeshPro até entrares na zona)
        MostrarTexto("");
    }

    void Update()
    {
        if (sabotado) return;

        // saiu da zona ou morreu (o objeto do jogador é destruído pouco depois)
        if (jogador == null || jogador.isDead)
        {
            if (tempoCarregado > 0f) Largar();
            return;
        }
        if (Keyboard.current == null) return;

        if (Keyboard.current[tecla].isPressed)
        {
            // começou agora a carregar: som das faíscas em loop
            if (tempoCarregado == 0f) TocarLoop(true);

            tempoCarregado += Time.deltaTime;
            progresso = Mathf.Clamp01(tempoCarregado / tempoSabotar);
            MostrarTexto("A sabotar... " + Mathf.RoundToInt(progresso * 100f) + "%");

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

        jogador = p;
        if (!sabotado) MostrarTexto(TextoAviso());
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerStats>() == null) return;

        jogador = null;
        Largar();
        MostrarTexto("");
    }

    // corta a energia da ala. Pública para mais tarde o tiro (Weapon) ou o Modo Debug a poderem chamar
    public void Sabotar()
    {
        if (sabotado) return;
        sabotado = true;
        progresso = 1f;
        tempoCarregado = 0f;

        foreach (GameObject luz in luzesDaAla)
            if (luz != null) luz.SetActive(false);

        foreach (GameObject luz in luzesEmergencia)
            if (luz != null) luz.SetActive(true);

        // Mathf.Infinity = até ao fim do nível
        foreach (CamaraVigilancia camara in camaras)
            if (camara != null) camara.Desligar(Mathf.Infinity);

        foreach (Collider estacao in estacoesDeEnergia)
        {
            if (estacao == null) continue;
            estacao.enabled = false;
            // para os terminais elétricos do GDD: se tiverem uma função DefinirEnergia(bool), é chamada
            estacao.SendMessage("DefinirEnergia", false, SendMessageOptions.DontRequireReceiver);
        }

        if (volumeEscuro != null) volumeEscuro.SetActive(true);

        if (materialApagado != null)
            foreach (Renderer r in lampadas)
            {
                if (r == null) continue;
                // troca todos os materiais da malha (pode ter mais do que um)
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = materialApagado;
                r.sharedMaterials = mats;
            }

        if (luzEstado != null) luzEstado.enabled = false;

        TocarLoop(false);
        if (audioSource != null && somCorte != null) audioSource.PlayOneShot(somCorte);

        // o fato absorve a descarga do quadro
        if (jogador != null && energiaDreno > 0f) jogador.ChargeEnergy(energiaDreno);

        MostrarTexto("");
        Debug.Log(name + ": energia cortada");
    }

    void Largar()
    {
        tempoCarregado = 0f;
        progresso = 0f;
        TocarLoop(false);
    }

    void TocarLoop(bool tocar)
    {
        if (audioSource == null || somSabotar == null) return;

        if (tocar)
        {
            audioSource.clip = somSabotar;
            audioSource.loop = true;
            audioSource.Play();
        }
        else if (audioSource.clip == somSabotar && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }
    }

    string TextoAviso()
    {
        return "[" + tecla + "] Manter para sabotar o quadro elétrico";
    }

    void MostrarTexto(string texto)
    {
        if (textoAviso != null && textoAviso.text != texto) textoAviso.text = texto;
    }
}
