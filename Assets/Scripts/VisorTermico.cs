using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Visor térmico do Fato (tecla 4). Enquanto está ligado:
//  - o mundo fica frio: tons de azul, sem as outras cores;
//  - os objetos com AlvoTermico (soldados, drones, líder e, mais tarde, minas) ficam a vermelho vivo.
// O azul é feito com o Channel Mixer, e não com Saturation: a Saturation tirava a cor a tudo,
// também ao vermelho dos alvos (ficavam brancos).
// Pôr num objeto vazio da cena, na layer VolumeMira (a mesma do Filtro_MiraPrecisa).
// O script cria o Volume e o perfil sozinho.
[RequireComponent(typeof(Volume))]
public class VisorTermico : MonoBehaviour
{
    [Header("Mundo frio")]
    [Tooltip("0 = cores normais. 1 = tudo em tons de azul, menos o que é vermelho forte (os alvos).")]
    [Range(0f, 1f)] public float forcaFrio = 1f;
    [Tooltip("Tom do mundo. Só contam o verde e o azul: o vermelho fica guardado para os alvos.")]
    public Color corMundo = new Color(0f, 0.6f, 1f);          // azul-ciano
    [Range(-2f, 3f)] public float exposicaoMundo = 0.5f;     // mais ou menos luz em tudo (em EV)
    [Range(-100f, 100f)] public float contrasteMundo = 15f;

    [Header("Vinheta")]
    [Range(0f, 1f)] public float vinheta = 0.35f;

    [Header("Cor dos alvos quentes")]
    [Tooltip("Pode ficar vazio: o script cria um material vermelho (HDRP/Unlit com emissão) que se vê mesmo no escuro.")]
    public Material materialTermico;
    [ColorUsage(false, true)] public Color corAlvos = new Color(1f, 0.05f, 0.02f);
    [Tooltip("Brilho do vermelho. Se os alvos parecerem escuros, sobe; se parecerem cor de laranja ou brancos, desce.")]
    public float brilhoAlvos = 1f;

    [Header("Transição (segundos)")]
    public float tempoEntrar = 0.2f;
    public float tempoSair = 0.3f;

    [Header("Som")]
    public AudioClip somLigar;       // por exemplo Mira_Ativar
    public AudioClip somDesligar;    // por exemplo Mira_Desativar
    [Range(0f, 1f)] public float volumeSom = 0.8f;

    [Header("Referências (vazio = procura sozinho)")]
    public SuitAbilities aprimoramentos;

    // ---------- Valores que os outros scripts leem ----------
    public static bool Ligado { get; private set; }
    public static Material MaterialAlvos { get; private set; }

    Volume volume;
    ColorAdjustments cores;
    ChannelMixer mistura;
    Vignette cantos;
    AudioSource fonte;
    bool materialCriadoAqui;

    void Awake()
    {
        // Volume global, abaixo da mira precisa (Priority 50), para o laranja da mira ganhar quando os dois estão ligados
        volume = GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 40f;
        volume.weight = 0f;

        VolumeProfile perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        cores = perfil.Add<ColorAdjustments>(false);
        mistura = perfil.Add<ChannelMixer>(false);
        cantos = perfil.Add<Vignette>(false);
        volume.sharedProfile = perfil;
        AplicarValores();

        if (materialTermico == null)
        {
            materialTermico = CriarMaterial();
            materialCriadoAqui = materialTermico != null;
        }
        MaterialAlvos = materialTermico;

        fonte = GetComponent<AudioSource>();
        if (fonte == null) fonte = gameObject.AddComponent<AudioSource>();
        fonte.playOnAwake = false;
        fonte.spatialBlend = 0f;

        Ligado = false;
    }

    void Start()
    {
        if (aprimoramentos == null) aprimoramentos = FindFirstObjectByType<SuitAbilities>();
    }

    void OnDestroy()
    {
        Ligado = false;
    }

    void Update()
    {
        // Se o Player for destruído (morte), o aprimoramentos passa a null e o visor desliga
        bool querLigado = aprimoramentos != null && aprimoramentos.thermalActive;

        if (querLigado != Ligado)
        {
            Ligado = querLigado;
            AudioClip som = Ligado ? somLigar : somDesligar;
            if (som != null) fonte.PlayOneShot(som, volumeSom);
        }

        float tempo = Ligado ? tempoEntrar : tempoSair;
        float passo = tempo > 0f ? Time.deltaTime / tempo : 1f;
        volume.weight = Mathf.MoveTowards(volume.weight, Ligado ? 1f : 0f, passo);

        // dá para afinar os valores no Inspector durante o Play
        AplicarValores();
        if (materialCriadoAqui)
            materialTermico.SetColor("_EmissiveColor", corAlvos * brilhoAlvos);
    }

    void AplicarValores()
    {
        cores.postExposure.Override(exposicaoMundo);
        cores.contrast.Override(contrasteMundo);

        // Channel Mixer (valores em %, 100 = igual). Com forcaFrio = 1:
        //  - vermelho de saída = vermelho - verde - metade do azul: o que é cinzento, verde ou azul fica sem vermelho;
        //    só o vermelho forte (os alvos) se mantém;
        //  - verde e azul de saída = brilho da imagem, pintado com a corMundo.
        float f = forcaFrio;
        float g = corMundo.g, b = corMundo.b;
        mistura.redOutRedIn.Override(100f);
        mistura.redOutGreenIn.Override(Mathf.Lerp(0f, -100f, f));
        mistura.redOutBlueIn.Override(Mathf.Lerp(0f, -50f, f));
        mistura.greenOutRedIn.Override(Mathf.Lerp(0f, 10f * g, f));
        mistura.greenOutGreenIn.Override(Mathf.Lerp(100f, 70f * g, f));
        mistura.greenOutBlueIn.Override(Mathf.Lerp(0f, 20f * g, f));
        mistura.blueOutRedIn.Override(Mathf.Lerp(0f, 10f * b, f));
        mistura.blueOutGreenIn.Override(Mathf.Lerp(0f, 70f * b, f));
        mistura.blueOutBlueIn.Override(Mathf.Lerp(100f, 20f * b, f));

        cantos.color.Override(Color.black);
        cantos.intensity.Override(vinheta);
        cantos.smoothness.Override(0.5f);
    }

    // Material vermelho que não depende das luzes da cena: HDRP/Unlit, só com emissão.
    // Exposure Weight = 0 faz com que a exposição da câmara não o escureça (vê-se igual de dia e de noite).
    Material CriarMaterial()
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("VisorTermico: não encontrei o shader HDRP/Unlit. Arrasta um material para Material Termico.");
            return null;
        }

        Material mat = new Material(shader);
        mat.name = "M_Termico (criado pelo VisorTermico)";
        mat.SetColor("_UnlitColor", Color.black);
        mat.SetColor("_EmissiveColor", corAlvos * brilhoAlvos);
        mat.SetFloat("_EmissiveExposureWeight", 0f);
        HDMaterial.ValidateMaterial(mat);
        return mat;
    }
}
