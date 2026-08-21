using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Vegetacao estilizada: mata atlantica em manchas organicas nos morros
/// (copas low-poly facetadas, 3 tons de verde) + coqueiros na orla.
/// Tudo combinado por balde de material - ~6 draw calls no total.
/// </summary>
[ExecuteAlways]
public class RioVegetacao : MonoBehaviour
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
    static float Declive(float x, float z)
    {
        float h0 = Chao(x, z), hx = Chao(x + 6f, z), hz = Chao(x, z + 6f);
        if (h0 < -9000 || hx < -9000 || hz < -9000) return 90f;
        return Mathf.Atan(Mathf.Sqrt((hx - h0) * (hx - h0) + (hz - h0) * (hz - h0)) / 6f) * Mathf.Rad2Deg;
    }
    static uint _rng = 7u;
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

    // ------------- icosaedro (12 verts, 20 faces) pra copa facetada -------------
    static readonly float FI = 1.618034f;
    static readonly Vector3[] ICO_V = {
        new Vector3(-1,FI,0), new Vector3(1,FI,0), new Vector3(-1,-FI,0), new Vector3(1,-FI,0),
        new Vector3(0,-1,FI), new Vector3(0,1,FI), new Vector3(0,-1,-FI), new Vector3(0,1,-FI),
        new Vector3(FI,0,-1), new Vector3(FI,0,1), new Vector3(-FI,0,-1), new Vector3(-FI,0,1) };
    static readonly int[] ICO_T = {
        0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
        3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };

    // copa: icosaedro achatado com jitter (FLAT SHADED: verts duplicados por face)
    static void Copa(List<Vector3> vs, List<int> ts, Vector3 c, float rw, float rh, uint sem)
    {
        var vj = new Vector3[12];
        for (int i = 0; i < 12; i++)
        {
            uint h = sem * 374761393u + (uint)i * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            float j1 = ((h & 0xFF) / 255f - 0.5f) * 0.42f;
            float j2 = (((h >> 8) & 0xFF) / 255f - 0.5f) * 0.42f;
            float j3 = (((h >> 16) & 0xFF) / 255f - 0.5f) * 0.42f;
            Vector3 v = ICO_V[i].normalized;
            vj[i] = new Vector3(v.x * (1f + j1) * rw, v.y * (1f + j2) * rh, v.z * (1f + j3) * rw);
        }
        for (int f = 0; f < 20; f++)
        {
            int v0 = vs.Count;
            vs.Add(c + vj[ICO_T[f * 3]]);
            vs.Add(c + vj[ICO_T[f * 3 + 1]]);
            vs.Add(c + vj[ICO_T[f * 3 + 2]]);
            ts.Add(v0); ts.Add(v0 + 1); ts.Add(v0 + 2);
        }
    }

    static void Tronco(List<Vector3> vs, List<int> ts, Vector3 pe, Vector3 topo, float r)
    {
        Vector3 d = (topo - pe).normalized;
        Vector3 s = Vector3.Cross(d, Vector3.right).sqrMagnitude > 0.01f
            ? Vector3.Cross(d, Vector3.right).normalized : Vector3.forward;
        Vector3 q = Vector3.Cross(d, s).normalized;
        Vector3[] baixo = { pe + s*r, pe + q*r, pe - s*r, pe - q*r };
        Vector3[] cima  = { topo + s*r*0.6f, topo + q*r*0.6f, topo - s*r*0.6f, topo - q*r*0.6f };
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4, v0 = vs.Count;
            vs.Add(baixo[i]); vs.Add(baixo[j]); vs.Add(cima[j]); vs.Add(cima[i]);
            ts.Add(v0); ts.Add(v0 + 2); ts.Add(v0 + 1); ts.Add(v0); ts.Add(v0 + 3); ts.Add(v0 + 2);
        }
    }

    [ContextMenu("Gerar VEGETACAO")]
    public void GerarTudo()
    {
        _ters = null; _rng = 7u;
        var raiz = Raiz("VEGETACAO");

        // hash das ruas: arvore mantem distancia
        var ocupRua = new HashSet<long>();
        {
            List<float> lgs; List<bool> pts;
            var ruas = RioCidade.TodasAsRuas(out lgs, out pts);
            foreach (var rua in ruas) foreach (var q in rua)
            {
                int cx = Mathf.FloorToInt(q.x / 9f), cz = Mathf.FloorToInt(q.y / 9f);
                for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++)
                    ocupRua.Add(((long)(cx + a + 100000) << 21) | (long)(cz + b + 100000));
            }
        }
        System.Func<float, float, bool> pertoDeRua = delegate(float qx, float qz)
        {
            return ocupRua.Contains(((long)(Mathf.FloorToInt(qx / 9f) + 100000) << 21) | (long)(Mathf.FloorToInt(qz / 9f) + 100000));
        };
        System.Func<float, float, bool> naCidade = delegate(float qx, float qz)
        {
            double[] BP = RioDados.BAIRRO_PT; int[] BI = RioDados.BAIRRO_INI;
            for (int b = 0; b < RioDados.BAIRRO_NOME.Length; b++)
            {
                int a0 = BI[b], f = BI[b + 1]; bool dentro = false;
                for (int i = a0, j = f - 1; i < f; j = i++)
                {
                    double xi = BP[2 * i], zi = BP[2 * i + 1], xj = BP[2 * j], zj = BP[2 * j + 1];
                    if (((zi > qz) != (zj > qz)) && (qx < (xj - xi) * (qz - zi) / (zj - zi) + xi)) dentro = !dentro;
                }
                if (dentro) return true;
            }
            return false;
        };

        // 3 baldes de copa + 1 tronco + palmeira(folha/tronco)
        var copaV = new List<Vector3>[3]; var copaT = new List<int>[3];
        for (int i = 0; i < 3; i++) { copaV[i] = new List<Vector3>(); copaT[i] = new List<int>(); }
        var troncoV = new List<Vector3>(); var troncoT = new List<int>();
        var folhaV = new List<Vector3>(); var folhaT = new List<int>();
        var ptroncoV = new List<Vector3>(); var ptroncoT = new List<int>();

        // ---------------- MATA nos morros (manchas organicas) ----------------
        float fx = (float)RioDados.FAVELA_X, fz = (float)RioDados.FAVELA_Z;
        float RFav = (float)RioDados.FAVELA_R;
        int nArv = 0, MAX = 9500;
        for (int passada = 0; passada < 2; passada++)
        for (float x = -9800f; x < 1900f && nArv < MAX; x += 17f)
        for (float z = -5800f; z < 3800f && nArv < MAX; z += 17f)
        {
            bool cone = RioCidade.NoCone(x, z);
            if (passada == 0 != cone) continue;   // passada 0 = SO o cone de visao (prioridade)
            float h = Chao(x, z);
            if (h < (cone ? 6f : 14f) || h > 690f) continue;
            float dec = Declive(x, z);
            if (dec > (cone ? 52f : 42f)) continue;               // morros do cone sao ingremes e TEM mata
            if (h > 240f && dec > 30f) continue;                  // paredao alto
            float m = Mathf.PerlinNoise(x * 0.004f + 31f, z * 0.004f + 77f) * 0.65f
                    + Mathf.PerlinNoise(x * 0.017f + 5f, z * 0.017f + 9f) * 0.35f;
            float limiar = cone ? 0.28f : 0.55f;   // denso no cone, ralo atras
            if (h < 30f) limiar += cone ? 0.08f : 0.12f;
            if (m < limiar) continue;
            if (R01() < (cone ? 0.10f : 0.50f)) continue;
            float dxf = x - fx, dzf = z - fz;
            if (dxf * dxf + dzf * dzf < (RFav + 14f) * (RFav + 14f)) continue;  // favela

            float px = x + RF(-7f, 7f), pz = z + RF(-7f, 7f);
            if (pertoDeRua(px, pz)) continue;                       // nunca em cima de rua
            if (naCidade(px, pz) && h < 45f && dec < 18f) continue; // miolo urbano plano: sem mata
            float ph = Chao(px, pz);
            if (ph < 12f) continue;
            float rw = RF(2.6f, 5.2f), rh = rw * RF(0.75f, 1.0f);
            float alt = RF(3.5f, 6.5f) + rw * 0.5f;
            int balde = (int)(R01() * 3f); if (balde > 2) balde = 2;
            Vector3 pe = new Vector3(px, ph - 0.7f, pz);
            Vector3 topoT = pe + Vector3.up * alt;
            Tronco(troncoV, troncoT, pe, topoT, RF(0.25f, 0.45f));
            Copa(copaV[balde], copaT[balde], topoT + Vector3.up * rh * 0.4f, rw, rh, (uint)(nArv * 977 + 13));
            nArv++;
        }

        // ---------------- COQUEIROS na orla ----------------
        int nPalm = 0;
        foreach (var via in RioCidadeDados.ARTERIAS)
        {
            bool orla = via.nome.Contains("Atlantica") || via.nome.Contains("Praia do Flamengo")
                     || via.nome.Contains("Praia de Botafogo");
            if (!orla) continue;
            for (int i = 0; i + 3 < via.ll.Length; i += 2)
            {
                double[] a = RioDados.P(via.ll[i], via.ll[i + 1]);
                double[] b = RioDados.P(via.ll[i + 2], via.ll[i + 3]);
                Vector2 pa = new Vector2((float)a[0], (float)a[1]);
                Vector2 pb = new Vector2((float)b[0], (float)b[1]);
                float L = Vector2.Distance(pa, pb);
                Vector2 d = (pb - pa) / Mathf.Max(L, 0.01f);
                Vector2 n = new Vector2(-d.y, d.x);
                for (float t = 0; t < L; t += 24f)
                {
                    for (int lado = -1; lado <= 1; lado += 2)
                    {
                        Vector2 p2 = pa + d * t + n * (via.larg * 0.5f + 3.5f) * lado;
                        float h = Chao(p2.x, p2.y);
                        if (h < 1.2f || h > 12f) continue;
                        if (R01() < 0.35f) continue;
                        // tronco curvado
                        float alt = RF(7f, 11f);
                        Vector3 pe = new Vector3(p2.x, h - 0.4f, p2.y);
                        float ang = RF(0f, Mathf.PI * 2f);
                        Vector3 curva = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * RF(0.8f, 1.8f);
                        Vector3 topo = pe + Vector3.up * alt + curva;
                        Vector3 meio = pe + Vector3.up * alt * 0.5f + curva * 0.4f;
                        Tronco(ptroncoV, ptroncoT, pe, meio, 0.30f);
                        Tronco(ptroncoV, ptroncoT, meio, topo, 0.22f);
                        // folhas: 7 lâminas caidas em leque
                        int nf = 7;
                        for (int f = 0; f < nf; f++)
                        {
                            float af = f / (float)nf * Mathf.PI * 2f + RF(-0.2f, 0.2f);
                            Vector3 dir = new Vector3(Mathf.Cos(af), 0, Mathf.Sin(af));
                            float comp = RF(2.6f, 3.6f);
                            Vector3 ponta = topo + dir * comp + Vector3.down * comp * 0.45f;
                            Vector3 lado2 = Vector3.Cross(dir, Vector3.up) * 0.55f;
                            int v0 = folhaV.Count;
                            folhaV.Add(topo + lado2 * 0.3f);
                            folhaV.Add(topo - lado2 * 0.3f);
                            folhaV.Add(ponta);
                            folhaT.Add(v0); folhaT.Add(v0 + 1); folhaT.Add(v0 + 2);
                            // verso com vertices proprios (normais validas, sem NaN)
                            folhaV.Add(topo + lado2 * 0.3f);
                            folhaV.Add(topo - lado2 * 0.3f);
                            folhaV.Add(ponta);
                            folhaT.Add(v0 + 3); folhaT.Add(v0 + 5); folhaT.Add(v0 + 4);
                        }
                        nPalm++;
                    }
                }
            }
        }

        // ---------------- monta meshes + materiais ----------------
        System.Action<string, List<Vector3>, List<int>, Color> monta = delegate(string nome, List<Vector3> vs, List<int> ts, Color cor) {
            var mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vs); mesh.SetTriangles(ts, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string cam = "Assets/Art/Meshes/Cidade/" + nome + ".asset";
            var ja = AssetDatabase.LoadAssetAtPath<Mesh>(cam);
            if (ja != null) { ja.Clear(); EditorUtility.CopySerialized(mesh, ja); EditorUtility.SetDirty(ja); }
            else AssetDatabase.CreateAsset(mesh, cam);
            string camM = "Assets/Art/Shaders/M_" + nome + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(camM);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, camM); }
            mat.SetColor("_BaseColor", cor);
            mat.SetFloat("_Smoothness", 0.02f);
            EditorUtility.SetDirty(mat);
            var go = new GameObject(nome);
            go.transform.SetParent(GameObject.Find("VEGETACAO").transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(cam);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        };
        monta("CopaEscura", copaV[0], copaT[0], new Color(0.10f, 0.28f, 0.13f));
        monta("CopaMedia",  copaV[1], copaT[1], new Color(0.16f, 0.36f, 0.15f));
        monta("CopaClara",  copaV[2], copaT[2], new Color(0.28f, 0.40f, 0.16f));
        monta("Troncos",    troncoV, troncoT,   new Color(0.26f, 0.19f, 0.13f));
        monta("PalmFolha",  folhaV, folhaT,     new Color(0.17f, 0.38f, 0.17f));
        monta("PalmTronco", ptroncoV, ptroncoT, new Color(0.42f, 0.34f, 0.24f));
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Vegetacao] " + nArv + " arvores de mata + " + nPalm + " coqueiros");
    }
#endif
}
