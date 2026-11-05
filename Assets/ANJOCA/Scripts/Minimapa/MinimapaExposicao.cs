using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Mantém o minimapa sempre com o mesmo aspeto, de dia e de noite.
// Quando o Sol fica mais fraco, o minimapa fica mais exposto na mesma proporção, e ao contrário.
// Pôr no Volume_Minimapa (o Volume só da câmara do minimapa).
[RequireComponent(typeof(Volume))]
public class MinimapaExposicao : MonoBehaviour
{
    [Header("Luz que faz o dia e a noite")]
    public Light sol;

    [Header("Referência: o aspeto que queres manter")]
    [Tooltip("Intensidade do Sol (lux) quando o minimapa tem o aspeto certo.")]
    public float intensidadeReferencia = 130000f;
    [Tooltip("Fixed Exposure que dá esse aspeto. Afina em Play, de dia.")]
    public float exposicaoReferencia = 14f;
    [Tooltip("Limite para a noite: sem ele, as lâmpadas ficavam brancas quando o Sol está quase apagado.")]
    public float exposicaoMinima = -4f;

    [Header("Valor atual (ver durante o Play)")]
    public float exposicaoAtual;

    Exposure exposicao;

    void Start()
    {
        // volume.profile cria uma cópia só para o Play: o ficheiro do perfil não é alterado
        VolumeProfile perfil = GetComponent<Volume>().profile;
        if (!perfil.TryGet(out exposicao)) exposicao = perfil.Add<Exposure>();

        exposicao.active = true;
        exposicao.mode.Override(ExposureMode.Fixed);
    }

    void Update()
    {
        if (sol == null || exposicao == null) return;

        // Sol desligado conta como quase sem luz (o Log de 0 não existe)
        float intensidade = sol.isActiveAndEnabled ? Mathf.Max(sol.intensity, 0.01f) : 0.01f;

        // Cada vez que o Sol cai para metade, a exposição desce 1, e a imagem fica 2x mais clara.
        // Assim o chão do minimapa fica sempre com o mesmo brilho.
        exposicaoAtual = exposicaoReferencia + Mathf.Log(intensidade / intensidadeReferencia, 2f);
        exposicaoAtual = Mathf.Max(exposicaoAtual, exposicaoMinima);

        exposicao.fixedExposure.Override(exposicaoAtual);
    }
}
