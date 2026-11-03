using UnityEngine;

// Pack de vida (+30) ou módulo de aprimoramento.
// Pôr num objeto com Collider marcado como "Is Trigger".
public class Pickup : MonoBehaviour
{
    // Os novos ficam no fim da lista, para os objetos que já estão na cena não mudarem de tipo
    public enum PickupType { HealthPack, JumpModule, ArmorModule, PrecisionModule, ThermalModule, SpeedModule }

    public PickupType type = PickupType.HealthPack;
    public float healAmount = 30f;

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

        SuitAbilities abilities = stats.GetComponent<SuitAbilities>();
        if (abilities != null)
        {
            if (type == PickupType.JumpModule) abilities.jumpUnlocked = true;
            if (type == PickupType.ArmorModule) abilities.armorUnlocked = true;
            if (type == PickupType.PrecisionModule) abilities.precisionUnlocked = true;
            if (type == PickupType.ThermalModule) abilities.thermalUnlocked = true;
            if (type == PickupType.SpeedModule) abilities.speedUnlocked = true;
        }

        Debug.Log("Apanhado: " + type);
        Destroy(gameObject);
    }
}
