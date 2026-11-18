using UnityEngine;

// HUD da barra de deteção da trincheira (Parte 4). Pôr no Player, ao lado do DebugHUD.
// Usa OnGUI, como o DebugHUD: não precisa de Canvas.
// Só aparece quando alguma zona (DetecaoZona) tem a barra acima de 0 ou está em alarme.
// Mostra a zona com a barra mais cheia:
//  - barra no canto inferior esquerdo, de amarelo (vazia) a vermelho (cheia), com a percentagem
//    e os segundos que faltam para encher;
//  - "ALERTA: SNIPER - ABAIXA-TE!" a piscar por cima da barra, enquanto um sniper te vê;
//  - "ALARME GERAL" por cima da barra quando ela enche, e por cima dele quem te está a ver
//    ou quanto falta para o alarme acabar, e "REFORÇOS A CAMINHO" quando os reforços são chamados;
//  - DE PÉ / ABAIXADO à direita da barra (opcional).
public class HUDDetecao : MonoBehaviour
{
   [Header("Posição (canto inferior esquerdo)")]
   // píxeis desde a borda esquerda do ecrã
   public float margemEsquerda = 20f;
   // píxeis desde a borda de baixo do ecrã
   public float margemBaixo = 20f;
   public float largura = 320f;
   public float altura = 24f;

   [Header("Cores")]
   // cor da barra quase vazia
   public Color corInicio = new Color(1f, 0.8f, 0.2f);
   // cor da barra cheia e do alarme
   public Color corCheia = new Color(0.9f, 0.1f, 0.1f);

   [Header("Postura")]
   // mostra DE PÉ ou ABAIXADO ao lado da barra
   public bool mostrarPostura = true;

   private PlayerMovement movimento;
   private GUIStyle estiloBarra;
   private GUIStyle estiloAviso;
   private GUIStyle estiloAlarme;
   private GUIStyle estiloPequeno;
   private GUIStyle estiloPostura;

   private void Awake()
   {
      movimento = GetComponent<PlayerMovement>();
      if (movimento == null) movimento = FindFirstObjectByType<PlayerMovement>();
   }

   private void OnGUI()
   {
      DetecaoZona zona = DetecaoZona.MaisAlta();
      if (zona == null) return;

      CriarEstilos();

      // a barra fica encostada ao canto de baixo, à esquerda; os avisos ficam por cima dela
      float x = margemEsquerda;
      float y = Screen.height - margemBaixo - altura;

      // fundo preto
      GUI.color = Color.black;
      GUI.DrawTexture(new Rect(x, y, largura, altura), Texture2D.whiteTexture);

      // parte cheia: amarelo a vermelho, conforme enche
      GUI.color = zona.alarme ? corCheia : Color.Lerp(corInicio, corCheia, zona.barra);
      GUI.DrawTexture(new Rect(x + 2, y + 2, (largura - 4) * Mathf.Clamp01(zona.barra), altura - 4), Texture2D.whiteTexture);
      GUI.color = Color.white;

      // texto dentro da barra: DETEÇÃO 65%   0.7s
      string texto = "DETEÇÃO " + (zona.barra * 100f).ToString("0") + "%";
      if (zona.vistoAgora && !zona.alarme) texto += "   " + zona.SegundosParaEncher.ToString("0.0") + "s";
      Texto(new Rect(x, y, largura, altura), texto, estiloBarra, Color.white);

      // DE PÉ / ABAIXADO à direita da barra
      if (mostrarPostura && movimento != null)
      {
         bool abaixado = movimento.Abaixado;
         Texto(new Rect(x + largura + 10, y, 120, altura), abaixado ? "ABAIXADO" : "DE PÉ", estiloPostura,
            abaixado ? new Color(0.4f, 1f, 0.4f) : corInicio);
      }

      // avisos por cima da barra (o texto assenta no fundo do retângulo, junto à barra)
      Rect linhaAviso = new Rect(x, y - 46, 600, 42);

      if (zona.alarme)
      {
         Texto(linhaAviso, "ALARME GERAL", estiloAlarme, corCheia);
         Rect linhaPequena = new Rect(x, y - 46 - 22, 600, 22);
         // um sniper ou um soldado da zona ainda te vê: o alarme não começa a acabar
         if (zona.vistoAgora || zona.soldadoVe)
            Texto(linhaPequena, zona.vistoAgora ? "o sniper vê-te: abaixa-te!" : "os soldados estão a ver-te",
               estiloPequeno, new Color(1f, 0.3f, 0.3f));
         else
            Texto(linhaPequena, "acaba em " + zona.SegundosParaFimAlarme.ToString("0") + " s se ninguém te vir",
               estiloPequeno, Color.white);

         // durante 4 s depois de os chamar: quantos reforços vêm a caminho
         if (zona.ultimosReforcos > 0 && Time.time - zona.reforcosChamadosEm < 4f)
            Texto(new Rect(x, y - 46 - 44, 600, 22), "REFORÇOS A CAMINHO (" + zona.ultimosReforcos + ")",
               estiloPequeno, corInicio);
      }
      else if (zona.vistoAgora)
      {
         // pisca: 0.4 s aceso, 0.2 s apagado
         if (Mathf.Repeat(Time.time, 0.6f) < 0.4f)
            Texto(linhaAviso, "ALERTA: SNIPER - ABAIXA-TE!", estiloAviso, new Color(1f, 0.3f, 0.3f));
      }
   }

   // texto com uma sombra preta por baixo, para se ler em cima da barra amarela e do cenário
   private void Texto(Rect rect, string texto, GUIStyle estilo, Color cor)
   {
      GUI.color = new Color(0f, 0f, 0f, 0.85f);
      GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), texto, estilo);
      GUI.color = cor;
      GUI.Label(rect, texto, estilo);
      GUI.color = Color.white;
   }

   private void CriarEstilos()
   {
      if (estiloBarra != null) return;

      estiloBarra = Estilo(14, TextAnchor.MiddleCenter);
      estiloAviso = Estilo(20, TextAnchor.LowerLeft);
      estiloAlarme = Estilo(30, TextAnchor.LowerLeft);
      estiloPequeno = Estilo(13, TextAnchor.LowerLeft);
      estiloPostura = Estilo(14, TextAnchor.MiddleLeft);
   }

   private GUIStyle Estilo(int tamanho, TextAnchor alinhamento)
   {
      GUIStyle estilo = new GUIStyle(GUI.skin.label)
      {
         fontSize = tamanho,
         fontStyle = FontStyle.Bold,
         alignment = alinhamento
      };
      // texto branco: a cor vem do GUI.color em Texto()
      estilo.normal.textColor = Color.white;
      return estilo;
   }
}
