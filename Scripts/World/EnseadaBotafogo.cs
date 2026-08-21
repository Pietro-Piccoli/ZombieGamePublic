
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ENSEADA DE BOTAFOGO - abre o U da baia e levanta a cidade atras da praia.
/// SEGURANCA: nunca toca terreno acima de 'alturaProtegida'.
/// SEM BORDA RETA: todo limite da area de edicao e esfumado.
/// </summary>
[ExecuteAlways]
public class EnseadaBotafogo : MonoBehaviour
{
    [Header("SEGURANCA")]
    public float alturaProtegida = 35f;

    [Header("Praia e cidade")]
    public float alturaCidade = 12f;
    public float larguraPraia = 70f;
    public float profundidadeCidade = 1400f;
    public float fundoDaEnseada = 16f;
    public float rampaDoFundo = 260f;
    [Range(0f, 8f)] public float ondulacaoDaCidade = 3f;

    [Header("Suavizacao das bordas (0 = corte seco, o que estragava)")]
    public float esfumado = 380f;

    [Header("Limites da terra firme")]
    public float xOeste = -3400f, xLeste = -1050f, zSul = -1400f;
    public float zLitoralOeste = 1650f, zLitoralLeste = 1000f;

    [Header("A margem da enseada (o formato em U)")]
    public Vector2[] margem = new Vector2[]
    {
        new Vector2(-2220f, 3000f), new Vector2(-2220f,  980f), new Vector2(-2185f,  700f),
        new Vector2(-2130f,  430f), new Vector2(-2075f,  200f), new Vector2(-1990f,   40f),
        new Vector2(-1850f,  -70f), new Vector2(-1690f, -115f), new Vector2(-1530f, -100f),
        new Vector2(-1390f,  -10f), new Vector2(-1290f,  160f), new Vector2(-1215f,  400f),
        new Vector2(-1160f,  680f), new Vector2(-1120f,  980f), new Vector2(-1120f, 3000f),
    };

    public bool DentroDaEnseada(float x, float z)
    {
        bool dentro = false;
        int n = margem.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
            if (((margem[i].y > z) != (margem[j].y > z)) &&
                (x < (margem[j].x - margem[i].x) * (z - margem[i].y) / (margem[j].y - margem[i].y) + margem[i].x))
                dentro = !dentro;
        return dentro;
    }

    public float DistanciaAteAMargem(float x, float z)
    {
        float melhor = 9e9f;
        for (int i = 0; i < margem.Length - 1; i++)
        {
            Vector2 a = margem[i], b = margem[i + 1];
            Vector2 ab = b - a, ap = new Vector2(x, z) - a;
            float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            float d = Vector2.Distance(new Vector2(x, z), a + ab * t);
            if (d < melhor) melhor = d;
        }
        return melhor;
    }

    private static float Suave(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    public float Peso(float x, float z, float d)
    {
        float m = Mathf.Max(1f, esfumado);
        float w = 1f;
        w = Mathf.Min(w, Suave((x - (xOeste - m * 0.5f)) / m));
        w = Mathf.Min(w, Suave(((xLeste + m * 0.5f) - x) / m));
        w = Mathf.Min(w, Suave((z - (zSul - m * 0.5f)) / m));
        float zLit = x <= -2250f
            ? Mathf.Lerp(zLitoralOeste, zLitoralLeste, Mathf.InverseLerp(xOeste, -2250f, x))
            : zLitoralLeste;
        w = Mathf.Min(w, Suave(((zLit + m * 0.5f) - z) / m));
        w = Mathf.Min(w, Suave(((profundidadeCidade + m * 0.5f) - d) / m));
        return w;
    }

    public float AlvoTerra(float x, float z, float d)
    {
        float r = Suave(d / larguraPraia);
        float h = Mathf.Lerp(0.4f, alturaCidade, r);
        if (r >= 0.999f)
            h += (Mathf.PerlinNoise(x * 0.004f + 5.3f, z * 0.004f + 2.9f) - 0.5f) * ondulacaoDaCidade;
        return h;
    }

#if UNITY_EDITOR
    [ContextMenu("Aplicar")]
    public void Aplicar()
    {
        var ters = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int mexidos = 0, protegidos = 0;
        float folga = esfumado + 400f;
        foreach (var ter in ters)
        {
            var td = ter.terrainData;
            Vector3 tp = ter.transform.position;
            int r = td.heightmapResolution;
            float passo = td.size.x / (r - 1);
            if (tp.x > xLeste + folga || tp.x + td.size.x < xOeste - folga) continue;
            if (tp.z > zLitoralOeste + folga || tp.z + td.size.z < zSul - folga) continue;

            float[,] h = td.GetHeights(0, 0, r, r);
            bool mudou = false;
            for (int j = 0; j < r; j++)
            {
                float wz = tp.z + j * passo;
                for (int i = 0; i < r; i++)
                {
                    float wx = tp.x + i * passo;
                    float atual = tp.y + h[j, i] * td.size.y;
                    if (atual > alturaProtegida) { protegidos++; continue; }

                    float d = DistanciaAteAMargem(wx, wz);
                    float novo;
                    if (DentroDaEnseada(wx, wz))
                    {
                        float alvo = -fundoDaEnseada * Suave(d / rampaDoFundo);
                        novo = Mathf.Min(atual, alvo);
                    }
                    else
                    {
                        float p = Peso(wx, wz, d);
                        if (p <= 0.001f) continue;
                        p *= 1f - Mathf.Clamp01((atual - (alturaProtegida - 18f)) / 18f);
                        if (p <= 0.001f) continue;
                        novo = Mathf.Lerp(atual, AlvoTerra(wx, wz, d), p);
                    }
                    if (novo > alturaProtegida) novo = alturaProtegida;
                    float norm = Mathf.Clamp01((novo - tp.y) / td.size.y);
                    if (Mathf.Abs(norm - h[j, i]) > 0.00001f) { h[j, i] = norm; mudou = true; mexidos++; }
                }
            }
            if (mudou) { td.SetHeights(0, 0, h); EditorUtility.SetDirty(td); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Enseada] alterados: " + mexidos + "  protegidos: " + protegidos);
    }

    /// <summary>
    /// Mata parede vertical. So age onde ha degrau grande E onde o terreno
    /// esta abaixo da altura protegida - morro esculpido nao e alisado.
    /// </summary>
    [ContextMenu("Alisar degraus")]
    public void AlisarDegraus()
    {
        float limite = 35f;          // so mexe abaixo disto
        float degrauMin = 4f;        // so age se o desnivel local passar disto
        int passadas = 4;
        var ters = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int tocados = 0;
        foreach (var ter in ters)
        {
            var td = ter.terrainData;
            Vector3 tp = ter.transform.position;
            int r = td.heightmapResolution;
            float passo = td.size.x / (r - 1);
            float folga = esfumado + 600f;
            if (tp.x > xLeste + folga || tp.x + td.size.x < xOeste - folga) continue;
            if (tp.z > zLitoralOeste + folga || tp.z + td.size.z < zSul - folga) continue;
            float[,] h = td.GetHeights(0, 0, r, r);
            bool mudou = false;
            for (int passe = 0; passe < passadas; passe++)
            {
                float[,] copia = (float[,])h.Clone();
                for (int j = 1; j < r - 1; j++)
                for (int i = 1; i < r - 1; i++)
                {
                    float c = tp.y + copia[j, i] * td.size.y;
                    if (c > limite) continue;
                    float mn = 9e9f, mx = -9e9f, soma = 0f;
                    bool temProtegido = false;
                    for (int b = -1; b <= 1; b++)
                    for (int a = -1; a <= 1; a++)
                    {
                        float v = tp.y + copia[j + b, i + a] * td.size.y;
                        if (v > limite) { temProtegido = true; }
                        if (v < mn) mn = v;
                        if (v > mx) mx = v;
                        soma += v;
                    }
                    if (mx - mn < degrauMin) continue;
                    float media = soma / 9f;
                    // perto de morro protegido alisa mais devagar, pra nao comer o pe dele
                    float forca = temProtegido ? 0.35f : 0.6f;
                    float alvo = Mathf.Lerp(c, media, forca);
                    if (alvo > limite) alvo = limite;
                    // dentro da agua o alisador so pode cavar, nunca encher a baia
                    if (DentroDaEnseada(tp.x + i * passo, tp.z + j * passo) && alvo > c) continue;
                    h[j, i] = Mathf.Clamp01((alvo - tp.y) / td.size.y);
                    tocados++;
                    mudou = true;
                }
            }
            if (mudou) { td.SetHeights(0, 0, h); EditorUtility.SetDirty(td); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Enseada] alisados: " + tocados);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f);
        for (int i = 0; i < margem.Length - 1; i++)
        {
            Vector3 a = new Vector3(margem[i].x, 2f, margem[i].y);
            Vector3 b = new Vector3(margem[i + 1].x, 2f, margem[i + 1].y);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 22f);
        }
    }
#endif
}
