using UnityEngine;

// Corpo do ANJOCA-01 que só se vê na sombra. Pôr no Player_Mixamo (filho do Player).
// Lê a velocidade do CharacterController do Player e passa-a ao Animator (Idle / Walk / Run).
public class CorpoSombra : MonoBehaviour
{
    [Header("Ligações (vazio = procura sozinho)")]
    public Animator anim;
    public CharacterController controller;

    [Header("Animação")]
    // nome do parâmetro Float no Player_Corpo_Animator
    public string parametroVelocidade = "Speed";
    // segundos que a animação demora a acompanhar a velocidade (evita saltos entre Idle e Run)
    public float suavizar = 0.1f;

    void Awake()
    {
        if (anim == null) anim = GetComponent<Animator>();
        if (controller == null) controller = GetComponentInParent<CharacterController>();
    }

    void Update()
    {
        if (anim == null || controller == null) return;

        // só conta o movimento no chão, sem a subida e a queda do salto
        Vector3 velocidade = controller.velocity;
        velocidade.y = 0f;

        anim.SetFloat(parametroVelocidade, velocidade.magnitude, suavizar, Time.deltaTime);
    }
}
