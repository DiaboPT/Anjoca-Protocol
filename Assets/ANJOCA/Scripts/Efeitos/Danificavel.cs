using UnityEngine;
using System.Collections;

// Equipamento que uma explosão pode avariar. Por agora, só a da porta principal (PortaExplosivos),
// que o tem na lista Equipamentos. Vai no próprio equipamento (caixa elétrica, candeeiro, câmara...)
// e não precisa de collider.
// Quando avaria:
//  - as Luzes piscam um bocado e apagam-se;
//  - os objetos de Desligar desligam-se e os de Ligar ligam-se (faíscas, fumo, o modelo partido);
//  - as Camaras ficam desligadas até ao fim do nível;
//  - se tiver um Quadro, corta-lhe a energia, como se o jogador o sabotasse
//    (luzes de emergência, câmaras, estações de energia e zonas às escuras, com os soldados de lanterna).
// Guia: explicacoes/Porta_Principal_Explosivos.md
public class Danificavel : MonoBehaviour
{
    [Tooltip("Probabilidade de avariar quando há uma explosão (0 = nunca, 1 = sempre).")]
    [Range(0f, 1f)] public float probabilidade = 0.5f;

    [Header("O que avaria")]
    [Tooltip("Luzes que piscam e se apagam (o componente Light de cada uma).")]
    public Light[] luzes;
    [Tooltip("Segundos a piscar antes de se apagarem.")]
    public float tempoPiscar = 1.2f;
    [Tooltip("Objetos que se desligam (por exemplo o modelo inteiro, ou a luz verde de estado).")]
    public GameObject[] desligar;
    [Tooltip("Objetos que se ligam (faíscas, fumo, o modelo partido). Começam desligados.")]
    public GameObject[] ligar;
    public CamaraVigilancia[] camaras;
    [Tooltip("Opcional: quadro elétrico a que a avaria corta a energia.")]
    public QuadroEletrico quadro;

    [Header("Som (opcional)")]
    public AudioClip somAvaria;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Estado (ver durante o Play)")]
    public bool danificado = false;

    void Awake()
    {
        foreach (GameObject g in ligar)
            if (g != null) g.SetActive(false);
    }

    // chamada pela explosão: tira à sorte se avaria. Devolve verdadeiro se avariou
    public bool TentarDanificar()
    {
        if (danificado) return false;
        if (Random.value >= probabilidade) return false;
        Danificar();
        return true;
    }

    // avaria sempre (para o Modo Debug ou para um tiro, mais tarde)
    public void Danificar()
    {
        if (danificado) return;
        danificado = true;

        foreach (GameObject g in desligar)
            if (g != null) g.SetActive(false);
        foreach (GameObject g in ligar)
            if (g != null) g.SetActive(true);

        // Mathf.Infinity = até ao fim do nível
        foreach (CamaraVigilancia c in camaras)
            if (c != null) c.Desligar(Mathf.Infinity);

        if (quadro != null) quadro.Sabotar();

        if (somAvaria != null) AudioSource.PlayClipAtPoint(somAvaria, transform.position, volume);

        if (luzes != null && luzes.Length > 0) StartCoroutine(Piscar());

        Debug.Log(name + ": avariado pela explosão");
    }

    // acende e apaga ao calhas durante tempoPiscar segundos e no fim fica apagada
    IEnumerator Piscar()
    {
        float fim = Time.time + tempoPiscar;
        while (Time.time < fim)
        {
            bool acesa = Random.value > 0.5f;
            foreach (Light l in luzes)
                if (l != null) l.enabled = acesa;
            yield return new WaitForSeconds(Random.Range(0.04f, 0.15f));
        }
        foreach (Light l in luzes)
            if (l != null) l.enabled = false;
    }
}
