using UnityEngine;

// Pack de vida (+30), caixa de munição (+10 balas) ou módulo de aprimoramento.
// Pôr num objeto com Collider marcado como "Is Trigger".
public class Pickup : MonoBehaviour
{
    // Os novos ficam no fim da lista, para os objetos que já estão na cena não mudarem de tipo
    public enum PickupType { HealthPack, JumpModule, ArmorModule, PrecisionModule, ThermalModule, SpeedModule, AmmoPack }

    public PickupType type = PickupType.HealthPack;

    [Header("Vida (só no Health Pack)")]
    [Tooltip("Vida que o pack dá")]
    public float healAmount = 30f;

    [Header("Munição (só no Ammo Pack)")]
    [Tooltip("Balas que vão para a reserva da arma")]
    public int balas = 10;
    [Tooltip("Para que arma são estas balas. As caixas que os soldados largam são de Espingarda")]
    public TipoMunicao municao = TipoMunicao.Espingarda;

    [Header("Aspeto e som (opcional)")]
    [Tooltip("Graus por segundo a rodar, para se ver que se apanha. 0 = parado")]
    public float velocidadeRodar = 0f;
    [Tooltip("Som ao apanhar")]
    public AudioClip somApanhar;
    [Tooltip("Segundos até desaparecer sozinho, se ninguém a apanhar. 0 = fica para sempre")]
    public float duracao = 0f;

    void Start()
    {
        if (duracao > 0f) Destroy(gameObject, duracao);
    }

    void Update()
    {
        if (velocidadeRodar != 0f)
            transform.Rotate(0f, velocidadeRodar * Time.deltaTime, 0f, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        // Só o jogador (vivo) apanha
        PlayerStats stats = other.GetComponentInParent<PlayerStats>();
        if (stats == null || stats.isDead) return;

        if (type == PickupType.HealthPack)
        {
            if (stats.health >= stats.maxHealth) return;   // vida cheia: o pack fica no mapa
            stats.Heal(healAmount);
        }

        if (type == PickupType.AmmoPack)
        {
            // o Weapon está dentro da Main Camera, que é filha do jogador
            Weapon arma;
            // com duas armas, o Troca Arma (no Player) diz qual é a arma desta munição.
            // Sem ele, como era antes, vai para a única arma que existe
            TrocaArma trocaArma = stats.GetComponent<TrocaArma>();
            if (trocaArma != null) arma = trocaArma.ArmaDaMunicao(municao);
            else arma = stats.GetComponentInChildren<Weapon>(true);
            if (arma == null) return;
            // reserva cheia (só acontece se a arma tiver Reserva Maxima): a caixa fica no chão
            if (!arma.AdicionarBalas(balas)) return;
        }

        SuitAbilities abilities = stats.GetComponent<SuitAbilities>();
        if (abilities != null)
        {
            if (type == PickupType.JumpModule) abilities.jumpUnlocked = true;
            if (type == PickupType.ArmorModule) abilities.armorUnlocked = true;
            if (type == PickupType.PrecisionModule) abilities.precisionUnlocked = true;
            if (type == PickupType.ThermalModule) abilities.thermalUnlocked = true;
            if (type == PickupType.SpeedModule) abilities.speedUnlocked = true;
        }

        if (somApanhar != null) AudioSource.PlayClipAtPoint(somApanhar, transform.position);

        Debug.Log("Apanhado: " + type);
        Destroy(gameObject);
    }
}
