using UnityEngine;

// Mina do caminho com minas. Quem a pisa morre: o jogador, os soldados e o líder.
// Os drones voam, por isso nunca a pisam.
//
// Fica à superfície: ao arrancar, a mina procura o chão por baixo (ou por cima) dela e cola-se a ele,
// inclinada como o terreno. Não é preciso acertar a altura à mão.
// O Mesh Renderer da malha fica desligado: só liga com o Visor térmico (tecla 4) ligado,
// e aí a mina aparece a vermelho, com o AlvoTermico.
//
// Montagem (guia em explicacoes/Caminho_Minas.md):
//  - objeto-pai "Mina", com este script e o AlvoTermico (é acrescentado sozinho);
//  - a malha (cilindro achatado) como filha, sem collider.
// Não precisa de collider: a mina procura quem está em cima dela com uma esfera (Physics.OverlapSphere).
// Um trigger não servia para os soldados, porque não têm Rigidbody e o Unity não avisa o trigger.
[RequireComponent(typeof(AlvoTermico))]
public class Mina : MonoBehaviour
{
    [Header("Ativação")]
    [Tooltip("Metros à volta do centro da mina que a fazem rebentar. Na Scene aparece como uma esfera vermelha.")]
    public float raioAtivacao = 0.5f;

    [Header("Colar ao chão")]
    [Tooltip("Ligado: ao arrancar, a mina procura o chão e cola-se a ele.")]
    public bool colarAoChao = true;
    [Tooltip("Procura o chão até esta distância, para cima e para baixo da mina (metros).")]
    public float procurarChao = 2f;
    [Tooltip("Ligado: a mina inclina-se como o terreno. Desligado: fica sempre direita.")]
    public bool inclinarComTerreno = true;
    [Tooltip("Camadas que contam como chão (terreno, estrada). Everything também serve.")]
    public LayerMask camadasChao = ~0;

    [Header("Explosão")]
    [Tooltip("Dano a quem a pisa. 100000 mata sempre: o jogador com a Blindagem ligada (e com a vida de teste a 1000) e o líder com o colete.")]
    public float dano = 100000f;
    [Tooltip("Opcional: prefab de partículas, por exemplo o Explosao do drone.")]
    public GameObject efeitoExplosao;
    [Tooltip("Segundos até apagar o efeito da explosão.")]
    public float duracaoEfeito = 4f;
    [Tooltip("Por exemplo Drone_Explosao_1 e Drone_Explosao_2: escolhe um ao calhas.")]
    public AudioClip[] sonsExplosao;
    [Range(0f, 1f)] public float volumeSom = 1f;

    [Header("Visibilidade")]
    [Tooltip("Ligado: o Mesh Renderer só liga com o Visor térmico. Desligado: vê-se sempre (para testar).")]
    public bool soComVisor = true;
    [Tooltip("Com o visor ligado, só aparece até esta distância do jogador (metros). 0 = sem limite.")]
    public float alcanceVisor = 25f;

    Renderer[] malhas;
    Transform jogador;
    bool rebentou;
    readonly Collider[] encontrados = new Collider[16];

    void Awake()
    {
        if (colarAoChao)
        {
            Vector3 ponto, normal;
            if (ProcurarChao(out ponto, out normal))
            {
                transform.position = ponto;
                if (inclinarComTerreno)
                    transform.rotation = Quaternion.FromToRotation(transform.up, normal) * transform.rotation;
            }
            else
            {
                Debug.LogWarning(name + ": não encontrei chão a menos de " + procurarChao + " m. A mina fica onde está.");
            }
        }

        // só malhas, como no AlvoTermico (partículas e lasers ficam de fora)
        Renderer[] todos = GetComponentsInChildren<Renderer>(true);
        int n = 0;
        foreach (Renderer r in todos)
            if (r is MeshRenderer || r is SkinnedMeshRenderer) n++;

        malhas = new Renderer[n];
        n = 0;
        foreach (Renderer r in todos)
            if (r is MeshRenderer || r is SkinnedMeshRenderer) malhas[n++] = r;

        // começa com o Mesh Renderer desligado (liga só com o visor)
        AtualizarVisibilidade();
    }

    void Start()
    {
        PlayerStats stats = FindFirstObjectByType<PlayerStats>();
        if (stats != null) jogador = stats.transform;
    }

    void Update()
    {
        if (rebentou) return;
        AtualizarVisibilidade();
        VerificarPisada();
    }

    // ---------- Chão ----------
    // Lança um raio de cima para baixo, de procurarChao acima da mina até procurarChao abaixo,
    // e fica com o chão mais perto da mina. Assim serve se a mina estiver um pouco acima ou abaixo do terreno.
    // Ignora a própria mina, triggers, o jogador e os inimigos.
    bool ProcurarChao(out Vector3 ponto, out Vector3 normal)
    {
        Vector3 origem = transform.position + Vector3.up * procurarChao;
        RaycastHit[] hits = Physics.RaycastAll(origem, Vector3.down, procurarChao * 2f,
                                               camadasChao, QueryTriggerInteraction.Ignore);

        float melhor = float.MaxValue;
        ponto = transform.position;
        normal = Vector3.up;
        bool achou = false;

        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<PlayerStats>() != null) continue;
            if (h.collider.GetComponentInParent<Enemy>() != null) continue;
            if (h.collider.GetComponentInParent<EnemyDrone>() != null) continue;

            float distanciaMina = Mathf.Abs(h.point.y - transform.position.y);
            if (distanciaMina < melhor)
            {
                melhor = distanciaMina;
                ponto = h.point;
                normal = h.normal;
                achou = true;
            }
        }
        return achou;
    }

    // ---------- Visibilidade ----------
    void AtualizarVisibilidade()
    {
        bool visivel = !soComVisor || (VisorTermico.Ligado && PertoDoJogador());
        foreach (Renderer r in malhas)
            if (r != null && r.enabled != visivel) r.enabled = visivel;
    }

    bool PertoDoJogador()
    {
        if (alcanceVisor <= 0f || jogador == null) return true;
        return (jogador.position - transform.position).sqrMagnitude <= alcanceVisor * alcanceVisor;
    }

    // ---------- Pisar ----------
    // Procura colliders sólidos dentro da esfera (os triggers, como as zonas das portas, ficam de fora).
    // O jogador conta pelo CharacterController; os soldados e o líder pelo Capsule Collider.
    void VerificarPisada()
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position, raioAtivacao, encontrados,
                                              ~0, QueryTriggerInteraction.Ignore);
        if (n == 0) return;

        for (int i = 0; i < n; i++)
        {
            if (AlvoVivo(encontrados[i]))
            {
                Rebentar(n);
                return;
            }
        }
    }

    // é o jogador, um soldado ou o líder, e está vivo (um corpo morto em cima da mina não a faz rebentar)
    bool AlvoVivo(Collider c)
    {
        PlayerStats stats = c.GetComponentInParent<PlayerStats>();
        if (stats != null) return !stats.isDead;

        Enemy soldado = c.GetComponentInParent<Enemy>();
        if (soldado != null) return soldado.presentHealth > 0f;

        return false;
    }

    void Rebentar(int n)
    {
        rebentou = true;

        if (efeitoExplosao != null)
        {
            GameObject efeito = Instantiate(efeitoExplosao, transform.position, Quaternion.identity);
            Destroy(efeito, duracaoEfeito);
        }

        if (sonsExplosao != null && sonsExplosao.Length > 0)
        {
            AudioClip som = sonsExplosao[Random.Range(0, sonsExplosao.Length)];
            if (som != null) AudioSource.PlayClipAtPoint(som, transform.position, volumeSom);
        }

        // mata todos os que estão em cima da mina (o mesmo soldado pode ter vários colliders: só leva dano uma vez)
        PlayerStats jogadorAtingido = null;
        for (int i = 0; i < n; i++)
        {
            Collider c = encontrados[i];
            if (c == null) continue;

            PlayerStats stats = c.GetComponentInParent<PlayerStats>();
            if (stats != null)
            {
                if (stats != jogadorAtingido && !stats.isDead)
                {
                    jogadorAtingido = stats;
                    Debug.Log("Mina pisada pelo jogador");
                    // a função de dano de sempre: queda, "Sinal perdido" e aviso ao terminal
                    stats.TakeDamage(dano);
                }
                continue;
            }

            Enemy soldado = c.GetComponentInParent<Enemy>();
            if (soldado != null && soldado.presentHealth > 0f)
            {
                Debug.Log("Mina pisada por " + soldado.name);
                // a mesma função dos tiros: animação de morte, sons e Destroy 5 s depois.
                // depois do primeiro dano fica com vida <= 0, por isso os outros colliders dele já não contam
                soldado.enemyHitDamage(dano);
            }
        }

        Destroy(gameObject);
    }

    // Na Scene: esfera vermelha = zona que faz rebentar, já no sítio onde a mina vai ficar colada ao chão.
    // Linha amarela = da posição atual até ao chão (só aparece se a mina não estiver já no chão).
    void OnDrawGizmos()
    {
        Vector3 ponto = transform.position;
        if (!Application.isPlaying && colarAoChao)
        {
            Vector3 normal;
            if (ProcurarChao(out ponto, out normal) && (ponto - transform.position).sqrMagnitude > 0.0001f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, ponto);
            }
        }

        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(ponto, raioAtivacao);
    }
}
