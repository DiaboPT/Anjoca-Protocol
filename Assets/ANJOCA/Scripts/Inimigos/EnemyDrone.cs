using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyDrone : MonoBehaviour
{
   [Header("Enemy Drone Health and Damage")] 
   public float enemyHealth = 100f;
   public float presentHealth;
   
   public float giveDamage = 3f;
   // public HealthBar healthBar;
   
   [Header("Enemy Drone Things")]
   // public NavMeshAgent enemyAgent;
   public Transform LookPoint;
   // ponta da arma, de onde sai o tiro (um objeto vazio basta, já não precisa de ser uma Camera)
   public Transform ShootingRaycastArea;
   public Transform playerBody;
   public LayerMask playerLayer;
   
   [Header("Enemy Drone Guarding Var")]
   public GameObject[] walkPoints;
   private int currentEnemyPosition = 0;
   public float enemySpeed;
   private float walkingPointRadius = 2;
   
   
   [Header("Sounds and UI")]
   // public AudioClip shootingSound;
   // public AudioClip flameSound;
   // public AudioSource audioSource;
   public AudioClip shootingSound;
   public AudioSource audioSource;
   // sons metálicos quando leva um tiro e continua a voar (escolhe um ao calhas)
   public AudioClip[] sonsImpacto;
   // sons da explosão quando é abatido (escolhe um ao calhas)
   public AudioClip[] sonsExplosao;

   [Header("Motor")]
   // zumbido das hélices, sempre a tocar (em loop) enquanto o drone voa
   public AudioClip somMotor;
   [Range(0f, 1f)] public float volumeMotor = 0.5f;
   // tom do motor parado no ar e à velocidade normal (a voar, o zumbido fica mais agudo)
   public float tomParado = 0.9f;
   public float tomMaximo = 1.2f;
   // Audio Source só para o motor (vazio = o script cria um igual ao dos tiros)
   public AudioSource fonteMotor;

   [Header("Alerta")]
   // bips de alerta quando te vê (escolhe um ao calhas)
   public AudioClip[] sonsAlerta;
   // segundos até poder dar outro alerta (um valor muito grande = só da primeira vez)
   public float intervaloAlerta = 15f;
   // se no frame anterior já te via (para saber o momento em que te vê)
   private bool viaJogador = false;
   private float proximoAlerta = 0f;

   [Header("Enemy Drone Shooting Var")]
   public float timeBetweenShoot;
   bool previouslyShoot;
   // depois de levar um tiro, fica este tempo sem disparar (enquanto recupera do solavanco)
   public float tempoAtordoado = 0.3f;
   // momento (Time.time) a partir do qual volta a poder disparar
   private float atordoadoAte = 0f;

   [Header("Laser")]
   // raio visível de cada tiro (Line Renderer com material brilhante); aparece sempre, não só no F3
   public LineRenderer raioLaser;
   // segundos que o raio fica visível (vai ficando mais fino até desaparecer)
   public float duracaoLaser = 0.15f;
   // energia do fato drenada por cada tiro que te acerta (além da vida, que é o Give Damage)
   public float energiaDrenada = 20f;
   // prefab do efeito no ponto onde o laser bate (parede, chão ou jogador)
   public ParticleSystem efeitoImpactoLaser;
   // largura do raio no Inspector (o script vai-a baixando até 0 em cada tiro)
   private float larguraLaser;
   private Coroutine laserAtivo;

   [Header("Pontaria")]
   // desvio máximo de cada tiro, em graus, com o drone parado no ar (0 = acerta sempre)
   public float desvioMira = 2f;
   // graus a somar ao desvio quando dispara a voar
   public float desvioExtraAVoar = 2f;

   [Header("Enemy Drone Animation and Spark Effect")]
   // public Animator anim;
   // public ParticleSystem muzzleSpark;
   // public ParticleSystem muzzleFlame;
   // public ParticleSystem DestroyEffect;
   // Animator do modelo 3D (vazio = procura-o nos filhos; sem Animator, o movimento é todo feito por código)
   public Animator anim;
   public ParticleSystem muzzleSpark;
   // luz na ponta da arma, acesa só durante o clarão (fica desligada no Inspector)
   public Light luzDisparo;
   public float duracaoLuz = 0.05f;
   // peça da arma que roda para apontar ao jogador (vazio = a arma fica fixa e roda o drone todo)
   public Transform torreta;
   // graus por segundo a que a torreta roda
   public float velocidadeTorreta = 240f;

   [Header("Corpo (movimento por código)")]
   // objeto vazio com o modelo lá dentro: é ele que oscila, inclina e dá o solavanco
   // (vazio = procura um filho chamado "Corpo")
   public Transform corpo;
   // hélices que rodam por código (deixa vazio se o modelo já trouxer essa animação)
   public Transform[] helices;
   // eixo (local, de cada hélice) à volta do qual rodam
   public Vector3 eixoHelices = Vector3.up;
   // graus por segundo das hélices com o drone parado no ar
   public float rotacaoHelices = 1500f;
   // metros que sobe e desce a pairar, e quantas vezes por segundo
   public float alturaOscilacao = 0.1f;
   public float velocidadeOscilacao = 0.8f;
   // graus que inclina para a frente (ou para o lado) a voar à velocidade normal
   public float inclinacaoMaxima = 15f;

   [Header("Dano")]
   // prefab das faíscas (Particle System), criado onde a bala do jogador acerta
   public ParticleSystem efeitoFaiscas;
   // graus do solavanco quando leva um tiro, e quão depressa volta ao normal
   public float forcaSolavanco = 15f;
   public float recuperacaoSolavanco = 6f;
   // fumo (filho do drone, em loop) que começa a sair quando está muito danificado
   public ParticleSystem fumoAvariado;
   // fração da vida a partir da qual deita fumo (0.35 = 35%)
   [Range(0f, 1f)] public float vidaFumo = 0.35f;

   [Header("Morte")]
   // prefab da explosão (Particle System, com o fumo e os destroços como filhos), criado onde o drone é abatido
   public ParticleSystem efeitoExplosao;
   // luz da explosão (filha do drone, desligada no Inspector): acende e vai apagando
   public Light luzExplosao;
   public float duracaoLuzExplosao = 0.4f;
   // segundos que o motor demora a calar-se enquanto cai
   public float duracaoDesligarMotor = 1.5f;

   [Header("Enemy Drone Mood/Situation")] 
   public float visionRadius;
   public float shootingRadius;
   public bool playerInVisionRadius;
   public bool playerInShootingRadius;
   
   // Usado para não ser necessário walkPoints nos inimigos fixos
   private Vector3 posicaoInicial;
   private Quaternion rotacaoInicial;
   
   // raios do Inspector, para repor quando volta ao posto ou à patrulha
   private float visionRadiusInicial;
   private float shootingRadiusInicial;
   
   // metros extra de visão dados por uma ZonaAlerta (0 = nenhum)
   private float bonusZona = 0f;
   
   // material que o inimigo passa a usar quando morre
   public Material materialMorto;
   
   // quão depressa roda enquanto cai
   public float giroQueda = 5f;
   
   // linha do tiro, igual à do Player
   public LineRenderer linhaTiro;
   public float duracaoLinha = 0.1f;

   // parâmetros que existem no Animator (um modelo sem certas animações não dá avisos na Console)
   private HashSet<string> parametrosAnim = new HashSet<string>();

   // o drone não tem NavMeshAgent: a velocidade é calculada pelo que andou em cada frame
   private Vector3 velocidadeAtual;
   private Vector3 posicaoAnterior;

   // pose do Corpo e da Torreta no Inspector
   private Vector3 posicaoCorpoInicial;
   private Quaternion rotacaoCorpoInicial;
   private Quaternion rotacaoTorretaInicial;

   // graus de inclinação a voar e do solavanco do último tiro (x = frente/trás, z = lados)
   private Vector3 inclinacao;
   private Vector3 solavanco;
   // cada drone sobe e desce num ritmo desencontrado dos outros
   private float faseOscilacao;
   // 1 = hélices à velocidade normal; depois de morrer vai descendo até 0
   private float forcaHelices = 1f;

   private void Awake()
   {
      // se o Audio Source vier vazio, procura-o neste objeto ou nos filhos
      if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();
      presentHealth = enemyHealth;
      // healthBar.GiveFullHealthBar(enemyHealth);
      playerBody = GameObject.Find("Player").transform;
      // enemyAgent =  GetComponent<NavMeshAgent>();

      // num prefab não dá para guardar objetos da cena: se o Look Point vier vazio, vai buscar o "Look" do Player
      if (LookPoint == null) LookPoint = playerBody.Find("Look");
      
      posicaoInicial = transform.position;
      rotacaoInicial = transform.rotation;
      posicaoAnterior = transform.position;
      
      visionRadiusInicial = visionRadius;
      shootingRadiusInicial = shootingRadius;
      
      // a linha começa escondida
      if (linhaTiro != null) linhaTiro.enabled = false;

      // o laser também, e as pontas são posições no mundo (não mexem com o drone)
      if (raioLaser != null)
      {
         larguraLaser = raioLaser.widthMultiplier;
         raioLaser.useWorldSpace = true;
         raioLaser.positionCount = 2;
         raioLaser.enabled = false;
      }

      // o Animator está no modelo 3D, que fica dentro do Corpo
      if (anim == null) anim = GetComponentInChildren<Animator>();
      if (anim != null)
         foreach (AnimatorControllerParameter p in anim.parameters) parametrosAnim.Add(p.name);

      if (corpo == null) corpo = transform.Find("Corpo");
      if (corpo != null)
      {
         posicaoCorpoInicial = corpo.localPosition;
         rotacaoCorpoInicial = corpo.localRotation;
      }
      if (torreta != null) rotacaoTorretaInicial = torreta.localRotation;
      faseOscilacao = Random.Range(0f, 10f);

      // o fumo só aparece quando estiver muito danificado
      if (fumoAvariado != null) fumoAvariado.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

      LigarMotor();
   }

   // o motor tem um Audio Source à parte, em loop, para os tiros e os bips não o interromperem
   private void LigarMotor()
   {
      if (somMotor == null) return;
      if (fonteMotor == null)
      {
         if (audioSource == null) return;
         fonteMotor = gameObject.AddComponent<AudioSource>();
         fonteMotor.spatialBlend = audioSource.spatialBlend;
         fonteMotor.rolloffMode = audioSource.rolloffMode;
         fonteMotor.minDistance = audioSource.minDistance;
         fonteMotor.maxDistance = audioSource.maxDistance;
         fonteMotor.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
      }
      fonteMotor.playOnAwake = false;
      fonteMotor.clip = somMotor;
      fonteMotor.loop = true;
      fonteMotor.volume = volumeMotor;
      fonteMotor.pitch = tomParado;
      fonteMotor.Play();
      // cada drone começa o loop num ponto diferente, para não zumbirem todos em uníssono
      fonteMotor.time = Random.Range(0f, somMotor.length * 0.9f);
   }

   private void Update()
   {
      // morto não faz mais nada
      if (presentHealth <= 0) return;
      
      playerInVisionRadius = Physics.CheckSphere(transform.position, visionRadius, playerLayer);
      playerInShootingRadius = Physics.CheckSphere(transform.position, shootingRadius, playerLayer);

      // acabou de te ver (no frame anterior não via): bip de alerta
      if (playerInVisionRadius && !viaJogador && Time.time >= proximoAlerta)
      {
         TocarSomAoCalhas(sonsAlerta);
         proximoAlerta = Time.time + intervaloAlerta;
      }
      viaJogador = playerInVisionRadius;

      if (!playerInVisionRadius && !playerInShootingRadius) Guard();

      if (playerInVisionRadius && !playerInShootingRadius) PursuePlayer();
      if (playerInVisionRadius && playerInShootingRadius) ShootPlayer(); 

      // velocidade deste frame (metros andados ÷ tempo)
      if (Time.deltaTime > 0f) velocidadeAtual = (transform.position - posicaoAnterior) / Time.deltaTime;
      posicaoAnterior = transform.position;

      // só muda os parâmetros que existirem no Animator
      AnimFloat("Speed", velocidadeAtual.magnitude);
      AnimBool("Shoot", playerInVisionRadius);

      // motor: a voar mais depressa, o zumbido fica mais agudo
      if (fonteMotor != null)
      {
         float tomAlvo = Mathf.Lerp(tomParado, tomMaximo, RapidezRelativa());
         fonteMotor.pitch = Mathf.MoveTowards(fonteMotor.pitch, tomAlvo, Time.deltaTime);
      }
   }

   // 0 = parado no ar, 1 = à velocidade normal (Enemy Speed)
   private float RapidezRelativa()
   {
      if (enemySpeed <= 0f) return 0f;
      return Mathf.Clamp01(velocidadeAtual.magnitude / enemySpeed);
   }

   // corre depois das animações: hélices, pairar, inclinar, solavanco e torreta
   private void LateUpdate()
   {
      RodarHelices();

      // morto: quem o mexe é a física (Rigidbody)
      if (presentHealth <= 0) return;

      MexerCorpo();
      ApontarTorreta();
   }

   private void RodarHelices()
   {
      // depois de morrer as hélices vão parando (em cerca de 2 s)
      if (presentHealth <= 0) forcaHelices = Mathf.MoveTowards(forcaHelices, 0f, 0.5f * Time.deltaTime);
      if (helices == null || forcaHelices <= 0f) return;

      // a voar rodam um pouco mais depressa
      float graus = rotacaoHelices * (1f + 0.3f * RapidezRelativa()) * forcaHelices * Time.deltaTime;
      foreach (Transform helice in helices)
         if (helice != null) helice.Rotate(eixoHelices, graus, Space.Self);
   }

   private void MexerCorpo()
   {
      if (corpo == null) return;

      // inclina para o lado para onde voa (velocidade passada para os eixos do drone)
      Vector3 v = transform.InverseTransformDirection(velocidadeAtual);
      Vector3 alvo = new Vector3(v.z, 0f, -v.x) / Mathf.Max(enemySpeed, 0.01f) * inclinacaoMaxima;
      alvo = Vector3.ClampMagnitude(alvo, inclinacaoMaxima);
      inclinacao = Vector3.Lerp(inclinacao, alvo, 4f * Time.deltaTime);

      // o solavanco do último tiro vai desaparecendo
      solavanco = Vector3.Lerp(solavanco, Vector3.zero, recuperacaoSolavanco * Time.deltaTime);

      // sobe e desce devagar, a pairar
      float sobe = Mathf.Sin((Time.time + faseOscilacao) * velocidadeOscilacao * 2f * Mathf.PI) * alturaOscilacao;

      corpo.localPosition = posicaoCorpoInicial + Vector3.up * sobe;
      corpo.localRotation = Quaternion.Euler(inclinacao + solavanco) * rotacaoCorpoInicial;
   }

   private void ApontarTorreta()
   {
      if (torreta == null) return;

      float passo = velocidadeTorreta * Time.deltaTime;
      if (playerInVisionRadius && LookPoint != null)
      {
         // vê-te: a torreta roda aos poucos até apontar para ti
         Vector3 direcao = LookPoint.position - torreta.position;
         if (direcao.sqrMagnitude > 0.001f)
            torreta.rotation = Quaternion.RotateTowards(torreta.rotation, Quaternion.LookRotation(direcao), passo);
      }
      else
      {
         // não te vê: volta à posição do Inspector
         torreta.localRotation = Quaternion.RotateTowards(torreta.localRotation, rotacaoTorretaInicial, passo);
      }
   }

   


   /*private void Guard()
   {
      if (Vector3.Distance(walkPoints[currentEnemyPosition].transform.position, transform.position) < walkingPointRadius)
      {
         currentEnemyPosition = UnityEngine.Random.Range(0, walkPoints.Length);
         if (currentEnemyPosition >= walkPoints.Length)
         {
            currentEnemyPosition = 0;
         }
      }

      transform.position = Vector3.MoveTowards(transform.position, walkPoints[currentEnemyPosition].transform.position,
         Time.deltaTime * enemySpeed);
      // changing enemy facing
      transform.LookAt(walkPoints[currentEnemyPosition].transform.position);
   }*/
   
   private void Guard()
   {
      // perdeu o contacto com o player: os raios voltam aos do Inspector
      ReporRaios();
      
      // drone fixo: não tem Walk Points, fica a pairar no sítio onde foi posto
      if (walkPoints.Length == 0)
      {
         // voa em linha reta para a posição guardada no Awake(), à velocidade normal
         // se já lá estiver, não se mexe; se te perseguiu, volta
         VoarPara(posicaoInicial, enemySpeed);

         // a menos de 0,1 m do posto conta como chegado
         if (Vector3.Distance(transform.position, posicaoInicial) < 0.1f)
            // roda aos poucos (180 graus por segundo) até ficar virado como no início
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacaoInicial, 180f * Time.deltaTime);

         // sai do Guard() para não correr o código da patrulha
         return;
      }
      
      Vector3 alvo = walkPoints[currentEnemyPosition].transform.position;
      VoarPara(alvo, enemySpeed);
      if (Vector3.Distance(transform.position, alvo) < 0.1f)
         currentEnemyPosition = (currentEnemyPosition + 1) % walkPoints.Length;
   }
   
   // volta aos raios que estavam no Inspector ao carregar Play
   private void ReporRaios()
   {
      visionRadius = visionRadiusInicial + bonusZona;
      shootingRadius = shootingRadiusInicial;
   }
   
   // chamada pela ZonaAlerta quando entras ou sais da zona
   public void BonusZona(float valor)
   {
      bonusZona = valor;
   }
   
   private void VoarPara(Vector3 alvo, float velocidade)
   {
      transform.position = Vector3.MoveTowards(transform.position, alvo, velocidade * Time.deltaTime);
      Vector3 direcao = alvo - transform.position;
      direcao.y = 0f;
      if (direcao.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direcao);
   }
   
   
   private void PursuePlayer()
   {
      Vector3 alvo = new Vector3(playerBody.position.x, transform.position.y, playerBody.position.z);
      VoarPara(alvo, enemySpeed);
      visionRadius = 30f;
      // shootingRadius = 16f;
      
      
      /*if (enemyAgent.SetDestination(playerBody.position))
      {
         // animations
         // anim.SetBool("Walk", false);
         // anim.SetBool("AimRun", true);
         // anim.SetBool("Shoot", false);
         // anim.SetBool("Die", false);
         
         // + vision and shooting radius
         visionRadius = 30f;
         shootingRadius = 16f;
      }
      else
      {
         // anim.SetBool("Walk", false);
         // anim.SetBool("AimRun", false);
         // anim.SetBool("Shoot", false);
         // anim.SetBool("Die", true);
      }*/
      
      Disparar();
   }
   
   
   private void Disparar()
   {
      if (previouslyShoot) return;
      // acabou de levar um tiro: ainda está a recuperar do solavanco e não dispara
      if (Time.time < atordoadoAte) return;

      // sem ponta da arma (por exemplo logo depois de trocar o campo de Camera para Transform), dispara do centro do drone
      Transform ponta = ShootingRaycastArea != null ? ShootingRaycastArea : transform;
      Vector3 origem = ponta.position;
      Vector3 direcao = (LookPoint.position - origem).normalized;

      // pontaria imperfeita: desvia a direção um ângulo ao calhas (para os lados e para cima/baixo).
      // a linha do tiro usa esta mesma direção, por isso mostra o tiro desviado
      float desvio = desvioMira;
      if (velocidadeAtual.magnitude > 0.3f) desvio += desvioExtraAVoar;
      Vector2 aoCalhas = Random.insideUnitCircle * desvio;
      direcao = Quaternion.LookRotation(direcao) * Quaternion.Euler(aoCalhas.y, aoCalhas.x, 0f) * Vector3.forward;
      
      // se não acertar em nada, a linha vai até ao alcance máximo
      Vector3 fim = origem + direcao * visionRadius;

      RaycastHit hit;
      if (Physics.Raycast(origem, direcao, out hit, visionRadius))
      {
         // Debug.Log("Shooting " + hit.transform.name);
         fim = hit.point;
         PlayerStats alvo = hit.transform.GetComponent<PlayerStats>();
         if (alvo != null)
         {
            // o laser tira vida e drena a energia do fato
            alvo.TakeDamage(giveDamage);
            alvo.DrainEnergy(energiaDrenada);
         }

         // brilho e faíscas onde o laser bate, virados para fora da superfície
         if (efeitoImpactoLaser != null)
         {
            ParticleSystem impacto = Instantiate(efeitoImpactoLaser, hit.point, Quaternion.LookRotation(hit.normal));
            Destroy(impacto.gameObject, DuracaoEfeito(impacto));
         }
         
         // queimadura do laser no cenário (brilha e arrefece)
         MarcasImpacto.Laser(hit);
      }

      // raio do laser, da ponta da arma até onde bateu
      MostrarLaser(origem, fim);
      
      
      // mostra a linha da ponta da arma até onde o tiro acabou (só com o debug visual, F3)
      if (linhaTiro != null)
      {
         linhaTiro.enabled = ModoDebugVisual.Tiros;
         linhaTiro.SetPosition(0, origem);
         linhaTiro.SetPosition(1, fim);
         Invoke(nameof(EsconderLinha), duracaoLinha);
      }

      // brilho na ponta da arma
      if (muzzleSpark != null) muzzleSpark.Play();
      if (luzDisparo != null)
      {
         luzDisparo.enabled = true;
         Invoke(nameof(ApagarLuz), duracaoLuz);
      }

      // som do laser, com um tom ligeiramente diferente em cada disparo (para não soar sempre igual)
      if (audioSource != null && shootingSound != null)
      {
         audioSource.pitch = Random.Range(0.92f, 1.08f);
         audioSource.PlayOneShot(shootingSound);
      }

      previouslyShoot = true;
      Invoke(nameof(ActiveShooting), timeBetweenShoot);
   }
   
   private void EsconderLinha()
   {
      linhaTiro.enabled = false;
   }

   private void MostrarLaser(Vector3 origem, Vector3 fim)
   {
      if (raioLaser == null) return;
      raioLaser.SetPosition(0, origem);
      raioLaser.SetPosition(1, fim);

      // se o raio anterior ainda estiver a apagar-se, recomeça
      if (laserAtivo != null) StopCoroutine(laserAtivo);
      laserAtivo = StartCoroutine(ApagarLaser());
   }

   // o raio aparece com a largura do Inspector e vai ficando mais fino até desaparecer
   private IEnumerator ApagarLaser()
   {
      raioLaser.widthMultiplier = larguraLaser;
      raioLaser.enabled = true;

      float tempo = 0f;
      while (tempo < duracaoLaser)
      {
         tempo += Time.deltaTime;
         raioLaser.widthMultiplier = Mathf.Lerp(larguraLaser, 0f, tempo / duracaoLaser);
         yield return null;
      }
      raioLaser.enabled = false;
      raioLaser.widthMultiplier = larguraLaser;
      laserAtivo = null;
   }

   private void ApagarLuz()
   {
      luzDisparo.enabled = false;
   }
   
   private void ShootPlayer()
   {
      // enemyAgent.SetDestination(transform.position);
      
      if (torreta != null)
      {
         // com torreta: o drone roda só no eixo Y e quem aponta para cima ou para baixo é a torreta
         Vector3 alvoOlhar = new Vector3(LookPoint.position.x, transform.position.y, LookPoint.position.z);
         transform.LookAt(alvoOlhar);
      }
      else
      {
         // sem torreta: o drone todo inclina-se para apontar a arma ao jogador (como antes)
         transform.LookAt(LookPoint);
      }

      /*if (!previouslyShoot)
      {
         // muzzleSpark.Play();
         // audioSource.PlayOneShot(shootingSound);
         // muzzleFlame.Play();
         // audioSource.PlayOneShot(flameSound);
         
         
         RaycastHit hit;

         if (Physics.Raycast(ShootingRaycastArea.transform.position, ShootingRaycastArea.transform.forward, out hit,
                shootingRadius))
         {
            Debug.Log("Shooting " + hit.transform.name);
            
            PlayerStats playerBody =  hit.transform.GetComponent<PlayerStats>();

            if (playerBody != null)
            {
               playerBody.TakeDamage(giveDamage);
            }
            
            // anim.SetBool("Shoot", true);
            // anim.SetBool("Walk", false);
            // anim.SetBool("AimRun", false);
            // anim.SetBool("Die", false);
         }


         previouslyShoot = true;
         Invoke(nameof(ActiveShooting), timeBetweenShoot);
      }*/
      Disparar();
   }

   private void ActiveShooting()
   {
      previouslyShoot = false;
   }

   public void enemyDroneHitDamage(float takeDamage)
   {
      // já está morto: ignora os tiros seguintes
      if (presentHealth <= 0) return;
      
      presentHealth -= takeDamage;
      // healthBar.SetHealth(presentHealth);
      
      // + vision and shooting radius
      visionRadius = 40f;
      // shootingRadius = 30f;

      if (presentHealth <= 0)
      {
         // anim.SetBool("Shoot", false);
         // anim.SetBool("Walk", false);
         // anim.SetBool("AimRun", false);
         // anim.SetBool("Die", true);
         
         enemyDie();
      }
      else
      {
         // ainda voa: som metálico do impacto
         TocarSomAoCalhas(sonsImpacto);

         // solavanco no ar, para um lado ao calhas (vai desaparecendo no MexerCorpo)
         solavanco += Random.insideUnitSphere * forcaSolavanco;
         solavanco = Vector3.ClampMagnitude(solavanco, forcaSolavanco * 2f);
         // e a animação Hit, se o Animator a tiver
         AnimTrigger("Hit");

         // fica uns instantes sem conseguir disparar
         atordoadoAte = Time.time + tempoAtordoado;

         // muito danificado: começa a deitar fumo
         if (fumoAvariado != null && !fumoAvariado.isPlaying && presentHealth <= enemyHealth * vidaFumo)
            fumoAvariado.Play();
      }
   }

   // faíscas no ponto onde a bala acertou, viradas para fora do drone (chamado pelo Weapon do jogador)
   public void MostrarFaiscas(Vector3 ponto, Vector3 normal)
   {
      if (efeitoFaiscas == null) return;
      ParticleSystem faiscas = Instantiate(efeitoFaiscas, ponto, Quaternion.LookRotation(normal));
      Destroy(faiscas.gameObject, DuracaoEfeito(faiscas));
   }

   // segundos até acabar a última partícula do efeito (contando com os Particle Systems filhos)
   private float DuracaoEfeito(ParticleSystem efeito)
   {
      float maximo = 0f;
      foreach (ParticleSystem ps in efeito.GetComponentsInChildren<ParticleSystem>())
      {
         ParticleSystem.MainModule main = ps.main;
         maximo = Mathf.Max(maximo, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
      }
      return maximo;
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

   // só mexe nos parâmetros que existem no Animator (sem Animator não faz nada)
   private void AnimFloat(string nome, float valor)
   {
      if (parametrosAnim.Contains(nome)) anim.SetFloat(nome, valor);
   }

   private void AnimBool(string nome, bool valor)
   {
      if (parametrosAnim.Contains(nome)) anim.SetBool(nome, valor);
   }

   private void AnimTrigger(string nome)
   {
      if (parametrosAnim.Contains(nome)) anim.SetTrigger(nome);
   }

   private void enemyDie()
   {
      // DestroyEffect.Play();
      // enemyAgent.SetDestination(transform.position);
      enemySpeed = 0f;
      shootingRadius = 0f;
      visionRadius = 0f;
      playerInVisionRadius = false;
      playerInShootingRadius =  false;

      // com Animator: para de disparar e toca a animação de morte (se existir)
      AnimBool("Shoot", false);
      AnimFloat("Speed", 0f);
      AnimTrigger("Die");

      // explosão no sítio onde foi abatido (fica no ar, não cai com o drone)
      if (efeitoExplosao != null)
      {
         ParticleSystem explosao = Instantiate(efeitoExplosao, transform.position, Quaternion.identity);
         Destroy(explosao.gameObject, DuracaoEfeito(explosao));
      }
      TocarSomAoCalhas(sonsExplosao);
      if (luzExplosao != null) StartCoroutine(ApagarLuzExplosao());

      // o motor vai-se calando enquanto cai (e as hélices vão parando no RodarHelices)
      if (fonteMotor != null) StartCoroutine(DesligarMotor());

      // deita fumo enquanto cai, mesmo que ainda não estivesse a deitar
      if (fumoAvariado != null && !fumoAvariado.isPlaying) fumoAvariado.Play();
      
      // pinta de preto todas as malhas do inimigo (corpo, arma, peças filhas)
      if (materialMorto != null)
      {
         foreach (Renderer malha in GetComponentsInChildren<Renderer>())
         {
            // salta linhas e partículas, que não fazem parte do corpo
            if (malha is LineRenderer || malha is ParticleSystemRenderer) continue;

            // troca todos os materiais desta malha pelo preto
            Material[] mats = malha.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = materialMorto;
            malha.sharedMaterials = mats;
         }
      }
      // enquanto voa é trigger e atravessa tudo; ao morrer passa a sólido para bater no chão
      GetComponent<Collider>().isTrigger = false;
      // liga a física para a gravidade o puxar para o chão
      Rigidbody rb = GetComponent<Rigidbody>();
      if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
      rb.isKinematic = false;
      rb.useGravity = true;

// põe-no a rodar numa direção ao acaso, para cair aos rebolões
      rb.angularVelocity = UnityEngine.Random.insideUnitSphere * giroQueda;
      
      Object.Destroy(gameObject, 5.0f);
   }
   
   // a luz da explosão fica no sítio onde ele rebentou, acende de repente e vai apagando
   private IEnumerator ApagarLuzExplosao()
   {
      luzExplosao.transform.SetParent(null, true);
      float intensidade = luzExplosao.intensity;
      luzExplosao.enabled = true;

      float tempo = 0f;
      while (tempo < duracaoLuzExplosao)
      {
         tempo += Time.deltaTime;
         luzExplosao.intensity = Mathf.Lerp(intensidade, 0f, tempo / duracaoLuzExplosao);
         yield return null; // espera pelo frame seguinte
      }
      Destroy(luzExplosao.gameObject);
   }

   // baixa o volume e o tom do motor até ficar calado
   private IEnumerator DesligarMotor()
   {
      float volume = fonteMotor.volume;
      float tom = fonteMotor.pitch;

      float tempo = 0f;
      while (tempo < duracaoDesligarMotor)
      {
         tempo += Time.deltaTime;
         float t = tempo / duracaoDesligarMotor;
         fonteMotor.volume = Mathf.Lerp(volume, 0f, t);
         fonteMotor.pitch = Mathf.Lerp(tom, tom * 0.5f, t);
         yield return null;
      }
      fonteMotor.Stop();
   }

   // desenha os raios na Scene quando o inimigo está selecionado
   private void OnDrawGizmosSelected()
   {
      // raio de visão a amarelo
      Gizmos.color = Color.yellow;
      Gizmos.DrawWireSphere(transform.position, visionRadius);

      // raio de disparo a vermelho
      Gizmos.color = Color.red;
      Gizmos.DrawWireSphere(transform.position, shootingRadius);
   }
}
