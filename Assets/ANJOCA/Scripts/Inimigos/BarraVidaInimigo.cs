using UnityEngine;
using UnityEngine.UI;

// Barra de vida por cima da cabeça do soldado.
// Só aparece com o debug visual ligado (ModoDebugVisual.BarrasVida; o F3 troca durante o Play).
// Vai no mesmo objeto do Enemy. É desenhada no ecrã (num Canvas próprio, criado pelo script),
// por isso não depende das luzes nem da exposição do HDRP.
public class BarraVidaInimigo : MonoBehaviour
{
   [Header("Posição e tamanho")]
   // metros acima do centro do soldado (o centro da cápsula fica a 1 m do chão)
   public float altura = 1.1f;
   // tamanho da barra em píxeis, com o soldado a 10 m (mais longe fica mais pequena)
   public Vector2 tamanho = new Vector2(80f, 8f);
   // a partir desta distância a barra deixa de aparecer
   public float distanciaMaxima = 60f;

   [Header("Cores")]
   public Color corCheia = new Color(0.2f, 0.85f, 0.2f);
   public Color corVazia = new Color(0.9f, 0.15f, 0.1f);
   public Color corFundo = new Color(0f, 0f, 0f, 0.6f);

   // um só Canvas para as barras de todos os soldados
   private static Canvas canvasBarras;

   private Enemy soldado;
   // só um dos dois existe neste objeto (soldado ou drone)
   private EnemyDrone drone;
   private Camera cam;
   private RectTransform fundo;
   private RectTransform enchimento;
   private Image imagemEnchimento;

   private void Start()
   {
      soldado = GetComponent<Enemy>();
      drone = GetComponent<EnemyDrone>();
      if (soldado == null && drone == null)
      {
         Debug.LogWarning(name + ": a Barra Vida Inimigo tem de estar no mesmo objeto do Enemy ou do EnemyDrone.");
         enabled = false;
         return;
      }
      CriarBarra();
   }

   // vida de 0 a 1, do soldado ou do drone
   private float Vida()
   {
      if (soldado != null) return Mathf.Clamp01(soldado.presentHealth / soldado.enemyHealth);
      return Mathf.Clamp01(drone.presentHealth / drone.enemyHealth);
   }

   private void CriarBarra()
   {
      if (canvasBarras == null)
      {
         GameObject goCanvas = new GameObject("Canvas_BarrasVida");
         canvasBarras = goCanvas.AddComponent<Canvas>();
         canvasBarras.renderMode = RenderMode.ScreenSpaceOverlay;
         // por baixo do HUD do jogador
         canvasBarras.sortingOrder = -10;
      }

      // fundo escuro
      GameObject goFundo = new GameObject("BarraVida_" + name, typeof(RectTransform), typeof(Image));
      goFundo.transform.SetParent(canvasBarras.transform, false);
      fundo = goFundo.GetComponent<RectTransform>();
      Image imagemFundo = goFundo.GetComponent<Image>();
      imagemFundo.color = corFundo;
      imagemFundo.raycastTarget = false;

      // parte colorida, que encolhe da direita para a esquerda
      GameObject goEnchimento = new GameObject("Enchimento", typeof(RectTransform), typeof(Image));
      goEnchimento.transform.SetParent(goFundo.transform, false);
      enchimento = goEnchimento.GetComponent<RectTransform>();
      enchimento.anchorMin = Vector2.zero;
      enchimento.anchorMax = Vector2.one;
      // 1 píxel de margem à volta
      enchimento.offsetMin = new Vector2(1f, 1f);
      enchimento.offsetMax = new Vector2(-1f, -1f);
      imagemEnchimento = goEnchimento.GetComponent<Image>();
      imagemEnchimento.raycastTarget = false;

      fundo.gameObject.SetActive(false);
   }

   private void LateUpdate()
   {
      if (fundo == null) return;
      if (cam == null) cam = Camera.main;

      float vida = Vida();
      bool mostrar = ModoDebugVisual.BarrasVida && vida > 0f && cam != null;

      Vector3 ecra = Vector3.zero;
      if (mostrar)
      {
         ecra = cam.WorldToScreenPoint(transform.position + Vector3.up * altura);
         // atrás da câmara ou longe demais: não mostra
         if (ecra.z <= 0f || ecra.z > distanciaMaxima) mostrar = false;
      }

      if (fundo.gameObject.activeSelf != mostrar) fundo.gameObject.SetActive(mostrar);
      if (!mostrar) return;

      // tamanho normal a 10 m; mais perto cresce um pouco, mais longe encolhe
      float escala = Mathf.Clamp(10f / ecra.z, 0.4f, 1.5f);
      fundo.position = new Vector3(ecra.x, ecra.y, 0f);
      fundo.sizeDelta = tamanho * escala;

      // a largura da parte colorida acompanha a vida, e a cor passa de verde a vermelho
      enchimento.anchorMax = new Vector2(vida, 1f);
      imagemEnchimento.color = Color.Lerp(corVazia, corCheia, vida);
   }

   private void OnDestroy()
   {
      // o soldado é destruído uns segundos depois de morrer: leva a barra com ele
      if (fundo != null) Destroy(fundo.gameObject);
   }
}
