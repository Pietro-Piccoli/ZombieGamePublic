using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Pacote de clima: nevoa atmosferica em camadas, agua de entardecer,
/// espuma nas praias e nuvens estilizadas no horizonte.
/// Tudo estatico e combinado - custo de render ~zero.
/// </summary>
[ExecuteAlways]
public class RioClima : MonoBehaviour
{
#if UNITY_EDITOR
    static Terrain[] _ters;
    static float Chao(float x, float z)
    {
        if (_ters == null || _ters.Length == 0)
            _ters = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var t in _ters)
        {
            Vector3 p = t.transform.position; var td = t.terrainData;
            if (x < p.x || x > p.x + td.size.x || z < p.z || z > p.z + td.size.z) continue;
            return p.y + td.GetInterpolatedHeight((x - p.x) / td.size.x, (z - p.z) / td.size.z);
        }
        return -9999f;
    }
    static uint _rng = 55u;
    static float R01() { _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5; return (_rng & 0xFFFFFF) / 16777216f; }
    static float RF(float a, float b) { return a + (b - a) * R01(); }

    static Transform Raiz(string nome)
    {
        var go = GameObject.Find(nome);
        if (go == null) go = new GameObject(nome);
        for (int i = go.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        return go.transform;
    }

    [ContextMenu("Aplicar TUDO")]
    public void AplicarTudo()
    {
        AplicarNevoa();
        AplicarAgua();
        GerarEspuma();
        GerarNuvens();
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Clima] tudo aplicado e salvo");
    }

    // -------- 1. nevoa atmosferica: funde a distancia com o ceu quente --------
    public void AplicarNevoa()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.93f, 0.77f, 0.60f);
        RenderSettings.fogDensity = 0.00017f;   // ~35% no Pao visto do mirante
        // ceu com horizonte mais denso/avermelhado
        var ceu = RenderSettings.skybox;
        if (ceu != null && ceu.HasProperty("_AtmosphereThickness"))
        {
            ceu.SetFloat("_AtmosphereThickness", 1.45f);
            ceu.SetFloat("_Exposure", 1.18f);
            EditorUtility.SetDirty(ceu);
        }
        DynamicGI.UpdateEnvironment();
    }

    // -------- 2. agua de entardecer, ondas mais vivas --------
    public void AplicarAgua()
    {
        var agua = GameObject.Find("AGUA");
        if (agua == null) return;
        var m = agua.GetComponent<MeshRenderer>().sharedMaterial;
        m.SetColor("_Deep_Water_Color", new Color(0.13f, 0.21f, 0.34f));
        m.SetColor("_Shallow_Water_Color", new Color(0.55f, 0.55f, 0.50f));
        m.SetFloat("_Amplitude_Wave", 0.025f);
        m.SetFloat("_Frequency_Wave", 6.5f);
        m.SetFloat("_Speed_Wave", 0.65f);
        EditorUtility.SetDirty(m);
    }

    // -------- 3. espuma: faixa clara na linha d'agua das praias --------
    public void GerarEspuma()
    {
        _ters = null; _rng = 55u;
        var raiz = Raiz("CLIMA_ESPUMA");
        double[] L = RioDados.LITORAL;
        double[] PR = RioDados.PRAIA;
        var vs = new List<Vector3>(); var ts = new List<int>(); var uv = new List<Vector2>();

        for (int k = 0; k < RioDados.PRAIA_NOME.Length; k++)
        {
            int i0 = (int)PR[k * 3], i1 = (int)PR[k * 3 + 1];
            for (int i = i0; i < i1; i++)
            {
                // acha a linha d'agua real: anda na normal ate h ~ 0
                Vector2 a = new Vector2((float)L[2 * i], (float)L[2 * i + 1]);
                Vector2 b = new Vector2((float)L[2 * i + 2], (float)L[2 * i + 3]);
                Vector2 d = (b - a).normalized;
                Vector2 n = new Vector2(-d.y, d.x);
                System.Func<Vector2, Vector2> naAgua = delegate(Vector2 p0)
                {
                    Vector2 melhor = p0; float mdif = 999f;
                    for (float o = -35f; o <= 35f; o += 3f)
                    {
                        Vector2 q = p0 + n * o;
                        float h = Chao(q.x, q.y);
                        float dif = Mathf.Abs(h - 0.05f);
                        if (dif < mdif) { mdif = dif; melhor = q; }
                    }
                    return melhor;
                };
                Vector2 pa = naAgua(a), pb = naAgua(b);
                int v0 = vs.Count;
                float w = 5.5f + R01() * 2f;
                vs.Add(new Vector3(pa.x - n.x * w, 0.28f, pa.y - n.y * w));
                vs.Add(new Vector3(pa.x + n.x * w * 0.5f, 0.28f, pa.y + n.y * w * 0.5f));
                vs.Add(new Vector3(pb.x + n.x * w * 0.5f, 0.28f, pb.y + n.y * w * 0.5f));
                vs.Add(new Vector3(pb.x - n.x * w, 0.28f, pb.y - n.y * w));
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
                ts.Add(v0); ts.Add(v0 + 1); ts.Add(v0 + 2); ts.Add(v0); ts.Add(v0 + 2); ts.Add(v0 + 3);
            }
        }
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vs); mesh.SetTriangles(ts, 0); mesh.SetUVs(0, uv);
        var nrm = new Vector3[vs.Count];
        for (int i = 0; i < nrm.Length; i++) nrm[i] = Vector3.up;
        mesh.SetNormals(new List<Vector3>(nrm));
        mesh.RecalculateBounds();
        System.IO.Directory.CreateDirectory("Assets/Art/Meshes/Cidade");
        var ja = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Meshes/Cidade/Espuma.asset");
        if (ja != null) { ja.Clear(); EditorUtility.CopySerialized(mesh, ja); EditorUtility.SetDirty(ja); }
        else AssetDatabase.CreateAsset(mesh, "Assets/Art/Meshes/Cidade/Espuma.asset");

        var mat = MatEspuma();
        var go = new GameObject("Espuma");
        go.transform.SetParent(raiz, false);
        go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Meshes/Cidade/Espuma.asset");
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Debug.Log("[Clima] espuma: " + (vs.Count / 4) + " trechos");
    }

    static Material MatEspuma()
    {
        // textura: gradiente suave com borda irregular
        string camT = "Assets/Art/Textures/Terreno/T_Espuma.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(camT) == null)
        {
            int W = 64, H = 16;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float v = y / (float)(H - 1);
                float irr = Mathf.PerlinNoise(x * 0.4f, y * 0.9f) * 0.35f;
                float alfa = Mathf.Clamp01(Mathf.Sin(v * Mathf.PI) - irr);
                px[y * W + x] = new Color(1f, 0.98f, 0.94f, alfa * 0.85f);
            }
            tex.SetPixels(px); tex.Apply();
            System.IO.File.WriteAllBytes(Application.dataPath + "/Art/Textures/Terreno/T_Espuma.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.Refresh();
            var imp = (TextureImporter)AssetImporter.GetAtPath(camT);
            imp.alphaIsTransparency = true; imp.SaveAndReimport();
        }
        string camM = "Assets/Art/Shaders/M_Espuma.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(camM);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, camM);
        }
        mat.SetFloat("_Surface", 1f); // transparente
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = 3100;
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/Terreno/T_Espuma.png"));
        mat.SetColor("_BaseColor", new Color(1f, 0.97f, 0.92f, 0.9f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // -------- 4. nuvens estilizadas no horizonte --------
    public void GerarNuvens()
    {
        _rng = 99u;
        var raiz = Raiz("CLIMA_NUVENS");
        // textura de nuvem: mancha suave
        string camT = "Assets/Art/Textures/Terreno/T_Nuvem.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(camT) == null)
        {
            int W = 128, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float u = (x - W * 0.5f) / (W * 0.5f), v = (y - H * 0.45f) / (H * 0.55f);
                float d = Mathf.Sqrt(u * u + v * v);
                float bolha = Mathf.PerlinNoise(x * 0.09f, y * 0.13f) * 0.45f;
                float alfa = Mathf.Clamp01(1.05f - d - bolha * d);
                px[y * W + x] = new Color(1f, 0.92f, 0.82f, alfa * alfa * 0.9f);
            }
            tex.SetPixels(px); tex.Apply();
            System.IO.File.WriteAllBytes(Application.dataPath + "/Art/Textures/Terreno/T_Nuvem.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.Refresh();
            var imp = (TextureImporter)AssetImporter.GetAtPath(camT);
            imp.alphaIsTransparency = true; imp.SaveAndReimport();
        }
        string camM = "Assets/Art/Shaders/M_Nuvem.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(camM);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, camM);
        }
        mat.SetFloat("_Surface", 1f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = 2900;
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(camT));
        mat.SetColor("_BaseColor", new Color(1f, 0.86f, 0.72f, 0.97f));
        EditorUtility.SetDirty(mat);

        // aneis de nuvens no horizonte, mais densas do lado do mar (leste)
        var vs = new List<Vector3>(); var ts = new List<int>(); var uv = new List<Vector2>();
        Vector3 centro = new Vector3(-2500f, 0f, -800f);
        for (int i = 0; i < 14; i++)
        {
            float ang = RF(0f, Mathf.PI * 2f);
            if (i < 7) ang = RF(-0.9f, 1.1f);      // metade delas pro lado do mar/Pao
            float dist = RF(4600f, 8500f);
            float alt = RF(300f, 780f);
            float wq = RF(800f, 1900f), hq = wq * RF(0.24f, 0.38f);
            Vector3 pos = centro + new Vector3(Mathf.Cos(ang) * dist, alt, Mathf.Sin(ang) * dist);
            Vector3 pra = new Vector3(centro.x, alt, centro.z);
            Vector3 dir = (pra - pos).normalized;
            Vector3 lado = Vector3.Cross(Vector3.up, dir).normalized;
            int v0 = vs.Count;
            vs.Add(pos - lado * wq * 0.5f - Vector3.up * hq * 0.5f);
            vs.Add(pos + lado * wq * 0.5f - Vector3.up * hq * 0.5f);
            vs.Add(pos + lado * wq * 0.5f + Vector3.up * hq * 0.5f);
            vs.Add(pos - lado * wq * 0.5f + Vector3.up * hq * 0.5f);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            ts.Add(v0); ts.Add(v0 + 2); ts.Add(v0 + 1); ts.Add(v0); ts.Add(v0 + 3); ts.Add(v0 + 2);
            ts.Add(v0); ts.Add(v0 + 1); ts.Add(v0 + 2); ts.Add(v0); ts.Add(v0 + 2); ts.Add(v0 + 3);
        }
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vs); mesh.SetTriangles(ts, 0); mesh.SetUVs(0, uv);
        var nrm = new Vector3[vs.Count];
        for (int i = 0; i < nrm.Length; i++) nrm[i] = Vector3.up;
        mesh.SetNormals(new List<Vector3>(nrm));
        mesh.RecalculateBounds();
        var ja = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Meshes/Cidade/Nuvens.asset");
        if (ja != null) { ja.Clear(); EditorUtility.CopySerialized(mesh, ja); EditorUtility.SetDirty(ja); }
        else AssetDatabase.CreateAsset(mesh, "Assets/Art/Meshes/Cidade/Nuvens.asset");
        var go = new GameObject("Nuvens");
        go.transform.SetParent(raiz, false);
        go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Meshes/Cidade/Nuvens.asset");
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Debug.Log("[Clima] 14 nuvens no horizonte");
    }
#endif
}
