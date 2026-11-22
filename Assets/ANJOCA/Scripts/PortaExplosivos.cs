using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

// Porta principal do piso 0: está trancada e só abre a rebentar com uma carga explosiva.
// Vai num objeto vazio (Porta_Principal) que tem:
//  - um Box Collider com Is Trigger: a zona, do lado de fora, onde o jogador coloca a carga;
//  - um filho "Folha" (Cube com Box Collider e NavMeshObstacle com Carve): a porta fechada;
//  - um filho "Carga" (desligado no início): a carga colada à porta, com uma Light vermelha filha;
//  - um filho "PontoInvestigar" (vazio): para onde os soldados vão depois da explosão, dentro do átrio;
//  - opcional, um filho "Folha_Caida" (desligado): a porta deitada no chão depois da explosão;
//  - opcional, variantes de escombros (desligadas): uma delas pode aparecer e tapar parte da porta.
//
// Sem carga aparece "Porta blindada...". Com carga (InventarioPlayer.cargas), manter E uns segundos cola-a à porta.
// Largar a meio recomeça. Depois, em qualquer sítio, G detona (ou, com Rebentar Sozinha, rebenta ao fim do tempo).
// A explosão:
//  - fere quem estiver perto (jogador, soldados e drones): muito no centro, nada na borda do raio,
//    e nada se houver uma parede no meio;
//  - deita a folha abaixo. O NavMeshObstacle sai com ela, por isso os soldados passam a usar a porta;
//  - os soldados vivos do mesmo piso, a menos de Raio Alerta, vão ver o que foi (Enemy.Investigar);
//  - cada equipamento da lista (Danificavel) pode avariar, conforme a probabilidade dele;
//  - pode deixar escombros a tapar parte da passagem;
//  - faz-se ver e ouvir: fogo e fumo em grande, clarão, pedaços da porta a voar, a câmara a tremer
//    e, para quem está perto, apito nos ouvidos e som abafado uns segundos.
// Guia: explicacoes/Porta_Principal_Explosivos.md
public class PortaExplosivos : MonoBehaviour
{
    [Header("Colocar e detonar")]
    public Key teclaColocar = Key.E;
    [Tooltip("Segundos com E carregado para colar a carga à porta.")]
    public float tempoColocar = 3f;
    public Key teclaDetonar = Key.G;
    [Tooltip("Ligado: a carga rebenta sozinha ao fim de Tempo Rebentar segundos (G continua a funcionar).")]
    public bool rebentarSozinha = false;
    public float tempoRebentar = 8f;

    [Header("Explosão: dano")]
    [Tooltip("Metros à volta da carga onde a explosão fere. Na Scene aparece como uma esfera vermelha.")]
    public float raioDano = 6f;
    [Tooltip("Dano mesmo ao lado da carga. Desce até 0 na borda do raio.")]
    public float danoMaximo = 120f;
    [Tooltip("Ligado: quem estiver atrás de uma parede não leva dano.")]
    public bool paredesProtegem = true;

    [Header("Explosão: alerta")]
    [Tooltip("Metros à volta da carga. Os soldados vivos dentro deste raio vão investigar. Esfera amarela na Scene.")]
    public float raioAlerta = 20f;
    [Tooltip("Só conta quem tem os pés a menos disto da altura da porta, ou seja, quem está no mesmo piso.")]
    public float alturaAlerta = 3f;
    [Tooltip("Segundos que os soldados ficam a investigar antes de voltarem ao posto ou à patrulha.")]
    public float tempoInvestigar = 20f;
    [Tooltip("Metros de visão a mais enquanto investigam.")]
    public float bonusVisaoInvestigar = 4f;
    [Tooltip("Cada soldado vai para um ponto ao calhas até esta distância do PontoInvestigar, para não irem todos ao mesmo.")]
    public float espalhar = 1.5f;

    [Header("Equipamentos e escombros")]
    [Tooltip("Equipamentos que a explosão pode avariar. Cada um tem a sua probabilidade (no Danificavel).")]
    public Danificavel[] equipamentos;
    [Tooltip("Probabilidade de aparecerem escombros a tapar parte da porta (0 = nunca, 1 = sempre).")]
    [Range(0f, 1f)] public float probabilidadeEscombros = 0.6f;
    [Tooltip("Variantes de escombros (desligadas). Se houver escombros, liga-se uma delas ao calhas.")]
    public GameObject[] variantesEscombros;
    [Tooltip("Opcional: a porta deitada no chão (desligada). Liga-se sempre na explosão.")]
    public GameObject folhaCaida;

    [Header("Peças (vazias = procura sozinho)")]
    public Transform folha;
    public GameObject carga;
    [Tooltip("A luz vermelha da carga (filha da Carga). Pisca enquanto a carga está colocada.")]
    public Light luzCarga;
    public Transform pontoInvestigar;
    [Tooltip("O Box Collider com Is Trigger neste objeto.")]
    public Collider zona;

    [Header("Efeito visual")]
    [Tooltip("Prefab de partículas, por exemplo o Explosao do drone.")]
    public GameObject efeitoExplosao;
    public float duracaoEfeito = 6f;
    [Tooltip("O prefab do drone é pequeno: aqui fica X vezes maior (fogo, fumo e faíscas).")]
    public float escalaEfeito = 3f;
    [Tooltip("Rebentamentos extra logo a seguir ao primeiro, à volta da porta, para ficar maior e mais confuso.")]
    public int rebentamentosExtra = 3;
    [Tooltip("Metros à volta da carga onde aparecem os rebentamentos extra.")]
    public float espalharRebentamentos = 1.2f;
    [Tooltip("Opcional: uma Point Light desligada, filha deste objeto. Acende-se muito forte e apaga-se aos poucos.")]
    public Light luzExplosao;
    public float duracaoLuz = 0.7f;

    [Header("Pedaços da porta (física)")]
    [Tooltip("Pedaços da folha que saltam para dentro do átrio e ficam no chão. 0 = nenhum.")]
    public int pedacos = 14;
    [Tooltip("Velocidade com que saltam (m/s).")]
    public float forcaPedacos = 10f;
    public Vector2 tamanhoPedacos = new Vector2(0.15f, 0.5f);
    [Tooltip("Segundos até os pedaços desaparecerem.")]
    public float duracaoPedacos = 15f;

    [Header("No jogador")]
    [Tooltip("Até esta distância, a câmara treme (mais quanto mais perto).")]
    public float raioTremer = 30f;
    [Tooltip("Tremor ao pé da carga: graus de rotação da câmara.")]
    public float tremorMaximo = 4f;
    public float duracaoTremor = 0.9f;
    [Tooltip("Até esta distância, os ouvidos apitam e o som fica abafado uns segundos.")]
    public float raioAtordoar = 12f;
    public float segundosAbafado = 3.5f;
    [Tooltip("O apito nos ouvidos (Zumbido_Ouvidos). Toca só para quem está perto.")]
    public AudioClip somZumbido;

    [Header("Som da explosão")]
    [Tooltip("Por exemplo Explosao_Porta (ou os Drone_Explosao): escolhe um ao calhas.")]
    public AudioClip[] sonsExplosao;
    [Range(0f, 1f)] public float volumeExplosao = 1f;
    [Tooltip("Até esta distância ouve-se com o volume todo; depois vai baixando.")]
    public float distanciaSomCheio = 25f;
    [Tooltip("Distância máxima a que ainda se ouve.")]
    public float alcanceSom = 300f;

    [Header("Outros sons (opcionais)")]
    [Tooltip("Audio Source neste objeto, para os sons de colocar, do bip e de negado.")]
    public AudioSource audioSource;
    [Tooltip("Toca em loop enquanto manténs E.")]
    public AudioClip somColocar;
    [Tooltip("Toca a cada piscar da luz da carga.")]
    public AudioClip somBip;
    [Tooltip("Quando carregas em E sem carga.")]
    public AudioClip somNegado;
    [Tooltip("Segundos entre bips (e entre cada piscar da luz).")]
    public float intervaloBip = 1f;

    [Header("HUD")]
    // o mesmo texto do Canvas que o quadro elétrico, o terminal, o emissor e as portas de cartão usam.
    // Vazio = procura no Start o objeto com o nome abaixo
    public TextMeshProUGUI textoAviso;
    public string nomeTextoAviso = "RedeEletricaTexto";
    public string textoSemCarga = "Porta blindada. Precisas de uma carga explosiva";

    [Header("Estado (ver durante o Play)")]
    public bool cargaColocada = false;
    public bool rebentada = false;
    [Range(0f, 1f)] public float progresso = 0f;

    PlayerStats jogadorNaZona;      // o jogador, só enquanto está dentro da zona
    PlayerStats jogador;            // o jogador, sempre (para o G funcionar longe da porta)
    InventarioPlayer inventario;
    float tempoCarregado = 0f;
    float horaColocada = 0f;
    float proximoBip = 0f;
    string ultimoTexto = null;      // o que esta porta escreveu, para só apagar o que é dela

    void Awake()
    {
        if (zona == null) zona = GetComponent<Collider>();
        if (folha == null) folha = transform.Find("Folha");
        if (carga == null)
        {
            Transform t = transform.Find("Carga");
            if (t != null) carga = t.gameObject;
        }
        if (luzCarga == null && carga != null) luzCarga = carga.GetComponentInChildren<Light>(true);
        if (pontoInvestigar == null) pontoInvestigar = transform.Find("PontoInvestigar");
        if (folhaCaida == null)
        {
            Transform t = transform.Find("Folha_Caida");
            if (t != null) folhaCaida = t.gameObject;
        }
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (luzExplosao == null)
        {
            Transform t = transform.Find("Luz_Explosao");
            if (t != null) luzExplosao = t.GetComponent<Light>();
        }

        if (folha == null) Debug.LogWarning(name + ": não encontrei a folha da porta (filho Folha)");
        if (zona == null || !zona.isTrigger) Debug.LogWarning(name + ": falta o Box Collider com Is Trigger neste objeto");
        if (pontoInvestigar == null) Debug.LogWarning(name + ": sem PontoInvestigar, os soldados vão até à própria carga");

        // a carga só aparece depois de colocada
        if (carga != null) carga.SetActive(false);
        if (folhaCaida != null) folhaCaida.SetActive(false);
        if (luzExplosao != null) luzExplosao.gameObject.SetActive(false);
        foreach (GameObject e in variantesEscombros)
            if (e != null) e.SetActive(false);
    }

    void Start()
    {
        if (textoAviso == null && !string.IsNullOrEmpty(nomeTextoAviso))
        {
            GameObject obj = GameObject.Find(nomeTextoAviso);
            if (obj != null) textoAviso = obj.GetComponent<TextMeshProUGUI>();
            if (textoAviso == null) Debug.LogWarning(name + ": não encontrei o texto \"" + nomeTextoAviso + "\" no Canvas");
        }

        jogador = FindFirstObjectByType<PlayerStats>();
        inventario = FindFirstObjectByType<InventarioPlayer>();
        if (inventario == null) Debug.LogWarning(name + ": não encontrei o InventarioPlayer no Player. A carga não vai poder ser colocada");
    }

    void Update()
    {
        if (rebentada) return;

        if (cargaColocada)
        {
            PiscarCarga();

            bool vivo = jogador != null && !jogador.isDead;
            bool carregouG = Keyboard.current != null && Keyboard.current[teclaDetonar].wasPressedThisFrame;
            if (vivo && carregouG) { Rebentar(); return; }
            if (rebentarSozinha && Time.time - horaColocada >= tempoRebentar) { Rebentar(); return; }

            if (jogadorNaZona != null) MostrarTexto(TextoCargaColocada());
            return;
        }

        // saiu da zona ou morreu
        if (jogadorNaZona == null || jogadorNaZona.isDead)
        {
            if (tempoCarregado > 0f) Largar();
            if (jogadorNaZona != null) { jogadorNaZona = null; Apagar(); }
            return;
        }

        if (Keyboard.current == null) return;
        bool aCarregar = Keyboard.current[teclaColocar].isPressed;

        // sem carga: só o aviso (e o som de negado ao carregar em E)
        if (inventario == null || inventario.cargas <= 0)
        {
            MostrarTexto(textoSemCarga);
            if (Keyboard.current[teclaColocar].wasPressedThisFrame && audioSource != null && somNegado != null)
                audioSource.PlayOneShot(somNegado);
            return;
        }

        if (aCarregar)
        {
            // começou agora a carregar: som em loop
            if (tempoCarregado == 0f) TocarLoop(true);
            tempoCarregado += Time.deltaTime;
            progresso = Mathf.Clamp01(tempoCarregado / tempoColocar);
            MostrarTexto("A colocar a carga... " + Mathf.RoundToInt(progresso * 100f) + "%");
            if (tempoCarregado >= tempoColocar) ColocarCarga();
        }
        else
        {
            // largou a tecla a meio: recomeça do zero
            if (tempoCarregado > 0f) Largar();
            MostrarTexto("[" + teclaColocar + "] Manter para colocar a carga explosiva");
        }
    }

    // ---------- Colocar ----------
    void ColocarCarga()
    {
        cargaColocada = true;
        horaColocada = Time.time;
        proximoBip = Time.time;
        tempoCarregado = 0f;
        progresso = 1f;
        TocarLoop(false);

        if (inventario != null) inventario.cargas--;
        if (carga != null) carga.SetActive(true);
        Debug.Log(name + ": carga colocada");
    }

    // a luz acende-se um instante a cada intervaloBip, com um bip
    void PiscarCarga()
    {
        if (Time.time >= proximoBip)
        {
            proximoBip = Time.time + intervaloBip;
            if (audioSource != null && somBip != null) audioSource.PlayOneShot(somBip);
        }
        if (luzCarga != null)
        {
            // acesa durante os primeiros 0.15 s de cada intervalo
            bool acesa = proximoBip - Time.time > intervaloBip - 0.15f;
            if (luzCarga.enabled != acesa) luzCarga.enabled = acesa;
        }
    }

    string TextoCargaColocada()
    {
        if (rebentarSozinha)
        {
            int falta = Mathf.CeilToInt(tempoRebentar - (Time.time - horaColocada));
            return "Carga colocada. Afasta-te! " + Mathf.Max(falta, 0) + " s";
        }
        return "Carga colocada. Afasta-te e carrega em [" + teclaDetonar + "] para detonar";
    }

    // ---------- Rebentar ----------
    // pública para mais tarde o Modo Debug, ou um tiro na carga, a poderem chamar
    public void Rebentar()
    {
        if (rebentada) return;
        rebentada = true;
        cargaColocada = false;
        TocarLoop(false);

        // o centro da explosão é a carga (se não houver, a folha)
        Vector3 centro = carga != null ? carga.transform.position
                       : folha != null ? folha.position : transform.position;

        // o material da folha, para os pedaços (tem de ser lido antes de a desligar)
        Material materialFolha = null;
        if (folha != null)
        {
            Renderer r = folha.GetComponent<Renderer>();
            if (r != null) materialFolha = r.sharedMaterial;
        }

        // primeiro sai a porta, para a explosão chegar a quem está do outro lado
        if (carga != null) carga.SetActive(false);
        if (folha != null) folha.gameObject.SetActive(false);
        if (folhaCaida != null) folhaCaida.SetActive(true);
        // desligar o trigger não avisa o OnTriggerExit: o texto é apagado à mão
        if (zona != null) zona.enabled = false;
        jogadorNaZona = null;
        Apagar();

        CriarEfeito(centro);
        StartCoroutine(RebentamentosExtra(centro));
        if (luzExplosao != null) StartCoroutine(Clarao(centro));
        TocarExplosao(centro);

        Ferir(centro);
        Alertar(centro);
        DanificarEquipamentos();
        Escombros();
        LancarPedacos(centro, materialFolha);
        EfeitosNoJogador(centro);

        Debug.Log(name + ": porta rebentada");
    }

    // ---------- Efeito visual ----------
    // o prefab do drone, em grande: põe todas as partículas a seguir a escala do objeto (Hierarchy)
    void CriarEfeito(Vector3 ponto)
    {
        if (efeitoExplosao == null) return;
        GameObject efeito = Instantiate(efeitoExplosao, ponto, Quaternion.identity);
        foreach (ParticleSystem ps in efeito.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.MainModule main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        efeito.transform.localScale = Vector3.one * escalaEfeito;
        Destroy(efeito, duracaoEfeito);
    }

    // mais uns rebentamentos à volta da porta, uns centésimos de segundo depois do primeiro
    IEnumerator RebentamentosExtra(Vector3 centro)
    {
        for (int i = 0; i < rebentamentosExtra; i++)
        {
            yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
            Vector3 desvio = new Vector3(Random.Range(-espalharRebentamentos, espalharRebentamentos),
                                         Random.Range(-0.3f, 1.2f),
                                         Random.Range(-espalharRebentamentos, espalharRebentamentos));
            CriarEfeito(centro + desvio);
        }
    }

    // clarão: a luz acende-se com a intensidade do Inspector e apaga-se aos poucos, a tremer
    IEnumerator Clarao(Vector3 centro)
    {
        // sai de dentro da porta, para não ser desligada com ela
        luzExplosao.transform.SetParent(null, true);
        luzExplosao.transform.position = centro + Vector3.up * 0.5f;
        float intensidade = luzExplosao.intensity;
        luzExplosao.enabled = true;
        luzExplosao.gameObject.SetActive(true);

        float tempo = 0f;
        while (tempo < duracaoLuz)
        {
            tempo += Time.deltaTime;
            float t = tempo / duracaoLuz;
            luzExplosao.intensity = intensidade * (1f - t) * (1f - t) * Random.Range(0.7f, 1f);
            yield return null;
        }
        Destroy(luzExplosao.gameObject);
    }

    // pedaços da porta: cubos com Rigidbody, com o material da folha, que saltam para dentro do átrio
    void LancarPedacos(Vector3 centro, Material material)
    {
        // a carga está do lado de fora: a explosão empurra para dentro (o Z positivo deste objeto)
        Vector3 origem = centro - transform.forward * 0.8f;
        for (int i = 0; i < pedacos; i++)
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = "Pedaco_Porta";
            float lado = Random.Range(tamanhoPedacos.x, tamanhoPedacos.y);
            p.transform.localScale = new Vector3(lado, lado * Random.Range(0.5f, 1.5f), Random.Range(0.08f, 0.2f));
            p.transform.position = transform.position
                                 + transform.right * Random.Range(-1.2f, 1.2f)
                                 + Vector3.up * Random.Range(0.2f, 2.1f);
            p.transform.rotation = Random.rotation;
            if (material != null) p.GetComponent<Renderer>().sharedMaterial = material;

            Rigidbody rb = p.AddComponent<Rigidbody>();
            rb.mass = 5f;
            Vector3 direcao = (p.transform.position - origem).normalized + Vector3.up * Random.Range(0.1f, 0.5f);
            rb.AddForce(direcao.normalized * forcaPedacos * Random.Range(0.6f, 1.3f), ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 15f, ForceMode.VelocityChange);
            Destroy(p, duracaoPedacos);
        }
    }

    // ---------- Som ----------
    // um Audio Source só para a explosão, que se ouve forte até distanciaSomCheio
    // (o PlayClipAtPoint de antes perdia volume logo a 1 m, por isso ao longe quase não se ouvia)
    void TocarExplosao(Vector3 centro)
    {
        if (sonsExplosao == null || sonsExplosao.Length == 0) return;
        AudioClip som = sonsExplosao[Random.Range(0, sonsExplosao.Length)];
        if (som == null) return;

        GameObject obj = new GameObject("Som_Explosao_Porta");
        obj.transform.position = centro;
        AudioSource fonte = obj.AddComponent<AudioSource>();
        fonte.clip = som;
        fonte.volume = volumeExplosao;
        fonte.spatialBlend = 0.85f;          // quase todo 3D, mas sempre forte
        fonte.rolloffMode = AudioRolloffMode.Logarithmic;
        fonte.minDistance = distanciaSomCheio;
        fonte.maxDistance = alcanceSom;
        fonte.dopplerLevel = 0f;
        fonte.priority = 0;                  // nunca é cortado por outros sons
        if (audioSource != null) fonte.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        fonte.Play();
        Destroy(obj, som.length + 0.5f);
    }

    // ---------- No jogador ----------
    // câmara a tremer e, se estiver perto, ouvidos a apitar e som abafado
    void EfeitosNoJogador(Vector3 centro)
    {
        if (jogador == null || jogador.isDead) return;
        float distancia = Vector3.Distance(jogador.transform.position, centro);

        if (distancia < raioTremer)
        {
            float forca = 1f - distancia / raioTremer;
            StartCoroutine(Tremer(tremorMaximo * forca * forca + 0.3f));
        }

        if (distancia < raioAtordoar)
        {
            float forca = 1f - distancia / raioAtordoar;
            StartCoroutine(Abafar(segundosAbafado * (0.4f + 0.6f * forca)));
            if (somZumbido != null)
            {
                GameObject obj = new GameObject("Zumbido_Ouvidos");
                AudioSource fonte = obj.AddComponent<AudioSource>();
                fonte.clip = somZumbido;
                fonte.spatialBlend = 0f;                 // dentro da cabeça
                fonte.volume = 0.3f + 0.7f * forca;
                fonte.bypassListenerEffects = true;      // o abafado não o apanha
                fonte.Play();
                Destroy(obj, somZumbido.length + 0.2f);
            }
        }
    }

    // a câmara do jogador (a do PlayerMovement) treme uns instantes.
    // O PlayerMovement volta a pôr a rotação e a posição da câmara em cada Update; isto corre depois
    // (o yield return null continua a seguir aos Update), por isso só soma o tremor e não estraga nada
    IEnumerator Tremer(float graus)
    {
        // a câmara que o PlayerMovement mexe (se não houver, a Main Camera)
        Transform t = null;
        PlayerMovement movimento = FindFirstObjectByType<PlayerMovement>();
        if (movimento != null) t = movimento.cameraTransform;
        if (t == null && Camera.main != null) t = Camera.main.transform;
        if (t == null) yield break;

        float tempo = 0f;
        while (tempo < duracaoTremor)
        {
            yield return null;
            // morreu ou a câmara já não existe (o Player é destruído pouco depois de morrer)
            if (t == null || jogador == null || jogador.isDead) yield break;
            tempo += Time.deltaTime;
            float queda = 1f - tempo / duracaoTremor;
            float g = graus * queda * queda;
            t.localRotation *= Quaternion.Euler(Random.Range(-g, g), Random.Range(-g, g), Random.Range(-g, g) * 0.5f);
            t.localPosition += Random.insideUnitSphere * g * 0.01f;
        }
    }

    // som abafado: um filtro no Audio Listener corta os agudos de tudo e volta ao normal aos poucos
    IEnumerator Abafar(float segundos)
    {
        AudioListener ouvinte = FindFirstObjectByType<AudioListener>();
        if (ouvinte == null) yield break;

        // deixa passar o estrondo, e só depois abafa (o resto da explosão e os estilhaços)
        yield return new WaitForSeconds(0.12f);

        AudioLowPassFilter filtro = ouvinte.GetComponent<AudioLowPassFilter>();
        if (filtro == null) filtro = ouvinte.gameObject.AddComponent<AudioLowPassFilter>();
        filtro.enabled = true;

        float tempo = 0f;
        while (tempo < segundos)
        {
            // o jogador morreu e a câmara (com o Audio Listener) foi destruída
            if (filtro == null) yield break;
            tempo += Time.deltaTime;
            float t = tempo / segundos;
            // de 400 Hz (muito abafado) até 22000 Hz (normal), devagar no início e mais depressa no fim
            filtro.cutoffFrequency = Mathf.Lerp(400f, 22000f, t * t);
            yield return null;
        }
        if (filtro == null) yield break;
        filtro.cutoffFrequency = 22000f;
        filtro.enabled = false;
    }

    // fere quem estiver dentro do raio: muito ao pé da carga, pouco na borda.
    // O mesmo soldado tem vários colliders (cápsula e ossos): só leva dano uma vez
    void Ferir(Vector3 centro)
    {
        Collider[] encontrados = Physics.OverlapSphere(centro, raioDano, ~0, QueryTriggerInteraction.Ignore);
        HashSet<Component> atingidos = new HashSet<Component>();

        foreach (Collider c in encontrados)
        {
            PlayerStats p = c.GetComponentInParent<PlayerStats>();
            Enemy soldado = p == null ? c.GetComponentInParent<Enemy>() : null;
            EnemyDrone drone = (p == null && soldado == null) ? c.GetComponentInParent<EnemyDrone>() : null;

            Component alvo = p != null ? (Component)p : soldado != null ? (Component)soldado : drone;
            if (alvo == null || atingidos.Contains(alvo)) continue;

            Vector3 ponto = c.bounds.center;
            if (paredesProtegem && Tapado(centro, ponto, alvo.transform)) continue;
            atingidos.Add(alvo);

            float dano = danoMaximo * (1f - Mathf.Clamp01(Vector3.Distance(centro, ponto) / raioDano));
            if (dano <= 0f) continue;

            // as funções de dano de sempre (as dos tiros e das minas)
            if (p != null) p.TakeDamage(dano);
            else if (soldado != null) soldado.enemyHitDamage(dano);
            else drone.enemyDroneHitDamage(dano);
            Debug.Log(name + ": explosão tirou " + Mathf.RoundToInt(dano) + " a " + alvo.name);
        }
    }

    // há alguma coisa (uma parede, um móvel) entre a carga e o alvo?
    bool Tapado(Vector3 centro, Vector3 ponto, Transform alvo)
    {
        RaycastHit hit;
        if (!Physics.Linecast(centro, ponto, out hit, ~0, QueryTriggerInteraction.Ignore)) return false;
        return !hit.collider.transform.IsChildOf(alvo);
    }

    // os soldados vivos do mesmo piso, perto da porta, vão ver o que foi.
    // FindObjectsByType não encontra os desligados (os da fuga e os que ainda não foram ativados ficam de fora)
    void Alertar(Vector3 centro)
    {
        Vector3 destino = pontoInvestigar != null ? pontoInvestigar.position : centro;
        int n = 0;

        foreach (Enemy soldado in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (soldado.presentHealth <= 0f) continue;
            Vector3 pe = soldado.transform.position;
            if (Mathf.Abs(pe.y - centro.y) > alturaAlerta) continue;
            if (Vector3.Distance(pe, centro) > raioAlerta) continue;

            // cada um vai para um ponto um pouco ao lado, mas sempre em cima do NavMesh
            Vector3 alvo = destino;
            if (espalhar > 0f)
            {
                Vector2 r = Random.insideUnitCircle * espalhar;
                NavMeshHit nh;
                if (NavMesh.SamplePosition(destino + new Vector3(r.x, 0f, r.y), out nh, 2f, NavMesh.AllAreas))
                    alvo = nh.position;
            }

            // o último valor (centro) é de onde veio o barulho: o soldado vira-se e olha para a porta
            soldado.Investigar(alvo, tempoInvestigar, bonusVisaoInvestigar, centro);
            n++;
        }
        Debug.Log(name + ": " + n + " soldados foram investigar a explosão");
    }

    void DanificarEquipamentos()
    {
        foreach (Danificavel e in equipamentos)
            if (e != null) e.TentarDanificar();
    }

    void Escombros()
    {
        if (variantesEscombros == null || variantesEscombros.Length == 0) return;
        if (Random.value >= probabilidadeEscombros) return;

        GameObject escolhida = variantesEscombros[Random.Range(0, variantesEscombros.Length)];
        if (escolhida != null)
        {
            escolhida.SetActive(true);
            Debug.Log(name + ": escombros " + escolhida.name);
        }
    }

    // ---------- Zona ----------
    void OnTriggerEnter(Collider other)
    {
        // só o Player tem PlayerStats; inimigos e balas são ignorados
        PlayerStats p = other.GetComponentInParent<PlayerStats>();
        if (p == null) return;
        jogadorNaZona = p;
        if (inventario == null) inventario = other.GetComponentInParent<InventarioPlayer>();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerStats>() == null) return;
        jogadorNaZona = null;
        Largar();
        Apagar();
    }

    void Largar()
    {
        tempoCarregado = 0f;
        if (!cargaColocada) progresso = 0f;
        TocarLoop(false);
    }

    void TocarLoop(bool tocar)
    {
        if (audioSource == null || somColocar == null) return;
        if (tocar)
        {
            audioSource.clip = somColocar;
            audioSource.loop = true;
            audioSource.Play();
        }
        else if (audioSource.clip == somColocar && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }
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

    // Na Scene, com o objeto selecionado: vermelho = raio do dano, amarelo = raio do alerta
    void OnDrawGizmosSelected()
    {
        Vector3 centro = folha != null ? folha.position : transform.position;
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(centro, raioDano);
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(centro, raioAlerta);
    }
}
