using UnityEngine;
using UnityEngine.InputSystem;

// Tipo de munição de cada arma.
// A Espingarda fica em primeiro (vale 0) de propósito: as caixas de munição que já estão na cena
// ficam com este tipo sozinhas e continuam a encher a reserva da M4, como sempre fizeram.
public enum TipoMunicao
{
   Espingarda,   // 5.56 da M4: as caixas que os soldados largam
   Glock         // 9 mm da Glock com silenciador
}

// Troca de arma do jogador: Q (ou a roda do rato) passa de uma arma para a outra.
// Vai no Player, o mesmo objeto que tem o Player Movement e o Debug HUD.
//
// Cada arma são DOIS objetos:
//  - o que tem o script Weapon (dentro da Main Camera, ao lado do Weapon da M4);
//  - o modelo que se vê na mão (o M4Colt ou a Glock, dentro dos braços).
// Este script liga os dois da arma escolhida e desliga os das outras.
//
// Quem pergunta por aqui: o DebugHUD (contador de balas) e o Pickup (caixas de munição).
public class TrocaArma : MonoBehaviour
{
   [System.Serializable]
   public class Arma
   {
      [Tooltip("Nome que aparece no HUD, antes das balas")]
      public string nome = "M4";

      [Tooltip("O objeto com o script Weapon (dentro da Main Camera)")]
      public Weapon weapon;

      [Tooltip("O modelo que se vê na mão, nos braços da câmara (o M4Colt ou a Glock). Vazio = não liga nem desliga nenhum modelo")]
      public GameObject modelo;

      [Tooltip("O mesmo modelo na mão do corpo da sombra (dentro do Player_Mixamo). Vazio = a sombra não muda de arma")]
      public GameObject modeloSombra;

      [Tooltip("As caixas de munição deste tipo enchem a reserva desta arma")]
      public TipoMunicao municao = TipoMunicao.Espingarda;

      [Tooltip("Estado parado desta arma no Animator dos braços (RifleAimingIdle na M4, PistolIdle na Glock). Vazio = não corta a animação de recarregar")]
      public string estadoParado = "RifleAimingIdle";
   }

   [Header("Armas (a primeira da lista é a que tens na mão ao começar)")]
   public Arma[] armas;

   [Header("Teclas")]
   [Tooltip("Tecla que passa à arma seguinte")]
   public Key teclaTrocar = Key.Q;
   [Tooltip("A roda do rato também troca de arma")]
   public bool usarRodaRato = true;

   [Header("Som (opcional)")]
   [Tooltip("Som de tirar a arma. Precisa de um Audio Source neste mesmo objeto")]
   public AudioClip somTrocar;

   [Header("Animação")]
   [Tooltip("Parâmetro Int nos dois Animators que diz qual é a arma na mão (0 = a primeira da lista). Vazio = não manda nada")]
   public string parametroArma = "Arma";
   [Tooltip("Nome do trigger de recarregar no Animator dos braços")]
   public string gatilhoRecarregar = "Recarregar";

   [Header("A meio de recarregar")]
   [Tooltip("Ligado: não deixa trocar de arma enquanto estás a recarregar. Desligado: troca e corta o recarregar")]
   public bool bloquearAoRecarregar = false;

   [Header("Estado (ver durante o Play)")]
   [Tooltip("Qual das armas da lista está na mão (0 = a primeira)")]
   [SerializeField] private int atual = 0;

   private AudioSource fonteSom;
   private Animator animBracos;   // Player_Bracos, dentro da Main Camera
   private Animator animCorpo;    // Player_Mixamo, o corpo que só se vê na sombra

   private void Awake()
   {
      fonteSom = GetComponent<AudioSource>();

      // o Animator dos braços (pose da arma e cortar o recarregar a meio)
      BracosFPS bracos = GetComponentInChildren<BracosFPS>(true);
      if (bracos != null) animBracos = bracos.GetComponent<Animator>();

      // o Animator do corpo que só se vê na sombra (pose do tronco)
      CorpoSombra corpo = GetComponentInChildren<CorpoSombra>(true);
      if (corpo != null) animCorpo = corpo.GetComponent<Animator>();
   }

   private void Start()
   {
      // põe na mão a arma escolhida no Inspector e desliga as outras
      Equipar(atual, false);
   }

   private void Update()
   {
      // com uma arma só não há nada para trocar
      if (armas == null || armas.Length < 2) return;

      Keyboard kb = Keyboard.current;
      if (kb != null && kb[teclaTrocar].wasPressedThisFrame) Seguinte(1);

      if (usarRodaRato && Mouse.current != null)
      {
         float roda = Mouse.current.scroll.ReadValue().y;
         if (roda > 0.1f) Seguinte(1);
         else if (roda < -0.1f) Seguinte(-1);
      }
   }

   // passa à arma seguinte (passo 1) ou à anterior (passo -1), saltando as linhas vazias da lista
   private void Seguinte(int passo)
   {
      // a recarregar não se troca de arma (só com o Bloquear Ao Recarregar ligado)
      if (bloquearAoRecarregar)
      {
         Weapon naMao = ArmaAtiva;
         if (naMao != null && naMao.ARecarregar) return;
      }

      int i = atual;
      for (int n = 0; n < armas.Length; n++)
      {
         i = (i + passo + armas.Length) % armas.Length;
         if (i == atual) continue;
         if (armas[i] != null && armas[i].weapon != null)
         {
            Equipar(i, true);
            return;
         }
      }
   }

   // põe na mão a arma da posição indicada
   public void Equipar(int indice, bool comSom)
   {
      if (armas == null || armas.Length == 0) return;
      if (indice < 0 || indice >= armas.Length) indice = 0;

      // PRIMEIRO desliga todas, só DEPOIS liga a escolhida.
      // Ao contrário não funcionava: as duas armas partilham a mesma Fire Action, e o OnDisable
      // da arma que sai desligava o botão de disparo da arma que entra.
      for (int i = 0; i < armas.Length; i++)
      {
         Arma a = armas[i];
         if (a == null) continue;
         if (a.weapon != null) a.weapon.gameObject.SetActive(false);
         if (a.modelo != null) a.modelo.SetActive(false);
         if (a.modeloSombra != null) a.modeloSombra.SetActive(false);
      }

      atual = indice;

      Arma nova = armas[atual];
      if (nova != null)
      {
         if (nova.weapon != null) nova.weapon.gameObject.SetActive(true);
         if (nova.modelo != null) nova.modelo.SetActive(true);
         if (nova.modeloSombra != null) nova.modeloSombra.SetActive(true);
      }

      // diz aos dois Animators qual é a arma na mão, para escolherem a pose (0 = a primeira da lista).
      // Os braços usam-no para o estado parado; o corpo da sombra, na camada Tronco
      if (parametroArma != "")
      {
         if (animBracos != null) animBracos.SetInteger(parametroArma, atual);
         if (animCorpo != null) animCorpo.SetInteger(parametroArma, atual);
      }

      // corta a animação de recarregar a meio e salta já para a pose desta arma.
      // Sem isto, trocavas de arma e os braços continuavam a acabar o recarregar da arma anterior,
      // com a arma nova a andar às voltas no ecrã
      if (animBracos != null && nova != null && nova.estadoParado != "")
      {
         if (gatilhoRecarregar != "") animBracos.ResetTrigger(gatilhoRecarregar);
         animBracos.Play(nova.estadoParado, 0, 0f);
      }

      if (comSom && fonteSom != null && somTrocar != null) fonteSom.PlayOneShot(somTrocar);
   }

   // a arma que tens na mão. Pode ser null se a lista estiver vazia ou mal preenchida
   public Weapon ArmaAtiva
   {
      get
      {
         if (armas == null || atual < 0 || atual >= armas.Length) return null;
         if (armas[atual] == null) return null;
         return armas[atual].weapon;
      }
   }

   // nome da arma que tens na mão, para o HUD ("M4", "PISTOLA")
   public string NomeArmaAtiva
   {
      get
      {
         if (armas == null || atual < 0 || atual >= armas.Length) return "";
         if (armas[atual] == null) return "";
         return armas[atual].nome;
      }
   }

   // a arma a que pertence uma caixa de munição deste tipo.
   // Se nenhuma arma usar este tipo, devolve a que tens na mão
   public Weapon ArmaDaMunicao(TipoMunicao tipo)
   {
      if (armas != null)
      {
         for (int i = 0; i < armas.Length; i++)
         {
            if (armas[i] != null && armas[i].weapon != null && armas[i].municao == tipo)
               return armas[i].weapon;
         }
      }
      return ArmaAtiva;
   }
}
