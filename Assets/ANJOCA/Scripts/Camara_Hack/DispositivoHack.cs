using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Dispositivo móvel do jogador para hackear as câmaras hack da base e ver por elas.
// [T] abre e fecha o dispositivo (a arma fica em baixo enquanto está aberto).
// Com ele aberto, perto de uma câmara (no mesmo piso, mesmo do outro lado de uma parede),
// mantém o botão esquerdo para a hackear. Depois vês por ela de qualquer sítio da base.
// [Tab] ou a roda do rato trocam entre as câmaras já hackeadas. O Q fica para a troca de arma.
// (o TrocaArma fica desligado enquanto o dispositivo está aberto, para a roda do rato não trocar de arma ao mesmo tempo).
// O ecrã do dispositivo é criado por este script quando o jogo começa: não é preciso montar nada no Canvas.
// Vai no Player (o objeto com o PlayerStats e o Character Controller).
public class DispositivoHack : MonoBehaviour
{
   [Header("Teclas")]
   public Key teclaAbrir = Key.T;
   public Key teclaSeguinte = Key.Tab;

   [Header("Hackear")]
   // metros até à câmara (contados no chão, as paredes não cortam o sinal)
   public float alcance = 12f;
   // segundos com o botão esquerdo carregado
   public float tempoHackear = 2f;
   // energia do fato gasta em cada câmara hackeada (0 = de graça)
   public float energiaHack = 0f;
   // diferença de altura entre o chão da câmara e os teus pés que já conta como outro piso
   public float alturaOutroPiso = 2.5f;

   [Header("Ecrã")]
   // tamanho da imagem no ecrã (num ecrã de 1920 x 1080)
   public Vector2 tamanhoImagem = new Vector2(720f, 405f);
   // canto do ecrã onde fica o dispositivo: (0.5, 0) = em baixo ao centro, (0, 0) = em baixo à esquerda, (1, 0) = em baixo à direita
   public Vector2 cantoEcra = new Vector2(0.5f, 0f);
   // distância a esse canto, em píxeis (num ecrã de 1920 x 1080)
   public Vector2 margemEcra = new Vector2(30f, 30f);
   // resolução da imagem da câmara (16:9)
   public Vector2Int resolucao = new Vector2Int(960, 540);
   // cor por cima da imagem (um verde claro dá o ar de câmara de vigilância; branco = cores normais)
   public Color tintaImagem = new Color(0.8f, 1f, 0.85f);

   [Header("Referências (vazias = procura sozinho)")]
   // câmara do jogador (a Main Camera): as câmaras hack copiam as camadas e os Volumes dela
   public Camera camaraJogador;
   public PlayerStats stats;
   // objetos a esconder enquanto o dispositivo está aberto (por exemplo a mira). Opcional
   public GameObject[] esconderAoAbrir;
   // scripts a desligar enquanto o dispositivo está aberto. O TrocaArma (se existir no Player)
   // entra sozinho: assim o Q e a roda do rato trocam de câmara e não de arma
   public Behaviour[] desligarAoAbrir;

   [Header("Sons (opcionais)")]
   public AudioClip somAbrir;      // Dispositivo_Abrir
   public AudioClip somFechar;     // Dispositivo_Fechar
   public AudioClip somHackeado;   // Dispositivo_Hackeado
   public AudioClip somNegado;     // Dispositivo_Negado
   public AudioClip somTrocar;     // Dispositivo_Trocar
   public AudioClip somAHackear;   // Dispositivo_AHackear: toca em loop enquanto manténs o botão esquerdo

   [Header("Teste")]
   // ligado = todas as câmaras ficam hackeadas logo no início (para testar as imagens)
   public bool todasHackeadas = false;

   // estado que os outros scripts podem ler
   public bool aberto { get; private set; }

   private readonly List<CamaraHack> camaras = new List<CamaraHack>();
   private readonly List<CamaraHack> hackeadas = new List<CamaraHack>();
   private int atual = -1;
   private CamaraHack aMostrar;

   private CamaraHack alvo;
   private float progresso = 0f;
   private string aviso = "";
   private float avisoAte = 0f;

   private CharacterController cc;
   private Weapon[] armas;
   private bool[] armasLigadas;
   private readonly List<Behaviour> scriptsDesligar = new List<Behaviour>();
   private readonly List<bool> scriptsLigados = new List<bool>();
   private AudioSource audioSource;
   private AudioSource audioLoop;
   private RenderTexture textura;

   // ecrã
   private GameObject painel;
   private RawImage imagem;
   private TextMeshProUGUI textoCamara, textoRec, textoCentro, textoEstado;
   private RectTransform barra;

   private void Start()
   {
      if (camaraJogador == null) camaraJogador = Camera.main;
      if (stats == null) stats = GetComponent<PlayerStats>();
      cc = GetComponent<CharacterController>();
      armas = GetComponentsInChildren<Weapon>(true);
      armasLigadas = new bool[armas.Length];

      // scripts a desligar: os do Inspector e o TrocaArma do Player, se houver
      // (procurado pelo nome, para este script compilar mesmo num projeto sem o TrocaArma)
      if (desligarAoAbrir != null)
         foreach (Behaviour b in desligarAoAbrir)
            if (b != null && !scriptsDesligar.Contains(b)) scriptsDesligar.Add(b);
      Behaviour trocaArma = GetComponent("TrocaArma") as Behaviour;
      if (trocaArma != null && !scriptsDesligar.Contains(trocaArma)) scriptsDesligar.Add(trocaArma);
      foreach (Behaviour b in scriptsDesligar) scriptsLigados.Add(b.enabled);

      audioSource = gameObject.AddComponent<AudioSource>();
      audioSource.playOnAwake = false;
      audioSource.spatialBlend = 0f;
      audioLoop = gameObject.AddComponent<AudioSource>();
      audioLoop.playOnAwake = false;
      audioLoop.spatialBlend = 0f;
      audioLoop.loop = true;
      audioLoop.clip = somAHackear;

      camaras.AddRange(FindObjectsByType<CamaraHack>(FindObjectsInactive.Include, FindObjectsSortMode.None));
      camaras.Sort((a, b) => string.Compare(a.Nome, b.Nome, System.StringComparison.Ordinal));
      if (camaras.Count == 0) Debug.LogWarning(name + ": não há nenhuma CamaraHack na cena");

      if (todasHackeadas)
         foreach (CamaraHack c in camaras) { c.Hackear(); hackeadas.Add(c); }

      textura = new RenderTexture(resolucao.x, resolucao.y, 24);
      textura.name = "Dispositivo_Ecra";

      CriarEcra();
      painel.SetActive(false);
   }

   private void Update()
   {
      // morto: fecha e não faz mais nada
      if (stats != null && stats.isDead)
      {
         if (aberto) Fechar();
         return;
      }

      Keyboard kb = Keyboard.current;
      Mouse rato = Mouse.current;
      if (kb == null) return;

      if (kb[teclaAbrir].wasPressedThisFrame)
      {
         if (aberto) Fechar(); else Abrir();
      }
      if (!aberto) return;

      // câmaras apagadas da cena (destruídas) saem das listas
      hackeadas.RemoveAll(c => c == null);
      camaras.RemoveAll(c => c == null);

      // trocar de câmara
      float roda = rato != null ? rato.scroll.ReadValue().y : 0f;
      if (kb[teclaSeguinte].wasPressedThisFrame || roda < 0f) Trocar(1);
      else if (roda > 0f) Trocar(-1);

      Hackear(rato != null && rato.leftButton.isPressed, rato != null && rato.leftButton.wasPressedThisFrame);
      SomAHackear(progresso > 0f);

      MostrarCamaraAtual();
      AtualizarEcra();
   }

   // ---------- Abrir e fechar ----------

   private void Abrir()
   {
      aberto = true;
      painel.SetActive(true);
      Tocar(somAbrir);

      // a arma fica em baixo: sem disparar nem recarregar
      for (int i = 0; i < armas.Length; i++)
      {
         armasLigadas[i] = armas[i].enabled;
         armas[i].enabled = false;
      }
      foreach (GameObject obj in esconderAoAbrir)
         if (obj != null) obj.SetActive(false);
      for (int i = 0; i < scriptsDesligar.Count; i++)
      {
         if (scriptsDesligar[i] == null) continue;
         scriptsLigados[i] = scriptsDesligar[i].enabled;
         scriptsDesligar[i].enabled = false;
      }

      if (atual < 0 && hackeadas.Count > 0) atual = 0;
      progresso = 0f;
   }

   private void Fechar()
   {
      aberto = false;
      painel.SetActive(false);
      Tocar(somFechar);

      for (int i = 0; i < armas.Length; i++)
         if (armas[i] != null) armas[i].enabled = armasLigadas[i];
      foreach (GameObject obj in esconderAoAbrir)
         if (obj != null) obj.SetActive(true);
      for (int i = 0; i < scriptsDesligar.Count; i++)
         if (scriptsDesligar[i] != null) scriptsDesligar[i].enabled = scriptsLigados[i];

      // a câmara que estava no ecrã deixa de desenhar
      if (aMostrar != null) aMostrar.Mostrar(null, camaraJogador);
      aMostrar = null;
      progresso = 0f;
      alvo = null;
      SomAHackear(false);
   }

   // liga e desliga o som em loop enquanto a barra está a encher
   private void SomAHackear(bool tocar)
   {
      if (audioLoop == null || audioLoop.clip == null) return;
      if (tocar && !audioLoop.isPlaying) audioLoop.Play();
      else if (!tocar && audioLoop.isPlaying) audioLoop.Stop();
   }

   private void Trocar(int passo)
   {
      // sem outra câmara para onde trocar
      if (hackeadas.Count < 2)
      {
         Avisar(hackeadas.Count == 0 ? "Ainda não hackeaste nenhuma câmara" : "Só tens uma câmara hackeada");
         Tocar(somNegado);
         return;
      }
      atual = (atual + passo + hackeadas.Count) % hackeadas.Count;
      Tocar(somTrocar);
   }

   // ---------- Hackear ----------

   private void Hackear(bool carregado, bool acabouDeCarregar)
   {
      CamaraHack maisPerto = CamaraParaHackear();

      // mudou de câmara (afastaste-te ou entraste no alcance de outra): recomeça
      if (maisPerto != alvo) progresso = 0f;
      alvo = maisPerto;

      if (alvo == null || !carregado)
      {
         // clicaste sem nenhuma câmara por hackear ao alcance
         if (alvo == null && acabouDeCarregar)
         {
            Avisar("Sem sinal de nenhuma câmara por hackear");
            Tocar(somNegado);
         }
         progresso = 0f;
         return;
      }

      // não tem energia que chegue: avisa uma vez por clique
      if (stats != null && energiaHack > 0f && stats.energy < energiaHack)
      {
         progresso = 0f;
         if (acabouDeCarregar)
         {
            Avisar("Energia insuficiente: precisas de " + energiaHack.ToString("0"));
            Tocar(somNegado);
         }
         return;
      }

      progresso += Time.deltaTime / Mathf.Max(0.01f, tempoHackear);
      if (progresso < 1f) return;

      // acabou
      if (stats != null && energiaHack > 0f) stats.DrainEnergy(energiaHack);
      alvo.Hackear();
      hackeadas.Add(alvo);
      atual = hackeadas.Count - 1;
      Avisar("Ligado a " + alvo.Nome);
      Tocar(somHackeado);

      progresso = 0f;
      alvo = null;
   }

   // a câmara ainda não hackeada mais perto, dentro do alcance e no mesmo piso
   private CamaraHack CamaraParaHackear()
   {
      float pes = cc != null ? cc.bounds.min.y : transform.position.y;
      CamaraHack melhor = null;
      float melhorDistancia = alcance;

      foreach (CamaraHack c in camaras)
      {
         if (c.hackeada || c.semSinal || !c.isActiveAndEnabled) continue;
         if (Mathf.Abs(c.chaoY - pes) >= alturaOutroPiso) continue;

         Vector3 d = c.transform.position - transform.position;
         d.y = 0f;
         float distancia = d.magnitude;
         if (distancia <= melhorDistancia)
         {
            melhor = c;
            melhorDistancia = distancia;
         }
      }
      return melhor;
   }

   // ---------- Ecrã ----------

   // só a câmara escolhida desenha, e só com o dispositivo aberto
   private void MostrarCamaraAtual()
   {
      CamaraHack escolhida = (atual >= 0 && atual < hackeadas.Count) ? hackeadas[atual] : null;
      if (escolhida == aMostrar) return;

      if (aMostrar != null) aMostrar.Mostrar(null, camaraJogador);
      aMostrar = escolhida;
      if (aMostrar != null) aMostrar.Mostrar(textura, camaraJogador);
   }

   private void AtualizarEcra()
   {
      bool temImagem = aMostrar != null && !aMostrar.semSinal;

      // a câmara pode ter ficado sem energia enquanto a vês: volta a pedir a imagem para a desligar
      if (aMostrar != null) aMostrar.Mostrar(textura, camaraJogador);

      imagem.texture = temImagem ? textura : null;
      imagem.color = temImagem ? tintaImagem : new Color(0.02f, 0.03f, 0.03f);

      if (aMostrar == null)
      {
         textoCamara.text = "";
         textoRec.text = "";
         textoCentro.text = "SEM CÂMARAS";
      }
      else
      {
         textoCamara.text = "CAM " + (atual + 1) + "/" + hackeadas.Count + "  " + aMostrar.Nome.ToUpper();
         // o REC vermelho pisca uma vez por segundo
         string rec = (Time.time % 1f) < 0.5f ? "<color=#FF3030>REC</color>" : "<color=#00000000>REC</color>";
         textoRec.text = temImagem ? rec + "  " + System.DateTime.Now.ToString("HH:mm:ss") : "";
         textoCentro.text = temImagem ? "" : "SEM SINAL";
      }

      // linha de cima: o que se está a passar
      string linha;
      if (Time.time < avisoAte) linha = aviso;
      else if (alvo != null && progresso > 0f) linha = "A hackear " + alvo.Nome + "...  " + Mathf.FloorToInt(progresso * 100f) + " %";
      else if (alvo != null)
      {
         float metros = Vector3.Distance(new Vector3(alvo.transform.position.x, 0f, alvo.transform.position.z),
            new Vector3(transform.position.x, 0f, transform.position.z));
         linha = "Sinal: " + alvo.Nome + " a " + metros.ToString("0") + " m.  Mantém o botão esquerdo para hackear"
            + (energiaHack > 0f ? " (" + energiaHack.ToString("0") + " de energia)" : "");
      }
      else linha = "Nenhuma câmara por hackear por perto (até " + alcance.ToString("0") + " m, no mesmo piso)";

      // linha de baixo: as teclas
      string teclas = "[" + teclaAbrir + "] Fechar";
      if (hackeadas.Count > 1) teclas += "    [" + teclaSeguinte + " / roda] Câmara seguinte";

      textoEstado.text = linha + "\n<color=#8FA89A>" + teclas + "</color>";
      barra.anchorMax = new Vector2(Mathf.Clamp01(progresso), 1f);
   }

   private void Avisar(string texto)
   {
      aviso = texto;
      avisoAte = Time.time + 2f;
   }

   private void Tocar(AudioClip som)
   {
      if (som != null && audioSource != null) audioSource.PlayOneShot(som);
   }

   // cria o Canvas do dispositivo: moldura escura em baixo ao centro, imagem, textos e barra de progresso
   private void CriarEcra()
   {
      GameObject raiz = new GameObject("Dispositivo_Ecra");
      raiz.transform.SetParent(transform, false);
      Canvas canvas = raiz.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 20;
      CanvasScaler escala = raiz.AddComponent<CanvasScaler>();
      escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      escala.referenceResolution = new Vector2(1920f, 1080f);
      escala.matchWidthOrHeight = 0.5f;

      // moldura
      painel = new GameObject("Moldura", typeof(RectTransform), typeof(Image));
      painel.transform.SetParent(raiz.transform, false);
      RectTransform rp = painel.GetComponent<RectTransform>();
      // ancorado ao canto escolhido (em baixo ao centro por defeito; o DebugHUD esconde as balas enquanto está aberto)
      rp.anchorMin = rp.anchorMax = cantoEcra;
      rp.pivot = cantoEcra;
      rp.anchoredPosition = new Vector2(margemEcra.x * (1f - 2f * cantoEcra.x), margemEcra.y * (1f - 2f * cantoEcra.y));
      rp.sizeDelta = tamanhoImagem + new Vector2(40f, 120f);
      painel.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.08f, 0.95f);

      // imagem da câmara
      GameObject img = new GameObject("Imagem", typeof(RectTransform), typeof(RawImage));
      img.transform.SetParent(painel.transform, false);
      RectTransform ri = img.GetComponent<RectTransform>();
      ri.anchorMin = Vector2.zero;
      ri.anchorMax = Vector2.one;
      ri.offsetMin = new Vector2(20f, 100f);
      ri.offsetMax = new Vector2(-20f, -20f);
      imagem = img.GetComponent<RawImage>();

      textoCamara = CriarTexto("Camara", img.transform, new Vector2(0f, 1f), new Vector2(14f, -10f), 24f, TextAlignmentOptions.TopLeft);
      textoRec = CriarTexto("Rec", img.transform, new Vector2(1f, 1f), new Vector2(-14f, -10f), 24f, TextAlignmentOptions.TopRight);
      textoCentro = CriarTexto("Centro", img.transform, new Vector2(0.5f, 0.5f), Vector2.zero, 44f, TextAlignmentOptions.Center);

      // barra de progresso (por baixo da imagem)
      GameObject fundo = new GameObject("Barra_Fundo", typeof(RectTransform), typeof(Image));
      fundo.transform.SetParent(painel.transform, false);
      RectTransform rf = fundo.GetComponent<RectTransform>();
      rf.anchorMin = new Vector2(0f, 0f);
      rf.anchorMax = new Vector2(1f, 0f);
      rf.offsetMin = new Vector2(20f, 86f);
      rf.offsetMax = new Vector2(-20f, 92f);
      fundo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

      GameObject cheio = new GameObject("Barra", typeof(RectTransform), typeof(Image));
      cheio.transform.SetParent(fundo.transform, false);
      barra = cheio.GetComponent<RectTransform>();
      barra.anchorMin = Vector2.zero;
      barra.anchorMax = new Vector2(0f, 1f);
      barra.offsetMin = barra.offsetMax = Vector2.zero;
      cheio.GetComponent<Image>().color = new Color(0.45f, 1f, 0.6f);

      // estado e teclas (em baixo)
      textoEstado = CriarTexto("Estado", painel.transform, new Vector2(0f, 0f), new Vector2(20f, 12f), 20f, TextAlignmentOptions.BottomLeft);
      textoEstado.rectTransform.pivot = new Vector2(0f, 0f);
      textoEstado.rectTransform.sizeDelta = new Vector2(tamanhoImagem.x, 66f);
      textoEstado.color = new Color(0.75f, 1f, 0.8f);
   }

   private TextMeshProUGUI CriarTexto(string nomeObjeto, Transform pai, Vector2 ancora, Vector2 posicao, float tamanho, TextAlignmentOptions alinhamento)
   {
      GameObject obj = new GameObject(nomeObjeto, typeof(RectTransform));
      obj.transform.SetParent(pai, false);
      TextMeshProUGUI t = obj.AddComponent<TextMeshProUGUI>();
      RectTransform r = t.rectTransform;
      r.anchorMin = r.anchorMax = ancora;
      r.pivot = ancora;
      r.anchoredPosition = posicao;
      r.sizeDelta = new Vector2(tamanhoImagem.x - 28f, 60f);
      t.fontSize = tamanho;
      t.alignment = alinhamento;
      t.color = Color.white;
      t.textWrappingMode = TextWrappingModes.Normal;
      t.raycastTarget = false;
      return t;
   }

   private void OnDestroy()
   {
      if (textura != null)
      {
         textura.Release();
         Destroy(textura);
      }
   }
}
