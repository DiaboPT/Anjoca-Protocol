using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

// Seta a flutuar por cima de um objeto, a apontar para baixo, para o jogador o ver de longe
// (feita para a Carga_Explosiva, mas serve para o cartão, os módulos ou outro objetivo).
// Vai num objeto vazio, FILHO do objeto a assinalar. A seta é desenhada pelo script: não precisa de modelo 3D.
// Sobe e desce devagar e roda sobre si própria. Não tem collider, por isso não atrapalha nem apanha tiros.
// Como é filha, desaparece com o objeto: a CargaExplosiva esconde os filhos quando é apanhada,
// e o CartaoAcesso apaga-se com os filhos.
// Na Scene (sem Play) aparece só o contorno, na cor escolhida, para veres onde fica.
// Guia: explicacoes/Porta_Principal_Explosivos.md, passo 10.1
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SetaObjetivo : MonoBehaviour
{
    [Header("Forma (metros)")]
    [Tooltip("Altura total da seta, da ponta ao topo.")]
    public float altura = 0.9f;
    [Tooltip("Largura da cabeça da seta (a pirâmide da ponta).")]
    public float larguraPonta = 0.4f;
    [Tooltip("Altura da cabeça da seta.")]
    public float alturaPonta = 0.4f;
    [Tooltip("Largura da haste.")]
    public float larguraHaste = 0.14f;

    [Header("Movimento")]
    [Tooltip("Metros que sobe e desce.")]
    public float amplitude = 0.15f;
    [Tooltip("Rapidez do sobe e desce.")]
    public float velocidadeSubir = 2.5f;
    [Tooltip("Graus por segundo a rodar (0 = não roda).")]
    public float velocidadeRodar = 90f;

    [Header("Aspeto")]
    [Tooltip("Material da seta (por exemplo HDRP/Unlit amarelo). Vazio = cria um HDRP/Unlit com a Cor abaixo.")]
    public Material material;
    public Color cor = new Color(1f, 0.8f, 0.1f);

    Vector3 posicaoInicial;

    void Awake()
    {
        GetComponent<MeshFilter>().mesh = CriarSeta();

        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;   // não faz sombra no chão
        mr.receiveShadows = false;

        if (material != null)
        {
            mr.sharedMaterial = material;
        }
        else
        {
            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader != null)
            {
                Material m = new Material(shader);
                m.SetColor("_UnlitColor", cor);
                mr.material = m;
            }
            else
            {
                Debug.LogWarning(name + ": não encontrei o shader HDRP/Unlit. Arrasta um material para o campo Material");
            }
        }

        posicaoInicial = transform.localPosition;
    }

    void Update()
    {
        transform.localPosition = posicaoInicial + Vector3.up * (Mathf.Sin(Time.time * velocidadeSubir) * amplitude);
        if (velocidadeRodar != 0f) transform.Rotate(0f, velocidadeRodar * Time.deltaTime, 0f, Space.Self);
    }

    // ---------- A malha da seta ----------
    // A ponta fica na origem do objeto, a apontar para baixo. Por cima, a cabeça (pirâmide de 4 lados)
    // e depois a haste (caixa). Cada face tem os seus vértices, para as arestas ficarem vincadas.
    Mesh CriarSeta()
    {
        List<Vector3> v = new List<Vector3>();
        List<Vector3> n = new List<Vector3>();
        List<int> t = new List<int>();

        float w = larguraPonta / 2f;
        float h = alturaPonta;
        float s = larguraHaste / 2f;
        float a = Mathf.Max(altura, alturaPonta + 0.05f);

        // cabeça: ponta em baixo e base quadrada em cima
        Vector3 ponta = Vector3.zero;
        Vector3[] baseCabeca =
        {
            new Vector3(-w, h, -w), new Vector3(w, h, -w), new Vector3(w, h, w), new Vector3(-w, h, w)
        };
        Vector3 centroCabeca = new Vector3(0f, h * 0.6f, 0f);
        for (int i = 0; i < 4; i++)
            Triangulo(v, n, t, ponta, baseCabeca[i], baseCabeca[(i + 1) % 4], centroCabeca);
        Quadrado(v, n, t, baseCabeca[0], baseCabeca[1], baseCabeca[2], baseCabeca[3], centroCabeca);

        // haste: caixa da base da cabeça até ao topo
        Vector3 centroHaste = new Vector3(0f, (h + a) / 2f, 0f);
        Vector3[] baixo =
        {
            new Vector3(-s, h, -s), new Vector3(s, h, -s), new Vector3(s, h, s), new Vector3(-s, h, s)
        };
        Vector3[] cima =
        {
            new Vector3(-s, a, -s), new Vector3(s, a, -s), new Vector3(s, a, s), new Vector3(-s, a, s)
        };
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            Quadrado(v, n, t, baixo[i], baixo[j], cima[j], cima[i], centroHaste);
        }
        Quadrado(v, n, t, cima[0], cima[1], cima[2], cima[3], centroHaste);

        Mesh m = new Mesh();
        m.name = "Seta";
        m.SetVertices(v);
        m.SetNormals(n);
        m.SetTriangles(t, 0);
        m.RecalculateBounds();
        return m;
    }

    // um triângulo virado para fora (afasta-se do centro da peça), com a normal da face
    static void Triangulo(List<Vector3> v, List<Vector3> n, List<int> t,
                          Vector3 a, Vector3 b, Vector3 c, Vector3 centro)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);
        // no Unity a frente do triângulo é para onde aponta Cross(b - a, c - a): se aponta para dentro, troca a ordem
        if (Vector3.Dot(normal, (a + b + c) / 3f - centro) < 0f)
        {
            Vector3 troca = b; b = c; c = troca;
            normal = -normal;
        }
        normal.Normalize();

        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        n.Add(normal); n.Add(normal); n.Add(normal);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    static void Quadrado(List<Vector3> v, List<Vector3> n, List<int> t,
                         Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 centro)
    {
        Triangulo(v, n, t, a, b, c, centro);
        Triangulo(v, n, t, a, c, d, centro);
    }

    // Na Scene: o contorno da seta, para a pores no sítio sem carregar Play
    void OnDrawGizmos()
    {
        Gizmos.color = cor;
        Gizmos.matrix = transform.localToWorldMatrix;
        float w = larguraPonta / 2f;
        float h = alturaPonta;
        float s = larguraHaste / 2f;
        Vector3[] baseCabeca =
        {
            new Vector3(-w, h, -w), new Vector3(w, h, -w), new Vector3(w, h, w), new Vector3(-w, h, w)
        };
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(Vector3.zero, baseCabeca[i]);
            Gizmos.DrawLine(baseCabeca[i], baseCabeca[(i + 1) % 4]);
        }
        Gizmos.DrawWireCube(new Vector3(0f, (h + altura) / 2f, 0f), new Vector3(s * 2f, altura - h, s * 2f));
    }
}
