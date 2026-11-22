using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Porta de dobradiças (abre para o lado) que se abre a hackear a fechadura, com um minijogo de reflexos.
// Perto da porta aparece "[E] Hackear a fechadura". Ao carregar em E abre-se o minijogo no meio do ecrã:
// um ponteiro roda à volta de um anel com segmentos verdes. Carrega no botão esquerdo quando o ponteiro
// está em cima de um segmento verde por acender, e ele acende. Com todos acesos, a porta roda na dobradiça e fica aberta até ao fim.
// Falhar (clicar fora do verde ou num segmento já aceso) muda o sentido do ponteiro e acelera-o.
// À última falha dispara o alarme: ruído que os soldados perto ouvem e fechadura bloqueada uns segundos.
// O jogo não pára: enquanto hackeias, os soldados continuam a andar. E cancela. Sair da zona também cancela.
// A arma fica em baixo durante o minijogo (o botão esquerdo é do minijogo, não dispara).
// O ecrã do minijogo é criado por este script: não é preciso montar nada no Canvas.
//
// Vai num objeto vazio (por exemplo Porta_Hack_P0) que tem:
//  - um Box Collider com Is Trigger: a zona onde o jogador tem de estar;
//  - um filho vazio "Dobradica", posto na aresta da porta onde ficam as dobradiças (é ele que roda);
//  - dentro da Dobradica, um filho "Folha" (Cube com Box Collider): a folha da porta;
//  - opcional, um filho "Painel" (o painel da fechadura, muda de cor) e uma Light (a luz do painel);
//  - opcional, um NavMeshObstacle com Carve na folha: os soldados não passam com a porta fechada
//    e, aberta, contornam a folha (o buraco no NavMesh roda com ela).
public class PortaHack : MonoBehaviour
{
   [Header("Tecla")]
   // começa o minijogo e, a meio, cancela
   public Key teclaHackear = Key.E;

   [Header("Minijogo")]
   // quantos segmentos verdes há no anel (todos acesos = porta aberta)
   [Range(1, 6)] public int segmentos = 3;
   // largura de cada segmento, em graus (mais pequeno = mais difícil)
   public float larguraSegmento = 28f;
   // velocidade do ponteiro no início, em graus por segundo (360 = uma volta por segundo)
   public float velocidade = 150f;
   // em cada falha a velocidade é multiplicada por isto (1 = não acelera)
   public float aumentoPorFalha = 1.3f;
   public float velocidadeMaxima = 520f;
   // à falha número X dispara o alarme (0 = nunca)
   public int falhasMaximas = 3;
   // segundos com a fechadura bloqueada depois do alarme
   public float tempoBloqueio = 5f;
   // raio do ruído do alarme, em metros (0 = sem ruído). Os soldados dentro do raio vêm investigar
   public float raioRuidoAlarme = 15f;
   // ligado: o alarme é eletrónico e chega a todos os soldados do mesmo piso dentro do raio, mesmo com paredes pelo meio,
   // e eles vêm direitos à porta. Desligado: é só um barulho (as paredes cortam o raio a metade e eles vão a um sítio perto, com erro)
   public bool alarmeAtravessaParedes = true;
   // nome que aparece no minijogo
   public string nomeFechadura = "Fechadura";

   [Header("Cartão (opcional)")]
   // ligado: só se pode hackear depois de apanhar o cartão de acesso (o temCartao do InventarioPlayer)
   public bool precisaCartao = false;
   public string textoSemCartao = "Fechadura sem resposta. Precisas do cartão de acesso";

   [Header("Porta")]
   // quanto a porta roda ao abrir, em graus, à volta do Y da Dobradica.
   // O sinal escolhe o lado: com a folha a sair da dobradiça para X positivo, 90 abre para Z negativo e -90 para Z positivo
   public float anguloAbrir = 90f;
   // segundos que a porta demora a abrir
   public float duracao = 1.2f;

   [Header("Peças (vazias = procura sozinho)")]
   // o vazio na aresta das dobradiças (filho Dobradica). É ele que roda
   public Transform dobradica;
   public Transform folha;
   public Collider zona;
   public Renderer painel;
   public Light luzPainel;
   public NavMeshObstacle obstaculo;
   public Color corTrancada = new Color(1f, 0.1f, 0.1f);
   public Color corBloqueada = new Color(1f, 0.55f, 0f);
   public Color corAberta = new Color(0.1f, 1f, 0.2f);

   [Header("Sons (opcionais)")]
   // vazio = cria um AudioSource 3D neste objeto
   public AudioSource audioSource;
   public AudioClip somComecar;    // Fechadura_Comecar
   public AudioClip somAcerto;     // Fechadura_Acerto
   public AudioClip somFalha;      // Fechadura_Falha
   public AudioClip somAlarme;     // Fechadura_Alarme
   public AudioClip somHackeada;   // Fechadura_Desbloqueada
   public AudioClip somAbrir;      // Porta_Abrir

   [Header("HUD")]
   // o mesmo texto do Canvas que o quadro elétrico, a tampa e as outras portas usam
   public TextMeshProUGUI textoAviso;
   public string nomeTextoAviso = "RedeEletricaTexto";

   [Header("Estado (ver durante o Play)")]
   public bool aberta = false;
   public bool aJogar = false;

   // jogador
   private PlayerStats jogador;
   private InventarioPlayer inventario;
   private readonly List<Behaviour> desligados = new List<Behaviour>();
   private bool religarPendente = false;

   // porta
   private Quaternion rotacaoFechada;
   private Collider[] collidersFolha;
   private bool aAbrir = false;
   private float tempo = 0f;
   private float bloqueadaAte = -1f;
   private string ultimoTexto = null;
   private int corPosta = -1;

   // minijogo
   private float angulo, sentido = 1f, velocidadeAtual;
   private float[] inicioSegmento;
   private bool[] aceso;
   private int acertos, falhas;
   private float flashAte = -1f;

   // ecrã do minijogo
   private GameObject ecra;
   private Image fundo, anel;
   private Image[] imagensSegmento;
   private RectTransform ponteiro;
   private TextMeshProUGUI textoTitulo, textoEstado;
   private static Sprite spriteAnel;

   private static readonly Color corSegmentoApagado = new Color(0.1f, 0.4f, 0.18f);
   private static readonly Color corSegmentoAceso = new Color(0.45f, 1f, 0.6f);
   private static readonly Color corFundo = new Color(0.04f, 0.05f, 0.06f, 0.92f);
   private static readonly Color corFundoFalha = new Color(0.35f, 0.03f, 0.03f, 0.92f);

   private void Awake()
   {
      if (zona == null) zona = GetComponent<Collider>();
      if (dobradica == null) dobradica = transform.Find("Dobradica");
      if (folha == null && dobradica != null) folha = dobradica.Find("Folha");
      if (folha == null) folha = transform.Find("Folha");
      // sem Dobradica, roda a própria folha (à volta do centro dela)
      if (dobradica == null) dobradica = folha;
      if (painel == null)
      {
         Transform t = transform.Find("Painel");
         if (t != null) painel = t.GetComponent<Renderer>();
      }
      if (luzPainel == null) luzPainel = GetComponentInChildren<Light>();
      if (obstaculo == null && folha != null) obstaculo = folha.GetComponent<NavMeshObstacle>();

      if (folha == null) Debug.LogWarning(name + ": não encontrei a folha da porta (filho Folha)");
      if (zona == null || !zona.isTrigger) Debug.LogWarning(name + ": falta o Box Collider com Is Trigger neste objeto");
      if (dobradica != null) rotacaoFechada = dobradica.localRotation;
      collidersFolha = folha != null ? folha.GetComponentsInChildren<Collider>() : new Collider[0];

      if (audioSource == null)
      {
         audioSource = gameObject.AddComponent<AudioSource>();
         audioSource.playOnAwake = false;
         audioSource.spatialBlend = 1f;
         audioSource.minDistance = 2f;
         audioSource.maxDistance = 25f;
      }
   }

   private void Start()
   {
      if (textoAviso == null && !string.IsNullOrEmpty(nomeTextoAviso))
      {
         GameObject obj = GameObject.Find(nomeTextoAviso);
         if (obj != null) textoAviso = obj.GetComponent<TextMeshProUGUI>();
         if (textoAviso == null) Debug.LogWarning(name + ": não encontrei o texto \"" + nomeTextoAviso + "\" no Canvas");
      }
      inventario = FindFirstObjectByType<InventarioPlayer>();
      AtualizarCor();
   }

   private void Update()
   {
      Mouse rato = Mouse.current;

      // a arma só volta depois de largares o botão esquerdo (senão o último clique do minijogo disparava)
      if (religarPendente && (rato == null || !rato.leftButton.isPressed)) Religar();

      AtualizarCor();

      if (aAbrir)
      {
         tempo += Time.deltaTime;
         float t = duracao > 0f ? Mathf.Clamp01(tempo / duracao) : 1f;
         if (dobradica != null) dobradica.localRotation = rotacaoFechada * Quaternion.Euler(0f, anguloAbrir * Mathf.SmoothStep(0f, 1f, t), 0f);
         if (t >= 1f)
         {
            aAbrir = false;
            // a folha volta a ser sólida, já aberta
            foreach (Collider c in collidersFolha) if (c != null) c.enabled = true;
         }
         return;
      }
      if (aberta) return;

      // saiu da zona ou morreu
      if (jogador == null || jogador.isDead)
      {
         if (aJogar) Terminar();
         if (jogador != null) { jogador = null; Apagar(); }
         return;
      }

      Keyboard kb = Keyboard.current;
      if (kb == null) return;
      bool carregouTecla = kb[teclaHackear].wasPressedThisFrame;

      if (aJogar)
      {
         if (carregouTecla) { Terminar(); return; }
         AtualizarMinijogo(rato != null && rato.leftButton.wasPressedThisFrame);
         return;
      }

      if (Time.time < bloqueadaAte)
      {
         MostrarTexto("Fechadura bloqueada: " + Mathf.CeilToInt(bloqueadaAte - Time.time) + " s");
         return;
      }
      if (precisaCartao && (inventario == null || !inventario.temCartao))
      {
         MostrarTexto(textoSemCartao);
         if (carregouTecla) Tocar(somFalha);
         return;
      }
      if (DispositivoAberto())
      {
         MostrarTexto("Fecha o dispositivo para hackear a fechadura");
         return;
      }

      MostrarTexto("[" + teclaHackear + "] Hackear a fechadura");
      if (carregouTecla) Comecar();
   }

   // ---------- Minijogo ----------

   private void Comecar()
   {
      aJogar = true;
      acertos = 0;
      falhas = 0;
      sentido = Random.value < 0.5f ? 1f : -1f;
      velocidadeAtual = velocidade;
      GerarSegmentos();

      if (ecra == null) CriarEcra();
      ecra.SetActive(true);
      DesligarArmas();
      Apagar();
      Tocar(somComecar);
      DesenharMinijogo();
   }

   // um segmento em cada fatia do anel, num sítio ao calhas dentro da fatia, com folga entre eles
   private void GerarSegmentos()
   {
      int n = Mathf.Max(1, segmentos);
      inicioSegmento = new float[n];
      aceso = new bool[n];
      float fatia = 360f / n;
      float largura = Mathf.Min(larguraSegmento, fatia - 4f);
      float folga = Mathf.Min(10f, (fatia - largura) * 0.5f);
      float rodar = Random.Range(0f, 360f);
      for (int i = 0; i < n; i++)
      {
         float livre = fatia - largura - 2f * folga;
         inicioSegmento[i] = Mathf.Repeat(rodar + i * fatia + folga + Random.Range(0f, Mathf.Max(0f, livre)), 360f);
      }
      // o ponteiro começa na fronteira de uma fatia, que fica sempre fora do verde
      angulo = rodar;
   }

   private void AtualizarMinijogo(bool clicou)
   {
      angulo = Mathf.Repeat(angulo + sentido * velocidadeAtual * Time.deltaTime, 360f);

      if (clicou)
      {
         int s = SegmentoEm(angulo);
         if (s >= 0 && !aceso[s])
         {
            aceso[s] = true;
            acertos++;
            Tocar(somAcerto);
            if (acertos >= aceso.Length) { Hackeada(); return; }
         }
         else
         {
            Falhar();
            if (!aJogar) return;
         }
      }
      DesenharMinijogo();
   }

   private int SegmentoEm(float a)
   {
      float largura = Mathf.Min(larguraSegmento, 360f / aceso.Length - 4f);
      for (int i = 0; i < inicioSegmento.Length; i++)
         if (Mathf.Repeat(a - inicioSegmento[i], 360f) <= largura) return i;
      return -1;
   }

   private void Falhar()
   {
      falhas++;
      sentido = -sentido;
      velocidadeAtual = Mathf.Min(velocidadeAtual * aumentoPorFalha, velocidadeMaxima);
      flashAte = Time.time + 0.2f;
      Tocar(somFalha);

      if (falhasMaximas > 0 && falhas >= falhasMaximas) Alarme();
   }

   private void Alarme()
   {
      Tocar(somAlarme);
      if (raioRuidoAlarme > 0f)
      {
         int vem;
         if (alarmeAtravessaParedes) vem = AvisarSoldados();
         else vem = Ruido.Emitir(folha != null ? folha.position : transform.position, raioRuidoAlarme, TipoRuido.Objeto, this);
         Debug.Log(name + ": alarme! " + vem + " soldado(s) vêm ver a porta");
      }
      bloqueadaAte = Time.time + tempoBloqueio;
      Terminar();
   }

   // chama cada soldado ligado do mesmo piso, até raioRuidoAlarme metros, do mais perto para o mais longe.
   // O ponto é o chão em frente à porta, do lado de quem a hackeia (o lado para onde ela abre), onde eles conseguem chegar
   private int AvisarSoldados()
   {
      Vector3 frente = transform.TransformPoint(new Vector3(0f, 0f, -1.5f));
      EventoRuido ruido = new EventoRuido
      {
         posicao = frente, raio = raioRuidoAlarme, tipo = TipoRuido.Objeto, origem = this, momento = Time.time
      };
      // aparece no F3 como os outros barulhos
      Ruido.Recentes.Add(ruido);
      if (Ruido.Recentes.Count > 24) Ruido.Recentes.RemoveAt(0);

      List<Enemy> perto = new List<Enemy>();
      List<float> distancias = new List<float>();
      foreach (Enemy e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
      {
         if (!e.isActiveAndEnabled) continue;
         Vector3 ouvidos = e.Ouvidos;
         if (Mathf.Abs(ouvidos.y - (frente.y + 1.6f)) > Ruido.AlturaOutroPiso) continue;   // outro piso
         float d = Vector2.Distance(new Vector2(ouvidos.x, ouvidos.z), new Vector2(frente.x, frente.z));
         if (d > raioRuidoAlarme) continue;
         int pos = 0;
         while (pos < distancias.Count && distancias[pos] <= d) pos++;
         perto.Insert(pos, e);
         distancias.Insert(pos, d);
      }
      for (int i = 0; i < perto.Count; i++)
         if (perto[i].Ouvir(ruido, frente, distancias[i])) ruido.reagiram++;
      return ruido.reagiram;
   }

   private void Hackeada()
   {
      Tocar(somHackeada);
      Terminar();
      Abrir();
   }

   // fecha o minijogo (acabou, cancelaste, saíste da zona ou morreste)
   private void Terminar()
   {
      aJogar = false;
      if (ecra != null) ecra.SetActive(false);
      religarPendente = true;
   }

   // ---------- Porta ----------

   // pública para o Modo Debug, um terminal ou o lockdown a poderem abrir
   public void Abrir()
   {
      if (aberta) return;
      if (aJogar) Terminar();
      aberta = true;
      aAbrir = true;
      tempo = 0f;
      // enquanto roda, a folha não é sólida: assim não empurra nem prende o jogador que está à frente
      foreach (Collider c in collidersFolha) if (c != null) c.enabled = false;
      // o NavMeshObstacle fica ligado: com Carve, o buraco no NavMesh acompanha a folha aberta
      Tocar(somAbrir);
      Apagar();
      AtualizarCor();
   }

   // painel: vermelho trancada, laranja bloqueada pelo alarme, verde aberta
   private void AtualizarCor()
   {
      int estado = aberta ? 2 : (Time.time < bloqueadaAte ? 1 : 0);
      if (estado == corPosta) return;
      corPosta = estado;
      Color cor = estado == 2 ? corAberta : (estado == 1 ? corBloqueada : corTrancada);
      if (luzPainel != null) luzPainel.color = cor;
      if (painel != null) painel.material.color = cor;
   }

   // ---------- Jogador ----------

   // a arma fica em baixo, o TrocaArma e o DispositivoHack ficam parados (procurados pelo nome, compila sem eles)
   private void DesligarArmas()
   {
      Religar();
      if (jogador == null) return;
      foreach (Weapon w in jogador.GetComponentsInChildren<Weapon>(true))
         if (w.enabled) { w.enabled = false; desligados.Add(w); }
      foreach (string nome in new[] { "TrocaArma", "DispositivoHack" })
      {
         Behaviour b = jogador.GetComponent(nome) as Behaviour;
         if (b != null && b.enabled) { b.enabled = false; desligados.Add(b); }
      }
   }

   private void Religar()
   {
      foreach (Behaviour b in desligados)
         if (b != null) b.enabled = true;
      desligados.Clear();
      religarPendente = false;
   }

   private bool DispositivoAberto()
   {
      Behaviour b = jogador != null ? jogador.GetComponent("DispositivoHack") as Behaviour : null;
      if (b == null || !b.enabled) return false;
      System.Reflection.PropertyInfo p = b.GetType().GetProperty("aberto");
      return p != null && (bool)p.GetValue(b);
   }

   private void OnTriggerEnter(Collider other)
   {
      // só o Player tem PlayerStats; inimigos e balas são ignorados
      PlayerStats p = other.GetComponentInParent<PlayerStats>();
      if (p == null) return;
      jogador = p;
      if (inventario == null) inventario = other.GetComponentInParent<InventarioPlayer>();
   }

   private void OnTriggerExit(Collider other)
   {
      if (other.GetComponentInParent<PlayerStats>() == null) return;
      if (aJogar) Terminar();
      jogador = null;
      Apagar();
   }

   private void OnDisable()
   {
      if (aJogar) Terminar();
      Religar();
   }

   // ---------- HUD ----------

   private void MostrarTexto(string texto)
   {
      if (textoAviso == null) return;
      if (textoAviso.text != texto) textoAviso.text = texto;
      ultimoTexto = texto;
   }

   private void Apagar()
   {
      if (textoAviso != null && ultimoTexto != null && textoAviso.text == ultimoTexto) textoAviso.text = "";
      ultimoTexto = null;
   }

   private void Tocar(AudioClip som)
   {
      if (som != null && audioSource != null) audioSource.PlayOneShot(som);
   }

   // ---------- Ecrã do minijogo ----------

   private void DesenharMinijogo()
   {
      ponteiro.localEulerAngles = new Vector3(0f, 0f, -angulo);
      for (int i = 0; i < imagensSegmento.Length; i++)
      {
         bool existe = i < aceso.Length;
         imagensSegmento[i].gameObject.SetActive(existe);
         if (!existe) continue;
         imagensSegmento[i].fillAmount = Mathf.Min(larguraSegmento, 360f / aceso.Length - 4f) / 360f;
         imagensSegmento[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, -inicioSegmento[i]);
         imagensSegmento[i].color = aceso[i] ? corSegmentoAceso : corSegmentoApagado;
      }
      fundo.color = Time.time < flashAte ? corFundoFalha : corFundo;

      string estado = "Acertos " + acertos + "/" + aceso.Length;
      if (falhasMaximas > 0) estado += "      Falhas " + falhas + "/" + falhasMaximas;
      textoEstado.text = estado + "\n<color=#8FA89A>[Botão esquerdo] no verde      [" + teclaHackear + "] Cancelar</color>";
   }

   private void CriarEcra()
   {
      ecra = new GameObject("Fechadura_Ecra", typeof(RectTransform));
      ecra.transform.SetParent(transform, false);
      Canvas canvas = ecra.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 25;
      CanvasScaler escala = ecra.AddComponent<CanvasScaler>();
      escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      escala.referenceResolution = new Vector2(1920f, 1080f);
      escala.matchWidthOrHeight = 0.5f;

      fundo = CriarImagem("Fundo", ecra.transform, null, new Vector2(520f, 560f));
      fundo.color = corFundo;

      anel = CriarImagem("Anel", fundo.transform, SpriteAnel(), new Vector2(360f, 360f));
      anel.rectTransform.anchoredPosition = new Vector2(0f, 10f);
      anel.color = new Color(1f, 1f, 1f, 0.12f);

      imagensSegmento = new Image[6];
      for (int i = 0; i < imagensSegmento.Length; i++)
      {
         Image s = CriarImagem("Segmento_" + i, anel.transform, SpriteAnel(), new Vector2(360f, 360f));
         s.type = Image.Type.Filled;
         s.fillMethod = Image.FillMethod.Radial360;
         s.fillOrigin = (int)Image.Origin360.Top;
         s.fillClockwise = true;
         imagensSegmento[i] = s;
      }

      Image p = CriarImagem("Ponteiro", anel.transform, null, new Vector2(6f, 196f));
      p.color = Color.white;
      ponteiro = p.rectTransform;
      ponteiro.pivot = new Vector2(0.5f, 0f);
      ponteiro.anchoredPosition = Vector2.zero;

      Image centro = CriarImagem("Centro", anel.transform, null, new Vector2(18f, 18f));
      centro.color = Color.white;

      textoTitulo = CriarTexto("Titulo", fundo.transform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), 30f);
      textoTitulo.text = nomeFechadura.ToUpper();
      textoEstado = CriarTexto("Estado", fundo.transform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), 22f);
      textoEstado.color = new Color(0.75f, 1f, 0.8f);
   }

   private Image CriarImagem(string nome, Transform pai, Sprite sprite, Vector2 tamanho)
   {
      GameObject obj = new GameObject(nome, typeof(RectTransform), typeof(Image));
      obj.transform.SetParent(pai, false);
      Image img = obj.GetComponent<Image>();
      img.sprite = sprite;
      img.raycastTarget = false;
      RectTransform r = img.rectTransform;
      r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
      r.sizeDelta = tamanho;
      return img;
   }

   private TextMeshProUGUI CriarTexto(string nome, Transform pai, Vector2 ancora, Vector2 posicao, float tamanho)
   {
      GameObject obj = new GameObject(nome, typeof(RectTransform));
      obj.transform.SetParent(pai, false);
      TextMeshProUGUI t = obj.AddComponent<TextMeshProUGUI>();
      RectTransform r = t.rectTransform;
      r.anchorMin = r.anchorMax = r.pivot = ancora;
      r.anchoredPosition = posicao;
      r.sizeDelta = new Vector2(480f, 70f);
      t.fontSize = tamanho;
      t.alignment = TextAlignmentOptions.Center;
      t.color = Color.white;
      t.raycastTarget = false;
      return t;
   }

   // anel branco desenhado no arranque (não é preciso importar nenhuma imagem)
   private static Sprite SpriteAnel()
   {
      if (spriteAnel != null) return spriteAnel;
      const int n = 256;
      Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
      tex.wrapMode = TextureWrapMode.Clamp;
      float c = (n - 1) * 0.5f;
      float fora = c, dentro = c * 0.8f;
      Color32[] px = new Color32[n * n];
      for (int y = 0; y < n; y++)
         for (int x = 0; x < n; x++)
         {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
            float a = Mathf.Clamp01(fora - d) * Mathf.Clamp01(d - dentro);
            px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
         }
      tex.SetPixels32(px);
      tex.Apply();
      spriteAnel = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
      return spriteAnel;
   }
}
