using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class Enemy : MonoBehaviour
{
   private DetectionBasic detectionManager;

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
   private float walkingPointRadius = 2;


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
   // vozes de alerta quando te vê (escolhe uma ao calhas)
   public AudioClip[] sonsAlerta;
   // segundos até poder dar outro alerta (um valor muito grande = só da primeira vez)
   public float intervaloAlerta = 15f;
   // se no frame anterior já te via (para saber o momento em que te vê)
   private bool viaJogador = false;
   private float proximoAlerta = 0f;

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

   [Header("Enemy Animation and Spark Effect")]
   public Animator anim;
   // graus a somar à mira, se a arma ainda ficar um pouco torta (+ roda para a esquerda, - para a direita)
   public float ajusteMira = 0f;
   public ParticleSystem muzzleSpark;
   // luz laranja na ponta da arma, acesa só durante o clarão (fica desligada no Inspector)
   public Light luzDisparo;
   public float duracaoLuz = 0.05f;

   [Header("Enemy Mood/Situation")] 
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
   
   // metros extra de visão dados pelo Lider aos reforços (0 = nenhum). Acaba quando te perde de vista
   private float bonusReforco = 0f;

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
      
      visionRadiusInicial = visionRadius;
      shootingRadiusInicial = shootingRadius;
      
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

        detectionManager = transform.parent.GetComponent<DetectionBasic>();
   }

   private void Update()
   {
      // morto não faz mais nada até objecto ser destruido 
      if (presentHealth <= 0) return;
      
      playerInVisionRadius = Physics.CheckSphere(transform.position, visionRadius, playerLayer);
      playerInShootingRadius = Physics.CheckSphere(transform.position, shootingRadius, playerLayer);

      // acabou de te ver (no frame anterior não via): voz de alerta
      if (playerInVisionRadius && !viaJogador && Time.time >= proximoAlerta && detectionManager.detectionLevel > 10)
      {
         TocarSomAoCalhas(sonsAlerta);
         proximoAlerta = Time.time + intervaloAlerta;
      }
      
      // acabou de te perder de vista (no frame anterior via): o bónus de reforço acaba
      // e o Guard() repõe os valores de defeito
      if (viaJogador && !playerInVisionRadius) bonusReforco = 0f;
      
      viaJogador = playerInVisionRadius;

      if (!playerInVisionRadius && !playerInShootingRadius) Guard();

      if (playerInVisionRadius && detectionManager.detectionLevel < detectionManager.detectionUpperLimit) detectionManager.detectionLevel++;
      if (playerInVisionRadius && !playerInShootingRadius && detectionManager.detectionLevel > detectionManager.detectionPursueLimit) PursuePlayer();
     
      
      // no fim do Update()
      if (anim != null)
      {
         anim.SetFloat("Speed", enemyAgent.velocity.magnitude);
         // sem munição baixa a arma
         anim.SetBool("Shoot", playerInVisionRadius && TemMunicao());
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
      
      
      // Debug.Log(name + " | no NavMesh: " + enemyAgent.isOnNavMesh + " | caminho: " + enemyAgent.pathStatus + " | ponto: " + currentEnemyPosition);
      
      
      // inimigo fixo: não tem Walk Points, fica de guarda no sítio onde foi posto
      if (walkPoints.Length == 0)
      {
         // anda à velocidade normal (não à de perseguição)
         enemyAgent.speed = enemySpeed;

         // manda-o para a posição guardada no Awake()
         // se já lá estiver, fica parado; se te perseguiu, volta
         enemyAgent.SetDestination(posicaoInicial);

         // pathPending: o agente ainda está a calcular o caminho
         // remainingDistance: quanto lhe falta andar até ao destino
         // se já calculou e está a menos de 0,6 m, chegou ao posto
         // inimigo fixo: chegou ao posto
         if (!enemyAgent.pathPending && enemyAgent.remainingDistance < 0.6f)
         {
            // roda aos poucos (180 graus por segundo) até ficar virado como no início
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacaoInicial, 180f * Time.deltaTime);
         }

         // sai do Guard() para não correr o código da patrulha
         return;
      }
      
      enemyAgent.speed = enemySpeed;

      Vector3 alvo = walkPoints[currentEnemyPosition].transform.position;
      Vector3 alvoMesmaAltura = new Vector3(alvo.x, transform.position.y, alvo.z);

      
      // chegou ao ponto atual: passa ao seguinte, por ordem
      if (Vector3.Distance(transform.position, alvoMesmaAltura) < 0.6f)
      {
         currentEnemyPosition = (currentEnemyPosition + 1) % walkPoints.Length;
         alvo = walkPoints[currentEnemyPosition].transform.position;
      }

      enemyAgent.SetDestination(alvo);
   }
   
   // volta aos raios que estavam no Inspector ao carregar Play
   private void ReporRaios()
   {
      visionRadius = visionRadiusInicial + bonusZona + bonusReforco;
      shootingRadius = shootingRadiusInicial;
   }
   
   // chamada pela ZonaAlerta quando entras ou sais da zona
   public void BonusZona(float valor)
   {
      bonusZona = valor;
   }
   
   // chamada pelo Lider quando chama os reforços: mais visão até te perder de vista
   public void BonusReforco(float valor)
   {
      bonusReforco = valor;
   }
   
   
   private void PursuePlayer()
   {
      if (enemyAgent.SetDestination(playerBody.position))
      {
         // animations
         // anim.SetBool("Walk", false);
         // anim.SetBool("AimRun", true);
         // anim.SetBool("Shoot", false);
         // anim.SetBool("Die", false);
         //
         
         // + vision and shooting radius
         //visionRadius = 30f;
         
         // faz o maior dos dois
         visionRadius = Mathf.Max(30f, visionRadiusInicial + bonusZona + bonusReforco);
         
         
         
         // shootingRadius = 16f;
      }
      else
      {
         // anim.SetBool("Walk", false);
         // anim.SetBool("AimRun", false);
         // anim.SetBool("Shoot", false);
         // anim.SetBool("Die", true);
      }
        Disparar();
    }

    private void Disparar()
   {
      if (previouslyShoot) return;
      // a recarregar não dispara
      if (aRecarregar) return;
      // gastou os carregadores todos: já não dispara
      if (balasRestantes <= 0) return;
      // acabou de levar um tiro: ainda está atordoado e não dispara
      if (Time.time < atordoadoAte || AReagirAoTiro()) return;

      Vector3 origem = ShootingRaycastArea.transform.position;
      Vector3 direcao = (LookPoint.position - origem).normalized;

      // pontaria imperfeita: desvia a direção um ângulo ao calhas (para os lados e para cima/baixo).
      // a linha do tiro usa esta mesma direção, por isso mostra o tiro desviado
      float desvio = desvioMira;
      if (enemyAgent.velocity.magnitude > 0.3f) desvio += desvioExtraAAndar;
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
   
   private void ShootPlayer()
   {
      enemyAgent.SetDestination(transform.position);
      
      // roda só no eixo Y, para o soldado não se inclinar para a frente ou para trás
      Vector3 alvoOlhar = new Vector3(LookPoint.position.x, transform.position.y, LookPoint.position.z);
      transform.LookAt(alvoOlhar);

      /*if (!previouslyShoot)
      {
         
         // muzzleSpark.Play();
         // audioSource.PlayOneShot(shootingSound);
         
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

   // naCabeca: true quando o tiro foi na cabeça (o Weapon do jogador é que avisa)
   public void enemyHitDamage(float takeDamage, bool naCabeca = false)
   {
      // já morreu: os tiros seguintes não fazem nada (senão recomeçava a morrer)
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
      // enemyAgent.SetDestination(transform.position);
      
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
      
      enemySpeed = 0f;
      shootingRadius = 0f;
      visionRadius = 0f;
      playerInVisionRadius = false;
      playerInShootingRadius =  false;
      
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

