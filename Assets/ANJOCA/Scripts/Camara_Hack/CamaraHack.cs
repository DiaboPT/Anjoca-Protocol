using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Câmara hack da base. Roda de um lado para o outro e não deteta ninguém:
// serve para o jogador a hackear com o DispositivoHack e depois ver por ela.
// Vai no objeto que roda (o "Pivo"). O corpo da câmara, o LED e a Lente são filhos dele.
// A imagem nasce na Lente (o eixo Z azul é para onde a câmara olha).
public class CamaraHack : MonoBehaviour
{
   [Header("Nome no dispositivo")]
   // aparece no ecrã do dispositivo. Vazio = o nome do objeto pai (por exemplo Cam_P0_Atrio)
   public string nome = "";

   [Header("Rotação")]
   // graus para cada lado a partir da posição em que a câmara foi posta (30 = varre 60 graus)
   public float anguloVarrimento = 30f;
   // graus por segundo
   public float velocidadeRotacao = 15f;
   // segundos parada em cada ponta antes de voltar
   public float pausaNasPontas = 2f;
   // graus para baixo (a câmara fica no alto e olha para o chão da sala)
   public float inclinacao = 25f;
   // ligado = começa num ângulo ao calhas, para as câmaras não rodarem todas ao mesmo tempo
   public bool inicioAleatorio = true;

   [Header("Imagem")]
   // de onde sai a imagem (um objeto vazio à frente da lente). Vazio = o próprio Pivo
   public Transform lente;
   // abertura vertical da imagem, em graus (60 a 75 parece uma câmara de vigilância)
   public float campoVisao = 70f;
   // metros até onde a imagem chega
   public float distanciaMaxima = 60f;

   [Header("LED (opcional)")]
   // a esfera pequenina do LED e o material para quando já está hackeada (verde, por exemplo)
   public Renderer led;
   public Material materialHackeada;

   // o dispositivo já entrou nesta câmara
   public bool hackeada { get; private set; }
   // altura do chão por baixo da câmara (para saber em que piso está)
   public float chaoY { get; private set; }
   // sem energia: o quadro elétrico da ala foi sabotado (o quadro tem esta câmara na lista Camaras Hackeaveis)
   public bool semSinal { get; private set; }
   public string Nome => string.IsNullOrEmpty(nome) ? (transform.parent != null ? transform.parent.name : name) : nome;

   // raiz da câmara (o objeto pai do Pivo): o raio que procura o chão salta as peças dela
   private Transform raiz;
   private Camera vista;
   private Quaternion rotacaoInicial;
   private float anguloAtual = 0f;
   private int sentido = 1;
   private float pausaAte = 0f;

   private void Awake()
   {
      if (lente == null) lente = transform;
      raiz = transform.parent != null ? transform.parent : transform;
      rotacaoInicial = transform.localRotation;

      if (inicioAleatorio)
      {
         anguloAtual = Random.Range(-anguloVarrimento, anguloVarrimento);
         sentido = Random.value < 0.5f ? 1 : -1;
      }
      AplicarRotacao();

      ProcurarChao();
   }

   private void Update()
   {
      // sem energia fica parada
      if (semSinal) return;

      if (Time.time >= pausaAte)
      {
         anguloAtual += sentido * velocidadeRotacao * Time.deltaTime;
         if (Mathf.Abs(anguloAtual) >= anguloVarrimento)
         {
            anguloAtual = Mathf.Clamp(anguloAtual, -anguloVarrimento, anguloVarrimento);
            sentido = -sentido;
            pausaAte = Time.time + pausaNasPontas;
         }
      }
      AplicarRotacao();
   }

   private void AplicarRotacao()
   {
      // primeiro roda para o lado, depois inclina para baixo (como um suporte de câmara a sério)
      transform.localRotation = rotacaoInicial * Quaternion.Euler(0f, anguloAtual, 0f) * Quaternion.Euler(inclinacao, 0f, 0f);
   }

   // chão por baixo da câmara, sem contar com as peças dela
   private void ProcurarChao()
   {
      chaoY = transform.position.y - 3f;
      RaycastHit[] hits = Physics.RaycastAll(transform.position, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);
      System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
      foreach (RaycastHit hit in hits)
      {
         if (hit.transform.IsChildOf(raiz)) continue;
         chaoY = hit.point.y;
         return;
      }
   }

   // chamado pelo QuadroEletrico quando é sabotado: fica parada e sem imagem até ao fim do nível
   public void CortarEnergia()
   {
      semSinal = true;
      if (vista != null) vista.enabled = false;
   }

   // chamado pelo DispositivoHack quando acaba de hackear
   public void Hackear()
   {
      hackeada = true;
      if (led != null && materialHackeada != null) led.sharedMaterial = materialHackeada;
   }

   // chamado pelo DispositivoHack: liga a imagem desta câmara para a textura do ecrã, ou desliga-a.
   // Só a câmara que está no ecrã desenha a imagem (as outras ficam desligadas e não pesam nada).
   public void Mostrar(RenderTexture ecra, Camera principal)
   {
      if (vista == null) CriarVista(principal);
      // desliga antes de tirar a textura: uma câmara ligada sem textura desenhava por cima do ecrã do jogador
      if (ecra == null || semSinal) vista.enabled = false;
      vista.targetTexture = ecra;
      if (ecra != null && !semSinal) vista.enabled = true;
   }

   // cria a câmara da imagem (um objeto filho da Lente) com as mesmas camadas e Volumes da câmara do jogador
   private void CriarVista(Camera principal)
   {
      GameObject obj = new GameObject("Vista");
      obj.transform.SetParent(lente, false);

      vista = obj.AddComponent<Camera>();
      vista.enabled = false;
      vista.fieldOfView = campoVisao;
      vista.nearClipPlane = 0.05f;
      vista.farClipPlane = distanciaMaxima;

      // o HDRP pode já ter acrescentado os dados da câmara sozinho
      if (!obj.TryGetComponent(out HDAdditionalCameraData dados)) dados = obj.AddComponent<HDAdditionalCameraData>();
      if (principal != null)
      {
         // as mesmas camadas que o jogador vê (os ícones do minimapa ficam de fora)
         vista.cullingMask = principal.cullingMask;
         HDAdditionalCameraData dadosPrincipal = principal.GetComponent<HDAdditionalCameraData>();
         // os mesmos Volumes (exposição, luz da base, cave). O Volume_Minimapa fica de fora
         if (dadosPrincipal != null) dados.volumeLayerMask = dadosPrincipal.volumeLayerMask;
      }
   }

   private void OnDestroy()
   {
      if (vista != null) vista.targetTexture = null;
   }

   // para onde a câmara olha, na Scene, quando está selecionada
   private void OnDrawGizmosSelected()
   {
      Transform origem = lente != null ? lente : transform;
      Gizmos.color = Color.cyan;
      Gizmos.DrawLine(origem.position, origem.position + origem.forward * 8f);
   }
}
