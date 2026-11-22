using System.Collections.Generic;
using UnityEngine;

// O "ouvido" dos soldados. Tudo o que faz barulho chama:
//
//    Ruido.Emitir(posicao, raio, TipoRuido.Tiro, this);
//
// e cada soldado ligado (Enemy) decide se ouviu:
//  1. Está a menos de "raio" metros? Se não, não ouve.
//  2. Há uma parede ou laje pelo meio? O raio conta metade.
//  3. Está noutro piso (mais de 2.5 m acima ou abaixo)? O raio conta um terço, menos nas explosões.
// Quem ouve fica com o sítio do barulho com um erro de 1 a 3 m (maior quanto mais longe), por isso
// vai ver o que foi mas não vai direito a ti. Ouvir nunca o põe a disparar: só disparam quando te veem.
//
// É só código: não vai em nenhum objeto. Os soldados registam-se sozinhos (Enemy.OnEnable).
// Quem emite hoje: Weapon (tiro), RuidoPassosJogador (passos e aterrar), Enemy (aviso e grito).
// A conversa "Distrair guardas com ruído" vai juntar o objeto atirado e o rádio (tipos Objeto e Radio).

// do menos para o mais importante: a ir ver um barulho, o soldado troca-o por um mais importante, nunca por um menos.
// O tiro vem depois da explosão porque diz onde TU estás
public enum TipoRuido
{
   PassosAndar,   // jogador a andar (3 m)
   PassosCorrer,  // jogador a correr (8 m)
   Aterrar,       // jogador a cair de um salto alto (6 m)
   Objeto,        // objeto atirado a cair no chão
   Radio,         // rádio ligado
   Grito,         // colega a morrer (15 m)
   Explosao,      // porta, carga, drone (60 m). Ouve-se noutros pisos com o raio inteiro
   Tiro,          // tiro do jogador (35 m)
   Aviso          // colega que te viu: "Está ali!" (20 m). Passa a tua posição sem erro
}

// um barulho, tal como chega a cada soldado
public class EventoRuido
{
   public Vector3 posicao;   // onde foi o barulho (a sério)
   public float raio;        // metros a que se ouve, sem paredes pelo meio
   public TipoRuido tipo;
   public Component origem;  // quem o fez (o Weapon, o soldado que avisa...). Pode ser null
   public float momento;     // Time.time
   // quantos soldados já reagiram a este barulho. Os soldados recebem-no do mais perto para o mais longe,
   // por isso no Aviso o primeiro a reagir (0) é o que está mais perto: é esse que vai direito à pista
   public int reagiram;
}

// quem pode ouvir (o Enemy; mais tarde o drone)
public interface IOuvinte
{
   // de onde ouve (a cabeça)
   Vector3 Ouvidos { get; }
   // o próprio corpo, para não contar como parede
   Transform Raiz { get; }
   // pontoOuvido: onde acha que foi (com erro). distancia: metros até ao barulho a sério.
   // Devolve true se reagiu (vai ver, muda de pista...)
   bool Ouvir(EventoRuido ruido, Vector3 pontoOuvido, float distancia);
}

public static class Ruido
{
   // ---------- Raios de partida (quem emite pode usar outros) ----------
   public const float RaioPassosAndar = 3f;
   public const float RaioPassosCorrer = 8f;
   public const float RaioAterrar = 6f;
   public const float RaioObjeto = 12f;   // a afinar na conversa "Distrair guardas com ruído"
   public const float RaioRadio = 10f;    // idem
   public const float RaioGrito = 15f;
   public const float RaioAviso = 20f;
   public const float RaioTiro = 35f;
   public const float RaioExplosao = 60f;

   // ---------- Paredes e pisos ----------
   // com uma parede ou laje pelo meio, o raio multiplica por isto
   public const float FatorParede = 0.5f;
   // noutro piso, o raio multiplica por isto (as explosões não)
   public const float FatorOutroPiso = 1f / 3f;
   // mais do que isto de diferença de altura entre o barulho e os ouvidos = outro piso
   public const float AlturaOutroPiso = 2.5f;
   // erro do ponto ouvido: de 1 m (colado) a 3 m (no limite do raio)
   public const float ErroMinimo = 1f;
   public const float ErroMaximo = 3f;

   static readonly List<IOuvinte> ouvintes = new List<IOuvinte>();
   static int mascaraParedes = 0;

   // os últimos barulhos (para o F3 desenhar as esferas)
   public static readonly List<EventoRuido> Recentes = new List<EventoRuido>();
   const int MaximoRecentes = 24;

   public static void Registar(IOuvinte o)
   {
      if (o != null && !ouvintes.Contains(o)) ouvintes.Add(o);
   }

   public static void Remover(IOuvinte o)
   {
      ouvintes.Remove(o);
   }

   // raio de partida de cada tipo, para quem não quiser escolher
   public static float RaioPadrao(TipoRuido tipo)
   {
      switch (tipo)
      {
         case TipoRuido.PassosAndar:  return RaioPassosAndar;
         case TipoRuido.PassosCorrer: return RaioPassosCorrer;
         case TipoRuido.Aterrar:      return RaioAterrar;
         case TipoRuido.Objeto:       return RaioObjeto;
         case TipoRuido.Radio:        return RaioRadio;
         case TipoRuido.Grito:        return RaioGrito;
         case TipoRuido.Aviso:        return RaioAviso;
         case TipoRuido.Tiro:         return RaioTiro;
         default:                     return RaioExplosao;
      }
   }

   // com o raio de partida do tipo
   public static int Emitir(Vector3 posicao, TipoRuido tipo, Component origem = null)
   {
      return Emitir(posicao, RaioPadrao(tipo), tipo, origem);
   }

   // faz um barulho. Devolve quantos soldados reagiram
   public static int Emitir(Vector3 posicao, float raio, TipoRuido tipo, Component origem = null)
   {
      if (raio <= 0f) return 0;
      if (mascaraParedes == 0)
      {
         // tapa o som tudo o que tapa a vista (paredes, lajes, portas, móveis), menos a vegetação
         mascaraParedes = ~LayerMask.GetMask("Ignore Raycast", "TransparentFX", "UI", "Player", "Enemy",
            "Minimapa", "VolumeMinimapa", "VolumeMira", "Vegetacao");
      }

      EventoRuido ruido = new EventoRuido
      {
         posicao = posicao, raio = raio, tipo = tipo, origem = origem, momento = Time.time
      };
      Recentes.Add(ruido);
      if (Recentes.Count > MaximoRecentes) Recentes.RemoveAt(0);

      // 1) quem ouve, e a que distância (lista nova em cada barulho: um soldado pode fazer barulho
      //    enquanto reage a outro, e não pode estragar a lista do primeiro)
      List<IOuvinte> quem = new List<IOuvinte>();
      List<float> distancias = new List<float>();
      List<float> raios = new List<float>();
      Transform raizOrigem = CorpoDe(origem);

      for (int i = ouvintes.Count - 1; i >= 0; i--)
      {
         IOuvinte o = ouvintes[i];
         // objeto destruído sem ter passado pelo OnDisable
         if (o == null || (o is Object uo && uo == null)) { ouvintes.RemoveAt(i); continue; }
         if (origem != null && ReferenceEquals(o, origem)) continue;   // não se ouve a si próprio

         Vector3 ouvidos = o.Ouvidos;
         float d = Vector3.Distance(posicao, ouvidos);
         if (d > raio) continue;   // longe demais mesmo sem paredes

         float r = raio;
         bool outroPiso = Mathf.Abs(ouvidos.y - posicao.y) > AlturaOutroPiso;
         if (outroPiso && tipo != TipoRuido.Explosao) r *= FatorOutroPiso;
         if (d > r) continue;
         if (TemParede(posicao, ouvidos, raizOrigem, o.Raiz)) r *= FatorParede;
         if (d > r) continue;

         // ordenado do mais perto para o mais longe
         int pos = 0;
         while (pos < distancias.Count && distancias[pos] <= d) pos++;
         quem.Insert(pos, o);
         distancias.Insert(pos, d);
         raios.Insert(pos, r);
      }

      // 2) cada um reage, do mais perto para o mais longe
      for (int i = 0; i < quem.Count; i++)
      {
         Vector3 ponto = posicao;
         // o aviso de um colega diz exatamente onde estás; os outros barulhos não
         if (tipo != TipoRuido.Aviso)
         {
            float erro = Mathf.Lerp(ErroMinimo, ErroMaximo, Mathf.Clamp01(distancias[i] / Mathf.Max(raios[i], 0.01f)));
            Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(erro * 0.5f, erro);
            ponto += new Vector3(dir.x, 0f, dir.y);
         }
         if (quem[i].Ouvir(ruido, ponto, distancias[i])) ruido.reagiram++;
      }
      return ruido.reagiram;
   }

   // o corpo de quem fez o barulho, para não contar como parede. Não pode ser o transform.root: os soldados estão
   // dentro do ANJOCA_Base, tal como as paredes e as lajes, e assim o aviso e o grito passavam por tudo
   static Transform CorpoDe(Component origem)
   {
      if (origem == null) return null;
      if (origem is IOuvinte ouvinte) return ouvinte.Raiz;                         // um soldado
      CharacterController cc = origem.GetComponentInParent<CharacterController>();  // o jogador (Weapon, passos)
      if (cc != null) return cc.transform;
      return origem.transform;                                                      // um objeto, um rádio
   }

   // há alguma coisa sólida entre o barulho e os ouvidos? (o corpo de quem fez o barulho,
   // o de quem ouve, os soldados e os triggers não contam)
   static bool TemParede(Vector3 de, Vector3 ate, Transform raizOrigem, Transform raizOuvinte)
   {
      Vector3 dir = ate - de;
      float dist = dir.magnitude;
      if (dist < 0.05f) return false;
      // nos dois sentidos: um chão feito de um plano só tem colisão de um lado
      return TemParedeNumSentido(de, dir / dist, dist, raizOrigem, raizOuvinte)
          || TemParedeNumSentido(ate, -dir / dist, dist, raizOrigem, raizOuvinte);
   }

   static bool TemParedeNumSentido(Vector3 de, Vector3 sentido, float dist, Transform raizOrigem, Transform raizOuvinte)
   {
      RaycastHit[] hits = Physics.RaycastAll(de, sentido, dist, mascaraParedes, QueryTriggerInteraction.Ignore);
      foreach (RaycastHit hit in hits)
      {
         Transform t = hit.collider.transform;
         if (raizOuvinte != null && t.IsChildOf(raizOuvinte)) continue;
         if (raizOrigem != null && t.IsChildOf(raizOrigem)) continue;
         if (hit.collider.GetComponentInParent<Enemy>() != null) continue;
         return true;
      }
      return false;
   }

   // cor de cada tipo no F3
   public static Color CorDebug(TipoRuido tipo)
   {
      switch (tipo)
      {
         case TipoRuido.PassosAndar:
         case TipoRuido.PassosCorrer:
         case TipoRuido.Aterrar:  return new Color(0.4f, 0.8f, 1f);   // azul: passos
         case TipoRuido.Objeto:
         case TipoRuido.Radio:    return new Color(0.8f, 0.4f, 1f);   // roxo: distrações
         case TipoRuido.Aviso:    return new Color(1f, 0.5f, 0f);     // laranja: aviso de um colega
         case TipoRuido.Grito:    return Color.magenta;
         default:                 return Color.red;                   // tiro, explosão
      }
   }
}
