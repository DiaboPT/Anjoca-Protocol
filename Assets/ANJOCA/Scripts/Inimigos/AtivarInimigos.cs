using System.Collections;
using UnityEngine;

// Liga inimigos que estão desligados na cena quando acontece um evento:
// - ApanharCartao: o jogador apanha o cartão de acesso (o temCartao do InventarioPlayer);
// - Autodestruicao: o terminal de autodestruição é ativado (começa a fuga).
// Os inimigos da lista ficam desligados na cena (sem o visto no topo do Inspector) até ao evento.
// Este objeto tem de ficar LIGADO, senão o Update não corre.
public class AtivarInimigos : MonoBehaviour
{
   public enum Quando { ApanharCartao, Autodestruicao }

   [Header("Quando")]
   public Quando quando = Quando.ApanharCartao;
   [Tooltip("Segundos depois do evento até ligar o primeiro inimigo")]
   public float atraso = 0f;
   [Tooltip("Segundos entre cada inimigo, para não aparecerem todos ao mesmo tempo")]
   public float intervalo = 1.5f;

   [Header("Quem liga (desligados na cena)")]
   // soldados ou drones: liga o objeto inteiro
   public GameObject[] inimigos;

   [Header("Visão extra dos soldados")]
   [Tooltip("Metros somados ao Vision Radius dos soldados até te perderem de vista. 0 = só o do Inspector")]
   public float bonusVisao = 0f;

   private InventarioPlayer inventario;
   private TerminalAutodestruicao terminal;
   private bool feito = false;

   private void Start()
   {
      inventario = FindFirstObjectByType<InventarioPlayer>();
      terminal = FindFirstObjectByType<TerminalAutodestruicao>();

      if (quando == Quando.ApanharCartao && inventario == null)
         Debug.LogWarning(name + ": não encontrei o InventarioPlayer. Estes inimigos não vão aparecer");
      if (quando == Quando.Autodestruicao && terminal == null)
         Debug.LogWarning(name + ": não encontrei o TerminalAutodestruicao. Estes inimigos não vão aparecer");
   }

   private void Update()
   {
      if (feito) return;

      bool aconteceu;
      if (quando == Quando.ApanharCartao) aconteceu = inventario != null && inventario.temCartao;
      else aconteceu = terminal != null && terminal.Ativado;

      if (!aconteceu) return;

      // só uma vez
      feito = true;
      StartCoroutine(Ligar());
   }

   private IEnumerator Ligar()
   {
      if (atraso > 0f) yield return new WaitForSeconds(atraso);

      foreach (GameObject inimigo in inimigos)
      {
         // o != null salta os que já foram apagados da cena
         if (inimigo == null) continue;

         // liga o inimigo: o Awake dele corre agora e guarda este sítio como o posto dele
         inimigo.SetActive(true);

         // mais visão só até te perder de vista, como os reforços do líder
         if (bonusVisao > 0f)
         {
            Enemy soldado = inimigo.GetComponent<Enemy>();
            if (soldado != null) soldado.BonusReforco(bonusVisao);
         }

         if (intervalo > 0f) yield return new WaitForSeconds(intervalo);
      }
   }
}
