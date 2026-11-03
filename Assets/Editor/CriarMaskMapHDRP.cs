using System.IO;
using UnityEditor;
using UnityEngine;

// Junta a Roughness, a Metalness e a Ambient Occlusion de uma textura PBR numa Mask Map do HDRP.
// Mask Map: R = Metallic, G = Ambient Occlusion, B = Detail Mask (1), A = Smoothness (1 - Roughness)
// Uso: seleciona no Project a Roughness e, se houver, a Metalness e a AO (Ctrl+clique) > Tools > ANJOCA > Criar Mask Map HDRP
public static class CriarMaskMapHDRP
{
    [MenuItem("Tools/ANJOCA/Criar Mask Map HDRP")]
    static void Criar()
    {
        Texture2D rough = null, metal = null, ao = null;
        foreach (var obj in Selection.objects)
        {
            if (obj is not Texture2D tex) continue;
            string nome = tex.name.ToLower();
            if (nome.Contains("rough")) rough = tex;
            else if (nome.Contains("metalness") || nome.Contains("metallic")) metal = tex;
            else if (nome.Contains("ao") || nome.Contains("occlusion")) ao = tex;
        }

        if (rough == null)
        {
            EditorUtility.DisplayDialog("Mask Map", "Seleciona pelo menos a textura de Roughness (e, se houver, a de AO).", "OK");
            return;
        }

        Color[] r = Ler(rough, out int w, out int h);
        Color[] a = ao != null ? Ler(ao, out _, out _, w, h) : null;
        Color[] m = metal != null ? Ler(metal, out _, out _, w, h) : null;

        var saida = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++)
        {
            float oclusao = a != null ? a[i].r : 1f;
            float metalico = m != null ? m[i].r : 0f;
            px[i] = new Color(metalico, oclusao, 1f, 1f - r[i].r);
        }
        saida.SetPixels(px);
        saida.Apply();

        string pasta = Path.GetDirectoryName(AssetDatabase.GetAssetPath(rough));
        string caminho = Path.Combine(pasta, rough.name.Replace("Roughness", "") + "MaskMap.png").Replace("\\", "/");
        File.WriteAllBytes(caminho, saida.EncodeToPNG());
        Object.DestroyImmediate(saida);
        AssetDatabase.ImportAsset(caminho);

        // A Mask Map são dados, não cor: tem de ficar sem sRGB
        var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
        imp.sRGBTexture = false;
        imp.SaveAndReimport();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        Debug.Log("Mask Map criada: " + caminho);
    }

    // Lê os píxeis mesmo que a textura não esteja marcada como Read/Write
    static Color[] Ler(Texture2D tex, out int w, out int h, int largura = 0, int altura = 0)
    {
        w = largura > 0 ? largura : tex.width;
        h = altura > 0 ? altura : tex.height;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(tex, rt);
        var anterior = RenderTexture.active;
        RenderTexture.active = rt;
        var copia = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        copia.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        copia.Apply();
        RenderTexture.active = anterior;
        RenderTexture.ReleaseTemporary(rt);
        Color[] px = copia.GetPixels();
        Object.DestroyImmediate(copia);
        return px;
    }
}
