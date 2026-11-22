using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// estados do soldado (ver o guia explicacoes/Inimigos_Inteligentes.md)
public enum EstadoInimigo
{
   Calmo,        // patrulha ou fica no posto
   Desconfiado,  // viu-te de relance ou ouviu algo: vai ver
   Combate,      // vê-te: dispara
   Perseguir,    // perdeu-te de vista há pouco: vai para onde achas que foste
   Procurar,     // chegou e não estás lá: visita os esconderijos prováveis
   Alerta        // perdeu-te o rasto: volta ao posto, mais atento, durante um tempo
}

public class Enemy : MonoBehaviour, IOuvinte
{
   [Header("Enemy Health and Damage")]
   public float enemyHealth = 100f;
   public float presentHealth;
   public float giveDamage = 5f;
   // public HealthBar healthBar;

   [Header("Enemy Things")]
   public NavMeshAgent enemyAgent;
   public Transform LookPoint;
   // ponta da arma, de onde sai o tiro (um objeto vazio basta, já não precisa de ser uma Camera)
   public Transform ShootingRaycastArea;
   public Transform playerBody;
   public LayerMask playerLayer;

   [Header("Enemy Guarding Var")]
   public GameObject[] walkPoints;
   private int currentEnemyPosition = 0;
   public float enemySpeed;


   [Header("Sounds and UI")]
   public AudioClip shootingSound;
   public AudioSource audioSource;
   // sons de morte: se houver mais do que um, escolhe um ao calhas
   public AudioClip[] sonsMorte;
   // sons de dor quando leva um tiro e ainda fica vivo (também escolhe um ao calhas)
   public AudioClip[] sonsDor;

   [Header("Sangue")]
   // prefab do efeito de sangue (Particle System), criado onde a bala do jogador acerta
   public ParticleSystem efeitoSangue;

   [Header("Alerta")]
   // vozes de alerta quando te deteta (escolhe uma ao calhas)
   public AudioClip[] sonsAlerta;
   // segundos até poder dar outro alerta (um valor muito grande = só da primeira vez)
   public float intervaloAlerta = 15f;
   private float proximoAlerta = 0f;

   [Header("Vozes dos estados (opcionais)")]
   // "Quem está aí?", "Ouviste isto?"
   public AudioClip[] sonsDesconfiado;
   // "Foi por ali!"
   public AudioClip[] sonsPerseguir;
   // "Procurem!", "Sei que estás aí"
   public AudioClip[] sonsProcurar;
   // "Perdi-o", "Deve ter fugido"
   public AudioClip[] sonsPerdeu;

   [Header("Passos")]
   // sons de passos (escolhe um ao calhas em cada passo)
   public AudioClip[] sonsPassos;
   // metros andados entre dois passos (a correr, como anda mais depressa, os passos saem mais seguidos)
   public float distanciaPasso = 0.8f;
   [Range(0f, 1f)] public float volumePassos = 0.6f;
   // Audio Source só para os passos (vazio = o script cria um igual ao dos tiros)
   public AudioSource fontePassos;
   // metros andados desde o último passo
   private float distanciaAndada = 0f;

   [Header("Enemy Shooting Var")]
   public float timeBetweenShoot;
   bool previouslyShoot;
   // depois de levar um tiro, não dispara enquanto a animação Hit (layer Reacao) estiver a tocar.
   // isto é o tempo MÍNIMO: se a animação for mais curta, fica atordoado pelo menos isto
   public float tempoAtordoado = 0.2f;
   // momento (Time.time) a partir do qual volta a poder disparar
   private float atordoadoAte = 0f;
   // número da layer Reacao no Animator (-1 = não existe)
   private int layerReacao = -1;

   [Header("Recarregar")]
   // tiros por carregador; quando chega a 0, para para recarregar
   public int balasCarregador = 30;
   // carregadores com que começa, contando com o que já está na arma (3 x 30 = 90 balas)
   public int carregadores = 3;
   // segundos a recarregar (sem disparar)
   public float tempoRecarregar = 2f;
   // som mecânico do carregador a sair e a entrar
   public AudioClip somRecarregar;
   // vozes a avisar que está a recarregar (escolhe uma ao calhas)
   public AudioClip[] vozesRecarregar;
   // balas que ainda tem no carregador (aparece no Inspector para ver durante o Play)
   [SerializeField] private int balasRestantes;
   // carregadores de reserva que ainda tem (aparece no Inspector para ver durante o Play)
   [SerializeField] private int carregadoresRestantes;
   private bool aRecarregar = false;

   [Header("Pontaria")]
   // desvio máximo de cada tiro, em graus, com o soldado parado (0 = acerta sempre)
   public float desvioMira = 2.5f;
   // graus a somar ao desvio quando o soldado dispara a andar ou a correr
   public float desvioExtraAAndar = 2f;
   // no fim do Alcance Disparo, o desvio é multiplicado por (1 + isto). 0.5 = mais 50% ao longe
   public float desvioExtraAoLonge = 0.5f;

   [Header("Combate")]
   // metros a que dispara, sempre com linha de vista. Mais longe, aproxima-se
   public float alcanceDisparo = 25f;
   // segundos entre te detetar e o primeiro tiro (ao calhas entre os dois). Dá-te tempo para te esconderes
   public Vector2 tempoReacao = new Vector2(0.3f, 0.5f);
   // o primeiro tiro depois de te detetar tem o desvio multiplicado por isto
   public float desvioPrimeiroTiro = 2f;
   // segundos sem te ver antes de passar a Perseguir. Um pilar ou o caixilho de uma porta a passar à frente não conta
   public float toleranciaPerderVista = 0.6f;
   // se te volta a ver até estes segundos depois de te perder, já está a apontar: sem tempo de reação nem primeiro tiro torto
   public float memoriaMira = 3f;
   // a perseguir, anda a enemySpeed vezes isto
   public float multiplicadorCorrida = 1.6f;

   [Header("Ouvido e equipa")]
   // metros a que os colegas ouvem o "Está ali!" quando te deteta (metade com uma parede pelo meio)
   public float raioAviso = Ruido.RaioAviso;
   // enquanto te vê, volta a avisar os colegas com a tua posição a cada X segundos
   public float intervaloAviso = 3f;
   // metros a que os colegas ouvem o grito quando morre
   public float raioGrito = Ruido.RaioGrito;
   // quantos soldados podem disparar ao mesmo tempo (os outros apontam e esperam a vez)
   public int atiradoresAoMesmoTempo = 2;
   // ao ouvir o aviso de um colega, os que não vão atrás de ti ficam X segundos parados, virados para lá
   public float tempoVigiar = 20f;
   // o 2.º a ouvir o aviso vai X metros para lá de onde estás, para te cortar a fuga
   public float distanciaFlanco = 6f;
   // a perseguir-te ou à tua procura: um tiro ou passos teus a menos de X metros fazem-no parar e virar-se para lá
   public float distanciaVirarRuido = 6f;

   [Header("Enemy Animation and Spark Effect")]
   public Animator anim;
   // graus a somar à mira, se a arma ainda ficar um pouco torta (+ roda para a esquerda, - para a direita)
   public float ajusteMira = 0f;
   public ParticleSystem muzzleSpark;
   // luz laranja na ponta da arma, acesa só durante o clarão (fica desligada no Inspector)
   public Light luzDisparo;
   public float duracaoLuz = 0.05f;

   [Header("Enemy Mood/Situation")]
   // já não decidem se te vê (isso agora é a PercecaoInimigo). Ficam para os outros scripts:
   // o IconeMinimapa usa visionRadius = 0 para saber que morreu, e o ModoDebug mostra-os
   public float visionRadius;
   public float shootingRadius;
   // vê-te agora e está em combate (o Lider, a Guarita, a DetecaoZona e o minimapa leem isto)
   public bool playerInVisionRadius;
   // vê-te e estás dentro do Alcance Disparo
   public bool playerInShootingRadius;

   [Header("Estado (só para ver durante o Play)")]
   public EstadoInimigo estado = EstadoInimigo.Calmo;
   // ouviu um barulho ou viu-te de relance e foi ver
   public bool aInvestigar = false;

   [Header("Memória e rasto")]
   // segundos, depois de te perder de vista, em que ainda sabe para onde foste (viu-te dobrar a esquina). 0 = desliga
   public float janelaIntuicao = 2f;
   // segundos à frente da tua fuga onde fica o ponto previsto
   public float tempoPrevisao = 1.5f;
   // metros máximos entre a última posição e o ponto previsto
   public float distanciaMaximaPrevisao = 8f;
   // anel à volta do ponto previsto onde procura esconderijos (mínimo e máximo, em metros)
   public float raioProcuraMinimo = 4f;
   public float raioProcuraMaximo = 15f;
   // caminho máximo (metros a andar) até um esconderijo, para não ir procurar ao outro lado da base
   public float caminhoMaximoProcura = 25f;
   // quantos esconderijos visita (ao calhas entre os dois)
   public int pontosAVisitarMinimo = 3;
   public int pontosAVisitarMaximo = 5;
   // segundos parado em cada esconderijo, a olhar à volta (ao calhas entre os dois)
   public Vector2 paragemEmCadaPonto = new Vector2(1.5f, 2.5f);
   // graus que varre com o olhar para cada lado quando para
   public float anguloVarrimento = 60f;
   // segundos sem nenhuma pista nova até desistir de procurar
   public float tempoProcura = 30f;
   // segundos de procura curta, depois de ir ver um barulho ou um relance e não encontrar nada
   public float tempoProcuraCurta = 10f;
   // segundos a olhar para o barulho quando chega ao sítio, antes de procurar à volta
   public float tempoOlharDesconfiado = 3f;
   // segundos mais atento depois de desistir (o alarme geral do GDD também é de 60 s)
   public float tempoAlerta = 60f;

   [Header("Medidor por estado")]
   // o medidor da PercecaoInimigo sobe mais depressa quando já anda à tua procura
   public float multiplicadorAlerta = 1.5f;
   public float multiplicadorCaca = 2.5f;

   [Header("Escuro (quadro elétrico sabotado)")]
   // Spot Light filha do soldado (na cabeça ou na arma), desligada no Inspector. Acende-se no escuro
   public Light lanterna;
   // metros que vê, na visão central, numa ZonaSemEnergia (fora do feixe da lanterna)
   public float raioVisaoEscuro = 6f;
   // está numa zona sem energia (aparece no Inspector para ver durante o Play)
   public bool noEscuro = false;

   // Usado para não ser necessário walkPoints nos inimigos fixos
   private Vector3 posicaoInicial;
   private Quaternion rotacaoInicial;

   // uma ZonaAlerta destes soldados tem o jogador lá dentro (0 = não). O medidor sobe mais depressa
   private float bonusZona = 0f;
   public bool NaZonaAlerta => bonusZona > 0f;

   // ossos do modelo 3D para corrigir a mira (preenchidos no Awake)
   private Transform ossoTronco;
   private Transform maoDireita;
   private Transform maoEsquerda;

   // rotação do modelo 3D para o corpo todo ficar virado para o jogador quando dispara parado
   private Quaternion rotacaoModeloInicial;
   private float rodaCorpo = 0f;

   // quanto tempo demora a cair (segundos)
   public float duracaoQueda = 0.2f;

   // linha do tiro, igual à do Player
   public LineRenderer linhaTiro;
   public float duracaoLinha = 0.1f;

   // ---------- Perceção e memória ----------
   private PercecaoInimigo percecao;
   public PercecaoInimigo Percecao => percecao;
   // último destino dado ao NavMeshAgent (para não recalcular o caminho em todos os frames)
   private Vector3 destinoAtual = new Vector3(float.MaxValue, 0f, 0f);
   private float proximoCaminho = 0f;
   // a pista que o estado atual está a usar (para saber quando aparece uma nova)
   private float contactoUsado = -1f;

   // combate
   private float podeDispararDesde = 0f;
   private bool primeiroTiro = false;
   private float semVistaDesde = -1f;          // -1 = está a ver-te
   private float ultimoMomentoCombate = -100f; // a última vez que te viu em combate

   // desconfiado (ir ver um barulho ou um relance)
   private Vector3 pontoInvestigar;
   private Vector3 fonteInvestigar;
   private float fimInvestigar = -1f;
   private float fimVirar = 0f;
   private bool chegouInvestigar = false;
   private float fimOlhar = 0f;

   // perseguir
   private float inicioPerseguir = 0f;
   private float fimIntuicao = 0f;
   private bool pontoPrevistoCalculado = false;
   private Vector3 pontoPrevisto;

   // procurar
   private readonly List<Vector3> candidatos = new List<Vector3>();
   private int indiceCandidato = 0;
   private bool aParar = false;
   private float fimParagem = 0f;
   private float inicioOlhar = 0f;
   private Vector3 direcaoOlhar;
   private Vector3 centroProcura;
   private float fimProcura = 0f;
   private float proximaMarcacao = 0f;
   // sítios que já viu e quando (para não voltar lá logo)
   private readonly List<Vector3> pontosVistos = new List<Vector3>();
   private readonly List<float> momentosVistos = new List<float>();
   private NavMeshPath caminhoTeste;

   // alerta
   private float fimAlerta = 0f;

   // ouvido e equipa
   private int prioridadeInvestigar = 0;     // quão forte foi o que está a ir ver (TipoRuido)
   private float proximoAviso = 0f;
   private bool aFlanquear = false;           // vai para o outro lado de onde estás, em vez de direito a ti
   private Vector3 pontoVigiar;
   private float fimVigiar = 0f;
   private Vector3 pontoVirarRuido;           // um barulho teu mesmo ao lado, a perseguir: vira-se para lá
   private float fimVirarRuido = -10f;
   // quem está a disparar agora (todos os soldados da cena partilham esta lista)
   private static readonly List<Enemy> atiradores = new List<Enemy>();
   public bool TemVezDeDisparar => atiradores.Contains(this);
   // vê-te, está perto e não está a recarregar, mas já há outros a disparar (é isto que o F2 mostra como "à espera")
   private bool aEsperarVez = false;

   private void Awake()
   {
      // audioSource =  GetComponent<AudioSource>();
      // se o Audio Source vier vazio, procura-o neste objeto ou nos filhos
      if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();
      // os passos têm um Audio Source à parte, para o tom de cada passo não mexer no som dos tiros
      if (fontePassos == null && audioSource != null)
      {
         fontePassos = gameObject.AddComponent<AudioSource>();
         fontePassos.playOnAwake = false;
         fontePassos.spatialBlend = audioSource.spatialBlend;
         fontePassos.rolloffMode = audioSource.rolloffMode;
         fontePassos.minDistance = audioSource.minDistance;
         fontePassos.maxDistance = audioSource.maxDistance;
         fontePassos.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
      }
      presentHealth = enemyHealth;
      balasRestantes = balasCarregador;
      carregadoresRestantes = carregadores - 1;
      // healthBar.GiveFullHealthBar(enemyHealth);
      playerBody = GameObject.Find("Player").transform;
      enemyAgent =  GetComponent<NavMeshAgent>();

      // num prefab não dá para guardar objetos da cena: se o Look Point vier vazio, vai buscar o "Look" do Player
      if (LookPoint == null) LookPoint = playerBody.Find("Look");

      posicaoInicial = transform.position;
      rotacaoInicial = transform.rotation;

      // a lanterna só se acende no escuro
      if (lanterna != null) lanterna.enabled = false;

      // a linha começa escondida
      if (linhaTiro != null) linhaTiro.enabled = false;

      // o Animator está no modelo 3D, que é filho deste objeto
      if (anim == null) anim = GetComponentInChildren<Animator>();
      if (anim != null) layerReacao = anim.GetLayerIndex("Reacao");

      // ossos usados para corrigir a mira (só existem em modelos Humanoid)
      if (anim != null && anim.isHuman)
      {
         ossoTronco = anim.GetBoneTransform(HumanBodyBones.Spine);
         maoDireita = anim.GetBoneTransform(HumanBodyBones.RightHand);
         maoEsquerda = anim.GetBoneTransform(HumanBodyBones.LeftHand);
         rotacaoModeloInicial = anim.transform.localRotation;
      }

      // os olhos do soldado. Se o prefab ainda não os tiver, acrescenta-os
      percecao = GetComponent<PercecaoInimigo>();
      if (percecao == null) percecao = gameObject.AddComponent<PercecaoInimigo>();
      percecao.Iniciar(this, playerBody, anim);

      // os passos do jogador fazem barulho. Se o Player ainda não tiver o RuidoPassosJogador, acrescenta-o
      if (playerBody != null && playerBody.GetComponent<RuidoPassosJogador>() == null)
         playerBody.gameObject.AddComponent<RuidoPassosJogador>();

      caminhoTeste = new NavMeshPath();
   }

   // só ouve quem está ligado (os soldados desligados pelo AtivarInimigos não ouvem nada)
   private void OnEnable()
   {
      if (presentHealth > 0f) Ruido.Registar(this);
   }

   private void OnDisable()
   {
      Ruido.Remover(this);
      LibertarVez();
   }

   private void Update()
   {
      // morto não faz mais nada até objecto ser destruido
      if (presentHealth <= 0) return;

      // numa zona sem energia: lanterna acesa e visão curta
      AtualizarEscuro();

      // olhos: atualiza o medidor e o que o soldado sabe de ti
      percecao.Atualizar(MultiplicadorEstado());

      // deteta-te (medidor cheio e a ver-te) em qualquer estado: combate
      if (percecao.Detetado && estado != EstadoInimigo.Combate) EntrarCombate();

      switch (estado)
      {
         case EstadoInimigo.Calmo:
         case EstadoInimigo.Alerta:      AtualizarCalmo(); break;
         case EstadoInimigo.Desconfiado: AtualizarDesconfiado(); break;
         case EstadoInimigo.Combate:     AtualizarCombate(); break;
         case EstadoInimigo.Perseguir:   AtualizarPerseguir(); break;
         case EstadoInimigo.Procurar:    AtualizarProcurar(); break;
      }

      // fora do combate não ocupa a vez de disparar
      if (estado != EstadoInimigo.Combate) LibertarVez();

      // o que os outros scripts leem (Lider, Guarita, DetecaoZona, minimapa, ModoDebug)
      playerInVisionRadius = estado == EstadoInimigo.Combate && percecao.VeAgora;
      playerInShootingRadius = playerInVisionRadius && DistanciaJogador() <= alcanceDisparo;
      aInvestigar = estado == EstadoInimigo.Desconfiado;

      // no fim do Update()
      if (anim != null)
      {
         anim.SetFloat("Speed", enemyAgent.velocity.magnitude);
         // sem munição baixa a arma
         // (em combate, mesmo no instante em que te perde atrás de um pilar, para não baixar e subir a arma)
         anim.SetBool("Shoot", estado == EstadoInimigo.Combate && TemMunicao());
      }

      // passos: um som a cada "distanciaPasso" metros andados
      float velocidade = enemyAgent.velocity.magnitude;
      if (velocidade > 0.2f)
      {
         distanciaAndada += velocidade * Time.deltaTime;
         if (distanciaAndada >= distanciaPasso)
         {
            distanciaAndada = 0f;
            TocarPasso();
         }
      }
   }

   // ======================================================================
   //  ESTADOS
   // ======================================================================

   private float MultiplicadorEstado()
   {
      switch (estado)
      {
         case EstadoInimigo.Calmo: return 1f;
         case EstadoInimigo.Alerta: return multiplicadorAlerta;
         default: return multiplicadorCaca;
      }
   }

   // ---------- Calmo e Alerta: patrulha ou posto ----------
   private void AtualizarCalmo()
   {
      // viu-te de relance: vai ver
      if (percecao.Desconfiado)
      {
         EntrarDesconfiado(percecao.UltimaPosicao, percecao.UltimaPosicao, tempoProcuraCurta);
         return;
      }

      if (estado == EstadoInimigo.Alerta && Time.time > fimAlerta) estado = EstadoInimigo.Calmo;

      // um colega avisou e outros já vão atrás de ti: fica parado, virado para lá, a vigiar a passagem
      if (Time.time < fimVigiar)
      {
         Parar();
         VirarPara(pontoVigiar, 180f);
         return;
      }

      // em alerta anda um pouco mais depressa
      Guard(estado == EstadoInimigo.Alerta ? 1.2f : 1f);
   }

   // ---------- Desconfiado: vai ver o que foi ----------
   // prioridade: quão forte foi (um TipoRuido). Um relance vale como um grito
   private void EntrarDesconfiado(Vector3 ponto, Vector3 fonte, float segundos, int prioridade = (int)TipoRuido.Grito)
   {
      if (estado != EstadoInimigo.Desconfiado) TocarSomAoCalhas(sonsDesconfiado);
      estado = EstadoInimigo.Desconfiado;
      prioridadeInvestigar = prioridade;
      fimVigiar = 0f;
      aFlanquear = false;
      pontoInvestigar = ponto;
      fonteInvestigar = fonte;
      fimInvestigar = Time.time + segundos;
      // primeiro para e vira-se para o barulho, como uma pessoa que ouve algo
      fimVirar = Time.time + 0.6f;
      chegouInvestigar = false;
      contactoUsado = percecao.MomentoContacto;
   }

   private void AtualizarDesconfiado()
   {
      // voltou a ver-te de relance: o ponto passa a ser onde te viu agora
      if (percecao.Desconfiado && percecao.MomentoContacto > contactoUsado)
      {
         pontoInvestigar = percecao.UltimaPosicao;
         fonteInvestigar = percecao.UltimaPosicao;
         chegouInvestigar = false;
         contactoUsado = percecao.MomentoContacto;
      }

      // 1) vira-se para o barulho, parado
      if (Time.time < fimVirar)
      {
         Parar();
         VirarPara(fonteInvestigar, 360f);
         return;
      }

      // 2) vai até lá, um pouco mais depressa do que a patrulha
      if (!chegouInvestigar)
      {
         IrPara(pontoInvestigar, enemySpeed * 1.5f);
         if (Chegou(1f))
         {
            chegouInvestigar = true;
            inicioOlhar = Time.time;
            fimOlhar = Time.time + tempoOlharDesconfiado;
         }
         return;
      }

      // 3) chegou: olha para o sítio do barulho, a varrer para os lados
      OlharEmVolta(fonteInvestigar - transform.position);
      if (Time.time < fimOlhar) return;

      // 4) não encontrou nada: procura à volta durante um bocado (pelo menos o tempo que a porta pediu)
      float resto = Mathf.Max(tempoProcuraCurta, fimInvestigar - Time.time);
      EntrarProcurar(pontoInvestigar, fonteInvestigar - transform.position, resto);
   }

   // ---------- Combate: vê-te ----------
   private void EntrarCombate()
   {
      estado = EstadoInimigo.Combate;
      semVistaDesde = -1f;
      if (Time.time - ultimoMomentoCombate < memoriaMira)
      {
         // perdeu-te há bocadinho: ainda está a apontar
         podeDispararDesde = Time.time + 0.1f;
         primeiroTiro = false;
      }
      else
      {
         // um instante para reagir antes do primeiro tiro, e esse tiro é menos certeiro
         podeDispararDesde = Time.time + Random.Range(tempoReacao.x, tempoReacao.y);
         primeiroTiro = true;
      }

      // voz de alerta (não repete antes de intervaloAlerta segundos)
      if (Time.time >= proximoAlerta)
      {
         TocarSomAoCalhas(sonsAlerta);
         proximoAlerta = Time.time + intervaloAlerta;
      }

      fimVigiar = 0f;
      aFlanquear = false;
      aEsperarVez = false;
      // "Está ali!": os colegas perto ficam a saber onde estás
      Avisar();
   }

   private void AtualizarCombate()
   {
      aEsperarVez = false;

      // perdeu-te de vista: espera um instante (podes só ter passado atrás de um pilar) e depois vai atrás de ti
      if (!percecao.VeAgora)
      {
         if (semVistaDesde < 0f) semVistaDesde = Time.time;
         if (Time.time - semVistaDesde >= toleranciaPerderVista)
         {
            EntrarPerseguir(true);
            return;
         }
         // entretanto não dispara: fica virado para onde te viu (ou continua a aproximar-se, se estava longe)
         if (DistanciaJogador() > alcanceDisparo) IrPara(percecao.UltimaPosicao, enemySpeed * multiplicadorCorrida);
         else
         {
            Parar();
            VirarPara(percecao.UltimaPosicao, 360f);
         }
         return;
      }
      semVistaDesde = -1f;
      ultimoMomentoCombate = Time.time;

      // enquanto te vê, volta a dizer aos colegas onde estás
      if (Time.time >= proximoAviso) Avisar();

      // longe: aproxima-se (e não ocupa a vez de disparar)
      if (DistanciaJogador() > alcanceDisparo)
      {
         LibertarVez();
         IrPara(playerBody.position, enemySpeed * multiplicadorCorrida);
         return;
      }

      // a recarregar passa a vez a outro
      if (aRecarregar) LibertarVez();

      // perto o suficiente: para e dispara, se tiver a vez. Senão aponta e espera
      if (!aRecarregar && PedirVez()) ShootPlayer();
      else
      {
         aEsperarVez = !aRecarregar;
         EsperarVez();
      }
   }

   // ---------- Vez de disparar: só "atiradoresAoMesmoTempo" disparam ao mesmo tempo ----------
   private bool PedirVez()
   {
      // limpa quem já não devia estar na lista (morto, destruído, fora do combate)
      for (int i = atiradores.Count - 1; i >= 0; i--)
      {
         Enemy a = atiradores[i];
         if (a == null || a.presentHealth <= 0f || a.estado != EstadoInimigo.Combate || !a.isActiveAndEnabled)
            atiradores.RemoveAt(i);
      }
      if (atiradores.Contains(this)) return true;
      if (atiradores.Count >= Mathf.Max(1, atiradoresAoMesmoTempo)) return false;
      atiradores.Add(this);
      return true;
   }

   private void LibertarVez()
   {
      atiradores.Remove(this);
   }

   // sem a vez: fica parado a apontar-te, pronto a disparar quando um colega parar
   private void EsperarVez()
   {
      Parar();
      Vector3 alvoOlhar = new Vector3(LookPoint.position.x, transform.position.y, LookPoint.position.z);
      VirarPara(alvoOlhar, 360f);
   }

   // ---------- Avisar os colegas ----------
   // grita "Está ali!" com a tua posição (a que ele sabe). Os colegas que ouvem dividem-se:
   // o mais perto vai direito à pista, o seguinte vai cortar-te a fuga, os outros ficam a vigiar
   private void Avisar()
   {
      proximoAviso = Time.time + intervaloAviso;
      if (raioAviso <= 0f) return;
      Ruido.Emitir(percecao.Olhos(), raioAviso, TipoRuido.Aviso, this);
   }

   // ---------- Perseguir: vai para onde achas que foste ----------
   private void EntrarPerseguir(bool comIntuicao)
   {
      if (estado != EstadoInimigo.Perseguir) TocarSomAoCalhas(sonsPerseguir);
      estado = EstadoInimigo.Perseguir;
      inicioPerseguir = Time.time;
      fimIntuicao = comIntuicao ? Time.time + janelaIntuicao : Time.time;
      pontoPrevistoCalculado = false;
      contactoUsado = percecao.MomentoContacto;
      aFlanquear = false;
      fimVigiar = 0f;
   }

   private void AtualizarPerseguir()
   {
      // ouviu-te mesmo ao lado: para um instante e vira-se para o barulho (se estiveres lá, vê-te)
      if (Time.time < fimVirarRuido)
      {
         Parar();
         VirarPara(pontoVirarRuido, 540f);
         return;
      }

      // janela de intuição: ainda sabe para onde foste (viu-te dobrar a esquina)
      if (Time.time < fimIntuicao)
      {
         percecao.RegistarPista(percecao.PosicaoJogador, percecao.VelocidadeJogador());
         contactoUsado = percecao.MomentoContacto;
         IrPara(percecao.PosicaoJogador, enemySpeed * multiplicadorCorrida);
         return;
      }

      // apareceu uma pista nova (um relance, um barulho teu): recalcula o ponto
      if (percecao.MomentoContacto > contactoUsado)
      {
         aFlanquear = false;
         pontoPrevistoCalculado = false;
         contactoUsado = percecao.MomentoContacto;
         inicioPerseguir = Time.time;
      }

      if (!pontoPrevistoCalculado)
      {
         pontoPrevisto = CalcularPontoPrevisto(percecao.UltimaPosicao, percecao.UltimaVelocidade);
         pontoPrevistoCalculado = true;
      }

      IrPara(pontoPrevisto, enemySpeed * multiplicadorCorrida);

      // chegou (ou ao fim de 20 s sem lá conseguir chegar): procura à volta
      if (Chegou(1f) || Time.time - inicioPerseguir > 20f)
         EntrarProcurar(pontoPrevisto, percecao.UltimaVelocidade, tempoProcura);
   }

   // a última posição mais um pouco na direção da fuga, sem atravessar paredes e sempre no NavMesh
   private Vector3 CalcularPontoPrevisto(Vector3 posicao, Vector3 velocidade)
   {
      NavMeshHit noChao;
      if (!NavMesh.SamplePosition(posicao, out noChao, 2f, NavMesh.AllAreas)) return posicao;
      Vector3 inicio = noChao.position;

      velocidade.y = 0f;
      Vector3 avanco = Vector3.ClampMagnitude(velocidade * tempoPrevisao, distanciaMaximaPrevisao);
      if (avanco.sqrMagnitude < 0.01f) return inicio;

      // o NavMesh.Raycast anda pelo chão e para na primeira parede (mas passa pelas portas abertas)
      NavMeshHit parede;
      Vector3 fim = inicio + avanco;
      if (NavMesh.Raycast(inicio, fim, out parede, NavMesh.AllAreas)) return parede.position;

      NavMeshHit noFim;
      if (NavMesh.SamplePosition(fim, out noFim, 2f, NavMesh.AllAreas)) return noFim.position;
      return inicio;
   }

   // ---------- Procurar: visita os esconderijos prováveis ----------
   private void EntrarProcurar(Vector3 centro, Vector3 direcaoFuga, float duracao)
   {
      if (estado != EstadoInimigo.Procurar) TocarSomAoCalhas(sonsProcurar);
      estado = EstadoInimigo.Procurar;
      aFlanquear = false;
      centroProcura = centro;
      fimProcura = Time.time + duracao;
      contactoUsado = percecao.MomentoContacto;
      GerarCandidatos(centro, direcaoFuga, percecao.UltimaPosicao);
      indiceCandidato = 0;
      aParar = false;
      SaltarVistos();
   }

   private void AtualizarProcurar()
   {
      // pista nova (relance, tiro, aviso): volta a perseguir, sem intuição
      if (percecao.MomentoContacto > contactoUsado)
      {
         EntrarPerseguir(false);
         return;
      }

      // sem tempo ou sem sítios para ver: desiste
      if (Time.time > fimProcura || indiceCandidato >= candidatos.Count)
      {
         EntrarAlerta();
         return;
      }

      // os sítios que vê pelo caminho já não precisam de visita
      if (Time.time >= proximaMarcacao)
      {
         proximaMarcacao = Time.time + 0.5f;
         MarcarVistosPeloCaminho();
      }

      Vector3 alvo = candidatos[indiceCandidato];

      if (!aParar)
      {
         // anda (não corre), de arma pronta
         IrPara(alvo, enemySpeed);
         if (Chegou(1f))
         {
            aParar = true;
            fimParagem = Time.time + Random.Range(paragemEmCadaPonto.x, paragemEmCadaPonto.y);
            inicioOlhar = Time.time;
            // olha para fora, no sentido em que veio do centro da procura
            direcaoOlhar = alvo - centroProcura;
            if (direcaoOlhar.sqrMagnitude < 0.25f) direcaoOlhar = transform.forward;
         }
         return;
      }

      // parado no esconderijo: olha à volta
      OlharEmVolta(direcaoOlhar);
      if (Time.time < fimParagem) return;

      MarcarVisto(alvo);
      indiceCandidato++;
      aParar = false;
      SaltarVistos();
   }

   // escolhe os sítios onde te podes ter escondido, à volta do centro
   private void GerarCandidatos(Vector3 centro, Vector3 direcaoFuga, Vector3 ultimaPosicao)
   {
      candidatos.Clear();
      List<Vector3> pontos = new List<Vector3>();
      List<float> valores = new List<float>();

      Vector3 fuga = direcaoFuga;
      fuga.y = 0f;
      bool temFuga = fuga.sqrMagnitude > 0.25f;
      if (temFuga) fuga.Normalize();

      // 16 direções, 2 distâncias dentro do anel
      float perto = Mathf.Lerp(raioProcuraMinimo, raioProcuraMaximo, 0.2f);
      float longe = Mathf.Lerp(raioProcuraMinimo, raioProcuraMaximo, 0.8f);
      for (int i = 0; i < 16; i++)
      {
         Vector3 dir = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
         AvaliarCandidato(centro + dir * perto, 0f, centro, fuga, temFuga, ultimaPosicao, pontos, valores);
         AvaliarCandidato(centro + dir * longe, 0f, centro, fuga, temFuga, ultimaPosicao, pontos, valores);
      }

      // os esconderijos postos à mão (PontoProcura) perto do centro, com pontos a mais
      foreach (PontoProcura pp in PontoProcura.Todos)
      {
         if (pp == null) continue;
         if (Vector3.Distance(pp.transform.position, centro) > raioProcuraMaximo + 5f) continue;
         AvaliarCandidato(pp.transform.position, pp.pontosExtra, centro, fuga, temFuga, ultimaPosicao, pontos, valores);
      }

      // os melhores primeiro, a pelo menos 3 m uns dos outros
      int quantos = Random.Range(pontosAVisitarMinimo, pontosAVisitarMaximo + 1);
      while (candidatos.Count < quantos && pontos.Count > 0)
      {
         int melhor = 0;
         for (int i = 1; i < valores.Count; i++)
            if (valores[i] > valores[melhor]) melhor = i;

         Vector3 p = pontos[melhor];
         pontos.RemoveAt(melhor);
         valores.RemoveAt(melhor);

         bool pertoDeOutro = false;
         foreach (Vector3 c in candidatos)
            if ((c - p).sqrMagnitude < 9f) { pertoDeOutro = true; break; }
         if (!pertoDeOutro) candidatos.Add(p);
      }
   }

   private void AvaliarCandidato(Vector3 p, float extra, Vector3 centro, Vector3 fuga, bool temFuga,
      Vector3 ultimaPosicao, List<Vector3> pontos, List<float> valores)
   {
      NavMeshHit hit;
      if (!NavMesh.SamplePosition(p, out hit, 1.5f, NavMesh.AllAreas)) return;
      // noutro piso não (as escadas já são tratadas pela perseguição)
      if (Mathf.Abs(hit.position.y - centro.y) > 2f) return;

      // tem de haver caminho, e não muito comprido
      if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, caminhoTeste)) return;
      if (caminhoTeste.status != NavMeshPathStatus.PathComplete) return;
      float comprimento = 0f;
      Vector3[] cantos = caminhoTeste.corners;
      for (int i = 1; i < cantos.Length; i++) comprimento += Vector3.Distance(cantos[i - 1], cantos[i]);
      if (comprimento > caminhoMaximoProcura) return;

      float valor = extra;
      // na direção para onde fugias
      Vector3 desdeCentro = hit.position - centro;
      desdeCentro.y = 0f;
      if (temFuga && desdeCentro.sqrMagnitude > 0.01f) valor += 2f * Vector3.Dot(fuga, desdeCentro.normalized);
      // escondido de onde te viu pela última vez: é aí que te podes ter escondido
      if (!percecao.LinhaLivre(ultimaPosicao + Vector3.up * 1.2f, hit.position + Vector3.up * 1.2f)) valor += 1.5f;
      // já visto há pouco
      if (VistoRecentemente(hit.position, 3f)) valor -= 3f;
      // longe
      valor -= comprimento / Mathf.Max(caminhoMaximoProcura, 1f);

      pontos.Add(hit.position);
      valores.Add(valor);
   }

   // os esconderijos que tem à vista e perto deixam de precisar de visita
   private void MarcarVistosPeloCaminho()
   {
      Vector3 olhos = percecao.Olhos();
      for (int i = indiceCandidato + 1; i < candidatos.Count; i++)
      {
         Vector3 c = candidatos[i];
         Vector3 para = c - transform.position;
         if (para.sqrMagnitude > 64f) continue; // mais de 8 m
         para.y = 0f;
         if (para.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, para) > percecao.anguloPeriferico * 0.5f) continue;
         if (percecao.LinhaLivre(olhos, c + Vector3.up)) MarcarVisto(c);
      }
   }

   private void MarcarVisto(Vector3 p)
   {
      pontosVistos.Add(p);
      momentosVistos.Add(Time.time);
   }

   // passa à frente os esconderijos que já viu
   private void SaltarVistos()
   {
      while (indiceCandidato < candidatos.Count && VistoRecentemente(candidatos[indiceCandidato], 1.5f))
         indiceCandidato++;
   }

   private bool VistoRecentemente(Vector3 p, float raio)
   {
      // esquece o que viu há mais de 20 s
      for (int i = momentosVistos.Count - 1; i >= 0; i--)
      {
         if (Time.time - momentosVistos[i] > 20f)
         {
            pontosVistos.RemoveAt(i);
            momentosVistos.RemoveAt(i);
         }
      }
      foreach (Vector3 v in pontosVistos)
         if ((v - p).sqrMagnitude < raio * raio) return true;
      return false;
   }

   // ---------- Alerta: perdeu-te o rasto ----------
   private void EntrarAlerta()
   {
      TocarSomAoCalhas(sonsPerdeu);
      estado = EstadoInimigo.Alerta;
      fimAlerta = Time.time + tempoAlerta;
   }

   // ======================================================================
   //  MOVIMENTO E OLHAR
   // ======================================================================

   // manda o NavMeshAgent para um ponto. Só recalcula o caminho se o destino mudar mais de meio metro,
   // ou uma vez por segundo (por segurança, se algo o tiver desviado)
   private void IrPara(Vector3 destino, float velocidade)
   {
      enemyAgent.speed = velocidade;
      if ((destino - destinoAtual).sqrMagnitude > 0.25f || Time.time >= proximoCaminho)
      {
         enemyAgent.SetDestination(destino);
         destinoAtual = destino;
         proximoCaminho = Time.time + 1f;
      }
   }

   private void Parar()
   {
      IrPara(transform.position, enemySpeed);
   }

   // já chegou ao destino (ou está o mais perto que consegue)
   private bool Chegou(float distancia)
   {
      return !enemyAgent.pathPending && enemyAgent.remainingDistance <= distancia;
   }

   // roda só no eixo Y até ficar virado para o ponto
   private void VirarPara(Vector3 ponto, float grausPorSegundo)
   {
      Vector3 dir = ponto - transform.position;
      dir.y = 0f;
      if (dir.sqrMagnitude < 0.01f) return;
      transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), grausPorSegundo * Time.deltaTime);
   }

   // parado, varre com o olhar para a esquerda e para a direita de uma direção
   private void OlharEmVolta(Vector3 direcaoBase)
   {
      direcaoBase.y = 0f;
      if (direcaoBase.sqrMagnitude < 0.01f) direcaoBase = transform.forward;
      float angulo = Mathf.Sin((Time.time - inicioOlhar) * 1.5f) * anguloVarrimento;
      Quaternion alvo = Quaternion.LookRotation(direcaoBase) * Quaternion.Euler(0f, angulo, 0f);
      transform.rotation = Quaternion.RotateTowards(transform.rotation, alvo, 180f * Time.deltaTime);
   }

   private float DistanciaJogador()
   {
      return Vector3.Distance(transform.position, playerBody.position);
   }

   private void TocarPasso()
   {
      if (fontePassos == null || sonsPassos == null || sonsPassos.Length == 0) return;

      AudioClip som = sonsPassos[Random.Range(0, sonsPassos.Length)];
      if (som == null) return;

      // tom e volume a variar um pouco, para os passos não soarem todos iguais
      fontePassos.pitch = Random.Range(0.9f, 1.1f);
      fontePassos.PlayOneShot(som, volumePassos * Random.Range(0.85f, 1f));
   }

   // corre depois das animações e vira o soldado para a arma apontar ao jogador
   // (a animação de tiro do Mixamo aponta a arma para o lado do corpo)
   // parado: roda o corpo todo, como um atirador de lado
   // a andar: roda só o tronco, para as pernas continuarem a andar para a frente
   private void LateUpdate()
   {
      // morto: fica como estava e deixa a animação de morte tocar
      if (presentHealth <= 0) return;
      if (ossoTronco == null || maoDireita == null || maoEsquerda == null) return;

      // a recarregar as mãos andam no carregador: não corrige a mira
      bool aMirar = playerInVisionRadius && !aRecarregar && TemMunicao();
      bool parado = enemyAgent.velocity.magnitude < 0.3f;

      // 1) corpo todo: roda aos poucos o modelo enquanto dispara parado; senão volta ao normal
      float alvoCorpo = 0f;
      if (aMirar && parado) alvoCorpo = rodaCorpo + AnguloParaOJogador();
      rodaCorpo = Mathf.MoveTowardsAngle(rodaCorpo, alvoCorpo, 360f * Time.deltaTime);
      anim.transform.localRotation = rotacaoModeloInicial * Quaternion.Euler(0f, rodaCorpo, 0f);

      // 2) tronco: acerta o que ainda faltar (a andar, ou enquanto o corpo está a rodar)
      if (aMirar)
      {
         float resto = AnguloParaOJogador();
         ossoTronco.rotation = Quaternion.AngleAxis(resto, Vector3.up) * ossoTronco.rotation;
      }
   }

   // quantos graus falta rodar (na horizontal) para a arma ficar virada para o jogador
   private float AnguloParaOJogador()
   {
      // direção da arma: da mão direita (punho) para a esquerda (guarda-mão)
      Vector3 dirArma = maoEsquerda.position - maoDireita.position;
      dirArma.y = 0f;

      // direção do jogador
      Vector3 dirAlvo = LookPoint.position - maoDireita.position;
      dirAlvo.y = 0f;

      if (dirArma.sqrMagnitude < 0.0001f || dirAlvo.sqrMagnitude < 0.0001f) return 0f;

      return Vector3.SignedAngle(dirArma, dirAlvo, Vector3.up) - ajusteMira;
   }

   // patrulha os Walk Points por ordem ou, sem Walk Points, volta ao posto e à rotação inicial
   private void Guard(float multiplicadorVelocidade = 1f)
   {
      float velocidade = enemySpeed * multiplicadorVelocidade;

      // inimigo fixo: não tem Walk Points, fica de guarda no sítio onde foi posto
      if (walkPoints.Length == 0)
      {
         // manda-o para a posição guardada no Awake()
         // se já lá estiver, fica parado; se te perseguiu, volta
         IrPara(posicaoInicial, velocidade);

         // inimigo fixo: chegou ao posto
         if (Chegou(0.6f))
         {
            // roda aos poucos (180 graus por segundo) até ficar virado como no início
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacaoInicial, 180f * Time.deltaTime);
         }

         // sai do Guard() para não correr o código da patrulha
         return;
      }

      Vector3 alvo = walkPoints[currentEnemyPosition].transform.position;
      Vector3 alvoMesmaAltura = new Vector3(alvo.x, transform.position.y, alvo.z);

      // chegou ao ponto atual: passa ao seguinte, por ordem
      if (Vector3.Distance(transform.position, alvoMesmaAltura) < 0.6f)
      {
         currentEnemyPosition = (currentEnemyPosition + 1) % walkPoints.Length;
         alvo = walkPoints[currentEnemyPosition].transform.position;
      }

      IrPara(alvo, velocidade);
   }

   // quadro elétrico sabotado: decide pela posição do SOLDADO (não pela do jogador).
   // A PercecaoInimigo lê o noEscuro e a lanterna para encurtar a visão
   private void AtualizarEscuro()
   {
      // mede à altura do peito, para não falhar quando os pés estão mesmo no fundo da caixa
      noEscuro = ZonaSemEnergia.NoEscuro(transform.position + Vector3.up);
      if (lanterna != null && lanterna.enabled != noEscuro) lanterna.enabled = noEscuro;
   }

   // ======================================================================
   //  CHAMADAS DE OUTROS SCRIPTS
   // ======================================================================

   // chamada pela ZonaAlerta quando entras ou sais da zona: lá dentro o medidor sobe mais depressa
   public void BonusZona(float valor)
   {
      bonusZona = valor;
   }

   // chamada pelo Lider, pela Guarita, pelo AtivarInimigos e pela DetecaoZona quando mandam soldados atrás de ti.
   // Antes dava metros de visão para virem direitos a ti. Agora ficam a saber onde estavas NESTE momento
   // e vêm a correr para lá: chegam onde estavas, não onde estás
   public void BonusReforco(float valor)
   {
      if (valor <= 0f || presentHealth <= 0) return;
      percecao.RegistarPista(percecao.PosicaoJogador, percecao.VelocidadeJogador());
      if (estado != EstadoInimigo.Combate) EntrarPerseguir(false);
   }

   // chamada pela PortaExplosivos quando a porta principal rebenta (e, mais tarde, pelos ruídos)
   // ponto: onde ir ver; segundos: quanto tempo fica à procura; bonusVisao: já não é usado (fica para a porta compilar)
   public void Investigar(Vector3 ponto, float segundos, float bonusVisao)
   {
      Investigar(ponto, segundos, bonusVisao, ponto);
   }

   // fonte: de onde veio o barulho (para onde olha). Pode ser diferente do ponto aonde vai
   public void Investigar(Vector3 ponto, float segundos, float bonusVisao, Vector3 fonte)
   {
      if (presentHealth <= 0) return;
      // um barulho não o tira do combate nem de uma perseguição
      if (estado == EstadoInimigo.Combate || estado == EstadoInimigo.Perseguir) return;
      // vale como uma explosão
      IrVerBarulho(ponto, fonte, segundos, (int)TipoRuido.Explosao);
   }

   // ======================================================================
   //  OUVIDO (Ruido.cs)
   // ======================================================================

   public Vector3 Ouvidos => percecao != null ? percecao.Olhos() : transform.position + Vector3.up * 1.6f;
   public Transform Raiz => transform;

   // chamada pelo Ruido.Emitir quando este soldado ouve um barulho.
   // ponto: onde acha que foi (com erro, menos no aviso). Devolve true se reagiu
   public bool Ouvir(EventoRuido ruido, Vector3 ponto, float distancia)
   {
      if (presentHealth <= 0f || percecao == null) return false;
      // a ver-te e a disparar: os barulhos já não interessam
      if (estado == EstadoInimigo.Combate) return false;

      switch (ruido.tipo)
      {
         case TipoRuido.Aviso:
            return OuvirAviso(ruido);

         // barulhos que tu fazes: dizem onde estás (com erro)
         case TipoRuido.Tiro:
         case TipoRuido.PassosCorrer:
         case TipoRuido.PassosAndar:
         case TipoRuido.Aterrar:
            if (estado == EstadoInimigo.Perseguir || estado == EstadoInimigo.Procurar)
            {
               // noutro piso sem caminho até lá: não troca a pista que tem por uma onde não consegue chegar
               if (!ConsegueChegar(ponto)) return false;
               // já anda atrás de ti: é uma pista nova. O Perseguir recalcula o ponto, o Procurar volta a Perseguir
               percecao.RegistarPista(ponto, Vector3.zero);
               // mesmo ao lado (um tiro a 2 m): para e vira-se para lá, em vez de continuar de costas para ti.
               // No máximo uma vez a cada 2 s, para uma rajada atrás de uma parede não o deixar pregado
               if (distancia <= distanciaVirarRuido && Time.time >= fimVirarRuido + 1.5f)
               {
                  pontoVirarRuido = ponto;
                  fimVirarRuido = Time.time + 0.5f;
               }
               return true;
            }
            return IrVerBarulho(ponto, ponto, ruido.tipo == TipoRuido.Tiro ? tempoProcura : tempoProcuraCurta, (int)ruido.tipo);

         // barulhos que não são tu (explosão, colega a morrer, objeto atirado, rádio): vai ver, mas não é uma pista tua
         default:
            if (estado == EstadoInimigo.Perseguir) return false;   // não larga a perseguição por isto
            float segundos = ruido.tipo == TipoRuido.Objeto || ruido.tipo == TipoRuido.Radio ? tempoProcuraCurta : tempoProcura;
            return IrVerBarulho(ponto, ponto, segundos, (int)ruido.tipo);
      }
   }

   // vai ver um barulho (Desconfiado), a não ser que já esteja a ver um mais forte
   private bool IrVerBarulho(Vector3 ponto, Vector3 fonte, float segundos, int prioridade)
   {
      if (estado == EstadoInimigo.Desconfiado && Time.time < fimInvestigar)
      {
         // já está a ver uma coisa mais importante
         if (prioridade < prioridadeInvestigar) return false;
         // o mesmo sítio (uma rajada, passos seguidos): só acerta o ponto, sem voltar a parar e a virar-se
         if ((ponto - pontoInvestigar).sqrMagnitude < 16f)
         {
            pontoInvestigar = ponto;
            fonteInvestigar = fonte;
            prioridadeInvestigar = Mathf.Max(prioridadeInvestigar, prioridade);
            fimInvestigar = Mathf.Max(fimInvestigar, Time.time + segundos);
            if (chegouInvestigar && (transform.position - ponto).sqrMagnitude > 4f) chegouInvestigar = false;
            return true;
         }
      }

      // não consegue lá chegar a pé (noutro piso, sem escada ligada no NavMesh): não vai pôr-se por cima
      // ou por baixo do sítio. Fica onde está, virado para lá e mais atento. Se andava a ver outra coisa, continua
      if (!ConsegueChegar(ponto))
      {
         if (estado != EstadoInimigo.Calmo && estado != EstadoInimigo.Alerta) return false;
         Vigiar(ponto, tempoVigiar * 0.5f);
         return true;
      }

      EntrarDesconfiado(ponto, fonte, segundos, prioridade);
      return true;
   }

   // há caminho a pé até lá? Um caminho parcial no mesmo piso serve: vai até onde der (por exemplo,
   // até à porta trancada, do lado de dentro). Um caminho que acaba no piso de cima ou de baixo não serve
   private bool ConsegueChegar(Vector3 ponto)
   {
      if (enemyAgent == null || !enemyAgent.isOnNavMesh) return false;
      NavMeshHit noChao;
      // um metro abaixo do barulho, para apanhar o chão desse piso e não o de cima
      if (!NavMesh.SamplePosition(ponto + Vector3.down, out noChao, 3f, NavMesh.AllAreas)) return false;
      if (!NavMesh.CalculatePath(enemyAgent.nextPosition, noChao.position, NavMesh.AllAreas, caminhoTeste)) return false;
      if (caminhoTeste.status == NavMeshPathStatus.PathComplete) return true;
      Vector3[] cantos = caminhoTeste.corners;
      if (cantos.Length == 0) return false;
      Vector3 fim = cantos[cantos.Length - 1];
      return Mathf.Abs(noChao.position.y - fim.y) <= 1.5f;
   }

   // fica parado, virado para um ponto, mais atento (o AtualizarCalmo trata do resto)
   private void Vigiar(Vector3 ponto, float segundos)
   {
      if (estado == EstadoInimigo.Desconfiado || estado == EstadoInimigo.Calmo) estado = EstadoInimigo.Alerta;
      fimAlerta = Mathf.Max(fimAlerta, Time.time + tempoAlerta);
      pontoVigiar = ponto;
      fimVigiar = Time.time + segundos;
   }

   // um colega viu-te e gritou. Recebe a posição que ele sabe, sem erro
   private bool OuvirAviso(EventoRuido ruido)
   {
      Enemy quem = ruido.origem as Enemy;
      if (quem == null || quem == this || quem.Percecao == null || quem.Percecao.MomentoContacto < 0f) return false;
      Vector3 pista = quem.Percecao.UltimaPosicao;

      // noutro piso sem caminho até lá: não ia conseguir chegar, por isso não fica com o lugar de caçador
      // nem de flanco (devolve false: esses lugares passam para os seguintes). Fica a vigiar, se estava sossegado
      if (!ConsegueChegar(pista))
      {
         if (estado == EstadoInimigo.Calmo || estado == EstadoInimigo.Alerta || estado == EstadoInimigo.Desconfiado)
            Vigiar(pista, tempoVigiar);
         return false;
      }

      percecao.RegistarPista(pista, quem.Percecao.UltimaVelocidade);

      // já anda atrás de ti: segue a pista nova (quem estava a flanquear continua a flanquear)
      if (estado == EstadoInimigo.Perseguir || estado == EstadoInimigo.Procurar)
      {
         if (estado == EstadoInimigo.Perseguir && aFlanquear)
         {
            pontoPrevisto = PontoFlanco(pista, quem.transform.position);
            contactoUsado = percecao.MomentoContacto;
            inicioPerseguir = Time.time;
         }
         return true;
      }

      // os que ouviram dividem-se, do mais perto para o mais longe
      int ordem = ruido.reagiram;
      if (ordem == 0)
      {
         // caçador: vai direito à pista
         EntrarPerseguir(false);
      }
      else if (ordem == 1)
      {
         // flanco: vai para lá de onde estás, para te cortar a fuga
         EntrarPerseguir(false);
         pontoPrevisto = PontoFlanco(pista, quem.transform.position);
         pontoPrevistoCalculado = true;
         aFlanquear = true;
      }
      else
      {
         // guarda: fica onde está, virado para a pista, mais atento
         Vigiar(pista, tempoVigiar);
      }
      return true;
   }

   // um ponto para lá da pista, visto de quem te viu (a outra porta, o fundo do corredor).
   // Se houver uma parede logo atrás de ti, tenta para um dos lados
   private Vector3 PontoFlanco(Vector3 pista, Vector3 quemViu)
   {
      NavMeshHit noChao;
      if (!NavMesh.SamplePosition(pista, out noChao, 2f, NavMesh.AllAreas)) return pista;
      Vector3 inicio = noChao.position;

      Vector3 frente = inicio - quemViu;
      frente.y = 0f;
      if (frente.sqrMagnitude < 0.01f) frente = inicio - transform.position;
      frente.y = 0f;
      if (frente.sqrMagnitude < 0.01f) return inicio;
      frente.Normalize();
      Vector3 lado = new Vector3(-frente.z, 0f, frente.x);
      // o lado mais perto deste soldado primeiro, para chegar por outro sítio que não o do colega
      if (Vector3.Dot(lado, transform.position - inicio) < 0f) lado = -lado;

      Vector3[] direcoes = { frente, lado, -lado };
      Vector3 melhor = inicio;
      float melhorDist = 0f;
      foreach (Vector3 dir in direcoes)
      {
         Vector3 fim = inicio + dir * distanciaFlanco;
         NavMeshHit parede;
         Vector3 ponto = NavMesh.Raycast(inicio, fim, out parede, NavMesh.AllAreas) ? parede.position : fim;
         float d = (ponto - inicio).sqrMagnitude;
         // basta afastar-se 3 m da pista para valer a pena
         if (d >= 9f) return ponto;
         if (d > melhorDist) { melhorDist = d; melhor = ponto; }
      }
      return melhor;
   }

   // texto do estado para o ModoDebug, com o medidor
   public string TextoEstado()
   {
      if (presentHealth <= 0f) return "<color=grey>morto</color>";
      string m = " " + Mathf.RoundToInt(percecao != null ? percecao.Medidor * 100f : 0f) + "%";
      switch (estado)
      {
         case EstadoInimigo.Combate:
            // em combate mas sem a vez de disparar (já há outros a disparar)
            if (aEsperarVez) return "<color=#ff9090>à espera" + m + "</color>";
            return "<color=red>combate" + m + "</color>";
         case EstadoInimigo.Perseguir:   return "<color=orange>perseguir" + m + "</color>";
         case EstadoInimigo.Procurar:    return "<color=orange>procurar" + m + "</color>";
         case EstadoInimigo.Desconfiado: return "<color=yellow>desconfiado" + m + "</color>";
         case EstadoInimigo.Alerta:      return "<color=yellow>alerta" + m + "</color>";
         default:                        return "<color=lime>calmo" + m + "</color>";
      }
   }

   // ======================================================================
   //  DISPARAR
   // ======================================================================

   private void Disparar()
   {
      if (previouslyShoot) return;
      // só com linha de vista: não dispara contra paredes nem lajes
      if (!percecao.VeAgora) return;
      // acabou de te detetar: ainda está a reagir
      if (Time.time < podeDispararDesde) return;
      // a recarregar não dispara
      if (aRecarregar) return;
      // gastou os carregadores todos: já não dispara
      if (balasRestantes <= 0) return;
      // acabou de levar um tiro: ainda está atordoado e não dispara
      if (Time.time < atordoadoAte || AReagirAoTiro()) return;

      Vector3 origem = ShootingRaycastArea.transform.position;
      Vector3 direcao = (LookPoint.position - origem).normalized;
      float distancia = Vector3.Distance(origem, LookPoint.position);

      // pontaria imperfeita: desvia a direção um ângulo ao calhas (para os lados e para cima/baixo).
      // a linha do tiro usa esta mesma direção, por isso mostra o tiro desviado
      float desvio = desvioMira;
      if (enemyAgent.velocity.magnitude > 0.3f) desvio += desvioExtraAAndar;
      // ao longe falha mais, e o primeiro tiro depois de te ver também
      desvio *= 1f + desvioExtraAoLonge * Mathf.Clamp01(distancia / Mathf.Max(alcanceDisparo, 0.01f));
      if (primeiroTiro) desvio *= desvioPrimeiroTiro;
      primeiroTiro = false;
      Vector2 aoCalhas = Random.insideUnitCircle * desvio;
      direcao = Quaternion.LookRotation(direcao) * Quaternion.Euler(aoCalhas.y, aoCalhas.x, 0f) * Vector3.forward;

      // alcance do raio: um pouco mais do que o de disparo
      float alcanceRaio = alcanceDisparo + 10f;
      // se não acertar em nada, a linha vai até ao alcance máximo
      Vector3 fim = origem + direcao * alcanceRaio;

      RaycastHit hit;
      if (Physics.Raycast(origem, direcao, out hit, alcanceRaio, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
      {
         // Debug.Log("Shooting " + hit.transform.name);
         fim = hit.point;
         PlayerStats alvo = hit.transform.GetComponent<PlayerStats>();
         if (alvo != null) alvo.TakeDamage(giveDamage);

         // buraco de bala no cenário quando falha o jogador
         MarcasImpacto.Bala(hit);
      }
      // mostra a linha da ponta da arma até onde o tiro acabou (só com o debug visual, F3)
      if (linhaTiro != null)
      {
         linhaTiro.enabled = ModoDebugVisual.Tiros;
         linhaTiro.SetPosition(0, origem);
         linhaTiro.SetPosition(1, fim);
         Invoke(nameof(EsconderLinha), duracaoLinha);
      }

      // clarão na ponta da arma
      if (muzzleSpark != null) muzzleSpark.Play();
      if (luzDisparo != null)
      {
         luzDisparo.enabled = true;
         Invoke(nameof(ApagarLuz), duracaoLuz);
      }

      // som do tiro, com um tom ligeiramente diferente em cada disparo (para não soar sempre igual)
      if (audioSource != null && shootingSound != null)
      {
         audioSource.pitch = Random.Range(0.92f, 1.08f);
         audioSource.PlayOneShot(shootingSound);
      }

      previouslyShoot = true;
      Invoke(nameof(ActiveShooting), timeBetweenShoot);

      // gastou uma bala: com o carregador vazio, recarrega
      balasRestantes--;
      // só recarrega se ainda tiver carregadores de reserva
      if (balasRestantes <= 0 && carregadoresRestantes > 0) Recarregar();
   }

   private void Recarregar()
   {
      aRecarregar = true;
      carregadoresRestantes--;
      // animação Reload na layer Tronco (só os braços, as pernas continuam)
      if (anim != null) anim.SetTrigger("Reload");
      TocarSomAoCalhas(vozesRecarregar);
      if (audioSource != null && somRecarregar != null) audioSource.PlayOneShot(somRecarregar);
      Invoke(nameof(AcabarRecarregar), tempoRecarregar);
   }

   // ainda tem balas na arma, ou está a meio de pôr um carregador novo
   private bool TemMunicao()
   {
      return balasRestantes > 0 || aRecarregar;
   }

   private void AcabarRecarregar()
   {
      balasRestantes = balasCarregador;
      aRecarregar = false;
   }

   // true enquanto a animação Hit está a tocar (ou a começar) na layer Reacao.
   // assim o tempo sem disparar acompanha sozinho o Speed e o Exit Time do Animator
   private bool AReagirAoTiro()
   {
      if (anim == null || layerReacao < 0) return false;
      if (anim.GetCurrentAnimatorStateInfo(layerReacao).IsName("Hit")) return true;
      return anim.IsInTransition(layerReacao) && anim.GetNextAnimatorStateInfo(layerReacao).IsName("Hit");
   }

   private void EsconderLinha()
   {
      linhaTiro.enabled = false;
   }

   private void ApagarLuz()
   {
      luzDisparo.enabled = false;
   }

   // para e dispara, virado para o jogador
   private void ShootPlayer()
   {
      Parar();

      // roda só no eixo Y, para o soldado não se inclinar para a frente ou para trás
      Vector3 alvoOlhar = new Vector3(LookPoint.position.x, transform.position.y, LookPoint.position.z);
      transform.LookAt(alvoOlhar);

      Disparar();
   }

   private void ActiveShooting()
   {
      previouslyShoot = false;
   }

   // naCabeca: true quando o tiro foi na cabeça (o Weapon do jogador é que avisa)
   public void enemyHitDamage(float takeDamage, bool naCabeca = false)
   {
      // já morreu: os tiros seguintes não fazem nada (senão recomeçava a morrer)
      if (presentHealth <= 0) return;

      presentHealth -= takeDamage;
      // healthBar.SetHealth(presentHealth);

      if (presentHealth <= 0)
      {
         enemyDie(naCabeca);
      }
      else
      {
         // ainda está vivo: som de dor
         TocarSomAoCalhas(sonsDor);
         // e encolhe-se com o tiro (animação Hit na layer Reacao do Animator)
         if (anim != null) anim.SetTrigger("Hit");
         // fica uns instantes sem conseguir disparar
         atordoadoAte = Time.time + tempoAtordoado;

         // sabe de onde veio o tiro: fica a saber onde estás e o medidor sobe muito.
         // Se não te estava a ver, vai atrás de ti (com a janela de intuição)
         percecao.RegistarPista(percecao.PosicaoJogador, percecao.VelocidadeJogador());
         percecao.SomarMedidor(0.5f);
         if (estado != EstadoInimigo.Combate) EntrarPerseguir(true);
         // "Fui atingido!": os colegas perto vêm ajudar (no máximo um aviso a cada intervaloAviso)
         if (Time.time >= proximoAviso) Avisar();
      }
   }

   // sangue no ponto onde a bala acertou, virado para fora do corpo (chamado pelo Weapon do jogador)
   public void MostrarSangue(Vector3 ponto, Vector3 normal)
   {
      if (efeitoSangue == null) return;
      ParticleSystem sangue = Instantiate(efeitoSangue, ponto, Quaternion.LookRotation(normal));
      // apaga o efeito só quando a última gota acabar (Duration + Start Lifetime máximo do Particle System)
      Destroy(sangue.gameObject, sangue.main.duration + sangue.main.startLifetime.constantMax);
   }

   // toca um dos sons da lista, escolhido ao calhas (lista vazia = não faz nada)
   private void TocarSomAoCalhas(AudioClip[] sons)
   {
      if (audioSource == null || sons == null || sons.Length == 0) return;

      AudioClip som = sons[Random.Range(0, sons.Length)];
      if (som == null) return;

      // o tom pode ter ficado alterado pelo último tiro
      audioSource.pitch = Random.Range(0.95f, 1.05f);
      audioSource.PlayOneShot(som);
   }

   private void enemyDie(bool naCabeca = false)
   {
      // desliga o NavMesh Agent, senão ele mantém o soldado de pé
      enemyAgent.enabled = false;

      if (anim != null)
      {
         // com modelo 3D: para de disparar e toca a animação de morte
         anim.SetBool("Shoot", false);
         anim.SetFloat("Speed", 0f);
         // morte por headshot tem uma animação própria (DeathHead)
         anim.SetTrigger(naCabeca ? "DieHead" : "Die");
      }
      else
      {
         // sem modelo (cápsula)
         // tomba o soldado
         StartCoroutine(Cair());
      }

      // som de morte
      TocarSomAoCalhas(sonsMorte);

      // deixa de ouvir e de ocupar a vez de disparar; os colegas perto ouvem-no cair e vêm ver
      Ruido.Remover(this);
      LibertarVez();
      if (raioGrito > 0f) Ruido.Emitir(transform.position + Vector3.up, raioGrito, TipoRuido.Grito, this);

      enemySpeed = 0f;
      shootingRadius = 0f;
      visionRadius = 0f;
      playerInVisionRadius = false;
      playerInShootingRadius =  false;
      aInvestigar = false;

      Object.Destroy(gameObject, 5.0f);
   }

   // tomba o soldado para trás e assenta-o no chão
   private IEnumerator Cair()
   {
      Quaternion rotInicio = transform.rotation;
      Quaternion rotFim = rotInicio * Quaternion.Euler(-90f, 0f, 0f); // deitado de costas
      Vector3 posInicio = transform.position;
      Vector3 posFim = posInicio + Vector3.down * 0.5f; // desce meia cápsula

      float tempo = 0f;
      while (tempo < duracaoQueda)
      {
         tempo += Time.deltaTime;
         float t = tempo / duracaoQueda;
         t = t * t; // começa devagar e acelera, como uma queda
         transform.rotation = Quaternion.Slerp(rotInicio, rotFim, t);
         transform.position = Vector3.Lerp(posInicio, posFim, t);
         yield return null; // espera pelo frame seguinte
      }
   }

   // ======================================================================
   //  DEBUG
   // ======================================================================

   // com o F3 (ModoDebugVisual) e os Gizmos ligados: a última posição conhecida (vermelho),
   // o ponto previsto (laranja), o ponto a investigar (amarelo) e os esconderijos (cinzento; verde os já vistos)
   private void OnDrawGizmos()
   {
      if (!Application.isPlaying || !ModoDebugVisual.Ativo || presentHealth <= 0 || percecao == null) return;
      if (estado == EstadoInimigo.Calmo && Time.time >= fimVigiar) return;

      if (percecao.MomentoContacto >= 0f)
      {
         Gizmos.color = Color.red;
         Gizmos.DrawWireSphere(percecao.UltimaPosicao, 0.5f);
      }
      if (estado == EstadoInimigo.Perseguir && pontoPrevistoCalculado)
      {
         Gizmos.color = new Color(1f, 0.5f, 0f);
         Gizmos.DrawWireSphere(pontoPrevisto, 0.6f);
         Gizmos.DrawLine(transform.position, pontoPrevisto);
      }
      if (Time.time < fimVigiar)
      {
         Gizmos.color = Color.cyan;
         Gizmos.DrawLine(transform.position + Vector3.up, pontoVigiar);
      }
      if (estado == EstadoInimigo.Desconfiado)
      {
         Gizmos.color = Color.yellow;
         Gizmos.DrawWireSphere(pontoInvestigar, 0.5f);
         Gizmos.DrawLine(transform.position, pontoInvestigar);
      }
      if (estado == EstadoInimigo.Procurar)
      {
         for (int i = 0; i < candidatos.Count; i++)
         {
            bool visto = i < indiceCandidato || VistoRecentementeSemLimpar(candidatos[i]);
            Gizmos.color = visto ? Color.green : Color.grey;
            Gizmos.DrawWireCube(candidatos[i] + Vector3.up * 0.25f, Vector3.one * 0.5f);
         }
         if (indiceCandidato < candidatos.Count)
         {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, candidatos[indiceCandidato]);
         }
      }
   }

   // igual ao VistoRecentemente, mas sem apagar nada (os Gizmos não devem mexer no estado)
   private bool VistoRecentementeSemLimpar(Vector3 p)
   {
      for (int i = 0; i < pontosVistos.Count; i++)
         if (Time.time - momentosVistos[i] <= 20f && (pontosVistos[i] - p).sqrMagnitude < 2.25f) return true;
      return false;
   }

   // desenha o alcance de disparo na Scene quando o inimigo está selecionado (o cone é da PercecaoInimigo)
   private void OnDrawGizmosSelected()
   {
      Gizmos.color = Color.red;
      Gizmos.DrawWireSphere(transform.position, alcanceDisparo);

      // visão no escuro (quadro elétrico sabotado) a azul
      Gizmos.color = Color.blue;
      Gizmos.DrawWireSphere(transform.position, raioVisaoEscuro);
   }
}
