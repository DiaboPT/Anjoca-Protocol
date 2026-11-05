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
    /****************/


    /**** CARLOS ****/
    private void Start() {
        balasRestantes = balasCarregador;
        reservaRestante = balasReserva;
        if (bracos != null) animBracos = bracos.GetComponent<Animator>();
    }
    /****************/

    private void OnEnable() {
        fireAction.action.Enable();
    }

    private void OnDisable() {
        fireAction.action.Disable();
    }


    private void LateUpdate() {
        /**** CARLOS ****/
        if (Keyboard.current != null && Keyboard.current[teclaRecarregar].wasPressedThisFrame)
            Recarregar();
        /****************/

        if (fireAction.action.WasPressedThisFrame())
            Shoot();
        else if (!fireAction.action.IsPressed())
            HideRay();
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

        // o raio parte da câmara (centro do ecrã), para acertar exatamente onde está a mira.
        // a linha verde continua a sair da arma (ShootingRaycastArea)
        Transform mira = aimOrigin;
        if (mira == null) mira = Camera.main != null ? Camera.main.transform : ShootingRaycastArea;
        Vector3 origin = mira.position;
        Vector3 direction = mira.forward;
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
            
            // buraco de bala no cenário (nos inimigos não faz nada)
            MarcasImpacto.Bala(hit);
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
