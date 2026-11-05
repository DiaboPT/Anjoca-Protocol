using UnityEngine;
using UnityEngine.InputSystem;

// Aprimoramentos do Fato: 1 Salto reforçado · 2 Blindagem · 3 Precisão · 4 Visor térmico · 5 Velocidade.
// Cada tecla liga/desliga. Enquanto está ligado, gasta energia por segundo.
// Vários ligados ao mesmo tempo somam o gasto. Sem energia, desliga tudo.
[RequireComponent(typeof(PlayerStats))]
public class SuitAbilities : MonoBehaviour
{
    public float minEnergyToActivate = 10f;   // energia mínima para conseguir ligar

    [Header("Módulos apanhados (true para testar já)")]
    public bool jumpUnlocked = true;
    public bool armorUnlocked = true;
    public bool precisionUnlocked = true;
    public bool thermalUnlocked = false;      // módulo novo: começa por apanhar
    public bool speedUnlocked = false;        // módulo novo: começa por apanhar

    [Header("Salto reforçado")]
    public float boostedJumpHeight = 3f;      // o salto normal está no PlayerMovement
    public float jumpDrainPerSecond = 3f;

    [Header("Blindagem")]
    [Range(0f, 1f)] public float armorDamageMultiplier = 0.5f;   // 0.5 = metade do dano
    public float armorDrainPerSecond = 5f;

    [Header("Precisão")]
    public float precisionDrainPerSecond = 4f;

    [Header("Visor térmico")]
    public float thermalDrainPerSecond = 5f;  // o efeito está no VisorTermico (ecrã) e no AlvoTermico (inimigos)

    [Header("Velocidade")]
    public float speedMultiplier = 2f;        // 2 = o dobro (10 m/s a andar, 16 m/s a correr)
    public float speedDrainPerSecond = 6f;

    [Header("Estado atual (ver durante o Play)")]
    public bool jumpActive;
    public bool armorActive;
    public bool precisionActive;
    public bool thermalActive;
    public bool speedActive;

    PlayerStats stats;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
    }

    void Update()
    {
        if (stats.isDead)
        {
            CancelAll();
            return;
        }

        // Teclas 1, 2, 3, 4, 5
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) jumpActive = Toggle(jumpActive, jumpUnlocked, "Salto reforçado");
            if (kb.digit2Key.wasPressedThisFrame) armorActive = Toggle(armorActive, armorUnlocked, "Blindagem");
            if (kb.digit3Key.wasPressedThisFrame) precisionActive = Toggle(precisionActive, precisionUnlocked, "Precisão");
            if (kb.digit4Key.wasPressedThisFrame) thermalActive = Toggle(thermalActive, thermalUnlocked, "Visor térmico");
            if (kb.digit5Key.wasPressedThisFrame) speedActive = Toggle(speedActive, speedUnlocked, "Velocidade");
        }

        // Gasto contínuo de energia
        float drain = GetDrainPerSecond();
        if (drain > 0f)
        {
            stats.DrainEnergy(drain * Time.deltaTime);

            if (stats.energy <= 0f)
            {
                Debug.Log("Energia do Fato esgotada: aprimoramentos desligados");
                CancelAll();
            }
        }
    }

    // Liga ou desliga um aprimoramento. Devolve o novo estado (true = ligado).
    bool Toggle(bool active, bool unlocked, string name)
    {
        if (active)
        {
            Debug.Log(name + " desligado");
            return false;
        }
        if (!unlocked)
        {
            Debug.Log(name + ": módulo ainda não apanhado");
            return false;
        }
        if (stats.energy < minEnergyToActivate)
        {
            Debug.Log(name + ": energia insuficiente");
            return false;
        }

        Debug.Log(name + " ligado");
        return true;
    }

    // Soma o gasto de tudo o que está ligado
    public float GetDrainPerSecond()
    {
        float drain = 0f;
        if (jumpActive) drain += jumpDrainPerSecond;
        if (armorActive) drain += armorDrainPerSecond;
        if (precisionActive) drain += precisionDrainPerSecond;
        if (thermalActive) drain += thermalDrainPerSecond;
        if (speedActive) drain += speedDrainPerSecond;
        return drain;
    }

    public void CancelAll()
    {
        jumpActive = false;
        armorActive = false;
        precisionActive = false;
        thermalActive = false;
        speedActive = false;
    }
}
