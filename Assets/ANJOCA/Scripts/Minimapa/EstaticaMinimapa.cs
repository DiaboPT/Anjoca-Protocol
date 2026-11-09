using UnityEngine;
using UnityEngine.UI;

// Estática por cima do minimapa enquanto o jogador está dentro de uma ZonaEstatica (o edifício).
// Pôr numa Raw Image chamada "Estatica", filha da Mascara e por baixo do Mapa (fica cortada no círculo).
// A imagem do ruído é criada pelo script: não é preciso textura nenhuma.
[RequireComponent(typeof(RawImage))]
public class EstaticaMinimapa : MonoBehaviour
{
    [Header("Sinal")]
    [Tooltip("Desligado: a estática nunca aparece. O emissor eletromagnético vai pôr isto a false quando for sabotado.")]
    public bool bloqueado = true;

    [Header("Aspeto")]
    public Color cor = new Color(0.75f, 0.85f, 0.9f);   // cinzento azulado, como o aro
    [Range(0f, 1f)] public float opacidade = 1f;         // 1 = o mapa não se vê nada
    public int resolucao = 96;                           // píxeis do ruído (baixo = grão grosso)
    public float trocasPorSegundo = 20f;                 // quantas vezes o ruído muda por segundo
    [Range(0f, 1f)] public float forcaRiscas = 0.35f;    // riscas horizontais, como uma TV sem sinal

    [Header("Transição (segundos)")]
    public float tempoEntrar = 0.25f;
    public float tempoSair = 0.6f;

    [Header("Chiado ao entrar (opcional)")]
    public bool chiado = true;
    [Range(0f, 1f)] public float volumeChiado = 0.25f;
    public float duracaoChiado = 0.4f;

    [Header("Texto \"SEM SINAL\" (opcional)")]
    [Tooltip("Um TextoSemSinal filho do Minimapa. Pisca enquanto há estática.")]
    public GameObject textoSemSinal;

    public static EstaticaMinimapa Instancia { get; private set; }

    // quantas ZonaEstatica têm o jogador lá dentro (o edifício pode ter mais do que uma caixa)
    int zonas;
    public bool DentroDaZona => zonas > 0;
    public bool Ativa => bloqueado && DentroDaZona;

    RawImage imagem;
    Texture2D ruido;
    Color32[] pixeis;
    float forca;              // 0 = sem estática, 1 = estática total
    float proximaTroca;
    int faixa;                // risca clara que desce devagar
    AudioSource fonte;
    AudioClip somChiado;

    void Awake()
    {
        Instancia = this;

        imagem = GetComponent<RawImage>();
        imagem.raycastTarget = false;

        // Point: o grão fica quadrado e nítido, sem ser esborratado
        ruido = new Texture2D(resolucao, resolucao, TextureFormat.RGBA32, false);
        ruido.filterMode = FilterMode.Point;
        ruido.wrapMode = TextureWrapMode.Repeat;
        pixeis = new Color32[resolucao * resolucao];
        imagem.texture = ruido;
        GerarRuido();

        imagem.color = new Color(cor.r, cor.g, cor.b, 0f);
        imagem.enabled = false;
        if (textoSemSinal != null) textoSemSinal.SetActive(false);

        if (chiado)
        {
            fonte = gameObject.AddComponent<AudioSource>();
            fonte.playOnAwake = false;
            fonte.spatialBlend = 0f;   // 2D: ouve-se igual em todo o lado
            somChiado = CriarChiado();
        }
    }

    // chamados pela ZonaEstatica
    public void Entrar()
    {
        zonas++;
        if (zonas == 1 && bloqueado && fonte != null) fonte.PlayOneShot(somChiado, volumeChiado);
    }

    public void Sair()
    {
        zonas = Mathf.Max(0, zonas - 1);
    }

    void Update()
    {
        // sobe ou desce a estática aos poucos
        float alvo = Ativa ? 1f : 0f;
        float tempo = Ativa ? tempoEntrar : tempoSair;
        forca = Mathf.MoveTowards(forca, alvo, Time.deltaTime / Mathf.Max(0.01f, tempo));

        bool visivel = forca > 0f;
        imagem.enabled = visivel;

        // com a estática total, a câmara do minimapa deixa de renderizar (poupa uma passagem do HDRP)
        MinimapaCamera mini = MinimapaCamera.Instancia;
        if (mini != null) mini.Camara.enabled = forca < 1f;

        if (textoSemSinal != null)
        {
            // pisca 2 vezes por segundo
            bool pisca = Ativa && Mathf.Repeat(Time.time * 2f, 1f) < 0.6f;
            if (textoSemSinal.activeSelf != pisca) textoSemSinal.SetActive(pisca);
        }

        if (!visivel) return;

        if (Time.time >= proximaTroca)
        {
            proximaTroca = Time.time + 1f / Mathf.Max(1f, trocasPorSegundo);
            GerarRuido();
        }

        // um tremor de opacidade para parecer sinal a falhar
        float tremor = Random.Range(0.9f, 1f);
        imagem.color = new Color(cor.r, cor.g, cor.b, opacidade * forca * tremor);
    }

    void GerarRuido()
    {
        faixa = (faixa + 1) % resolucao;

        for (int y = 0; y < resolucao; y++)
        {
            // cada linha tem um brilho próprio (riscas) e a faixa é mais clara
            float linha = 1f - forcaRiscas * Random.value;
            int distFaixa = Mathf.Abs(y - (resolucao - 1 - faixa));
            if (distFaixa < 4) linha += 0.25f;

            for (int x = 0; x < resolucao; x++)
            {
                byte v = (byte)Mathf.Clamp(Random.value * 255f * linha, 0f, 255f);
                pixeis[y * resolucao + x] = new Color32(v, v, v, 255);
            }
        }

        ruido.SetPixels32(pixeis);
        ruido.Apply(false);
    }

    // ruído branco curto que vai desaparecendo, criado em código
    AudioClip CriarChiado()
    {
        int frequencia = 22050;
        int amostras = Mathf.Max(1, (int)(frequencia * duracaoChiado));
        float[] dados = new float[amostras];
        for (int i = 0; i < amostras; i++)
        {
            float fim = 1f - (float)i / amostras;
            dados[i] = (Random.value * 2f - 1f) * fim;
        }
        AudioClip clip = AudioClip.Create("ChiadoMinimapa", amostras, 1, frequencia, false);
        clip.SetData(dados, 0);
        return clip;
    }

    void OnDestroy()
    {
        // se a estática desaparecer, a câmara do minimapa volta a renderizar
        MinimapaCamera mini = MinimapaCamera.Instancia;
        if (mini != null) mini.Camara.enabled = true;
        if (ruido != null) Destroy(ruido);
        if (Instancia == this) Instancia = null;
    }
}
