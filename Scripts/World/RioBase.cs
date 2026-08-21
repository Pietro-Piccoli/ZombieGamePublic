using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Gera a base do Rio (Enseada de Botafogo / Corcovado) a partir da base
/// geografica conferida em RioDados. Mesmo algoritmo do mapa de cima:
/// cada morro e uma CRISTA com largura variavel, espigoes e sulcos.
/// Nada de cone, nada de aresta reta (a uniao entre segmentos e suave).
/// </summary>
[ExecuteAlways]
public class RioBase : MonoBehaviour
{
    [Header("Grade de terrenos")]
    public float tamanhoTile = 2000f;
    public int resolucao = 1025;
    public float baseY = -200f;
    public float alturaTile = 1000f;
    public int ix0 = -5, ix1 = 0;
    public int iz0 = -3, iz1 = 1;
    public string pasta = "Assets/Terrenos/Rio";

    [Header("Malha de distancia da costa")]
    public float passoSDF = 10f;   // 2000 / 10 = 200: as grades dos tiles casam exatamente
    public float margemSDF = 3000f;

    // ==================================================== RUIDO
    const double CA = 0.73933729, SA = 0.67334348;

    static double Hash01(int ix, int iz, int semente)
    {
        unchecked
        {
            uint h = (uint)ix * 374761393u + (uint)iz * 668265263u + (uint)semente * 1442695041u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h = h ^ (h >> 16);
            return h / 4294967296.0;
        }
    }

    static double Ruido(double x, double z, double freq, int semente)
    {
        double fx = x * freq, fz = z * freq;
        double ffx = System.Math.Floor(fx), ffz = System.Math.Floor(fz);
        double tx = fx - ffx, tz = fz - ffz;
        tx = tx * tx * (3.0 - 2.0 * tx);
        tz = tz * tz * (3.0 - 2.0 * tz);
        int ix = (int)ffx, iz = (int)ffz;
        double a = Hash01(ix, iz, semente);
        double b = Hash01(ix + 1, iz, semente);
        double c = Hash01(ix, iz + 1, semente);
        double d = Hash01(ix + 1, iz + 1, semente);
        return (a * (1 - tx) + b * tx) * (1 - tz) + (c * (1 - tx) + d * tx) * tz;
    }

    static double Fbm(double x, double z, double freq, int oit, int semente)
    {
        double s = 0, amp = 1, tot = 0, px = x, pz = z;
        for (int i = 0; i < oit; i++)
        {
            s += Ruido(px, pz, freq, semente + i * 17) * amp;
            tot += amp; amp *= 0.5; freq *= 2.0;
            double nx = px * CA - pz * SA;
            pz = px * SA + pz * CA; px = nx;
        }
        return s / tot;
    }

    static double Ridged(double x, double z, double freq, int oit, int semente)
    {
        double s = 0, amp = 1, tot = 0, px = x, pz = z;
        for (int i = 0; i < oit; i++)
        {
            double n = 1.0 - System.Math.Abs(Ruido(px, pz, freq, semente + i * 23) * 2.0 - 1.0);
            s += n * n * amp;
            tot += amp; amp *= 0.5; freq *= 2.1;
            double nx = px * CA - pz * SA;
            pz = px * SA + pz * CA; px = nx;
        }
        return s / tot;
    }

    static double Suave(double t)
    {
        if (t < 0) t = 0; else if (t > 1) t = 1;
        return t * t * (3.0 - 2.0 * t);
    }

    static double Smax(double a, double b, double k)
    {
        double h = 0.5 + 0.5 * (a - b) / k;
        if (h < 0) h = 0; else if (h > 1) h = 1;
        return b + (a - b) * h + k * h * (1.0 - h) * 0.5;
    }

    // ==================================== campo de distancia da costa
    float[] sdf;
    int sdfNX, sdfNZ;
    double sdfX0, sdfZ0, sdfP;

    static void PoliTerra(int a, int n, out double x, out double z)
    {
        double[] L = RioDados.LITORAL;
        if (a < n) { x = L[2 * a]; z = L[2 * a + 1]; return; }
        double zN = L[1], zS = L[2 * n - 1];
        if (a == n) { x = -14000.0; z = zS - 4000.0; return; }
        if (a == n + 1) { x = -14000.0; z = zN + 4000.0; return; }
        x = L[0]; z = zN + 4000.0;
    }

    public void ConstruirSDF(double x0, double z0, double x1, double z1)
    {
        sdfP = passoSDF;
        sdfX0 = x0 - margemSDF; sdfZ0 = z0 - margemSDF;
        sdfNX = (int)((x1 + margemSDF - sdfX0) / sdfP) + 2;
        sdfNZ = (int)((z1 + margemSDF - sdfZ0) / sdfP) + 2;
        sdf = new float[sdfNX * sdfNZ];
        double[] L = RioDados.LITORAL;
        int n = L.Length / 2;
        int m = n + 3;
        for (int j = 0; j < sdfNZ; j++)
        {
            double pz = sdfZ0 + j * sdfP;
            for (int i = 0; i < sdfNX; i++)
            {
                double px = sdfX0 + i * sdfP;
                double melhor = 1e18;
                for (int s = 0; s < n - 1; s++)
                {
                    double ax = L[2 * s], az = L[2 * s + 1];
                    double bx = L[2 * s + 2], bz = L[2 * s + 3];
                    double dx = bx - ax, dz = bz - az;
                    double l2 = dx * dx + dz * dz;
                    if (l2 < 1e-9) continue;
                    double t = ((px - ax) * dx + (pz - az) * dz) / l2;
                    if (t < 0) t = 0; else if (t > 1) t = 1;
                    double qx = px - (ax + t * dx), qz = pz - (az + t * dz);
                    double d = qx * qx + qz * qz;
                    if (d < melhor) melhor = d;
                }
                melhor = System.Math.Sqrt(melhor);
                bool dentro = false;
                int k = m - 1;
                for (int a = 0; a < m; a++)
                {
                    double xi, zi, xj, zj;
                    PoliTerra(a, n, out xi, out zi);
                    PoliTerra(k, n, out xj, out zj);
                    if (((zi > pz) != (zj > pz)) &&
                        (px < (xj - xi) * (pz - zi) / (zj - zi) + xi))
                        dentro = !dentro;
                    k = a;
                }
                sdf[j * sdfNX + i] = (float)(dentro ? melhor : -melhor);
            }
        }
    }

    double AmostraSDF(double x, double z)
    {
        double fx = (x - sdfX0) / sdfP, fz = (z - sdfZ0) / sdfP;
        int i = (int)fx, j = (int)fz;
        if (i < 0) i = 0;
        if (j < 0) j = 0;
        if (i > sdfNX - 2) i = sdfNX - 2;
        if (j > sdfNZ - 2) j = sdfNZ - 2;
        double tx = fx - i, tz = fz - j;
        if (tx < 0) tx = 0; else if (tx > 1) tx = 1;
        if (tz < 0) tz = 0; else if (tz > 1) tz = 1;
        double a = sdf[j * sdfNX + i], b = sdf[j * sdfNX + i + 1];
        double c = sdf[(j + 1) * sdfNX + i], d = sdf[(j + 1) * sdfNX + i + 1];
        return (a * (1 - tx) + b * tx) * (1 - tz) + (c * (1 - tx) + d * tx) * tz;
    }

    static double[] _picoMorro;
    static double PicoMorro(int mi)
    {
        if (_picoMorro == null)
        {
            int nm = RioDados.MORRO_NOME.Length;
            _picoMorro = new double[nm];
            double[] S = RioDados.SEG;
            for (int s = 0; s < S.Length; s += 9)
            {
                int m = (int)S[s];
                double h = System.Math.Max(S[s + 3], S[s + 7]);
                if (h > _picoMorro[m]) _picoMorro[m] = h;
            }
        }
        return _picoMorro[mi];
    }

    public static double DBG_sd, DBG_base, DBG_maxF, DBG_fLarg, DBG_fBase, DBG_nEsp;
    // ==================================================== ALTURA
    public double Altura(double x, double z)
    {
        double sd = AmostraSDF(x, z);
        double dS = System.Math.Abs(sd);
        bool terra = sd > 0;

        // PRAIA: rampa unica e suave, de -3 m (100 m mar adentro) ate a areia.
        // Nada de degrau na linha d'agua; declive de areia ~1.5 grau.
        double hPraia = -3.0 + 7.2 * Suave((sd + 60.0) / 160.0);
        double hCid = 16.0 * Suave((sd - 160.0) / 700.0)
                    + 22.0 * Suave((sd - 800.0) / 1800.0);
        double hMar = -(6.0 + 52.0 * Suave(dS / 1500.0) + 70.0 * Suave((dS - 1200.0) / 2600.0));
        double kMar = Suave((-sd - 40.0) / 110.0);   // 1 = mar fundo
        double h = (hPraia + hCid) * (1.0 - kMar) + hMar * kMar;
        // ruido: zero na faixa da praia, cresce pra dentro da cidade e pro fundo do mar
        double amp = (sd > 0 ? 6.0 : 12.0) * Suave((dS - 45.0) / 120.0);
        h += (Fbm(x, z, 0.0016, 5, 3) - 0.5) * amp;

        double w1 = (Fbm(x, z, 0.00060, 5, 11) - 0.5) * 900.0;
        double w2 = (Fbm(x, z, 0.00060, 5, 29) - 0.5) * 900.0;
        double fLarg = 0.55 + 1.05 * Fbm(x + w1, z + w2, 0.0016, 5, 41);
        double fBase = 0.70 + 0.65 * Fbm(x + w1, z + w2, 0.0011, 5, 53);
        double nEsp = Ridged(x, z, 0.0019, 5, 61) - 0.45;
        double nSul = Ridged(x * 1.5, z * 1.5, 0.0031, 4, 83);

        double maxF = 0.0;   // relevo dos morros, acima do chao local
        double[] S = RioDados.SEG;
        double[] P = RioDados.MORRO_PAR;
        int nm = RioDados.MORRO_NOME.Length;
        int ns = S.Length / 9;

        for (int mi = 0; mi < nm; mi++)
        {
            double perfil = P[mi * 4], rugo = P[mi * 4 + 1];
            double fbase = P[mi * 4 + 2], largura = P[mi * 4 + 3];
            double acc = 0.0, pico = PicoMorro(mi);
            bool tocou = false;
            for (int s = 0; s < ns; s++)
            {
                if ((int)S[s * 9] != mi) continue;
                double ax = S[s * 9 + 1], az = S[s * 9 + 2], ha = S[s * 9 + 3], wa = S[s * 9 + 4];
                double bx = S[s * 9 + 5], bz = S[s * 9 + 6], hb = S[s * 9 + 7], wb = S[s * 9 + 8];
                double alcance = System.Math.Max(wa, wb) * fbase * 1.5 + 60.0;
                if (x < System.Math.Min(ax, bx) - alcance || x > System.Math.Max(ax, bx) + alcance) continue;
                if (z < System.Math.Min(az, bz) - alcance || z > System.Math.Max(az, bz) + alcance) continue;

                double dx = bx - ax, dz = bz - az;
                double l2 = dx * dx + dz * dz;
                if (l2 < 1e-9) continue;
                double t = ((x - ax) * dx + (z - az) * dz) / l2;
                if (t < 0) t = 0; else if (t > 1) t = 1;
                double qx = x - (ax + t * dx), qz = z - (az + t * dz);
                double d = System.Math.Sqrt(qx * qx + qz * qz);
                double Hs = ha + (hb - ha) * t;
                double Ws = wa + (wb - wa) * t;

                double lc = System.Math.Max(Ws * 1.30 * fLarg, 50.0);
                double cume = Hs * System.Math.Pow(Suave(1.0 - d / lc), perfil * 0.34);

                double lb = System.Math.Max(Ws * fbase * fBase, 70.0);
                double bas = Hs * 0.46 * System.Math.Pow(Suave(1.0 - d / lb), 1.10);

                double v = cume > bas ? cume : bas;
                // uniao sem vies: maximo puro perto de zero, sela suave so entre campos relevantes
                double lo1 = acc < v ? acc : v, hi1 = acc > v ? acc : v;
                double wS1 = Suave((lo1 - 2.0) / 6.0);
                acc = hi1 + (Smax(hi1, lo1, 16.0) - hi1) * wS1;
                tocou = true;
            }
            if (!tocou || acc <= 0.5) continue;

            double rel = pico > 1.0 ? acc / pico : 0.0;
            if (rel > 1) rel = 1;
            double janela = rel * (1.0 - rel) * 4.0;
            acc += nEsp * largura * rugo * 0.11 * janela;
            double g = 1.0 - nSul; if (g < 0) g = 0; if (g > 1) g = 1;
            acc -= g * g * g * largura * 0.05 * janela;   // sulco raso, sem prateleira

            if (acc <= 0.0) continue;
            double lo2 = maxF < acc ? maxF : acc, hi2 = maxF > acc ? maxF : acc;
            double wS2 = Suave((lo2 - 2.0) / 6.0);
            maxF = hi2 + (Smax(hi2, lo2, 16.0) - hi2) * wS2;   // sela suave sem vies
        }

        DBG_sd = sd; DBG_maxF = maxF; DBG_base = h; DBG_fLarg = fLarg; DBG_fBase = fBase; DBG_nEsp = nEsp;
        // morro NASCE do chao (praia, cidade ou fundo do mar) e o chao some
        // gradualmente onde o morro domina: sem plato nem penhasco no pe.
        h = h * (1.0 - Suave(maxF / 60.0)) + maxF;

        double[] LG = RioDados.LAGOA;
        int nl = LG.Length / 2;
        bool dentroLag = false;
        double dLag = 1e18;
        for (int a = 0, b = nl - 1; a < nl; b = a++)
        {
            double xi = LG[2 * a], zi = LG[2 * a + 1];
            double xj = LG[2 * b], zj = LG[2 * b + 1];
            if (((zi > z) != (zj > z)) && (x < (xj - xi) * (z - zi) / (zj - zi) + xi))
                dentroLag = !dentroLag;
            double dx = xj - xi, dz = zj - zi;
            double l2 = dx * dx + dz * dz;
            if (l2 < 1e-9) continue;
            double t = ((x - xi) * dx + (z - zi) * dz) / l2;
            if (t < 0) t = 0; else if (t > 1) t = 1;
            double qx = x - (xi + t * dx), qz = z - (zi + t * dz);
            double d2 = qx * qx + qz * qz;
            if (d2 < dLag) dLag = d2;
        }
        double distLag = System.Math.Sqrt(dLag);
        if (dentroLag)
        {
            double fundo = RioDados.LAGOA_NIVEL
                         - RioDados.LAGOA_PROF * Suave(distLag / 160.0);
            if (fundo < h) h = fundo;
        }
        else if (distLag < 90.0)
        {
            // margem da Lagoa: MISTURA continua (zero degrau em qualquer raio)
            double alvo = RioDados.LAGOA_NIVEL + 1.8;
            double baixo = h < alvo ? h : alvo;
            double w = 1.0 - Suave(distLag / 90.0);   // 1 na borda, 0 a 90 m
            h = h + (baixo - h) * w;
        }

        h += (Fbm(x, z, 0.006, 4, 97) - 0.5) * 3.2;
        return h;
    }

#if UNITY_EDITOR
    [ContextMenu("1. Desligar terrenos antigos")]
    public void DesligarAntigos()
    {
        int n = 0;
        var ters = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in ters)
        {
            if (t.transform.parent != null && t.transform.parent.name == "RIO") continue;
            if (t.gameObject.activeSelf) { t.gameObject.SetActive(false); n++; }
        }
        Debug.Log("[Rio] terrenos antigos desligados (nao apagados): " + n);
    }

    [ContextMenu("2. Gerar TUDO")]
    public void GerarTudo()
    {
        for (int iz = iz0; iz <= iz1; iz++)
            for (int ix = ix0; ix <= ix1; ix++)
                GerarTile(ix, iz);
        AssetDatabase.SaveAssets();
        Debug.Log("[Rio] terminado");
    }

    public void GerarTile(int ix, int iz)
    {
        if (Application.isPlaying) { Debug.LogError("[Rio] em PLAY MODE - abortado"); return; }
        System.IO.Directory.CreateDirectory(pasta);
        double x0 = ix * (double)tamanhoTile, z0 = iz * (double)tamanhoTile;
        double x1 = x0 + tamanhoTile, z1 = z0 + tamanhoTile;

        ConstruirSDF(x0, z0, x1, z1);

        string nome = "RIO_" + ix + "_" + iz;
        string caminho = pasta + "/" + nome + ".asset";
        TerrainData td = AssetDatabase.LoadAssetAtPath<TerrainData>(caminho);
        bool novo = td == null;
        if (novo) td = new TerrainData();
        td.heightmapResolution = resolucao;
        td.size = new Vector3(tamanhoTile, alturaTile, tamanhoTile);
        if (novo) AssetDatabase.CreateAsset(td, caminho);

        int r = td.heightmapResolution;
        double passo = tamanhoTile / (double)(r - 1);
        float[,] h = new float[r, r];
        for (int j = 0; j < r; j++)
        {
            double wz = z0 + j * passo;
            for (int i = 0; i < r; i++)
            {
                double wx = x0 + i * passo;
                double y = Altura(wx, wz);
                double norm = (y - baseY) / alturaTile;
                if (norm < 0) norm = 0; else if (norm > 1) norm = 1;
                h[j, i] = (float)norm;
            }
        }
        td.SetHeights(0, 0, h);
        EditorUtility.SetDirty(td);

        GameObject raizGo = GameObject.Find("RIO");
        if (raizGo == null) raizGo = new GameObject("RIO");
        Transform raiz = raizGo.transform;

        Transform ja = raiz.Find(nome);
        GameObject go = ja != null ? ja.gameObject : Terrain.CreateTerrainGameObject(td);
        go.name = nome;
        go.transform.SetParent(raiz, true);
        go.transform.position = new Vector3((float)x0, baseY, (float)z0);
        var ter = go.GetComponent<Terrain>();
        ter.terrainData = td;
        ter.allowAutoConnect = true;
        var col = go.GetComponent<TerrainCollider>();
        if (col != null) col.terrainData = td;
        Debug.Log("[Rio] " + nome + " ok");
    }
#endif
}
