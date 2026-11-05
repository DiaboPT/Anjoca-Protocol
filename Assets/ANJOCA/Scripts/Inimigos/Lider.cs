using UnityEngine;

// Regras especiais do líder inimigo (GDD): colete, tiro na cabeça a dobrar e reforços.
// Vai no mesmo objeto do Enemy. O resto (andar, disparar, animações, sons) é do Enemy.
[RequireComponent(typeof(Enemy))]
public class Lider : MonoBehaviour
{
   [Header("Colete")]
   // parte do dano que o colete corta nos tiros no tronco (0.25 = 25%).
   // o tronco é o collider com a tag Colete; braços, pernas e anca levam o dano todo
   [Range(0f, 1f)] public float reducaoColete = 0.25f;

   [Header("Cabeça")]
   // o tiro na cabeça não mata logo, como nos soldados: conta a dobrar (e o colete não protege)
   public float multiplicadorCabeca = 2f;

   [Header("Reforços")]
   // soldados que entram quando o líder leva o primeiro tiro.
   // ficam desligados na cena (sem o visto no topo do Inspector) até serem chamados
   public Enemy[] reforcos;
   // metros somados ao Vision Radius dos reforços, para virem direitos ao jogador.
   // quando te perdem de vista, voltam ao Vision Radius do Inspector
   public float bonusVisaoReforcos = 30f;
   // voz do líder a chamar os reforços (opcional)
   public AudioClip vozReforcos;

   private Enemy enemy;
   // true depois de chamar os reforços. Volta a false quando sais da visão do líder,
   // para os chamar outra vez se voltares e lhe acertares
   private bool reforcosChamados = false;
   // se no frame anterior o líder te via (para saber o momento em que sais da visão dele)
   private bool liderViaJogador = false;

   private void Awake()
   {
      enemy = GetComponent<Enemy>();
   }

   private void Update()
   {
      // acabaste de sair da visão do líder (no frame anterior via): pode voltar a chamar os reforços
      if (liderViaJogador && !enemy.playerInVisionRadius) reforcosChamados = false;
      liderViaJogador = enemy.playerInVisionRadius;
   }

   // chamado pelo Weapon do jogador em cada tiro que acerta no líder.
   // devolve o dano que o Enemy deve tirar, já com o colete ou a cabeça.
   // noColete: o tiro acertou no collider com a tag Colete (tronco)
   public float ReceberTiro(float dano, bool naCabeca, bool noColete)
   {
      // já morreu: não chama reforços nem conta dano
      if (enemy.presentHealth <= 0) return 0f;

      // só chama os reforços no primeiro tiro de cada vez que entras na visão do líder
      if (!reforcosChamados) ChamarReforcos();

      if (naCabeca) return dano * multiplicadorCabeca;
      if (noColete) return dano * (1f - reducaoColete);
      // braços, pernas e anca: o colete não protege
      return dano;
   }

   private void ChamarReforcos()
   {
      reforcosChamados = true;

      foreach (Enemy soldado in reforcos)
      {
         // o != null salta os que já foram apagados da cena
         if (soldado == null) continue;

         // liga o soldado: da primeira vez o Awake do Enemy corre agora; depois já está ligado e não faz nada
         soldado.gameObject.SetActive(true);
         // mais visão só até te perder de vista. É o Enemy que tira o bónus,
         // por isso funciona mesmo que o líder já tenha morrido
         soldado.BonusReforco(bonusVisaoReforcos);
      }

      if (vozReforcos != null && enemy.audioSource != null)
         enemy.audioSource.PlayOneShot(vozReforcos);
   }
}
