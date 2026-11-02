using UnityEngine;
using UnityEngine.InputSystem;

// Um só sítio para escolher entre Modo Teste e Modo Normal.
// Pôr num GameObject vazio chamado "ModoTeste" (só um na cena).
public class ModoTeste : MonoBehaviour
{
    [Header("Interruptor principal")]
    [Tooltip("Ligado = opções de teste. Desligado = jogo normal.")]
    public bool modoTeste = true;

    [Tooltip("Tecla para trocar de modo durante o Play (só no Editor).")]
    public Key teclaTrocar = Key.F1;

    [Header("Velocidade do jogador (normal / teste)")]
    public PlayerMovement movimento;
    public float andarNormal = 5f;
    public float correrNormal = 8f;
    public float andarTeste = 15f;
    public float correrTeste = 30f;

    [Header("Vida máxima do jogador (normal / teste)")]
    public PlayerStats jogador;
    public float vidaMaximaNormal = 100f;
    public float vidaMaximaTeste = 1000f;

    [Header("Aprimoramentos do Fato")]
    [Tooltip("Pode ficar vazio: se o SuitAbilities estiver no Player, é encontrado sozinho.")]
    public SuitAbilities aprimoramentos;
    [Tooltip("Em teste, os módulos de Salto, Blindagem, Precisão, Visor térmico e Velocidade ficam todos desbloqueados.")]
    public bool desbloquearTudoEmTeste = true;

    [Header("Energia máxima do Fato (normal / teste)")]
    public float energiaMaximaNormal = 100f;
    public float energiaMaximaTeste = 1000f;

    [Header("Dano de cada bala nos inimigos (normal / teste)")]
    [Tooltip("Pode ficar vazio: se o Weapon estiver no Player ou num filho dele, é encontrado sozinho.")]
    public Weapon arma;
    public float danoNormal = 20f;
    public float danoTeste = 100f;

    [Header("Autodestruição (normal / teste)")]
    public TerminalAutodestruicao terminal;
    public float tempoContagemNormal = 60f;
    public float tempoContagemTeste = 15f;
    public float raioExplosaoNormal = 40f;
    public float raioExplosaoTeste = 70f;

    [Header("Objetos só ligados em modo normal (ex.: Telhado)")]
    public GameObject[] desligarEmTeste;

    [Header("Objetos só ligados em teste (ex.: DebugHUD)")]
    public GameObject[] ligarSoEmTeste;

    [Header("Componentes só visíveis em teste (ex.: Line Renderer das rotas)")]
    public Renderer[] mostrarSoEmTeste;

    // ---------- Valores que os outros scripts leem ----------
    public static bool Ativo { get; private set; }

    bool modoAplicado;

    // O que o jogador já tinha apanhado antes de entrar em teste (para repor no modo normal)
    bool tinhaSalto, tinhaBlindagem, tinhaPrecisao, tinhaVisor, tinhaVelocidade;
    bool desbloqueioAplicado;

    void Awake()
    {
#if !UNITY_EDITOR
        modoTeste = false;   // num Build o jogo arranca sempre em modo normal
#endif
        Aplicar();
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current[teclaTrocar].wasPressedThisFrame)
            modoTeste = !modoTeste;
#endif
        // Também apanha a caixa do Inspector clicada durante o Play
        if (modoTeste != modoAplicado) Aplicar();
    }

    void Aplicar()
    {
        modoAplicado = modoTeste;
        Ativo = modoTeste;

        if (movimento != null)
        {
            movimento.walkSpeed = modoTeste ? andarTeste : andarNormal;
            movimento.runSpeed = modoTeste ? correrTeste : correrNormal;
        }

        if (jogador != null)
        {
            jogador.maxHealth = modoTeste ? vidaMaximaTeste : vidaMaximaNormal;
            // Em teste enche a vida; ao voltar ao normal não pode ficar acima do máximo
            jogador.health = modoTeste ? jogador.maxHealth : Mathf.Min(jogador.health, jogador.maxHealth);

            jogador.maxEnergy = modoTeste ? energiaMaximaTeste : energiaMaximaNormal;
            jogador.energy = modoTeste ? jogador.maxEnergy : Mathf.Min(jogador.energy, jogador.maxEnergy);
        }

        AplicarAprimoramentos();

        if (arma == null && jogador != null)
            arma = jogador.GetComponentInChildren<Weapon>(true);
        if (arma != null)
            arma.damage = modoTeste ? danoTeste : danoNormal;

        if (terminal != null)
        {
            terminal.tempoContagem = modoTeste ? tempoContagemTeste : tempoContagemNormal;
            terminal.raioExplosao = modoTeste ? raioExplosaoTeste : raioExplosaoNormal;
        }

        foreach (GameObject go in desligarEmTeste)
            if (go != null) go.SetActive(!modoTeste);

        foreach (GameObject go in ligarSoEmTeste)
            if (go != null) go.SetActive(modoTeste);

        foreach (Renderer r in mostrarSoEmTeste)
            if (r != null) r.enabled = modoTeste;

        Debug.Log(modoTeste ? "MODO TESTE ligado" : "Modo normal");
    }

    void AplicarAprimoramentos()
    {
        if (aprimoramentos == null && jogador != null)
            aprimoramentos = jogador.GetComponent<SuitAbilities>();
        if (aprimoramentos == null) return;

        bool desbloquear = modoTeste && desbloquearTudoEmTeste;

        if (desbloquear && !desbloqueioAplicado)
        {
            // Guarda o que já estava apanhado e desbloqueia tudo
            tinhaSalto = aprimoramentos.jumpUnlocked;
            tinhaBlindagem = aprimoramentos.armorUnlocked;
            tinhaPrecisao = aprimoramentos.precisionUnlocked;
            tinhaVisor = aprimoramentos.thermalUnlocked;
            tinhaVelocidade = aprimoramentos.speedUnlocked;

            aprimoramentos.jumpUnlocked = true;
            aprimoramentos.armorUnlocked = true;
            aprimoramentos.precisionUnlocked = true;
            aprimoramentos.thermalUnlocked = true;
            aprimoramentos.speedUnlocked = true;
            desbloqueioAplicado = true;
        }
        else if (!desbloquear && desbloqueioAplicado)
        {
            // Volta ao normal: só fica desbloqueado o que tinha sido apanhado
            aprimoramentos.jumpUnlocked = tinhaSalto;
            aprimoramentos.armorUnlocked = tinhaBlindagem;
            aprimoramentos.precisionUnlocked = tinhaPrecisao;
            aprimoramentos.thermalUnlocked = tinhaVisor;
            aprimoramentos.speedUnlocked = tinhaVelocidade;

            // Desliga os que ficaram bloqueados mas estavam ligados
            if (!tinhaSalto) aprimoramentos.jumpActive = false;
            if (!tinhaBlindagem) aprimoramentos.armorActive = false;
            if (!tinhaPrecisao) aprimoramentos.precisionActive = false;
            if (!tinhaVisor) aprimoramentos.thermalActive = false;
            if (!tinhaVelocidade) aprimoramentos.speedActive = false;
            desbloqueioAplicado = false;
        }
    }
}
