using UnityEngine;

// Os passos e as quedas do jogador fazem barulho para os soldados (Ruido.cs).
//   A correr: ouvem-te a 8 m.   A andar: a 3 m.   Abaixado (Ctrl) ou devagar (Alt): não te ouvem.
//   Cair de um salto alto (o salto reforçado): ouvem-te a 6 m.
// Com uma parede pelo meio, metade. Noutro piso, um terço.
//
// Vai no Player (o objeto com o Character Controller e o PlayerMovement).
// Se faltar, o primeiro soldado a arrancar acrescenta-o sozinho.
// Com o F3 (ModoDebugVisual) e os Gizmos ligados, desenha cada barulho como uma esfera do tamanho do raio:
// azul passos, vermelho tiros, laranja avisos dos soldados, rosa gritos, roxo objetos e rádios.
public class RuidoPassosJogador : MonoBehaviour
{
   [Header("Raios (metros)")]
   public float raioCorrer = Ruido.RaioPassosCorrer;
   public float raioAndar = Ruido.RaioPassosAndar;
   [Tooltip("0 = abaixado não se ouve.")]
   public float raioAbaixado = 0f;
   [Tooltip("A andar devagar (Alt esquerdo). 0 = não se ouve. Para um meio-termo, 1 (só ouvem colados a ti).")]
   public float raioDevagar = 0f;
   public float raioAterrar = Ruido.RaioAterrar;

   [Header("Ritmo")]
   [Tooltip("Segundos entre dois barulhos de passos a correr.")]
   public float intervaloCorrer = 0.35f;
   [Tooltip("Segundos entre dois barulhos de passos a andar.")]
   public float intervaloAndar = 0.55f;
   [Tooltip("Segundos no ar a partir dos quais aterrar faz barulho (um salto normal fica abaixo disto).")]
   public float tempoNoArParaAterrar = 0.7f;

   private CharacterController cc;
   private PlayerMovement movimento;
   private float proximoPasso;
   private bool estavaNoChao = true;
   private float inicioNoAr;

   private void Awake()
   {
      cc = GetComponent<CharacterController>();
      movimento = GetComponent<PlayerMovement>();
   }

   private void Update()
   {
      if (cc == null) return;

      bool noChao = cc.isGrounded;

      // aterrar depois de muito tempo no ar
      if (!estavaNoChao && noChao && Time.time - inicioNoAr >= tempoNoArParaAterrar && raioAterrar > 0f)
         Ruido.Emitir(PontoDoBarulho(), raioAterrar, TipoRuido.Aterrar, this);
      if (estavaNoChao && !noChao) inicioNoAr = Time.time;
      estavaNoChao = noChao;

      if (!noChao) return;

      Vector3 v = cc.velocity;
      v.y = 0f;
      float rapidez = v.magnitude;
      if (rapidez < 0.5f) return;
      if (Time.time < proximoPasso) return;

      bool abaixado = movimento != null && movimento.Abaixado;
      bool devagar = movimento != null && movimento.Devagar;
      bool aCorrer = movimento != null ? rapidez > movimento.walkSpeed + 0.5f : rapidez > 6f;

      float raio;
      if (abaixado) raio = raioAbaixado;
      else if (devagar) raio = raioDevagar;
      else if (aCorrer) raio = raioCorrer;
      else raio = raioAndar;

      proximoPasso = Time.time + (aCorrer ? intervaloCorrer : intervaloAndar);
      if (raio > 0f)
         Ruido.Emitir(PontoDoBarulho(), raio, aCorrer ? TipoRuido.PassosCorrer : TipoRuido.PassosAndar, this);
   }

   // à altura das ancas, para o raio da parede não bater no chão
   private Vector3 PontoDoBarulho()
   {
      return transform.position + Vector3.up * 0.5f;
   }

   // os barulhos do último segundo, como esferas do tamanho do raio
   private void OnDrawGizmos()
   {
      if (!Application.isPlaying || !ModoDebugVisual.Ativo) return;
      foreach (EventoRuido r in Ruido.Recentes)
      {
         float idade = Time.time - r.momento;
         if (idade < 0f || idade > 1f) continue;
         Color c = Ruido.CorDebug(r.tipo);
         c.a = 1f - idade;
         Gizmos.color = c;
         Gizmos.DrawWireSphere(r.posicao, r.raio);
      }
   }
}
