using System.Collections.Generic;
using UnityEngine;

// Sniper numa torre de vigia da trincheira.
// Varre o terreno com o holofote e vê o jogador se ele estiver dentro do cone e nada o tapar.
// Aponta ao TOPO da cápsula do jogador: de pé (2 m) passa por cima da parede da trincheira,
// abaixado (1,2 m) a parede tapa. Por isso o abaixar funciona sem ler o PlayerMovement.
// Enquanto te vê, roda para ti e aponta-te o laser vermelho.
// A barra de deteção (DetecaoZona) lê o "aVer". Quando a barra enche, a zona liga o alarme (Alarme(true))
// e o sniper passa a disparar enquanto te vê: só acerta com a linha livre, por isso abaixado estás protegido.
// Pode ser morto: o Weapon do jogador chama Sniper.Atingido(collider) e LevarTiro(...).
// Os colliders do tiro estão no Corpo (o Sniper_Soldado), que tem de estar preenchido.
// Vai no objeto que roda (o "Pivo"), à altura dos olhos do sniper. O holofote é filho dele.
public class Sniper : MonoBehaviour
{
   [Header("Torre")]
   // objeto raiz da torre: as peças dela (e o soldado) não tapam a visão. Vazio = o pai do Pivo
   public Transform torre;
   // modelo do soldado: roda só para os lados, a acompanhar o Pivo (opcional)
   public Transform corpo;
   // ponta da arma, de onde sai o laser. Vazio = o próprio Pivo
   public Transform pontaArma;

   [Header("Rotação")]
   // graus para cada lado a partir da posição em que o Pivo foi posto (30 = varre 60 graus)
   public float anguloVarrimento = 30f;
   // graus por segundo
   public float velocidadeRotacao = 10f;
   // segundos parado em cada ponta antes de voltar
   public float pausaNasPontas = 1f;
   // graus para baixo (a torre é alta e olha para a trincheira ao longe)
   public float inclinacao = 8f;
   // ligado = começa num ângulo e num sentido ao calhas (as torres não rodam todas iguais)
   public bool inicioAleatorio = true;

   [Header("Visão")]
   // metros até onde o sniper vê
   public float alcance = 120f;
   // abertura do cone de visão, em graus (de um lado ao outro)
   public float anguloVisao = 20f;
   // metros abaixo do topo da cápsula a que o sniper aponta (para o raio bater na cápsula e não passar por cima)
   public float margemTopo = 0.1f;
   // segundos a olhar para o último sítio onde te viu, antes de voltar a varrer
   public float tempoEsquecer = 2f;
   // camadas que tapam a visão (paredes, terreno, árvores) e a camada do Player
   public LayerMask camadasVisao = ~0;

   [Header("Holofote (Spot Light filha do Pivo)")]
   public Light holofote;
   // o ângulo e o alcance da luz seguem os da visão
   public bool luzIgualVisao = true;

   [Header("Laser")]
   // material do laser (HDRP Unlit vermelho). Vazio = sem laser
   public Material materialLaser;
   public float larguraLaser = 0.06f;
   // ligado = o laser está sempre ligado (aponta para onde o sniper olha); desligado = só quando te vê
   public bool laserSempre = false;

   [Header("Sons (opcionais)")]
   public AudioSource audioSource;
   // quando te apanha (Mira_Ativar)
   public AudioClip somApanhou;
   // quando te perde (Mira_Desativar)
   public AudioClip somPerdeu;

   [Header("Tiros (só durante o alarme)")]
   // vida que tira em cada tiro (a Blindagem do fato reduz)
   public float danoTiro = 25f;
   // segundos entre tiros
   public float intervaloTiro = 2.5f;
   // segundos a apontar antes do primeiro tiro, sempre que te apanha (dá tempo para te abaixares)
   public float tempoMira = 1f;
   // som do tiro (Tiro_M4, como os soldados)
   public AudioClip somTiro;
   // clarão e luz da M4 (o MuzzleFlash e a LuzDisparo que vieram com a arma copiada de um soldado)
   public ParticleSystem clarao;
   public Light luzDisparo;
   public float duracaoLuz = 0.05f;

   [Header("Vida")]
   public float vida = 100f;
   // sangue no sítio onde a bala acertou (o mesmo efeito dos soldados). Opcional
   public ParticleSystem efeitoSangue;
   // um ao calhas quando morre (Morte_1, Morte_2). Opcional
   public AudioClip[] sonsMorte;
   // segundos virado para ti depois de levar um tiro, mesmo sem te ver
   public float tempoVirarAoTiro = 3f;

   [Header("Estado (não mexer, só para ver no Play)")]
   // true enquanto te vê. É isto que a barra de deteção lê
   public bool aVer;
   // porque é que te vê ou não te vê (para testar: lê-se no Inspector durante o Play)
   public string motivo;
   // ligado pela DetecaoZona quando a barra enche: dispara enquanto te vê
   public bool emAlarme;
   public bool morto;

   // todos os snipers da cena (o Weapon procura aqui em qual acertou)
   public static readonly List<Sniper> todos = new List<Sniper>();

   private Transform jogador;
   private CharacterController controladorJogador;
   private Quaternion rotacaoInicial;
   private Quaternion rotacaoInicialCorpo;
   private float anguloAtual = 0f;
   private float inclinacaoAtual;
   private int sentido = 1;
   private float pausaAte = 0f;
   private float ultimaVezQueViu = -999f;
   private Vector3 pontoVisto;
   private LineRenderer laser;
   private PlayerStats statsJogador;
   private Animator anim;
   private bool temDie;
   private bool temDieHead;
   private bool temHit;
   private bool temShoot;
   private float proximoTiro = 0f;
   private float viradoAte = 0f;

   private void Awake()
   {
      if (torre == null) torre = transform.parent != null ? transform.parent : transform;
      if (pontaArma == null) pontaArma = transform;
      if (audioSource == null) audioSource = GetComponent<AudioSource>();

      // o mesmo que o Enemy e a CamaraVigilancia: o objeto do jogador chama-se "Player"
      GameObject p = GameObject.Find("Player");
      if (p != null)
      {
         jogador = p.transform;
         controladorJogador = p.GetComponent<CharacterController>();
         statsJogador = p.GetComponent<PlayerStats>();
      }
      else Debug.LogWarning(name + ": não encontrei o objeto Player");

      rotacaoInicial = transform.localRotation;
      if (corpo != null) rotacaoInicialCorpo = corpo.localRotation;
      inclinacaoAtual = inclinacao;

      // Animator do modelo do soldado. Só usa os parâmetros que existirem:
      // com o Soldado_Animator (soldado copiado) há Shoot, Hit, Die e DieHead;
      // com o Sniper_Animator só há os que lhe acrescentares
      if (corpo != null) anim = corpo.GetComponentInChildren<Animator>();
      if (anim != null)
      {
         foreach (AnimatorControllerParameter par in anim.parameters)
         {
            bool trigger = par.type == AnimatorControllerParameterType.Trigger;
            if (trigger && par.name == "Die") temDie = true;
            if (trigger && par.name == "DieHead") temDieHead = true;
            if (trigger && par.name == "Hit") temHit = true;
            if (par.type == AnimatorControllerParameterType.Bool && par.name == "Shoot") temShoot = true;
         }
      }
      if (luzDisparo != null) luzDisparo.enabled = false;

      if (inicioAleatorio)
      {
         anguloAtual = Random.Range(-anguloVarrimento, anguloVarrimento);
         sentido = Random.value < 0.5f ? 1 : -1;
      }
      AplicarRotacao();
      CriarLaser();
   }

   private void OnEnable()
   {
      todos.Add(this);
   }

   private void OnDisable()
   {
      todos.Remove(this);
   }

   private void OnValidate()
   {
      // no Editor, o holofote acompanha logo os valores da visão
      if (holofote != null && luzIgualVisao)
      {
         holofote.type = LightType.Spot;
         holofote.spotAngle = anguloVisao;
         holofote.range = alcance;
      }
   }

   private void Update()
   {
      if (morto) return;

      if (jogador == null || controladorJogador == null)
      {
         motivo = "não encontrei o Player (ou não tem Character Controller)";
         return;
      }

      bool viaAntes = aVer;
      aVer = VeJogador();

      // animação de disparar (Soldado_Animator) só durante o alarme, enquanto te vê
      if (temShoot) anim.SetBool("Shoot", emAlarme && aVer);

      if (aVer)
      {
         ultimaVezQueViu = Time.time;
         SeguirJogador();
         if (!viaAntes)
         {
            Tocar(somApanhou);
            // acabou de te apanhar: aponta tempoMira segundos antes de poder disparar
            proximoTiro = Mathf.Max(proximoTiro, Time.time + tempoMira);
         }
         // só dispara durante o alarme, e só enquanto te vê (a linha até ti está livre)
         if (emAlarme && Time.time >= proximoTiro) Disparar();
      }
      else
      {
         if (viaAntes) Tocar(somPerdeu);
         // levou um tiro há pouco: fica virado para ti
         if (Time.time < viradoAte) SeguirJogador();
         // fica um bocado a olhar para onde te viu; depois volta a varrer
         else if (Time.time - ultimaVezQueViu > tempoEsquecer) Varrer();
      }

      AplicarRotacao();
   }

   private void LateUpdate()
   {
      if (morto) return;
      AtualizarLaser();
   }

   // ---------- Alarme e tiros ----------

   // chamado pela DetecaoZona: true quando a barra enche, false quando o alarme acaba
   public void Alarme(bool ligado)
   {
      emAlarme = ligado;
      // o primeiro tiro só sai depois de apontar (não dispara no mesmo instante em que a barra enche)
      if (ligado) proximoTiro = Time.time + tempoMira;
   }

   private void Disparar()
   {
      proximoTiro = Time.time + intervaloTiro;

      // a linha até ao topo da tua cápsula está livre (é o que o aVer quer dizer): acerta sempre
      if (statsJogador != null) statsJogador.TakeDamage(danoTiro);

      if (clarao != null) clarao.Play();
      if (luzDisparo != null)
      {
         luzDisparo.enabled = true;
         Invoke(nameof(ApagarLuz), duracaoLuz);
      }

      // som do tiro com um tom um pouco diferente em cada disparo, como nos soldados
      if (somTiro != null && audioSource != null)
      {
         audioSource.pitch = Random.Range(0.92f, 1.08f);
         audioSource.PlayOneShot(somTiro);
      }
   }

   private void ApagarLuz()
   {
      if (luzDisparo != null) luzDisparo.enabled = false;
   }

   // ---------- Levar tiros ----------

   // devolve o sniper em que o tiro acertou (o collider está dentro do Corpo dele), ou null
   public static Sniper Atingido(Collider collider)
   {
      foreach (Sniper sniper in todos)
         if (sniper.corpo != null && collider.transform.IsChildOf(sniper.corpo)) return sniper;
      return null;
   }

   // chamado pelo Weapon do jogador. Tiro na cabeça (collider com a tag Head) mata logo, como nos soldados
   public void LevarTiro(float dano, bool naCabeca, Vector3 ponto, Vector3 normal)
   {
      if (morto) return;

      if (efeitoSangue != null)
      {
         ParticleSystem sangue = Instantiate(efeitoSangue, ponto, Quaternion.LookRotation(normal));
         Destroy(sangue.gameObject, sangue.main.duration + sangue.main.startLifetime.constantMax);
      }

      vida -= naCabeca ? vida : dano;
      if (vida <= 0f)
      {
         Morrer(naCabeca);
         return;
      }

      // ainda vivo: reage ao tiro e vira-se para ti durante uns segundos
      if (temHit) anim.SetTrigger("Hit");
      viradoAte = Time.time + tempoVirarAoTiro;
   }

   private void Morrer(bool naCabeca)
   {
      morto = true;
      vida = 0f;
      aVer = false;
      emAlarme = false;
      motivo = "morto";

      if (laser != null) laser.enabled = false;
      if (holofote != null) holofote.enabled = false;
      ApagarLuz();

      if (anim != null)
      {
         if (temShoot) anim.SetBool("Shoot", false);
         if (naCabeca && temDieHead) anim.SetTrigger("DieHead");
         else if (temDie) anim.SetTrigger("Die");
      }

      if (sonsMorte != null && sonsMorte.Length > 0) Tocar(sonsMorte[Random.Range(0, sonsMorte.Length)]);
   }

   // ---------- Ver ----------

   // ponto a que o sniper aponta: o topo da cápsula do jogador (baixa quando ele se abaixa)
   private Vector3 PontoAlvo()
   {
      Bounds b = controladorJogador.bounds;
      return new Vector3(b.center.x, b.max.y - margemTopo, b.center.z);
   }

   // o jogador está dentro do cone, ao alcance, e nada o tapa
   private bool VeJogador()
   {
      Vector3 alvo = PontoAlvo();
      Vector3 paraAlvo = alvo - transform.position;
      float distancia = paraAlvo.magnitude;
      float angulo = Vector3.Angle(transform.forward, paraAlvo);

      // na Scene (com os Gizmos ligados): cinzento = fora do cone ou longe,
      // vermelho = no cone mas tapado, verde = vê-te
      if (distancia > alcance)
      {
         motivo = "longe: " + distancia.ToString("0") + " m (Alcance " + alcance.ToString("0") + ")";
         Debug.DrawLine(transform.position, alvo, Color.gray);
         return false;
      }
      if (angulo > anguloVisao * 0.5f)
      {
         motivo = "fora do cone: " + angulo.ToString("0") + " graus do centro (máximo " + (anguloVisao * 0.5f).ToString("0") + ")";
         Debug.DrawLine(transform.position, alvo, Color.gray);
         return false;
      }

      bool livre = LinhaLivre(alvo);
      Debug.DrawLine(transform.position, alvo, livre ? Color.green : Color.red);
      return livre;
   }

   // a linha do sniper até ao ponto alvo não é tapada por nada (paredes, terreno, árvores)
   private bool LinhaLivre(Vector3 alvo)
   {
      Vector3 direcao = alvo - transform.position;
      float distancia = direcao.magnitude;
      RaycastHit[] hits = Physics.RaycastAll(transform.position, direcao.normalized, distancia + 0.5f,
         camadasVisao, QueryTriggerInteraction.Ignore);
      System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

      foreach (RaycastHit hit in hits)
      {
         // as peças da torre e o próprio soldado não contam
         if (hit.transform.IsChildOf(torre)) continue;
         // bateu primeiro em ti: vê-te
         if (hit.transform.IsChildOf(jogador))
         {
            pontoVisto = hit.point;
            motivo = "vê-te";
            return true;
         }
         // bateu noutra coisa antes de chegar ao ponto alvo: estás tapado
         if (hit.distance < distancia - 0.05f)
         {
            motivo = "tapado por: " + hit.collider.name + " (a " + hit.distance.ToString("0") + " m)";
            return false;
         }
         // o resto está para lá de ti (o chão atrás, por exemplo): não tapa
         break;
      }
      // a linha chegou ao ponto alvo sem bater em nada pelo caminho: vê-te
      pontoVisto = alvo;
      motivo = "vê-te (a linha chegou ao topo da cápsula sem nada pelo caminho)";
      return true;
   }

   // ---------- Rodar ----------

   // de uma ponta à outra, com uma pausa em cada ponta
   private void Varrer()
   {
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

   // roda para o jogador, sem passar das pontas do varrimento
   private void SeguirJogador()
   {
      Quaternion baseMundo = (transform.parent != null ? transform.parent.rotation : Quaternion.identity) * rotacaoInicial;
      Vector3 d = Quaternion.Inverse(baseMundo) * (PontoAlvo() - transform.position);

      float anguloAlvo = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
      float inclinacaoAlvo = Mathf.Atan2(-d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;

      anguloAlvo = Mathf.Clamp(anguloAlvo, -anguloVarrimento, anguloVarrimento);
      inclinacaoAlvo = Mathf.Clamp(inclinacaoAlvo, -10f, 60f);

      // a seguir roda mais depressa do que a varrer
      float rapidez = velocidadeRotacao * 4f * Time.deltaTime;
      anguloAtual = Mathf.MoveTowards(anguloAtual, anguloAlvo, rapidez);
      inclinacaoAtual = Mathf.MoveTowards(inclinacaoAtual, inclinacaoAlvo, rapidez);
   }

   private void AplicarRotacao()
   {
      // primeiro inclina para baixo, depois roda para o lado
      transform.localRotation = rotacaoInicial * Quaternion.Euler(inclinacaoAtual, anguloAtual, 0f);
      // o soldado só roda para os lados (não se inclina com o holofote)
      if (corpo != null) corpo.localRotation = rotacaoInicialCorpo * Quaternion.Euler(0f, anguloAtual, 0f);
   }

   // ---------- Laser ----------

   private void CriarLaser()
   {
      if (materialLaser == null) return;

      GameObject obj = new GameObject("Laser");
      obj.transform.SetParent(transform, false);
      laser = obj.AddComponent<LineRenderer>();
      laser.sharedMaterial = materialLaser;
      laser.positionCount = 2;
      laser.useWorldSpace = true;
      laser.startWidth = larguraLaser;
      laser.endWidth = larguraLaser;
      laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      laser.receiveShadows = false;
      laser.enabled = false;
   }

   private void AtualizarLaser()
   {
      if (laser == null) return;

      bool mostrar = aVer || laserSempre;
      laser.enabled = mostrar;
      if (!mostrar) return;

      Vector3 inicio = pontaArma.position;
      Vector3 fim;
      if (aVer)
      {
         // acaba no sítio onde o raio te acertou
         fim = pontoVisto;
      }
      else
      {
         // aponta para onde o sniper olha, até bater em alguma coisa
         fim = transform.position + transform.forward * alcance;
         RaycastHit[] hits = Physics.RaycastAll(transform.position, transform.forward, alcance,
            camadasVisao, QueryTriggerInteraction.Ignore);
         float maisPerto = alcance;
         foreach (RaycastHit hit in hits)
         {
            if (hit.transform.IsChildOf(torre)) continue;
            if (hit.distance < maisPerto) { maisPerto = hit.distance; fim = hit.point; }
         }
      }
      laser.SetPosition(0, inicio);
      laser.SetPosition(1, fim);
   }

   private void Tocar(AudioClip som)
   {
      if (som == null || audioSource == null) return;
      // o tom pode ter ficado alterado pelo último tiro
      audioSource.pitch = 1f;
      audioSource.PlayOneShot(som);
   }

   // cone de visão na Scene quando o Pivo está selecionado.
   // Antes do Play, desenha já com a Inclinacao (o script só inclina o Pivo no Play)
   // e mostra a amarelo as duas pontas do varrimento
   private void OnDrawGizmosSelected()
   {
      Quaternion r = transform.rotation;
      if (!Application.isPlaying) r = transform.rotation * Quaternion.Euler(inclinacao, 0f, 0f);
      Vector3 frente = r * Vector3.forward;
      Vector3 cima = r * Vector3.up;
      Vector3 direita = r * Vector3.right;

      Gizmos.color = Color.red;
      float meio = anguloVisao * 0.5f;
      Vector3[] bordas =
      {
         Quaternion.AngleAxis(meio, cima) * frente,
         Quaternion.AngleAxis(-meio, cima) * frente,
         Quaternion.AngleAxis(meio, direita) * frente,
         Quaternion.AngleAxis(-meio, direita) * frente,
      };
      foreach (Vector3 b in bordas)
         Gizmos.DrawLine(transform.position, transform.position + b * alcance);
      Gizmos.DrawLine(transform.position, transform.position + frente * alcance);

      if (!Application.isPlaying)
      {
         // até onde o holofote vai para cada lado
         Gizmos.color = Color.yellow;
         Vector3 eixo = transform.parent != null ? transform.parent.up : Vector3.up;
         Gizmos.DrawLine(transform.position, transform.position + Quaternion.AngleAxis(anguloVarrimento, eixo) * frente * alcance);
         Gizmos.DrawLine(transform.position, transform.position + Quaternion.AngleAxis(-anguloVarrimento, eixo) * frente * alcance);
      }
   }
}
