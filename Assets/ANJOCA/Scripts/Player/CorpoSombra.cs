using UnityEngine;

// Corpo do ANJOCA-01 que só se vê na sombra. Pôr no Player_Mixamo (filho do Player).
// Lê a velocidade do CharacterController do Player e passa-a ao Animator (Idle / Walk / Run).
// Também diz ao Animator se o jogador está abaixado (lido do PlayerMovement).
public class CorpoSombra : MonoBehaviour
{
    [Header("Ligações (vazio = procura sozinho)")]
    public Animator anim;
    public CharacterController controller;
    public PlayerMovement movimento;

    [Header("Animação")]
    // nome do parâmetro Float no Player_Corpo_Animator
    public string parametroVelocidade = "Speed";
    // nome do parâmetro Bool no Player_Corpo_Animator
    public string parametroAbaixado = "Abaixado";
    // segundos que a animação demora a acompanhar a velocidade (evita saltos entre Idle e Run)
    public float suavizar = 0.1f;

    // só manda o Abaixado se o parâmetro existir no Animator (evita avisos na Console antes de o criares)
    bool temAbaixado;

    void Awake()
    {
        if (anim == null) anim = GetComponent<Animator>();
        if (controller == null) controller = GetComponentInParent<CharacterController>();
        if (movimento == null) movimento = GetComponentInParent<PlayerMovement>();

        if (anim != null)
        {
            foreach (AnimatorControllerParameter p in anim.parameters)
                if (p.name == parametroAbaixado && p.type == AnimatorControllerParameterType.Bool) temAbaixado = true;
        }
    }

    void Update()
    {
        if (anim == null || controller == null) return;

        // só conta o movimento no chão, sem a subida e a queda do salto
        Vector3 velocidade = controller.velocity;
        velocidade.y = 0f;

        anim.SetFloat(parametroVelocidade, velocidade.magnitude, suavizar, Time.deltaTime);

        if (temAbaixado && movimento != null)
            anim.SetBool(parametroAbaixado, movimento.Abaixado);
    }
}