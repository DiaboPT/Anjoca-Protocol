using UnityEngine;

// Câmara vista de cima que segue o jogador e roda com ele (a seta do jogador aponta sempre para cima).
// Pôr numa Camera com Projection = Orthographic e Target Texture = RT_Minimapa. Só uma na cena.
// Dentro do edifício, mostra só o piso onde o jogador está: a câmara desce para logo acima do topo
// das paredes desse piso e o Near Clip corta a laje e tudo o que está por cima.
// Uma caixa cortada pelo Near Clip não se vê de cima, por isso a laje desaparece e as paredes ficam.
[RequireComponent(typeof(Camera))]
public class MinimapaCamera : MonoBehaviour
{
    [Header("Jogador")]
    [Tooltip("Vazio: procura o GameObject chamado \"Player\".")]
    public Transform jogador;

    [Header("Posição e zoom")]
    public float altura = 40f;          // metros acima do jogador (fora do edifício)
    public float zoom = 25f;            // metros do centro à borda do minimapa (Orthographic Size)
    public bool rodarComJogador = true; // desligado: norte sempre para cima

    [Header("Pisos do edifício")]
    [Tooltip("Ligado: dentro do edifício, o minimapa mostra só o piso onde o jogador está.")]
    public bool mostrarSoOPiso = true;
    [Tooltip("Canto sudoeste do edifício (X, Z).")]
    public Vector2 edificioMin = new Vector2(10f, 15f);
    [Tooltip("Canto nordeste do edifício (X, Z).")]
    public Vector2 edificioMax = new Vector2(72f, 40f);
    [Tooltip("Topo do chão do piso 0.")]
    public float chaoPiso0 = 0.05f;
    [Tooltip("Metros do chão de um piso ao chão do piso de cima.")]
    public float alturaPiso = 4f;
    [Tooltip("Número do último piso (2 = pisos 0, 1 e 2).")]
    public int ultimoPiso = 2;
    [Tooltip("Altura do corte acima do chão de cada piso: entre o topo das paredes (3.95) e o topo da laje (4).")]
    public float corteAcimaDoChao = 3.97f;
    [Tooltip("Altura do corte na cave: logo acima da face de baixo do teto da cave (-0.2).")]
    public float corteCave = -0.19f;

    [Header("Indicador do norte (opcional)")]
    [Tooltip("RectTransform com o pivô no centro do minimapa e o texto \"N\" em cima.")]
    public RectTransform norte;

    [Header("Estado (ver durante o Play)")]
    public bool dentroDoEdificio;
    public int piso;                    // -1 = cave
    public float alturaCorte;           // a câmara só desenha o que está abaixo disto

    // Os ícones leem estes valores para saber onde fica a borda do minimapa
    public static MinimapaCamera Instancia { get; private set; }
    public Camera Camara { get; private set; }

    // os ícones ficam no máximo aqui, logo abaixo do corte, para a câmara os ver. Fora do edifício, sem limite
    public float AlturaMaxIcones
    {
        get { return CortarAgora ? alturaCorte - 0.01f : float.PositiveInfinity; }
    }

    // o corte está a ser usado neste momento
    public bool CortarAgora
    {
        get { return mostrarSoOPiso && dentroDoEdificio; }
    }

    CharacterController cc;

    void Awake()
    {
        Instancia = this;
        Camara = GetComponent<Camera>();
        Camara.orthographic = true;
    }

    void Start()
    {
        if (jogador == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) jogador = p.transform;
        }
        if (jogador != null) cc = jogador.GetComponent<CharacterController>();
    }

    // LateUpdate: corre depois de o jogador se mexer neste frame, por isso o minimapa não treme
    void LateUpdate()
    {
        // o Player é destruído 1 s depois de morrer: o minimapa fica parado onde estava
        if (jogador == null) return;

        Camara.orthographicSize = zoom;

        Vector3 p = jogador.position;
        dentroDoEdificio = p.x > edificioMin.x && p.x < edificioMax.x
                        && p.z > edificioMin.y && p.z < edificioMax.y;

        float y;
        if (CortarAgora)
        {
            // só muda de piso com os pés no chão: um salto não troca o minimapa
            if (cc == null || cc.isGrounded) piso = PisoDoJogador();
            alturaCorte = piso < 0 ? corteCave : chaoPiso0 + piso * alturaPiso + corteAcimaDoChao;
            // o Near Clip começa a desenhar nearClipPlane metros abaixo da câmara, ou seja, no corte
            y = alturaCorte + Camara.nearClipPlane;
        }
        else
        {
            y = p.y + altura;
        }
        transform.position = new Vector3(p.x, y, p.z);

        // X = 90 olha para baixo; Y = para onde o jogador está virado (o corpo roda para os lados)
        float rumo = rodarComJogador ? jogador.eulerAngles.y : 0f;
        transform.rotation = Quaternion.Euler(90f, rumo, 0f);

        // o "N" roda à volta do minimapa para continuar a apontar para o norte do mapa
        if (norte != null) norte.localEulerAngles = new Vector3(0f, 0f, rumo);
    }

    // em que piso estão os pés do jogador (-1 = cave)
    int PisoDoJogador()
    {
        float pes = cc != null ? cc.bounds.min.y : jogador.position.y;
        // na escada da cave, passa à cave a partir de 1.5 m abaixo do chão do piso 0
        if (pes < chaoPiso0 - 1.5f) return -1;
        // na escada principal, passa ao piso de cima 1 m antes de lá chegar
        int n = Mathf.FloorToInt((pes - chaoPiso0 + 1f) / alturaPiso);
        return Mathf.Clamp(n, 0, ultimoPiso);
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }
}
