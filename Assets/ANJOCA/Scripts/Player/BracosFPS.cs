using UnityEngine;

// Braços do ANJOCA-01 à frente da câmara. Pôr no Player_Bracos (filho da Main Camera).
// É uma cópia do modelo inteiro: o script esconde a cabeça (a câmara está lá dentro),
// mantém os braços no sítio certo em relação à câmara e dá um pequeno recuo em cada tiro.
public class BracosFPS : MonoBehaviour
{
    [Header("Posição em relação à câmara (acertar durante o Play)")]
    public Vector3 posicao = new Vector3(0f, -1.7f, -0.1f);
    public Vector3 rotacao = Vector3.zero;

    [Header("Cabeça (vazio = procura sozinho)")]
    public Transform cabeca;

    [Header("Recuo do tiro")]
    public float recuoDistancia = 0.04f;   // metros que os braços vão para trás em cada tiro
    public float recuoAngulo = 3f;         // graus que a arma sobe em cada tiro
    public float recuperar = 12f;          // rapidez com que os braços voltam ao sítio

    // recuo atual, que vai voltando a 0
    float recuoAtual = 0f;

    void Awake()
    {
        if (cabeca == null)
        {
            Animator anim = GetComponent<Animator>();
            if (anim != null && anim.isHuman) cabeca = anim.GetBoneTransform(HumanBodyBones.Head);
        }
    }

    void LateUpdate()
    {
        // a câmara está dentro da cabeça: encolhe-a para não tapar o ecrã
        if (cabeca != null) cabeca.localScale = Vector3.zero;

        // o recuo volta aos poucos a 0
        recuoAtual = Mathf.Lerp(recuoAtual, 0f, recuperar * Time.deltaTime);

        // o recuo roda os braços à volta da câmara (e não dos pés do modelo), para a arma subir
        Quaternion subida = Quaternion.Euler(-recuoAngulo * recuoAtual, 0f, 0f);
        Vector3 paraTras = new Vector3(0f, 0f, -recuoDistancia * recuoAtual);

        transform.localPosition = subida * (posicao + paraTras);
        transform.localRotation = subida * Quaternion.Euler(rotacao);
    }

    // chamado pelo Weapon em cada tiro
    public void Recuo()
    {
        recuoAtual = 1f;
    }
}
