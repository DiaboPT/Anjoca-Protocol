using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// HUD provisório + teclas de teste. Pôr no jogador (junto dos outros scripts).
// K = tiro de soldado (10 dano) · L = laser do drone (10 dano + 20 energia) · H = curar 30
public class DebugHUD : MonoBehaviour
{
    PlayerStats stats;
    SuitAbilities abilities;
    PlayerMovement movement;
    Weapon arma;                             // para o contador de balas
    GUIStyle estiloBalas;                    // letra maior, alinhada à direita
    
    public TextMeshProUGUI textoResultado;   // o TextoResultado do Canvas
    bool mostrouDerrota = false;             // para escrever só uma vez

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        abilities = GetComponent<SuitAbilities>();
        movement = GetComponent<PlayerMovement>();
        // o Weapon está dentro da Main Camera, que é filha do jogador
        arma = GetComponentInChildren<Weapon>(true);
    }

    void Update()
    {
        // morreu: escreve a derrota no TextoResultado (uma só vez)
        if (stats != null && stats.isDead && !mostrouDerrota && textoResultado != null)
        {
            mostrouDerrota = true;
            if (!textoResultado.text.StartsWith("MISSÃO"))   // não apaga uma vitória já mostrada
                textoResultado.text = "SINAL PERDIDO\nPerdeste";
        }
        
        Keyboard kb = Keyboard.current;
        if (kb == null || stats == null) return;
        
        if (!ModoTeste.Ativo) return;   // fora do modo teste, as teclas não fazem nada
        if (kb.kKey.wasPressedThisFrame) stats.TakeDamage(10f);
        if (kb.lKey.wasPressedThisFrame)
        {
            stats.TakeDamage(10f);
            stats.DrainEnergy(20f);
        }
        if (kb.hKey.wasPressedThisFrame) stats.Heal(30f);
    }

    void OnGUI()
    {
        if (stats == null) return;
        
        DrawBar(20, stats.health / stats.maxHealth, new Color(0.9f, 0.2f, 0.2f), "VIDA HUMANA: " + stats.health.ToString("0"));
        DrawBar(48, stats.energy / stats.maxEnergy, new Color(0.2f, 0.6f, 1f), "FATO: " + stats.energy.ToString("0"));

        if (abilities != null && movement != null)
        {
            GUI.Label(new Rect(20, 76, 400, 20), "[1] Salto " + OnOff(abilities.jumpActive) + "  (altura " + movement.GetJumpHeight() + " m)");
            GUI.Label(new Rect(20, 94, 400, 20), "[2] Blindagem " + OnOff(abilities.armorActive));
            GUI.Label(new Rect(20, 112, 400, 20), "[3] Precisão " + OnOff(abilities.precisionActive));
            GUI.Label(new Rect(20, 130, 400, 20), "[4] Visor térmico " + OnOff(abilities.thermalActive));
            GUI.Label(new Rect(20, 148, 400, 20), "[5] Velocidade " + OnOff(abilities.speedActive) + "  · gasto " + abilities.GetDrainPerSecond().ToString("0") + "/s");
        }
        if (ModoTeste.Ativo)
            GUI.Label(new Rect(20, 172, 400, 20), "Teste: K dano · L laser drone · H curar");

        // balas no canto de baixo à direita (BALAS: 30 / 90, A RECARREGAR... ou SEM BALAS)
        if (arma != null)
        {
            if (estiloBalas == null)
                estiloBalas = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            GUI.Label(new Rect(Screen.width - 320, Screen.height - 50, 300, 30), arma.TextoBalas, estiloBalas);
        }

        // if (stats.isDead)
        //     GUI.Label(new Rect(Screen.width / 2 - 60, Screen.height / 2, 200, 30), "SINAL PERDIDO");
    }

    string OnOff(bool active)
    {
        if (active) return "LIGADO";
        return "-";
    }

    // Barra: fundo preto + parte colorida proporcional ao valor (fill entre 0 e 1)
    void DrawBar(float y, float fill, Color color, string label)
    {
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(20, y, 250, 22), Texture2D.whiteTexture);

        GUI.color = color;
        GUI.DrawTexture(new Rect(22, y + 2, 246 * Mathf.Clamp01(fill), 18), Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(new Rect(26, y + 2, 250, 22), label);
    }
}
