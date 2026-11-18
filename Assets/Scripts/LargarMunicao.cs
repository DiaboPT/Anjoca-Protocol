using UnityEngine;

// Deixa cair uma caixa de munição no chão quando o soldado (ou o líder) morre.
// Vai no MESMO objeto que tem o Enemy. Os drones e o sniper não levam este script.
// Não é preciso mexer no Enemy.cs: este script só olha para a vida dele.
// O que cai é o prefab do campo Prefab Municao (a Caixa_Municao, com um Pickup em Ammo Pack).
public class LargarMunicao : MonoBehaviour
{
    [Header("O que larga")]
    [Tooltip("O prefab Caixa_Municao (o que tem o script Pickup com o Type em Ammo Pack)")]
    public GameObject prefabMunicao;

    [Tooltip("1 = larga sempre. 0.5 = larga em metade das vezes, 0 = nunca larga")]
    [Range(0f, 1f)] public float hipoteseLargar = 1f;

    [Header("Onde cai")]
    [Tooltip("Metros acima do chão onde a caixa fica")]
    public float alturaSobreChao = 0.15f;
    [Tooltip("Até onde procura o chão por baixo do soldado")]
    public float procurarChaoAte = 6f;
    [Tooltip("Metros ao acaso para o lado, para as caixas não ficarem todas no mesmo sítio")]
    public float espalhar = 0.3f;

    [Header("Estado (ver durante o Play)")]
    public bool jaLargou = false;

    private Enemy soldado;
    // só conta como morto depois de o termos visto vivo pelo menos um frame
    // (no primeiro frame a vida ainda pode não estar preenchida)
    private bool vistoVivo = false;

    private void Awake()
    {
        soldado = GetComponent<Enemy>();

        if (soldado == null)
            Debug.LogWarning(name + ": o LargarMunicao tem de ir no mesmo objeto do Enemy (o soldado ou o líder). Aqui não faz nada");
    }

    private void Update()
    {
        if (jaLargou || soldado == null) return;

        if (soldado.presentHealth > 0f)
        {
            vistoVivo = true;
            return;
        }

        // ainda não o vimos vivo: espera pelo frame seguinte
        if (!vistoVivo) return;

        // morreu: larga uma vez só (mesmo que a sorte diga que não larga nada)
        jaLargou = true;

        if (prefabMunicao == null)
        {
            Debug.LogWarning(name + ": falta a Caixa_Municao no campo Prefab Municao. Não larga nada");
            return;
        }
        if (Random.value > hipoteseLargar) return;

        Largar();
    }

    private void Largar()
    {
        Vector3 ponto = transform.position;
        if (espalhar > 0f)
        {
            Vector2 desvio = Random.insideUnitCircle * espalhar;
            ponto += new Vector3(desvio.x, 0f, desvio.y);
        }

        Vector3 sitio = ChaoPorBaixo(ponto) + Vector3.up * alturaSobreChao;
        // roda cada caixa ao calhas, para não ficarem todas viradas da mesma maneira
        Instantiate(prefabMunicao, sitio, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

        Debug.Log(name + " deixou cair munição");
    }

    // o primeiro chão por baixo do ponto, sem contar com o corpo do soldado nem com triggers
    private Vector3 ChaoPorBaixo(Vector3 ponto)
    {
        Vector3 origem = ponto + Vector3.up * 1f;
        RaycastHit[] hits = Physics.RaycastAll(origem, Vector3.down, procurarChaoAte + 1f, ~0, QueryTriggerInteraction.Ignore);

        float melhor = float.MaxValue;
        Vector3 chao = ponto;
        foreach (RaycastHit h in hits)
        {
            // salta o próprio soldado (o corpo, a arma e as peças filhas)
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < melhor) { melhor = h.distance; chao = h.point; }
        }
        return chao;
    }
}
