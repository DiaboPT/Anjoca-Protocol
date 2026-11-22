using System.Collections.Generic;
using UnityEngine;

// Esconderijo marcado à mão para a procura dos soldados (Enemy, estado Procurar).
// Os soldados já encontram esconderijos sozinhos, à volta do sítio onde te perderam.
// Usa isto só onde eles falham: atrás de um balcão, ao fundo de uma sala da cave, num vão de escada.
//
// Pôr num GameObject vazio, em cima do chão (do NavMesh), onde o jogador se poderia esconder.
// Na Scene aparece como uma esfera azul-clara.
public class PontoProcura : MonoBehaviour
{
   [Tooltip("Pontos a mais em relação aos esconderijos automáticos. Mais alto = é escolhido mais vezes.")]
   public float pontosExtra = 2f;

   // todos os pontos ligados na cena (o Enemy percorre esta lista)
   public static readonly List<PontoProcura> Todos = new List<PontoProcura>();

   private void OnEnable()
   {
      if (!Todos.Contains(this)) Todos.Add(this);
   }

   private void OnDisable()
   {
      Todos.Remove(this);
   }

   private void OnDrawGizmos()
   {
      Gizmos.color = new Color(0.3f, 0.8f, 1f);
      Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.4f, 0.4f);
   }
}
