using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// Marcas de impacto nas superfícies (Decal Projector do HDRP):
//  - buracos de bala: tiros do jogador, dos soldados e do líder
//  - queimaduras de laser: tiros dos drones (começam a brilhar e arrefecem)
// Vai num objeto vazio da cena (por exemplo "MarcasImpacto"). Só deve haver um.
// Os scripts de disparo chamam MarcasImpacto.Bala(hit) ou MarcasImpacto.Laser(hit) depois do Raycast.
// Sem este objeto na cena, essas chamadas não fazem nada (não dá erro).
public class MarcasImpacto : MonoBehaviour
{
   [System.Serializable]
   public class TipoMarca
   {
      // prefab com um Decal Projector na raiz (a marca).
      // a queimadura de laser tem um segundo Decal Projector filho (o brilho, que arrefece)
      public DecalProjector prefab;
      // marcas deste tipo ao mesmo tempo na cena. Quando chega ao limite, a mais antiga é reaproveitada
      public int maximo = 40;
      // segundos que a marca fica visível antes de começar a desaparecer (0 = nunca desaparece sozinha)
      public float duracao = 20f;
      // segundos a desaparecer aos poucos no fim
      public float desvanecer = 3f;
      // tamanho ao calhas entre estes dois (multiplica o Size do prefab), para não ficarem todas iguais
      public float escalaMin = 0.8f;
      public float escalaMax = 1.2f;
      // segundos que o brilho (Decal Projector filho) leva a arrefecer. A bala não tem brilho e ignora isto
      public float duracaoBrilho = 0f;

      // marcas visíveis (da mais antiga para a mais recente) e marcas guardadas para reaproveitar
      [System.NonSerialized] public List<Marca> ativas;
      [System.NonSerialized] public Queue<Marca> livres;
   }

   public class Marca
   {
      public DecalProjector decal;
      public DecalProjector brilho;   // null na bala
      public Vector3 tamanho;         // Size desta marca (já com a escala ao calhas)
      public Vector3 tamanhoBrilho;
      public Vector3 brilhoOriginal;  // Size do brilho no prefab
      public float criada;            // Time.time em que apareceu
   }

   [Header("Balas (jogador, soldados, líder)")]
   public TipoMarca bala = new TipoMarca { maximo = 40, duracao = 20f, desvanecer = 3f };

   [Header("Laser (drones)")]
   public TipoMarca laser = new TipoMarca { maximo = 20, duracao = 15f, desvanecer = 3f, duracaoBrilho = 2.5f };

   private static MarcasImpacto instancia;

   private void Awake()
   {
      instancia = this;
      Preparar(bala);
      Preparar(laser);
   }

   private void OnDestroy()
   {
      if (instancia == this) instancia = null;
   }

   private void Preparar(TipoMarca tipo)
   {
      tipo.ativas = new List<Marca>();
      tipo.livres = new Queue<Marca>();
   }

   // ---------- chamadas dos scripts de disparo ----------

   // buraco de bala onde o Raycast acertou (Weapon do jogador, Enemy dos soldados e do líder)
   public static void Bala(RaycastHit hit)
   {
      if (instancia != null) instancia.Colocar(instancia.bala, hit);
   }

   // queimadura de laser onde o Raycast acertou (EnemyDrone)
   public static void Laser(RaycastHit hit)
   {
      if (instancia != null) instancia.Colocar(instancia.laser, hit);
   }

   // ---------- colocar uma marca ----------

   private void Colocar(TipoMarca tipo, RaycastHit hit)
   {
      if (tipo.prefab == null || hit.collider == null) return;
      // só no cenário: nos soldados, drones e no jogador já há sangue ou faíscas
      if (!Superficie(hit.collider)) return;

      Marca m = Obter(tipo);

      // se a superfície se mexe (tem Rigidbody: uma porta, uma caixa), a marca vai agarrada a ela
      Transform t = m.decal.transform;
      t.SetParent(hit.rigidbody != null ? hit.rigidbody.transform : transform, true);

      // virada para dentro da superfície (o Decal projeta para a frente, eixo Z azul)
      // e rodada ao calhas sobre si própria, para não ficarem todas iguais
      t.SetPositionAndRotation(hit.point,
         Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));

      float escala = Random.Range(tipo.escalaMin, tipo.escalaMax);
      Vector3 base_ = tipo.prefab.size;
      m.tamanho = new Vector3(base_.x * escala, base_.y * escala, base_.z);
      m.decal.size = m.tamanho;
      m.decal.fadeFactor = 1f;

      if (m.brilho != null)
      {
         Vector3 o = m.brilhoOriginal;
         m.tamanhoBrilho = new Vector3(o.x * escala, o.y * escala, o.z);
         m.brilho.size = m.tamanhoBrilho;
         m.brilho.fadeFactor = 1f;
         m.brilho.enabled = true;
      }

      m.criada = Time.time;
      m.decal.gameObject.SetActive(true);
      tipo.ativas.Add(m);
   }

   // o collider é cenário (não é trigger, nem inimigo, nem o jogador)
   private bool Superficie(Collider c)
   {
      if (c.isTrigger) return false;
      if (c.GetComponentInParent<Enemy>() != null) return false;
      if (c.GetComponentInParent<EnemyDrone>() != null) return false;
      if (c.GetComponentInParent<PlayerStats>() != null) return false;
      return true;
   }

   // devolve uma marca para usar: uma guardada, a mais antiga (se já chegou ao limite) ou uma nova
   private Marca Obter(TipoMarca tipo)
   {
      // marcas que iam agarradas a um objeto que entretanto foi destruído
      tipo.ativas.RemoveAll(x => x.decal == null);

      while (tipo.livres.Count > 0)
      {
         Marca livre = tipo.livres.Dequeue();
         if (livre.decal != null) return livre;
      }

      if (tipo.ativas.Count >= Mathf.Max(1, tipo.maximo))
      {
         Marca antiga = tipo.ativas[0];
         tipo.ativas.RemoveAt(0);
         return antiga;
      }

      return Criar(tipo);
   }

   private Marca Criar(TipoMarca tipo)
   {
      DecalProjector d = Instantiate(tipo.prefab, transform);
      d.gameObject.SetActive(false);
      // o centro da caixa do Decal fica no ponto do impacto (metade à frente, metade atrás da superfície)
      d.pivot = Vector3.zero;

      Marca m = new Marca { decal = d };
      foreach (DecalProjector filho in d.GetComponentsInChildren<DecalProjector>(true))
      {
         if (filho == d) continue;
         m.brilho = filho;
         m.brilhoOriginal = filho.size;
         filho.pivot = Vector3.zero;
         filho.transform.localPosition = Vector3.zero;
         filho.transform.localRotation = Quaternion.identity;
         break;
      }
      return m;
   }

   // ---------- arrefecer o brilho e fazer desaparecer ----------

   private void Update()
   {
      Atualizar(bala);
      Atualizar(laser);
   }

   private void Atualizar(TipoMarca tipo)
   {
      if (tipo.ativas == null) return;

      for (int i = tipo.ativas.Count - 1; i >= 0; i--)
      {
         Marca m = tipo.ativas[i];
         if (m.decal == null) { tipo.ativas.RemoveAt(i); continue; }

         float idade = Time.time - m.criada;

         // brilho do laser: vai ficando mais fraco e mais pequeno (arrefece das bordas para o centro)
         if (m.brilho != null && m.brilho.enabled)
         {
            float b = tipo.duracaoBrilho > 0f ? 1f - Mathf.Clamp01(idade / tipo.duracaoBrilho) : 0f;
            float encolher = Mathf.Lerp(0.4f, 1f, b);
            m.brilho.fadeFactor = b;
            m.brilho.size = new Vector3(m.tamanhoBrilho.x * encolher, m.tamanhoBrilho.y * encolher, m.tamanhoBrilho.z);
            if (b <= 0f) m.brilho.enabled = false;
         }

         // duração 0: fica até ser reaproveitada
         if (tipo.duracao <= 0f) continue;

         if (idade >= tipo.duracao + tipo.desvanecer)
         {
            Libertar(tipo, i);
            continue;
         }

         if (idade > tipo.duracao)
            m.decal.fadeFactor = 1f - Mathf.Clamp01((idade - tipo.duracao) / Mathf.Max(tipo.desvanecer, 0.01f));
      }
   }

   // esconde a marca e guarda-a para a próxima vez
   private void Libertar(TipoMarca tipo, int indice)
   {
      Marca m = tipo.ativas[indice];
      tipo.ativas.RemoveAt(indice);
      m.decal.gameObject.SetActive(false);
      m.decal.transform.SetParent(transform, false);
      tipo.livres.Enqueue(m);
   }
}
