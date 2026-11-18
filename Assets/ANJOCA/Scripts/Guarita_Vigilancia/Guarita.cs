using System.Collections;
using UnityEngine;

// Guarita nos trajetos: quando uma das suas câmaras vê o Player, abre a porta
// e saem os soldados que estão lá dentro, um a um.
// Quando te perdem de vista, voltam ao lugar deles lá dentro e desligam-se:
// só a câmara os volta a acordar. Quando estão todos dentro, a porta fecha.
// Os soldados ficam desligados na cena (sem o visto no topo do Inspector), como os reforços do líder.
// Vai no objeto raiz da guarita. As câmaras são filhas dela.
public class Guarita : MonoBehaviour
{
   [Header("Soldados lá dentro")]
   // soldados (Enemy) postos dentro da guarita e desligados até ao alerta.
   // saem por esta ordem: o primeiro da lista deve ser o que está mais perto da porta
   public Enemy[] soldados;
   // metros somados à visão dos soldados para virem direitos a ti.
   // quando te perdem de vista, voltam ao Vision Radius do Inspector e regressam à guarita
   public float bonusVisao = 40f;
   // segundos entre a saída de cada soldado
   public float intervaloEntreSoldados = 0.7f;

   [Header("Regresso à guarita")]
   // segundos parado no lugar, sem te ver, até o soldado se desligar
   // (dá tempo para acabar de recarregar e de se virar para a porta)
   public float tempoAteDesligar = 3f;
   // metros até ao lugar inicial que contam como "já chegou"
   public float distanciaLugar = 0.7f;
   // fecha a porta quando os soldados vivos estão todos outra vez lá dentro
   public bool fecharPortaNoFim = true;

   [Header("Porta (opcional)")]
   // objeto da porta, com o pivô na dobradiça
   public Transform porta;
   // graus que a porta roda para abrir (negativo = abre para o outro lado)
   public float anguloPorta = -90f;
   public float tempoAbrir = 0.4f;

   [Header("Alarme (opcional)")]
   public AudioSource audioSource;
   public AudioClip somAlarme;

   // a guarita já deu o alerta pelo menos uma vez
   public bool alertada { get; private set; }

   private Coroutine aSoltar;
   private Coroutine aMoverPorta;
   private bool portaAberta = false;
   private Quaternion rotacaoPortaFechada;

   // por soldado: o lugar dele dentro da guarita, se está cá fora, desde quando, e desde quando está parado no lugar
   private Vector3[] lugares;
   private bool[] fora;
   private float[] saiuEm;
   private float[] noLugarDesde;

   private void Awake()
   {
      if (audioSource == null) audioSource = GetComponent<AudioSource>();
      if (porta != null) rotacaoPortaFechada = porta.localRotation;

      int n = soldados.Length;
      lugares = new Vector3[n];
      fora = new bool[n];
      saiuEm = new float[n];
      noLugarDesde = new float[n];

      // os soldados ainda estão desligados, mas a posição deles já se pode ler
      for (int i = 0; i < n; i++)
         if (soldados[i] != null) lugares[i] = soldados[i].transform.position;
   }

   // chamado por uma CamaraVigilancia quando te deteta
   public void Alertar()
   {
      alertada = true;

      if (somAlarme != null && audioSource != null) audioSource.PlayOneShot(somAlarme);
      AbrirPorta();

      // ainda há soldados a sair de um alerta anterior: deixa acabar
      if (aSoltar != null) return;
      aSoltar = StartCoroutine(SoltarSoldados());
   }

   private IEnumerator SoltarSoldados()
   {
      // espera que a porta abra antes do primeiro sair
      if (porta != null) yield return new WaitForSeconds(tempoAbrir);

      for (int i = 0; i < soldados.Length; i++)
      {
         Enemy soldado = soldados[i];
         // o == null salta os que já morreram e foram apagados da cena
         if (soldado == null) continue;
         // já está cá fora e morreu (ainda não foi apagado): salta
         if (soldado.gameObject.activeSelf && soldado.presentHealth <= 0f) continue;

         bool estavaDentro = !soldado.gameObject.activeSelf;

         // da primeira vez o Awake do Enemy corre agora e guarda esta posição como o posto dele,
         // por isso quando te perde de vista volta para dentro da guarita
         soldado.gameObject.SetActive(true);
         // os que já estão cá fora (alerta anterior) também voltam a vir atrás de ti
         soldado.BonusReforco(bonusVisao);

         fora[i] = true;
         saiuEm[i] = Time.time;
         noLugarDesde[i] = -1f;

         if (estavaDentro) yield return new WaitForSeconds(intervaloEntreSoldados);
      }

      aSoltar = null;
   }

   private void Update()
   {
      bool alguemFora = false;

      for (int i = 0; i < soldados.Length; i++)
      {
         if (!fora[i]) continue;
         Enemy soldado = soldados[i];

         // morreu (ou já foi apagado): deixa de contar
         if (soldado == null || soldado.presentHealth <= 0f) { fora[i] = false; continue; }
         // outro script desligou-o: deixa de contar
         if (!soldado.gameObject.activeSelf) { fora[i] = false; continue; }

         alguemFora = true;

         // acabou de sair: dá-lhe 2 s para te ver antes de verificar se já pode voltar a desligar-se
         if (Time.time - saiuEm[i] < 2f) continue;

         // está no lugar dele e não te vê
         Vector3 distancia = soldado.transform.position - lugares[i];
         distancia.y = 0f;
         bool noLugar = distancia.magnitude < distanciaLugar
            && !soldado.playerInVisionRadius && !soldado.playerInShootingRadius;

         if (!noLugar) { noLugarDesde[i] = -1f; continue; }
         if (noLugarDesde[i] < 0f) noLugarDesde[i] = Time.time;

         // está parado no lugar há tempo suficiente: desliga-se até a câmara te voltar a ver.
         // a vida que tinha fica guardada (um soldado ferido volta ferido)
         if (Time.time - noLugarDesde[i] >= tempoAteDesligar)
         {
            soldado.gameObject.SetActive(false);
            fora[i] = false;
         }
      }

      // já voltaram todos (ou morreram): fecha a porta
      if (fecharPortaNoFim && portaAberta && !alguemFora && aSoltar == null) FecharPorta();
   }

   private void AbrirPorta()
   {
      if (porta == null || portaAberta) return;
      portaAberta = true;
      MoverPorta(rotacaoPortaFechada * Quaternion.Euler(0f, anguloPorta, 0f));
   }

   private void FecharPorta()
   {
      if (porta == null || !portaAberta) return;
      portaAberta = false;
      MoverPorta(rotacaoPortaFechada);
   }

   // roda a porta da posição em que está até à pedida (para a meio se a câmara te vir enquanto fecha)
   private void MoverPorta(Quaternion alvo)
   {
      if (aMoverPorta != null) StopCoroutine(aMoverPorta);
      aMoverPorta = StartCoroutine(RodarPorta(alvo));
   }

   private IEnumerator RodarPorta(Quaternion alvo)
   {
      Quaternion inicio = porta.localRotation;
      float t = 0f;
      while (t < 1f)
      {
         t += Time.deltaTime / Mathf.Max(0.01f, tempoAbrir);
         porta.localRotation = Quaternion.Slerp(inicio, alvo, t);
         yield return null;
      }
      aMoverPorta = null;
   }

   // linhas da guarita até cada soldado, na Scene, quando a guarita está selecionada
   private void OnDrawGizmosSelected()
   {
      if (soldados == null) return;
      Gizmos.color = Color.cyan;
      foreach (Enemy soldado in soldados)
         if (soldado != null) Gizmos.DrawLine(transform.position + Vector3.up, soldado.transform.position + Vector3.up);
   }
}
