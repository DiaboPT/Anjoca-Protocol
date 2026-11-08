using UnityEngine;
using UnityEngine.InputSystem;

// Movimento FPS: WASD andar, Shift correr, Espaço saltar, Ctrl esquerdo abaixar, rato olhar, Esc solta o rato.
// Pôr na Capsule. A Main Camera fica como filha da Capsule.
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimento")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float jumpHeight = 1.5f;   // salto normal (o reforçado está no SuitAbilities)
    public float gravity = -20f;

    [Header("Câmara")]
    public Transform cameraTransform; // arrastar a Main Camera
    public float mouseSensitivity = 0.1f;
    
    [Header("Zoom (depois de apanhar o módulo de Precisão)")]
    public float zoomFOV = 30f;     // campo de visão com zoom (o normal é 60)
    public float zoomSpeed = 10f;   // rapidez da transição
    public float zoomSensitivity = 0.05f;   // sensibilidade do rato com zoom total

    [Header("Andar abaixado")]
    public Key teclaAbaixar = Key.LeftCtrl;
    public bool segurarTecla = false;       // desligado: carrega uma vez para baixar e outra para levantar. Ligado: só fica abaixado enquanto a tecla estiver premida
    public float crouchSpeed = 2.5f;        // velocidade abaixado (o Shift não corre abaixado)
    public float alturaAbaixado = 1.2f;     // altura da cápsula abaixado (de pé é a do Character Controller)
    public float rapidezAbaixar = 4f;       // metros por segundo a que a cápsula e a câmara descem e sobem

    // true enquanto está abaixado (ou a meio de baixar). Lido pela sombra e, mais tarde, pela deteção dos snipers
    public bool Abaixado { get; private set; }

    Camera cam;
    float normalFOV;

    CharacterController controller;
    SuitAbilities abilities;
    float verticalVelocity;
    float pitch;

    // valores de pé, guardados no arranque
    float alturaDePe;
    float pes;                  // Y dos pés, em relação ao Player (Center Y − Height ÷ 2)
    Vector3 centroDePe;
    Vector3 cameraDePe;
    float alturaAtual;
    bool pedidoAbaixado;        // no modo de carregar uma vez: o jogador pediu para ficar abaixado

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        abilities = GetComponent<SuitAbilities>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        if (cameraTransform != null)
        {
            cam = cameraTransform.GetComponent<Camera>();
            if (cam != null) normalFOV = cam.fieldOfView;
            cameraDePe = cameraTransform.localPosition;
        }

        // a altura de pé é a que está no Character Controller
        alturaDePe = controller.height;
        centroDePe = controller.center;
        pes = centroDePe.y - alturaDePe / 2f;
        alturaAtual = alturaDePe;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // --- Olhar ---
        // Passa suavemente de mouseSensitivity para zoomSensitivity à medida que a câmara aproxima
        float sensibilidade = mouseSensitivity;
        if (cam != null)
        {
            float progressoZoom = Mathf.InverseLerp(normalFOV, zoomFOV, cam.fieldOfView); // 0 = normal, 1 = zoom total
            sensibilidade = Mathf.Lerp(mouseSensitivity, zoomSensitivity, progressoZoom);
        }

        Vector2 look = mouse.delta.ReadValue() * sensibilidade;
        transform.Rotate(0f, look.x, 0f);                 // o corpo roda para os lados
        pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);   // a câmara sobe/desce, com limite
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // --- Zoom: botão direito, só depois de apanhar o módulo de Precisão e ativar no módulo na tecla 3 ---
        if (cam != null)
        {
            bool aFazerZoom = abilities != null && abilities.precisionActive && mouse.rightButton.isPressed;
            float fovAlvo = aFazerZoom ? zoomFOV : normalFOV;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fovAlvo, zoomSpeed * Time.deltaTime);
        }
        
        // --- Abaixar ---
        bool querAbaixado;
        if (segurarTecla)
        {
            querAbaixado = kb[teclaAbaixar].isPressed;
        }
        else
        {
            if (kb[teclaAbaixar].wasPressedThisFrame) pedidoAbaixado = !pedidoAbaixado;
            // abaixado, o Espaço levanta (em vez de saltar)
            if (pedidoAbaixado && kb.spaceKey.wasPressedThisFrame) pedidoAbaixado = false;
            querAbaixado = pedidoAbaixado;
        }
        // quer levantar mas tem um teto por cima: continua abaixado até sair de lá
        Abaixado = querAbaixado || (alturaAtual < alturaDePe - 0.01f && !TemEspacoParaLevantar());
        AtualizarAltura();

        // --- Andar ---
        Vector3 move = Vector3.zero;
        if (kb.wKey.isPressed) move += transform.forward;
        if (kb.sKey.isPressed) move -= transform.forward;
        if (kb.dKey.isPressed) move += transform.right;
        if (kb.aKey.isPressed) move -= transform.right;
        move = Vector3.ClampMagnitude(move, 1f);          // na diagonal não anda mais rápido

        float speed = walkSpeed;
        if (Abaixado) speed = crouchSpeed;                 // abaixado não corre
        else if (kb.leftShiftKey.isPressed) speed = runSpeed;
        // Velocidade do Fato (tecla 5): multiplica o andar e o correr
        if (abilities != null && abilities.speedActive) speed *= abilities.speedMultiplier;

        // --- Saltar e gravidade ---
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;   // mantém colado ao chão
            if (kb.spaceKey.wasPressedThisFrame && alturaAtual >= alturaDePe - 0.01f)   // abaixado (ou a levantar) não salta
                verticalVelocity = Mathf.Sqrt(GetJumpHeight() * -2f * gravity);
        }
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * speed;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        // --- Esc solta o rato (útil no editor) ---
        if (kb.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // Encolhe ou estica a cápsula aos poucos, com os pés sempre no mesmo sítio, e a câmara desce ou sobe o mesmo
    void AtualizarAltura()
    {
        float alturaAlvo = Abaixado ? alturaAbaixado : alturaDePe;
        alturaAtual = Mathf.MoveTowards(alturaAtual, alturaAlvo, rapidezAbaixar * Time.deltaTime);

        controller.height = alturaAtual;
        controller.center = new Vector3(centroDePe.x, pes + alturaAtual / 2f, centroDePe.z);

        if (cameraTransform != null)
        {
            Vector3 posicao = cameraDePe;
            posicao.y -= alturaDePe - alturaAtual;
            cameraTransform.localPosition = posicao;
        }
    }

    // Vê se há espaço por cima da cabeça para pôr-se de pé (ex.: a passagem coberta da trincheira)
    bool TemEspacoParaLevantar()
    {
        float falta = alturaDePe - alturaAtual;
        if (falta <= 0.01f) return true;

        // uma esfera do tamanho da cápsula, a partir do topo da cabeça, sobe o que falta para ficar de pé
        float raio = controller.radius * 0.9f;
        Vector3 topo = transform.TransformPoint(controller.center + Vector3.up * (alturaAtual / 2f - controller.radius));
        RaycastHit[] toques = Physics.SphereCastAll(topo, raio, Vector3.up, falta + 0.05f, ~0, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit toque in toques)
        {
            // ignora o próprio jogador (cápsula, braços, arma)
            if (toque.collider == controller || toque.transform.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }

    // Altura do salto: reforçada se o Salto do Fato estiver ligado
    public float GetJumpHeight()
    {
        if (abilities != null && abilities.jumpActive)
            return abilities.boostedJumpHeight;
        return jumpHeight;
    }
}
