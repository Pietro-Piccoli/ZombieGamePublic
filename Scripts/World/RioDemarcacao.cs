using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Desenha no chao a demarcacao geografica: linha de costa, praias, cristas
/// dos morros, areas de predio, lagoa e a favela onde o jogo acontece.
/// So gizmo: nao tem renderer nem collider, nao aparece no jogo.
/// </summary>
[ExecuteAlways]
public class RioDemarcacao : MonoBehaviour
{
    public bool litoral = true;
    public bool praias = true;
    public bool cristas = true;
    public bool bairros = true;
    public bool favela = true;
    public bool grade1km = true;
    public bool nomes = true;
    public float alturaDaLinha = 4f;

#if UNITY_EDITOR
    static Vector3 Q(double x, double z, float y) { return new Vector3((float)x, y, (float)z); }

    void OnDrawGizmos()
    {
        float y = alturaDaLinha;
        double[] L = RioDados.LITORAL;
        int n = L.Length / 2;

        if (litoral)
        {
            Gizmos.color = new Color(0.05f, 0.05f, 0.05f);
            for (int i = 0; i < n - 1; i++)
                Gizmos.DrawLine(Q(L[2 * i], L[2 * i + 1], y), Q(L[2 * i + 2], L[2 * i + 3], y));
        }

        if (praias)
        {
            double[] PR = RioDados.PRAIA;
            Gizmos.color = new Color(1f, 0.78f, 0.25f);
            for (int k = 0; k < RioDados.PRAIA_NOME.Length; k++)
            {
                int i0 = (int)PR[k * 3], i1 = (int)PR[k * 3 + 1];
                float w = (float)PR[k * 3 + 2];
                for (int i = i0; i < i1; i++)
                {
                    Vector3 a = Q(L[2 * i], L[2 * i + 1], y + 1f);
                    Vector3 b = Q(L[2 * i + 2], L[2 * i + 3], y + 1f);
                    Gizmos.DrawLine(a, b);
                    Vector3 d = (b - a).normalized;
                    Vector3 nr = new Vector3(-d.z, 0, d.x) * w;
                    Gizmos.DrawLine(a + nr, b + nr);
                    if (i == i0 || i == i1 - 1) Gizmos.DrawLine(a, a + nr);
                }
                if (nomes)
                {
                    int im = (i0 + i1) / 2;
                    Handles.color = new Color(0.55f, 0.33f, 0f);
                    Handles.Label(Q(L[2 * im], L[2 * im + 1], y + 40f), RioDados.PRAIA_NOME[k]);
                }
            }
        }

        if (bairros)
        {
            double[] BP = RioDados.BAIRRO_PT;
            int[] BI = RioDados.BAIRRO_INI;
            Gizmos.color = new Color(0.85f, 0.25f, 0.15f);
            for (int k = 0; k < RioDados.BAIRRO_NOME.Length; k++)
            {
                int a = BI[k], b = BI[k + 1];
                Vector3 c = Vector3.zero;
                for (int i = a; i < b; i++)
                {
                    int j = (i + 1 < b) ? i + 1 : a;
                    Gizmos.DrawLine(Q(BP[2 * i], BP[2 * i + 1], y + 2f),
                                    Q(BP[2 * j], BP[2 * j + 1], y + 2f));
                    c += Q(BP[2 * i], BP[2 * i + 1], y + 2f);
                }
                c /= Mathf.Max(1, b - a);
                if (nomes)
                {
                    Handles.color = new Color(0.6f, 0.1f, 0.05f);
                    Handles.Label(c + Vector3.up * 30f, RioDados.BAIRRO_NOME[k] + "  (predios)");
                }
            }
        }

        if (cristas)
        {
            double[] S = RioDados.SEG;
            int ns = S.Length / 9;
            for (int s = 0; s < ns; s++)
            {
                double ax = S[s * 9 + 1], az = S[s * 9 + 2], ha = S[s * 9 + 3];
                double bx = S[s * 9 + 5], bz = S[s * 9 + 6], hb = S[s * 9 + 7];
                Gizmos.color = new Color(0.1f, 0.1f, 0.1f);
                Gizmos.DrawLine(new Vector3((float)ax, (float)ha, (float)az),
                                new Vector3((float)bx, (float)hb, (float)bz));
                Gizmos.color = new Color(0.35f, 0.35f, 0.35f, 0.6f);
                Gizmos.DrawLine(Q(ax, az, y), new Vector3((float)ax, (float)ha, (float)az));
            }
            if (nomes)
            {
                for (int mi = 0; mi < RioDados.MORRO_NOME.Length; mi++)
                {
                    double bh = -1, bxx = 0, bzz = 0;
                    for (int s = 0; s < ns; s++)
                    {
                        if ((int)S[s * 9] != mi) continue;
                        if (S[s * 9 + 3] > bh) { bh = S[s * 9 + 3]; bxx = S[s * 9 + 1]; bzz = S[s * 9 + 2]; }
                        if (S[s * 9 + 7] > bh) { bh = S[s * 9 + 7]; bxx = S[s * 9 + 5]; bzz = S[s * 9 + 6]; }
                    }
                    Handles.color = Color.black;
                    Handles.Label(new Vector3((float)bxx, (float)bh + 40f, (float)bzz),
                                  RioDados.MORRO_NOME[mi] + "  " + Mathf.RoundToInt((float)bh) + " m");
                }
            }
        }

        double[] LG = RioDados.LAGOA;
        int nl = LG.Length / 2;
        Gizmos.color = new Color(0.2f, 0.5f, 0.85f);
        for (int i = 0; i < nl; i++)
        {
            int j = (i + 1) % nl;
            Gizmos.DrawLine(Q(LG[2 * i], LG[2 * i + 1], y), Q(LG[2 * j], LG[2 * j + 1], y));
        }

        if (favela)
        {
            Gizmos.color = Color.red;
            Vector3 c = Q(RioDados.FAVELA_X, RioDados.FAVELA_Z, 380f);
            float rr = (float)RioDados.FAVELA_R;
            Vector3 ant = Vector3.zero;
            for (int i = 0; i <= 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                Vector3 q = c + new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr);
                if (i > 0) Gizmos.DrawLine(ant, q);
                ant = q;
            }
            if (nomes)
            {
                Handles.color = Color.red;
                Handles.Label(c + Vector3.up * 50f, "FAVELA - onde o jogo acontece");
            }
        }

        if (grade1km)
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.12f);
            for (int gx = -10000; gx <= 2000; gx += 1000)
                Gizmos.DrawLine(new Vector3(gx, y, -6000), new Vector3(gx, y, 4000));
            for (int gz = -6000; gz <= 4000; gz += 1000)
                Gizmos.DrawLine(new Vector3(-10000, y, gz), new Vector3(2000, y, gz));
        }

        Gizmos.color = new Color(1f, 0.18f, 0.33f);
        Gizmos.DrawLine(new Vector3(-120, y, 0), new Vector3(120, y, 0));
        Gizmos.DrawLine(new Vector3(0, y, -120), new Vector3(0, y, 120));
        if (nomes)
        {
            Handles.color = new Color(1f, 0.18f, 0.33f);
            Handles.Label(new Vector3(0, 420f, 0), "ORIGEM (0,0) - cume do Pao de Acucar");
        }
    }
#endif
}
