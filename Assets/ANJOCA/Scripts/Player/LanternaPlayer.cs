using UnityEngine;
using UnityEngine.InputSystem;

// Lanterna do jogador: liga e desliga com uma tecla (F por defeito).
// Pôr no objeto "Lanterna" que está dentro da Main Camera do Player (o que tem a Light).
// Liga e desliga só o componente Light: o objeto e este script continuam ativos.
public class LanternaPlayer : MonoBehaviour
{
    [Tooltip("A luz da lanterna. Se ficar vazio, usa a Light deste objeto.")]
    public Light luz;

    [Tooltip("Tecla para ligar e desligar.")]
    public Key tecla = Key.F;

    [Tooltip("Ligada = a lanterna já vem acesa quando o nível começa.")]
    public bool comecaLigada = false;

    [Header("Sons (opcionais)")]
    public AudioSource audioSource;
    public AudioClip somLigar;
    public AudioClip somDesligar;

    // para outros scripts (por exemplo, no futuro, os inimigos verem-te mais longe com a lanterna acesa)
    public bool Ligada => luz != null && luz.enabled;
    public static LanternaPlayer Instancia { get; private set; }

    void Awake()
    {
        if (luz == null) luz = GetComponent<Light>();
        if (luz == null)
        {
            Debug.LogWarning(name + ": LanternaPlayer sem Light. Põe o script no objeto da lanterna.");
            enabled = false;
            return;
        }
        Instancia = this;
        luz.enabled = comecaLigada;
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    void Update()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current[tecla].wasPressedThisFrame) return;

        luz.enabled = !luz.enabled;

        AudioClip som = luz.enabled ? somLigar : somDesligar;
        if (audioSource != null && som != null) audioSource.PlayOneShot(som);
    }
}
