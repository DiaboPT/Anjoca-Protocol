using UnityEngine;

// Olhos do soldado: decide se te vê, e quão depressa dá por ti.
// Substitui o Physics.CheckSphere do Enemy (que via através das paredes e pelas costas,
// e não via nada a 20 m mesmo contigo à frente).
//
// Como funciona:
//  1. Cone de visão: central (vê longe e depressa) e periférico (vê perto e devagar). Pelas costas não vê.
//  2. Alcance: até onde vê com luz. No escuro (ZonaSemEnergia) vê pouco, menos dentro do feixe da lanterna.
//  3. Linha de vista: 3 raios dos olhos do soldado até à tua cabeça, ao peito e às ancas.
//     Os 3 tapados (parede, laje, caixote alto) = não te vê, nem que esteja a 2 m.
//  4. Medidor de 0 a 1: enche quando te vê, mais depressa quanto mais perto, mais à vista e mais iluminado estiveres.
//     0.3 = desconfiado (vai ver). 1 = detetado (combate).
//
// Também guarda o que o soldado SABE de ti: onde te viu pela última vez e para onde ias.
// O Enemy usa isso para te perseguir e procurar, em vez de ler a tua posição real.
//
// Vai no mesmo objeto do Enemy. Se faltar, o Enemy acrescenta-o sozinho no Awake.
// Não tem Awake próprio: é o Enemy que o prepara (Iniciar), para não depender da ordem dos scripts.
public class PercecaoInimigo : MonoBehaviour
{
   [Header("Cone de visão")]
   [Tooltip("Graus, no total, da visão central (metade para cada lado da frente). Aqui vê longe e depressa.")]
   public float anguloCentral = 50f;
   [Tooltip("Graus, no total, da visão periférica. Fora deste cone (pelas costas) não te vê.")]
   public float anguloPeriferico = 120f;
   [Tooltip("Metros que vê na visão central, com luz.")]
   public float alcanceCentral = 35f;
   [Tooltip("Metros que vê na visão periférica, com luz.")]
   public float alcancePeriferico = 15f;
   [Tooltip("Mais perto do que isto sente-te a toda a volta (mesmo pelas costas), se não houver nada pelo meio.")]
   public float distanciaSentir = 1.5f;
   [Tooltip("Mais perto do que isto, dentro do cone, a distância e o escuro já não te escondem: à frente dele, a 2 ou 3 m, dá por ti em menos de 1 s.")]
   public float distanciaPerto = 3f;

   [Header("Escuro (quadro elétrico sabotado)")]
   [Tooltip("Metros que vê no escuro, fora da lanterna, na visão periférica. Na central usa o Raio Visao Escuro do Enemy.")]
   public float alcanceEscuroPeriferico = 3f;
   [Tooltip("Metros que vê dentro do feixe da lanterna (o ângulo é o Spot Angle da própria Light).")]
   public float alcanceLanterna = 15f;

   [Header("Medidor")]
   [Tooltip("Quanto sobe por segundo no caso mais fácil (colado a ele, na central, de pé, à vista). 2 = cheio em 0.5 s.")]
   public float subidaMedidor = 2f;
   [Tooltip("Quanto desce por segundo quando deixa de te ver. 0.25 = esquece um relance em 4 s.")]
   public float descidaMedidor = 0.25f;
   [Tooltip("A partir daqui fica desconfiado: para, olha e vai ver.")]
   [Range(0f, 1f)] public float limiarDesconfiado = 0.3f;

   [Header("Fatores do medidor")]
   [Tooltip("Multiplica a subida no fim do alcance (colado a ele é 1).")]
   [Range(0f, 1f)] public float fatorLonge = 0.15f;
   [Tooltip("Multiplica a subida na visão periférica.")]
   [Range(0f, 1f)] public float fatorPeriferico = 0.4f;
   [Tooltip("Multiplica a subida com só 2 das 3 partes do corpo à vista.")]
   [Range(0f, 1f)] public float fatorDuasPartes = 0.7f;
   [Tooltip("Multiplica a subida com só 1 das 3 partes do corpo à vista.")]
   [Range(0f, 1f)] public float fatorUmaParte = 0.4f;
   [Tooltip("Multiplica a subida com o jogador abaixado (Ctrl).")]
   [Range(0f, 1f)] public float fatorAbaixado = 0.6f;
   [Tooltip("Multiplica a subida com o jogador parado.")]
   [Range(0f, 2f)] public float fatorParado = 0.7f;
   [Tooltip("Multiplica a subida com o jogador a andar devagar (Alt esquerdo). 0.7 = como se estivesse parado. Junta-se ao Fator Abaixado.")]
   [Range(0f, 2f)] public float fatorDevagar = 0.7f;
   [Tooltip("Multiplica a subida com o jogador a correr.")]
   [Range(0f, 3f)] public float fatorACorrer = 1.5f;
   [Tooltip("Multiplica a subida no escuro, fora da lanterna.")]
   [Range(0f, 1f)] public float fatorEscuro = 0.3f;
   [Tooltip("Multiplica a subida enquanto estás dentro de uma ZonaAlerta destes soldados.")]
   public float fatorZonaAlerta = 2f;

   [Header("Linha de vista")]
   [Tooltip("Cada raio que atravessa a layer Vegetacao (árvores) vale isto em vez de 1.")]
   [Range(0f, 1f)] public float valorVegetacao = 0.5f;
   [Tooltip("Segundos entre verificações. 0.1 = 10 vezes por segundo (chega, e poupa raios com muitos soldados).")]
   public float intervaloVerificacao = 0.1f;

   // ---------- O que os outros scripts leem ----------
   // valor do medidor, de 0 a 1 (aparece no Inspector para ver durante o Play)
   [SerializeField] private float medidor = 0f;
   public float Medidor => medidor;
   // na última verificação, havia pelo menos uma parte do teu corpo à vista, dentro do cone e do alcance
   public bool VeAgora { get; private set; }
   // medidor cheio e a ver-te agora
   public bool Detetado => VeAgora && medidor >= 1f;
   // medidor acima do limiar e a ver-te agora (um relance chega)
   public bool Desconfiado => VeAgora && medidor >= limiarDesconfiado;

   // ---------- O que o soldado sabe de ti ----------
   // onde te viu (ou soube de ti) pela última vez
   public Vector3 UltimaPosicao { get; private set; }
   // para onde ias, e a que velocidade, nesse momento (metros por segundo, só na horizontal)
   public Vector3 UltimaVelocidade { get; private set; }
   // quando foi (Time.time). -1 = nunca soube de ti
   public float MomentoContacto { get; private set; } = -1f;

   private Enemy enemy;
   private Transform jogador;
   private CharacterController ccJogador;
   private PlayerMovement movimentoJogador;
   private Transform cabeca;          // osso Head do modelo (pode não existir)
   private int mascaraOclusao;        // o que tapa a vista
   private int layerVegetacao = -1;
   private float proximaVerificacao;
   private float ultimaVerificacao;
   // as 3 partes do corpo na última verificação, e se cada uma estava à vista (para o debug)
   private readonly Vector3[] pontosCorpo = new Vector3[3];
   private readonly bool[] parteVista = new bool[3];

   // chamado pelo Enemy no Awake dele
   public void Iniciar(Enemy dono, Transform corpoJogador, Animator anim)
   {
      enemy = dono;
      jogador = corpoJogador;
      if (jogador != null)
      {
         ccJogador = jogador.GetComponent<CharacterController>();
         movimentoJogador = jogador.GetComponent<PlayerMovement>();
      }
      if (anim != null && anim.isHuman) cabeca = anim.GetBoneTransform(HumanBodyBones.Head);

      // tapa tudo, menos o jogador, os inimigos e as layers que só servem para triggers, mira e minimapa
      mascaraOclusao = ~LayerMask.GetMask("Ignore Raycast", "TransparentFX", "UI", "Player", "Enemy",
         "Minimapa", "VolumeMinimapa", "VolumeMira");
      layerVegetacao = LayerMask.NameToLayer("Vegetacao");

      // cada soldado começa a verificar num momento diferente, para não calharem todos no mesmo frame
      proximaVerificacao = Time.time + Random.Range(0f, intervaloVerificacao);
      ultimaVerificacao = Time.time;
   }

   // chamado pelo Enemy em cada frame. multiplicadorEstado: 1 calmo, mais quando já anda à tua procura
   public void Atualizar(float multiplicadorEstado)
   {
      if (enemy == null || jogador == null) return;
      if (Time.time < proximaVerificacao) return;

      float dt = Time.time - ultimaVerificacao;
      ultimaVerificacao = Time.time;
      proximaVerificacao = Time.time + intervaloVerificacao;

      float subida = CalcularSubida();
      VeAgora = subida > 0f;

      if (VeAgora)
      {
         medidor = Mathf.Min(1f, medidor + subida * multiplicadorEstado * dt);
         // um relance já conta como pista
         if (medidor >= limiarDesconfiado) RegistarPista(jogador.position, VelocidadeJogador());
      }
      else
      {
         medidor = Mathf.Max(0f, medidor - descidaMedidor * dt);
      }
   }

   // o soldado ficou a saber onde estás (viu-te, levou um tiro, um colega ou o líder disse-lhe)
   public void RegistarPista(Vector3 posicao, Vector3 velocidade)
   {
      UltimaPosicao = posicao;
      velocidade.y = 0f;
      UltimaVelocidade = velocidade;
      MomentoContacto = Time.time;
   }

   // sobe o medidor de uma vez (levar um tiro, por exemplo). Nunca passa de 1
   public void SomarMedidor(float valor)
   {
      medidor = Mathf.Clamp01(medidor + valor);
   }

   // a posição e a velocidade reais do jogador (só para quando o soldado tem direito a sabê-las)
   public Vector3 PosicaoJogador => jogador != null ? jogador.position : transform.position;

   public Vector3 VelocidadeJogador()
   {
      if (ccJogador == null) return Vector3.zero;
      Vector3 v = ccJogador.velocity;
      v.y = 0f;
      return v;
   }

   // olhos do soldado: o osso da cabeça, ou os pés mais 1.6 m
   public Vector3 Olhos()
   {
      if (cabeca != null) return cabeca.position;
      // o NavMeshAgent põe o objeto baseOffset metros acima do chão
      float pes = transform.position.y;
      if (enemy != null && enemy.enemyAgent != null) pes -= enemy.enemyAgent.baseOffset;
      return new Vector3(transform.position.x, pes + 1.6f, transform.position.z);
   }

   // há linha de vista entre dois pontos? (paredes e lajes tapam; árvores, inimigos e o jogador não)
   public bool LinhaLivre(Vector3 de, Vector3 ate)
   {
      return !Physics.Linecast(de, ate, mascaraOclusao & ~(layerVegetacao >= 0 ? 1 << layerVegetacao : 0),
         QueryTriggerInteraction.Ignore);
   }

   // quanto sobe o medidor por segundo agora (0 = não te vê)
   private float CalcularSubida()
   {
      Vector3 olhos = Olhos();

      // cabeça, peito e ancas: seguem a altura da cápsula, por isso abaixado ficam mais baixos sozinhos
      float pes, alto;
      if (ccJogador != null)
      {
         pes = ccJogador.bounds.min.y;
         alto = ccJogador.bounds.max.y;
      }
      else
      {
         pes = jogador.position.y - 1f;
         alto = jogador.position.y + 1f;
      }
      float altura = alto - pes;
      Vector3 centro = new Vector3(jogador.position.x, 0f, jogador.position.z);
      pontosCorpo[0] = centro + Vector3.up * (alto - 0.15f);         // cabeça
      pontosCorpo[1] = centro + Vector3.up * (pes + altura * 0.7f);  // peito
      pontosCorpo[2] = centro + Vector3.up * (pes + altura * 0.45f); // ancas

      Vector3 paraJogador = pontosCorpo[1] - olhos;
      float distancia = paraJogador.magnitude;

      // 1) zona do cone e alcance (o mais barato primeiro: se falhar, nem lança raios)
      Vector3 frente = transform.forward; frente.y = 0f;
      Vector3 plano = paraJogador; plano.y = 0f;
      float angulo = plano.sqrMagnitude > 0.0001f ? Vector3.Angle(frente, plano) : 0f;

      bool escuro = enemy.noEscuro;
      bool naLanterna = false;
      if (escuro && enemy.lanterna != null && enemy.lanterna.enabled)
      {
         Vector3 dirLuz = enemy.lanterna.transform.forward;
         naLanterna = distancia <= alcanceLanterna && Vector3.Angle(dirLuz, paraJogador) <= enemy.lanterna.spotAngle * 0.5f;
      }

      float alcCentral = alcanceCentral, alcPeriferico = alcancePeriferico;
      if (escuro && !naLanterna)
      {
         alcCentral = enemy.raioVisaoEscuro;
         alcPeriferico = alcanceEscuroPeriferico;
      }

      float fatorZona, alcanceZona;
      bool aSentir = false;
      if (naLanterna)
      {
         fatorZona = 1f; alcanceZona = alcanceLanterna;
      }
      else if (angulo <= anguloCentral * 0.5f && distancia <= alcCentral)
      {
         fatorZona = 1f; alcanceZona = alcCentral;
      }
      else if (angulo <= anguloPeriferico * 0.5f && distancia <= alcPeriferico)
      {
         fatorZona = fatorPeriferico; alcanceZona = alcPeriferico;
      }
      else if (distancia <= distanciaSentir)
      {
         fatorZona = 1f; alcanceZona = distanciaSentir; aSentir = true;
      }
      else
      {
         for (int i = 0; i < 3; i++) parteVista[i] = false;
         return 0f;
      }

      // 2) linha de vista: quanto do corpo está à vista (0 a 3; uma árvore pelo meio vale metade)
      float vista = 0f;
      for (int i = 0; i < 3; i++)
      {
         float v = ValorRaio(olhos, pontosCorpo[i]);
         parteVista[i] = v > 0f;
         vista += v;
      }
      if (vista <= 0f) return 0f;

      float fatorPartes = vista >= 2.5f ? 1f : (vista >= 1.5f ? fatorDuasPartes : fatorUmaParte);

      // 3) os outros fatores
      // até à Distancia Perto não abranda (nem pelas costas, dentro do círculo de sentir); a partir daí desce até ao Fator Longe
      bool perto = aSentir || distancia <= distanciaPerto;
      float fatorDistancia = perto ? 1f
         : Mathf.Lerp(1f, fatorLonge, Mathf.InverseLerp(distanciaPerto, Mathf.Max(alcanceZona, distanciaPerto + 0.01f), distancia));

      float fatorPostura = (movimentoJogador != null && movimentoJogador.Abaixado) ? fatorAbaixado : 1f;

      float rapidez = VelocidadeJogador().magnitude;
      float fatorMovimento = 1f;
      if (rapidez < 0.3f) fatorMovimento = fatorParado;
      else if (movimentoJogador != null && movimentoJogador.Devagar) fatorMovimento = fatorDevagar;
      else if (movimentoJogador != null && rapidez > movimentoJogador.walkSpeed + 0.5f) fatorMovimento = fatorACorrer;

      // o escuro só te esconde a partir da Distancia Perto: à frente dele, a 2 m, vê-te mesmo sem luz
      float fatorLuz = (escuro && !naLanterna && !perto) ? fatorEscuro : 1f;
      float fatorZonaAlertaAgora = enemy.NaZonaAlerta ? fatorZonaAlerta : 1f;

      return subidaMedidor * fatorZona * fatorPartes * fatorDistancia * fatorPostura * fatorMovimento
             * fatorLuz * fatorZonaAlertaAgora;
   }

   // um raio dos olhos até uma parte do corpo: 1 = à vista, 0 = tapado, valorVegetacao por cada árvore pelo meio
   private float ValorRaio(Vector3 olhos, Vector3 alvo)
   {
      Vector3 dir = alvo - olhos;
      float dist = dir.magnitude;
      if (dist < 0.01f) return 1f;

      RaycastHit[] hits = Physics.RaycastAll(olhos, dir / dist, dist, mascaraOclusao, QueryTriggerInteraction.Ignore);
      float valor = 1f;
      foreach (RaycastHit hit in hits)
      {
         // o próprio corpo e o do jogador não tapam
         if (hit.transform.IsChildOf(transform)) continue;
         if (jogador != null && hit.transform.IsChildOf(jogador)) continue;
         // outro soldado (num objeto com Enemy, mesmo que a layer não seja Enemy) também não
         if (hit.collider.GetComponentInParent<Enemy>() != null) continue;

         if (hit.collider.gameObject.layer == layerVegetacao) { valor *= valorVegetacao; continue; }
         return 0f; // parede, laje, móvel: tapado
      }
      return valor;
   }

   // ---------- Debug ----------
   // com o F3 (ModoDebugVisual) e os Gizmos ligados, desenha os 3 raios: verde à vista, vermelho tapado
   private void OnDrawGizmos()
   {
      if (!Application.isPlaying || !ModoDebugVisual.Ativo || enemy == null || jogador == null) return;
      if (enemy.presentHealth <= 0f) return;
      Vector3 olhos = Olhos();
      if ((pontosCorpo[1] - olhos).sqrMagnitude > alcanceCentral * alcanceCentral) return;
      for (int i = 0; i < 3; i++)
      {
         Gizmos.color = parteVista[i] ? Color.green : new Color(1f, 0f, 0f, 0.4f);
         Gizmos.DrawLine(olhos, pontosCorpo[i]);
      }
   }

   // com o soldado selecionado: o cone central (amarelo) e o periférico (laranja) no chão
   private void OnDrawGizmosSelected()
   {
      Vector3 o = transform.position + Vector3.up * 0.1f;
      DesenharCone(o, anguloCentral, alcanceCentral, Color.yellow);
      DesenharCone(o, anguloPeriferico, alcancePeriferico, new Color(1f, 0.5f, 0f));
      Gizmos.color = Color.white;
      Gizmos.DrawWireSphere(o, distanciaSentir);
   }

   private void DesenharCone(Vector3 o, float angulo, float alcance, Color cor)
   {
      Gizmos.color = cor;
      Vector3 frente = transform.forward; frente.y = 0f;
      if (frente.sqrMagnitude < 0.0001f) frente = Vector3.forward;
      frente.Normalize();
      int passos = 12;
      Vector3 anterior = o + Quaternion.Euler(0f, -angulo * 0.5f, 0f) * frente * alcance;
      Gizmos.DrawLine(o, anterior);
      for (int i = 1; i <= passos; i++)
      {
         float a = -angulo * 0.5f + angulo * i / passos;
         Vector3 p = o + Quaternion.Euler(0f, a, 0f) * frente * alcance;
         Gizmos.DrawLine(anterior, p);
         anterior = p;
      }
      Gizmos.DrawLine(o, anterior);
   }
}
