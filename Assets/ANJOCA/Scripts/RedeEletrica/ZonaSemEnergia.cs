using UnityEngine;
using System.Collections.Generic;

// Caixa invisível que marca uma parte da ala alimentada por um QuadroEletrico.
// Quando o quadro é sabotado, a caixa fica às escuras: os soldados que estiverem lá dentro
// acendem a lanterna e passam a ver menos (Enemy.cs). Se saírem para uma zona com luz, voltam ao normal.
// Não tem Collider: o tamanho da caixa é a Scale do objeto (ferramenta Scale, tecla R) e pode ser rodada.
// Uma ala com forma irregular leva várias caixas, todas a apontar para o mesmo quadro.
public class ZonaSemEnergia : MonoBehaviour
{
    // quadro que alimenta esta zona. Vazio = procura nos pais
    public QuadroEletrico quadro;
    // cor da caixa na Scene (fica azul escuro em Play quando o quadro é sabotado)
    public Color corGizmo = new Color(1f, 0.85f, 0.2f, 0.12f);

    // todas as zonas ativas da cena, para os soldados perguntarem sem ter de as procurar
    static readonly List<ZonaSemEnergia> zonas = new List<ZonaSemEnergia>();

    void Awake()
    {
        if (quadro == null) quadro = GetComponentInParent<QuadroEletrico>();
        if (quadro == null) Debug.LogWarning(name + ": ZonaSemEnergia sem quadro elétrico");
    }

    void OnEnable() { zonas.Add(this); }
    void OnDisable() { zonas.Remove(this); }

    // o ponto está dentro desta caixa? (passa o ponto para as coordenadas da caixa, onde ela é um cubo de 1 x 1 x 1)
    public bool Contem(Vector3 ponto)
    {
        Vector3 local = transform.InverseTransformPoint(ponto);
        return Mathf.Abs(local.x) <= 0.5f && Mathf.Abs(local.y) <= 0.5f && Mathf.Abs(local.z) <= 0.5f;
    }

    // chamada pelo Enemy: o ponto está numa zona cujo quadro já foi sabotado?
    public static bool NoEscuro(Vector3 ponto)
    {
        foreach (ZonaSemEnergia zona in zonas)
            if (zona.quadro != null && zona.quadro.sabotado && zona.Contem(ponto)) return true;
        return false;
    }

    // a caixa aparece sempre na Scene (não só quando está selecionada), para veres onde ficam as alas
    void OnDrawGizmos()
    {
        bool escuro = Application.isPlaying && quadro != null && quadro.sabotado;
        Color cor = escuro ? new Color(0.1f, 0.15f, 0.6f, 0.3f) : corGizmo;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = cor;
        Gizmos.DrawCube(Vector3.zero, Vector3.one);
        cor.a = 1f;
        Gizmos.color = cor;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}
