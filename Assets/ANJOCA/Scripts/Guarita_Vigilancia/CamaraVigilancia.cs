using UnityEngine;

// Câmara de vigilância de uma guarita: roda de um lado para o outro e, assim que vê o Player,
// avisa a Guarita, que solta os soldados que tem lá dentro.
// Desenha em jogo um cone vermelho transparente com o que vê,
// cortado pelas paredes e pelo chão, para o jogador saber por onde não pode passar.
// Vai no objeto que roda (o "Pivo"). O corpo da câmara, a Lente e a Luz são filhos dele.
public class CamaraVigilancia : MonoBehaviour
{
   [Header("Guarita")]
   // guarita que esta câmara vigia. Vazio = procura nos pais (a câmara é filha da guarita)
   public Guarita guarita;

   [Header("Rotação")]
   // graus para cada lado a partir da posição em que a câmara foi posta (45 = varre 90 graus)
   public float anguloVarrimento = 45f;
   // graus por segundo
   public float velocidadeRotacao = 20f;
   // segundos parada em cada ponta antes de voltar
   public float pausaNasPontas = 1.5f;
   // graus para baixo (a câmara fica no alto da guarita e olha para o chão à frente)
   public float inclinacao = 30f;
   // ligado = cada câmara começa num ângulo e num sentido ao calhas,
   // para as guaritas (e as duas câmaras da mesma guarita) não rodarem todas ao mesmo tempo
   public bool inicioAleatorio = true;

   [Header("Visão")]
   // metros até onde a câmara vê
   public float alcance = 20f;
   // abertura do cone de visão, em graus (de um lado ao outro)
   public float anguloVisao = 40f;
   // segundos sem ver o Player até voltar a rodar
   public float tempoEsquecer = 4f;
   // de onde sai a visão e o cone (um objeto vazio à frente da lente). Vazio = o próprio Pivo
   public Transform lente;
   // camadas que tapam a visão (paredes, árvores, terreno) e a camada do Player
   public LayerMask camadasVisao = ~0;

   [Header("Cor")]
   // cor do cone e da luz (sempre a mesma)
   public Color corCone = Color.red;

   [Header("Cone visível")]
   // material transparente do cone (HDRP Unlit, Transparent, Double-Sided). Vazio = sem cone.
   // a cor é a do campo Cor Cone
   public Material materialCone;
   // 0 = invisível, 1 = opaco
   [Range(0f, 1f)] public float opacidadeCone = 0.15f;
   // desligado = só se vê a mancha no chão; ligado = vê-se também o cone no ar
   public bool mostrarLados = true;
   // raios à volta do cone (mais = contorno mais redondo, mas mais pesado)
   [Range(8, 64)] public int raiosPorAnel = 32;
   // anéis do centro para fora (mais = a mancha no chão segue melhor os desníveis)
   [Range(1, 6)] public int aneis = 3;

   [Header("Luz (opcional, Spot Light filha do Pivo)")]
   public Light luz;
   // o ângulo e o alcance da luz seguem os da visão
   public bool luzIgualVisao = true;

   [Header("Sons (opcionais)")]
   public AudioSource audioSource;
   // quando te vê e dá o alerta
   public AudioClip somAlerta;

   // estado que os outros scripts podem ler (Modo Debug, por exemplo)
   public bool emAlerta { get; private set; }

   private Transform jogador;
   private Transform alvoJogador;
   private Quaternion rotacaoInicial;
   // ângulo atual para o lado (0 = como foi posta) e para baixo
   private float anguloAtual = 0f;
   private float inclinacaoAtual;
   // 1 = a rodar para a direita, -1 = para a esquerda
   private int sentido = 1;
   private float pausaAte = 0f;
   private float ultimaVezQueViu = -999f;
   private float desligadaAte = 0f;

   // cone visível
   private Transform cone;
   private MeshRenderer coneRenderer;
   private Mesh coneMesh;
   private Material coneMaterial;
   private Vector3[] coneVertices;
   private Vector3[] coneDirecoes;
   private int propriedadeCor = -1;

   private void Awake()
   {
      if (guarita == null) guarita = GetComponentInParent<Guarita>();
      if (lente == null) lente = transform;
      if (audioSource == null) audioSource = GetComponent<AudioSource>();

      // o mesmo que o Enemy: o objeto do jogador chama-se "Player" e tem um filho "Look" à altura do peito
      GameObject p = GameObject.Find("Player");
      if (p != null)
      {
         jogador = p.transform;
         alvoJogador = jogador.Find("Look");
      }

      rotacaoInicial = transform.localRotation;
      inclinacaoAtual = inclinacao;

      if (inicioAleatorio)
      {
         // um ponto qualquer entre as duas pontas do varrimento, a rodar para um lado ou para o outro
         anguloAtual = Random.Range(-anguloVarrimento, anguloVarrimento);
         sentido = Random.value < 0.5f ? 1 : -1;
      }
      AplicarRotacao();

      CriarCone();
      MudarCor(corCone);
   }

   private void OnValidate()
   {
      // no Editor, a luz acompanha logo os valores da visão
      if (luz != null && luzIgualVisao)
      {
         luz.type = LightType.Spot;
         luz.spotAngle = anguloVisao;
         luz.range = alcance;
      }
   }

   private void Update()
   {
      if (jogador == null) return;

      // desligada pelo posto de vigilância ou pelo quadro elétrico
      if (Time.time < desligadaAte) return;
      MostrarCone(true);

      bool ve = VeJogador();

      if (ve)
      {
         ultimaVezQueViu = Time.time;
         // segue o jogador enquanto o vê
         SeguirJogador();
         // viu-te: alerta logo. Só uma vez, até te perder de vista
         if (!emAlerta) DarAlerta();
      }
      else if (Time.time - ultimaVezQueViu > tempoEsquecer)
      {
         // perdeu-te de vista há tempo suficiente: volta a vigiar (e pode voltar a dar o alerta)
         emAlerta = false;
         Varrer();
      }

      AplicarRotacao();
   }

   private void LateUpdate()
   {
      // depois de rodar: o cone acompanha a câmara e para nas paredes e no chão
      if (Time.time >= desligadaAte) AtualizarCone();
   }

   // roda de uma ponta à outra, com uma pausa em cada ponta
   private void Varrer()
   {
      // volta devagar à inclinação normal (pode ter ficado a olhar para outro lado ao seguir-te)
      inclinacaoAtual = Mathf.MoveTowards(inclinacaoAtual, inclinacao, velocidadeRotacao * Time.deltaTime);

      if (Time.time < pausaAte) return;

      anguloAtual += sentido * velocidadeRotacao * Time.deltaTime;

      if (Mathf.Abs(anguloAtual) >= anguloVarrimento)
      {
         anguloAtual = Mathf.Clamp(anguloAtual, -anguloVarrimento, anguloVarrimento);
         sentido = -sentido;
         pausaAte = Time.time + pausaNasPontas;
      }
   }

   // roda para o jogador, mas sem passar das pontas do varrimento (o suporte não deixa)
   private void SeguirJogador()
   {
      // direção para o jogador vista a partir da posição inicial da câmara
      Quaternion baseMundo = (transform.parent != null ? transform.parent.rotation : Quaternion.identity) * rotacaoInicial;
      Vector3 d = Quaternion.Inverse(baseMundo) * (PontoAlvo() - lente.position);

      float anguloAlvo = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
      float inclinacaoAlvo = Mathf.Atan2(-d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;

      anguloAlvo = Mathf.Clamp(anguloAlvo, -anguloVarrimento, anguloVarrimento);
      inclinacaoAlvo = Mathf.Clamp(inclinacaoAlvo, -10f, 70f);

      // a seguir roda mais depressa do que a vigiar
      float rapidez = velocidadeRotacao * 3f * Time.deltaTime;
      anguloAtual = Mathf.MoveTowards(anguloAtual, anguloAlvo, rapidez);
      inclinacaoAtual = Mathf.MoveTowards(inclinacaoAtual, inclinacaoAlvo, rapidez);
   }

   private void AplicarRotacao()
   {
      // primeiro inclina para baixo, depois roda para o lado (como um suporte de câmara a sério)
      transform.localRotation = rotacaoInicial * Quaternion.Euler(inclinacaoAtual, anguloAtual, 0f);
   }

   // o jogador está dentro do cone e nada o tapa
   private bool VeJogador()
   {
      Vector3 alvo = PontoAlvo();
      Vector3 paraAlvo = alvo - lente.position;

      if (paraAlvo.magnitude > alcance) return false;
      if (Vector3.Angle(lente.forward, paraAlvo) > anguloVisao * 0.5f) return false;

      // vê o peito ou os pés: basta um dos dois não estar tapado
      return LinhaLivre(alvo) || LinhaLivre(jogador.position + Vector3.up * 0.3f);
   }

   private bool LinhaLivre(Vector3 alvo)
   {
      Vector3 direcao = alvo - lente.position;
      RaycastHit[] hits = Physics.RaycastAll(lente.position, direcao.normalized, direcao.magnitude + 0.5f,
         camadasVisao, QueryTriggerInteraction.Ignore);
      System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

      foreach (RaycastHit hit in hits)
      {
         // o corpo da própria câmara não conta
         if (hit.transform.IsChildOf(transform)) continue;
         // a primeira coisa em que bate é o jogador: vê-o. Outra coisa qualquer: está tapado
         return hit.transform.IsChildOf(jogador);
      }
      // não bateu em nada (o collider do jogador não está nas Camadas Visão)
      return false;
   }

   private Vector3 PontoAlvo()
   {
      return alvoJogador != null ? alvoJogador.position : jogador.position + Vector3.up * 1.2f;
   }

   private void DarAlerta()
   {
      emAlerta = true;
      Tocar(somAlerta);

      if (guarita != null) guarita.Alertar();
      else Debug.LogWarning(name + ": câmara sem Guarita, o alerta não solta ninguém");
   }

   // para o posto de vigilância e o quadro elétrico do GDD: apaga a câmara durante uns segundos
   // (Mathf.Infinity = até ao fim do nível)
   public void Desligar(float segundos)
   {
      // nunca encurta um corte que já existe: depois do quadro elétrico (para sempre),
      // um Desligar(90f) do posto de vigilância não pode voltar a ligar a câmara ao fim de 90 s
      desligadaAte = Mathf.Max(desligadaAte, Time.time + segundos);
      emAlerta = false;
      MostrarCone(false);
   }

   // ---------- Cone visível ----------

   // cria um objeto filho da Lente com uma malha que é refeita em cada frame
   private void CriarCone()
   {
      if (materialCone == null) return;

      GameObject obj = new GameObject("ConeVisao");
      cone = obj.transform;
      cone.SetParent(lente, false);

      coneMesh = new Mesh();
      coneMesh.name = "ConeVisao";
      coneMesh.MarkDynamic();
      obj.AddComponent<MeshFilter>().sharedMesh = coneMesh;

      coneRenderer = obj.AddComponent<MeshRenderer>();
      // uma cópia do material para cada câmara, para cada uma ter a sua cor
      coneMaterial = new Material(materialCone);
      coneRenderer.sharedMaterial = coneMaterial;
      coneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      coneRenderer.receiveShadows = false;

      // a cor chama-se de maneira diferente conforme o shader
      foreach (string nome in new[] { "_UnlitColor", "_BaseColor", "_Color" })
      {
         if (coneMaterial.HasProperty(nome)) { propriedadeCor = Shader.PropertyToID(nome); break; }
      }

      // vértices: 0 = ponta (na lente), 1 = centro, depois cada anel de dentro para fora
      int n = raiosPorAnel;
      coneVertices = new Vector3[2 + aneis * n];
      coneDirecoes = new Vector3[coneVertices.Length];

      // direções de cada raio, no espaço da lente (o eixo Z é para onde a câmara olha)
      float meio = anguloVisao * 0.5f;
      coneDirecoes[1] = Vector3.forward;
      for (int a = 0; a < aneis; a++)
      {
         float abertura = meio * (a + 1) / aneis;
         for (int i = 0; i < n; i++)
         {
            float volta = 360f * i / n;
            coneDirecoes[2 + a * n + i] = Quaternion.AngleAxis(volta, Vector3.forward)
               * Quaternion.AngleAxis(abertura, Vector3.up) * Vector3.forward;
         }
      }

      // triângulos (fixos: só os vértices mudam de frame para frame)
      var tri = new System.Collections.Generic.List<int>();
      // mancha: do centro para o primeiro anel
      for (int i = 0; i < n; i++)
         tri.AddRange(new[] { 1, 2 + i, 2 + (i + 1) % n });
      // mancha: entre cada anel e o seguinte
      for (int a = 0; a < aneis - 1; a++)
      {
         for (int i = 0; i < n; i++)
         {
            int dentro = 2 + a * n + i, dentroSeg = 2 + a * n + (i + 1) % n;
            int fora = dentro + n, foraSeg = dentroSeg + n;
            tri.AddRange(new[] { dentro, fora, foraSeg, dentro, foraSeg, dentroSeg });
         }
      }
      // lados: da ponta até ao anel de fora
      if (mostrarLados)
      {
         int ultimo = 2 + (aneis - 1) * n;
         for (int i = 0; i < n; i++)
            tri.AddRange(new[] { 0, ultimo + i, ultimo + (i + 1) % n });
      }

      AtualizarCone();
      coneMesh.SetTriangles(tri, 0);
      coneMesh.RecalculateBounds();
   }

   // cada raio vai até ao alcance ou até bater numa parede ou no chão
   private void AtualizarCone()
   {
      if (cone == null) return;

      coneVertices[0] = Vector3.zero;
      for (int v = 1; v < coneVertices.Length; v++)
      {
         Vector3 direcaoMundo = lente.TransformDirection(coneDirecoes[v]).normalized;
         float distancia = DistanciaLivre(lente.position, direcaoMundo, alcance);
         coneVertices[v] = cone.InverseTransformPoint(lente.position + direcaoMundo * distancia);
      }

      coneMesh.vertices = coneVertices;
      coneMesh.RecalculateBounds();
   }

   // distância até à primeira coisa que tapa a visão. O jogador não corta o cone (atravessa-o)
   private float DistanciaLivre(Vector3 origem, Vector3 direcao, float maximo)
   {
      float andado = 0f;
      // no máximo 3 tentativas: o corpo da câmara e o jogador são saltados
      for (int t = 0; t < 3; t++)
      {
         if (!Physics.Raycast(origem, direcao, out RaycastHit hit, maximo - andado,
               camadasVisao, QueryTriggerInteraction.Ignore))
            return maximo;

         bool saltar = hit.transform.IsChildOf(transform) || (jogador != null && hit.transform.IsChildOf(jogador));
         // um pouco antes do ponto, para a mancha não piscar em cima do chão
         if (!saltar) return Mathf.Max(0f, andado + hit.distance - 0.03f);

         andado += hit.distance + 0.05f;
         origem = hit.point + direcao * 0.05f;
      }
      return maximo;
   }

   private void MostrarCone(bool mostrar)
   {
      if (coneRenderer != null && coneRenderer.enabled != mostrar) coneRenderer.enabled = mostrar;
      if (luz != null && luz.enabled != mostrar) luz.enabled = mostrar;
   }

   private void MudarCor(Color cor)
   {
      if (luz != null) luz.color = cor;
      if (coneMaterial != null && propriedadeCor != -1)
      {
         cor.a = opacidadeCone;
         coneMaterial.SetColor(propriedadeCor, cor);
      }
   }

   private void Tocar(AudioClip som)
   {
      if (som != null && audioSource != null) audioSource.PlayOneShot(som);
   }

   private void OnDestroy()
   {
      if (coneMaterial != null) Destroy(coneMaterial);
      if (coneMesh != null) Destroy(coneMesh);
   }

   // cone de visão na Scene quando a câmara está selecionada (antes de carregar Play)
   private void OnDrawGizmosSelected()
   {
      Transform origem = lente != null ? lente : transform;
      Gizmos.color = Color.red;

      float meio = anguloVisao * 0.5f;
      Vector3[] bordas =
      {
         Quaternion.AngleAxis(meio, origem.up) * origem.forward,
         Quaternion.AngleAxis(-meio, origem.up) * origem.forward,
         Quaternion.AngleAxis(meio, origem.right) * origem.forward,
         Quaternion.AngleAxis(-meio, origem.right) * origem.forward,
      };
      foreach (Vector3 b in bordas)
         Gizmos.DrawLine(origem.position, origem.position + b * alcance);
      Gizmos.DrawLine(origem.position, origem.position + origem.forward * alcance);
   }
}
