using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Gera a cidade: ruas reais drapejadas no terreno, quadras com predios em
/// tamanho real, casas onde e bairro de casas, favela Santa Marta (area do
/// jogo) e iluminacao noturna. OTIMIZADO: tudo combinado em poucos meshes
/// por regiao, 4 materiais no total, luz realtime so na favela.
/// </summary>
[ExecuteAlways]
public class RioCidade : MonoBehaviour
{
#if UNITY_EDITOR
    const string PASTA = "Assets/Art/Meshes/Cidade";

    // ---------------- util ----------------
    static Terrain[] _ters;
    static float AlturaChao(float x, float z)
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
        float h0 = AlturaChao(x, z);
        float hx = AlturaChao(x + 6f, z), hz = AlturaChao(x, z + 6f);
        if (h0 < -9000 || hx < -9000 || hz < -9000) return 90f;
        return Mathf.Atan(Mathf.Sqrt((hx - h0) * (hx - h0) + (hz - h0) * (hz - h0)) / 6f) * Mathf.Rad2Deg;
    }
    static uint _rng = 12345u;
    static float R01() { _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5; return (_rng & 0xFFFFFF) / 16777216f; }
    static float RFx(float a, float b) { return a + (b - a) * R01(); }

    static bool DentroBairro(int b, float x, float z)
    {
        double[] P = RioDados.BAIRRO_PT; int[] I = RioDados.BAIRRO_INI;
        int a = I[b], f = I[b + 1];
        bool dentro = false;
        for (int i = a, j = f - 1; i < f; j = i++)
        {
            double xi = P[2 * i], zi = P[2 * i + 1], xj = P[2 * j], zj = P[2 * j + 1];
            if (((zi > z) != (zj > z)) && (x < (xj - xi) * (z - zi) / (zj - zi) + xi))
                dentro = !dentro;
        }
        return dentro;
    }

    // cone de visao do jogador: apice no mirante da Dona Marta, mirando o Pao
    public static bool NoCone(float x, float z)
    {
        float ax = (float)RioDados.FAVELA_X, az = (float)RioDados.FAVELA_Z;
        Vector2 v = new Vector2(x - ax, z - az);
        float dist = v.magnitude;
        if (dist > 4400f) return false;
        if (dist < 60f) return true;
        Vector2 dir = new Vector2(-ax, -az).normalized;
        return Vector2.Dot(v / dist, dir) > 0.60f;
    }

    static Transform Raiz(string nome)
    {
        var go = GameObject.Find(nome);
        if (go == null) go = new GameObject(nome);
        for (int i = go.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        return go.transform;
    }

    static void SalvarMesh(Mesh m, string nome)
    {
        System.IO.Directory.CreateDirectory(PASTA);
        string cam = PASTA + "/" + nome + ".asset";
        var ja = AssetDatabase.LoadAssetAtPath<Mesh>(cam);
        if (ja != null) { ja.Clear(); EditorUtility.CopySerialized(m, ja); EditorUtility.SetDirty(ja); }
        else AssetDatabase.CreateAsset(m, cam);
    }
    static Mesh CarregarMesh(string nome)
    { return AssetDatabase.LoadAssetAtPath<Mesh>(PASTA + "/" + nome + ".asset"); }

    // ---------------- RUAS ----------------
    // junta todas as polilinhas de rua do mapa (arterias + malha dos bairros)
    public static List<List<Vector2>> TodasAsRuas(out List<float> largs, out List<bool> postes)
    {
        var ruas = new List<List<Vector2>>();
        largs = new List<float>(); postes = new List<bool>();

        foreach (var v in RioCidadeDados.ARTERIAS)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < v.ll.Length; i += 2)
            {
                double[] q = RioDados.P(v.ll[i], v.ll[i + 1]);
                pts.Add(new Vector2((float)q[0], (float)q[1]));
            }
            ruas.Add(pts); largs.Add(v.larg); postes.Add(v.poste);
        }

        // malha de quadras por bairro: paralelas e transversais clipadas no poligono
        foreach (var m in RioCidadeDados.MALHAS)
        {
            // caixa do bairro
            double[] P = RioDados.BAIRRO_PT; int[] I = RioDados.BAIRRO_INI;
            float x0 = 1e9f, x1 = -1e9f, z0 = 1e9f, z1 = -1e9f;
            for (int i = I[m.bairro]; i < I[m.bairro + 1]; i++)
            {
                x0 = Mathf.Min(x0, (float)P[2 * i]); x1 = Mathf.Max(x1, (float)P[2 * i]);
                z0 = Mathf.Min(z0, (float)P[2 * i + 1]); z1 = Mathf.Max(z1, (float)P[2 * i + 1]);
            }
            Vector2 e = new Vector2((float)m.eixoX, (float)m.eixoZ).normalized;
            Vector2 n = new Vector2(-e.y, e.x);
            Vector2 c = new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
            float meia = Mathf.Max(x1 - x0, z1 - z0) * 0.75f;

            for (int dir = 0; dir < 2; dir++)
            {
                Vector2 ao = dir == 0 ? e : n, per = dir == 0 ? n : e;
                float esp = dir == 0 ? m.espPar : m.espCruz;
                for (float off = -meia; off <= meia; off += esp)
                {
                    List<Vector2> atual = null;
                    for (float t = -meia; t <= meia; t += 14f)
                    {
                        Vector2 p2 = c + per * off + ao * t;
                        bool ok = DentroBairro(m.bairro, p2.x, p2.y);
                        if (ok)
                        {
                            float h = AlturaChao(p2.x, p2.y);
                            ok = h > 2.0f && h < 90f && Declive(p2.x, p2.y) < 14f;
                        }
                        if (ok) { if (atual == null) atual = new List<Vector2>(); atual.Add(p2); }
                        else if (atual != null)
                        {
                            if (atual.Count >= 4) { ruas.Add(atual); largs.Add(m.larg); postes.Add(false); }
                            atual = null;
                        }
                    }
                    if (atual != null && atual.Count >= 4) { ruas.Add(atual); largs.Add(m.larg); postes.Add(false); }
                }
            }
        }
        return ruas;
    }

    [ContextMenu("1. Gerar RUAS")]
    public void GerarRuas()
    {
        _ters = null; _rng = 12345u;
        var raiz = Raiz("CIDADE_RUAS");
        List<float> largs; List<bool> postes;
        var ruas = TodasAsRuas(out largs, out postes);

        var vs = new List<Vector3>(); var ts = new List<int>(); var uv = new List<Vector2>();
        int nRuas = 0;
        for (int r = 0; r < ruas.Count; r++)
        {
            var pts = ruas[r]; float w = largs[r] * 0.5f;
            float dist = 0;
            int baseIni = vs.Count;
            var fila = new List<Vector2>();
            // densifica a cada 12 m
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                float L = Vector2.Distance(a, b);
                int nseg = Mathf.Max(1, Mathf.CeilToInt(L / 12f));
                for (int k = (i == 0 ? 0 : 1); k <= nseg; k++)
                    fila.Add(Vector2.Lerp(a, b, k / (float)nseg));
            }
            if (fila.Count < 2) continue;
            int validos = 0;
            for (int i = 0; i < fila.Count; i++)
            {
                Vector2 p2 = fila[i];
                Vector2 d = (i < fila.Count - 1 ? fila[i + 1] - p2 : p2 - fila[i - 1]).normalized;
                Vector2 nn = new Vector2(-d.y, d.x);
                float h = AlturaChao(p2.x, p2.y);
                if (h < 1.0f || h > 60f) { validos = 0; continue; }   // agua ou morro alto: corta
                if (Declive(p2.x, p2.y) > 13f) { validos = 0; continue; } // encosta ingreme: rua nao escala
                float he = AlturaChao(p2.x + nn.x * w, p2.y + nn.y * w);
                float hd = AlturaChao(p2.x - nn.x * w, p2.y - nn.y * w);
                float y = Mathf.Max(h, Mathf.Max(he, hd)) + 0.22f;
                vs.Add(new Vector3(p2.x + nn.x * w, y, p2.y + nn.y * w));
                vs.Add(new Vector3(p2.x - nn.x * w, y, p2.y - nn.y * w));
                uv.Add(new Vector2(0, dist / 12f)); uv.Add(new Vector2(1, dist / 12f));
                if (i > 0) dist += 12f;
                validos++;
                if (validos > 1)
                {
                    int v0 = vs.Count - 4;
                    ts.Add(v0); ts.Add(v0 + 2); ts.Add(v0 + 1);
                    ts.Add(v0 + 1); ts.Add(v0 + 2); ts.Add(v0 + 3);
                }
            }
            if (validos > 1) nRuas++;
        }

        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vs); mesh.SetTriangles(ts, 0); mesh.SetUVs(0, uv);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        SalvarMesh(mesh, "Ruas");

        var mat = MaterialSimples("M_Asfalto", new Color(0.13f, 0.13f, 0.15f), Color.black);
        var go = new GameObject("Ruas");
        go.transform.SetParent(raiz, false);
        go.AddComponent<MeshFilter>().sharedMesh = CarregarMesh("Ruas");
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        AssetDatabase.SaveAssets();
        Debug.Log("[Cidade] ruas: " + nRuas + " tracados, " + vs.Count + " verts");
    }

    // ---------------- MATERIAIS ----------------
    static Material MaterialSimples(string nome, Color cor, Color emissao)
    {
        string cam = "Assets/Art/Shaders/" + nome + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(cam);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, cam);
        }
        mat.SetColor("_BaseColor", cor);
        mat.SetFloat("_Smoothness", 0.05f);
        if (emissao.maxColorComponent > 0.01f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", emissao);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // textura de janelas: grade 8x8, parte acesa (amarelo quente), resto escuro
    static Material MaterialPredio(string nome, Color parede, float fracaoAcesa, int semente)
    {
        string camT = "Assets/Art/Textures/Terreno/" + nome + "_tex.png";
        string camE = "Assets/Art/Textures/Terreno/" + nome + "_emi.png";
        if (!System.IO.File.Exists(camT.Replace("Assets", Application.dataPath.Replace("/Assets", "") + "/Assets")))
        {
            int N = 128; int cel = N / 8;
            var tb = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var te = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var pb = new Color[N * N]; var pe = new Color[N * N];
            uint rng = (uint)(semente * 7919 + 13);
            for (int cy = 0; cy < 8; cy++) for (int cx = 0; cx < 8; cx++)
            {
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                bool acesa = ((rng & 0xFFFF) / 65536f) < fracaoAcesa;
                Color jan = acesa ? new Color(1f, 0.83f, 0.55f) : new Color(0.06f, 0.07f, 0.10f);
                Color emi = acesa ? new Color(1f, 0.78f, 0.45f) : Color.black;
                for (int y = 0; y < cel; y++) for (int x = 0; x < cel; x++)
                {
                    int px = cx * cel + x, py = cy * cel + y;
                    bool ehJanela = x >= cel / 4 && x < cel * 3 / 4 && y >= cel / 4 && y < cel * 3 / 4;
                    pb[py * N + px] = ehJanela ? jan : parede;
                    pe[py * N + px] = ehJanela ? emi : Color.black;
                }
            }
            tb.SetPixels(pb); tb.Apply(); te.SetPixels(pe); te.Apply();
            System.IO.File.WriteAllBytes(Application.dataPath + "/Art/Textures/Terreno/" + nome + "_tex.png", tb.EncodeToPNG());
            System.IO.File.WriteAllBytes(Application.dataPath + "/Art/Textures/Terreno/" + nome + "_emi.png", te.EncodeToPNG());
            Object.DestroyImmediate(tb); Object.DestroyImmediate(te);
            AssetDatabase.Refresh();
        }
        string camM = "Assets/Art/Shaders/" + nome + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(camM);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, camM);
        }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(camT));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.05f);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(camE));
        mat.SetColor("_EmissionColor", Color.white * 1.6f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------------- caixa de predio com UV em metros ----------------
    // UV: 1 celula de janela = 3.5 m x 3.0 m; textura tem 8x8 celulas
    static void Caixa(List<Vector3> vs, List<int> ts, List<Vector2> uv,
                      Vector2 centro, Vector2 frente, float largura, float fundo, float yBase, float alt)
    {
        Vector2 f = frente.normalized;
        Vector2 n = new Vector2(-f.y, f.x);
        float hw = largura * 0.5f, hd = fundo * 0.5f;
        Vector2[] cant = new Vector2[] {
            centro - f * hw - n * hd, centro + f * hw - n * hd,
            centro + f * hw + n * hd, centro - f * hw + n * hd };
        float uEsc = 1f / (3.5f * 8f), vEsc = 1f / (3.0f * 8f);
        // 4 paredes
        for (int i = 0; i < 4; i++)
        {
            Vector2 a = cant[i], b = cant[(i + 1) % 4];
            float wLado = Vector2.Distance(a, b);
            int v0 = vs.Count;
            vs.Add(new Vector3(a.x, yBase, a.y)); vs.Add(new Vector3(b.x, yBase, b.y));
            vs.Add(new Vector3(b.x, yBase + alt, b.y)); vs.Add(new Vector3(a.x, yBase + alt, a.y));
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(wLado * uEsc, 0));
            uv.Add(new Vector2(wLado * uEsc, alt * vEsc)); uv.Add(new Vector2(0, alt * vEsc));
            ts.Add(v0); ts.Add(v0 + 2); ts.Add(v0 + 1); ts.Add(v0); ts.Add(v0 + 3); ts.Add(v0 + 2);
        }
        // teto (UV mini = sem janela, canto da textura)
        int t0 = vs.Count;
        for (int i = 0; i < 4; i++) { vs.Add(new Vector3(cant[i].x, yBase + alt, cant[i].y)); uv.Add(new Vector2(0.001f, 0.001f)); }
        ts.Add(t0); ts.Add(t0 + 2); ts.Add(t0 + 1); ts.Add(t0); ts.Add(t0 + 3); ts.Add(t0 + 2);
    }

    // ---------------- PREDIOS ----------------
    [ContextMenu("2. Gerar PREDIOS")]
    public void GerarPredios()
    {
        _ters = null; _rng = 777u;
        var raiz = Raiz("CIDADE_PREDIOS");
        List<float> largs; List<bool> postes;
        var ruas = TodasAsRuas(out largs, out postes);

        // ocupacao: hash espacial de celulas de 6 m pra nao sobrepor
        var ocup = new HashSet<long>();
        System.Func<float, float, float, float, bool> livreR = delegate(float x0r, float z0r, float x1r, float z1r)
        {
            int ca = Mathf.FloorToInt(x0r / 6f), cb = Mathf.FloorToInt(x1r / 6f);
            int cc = Mathf.FloorToInt(z0r / 6f), cd = Mathf.FloorToInt(z1r / 6f);
            for (int a = ca; a <= cb; a++) for (int b = cc; b <= cd; b++)
                if (ocup.Contains(((long)(a + 100000) << 21) | (long)(b + 100000))) return false;
            return true;
        };
        System.Action<float, float, float, float> marcaR = delegate(float x0r, float z0r, float x1r, float z1r)
        {
            int ca = Mathf.FloorToInt(x0r / 6f), cb = Mathf.FloorToInt(x1r / 6f);
            int cc = Mathf.FloorToInt(z0r / 6f), cd = Mathf.FloorToInt(z1r / 6f);
            for (int a = ca; a <= cb; a++) for (int b = cc; b <= cd; b++)
                ocup.Add(((long)(a + 100000) << 21) | (long)(b + 100000));
        };
        // marca so o miolo das ruas (faixa estreita) pra esquina nao virar predio
        for (int r = 0; r < ruas.Count; r++)
            foreach (var p in ruas[r]) marcaR(p.x - 2f, p.y - 2f, p.x + 2f, p.y + 2f);

        // 3 baldes de mesh: altos, medios, casas
        var lotes = new List<Vector3>[6]; var lotesT = new List<int>[6]; var lotesU = new List<Vector2>[6];
        for (int i = 0; i < 6; i++) { lotes[i] = new List<Vector3>(); lotesT[i] = new List<int>(); lotesU[i] = new List<Vector2>(); }
        int nP = 0, nC = 0;

        for (int r = 0; r < ruas.Count; r++)
        {
            var pts = ruas[r];
            // acha o bairro do meio da rua
            Vector2 meio = pts[pts.Count / 2];
            int bi = -1;
            for (int b = 0; b < RioCidadeDados.MALHAS.Length; b++)
                if (DentroBairro(RioCidadeDados.MALHAS[b].bairro, meio.x, meio.y)) { bi = b; break; }
            if (bi < 0) continue;
            var M = RioCidadeDados.MALHAS[bi];

            float recuo = largs[r] * 0.5f + 4f;
            for (int lado = -1; lado <= 1; lado += 2)
            {
                float andado = 999f;
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector2 a = pts[i], b = pts[i + 1];
                    float L = Vector2.Distance(a, b);
                    Vector2 d = (b - a) / Mathf.Max(L, 0.01f);
                    Vector2 n = new Vector2(-d.y, d.x) * lado;
                    for (float t = 0; t < L; t += 6f)
                    {
                        andado += 6f;
                        float frente = RFx(M.frenteMin, M.frenteMax);
                        Vector2 pref = a + d * t;
                        bool cone = NoCone(pref.x, pref.y);
                        if (andado < frente * 0.5f + (cone ? 1f : 3f)) continue;
                        float fundo = cone ? RFx(16f, 28f) : RFx(14f, 24f);
                        if (M.casas) fundo = RFx(8f, 13f);
                        Vector2 pos = a + d * t + n * (recuo + fundo * 0.5f);
                        if (!DentroBairro(M.bairro, pos.x, pos.y)) continue;
                        float h = AlturaChao(pos.x, pos.y);
                        if (h < 2.2f || h > 80f) continue;
                        if (Declive(pos.x, pos.y) > (M.casas ? (cone ? 23f : 17f) : (cone ? 16f : 12f))) continue;
                        // AABB do predio girado, com 1 m de folga interna
                        Vector2 ff = d; Vector2 nn2 = new Vector2(-d.y, d.x);
                        float ex = Mathf.Abs(ff.x) * frente * 0.5f + Mathf.Abs(nn2.x) * fundo * 0.5f;
                        float ez = Mathf.Abs(ff.y) * frente * 0.5f + Mathf.Abs(nn2.y) * fundo * 0.5f;
                        if (!livreR(pos.x - ex + 1f, pos.y - ez + 1f, pos.x + ex - 1f, pos.y + ez - 1f)) continue;
                        marcaR(pos.x - ex - 1.5f, pos.y - ez - 1.5f, pos.x + ex + 1.5f, pos.y + ez + 1.5f);
                        float alt = RFx(M.altMin, M.altMax);
                        alt = Mathf.Round(alt / 3f) * 3f;  // andares inteiros de 3 m
                        int balde = (M.casas ? 2 : (alt >= 25f ? 0 : 1)) + (R01() < 0.45f ? 3 : 0);
                        Caixa(lotes[balde], lotesT[balde], lotesU[balde], pos, d, frente, fundo, h - 0.6f, alt);
                        if (M.casas) nC++; else nP++;
                        andado = 0f;
                        // MIOLO DE QUADRA: segunda fileira atras, so no cone de visao
                        if (cone)
                        {
                            float fundo2 = M.casas ? RFx(8f, 13f) : RFx(14f, 26f);
                            float frente2 = RFx(M.frenteMin, M.frenteMax) * 0.9f;
                            Vector2 pos2 = a + d * t + n * (recuo + fundo + 6f + fundo2 * 0.5f);
                            float h2 = AlturaChao(pos2.x, pos2.y);
                            if (DentroBairro(M.bairro, pos2.x, pos2.y) && h2 > 2.2f && h2 < 80f
                                && Declive(pos2.x, pos2.y) < (M.casas ? 23f : 16f))
                            {
                                float ex2 = Mathf.Abs(d.x) * frente2 * 0.5f + Mathf.Abs(n.x) * fundo2 * 0.5f;
                                float ez2 = Mathf.Abs(d.y) * frente2 * 0.5f + Mathf.Abs(n.y) * fundo2 * 0.5f;
                                if (livreR(pos2.x - ex2 + 1f, pos2.y - ez2 + 1f, pos2.x + ex2 - 1f, pos2.y + ez2 - 1f))
                                {
                                    marcaR(pos2.x - ex2 - 1f, pos2.y - ez2 - 1f, pos2.x + ex2 + 1f, pos2.y + ez2 + 1f);
                                    float alt2 = Mathf.Round(RFx(M.altMin * 0.7f, M.altMax * 0.85f) / 3f) * 3f;
                                    int balde2 = (M.casas ? 2 : (alt2 >= 25f ? 0 : 1)) + (R01() < 0.45f ? 3 : 0);
                                    Caixa(lotes[balde2], lotesT[balde2], lotesU[balde2], pos2, d, frente2, fundo2, h2 - 0.6f, alt2);
                                    if (M.casas) nC++; else nP++;
                                }
                            }
                        }
                    }
                }
            }
        }

        string[] nomes = { "PrediosAltos", "PrediosMedios", "Casas", "PrediosAltosB", "PrediosMediosB", "CasasB" };
        Material[] mats = {
            MaterialPredio("M_PredioAlto", new Color(0.72f, 0.70f, 0.66f), 0.38f, 1),
            MaterialPredio("M_PredioMedio", new Color(0.66f, 0.62f, 0.58f), 0.30f, 2),
            MaterialPredio("M_Casa", new Color(0.78f, 0.68f, 0.55f), 0.22f, 3),
            MaterialPredio("M_PredioAltoB", new Color(0.58f, 0.60f, 0.63f), 0.34f, 11),
            MaterialPredio("M_PredioMedioB", new Color(0.75f, 0.66f, 0.54f), 0.28f, 12),
            MaterialPredio("M_CasaB", new Color(0.70f, 0.74f, 0.64f), 0.20f, 13) };
        for (int i = 0; i < 6; i++)
        {
            var mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(lotes[i]); mesh.SetTriangles(lotesT[i], 0); mesh.SetUVs(0, lotesU[i]);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            SalvarMesh(mesh, nomes[i]);
            var go = new GameObject(nomes[i]);
            go.transform.SetParent(raiz, false);
            go.AddComponent<MeshFilter>().sharedMesh = CarregarMesh(nomes[i]);
            go.AddComponent<MeshRenderer>().sharedMaterial = mats[i];
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Cidade] predios: " + nP + "  casas: " + nC);
    }

    // ---------------- FAVELA (area do jogo) ----------------
    [ContextMenu("3. Gerar FAVELA")]
    public void GerarFavela()
    {
        _ters = null; _rng = 4242u;
        var raiz = Raiz("CIDADE_FAVELA");
        float fx = (float)RioDados.FAVELA_X, fz = (float)RioDados.FAVELA_Z;
        float R = (float)RioDados.FAVELA_R;

        var vs = new List<Vector3>(); var ts = new List<int>(); var uv = new List<Vector2>();
        int n = 0;
        for (float r = 14f; r < R; r += RioCidadeDados.FAV_PASSO)
        {
            float passoAng = Mathf.Max(RioCidadeDados.FAV_PASSO / r, 0.03f);
            for (float a = 0; a < Mathf.PI * 2f; a += passoAng)
            {
                if (R01() < 0.10f) continue; // vielas
                float px = fx + Mathf.Cos(a) * (r + RFx(-3f, 3f));
                float pz = fz + Mathf.Sin(a) * (r + RFx(-3f, 3f));
                float h = AlturaChao(px, pz);
                if (h < 12f) continue;
                float dec = Declive(px, pz);
                if (dec > 38f) continue;
                float lado = RFx(RioCidadeDados.FAV_CASA_MIN, RioCidadeDados.FAV_CASA_MAX);
                float alt = RFx(RioCidadeDados.FAV_ALT_MIN, RioCidadeDados.FAV_ALT_MAX);
                // orienta tangente ao morro (contorno)
                Vector2 tang = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
                tang = (tang + new Vector2(RFx(-0.3f, 0.3f), RFx(-0.3f, 0.3f))).normalized;
                Caixa(vs, ts, uv, new Vector2(px, pz), tang, lado, lado * RFx(0.7f, 1f), h - 1.2f, alt);
                n++;
            }
        }
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vs); mesh.SetTriangles(ts, 0); mesh.SetUVs(0, uv);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        SalvarMesh(mesh, "Favela");
        var mat = MaterialPredio("M_Favela", new Color(0.62f, 0.42f, 0.32f), 0.45f, 4);
        var go = new GameObject("Favela");
        go.transform.SetParent(raiz, false);
        go.AddComponent<MeshFilter>().sharedMesh = CarregarMesh("Favela");
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.AddComponent<MeshCollider>().sharedMesh = CarregarMesh("Favela"); // jogo acontece aqui
        AssetDatabase.SaveAssets();
        Debug.Log("[Cidade] favela: " + n + " casinhas");
    }

    // ---------------- CASAS DE ENCOSTA (cone de visao) ----------------
    [ContextMenu("5. Gerar CASAS DE ENCOSTA")]
    public void GerarEncosta()
    {
        _ters = null; _rng = 31337u;
        var raiz = Raiz("CIDADE_ENCOSTA");
        var vs = new List<Vector3>(); var ts = new List<int>(); var uv = new List<Vector2>();
        float fx = (float)RioDados.FAVELA_X, fz = (float)RioDados.FAVELA_Z;
        float R = (float)RioDados.FAVELA_R;
        // ruas ocupadas: casinha nunca em cima nem colada
        var ocupRua = new HashSet<long>();
        {
            List<float> lgs; List<bool> pts;
            foreach (var rua in TodasAsRuas(out lgs, out pts)) foreach (var q in rua)
            {
                int cx = Mathf.FloorToInt(q.x / 8f), cz = Mathf.FloorToInt(q.y / 8f);
                for (int a2 = -1; a2 <= 1; a2++) for (int b2 = -1; b2 <= 1; b2++)
                    ocupRua.Add(((long)(cx + a2 + 100000) << 21) | (long)(cz + b2 + 100000));
            }
        }
        int n = 0;
        for (float x = fx - 320f; x < 950f; x += 13f)
        for (float z = -2700f; z < 1900f; z += 13f)
        {
            if (!NoCone(x, z)) continue;
            float dx = x - fx, dz = z - fz;
            float dFav = Mathf.Sqrt(dx * dx + dz * dz);
            if (dFav < R + 12f) continue;   // a favela ja tem casario proprio
            float h = AlturaChao(x, z);
            if (h < 8f || h > 115f) continue;
            float dec = Declive(x, z);
            if (dec < 12f || dec > 30f) continue;  // so encosta de morro de verdade
            bool emBairro = false;
            for (int b3 = 0; b3 < RioDados.BAIRRO_NOME.Length; b3++)
                if (DentroBairro(b3, x, z)) { emBairro = true; break; }
            if (emBairro && dec < 20f) continue;   // faixa suave dentro da cidade: NADA de casinha
            if (ocupRua.Contains(((long)(Mathf.FloorToInt(x / 8f) + 100000) << 21) | (long)(Mathf.FloorToInt(z / 8f) + 100000))) continue;
            if (R01() < 0.42f) continue;
            float hx = AlturaChao(x + 5f, z), hz = AlturaChao(x, z + 5f);
            Vector2 tang = new Vector2(-(hz - h), hx - h);
            tang = tang.sqrMagnitude > 0.01f ? tang.normalized : Vector2.right;
            float lado = RFx(4.5f, 8.5f);
            Caixa(vs, ts, uv, new Vector2(x + RFx(-3f, 3f), z + RFx(-3f, 3f)), tang,
                  lado, lado * RFx(0.7f, 1.05f), h - 1.4f, RFx(3f, 6.5f));
            n++;
        }
        var mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vs); mesh.SetTriangles(ts, 0); mesh.SetUVs(0, uv);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        SalvarMesh(mesh, "CasasEncosta");
        var mat = MaterialPredio("M_Favela", new Color(0.62f, 0.42f, 0.32f), 0.45f, 4);
        var go = new GameObject("CasasEncosta");
        go.transform.SetParent(raiz, false);
        go.AddComponent<MeshFilter>().sharedMesh = CarregarMesh("CasasEncosta");
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        AssetDatabase.SaveAssets();
        Debug.Log("[Cidade] encosta: " + n + " casas");
    }

    // ---------------- NOITE ----------------
    [ContextMenu("4. Gerar NOITE (postes + luz)")]
    public void GerarNoite()
    {
        _ters = null; _rng = 999u;
        var raiz = Raiz("CIDADE_NOITE");

        // ILUMINACAO GLOBAL: gerida fora daqui (perfil Rio_FimDeTarde / RioClima).
        // Este metodo so gera geometria de postes e as luzes da favela.
        // postes: geometria combinada + lampada emissiva (SEM luz realtime fora da favela)
        List<float> largs; List<bool> postes;
        var ruas = TodasAsRuas(out largs, out postes);
        var vs = new List<Vector3>(); var ts = new List<int>(); var uvp = new List<Vector2>();
        var vsL = new List<Vector3>(); var tsL = new List<int>(); var uvL = new List<Vector2>();
        int nPostes = 0;
        for (int r = 0; r < ruas.Count; r++)
        {
            if (!postes[r]) continue;
            var pts = ruas[r];
            float acum = 0;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                float L = Vector2.Distance(a, b);
                Vector2 d = (b - a) / Mathf.Max(L, 0.01f);
                Vector2 n = new Vector2(-d.y, d.x);
                for (float t = 0; t < L; t += 4f)
                {
                    acum += 4f;
                    if (acum < 30f) continue;
                    acum = 0;
                    Vector2 p2 = a + d * t + n * (largs[r] * 0.5f + 1f);
                    float h = AlturaChao(p2.x, p2.y);
                    if (h < 0.8f || h > 60f) continue;
                    if (Declive(p2.x, p2.y) > 13f) continue;
                    // poste: caixinha 0.25 x 5.2
                    int v0 = vs.Count;
                    float py = h + 0.1f;
                    vs.Add(new Vector3(p2.x - 0.12f, py, p2.y - 0.12f));
                    vs.Add(new Vector3(p2.x + 0.12f, py, p2.y - 0.12f));
                    vs.Add(new Vector3(p2.x + 0.12f, py + 5.2f, p2.y + 0.12f));
                    vs.Add(new Vector3(p2.x - 0.12f, py + 5.2f, p2.y + 0.12f));
                    uvp.Add(Vector2.zero); uvp.Add(Vector2.zero); uvp.Add(Vector2.zero); uvp.Add(Vector2.zero);
                    ts.Add(v0); ts.Add(v0 + 2); ts.Add(v0 + 1); ts.Add(v0); ts.Add(v0 + 3); ts.Add(v0 + 2);
                    // lampada: quad 0.9 m no topo
                    int l0 = vsL.Count;
                    vsL.Add(new Vector3(p2.x - 0.45f, py + 5.0f, p2.y));
                    vsL.Add(new Vector3(p2.x + 0.45f, py + 5.0f, p2.y));
                    vsL.Add(new Vector3(p2.x + 0.45f, py + 5.6f, p2.y));
                    vsL.Add(new Vector3(p2.x - 0.45f, py + 5.6f, p2.y));
                    uvL.Add(Vector2.zero); uvL.Add(Vector2.zero); uvL.Add(Vector2.zero); uvL.Add(Vector2.zero);
                    tsL.Add(l0); tsL.Add(l0 + 2); tsL.Add(l0 + 1); tsL.Add(l0); tsL.Add(l0 + 3); tsL.Add(l0 + 2);
                    tsL.Add(l0); tsL.Add(l0 + 1); tsL.Add(l0 + 2); tsL.Add(l0); tsL.Add(l0 + 2); tsL.Add(l0 + 3);
                    nPostes++;
                }
            }
        }
        var meshP = new Mesh(); meshP.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        meshP.SetVertices(vs); meshP.SetTriangles(ts, 0); meshP.SetUVs(0, uvp);
        meshP.RecalculateNormals(); meshP.RecalculateBounds();
        SalvarMesh(meshP, "Postes");
        var meshL = new Mesh(); meshL.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        meshL.SetVertices(vsL); meshL.SetTriangles(tsL, 0); meshL.SetUVs(0, uvL);
        var nrmL = new Vector3[vsL.Count];
        for (int i = 0; i < nrmL.Length; i++) nrmL[i] = Vector3.up;  // dupla-face: normal fixa (NaN mata o bloom)
        meshL.SetNormals(new List<Vector3>(nrmL)); meshL.RecalculateBounds();
        SalvarMesh(meshL, "Lampadas");

        var goP = new GameObject("Postes");
        goP.transform.SetParent(raiz, false);
        goP.AddComponent<MeshFilter>().sharedMesh = CarregarMesh("Postes");
        goP.AddComponent<MeshRenderer>().sharedMaterial = MaterialSimples("M_Poste", new Color(0.10f, 0.10f, 0.11f), Color.black);
        var goL = new GameObject("Lampadas");
        goL.transform.SetParent(raiz, false);
        goL.AddComponent<MeshFilter>().sharedMesh = CarregarMesh("Lampadas");
        goL.AddComponent<MeshRenderer>().sharedMaterial = MaterialSimples("M_Lampada", new Color(0.05f, 0.04f, 0.03f), new Color(1.05f, 0.78f, 0.45f));

        // LUZ REALTIME so na favela: ~36 pontos quentes, sem sombra
        float fx = (float)RioDados.FAVELA_X, fz = (float)RioDados.FAVELA_Z;
        var luzes = new GameObject("LuzesFavela");
        luzes.transform.SetParent(raiz, false);
        int nl = 0;
        for (int i = 0; i < 200 && nl < 36; i++)
        {
            float ang = R01() * Mathf.PI * 2f, rr = Mathf.Sqrt(R01()) * (float)RioDados.FAVELA_R;
            float px = fx + Mathf.Cos(ang) * rr, pz = fz + Mathf.Sin(ang) * rr;
            float h = AlturaChao(px, pz);
            if (h < 12f) continue;
            var lg = new GameObject("luz_" + nl);
            lg.transform.SetParent(luzes.transform, false);
            lg.transform.position = new Vector3(px, h + 4.2f, pz);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.72f, 0.45f);
            l.intensity = 1.5f;
            l.range = 16f;
            l.shadows = LightShadows.None;
            nl++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Cidade] noite: " + nPostes + " postes, " + nl + " luzes na favela");
    }
#endif
}
