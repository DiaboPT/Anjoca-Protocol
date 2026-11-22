using UnityEngine;
using UnityEngine.InputSystem;

public class Weapon : MonoBehaviour {
    [Header("Raycast")]
    [SerializeField] private float range = 100f;
    [SerializeField] private LayerMask hitLayers;

    [Header("Visual")]
    [SerializeField] private LineRenderer rayVisual;
    /**** CARLOS ****/
    // ponta do cano da arma (o ShootingArea da M4), com o mesmo nome do Enemy.cs.
    // só serve de início para a linha verde do F3: o tiro em si sai da câmara (ver Mira).
    [SerializeField] private Transform ShootingRaycastArea;
    /****************/

    [Header("Input")]
    [SerializeField] private InputActionReference fireAction;


    /**** CARLOS ****/
    [Header("Dano")]
    public float damage;

    [Header("Mira")]
    // de onde sai o raio do tiro (vazio = Main Camera, ou seja, o centro do ecrã)
    [SerializeField] private Transform aimOrigin;

    [Header("Braços (opcional)")]
    // braços do player à frente da câmara (Player_Bracos): dão um pequeno recuo em cada tiro
    [SerializeField] private BracosFPS bracos;

    [Header("Clarão e som (opcional)")]
    // o MuzzleFlash e a LuzDisparo que vieram com a M4 do soldado (dentro do M4Colt do Player_Bracos)
    [SerializeField] private ParticleSystem clarao;
    // luz laranja na ponta da arma, acesa só durante o clarão (fica desligada no Inspector)
    [SerializeField] private Light luzDisparo;
    [SerializeField] private float duracaoLuz = 0.05f;
    // som do tiro (por exemplo o Tiro_M4). Precisa de um Audio Source no Weapon
    [SerializeField] private AudioClip somTiro;
    [SerializeField] private AudioSource fonteSom;

    [Header("Munição")]
    // tiros por carregador (como os soldados)
    [SerializeField] private int balasCarregador = 30;
    // balas de reserva com que começas, fora as da arma (90 = 3 carregadores)
    [SerializeField] private int balasReserva = 90;
    // máximo de balas que podes ter na reserva (0 = sem limite).
    // com limite, as caixas de munição dos inimigos ficam no chão quando já estás cheio
    [SerializeField] private int reservaMaxima = 0;
    // segundos a recarregar (sem disparar). Acertar pela duração da animação
    [SerializeField] private float tempoRecarregar = 2f;
    // tecla para recarregar
    [SerializeField] private Key teclaRecarregar = Key.R;
    // som mecânico do carregador (o Recarregar dos soldados)
    [SerializeField] private AudioClip somRecarregar;
    // balas que ainda tens no carregador e na reserva (aparecem no Inspector para ver durante o Play)
    [SerializeField] private int balasRestantes;
    [SerializeField] private int reservaRestante;
    private bool aRecarregar = false;
    // Animator do Player_Bracos, para a animação de recarregar
    private Animator animBracos;

    [Header("Cadência")]
    // ligado: segurar o gatilho dispara em rajada (a M4). Desligado: um tiro por cada clique (a Glock)
    [SerializeField] private bool automatica = false;
    // tiros por minuto quando é automática (a M4 anda pelos 750; 60/este valor são os segundos entre tiros)
    [SerializeField] private float tirosPorMinuto = 750f;
    // quando pode sair o próximo tiro
    private float proximoTiro = 0f;

    [Header("Dispersão e coice")]
    // A dispersão e o coice são a mesma coisa: cada tiro empurra a câmara, e o tiro seguinte sai
    // já da mira nova. A bala vai sempre ter onde a mira está, e o que espalha a rajada é a câmara a subir.
    // cone fixo à volta da mira, em graus (a arma nunca é perfeita). 0 = acerta sempre no centro
    [SerializeField] private float dispersaoBase = 0.25f;
    // graus que a câmara sobe a cada tiro
    [SerializeField] private float dispersaoPorTiro = 0.4f;
    // limite do desvio acumulado, para a câmara não fugir para o céu numa rajada longa
    [SerializeField] private float dispersaoMaxima = 4f;
    // graus por segundo a que a câmara volta ao sítio, depois de largares o gatilho
    [SerializeField] private float recuperacaoDispersao = 10f;
    // quanto o coice atira para o lado, em relação ao que sobe (0 = só sobe, 1 = tanto para o lado como para cima)
    [SerializeField] private float coiceDeLado = 0.6f;
    // o Player, que é quem manda na câmara
    private PlayerMovement jogador;

    [Header("Barulho (os soldados ouvem os tiros)")]
    // metros a que os soldados ouvem o tiro (metade com uma parede pelo meio, um terço noutro piso). 0 = não se ouve
    [SerializeField] private float raioRuidoTiro = 35f;
    private float proximoRuido = 0f;
    /****************/


    /**** CARLOS ****/
    private void Start() {
        balasRestantes = balasCarregador;
        reservaRestante = balasReserva;
        if (bracos != null) animBracos = bracos.GetComponent<Animator>();
        // o Weapon está dentro da Main Camera, que está dentro do Player
        jogador = GetComponentInParent<PlayerMovement>();
    }
    /****************/

    private void OnEnable() {
        fireAction.action.Enable();
    }

    private void OnDisable() {
        fireAction.action.Disable();

        /**** CARLOS ****/
        // guardaste a arma a meio de recarregar: o recarregar fica sem efeito (tens de voltar a carregar em R).
        // Sem isto, a arma ficava presa em "A RECARREGAR..." para sempre, porque o Invoke não corre
        // enquanto o objeto está desligado
        CancelInvoke(nameof(AcabarRecarregar));
        aRecarregar = false;
        /****************/
    }


    private void LateUpdate() {
        /**** CARLOS ****/
        if (Keyboard.current != null && Keyboard.current[teclaRecarregar].wasPressedThisFrame)
            Recarregar();
        /****************/

        /**** CARLOS ****/
        // automática: enquanto o gatilho está em baixo, sai um tiro de cada vez que a cadência deixa.
        // semiautomática: um tiro por cada clique, como sempre foi
        if (automatica) {
            if (fireAction.action.IsPressed()) {
                if (Time.time >= proximoTiro) Shoot();
            }
            else HideRay();
        }
        else if (fireAction.action.WasPressedThisFrame())
            Shoot();
        else if (!fireAction.action.IsPressed())
            HideRay();
        /****************/
    }

    // private void LateUpdate() {
    //     if (fireAction.action.IsPressed())
    //         Shoot();
    //     else
    //         HideRay();
    // }

    private void Shoot() {
        // Vector3 origin = ShootingRaycastArea.position;
        // Vector3 direction = ShootingRaycastArea.forward;

        /**** CARLOS ****/
        // a recarregar não dispara
        if (aRecarregar) return;

        // carregador vazio: recarrega sozinho (se ainda houver reserva)
        if (balasRestantes <= 0) {
            Recarregar();
            return;
        }

        // gastou uma bala
        balasRestantes--;

        // segura o ritmo da rajada. Soma-se ao momento marcado, e não ao de agora: assim a cadência
        // não fica presa às imagens por segundo (a 30 fps, de outra maneira, 750 tiros/min davam 600)
        float intervalo = tirosPorMinuto > 0f ? 60f / tirosPorMinuto : 0f;
        proximoTiro += intervalo;
        if (proximoTiro < Time.time) proximoTiro = Time.time + intervalo;

        // recuo dos braços (se estiverem ligados no Inspector)
        if (bracos != null) bracos.Recuo();

        // clarão na ponta da arma
        if (clarao != null) clarao.Play();
        if (luzDisparo != null) {
            luzDisparo.enabled = true;
            CancelInvoke(nameof(ApagarLuz));
            Invoke(nameof(ApagarLuz), duracaoLuz);
        }

        // som do tiro, com um tom ligeiramente diferente em cada disparo (como nos soldados)
        if (fonteSom != null && somTiro != null) {
            fonteSom.pitch = Random.Range(0.92f, 1.08f);
            fonteSom.PlayOneShot(somTiro);
        }

        // os soldados perto ouvem o tiro e vêm ver (numa rajada, um barulho a cada 0,2 s chega)
        if (raioRuidoTiro > 0f && Time.time >= proximoRuido) {
            proximoRuido = Time.time + 0.2f;
            Ruido.Emitir(transform.position, raioRuidoTiro, TipoRuido.Tiro, this);
        }

        // o raio parte da câmara (centro do ecrã), para acertar exatamente onde está a mira.
        // a linha verde continua a sair da arma (ShootingRaycastArea)
        Transform mira = aimOrigin;
        if (mira == null) mira = Camera.main != null ? Camera.main.transform : ShootingRaycastArea;
        Vector3 origin = mira.position;
        Vector3 direction = mira.forward;

        // o cone fixo da arma: um desvio pequeno à volta da mira, igual em todos os tiros
        if (dispersaoBase > 0f) {
            Vector2 desvio = Random.insideUnitCircle * Mathf.Tan(dispersaoBase * Mathf.Deg2Rad);
            direction = (direction + mira.right * desvio.x + mira.up * desvio.y).normalized;
        }

        // o coice: a bala desta vez já saiu, e agora a câmara sobe. O tiro seguinte sai da mira nova,
        // e é assim que a rajada se espalha: vês para onde vai cada bala, em vez de ser às escondidas
        if (jogador != null && dispersaoPorTiro > 0f) {
            float lado = Random.Range(-dispersaoPorTiro, dispersaoPorTiro) * coiceDeLado;
            float espera = (tirosPorMinuto > 0f ? 60f / tirosPorMinuto : 0f) + 0.08f;
            jogador.AplicarCoice(dispersaoPorTiro, lado, dispersaoMaxima, recuperacaoDispersao, espera);
        }
        /****************/

        Vector3 endPoint = origin + direction * range;

        if (Physics.Raycast(
            origin,
            direction,
            out RaycastHit hit,
            range,
            hitLayers)
        ) {
            endPoint = hit.point;

            Debug.Log($"Hit: {hit.collider.name}");

            /**** CARLOS ****/
            Enemy soldado = hit.collider.GetComponentInParent<Enemy>();
            if (soldado != null) {
                // sangue no ponto onde a bala acertou
                soldado.MostrarSangue(hit.point, hit.normal);

                bool naCabeca = hit.collider.CompareTag("Head");

                // o líder tem regras próprias (colete, cabeça ×2, reforços): o Lider calcula o dano
                Lider lider = soldado.GetComponent<Lider>();
                if (lider != null) {
                    // collider com a tag Colete: o tiro foi no tronco, onde o colete protege
                    bool noColete = hit.collider.CompareTag("Colete");
                    soldado.enemyHitDamage(lider.ReceberTiro(damage, naCabeca, noColete), naCabeca);
                }
                // tiro na cabeça (collider com a tag Head): tira a vida toda de uma vez se for um soldado comum
                else if (naCabeca)
                    soldado.enemyHitDamage(soldado.presentHealth, true);
                else
                    soldado.enemyHitDamage(damage);
            }

            EnemyDrone drone = hit.collider.GetComponentInParent<EnemyDrone>();
            if (drone != null) {
                // faíscas no ponto onde a bala acertou
                drone.MostrarFaiscas(hit.point, hit.normal);
                drone.enemyDroneHitDamage(damage);
            }

            // sniper da torre da trincheira: o collider está no Sniper_Soldado e o script Sniper no Pivo
            Sniper sniper = Sniper.Atingido(hit.collider);
            if (sniper != null) sniper.LevarTiro(damage, hit.collider.CompareTag("Head"), hit.point, hit.normal);

            // buraco de bala no cenário (nos inimigos não faz nada; no sniper também não)
            if (sniper == null) MarcasImpacto.Bala(hit);
            /****************/
        }

        /**** CARLOS ****/
        // a linha do tiro só aparece com o debug visual (F3)
        rayVisual.enabled = ModoDebugVisual.Tiros;
        /****************/

        rayVisual.SetPosition(0, ShootingRaycastArea.position);
        rayVisual.SetPosition(1, endPoint);

        /**** CARLOS ****/
        // foi a última bala do carregador: recarrega logo
        if (balasRestantes <= 0) Recarregar();
        /****************/
    }

    private void HideRay() {
        rayVisual.enabled = false;
    }

    /**** CARLOS ****/
    private void ApagarLuz() {
        luzDisparo.enabled = false;
    }

    private void Recarregar() {
        // já está a recarregar, o carregador está cheio ou não há balas de reserva
        if (aRecarregar || balasRestantes >= balasCarregador || reservaRestante <= 0) return;

        aRecarregar = true;
        // animação Recarregar nos braços (trigger no Player_Bracos_Animator)
        if (animBracos != null) animBracos.SetTrigger("Recarregar");
        if (fonteSom != null && somRecarregar != null) {
            fonteSom.pitch = 1f;
            fonteSom.PlayOneShot(somRecarregar);
        }
        Invoke(nameof(AcabarRecarregar), tempoRecarregar);
    }

    private void AcabarRecarregar() {
        // tira da reserva só as balas que faltam no carregador
        int tirar = Mathf.Min(balasCarregador - balasRestantes, reservaRestante);
        balasRestantes += tirar;
        reservaRestante -= tirar;
        aRecarregar = false;
    }

    // chamado pelas caixas de munição que os inimigos deixam cair (um Pickup com o Type em Ammo Pack).
    // devolve false se não houver nada a dar (reserva no máximo): a caixa fica no chão para mais tarde
    public bool AdicionarBalas(int quantidade) {
        if (quantidade <= 0) return false;
        if (reservaMaxima > 0 && reservaRestante >= reservaMaxima) return false;

        reservaRestante += quantidade;
        if (reservaMaxima > 0 && reservaRestante > reservaMaxima) reservaRestante = reservaMaxima;

        // estavas com o carregador vazio: recarrega logo, para poderes voltar a disparar
        if (balasRestantes <= 0) Recarregar();
        return true;
    }

    // balas que tens na reserva (para o HUD ou para outros scripts)
    public int ReservaRestante { get { return reservaRestante; } }

    // está a recarregar? (o TrocaArma usa isto para não deixar trocar de arma a meio, se assim o quiseres)
    public bool ARecarregar { get { return aRecarregar; } }

    // texto das balas para o HUD (o DebugHUD mostra-o no canto de baixo à direita)
    public string TextoBalas {
        get {
            if (aRecarregar) return "A RECARREGAR...";
            if (balasRestantes <= 0 && reservaRestante <= 0) return "SEM BALAS";
            return "BALAS: " + balasRestantes + " / " + reservaRestante;
        }
    }
    /****************/
}
