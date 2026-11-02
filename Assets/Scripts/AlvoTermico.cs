using System.Collections.Generic;
using UnityEngine;

// Fica a vermelho quando o Visor térmico (tecla 4) está ligado. Vê-se mesmo no escuro,
// mas não através das paredes.
// Pôr no objeto principal de cada soldado, drone e do líder (o mesmo do Enemy / EnemyDrone).
// Mais tarde, também nas minas.
// Os inimigos mortos ficam frios: deixam de ficar vermelhos.
public class AlvoTermico : MonoBehaviour
{
    [Tooltip("Vazio: usa todas as malhas deste objeto e dos filhos (menos o ícone do minimapa, lasers e partículas).")]
    public Renderer[] malhas;

    // o que cada malha tinha antes de ficar vermelha, para repor no fim
    class Guardado
    {
        public Renderer malha;
        public Material[] originais;
        public Material[] termicos;
    }

    readonly List<Guardado> guardados = new List<Guardado>();
    bool aplicado;
    Enemy soldado;
    EnemyDrone drone;

    void Awake()
    {
        soldado = GetComponentInParent<Enemy>();
        drone = GetComponentInParent<EnemyDrone>();
    }

    void Update()
    {
        bool quer = VisorTermico.Ligado && VisorTermico.MaterialAlvos != null && !EstaMorto();

        if (quer && !aplicado) Aplicar();
        else if (!quer && aplicado) Repor();
    }

    void OnDisable()
    {
        if (aplicado) Repor();
    }

    bool EstaMorto()
    {
        if (soldado != null) return soldado.presentHealth <= 0f;
        if (drone != null) return drone.presentHealth <= 0f;
        return false;   // minas e outros objetos sem vida
    }

    void Aplicar()
    {
        guardados.Clear();
        Renderer[] lista = (malhas != null && malhas.Length > 0) ? malhas : GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in lista)
        {
            if (r == null) continue;
            // só malhas: lasers (Line Renderer), partículas e rastos ficam como estão
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            // o ícone do minimapa não muda de cor
            if (r.GetComponent<IconeMinimapa>() != null) continue;

            Material[] originais = r.sharedMaterials;
            Material[] termicos = new Material[originais.Length];
            for (int i = 0; i < termicos.Length; i++) termicos[i] = VisorTermico.MaterialAlvos;

            r.sharedMaterials = termicos;
            guardados.Add(new Guardado { malha = r, originais = originais, termicos = termicos });
        }

        aplicado = true;
    }

    void Repor()
    {
        foreach (Guardado g in guardados)
        {
            if (g.malha == null) continue;
            // se outro script mudou os materiais entretanto (por exemplo o drone ao morrer), não mexe
            if (!MesmosMateriais(g.malha.sharedMaterials, g.termicos)) continue;
            g.malha.sharedMaterials = g.originais;
        }

        guardados.Clear();
        aplicado = false;
    }

    static bool MesmosMateriais(Material[] a, Material[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}
