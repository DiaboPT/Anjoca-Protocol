using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Filtro alaranjado (a cor do olho do ANJOCA-01) e sons enquanto se usa a mira precisa
// (Precisão ligada + botão direito, a mesma regra da CrosshairDinamica).
// Pôr num objeto vazio da cena, na layer VolumeMira. O script cria o Volume e o perfil sozinho.
[RequireComponent(typeof(Volume))]
public class FiltroMiraPrecisa : MonoBehaviour
{
    [Header("Cor do olho")]
    public Color corOlho = new Color(1f, 0.63f, 0.24f);     // laranja âmbar (#FFA03C), tirado do olho da imagem
    [Range(0f, 1f)] public float forcaFiltro = 0.55f;       // 0 = sem cor, 1 = laranja total
    [Range(-2f, 2f)] public float exposicao = 0.2f;         // compensa o escurecimento do filtro (em EV)
    [Range(-100f, 100f)] public float saturacao = -15f;     // tira um pouco das outras cores
    [Range(-100f, 100f)] public float contraste = 10f;

    [Header("Vinheta (cantos escurecidos com a cor do olho)")]
    [Range(0f, 1f)] public float vinheta = 0.4f;
    [Range(0.01f, 1f)] public float suavidadeVinheta = 0.5f;

    [Header("Aberração cromática (cores separadas nas bordas)")]
    [Range(0f, 1f)] public float aberracao = 0.15f;

    [Header("Transição (segundos)")]
    public float tempoEntrar = 0.15f;
    public float tempoSair = 0.25f;

    [Header("Som")]
    public AudioClip somAtivar;      // Mira_Ativar
    public AudioClip somDesativar;   // Mira_Desativar
    [Range(0f, 1f)] public float volumeSom = 0.8f;

    [Header("Referências (vazio = procura sozinho)")]
    public CrosshairDinamica cruz;

    Volume volume;
    ColorAdjustments cores;
    Vignette cantos;
    ChromaticAberration aberracaoCromatica;
    AudioSource fonte;
    bool ativo;

    void Awake()
    {
        // Volume global, acima dos outros (o Sky and Fog Volume / Volume_Exterior tem Priority 0)
        volume = GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.weight = 0f;

        // perfil criado em código: só liga os campos que este filtro muda, o resto vem dos outros Volumes
        VolumeProfile perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        cores = perfil.Add<ColorAdjustments>(false);
        cantos = perfil.Add<Vignette>(false);
        aberracaoCromatica = perfil.Add<ChromaticAberration>(false);
        volume.sharedProfile = perfil;
        AplicarValores();

        // som 2D (ouve-se igual em todo o lado)
        fonte = GetComponent<AudioSource>();
        if (fonte == null) fonte = gameObject.AddComponent<AudioSource>();
        fonte.playOnAwake = false;
        fonte.spatialBlend = 0f;
    }

    void Start()
    {
        if (cruz == null) cruz = FindFirstObjectByType<CrosshairDinamica>();
    }

    void Update()
    {
        bool querAtivo = cruz != null && cruz.MiraPrecisa;

        // mudou de estado: toca o som certo
        if (querAtivo != ativo)
        {
            ativo = querAtivo;
            AudioClip som = ativo ? somAtivar : somDesativar;
            if (som != null) fonte.PlayOneShot(som, volumeSom);
        }

        // o filtro entra e sai aos poucos
        float tempo = ativo ? tempoEntrar : tempoSair;
        float passo = tempo > 0f ? Time.deltaTime / tempo : 1f;
        volume.weight = Mathf.MoveTowards(volume.weight, ativo ? 1f : 0f, passo);

        // assim dá para afinar os valores no Inspector durante o Play
        AplicarValores();
    }

    void AplicarValores()
    {
        cores.colorFilter.Override(Color.Lerp(Color.white, corOlho, forcaFiltro));
        cores.postExposure.Override(exposicao);
        cores.saturation.Override(saturacao);
        cores.contrast.Override(contraste);

        cantos.color.Override(corOlho);
        cantos.intensity.Override(vinheta);
        cantos.smoothness.Override(suavidadeVinheta);

        aberracaoCromatica.intensity.Override(aberracao);
    }
}
