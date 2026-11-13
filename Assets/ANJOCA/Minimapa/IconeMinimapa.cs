using UnityEngine;

// Ícone que só a câmara do minimapa vê (layer Minimapa).
// Pôr num Quad filho do Player, de cada soldado e de cada drone, ou num Quad solto para o objetivo.
// No início solta-se do pai e passa a segui-lo por código, para não herdar a escala nem a inclinação.
public class IconeMinimapa : MonoBehaviour
{
    public enum Tipo { Jogador, Inimigo, Objetivo }

    [Header("O que este ícone mostra")]
    public Tipo tipo = Tipo.Inimigo;
    [Tooltip("Vazio: segue o objeto-pai.")]
    public Transform alvo;

    [Header("Aspeto")]
    public float tamanho = 3f;          // metros (com zoom 25, o minimapa mostra 50 m de lado)
    public float alturaAcima = 10f;     // metros acima do alvo, para ficar por cima das paredes
    [Tooltip("Ligado: o ícone roda com o alvo (seta do jogador e dos inimigos). Desligado: fica sempre direito no minimapa.")]
    public bool rodarComAlvo = true;
    [Tooltip("Ligado: se o alvo estiver fora do minimapa, o ícone fica preso na borda a apontar a direção.")]
    public bool prenderNaBorda = false;

    [Header("Só para inimigos")]
    public float raioPerto = 15f;           // aparece sempre que o jogador está a menos disto
    public float tempoVisivelDepois = 3f;   // segundos que continua visível depois de deixar de te ver

    Renderer rend;
    Enemy soldado;
    EnemyDrone drone;
    Transform jogador;
    float ultimoAvistamento = -999f;

    void Awake()
    {
        if (alvo == null) alvo = transform.parent;

        if (alvo != null)
        {
            soldado = alvo.GetComponentInParent<Enemy>();
            drone = alvo.GetComponentInParent<EnemyDrone>();
        }

        // solta-se do pai: a posição e a rotação passam a ser postas no LateUpdate
        transform.SetParent(null);
        transform.localScale = Vector3.one * tamanho;

        rend = GetComponent<Renderer>();
        // o ícone não pode fazer sombra no cenário nem travar balas
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        Collider col = GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    void Start()
    {
        GameObject p = GameObject.Find("Player");
        if (p != null) jogador = p.transform;
    }

    void LateUpdate()
    {
        // o inimigo foi destruído (5 s depois de morrer): o ícone desaparece com ele
        if (alvo == null)
        {
            if (tipo == Tipo.Inimigo) Destroy(gameObject);
            else rend.enabled = false;
            return;
        }

        rend.enabled = Visivel();
        if (!rend.enabled) return;

        MinimapaCamera mini = MinimapaCamera.Instancia;

        Vector3 posicao = alvo.position + Vector3.up * alturaAcima;
        // dentro do edifício, a câmara do minimapa corta tudo acima do piso do jogador:
        // o ícone fica logo abaixo do corte, para continuar a aparecer
        if (mini != null) posicao.y = Mathf.Min(posicao.y, mini.AlturaMaxIcones);
        if (prenderNaBorda && mini != null)
        {
            // distância ao centro do minimapa, só em X e Z
            Vector3 centro = mini.transform.position;
            Vector3 offset = new Vector3(posicao.x - centro.x, 0f, posicao.z - centro.z);
            float raioBorda = mini.Camara.orthographicSize * 0.85f;   // um pouco para dentro do aro

            if (offset.magnitude > raioBorda)
            {
                offset = offset.normalized * raioBorda;
                posicao = new Vector3(centro.x + offset.x, posicao.y, centro.z + offset.z);
            }
        }
        transform.position = posicao;

        // X = 90 deita o Quad virado para cima, para a câmara do minimapa o ver
        float rumo;
        if (rodarComAlvo) rumo = alvo.eulerAngles.y;
        else rumo = mini != null ? mini.transform.eulerAngles.y : 0f;
        transform.rotation = Quaternion.Euler(90f, rumo, 0f);
    }

    bool Visivel()
    {
        if (tipo != Tipo.Inimigo) return true;

        // morto: o enemyDie() põe o visionRadius a 0
        // viu-te: playerInVisionRadius já é calculado pelo Enemy / EnemyDrone em cada frame
        bool morto = false;
        bool viu = false;
        if (soldado != null) { morto = soldado.visionRadius <= 0f; viu = soldado.playerInVisionRadius; }
        else if (drone != null) { morto = drone.visionRadius <= 0f; viu = drone.playerInVisionRadius; }

        if (morto) return false;

        // dentro do edifício, o minimapa mostra só o teu piso: os inimigos dos outros pisos não aparecem
        MinimapaCamera mini = MinimapaCamera.Instancia;
        if (mini != null && mini.CortarAgora && jogador != null
            && Mathf.Abs(alvo.position.y - jogador.position.y) > 2.5f)
            return false;

        bool perto = false;
        if (jogador != null)
        {
            Vector3 d = jogador.position - alvo.position;
            d.y = 0f;   // um drone lá em cima também conta como perto
            perto = d.magnitude <= raioPerto;
        }

        if (viu || perto) ultimoAvistamento = Time.time;
        return Time.time - ultimoAvistamento <= tempoVisivelDepois;
    }
}
