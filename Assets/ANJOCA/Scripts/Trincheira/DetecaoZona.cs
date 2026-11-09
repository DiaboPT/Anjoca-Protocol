using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Barra de deteção de uma zona da trincheira (Parte 3).
// Vai num Empty com um Box Collider (Is Trigger) que cobre um pedaço da trincheira e o terreno à volta.
// Layer do Empty: Ignore Raycast (a caixa não pode travar balas nem a visão dos soldados).
// Enquanto o jogador está dentro da caixa e algum sniper da lista o vê, a barra enche (2 s até 100%).
// Quando ninguém o vê, a barra fica parada 1 s e depois desce aos poucos.
// Barra cheia = alarme geral: toca o alarme, os snipers da lista disparam enquanto te veem
// e os soldados da lista recebem mais visão (BonusReforco) e vêm direitos a ti.
// Reforços (parte 6): soldados desligados na cena, em pontos de emboscada à volta da trincheira.
// No alarme ligam-se os mais perto de ti (até 3, a pelo menos 15 m, de preferência onde não os estás a ver
// e um de cada lado). Quando o alarme acaba e te perdem de vista, voltam ao ponto deles e desligam-se outra vez.
// Passados 10 s sem nenhum sniper, soldado ou reforço da zona te ver, o alarme acaba e a barra volta a 0.
public class DetecaoZona : MonoBehaviour
{
   [Header("Quem vigia esta zona")]
   // o Pivo de cada torre desta zona (o objeto com o Sniper)
   public Sniper[] snipers;
   // soldados (Enemy) desta zona: no alarme vêm direitos a ti
   public Enemy[] soldados;
   // metros somados à visão dos soldados no alarme (como a Guarita e o Lider).
   // quando te perdem de vista, voltam ao Vision Radius do Inspector e regressam ao posto
   public float bonusVisaoSoldados = 40f;

   [Header("Reforços (soldados desligados em pontos de emboscada)")]
   // soldados (Enemy) desligados na cena (sem o visto no topo do Inspector), à volta da trincheira
   public Enemy[] reforcos;
   // quantos se ligam em cada alarme
   public int maxReforcos = 3;
   // só se ligam os que estão entre estas distâncias de ti (metros)
   public float distanciaMinima = 15f;
   public float distanciaMaxima = 60f;
   // segundos entre cada reforço que se liga
   public float intervaloReforcos = 0.6f;
   // ligado = primeiro os pontos que não estás a ver (fora do ecrã ou atrás de uma parede)
   public bool preferirForaDeVista = true;
   // segundos parado no ponto dele, sem te ver e sem alarme, até se desligar outra vez
   public float tempoAteDesligar = 3f;
   // metros até ao ponto inicial que contam como "já chegou"
   public float distanciaLugar = 0.7f;

   [Header("Barra")]
   // segundos à vista até a barra encher
   public float tempoEncher = 2f;
   // segundos sem ser visto até a barra começar a descer
   public float esperaAntesDeDescer = 1f;
   // quanto desce por segundo (0.25 = 25% por segundo: 4 s de cheia a vazia)
   public float descidaPorSegundo = 0.25f;

   [Header("Alarme")]
   // segundos sem nenhum sniper ou soldado da zona te ver até o alarme acabar (a barra volta a 0)
   public float tempoFimAlarme = 10f;
   // Audio Source no mesmo objeto, em 2D (vazio = procura sozinho)
   public AudioSource audioSource;
   // sirene quando a barra enche (Alarme_Geral). Opcional
   public AudioClip somAlarme;
   // uma voz ao calhas quando a barra enche (Alerta_1 a Alerta_3). Opcional
   public AudioClip[] vozesAlarme;

   [Header("Estado (não mexer, só para ver no Play)")]
   // o jogador está dentro da caixa
   public bool jogadorDentro;
   // dentro da caixa e algum sniper da lista vê-o neste momento
   public bool vistoAgora;
   // durante o alarme: algum soldado da lista vê-o neste momento
   public bool soldadoVe;
   [Range(0f, 1f)] public float barra;
   public bool alarme;
   // reforços ligados neste momento
   public int reforcosFora;

   // para o HUD: quantos reforços foram chamados no último alarme, e quando
   public int ultimosReforcos { get; private set; }
   public float reforcosChamadosEm { get; private set; } = -999f;

   // todas as zonas da cena: o HUD mostra a que tem a barra mais cheia
   public static readonly List<DetecaoZona> todas = new List<DetecaoZona>();

   private float ultimaVezVisto = -999f;
   private Transform jogador;
   private Coroutine aChamar;

   // por reforço: o ponto dele, se está cá fora, desde quando, e desde quando está parado no ponto
   private Vector3[] lugares;
   private bool[] fora;
   private float[] saiuEm;
   private float[] noLugarDesde;

   // segundos que faltam para a barra encher, se continuares à vista
   public float SegundosParaEncher => (1f - barra) * tempoEncher;

   // segundos que faltam para o alarme acabar, se ninguém te vir
   public float SegundosParaFimAlarme => Mathf.Max(0f, tempoFimAlarme - (Time.time - ultimaVezVisto));

   private void Awake()
   {
      if (audioSource == null) audioSource = GetComponent<AudioSource>();

      Collider caixa = GetComponent<Collider>();
      if (caixa == null)
         Debug.LogWarning(name + ": a DetecaoZona precisa de um Box Collider com Is Trigger no mesmo objeto.", this);
      else if (!caixa.isTrigger)
      {
         caixa.isTrigger = true;
         Debug.LogWarning(name + ": a caixa não tinha o Is Trigger ligado. Liguei-o agora, mas liga-o também no Inspector.", this);
      }

      if (snipers == null || snipers.Length == 0)
         Debug.LogWarning(name + ": a lista Snipers está vazia, a barra nunca vai encher.", this);

      PlayerStats stats = FindFirstObjectByType<PlayerStats>();
      if (stats != null) jogador = stats.transform;

      // os reforços ainda estão desligados, mas a posição deles já se pode ler (como na Guarita)
      int n = reforcos != null ? reforcos.Length : 0;
      lugares = new Vector3[n];
      fora = new bool[n];
      saiuEm = new float[n];
      noLugarDesde = new float[n];
      for (int i = 0; i < n; i++)
         if (reforcos[i] != null) lugares[i] = reforcos[i].transform.position;
   }

   private void OnEnable()
   {
      todas.Add(this);
   }

   private void OnDisable()
   {
      todas.Remove(this);
      jogadorDentro = false;
   }

   private void OnTriggerEnter(Collider other)
   {
      // só reage ao Player (é o único com PlayerStats), como a ZonaAlerta
      if (other.GetComponent<PlayerStats>() != null) jogadorDentro = true;
   }

   private void OnTriggerExit(Collider other)
   {
      if (other.GetComponent<PlayerStats>() != null) jogadorDentro = false;
   }

   private void Update()
   {
      vistoAgora = jogadorDentro && AlgumSniperVe();
      soldadoVe = alarme && AlgumSoldadoVe();
      if (vistoAgora || soldadoVe) ultimaVezVisto = Time.time;

      RegressoReforcos();

      if (alarme)
      {
         // durante o alarme a barra fica cheia. Acaba quando ninguém da zona te vê durante tempoFimAlarme
         if (Time.time - ultimaVezVisto >= tempoFimAlarme) FimAlarme();
         return;
      }

      if (vistoAgora)
      {
         barra += Time.deltaTime / Mathf.Max(0.01f, tempoEncher);
         if (barra >= 1f)
         {
            barra = 1f;
            Alarme();
         }
      }
      else if (Time.time - ultimaVezVisto >= esperaAntesDeDescer)
      {
         barra = Mathf.Max(0f, barra - descidaPorSegundo * Time.deltaTime);
      }
   }

   private bool AlgumSniperVe()
   {
      if (snipers == null) return false;

      // o != null salta os snipers que já morreram e foram destruídos
      foreach (Sniper sniper in snipers)
         if (sniper != null && sniper.aVer) return true;

      return false;
   }

   // soldados e reforços vivos e ligados
   private bool AlgumSoldadoVe()
   {
      if (soldados != null)
         foreach (Enemy soldado in soldados)
            if (SoldadoAtivo(soldado) && soldado.playerInVisionRadius) return true;

      if (reforcos != null)
         foreach (Enemy reforco in reforcos)
            if (SoldadoAtivo(reforco) && reforco.playerInVisionRadius) return true;

      return false;
   }

   // o != null salta os que já foram apagados; os desligados (reforços ainda escondidos) e os mortos não contam
   private bool SoldadoAtivo(Enemy soldado)
   {
      return soldado != null && soldado.gameObject.activeInHierarchy && soldado.presentHealth > 0f;
   }

   private void Alarme()
   {
      alarme = true;
      Debug.Log(name + ": ALARME GERAL", this);

      // sirene e uma voz ao calhas
      if (audioSource != null)
      {
         if (somAlarme != null) audioSource.PlayOneShot(somAlarme);
         if (vozesAlarme != null && vozesAlarme.Length > 0)
         {
            AudioClip voz = vozesAlarme[Random.Range(0, vozesAlarme.Length)];
            if (voz != null) audioSource.PlayOneShot(voz);
         }
      }

      // os snipers passam a disparar enquanto te veem
      AlarmeSnipers(true);

      // os soldados vêm direitos a ti (o Enemy tira o bónus sozinho quando te perde de vista)
      DarBonus(soldados, bonusVisaoSoldados);
      // os reforços que ainda estão cá fora de um alarme anterior também
      DarBonus(reforcos, bonusVisaoSoldados);

      // reforços novos junto a ti
      ChamarReforcos();
   }

   private void DarBonus(Enemy[] lista, float valor)
   {
      if (lista == null) return;
      foreach (Enemy soldado in lista)
         if (SoldadoAtivo(soldado)) soldado.BonusReforco(valor);
   }

   // ---------- Reforços ----------

   private void ChamarReforcos()
   {
      ultimosReforcos = 0;
      // ainda há reforços a sair (alarme anterior): deixa acabar
      if (aChamar != null || reforcos == null || jogador == null) return;

      List<int> escolhidos = EscolherReforcos();
      if (escolhidos.Count == 0) return;

      ultimosReforcos = escolhidos.Count;
      reforcosChamadosEm = Time.time;
      aChamar = StartCoroutine(LigarReforcos(escolhidos));
   }

   // os mais perto de ti (entre a distância mínima e a máxima), primeiro os que não estás a ver,
   // e o segundo do lado oposto ao primeiro, para te apanharem entre os dois
   private List<int> EscolherReforcos()
   {
      List<int> candidatos = new List<int>();
      List<float> pontos = new List<float>();

      for (int i = 0; i < reforcos.Length; i++)
      {
         Enemy r = reforcos[i];
         // apagado (morreu) ou já está cá fora
         if (r == null || r.gameObject.activeSelf) continue;

         float distancia = Vector3.Distance(jogador.position, lugares[i]);
         if (distancia < distanciaMinima || distancia > distanciaMaxima) continue;

         float p = distancia;
         if (preferirForaDeVista && JogadorVe(lugares[i])) p += 1000f;
         candidatos.Add(i);
         pontos.Add(p);
      }

      // ordena do melhor (mais pequeno) para o pior
      List<int> ordem = new List<int>();
      for (int k = 0; k < candidatos.Count; k++) ordem.Add(k);
      ordem.Sort((a, b) => pontos[a].CompareTo(pontos[b]));

      List<int> escolhidos = new List<int>();
      if (ordem.Count == 0 || maxReforcos <= 0) return escolhidos;

      // o primeiro: o melhor
      int primeiro = candidatos[ordem[0]];
      escolhidos.Add(primeiro);
      ordem.RemoveAt(0);

      // o segundo: o melhor do outro lado (se houver)
      if (escolhidos.Count < maxReforcos && ordem.Count > 0)
      {
         Vector3 lado1 = lugares[primeiro] - jogador.position;
         lado1.y = 0f;
         int escolha = 0;
         for (int k = 0; k < ordem.Count; k++)
         {
            Vector3 lado = lugares[candidatos[ordem[k]]] - jogador.position;
            lado.y = 0f;
            if (Vector3.Dot(lado1, lado) < 0f) { escolha = k; break; }
         }
         escolhidos.Add(candidatos[ordem[escolha]]);
         ordem.RemoveAt(escolha);
      }

      // os restantes, por ordem
      while (escolhidos.Count < maxReforcos && ordem.Count > 0)
      {
         escolhidos.Add(candidatos[ordem[0]]);
         ordem.RemoveAt(0);
      }
      return escolhidos;
   }

   // o jogador consegue ver este ponto: está no ecrã e nada tapa a linha da câmara até lá
   private bool JogadorVe(Vector3 lugar)
   {
      Camera camara = Camera.main;
      if (camara == null) return false;

      // à altura da cabeça do soldado (o pai do soldado é a cápsula, com o centro a 1 m dos pés)
      Vector3 alvo = lugar + Vector3.up * 0.5f;
      Vector3 noEcra = camara.WorldToViewportPoint(alvo);
      if (noEcra.z <= 0f || noEcra.x < 0f || noEcra.x > 1f || noEcra.y < 0f || noEcra.y > 1f) return false;

      Vector3 direcao = alvo - camara.transform.position;
      float distancia = direcao.magnitude;
      RaycastHit[] hits = Physics.RaycastAll(camara.transform.position, direcao / distancia, distancia,
         ~0, QueryTriggerInteraction.Ignore);
      foreach (RaycastHit hit in hits)
      {
         // o próprio jogador (cápsula, braços) não tapa
         if (hit.transform.IsChildOf(jogador)) continue;
         // uma parede, o terreno ou uma árvore antes de chegar ao ponto: não o vês
         if (hit.distance < distancia - 0.5f) return false;
      }
      return true;
   }

   private IEnumerator LigarReforcos(List<int> escolhidos)
   {
      foreach (int i in escolhidos)
      {
         Enemy r = reforcos[i];
         if (r == null) continue;

         // da primeira vez o Awake do Enemy corre agora e guarda este ponto como o posto dele,
         // por isso, quando te perde de vista, volta para aqui
         r.gameObject.SetActive(true);
         r.BonusReforco(bonusVisaoSoldados);
         fora[i] = true;
         saiuEm[i] = Time.time;
         noLugarDesde[i] = -1f;

         yield return new WaitForSeconds(intervaloReforcos);
      }
      aChamar = null;
   }

   // os reforços que voltaram ao ponto deles, sem te ver e sem alarme, desligam-se outra vez (como na Guarita).
   // a vida que tinham fica guardada (um reforço ferido volta ferido)
   private void RegressoReforcos()
   {
      reforcosFora = 0;
      if (reforcos == null) return;

      for (int i = 0; i < reforcos.Length; i++)
      {
         if (!fora[i]) continue;
         Enemy r = reforcos[i];

         // morreu (ou já foi apagado), ou outro script desligou-o: deixa de contar
         if (r == null || r.presentHealth <= 0f || !r.gameObject.activeSelf) { fora[i] = false; continue; }
         reforcosFora++;

         // acabou de sair, ou o alarme ainda está ligado: fica cá fora
         if (alarme || Time.time - saiuEm[i] < 2f) { noLugarDesde[i] = -1f; continue; }

         Vector3 distancia = r.transform.position - lugares[i];
         distancia.y = 0f;
         bool noLugar = distancia.magnitude < distanciaLugar && !r.playerInVisionRadius && !r.playerInShootingRadius;
         if (!noLugar) { noLugarDesde[i] = -1f; continue; }

         if (noLugarDesde[i] < 0f) noLugarDesde[i] = Time.time;
         if (Time.time - noLugarDesde[i] >= tempoAteDesligar)
         {
            r.gameObject.SetActive(false);
            fora[i] = false;
         }
      }
   }

   private void FimAlarme()
   {
      alarme = false;
      barra = 0f;
      AlarmeSnipers(false);

      // tira o bónus aos que nunca chegaram a ver-te (os outros já o perderam sozinhos).
      // os reforços voltam ao ponto deles e desligam-se (RegressoReforcos)
      DarBonus(soldados, 0f);
      DarBonus(reforcos, 0f);

      Debug.Log(name + ": fim do alarme", this);
   }

   private void AlarmeSnipers(bool ligado)
   {
      if (snipers == null) return;
      foreach (Sniper sniper in snipers)
         if (sniper != null) sniper.Alarme(ligado);
   }

   // a zona com a barra mais cheia (uma zona em alarme ganha sempre). Null = nenhuma barra acima de 0
   public static DetecaoZona MaisAlta()
   {
      DetecaoZona melhor = null;
      foreach (DetecaoZona zona in todas)
      {
         if (zona.barra <= 0f && !zona.alarme) continue;
         if (melhor == null || Pontos(zona) > Pontos(melhor)) melhor = zona;
      }
      return melhor;
   }

   private static float Pontos(DetecaoZona zona)
   {
      return zona.alarme ? 2f : zona.barra;
   }

   // caixa laranja na Scene (mais forte durante o alarme), para veres onde a zona conta
   private void OnDrawGizmos()
   {
      BoxCollider caixa = GetComponent<BoxCollider>();
      if (caixa == null) return;

      Gizmos.matrix = transform.localToWorldMatrix;
      Gizmos.color = new Color(1f, 0.5f, 0f, alarme ? 0.25f : 0.06f);
      Gizmos.DrawCube(caixa.center, caixa.size);
      Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
      Gizmos.DrawWireCube(caixa.center, caixa.size);
   }

   // com a zona selecionada: uma linha vermelha até cada sniper e uma laranja até cada soldado da lista
   private void OnDrawGizmosSelected()
   {
      Gizmos.matrix = Matrix4x4.identity;

      Gizmos.color = Color.red;
      if (snipers != null)
         foreach (Sniper sniper in snipers)
            if (sniper != null) Gizmos.DrawLine(transform.position, sniper.transform.position);

      Gizmos.color = new Color(1f, 0.5f, 0f);
      if (soldados != null)
         foreach (Enemy soldado in soldados)
            if (soldado != null) Gizmos.DrawLine(transform.position, soldado.transform.position);

      // reforços: uma linha azul e uma esfera no ponto de cada um
      Gizmos.color = new Color(0.3f, 0.6f, 1f);
      if (reforcos != null)
         foreach (Enemy reforco in reforcos)
            if (reforco != null)
            {
               Gizmos.DrawLine(transform.position, reforco.transform.position);
               Gizmos.DrawWireSphere(reforco.transform.position, 0.6f);
            }
   }
}
