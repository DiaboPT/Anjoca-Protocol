using UnityEngine;
using UnityEngine.InputSystem;

// Painel de debug: mostra variáveis e parâmetros do jogo por cima do ecrã.
// Pôr num GameObject vazio chamado "ModoDebug" (só um na cena).
// Só lê valores: não muda nada no jogo (quem muda valores é o ModoTeste, F1).
public class ModoDebug : MonoBehaviour
{
    [Header("Interruptor principal")]
    [Tooltip("Ligado = painel de debug visível.")]
    public bool modoDebug = false;

    [Tooltip("Tecla para mostrar/esconder o painel durante o Play.")]
    public Key teclaTrocar = Key.F2;

    [Tooltip("Deixar o F2 funcionar também num Build (por defeito só no Editor).")]
    public bool funcionarNoBuild = false;

    [Header("Jogador (arrastar o Player)")]
    public PlayerStats jogador;
    public PlayerMovement movimento;
    public SuitAbilities aprimoramentos;

    [Header("Inimigos")]
    [Tooltip("Mostrar só os inimigos até esta distância do jogador (0 = todos).")]
    public float distanciaMaxima = 0f;
    [Tooltip("Segundos entre cada procura de inimigos na cena.")]
    public float intervaloProcura = 1f;

    [Header("Aspeto do painel")]
    public int tamanhoLetra = 14;
    public float largura = 540f;
    [Tooltip("Largura da coluna dos nomes dos inimigos (e dos nomes do jogador).")]
    public float larguraNome = 145f;
    [Tooltip("Píxeis extra entre linhas (0 = linhas juntas).")]
    public int espacoEntreLinhas = 0;

    [Header("Posição do painel (canto superior direito, por baixo do minimapa)")]
    [Tooltip("Distância do topo do ecrã. Minimapa: 20 + 220 + 20 de folga = 260.")]
    public float margemTopo = 260f;
    [Tooltip("Distância da borda direita do ecrã (igual à do minimapa).")]
    public float margemDireita = 20f;
    [Tooltip("Distância da borda de baixo do ecrã.")]
    public float margemBaixo = 20f;

    // ---------- Valor que os outros scripts leem ----------
    public static bool Ativo { get; private set; }

    Enemy[] inimigos = new Enemy[0];
    EnemyDrone[] drones = new EnemyDrone[0];
    float proximaProcura;

    float fpsSuavizado;
    Vector3 posicaoAnterior;
    float velocidadeJogador;

    GUIStyle estilo;
    Vector2 scroll;

    void Awake()
    {
#if !UNITY_EDITOR
        if (!funcionarNoBuild) modoDebug = false;   // num Build arranca sempre desligado
#endif
        if (jogador != null)
        {
            if (movimento == null) movimento = jogador.GetComponent<PlayerMovement>();
            if (aprimoramentos == null) aprimoramentos = jogador.GetComponent<SuitAbilities>();
            posicaoAnterior = jogador.transform.position;
        }
    }

    void Update()
    {
        bool teclaPermitida = Application.isEditor || funcionarNoBuild;
        if (teclaPermitida && Keyboard.current != null && Keyboard.current[teclaTrocar].wasPressedThisFrame)
        {
            modoDebug = !modoDebug;
            Debug.Log(modoDebug ? "MODO DEBUG ligado" : "Modo debug desligado");
        }
        // Também apanha a caixa do Inspector clicada durante o Play
        Ativo = modoDebug;
        if (!modoDebug) return;

        // FPS suavizado para os números não saltarem
        float fpsAgora = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        fpsSuavizado = Mathf.Lerp(fpsSuavizado, fpsAgora, 0.1f);

        // Velocidade real do jogador (m/s), medida pela distância percorrida
        if (jogador != null && Time.deltaTime > 0f)
        {
            Vector3 agora = jogador.transform.position;
            Vector3 delta = agora - posicaoAnterior;
            delta.y = 0f;   // só a velocidade no chão
            velocidadeJogador = delta.magnitude / Time.deltaTime;
            posicaoAnterior = agora;
        }

        // Procurar inimigos de vez em quando (procurar em todos os frames é pesado)
        if (Time.unscaledTime >= proximaProcura)
        {
            proximaProcura = Time.unscaledTime + intervaloProcura;
            inimigos = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            drones = FindObjectsByType<EnemyDrone>(FindObjectsSortMode.None);
        }

        // Ordenar em todos os frames: o mais próximo do jogador fica em cima
        // (feito no Update e não no OnGUI, para a ordem não mudar a meio de um frame)
        // Soldados: primeiro os que andam atrás de ti (não calmos), depois os calmos, os mortos no fim
        if (jogador != null)
        {
            System.Array.Sort(inimigos, (a, b) => OrdemSoldado(a).CompareTo(OrdemSoldado(b)));
            System.Array.Sort(drones, (a, b) => DistanciaOrdem(a).CompareTo(DistanciaOrdem(b)));
        }
    }

    void OnGUI()
    {
        if (!modoDebug) return;

        if (estilo == null)
        {
            // Sem margens nem padding por defeito, para as linhas ficarem juntas
            estilo = new GUIStyle(GUI.skin.label) { richText = true };
            estilo.padding = new RectOffset(0, 0, 0, 0);
            estilo.wordWrap = false;   // cada inimigo numa só linha (se não couber, aumentar a Largura)
        }
        estilo.fontSize = tamanhoLetra;
        estilo.margin = new RectOffset(0, 0, 0, espacoEntreLinhas);

        // Encostado à direita, por baixo do minimapa
        float x = Screen.width - margemDireita - largura;
        float altura = Mathf.Max(Screen.height - margemTopo - margemBaixo, 50f);
        GUI.Box(new Rect(x, margemTopo, largura, altura), GUIContent.none);
        GUILayout.BeginArea(new Rect(x + 8, margemTopo + 6, largura - 16, altura - 12));
        scroll = GUILayout.BeginScrollView(scroll);

        Titulo("MODO DEBUG (F2)");
        Linha("FPS", fpsSuavizado.ToString("0"));
        Linha("Time.timeScale", Time.timeScale.ToString("0.00"));
        Linha("Modo Teste (F1)", ModoTeste.Ativo ? "<color=yellow>ligado</color>" : "desligado");
        Linha("Debug Visual (F3)", ModoDebugVisual.Ativo ? "<color=yellow>ligado</color>" : "desligado");

        MostrarJogador();
        MostrarInimigos();
        MostrarDrones();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ---------- Secções ----------

    void MostrarJogador()
    {
        Titulo("JOGADOR");
        if (jogador == null)
        {
            Linha("PlayerStats", "<color=red>não ligado no Inspector</color>");
            return;
        }

        Linha("Vida", jogador.health.ToString("0.0") + " / " + jogador.maxHealth.ToString("0"));
        Linha("Energia do Fato", jogador.energy.ToString("0.0") + " / " + jogador.maxEnergy.ToString("0"));
        Linha("Regeneração", jogador.regenRate.ToString("0.0") + "/s até " + jogador.regenCap.ToString("0")
                             + " (espera " + jogador.regenDelay.ToString("0") + " s)");
        Linha("Morto", jogador.isDead ? "<color=red>sim</color>" : "não");

        Vector3 p = jogador.transform.position;
        Linha("Posição", p.x.ToString("0.0") + "  " + p.y.ToString("0.0") + "  " + p.z.ToString("0.0"));
        Linha("Velocidade real", velocidadeJogador.ToString("0.0") + " m/s");

        if (movimento != null)
            Linha("Andar / Correr", movimento.walkSpeed.ToString("0.0") + " / " + movimento.runSpeed.ToString("0.0") + " m/s");

        if (aprimoramentos != null)
            Linha("Blindagem", aprimoramentos.armorActive ? "<color=cyan>ativa</color>" : "desligada");
    }

    void MostrarInimigos()
    {
        int vivos = 0;
        foreach (Enemy e in inimigos)
            if (e != null && e.presentHealth > 0f) vivos++;

        Titulo("SOLDADOS (" + vivos + " vivos de " + inimigos.Length + ")");

        foreach (Enemy e in inimigos)
        {
            if (e == null) continue;

            float distancia = Distancia(e.transform.position);
            if (distanciaMaxima > 0f && distancia > distanciaMaxima) continue;

            // estado do soldado (calmo, desconfiado, combate, perseguir, procurar, alerta) e o medidor da PercecaoInimigo
            string estado = e.TextoEstado();
            GUILayout.BeginHorizontal();
            Coluna("<b>" + e.name + "</b>", larguraNome);
            Coluna(estado, 115);
            Coluna(SEP + "vida " + e.presentHealth.ToString("0") + "/" + e.enemyHealth.ToString("0"), 85);
            Coluna(SEP + "dist " + TextoDistancia(distancia), 85);
            Coluna(SEP + "tiro " + e.alcanceDisparo.ToString("0"), 50);
            GUILayout.EndHorizontal();
        }
    }

    void MostrarDrones()
    {
        if (drones.Length == 0) return;

        int vivos = 0;
        foreach (EnemyDrone d in drones)
            if (d != null && d.presentHealth > 0f) vivos++;

        Titulo("DRONES (" + vivos + " vivos de " + drones.Length + ")");

        foreach (EnemyDrone d in drones)
        {
            if (d == null) continue;

            float distancia = Distancia(d.transform.position);
            if (distanciaMaxima > 0f && distancia > distanciaMaxima) continue;

            string estado = EstadoInimigo(d.presentHealth, d.playerInVisionRadius, d.playerInShootingRadius);
            GUILayout.BeginHorizontal();
            Coluna("<b>" + d.name + "</b>", larguraNome);
            Coluna(estado, 75);
            Coluna(SEP + "vida " + d.presentHealth.ToString("0") + "/" + d.enemyHealth.ToString("0"), 85);
            Coluna(SEP + "dist " + TextoDistancia(distancia), 85);
            Coluna(SEP + "visão " + d.visionRadius.ToString("0"), 65);
            Coluna(SEP + "tiro " + d.shootingRadius.ToString("0"), 50);
            GUILayout.EndHorizontal();
        }
    }

    // ---------- Ajudas ----------

    // Separador entre os atributos de cada inimigo, na mesma linha do nome
    const string SEP = "<color=grey>|</color> ";

    // Uma célula da tabela de inimigos, com largura fixa para as colunas ficarem alinhadas.
    // As larguras foram escolhidas para letra 14 e crescem com o Tamanho Letra.
    void Coluna(string texto, float larguraBase)
    {
        GUILayout.Label(texto, estilo, GUILayout.Width(larguraBase * tamanhoLetra / 14f));
    }

    // Distância para ordenar; inimigos destruídos vão para o fim
    float DistanciaOrdem(Component c)
    {
        if (c == null) return float.MaxValue;
        return Vector3.Distance(jogador.transform.position, c.transform.position);
    }

    // Ordem dos soldados na tabela: ocupados (por distância), calmos (por distância), mortos
    float OrdemSoldado(Enemy e)
    {
        if (e == null) return float.MaxValue;
        float grupo;
        if (e.presentHealth <= 0f) grupo = 2f;
        else if (e.estado == global::EstadoInimigo.Calmo) grupo = 1f;
        else grupo = 0f;
        return grupo * 100000f + DistanciaOrdem(e);
    }

    string EstadoInimigo(float vida, bool vejo, bool aoAlcance)
    {
        if (vida <= 0f) return "<color=grey>morto</color>";
        if (aoAlcance) return "<color=red>a disparar</color>";
        if (vejo) return "<color=orange>a perseguir</color>";
        return "<color=lime>patrulha</color>";
    }

    float Distancia(Vector3 posicao)
    {
        if (jogador == null) return -1f;
        return Vector3.Distance(jogador.transform.position, posicao);
    }

    string TextoDistancia(float distancia)
    {
        return distancia < 0f ? "?" : distancia.ToString("0.0") + " m";
    }

    void Titulo(string texto)
    {
        GUILayout.Space(4);
        GUILayout.Label("<b><color=#7fd4ff>" + texto + "</color></b>", estilo);
    }

    void Linha(string nome, string valor)
    {
        // Nome numa coluna de largura fixa, para os valores ficarem todos alinhados
        GUILayout.BeginHorizontal();
        Coluna(nome, larguraNome);
        GUILayout.Label("<b>" + valor + "</b>", estilo);
        GUILayout.EndHorizontal();
    }
}
