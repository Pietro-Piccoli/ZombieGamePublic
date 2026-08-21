
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// GUIA GEOGRAFICO DO RIO - Enseada de Botafogo / Pao de Acucar.
///
/// Isto NAO e cenario: e regua. So desenha gizmo na Scene view, nao tem
/// renderer nem colisor, nao aparece no jogo e nao encosta em terreno.
/// Serve pra voce esculpir o morro no lugar certo e na altura certa.
///
/// As coordenadas sao lat/lon REAIS, projetadas em metros. 1 unidade Unity
/// = 1 metro real. A origem (0,0) e o pico do Pao de Acucar.
///
/// VERDE  = coordenada conferida em fonte
/// AMARELO = posicao aproximada (altura costuma estar certa; o ponto pode
///           estar uns 100-200 m fora). Corrija a vontade no Inspector.
/// </summary>
[ExecuteAlways]
public class GuiaRio : MonoBehaviour
{
    public enum Tipo { Morro, Praia, Referencia }
    public enum Fonte { Conferido, Aproximado }

    [System.Serializable]
    public struct Marco
    {
        public string nome;
        public double lat;
        public double lon;
        [Tooltip("Altitude do cume, em metros acima do mar.")]
        public float altura;
        [Tooltip("Raio aproximado da base ao nivel do mar. E o circulo que voce preenche esculpindo.")]
        public float raioBase;
        public Tipo tipo;
        public Fonte fonte;
    }

    [Header("Projecao")]
    [Tooltip("Origem do mundo. Padrao = pico do Pao de Acucar.")]
    public double latOrigem = -22.94861;
    public double lonOrigem = -43.15722;
    [Tooltip("Metros reais por unidade Unity. 1 = escala real.")]
    public float escala = 1f;
    [Tooltip("Empurra o guia inteiro, pra encaixar no seu grid de terrenos.")]
    public Vector3 deslocamento = Vector3.zero;
    [Tooltip("Em que Y fica o nivel do mar no seu mundo.")]
    public float nivelDoMar = 0f;

    [Header("O que desenhar")]
    public bool morros = true;
    public bool praias = true;
    public bool nomes = true;
    public bool circulosDeBase = true;
    public bool gradeDeUmKm = true;
    [Range(0.2f, 4f)] public float grossuraDoPoste = 1f;

    // ---------------- os dados ----------------
    public Marco[] marcos = new Marco[]
    {
        // --- os morros da foto ---
        new Marco { nome = "Pao de Acucar",    lat = -22.94861, lon = -43.15722, altura = 396f, raioBase = 200f, tipo = Tipo.Morro, fonte = Fonte.Conferido },
        new Marco { nome = "Morro da Urca",    lat = -22.95278, lon = -43.16500, altura = 220f, raioBase = 250f, tipo = Tipo.Morro, fonte = Fonte.Aproximado },
        new Marco { nome = "Morro Cara de Cao",lat = -22.94250, lon = -43.16500, altura = 136f, raioBase = 180f, tipo = Tipo.Morro, fonte = Fonte.Aproximado },
        new Marco { nome = "Morro da Babilonia",lat= -22.96000, lon = -43.16800, altura = 180f, raioBase = 400f, tipo = Tipo.Morro, fonte = Fonte.Aproximado },
        new Marco { nome = "Morro do Leme",    lat = -22.96350, lon = -43.16400, altura = 217f, raioBase = 250f, tipo = Tipo.Morro, fonte = Fonte.Aproximado },
        new Marco { nome = "Corcovado (Cristo)",lat= -22.951944,lon = -43.210556,altura = 710f, raioBase = 500f, tipo = Tipo.Morro, fonte = Fonte.Conferido },
        new Marco { nome = "Morro Dona Marta", lat = -22.94700, lon = -43.19500, altura = 362f, raioBase = 350f, tipo = Tipo.Morro, fonte = Fonte.Aproximado },
        new Marco { nome = "Morro do Pasmado", lat = -22.94500, lon = -43.17600, altura =  70f, raioBase = 150f, tipo = Tipo.Morro, fonte = Fonte.Aproximado },

        // --- referencias de costa (a proporcao que voce quer acertar) ---
        new Marco { nome = "Praia de Botafogo N", lat = -22.94100, lon = -43.18050, altura = 0f, raioBase = 0f, tipo = Tipo.Praia, fonte = Fonte.Aproximado },
        new Marco { nome = "Praia de Botafogo S", lat = -22.94720, lon = -43.17800, altura = 0f, raioBase = 0f, tipo = Tipo.Praia, fonte = Fonte.Aproximado },
        new Marco { nome = "Praia Vermelha",      lat = -22.95530, lon = -43.16480, altura = 0f, raioBase = 0f, tipo = Tipo.Praia, fonte = Fonte.Aproximado },
        new Marco { nome = "Leme (inicio Copacabana)", lat = -22.96300, lon = -43.16900, altura = 0f, raioBase = 0f, tipo = Tipo.Praia, fonte = Fonte.Aproximado },
        new Marco { nome = "Boca da enseada (Urca)",   lat = -22.94300, lon = -43.16650, altura = 0f, raioBase = 0f, tipo = Tipo.Referencia, fonte = Fonte.Aproximado },
        new Marco { nome = "Boca da enseada (Flamengo)",lat= -22.93600, lon = -43.17300, altura = 0f, raioBase = 0f, tipo = Tipo.Referencia, fonte = Fonte.Aproximado },
    };

    /// <summary>lat/lon real -> posicao no mundo, em metros.</summary>
    public Vector3 Projetar(double lat, double lon, float altura)
    {
        double mPorGrauLat = 111132.0;
        double mPorGrauLon = 111320.0 * System.Math.Cos(latOrigem * System.Math.PI / 180.0);
        float x = (float)((lon - lonOrigem) * mPorGrauLon) * escala;
        float z = (float)((lat - latOrigem) * mPorGrauLat) * escala;
        return new Vector3(x, nivelDoMar + altura * escala, z) + deslocamento;
    }

    public Vector3 Projetar(Marco m) { return Projetar(m.lat, m.lon, m.altura); }

    /// <summary>Distancia real em metros entre dois marcos, pra conferir proporcao.</summary>
    public float DistanciaEntre(string a, string b)
    {
        Vector3 pa = Vector3.zero, pb = Vector3.zero; bool ok1 = false, ok2 = false;
        foreach (var m in marcos)
        {
            if (m.nome == a) { pa = Projetar(m.lat, m.lon, 0f); ok1 = true; }
            if (m.nome == b) { pb = Projetar(m.lat, m.lon, 0f); ok2 = true; }
        }
        if (!ok1 || !ok2) return -1f;
        return Vector2.Distance(new Vector2(pa.x, pa.z), new Vector2(pb.x, pb.z));
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Color verde = new Color(0.25f, 1f, 0.35f);
        Color amarelo = new Color(1f, 0.85f, 0.2f);
        Color azul = new Color(0.3f, 0.75f, 1f);

        if (gradeDeUmKm) DesenharGrade();

        var estilo = new GUIStyle();
        estilo.normal.textColor = Color.white;
        estilo.fontStyle = FontStyle.Bold;

        for (int i = 0; i < marcos.Length; i++)
        {
            Marco m = marcos[i];
            Vector3 pe = Projetar(m.lat, m.lon, 0f);
            Vector3 topo = Projetar(m);
            Color c = m.fonte == Fonte.Conferido ? verde : amarelo;
            if (m.tipo != Tipo.Morro) c = azul;
            Gizmos.color = c;

            if (m.tipo == Tipo.Morro)
            {
                if (!morros) continue;
                // poste do nivel do mar ate o cume: e a altura que voce tem que atingir
                for (int k = 0; k < Mathf.RoundToInt(grossuraDoPoste * 3f); k++)
                {
                    float o = (k - grossuraDoPoste) * 1.5f;
                    Gizmos.DrawLine(pe + new Vector3(o, 0f, 0f), topo + new Vector3(o, 0f, 0f));
                }
                Gizmos.DrawWireSphere(topo, 25f * escala);
                // marcas a cada 100 m de altura
                Gizmos.color = c * 0.6f;
                for (float h = 100f; h < m.altura; h += 100f)
                {
                    Vector3 t = Projetar(m.lat, m.lon, h);
                    Gizmos.DrawLine(t + Vector3.left * 15f, t + Vector3.right * 15f);
                }
                if (circulosDeBase && m.raioBase > 1f)
                {
                    Gizmos.color = c;
                    DesenharCirculo(pe, m.raioBase * escala);
                }
                if (nomes)
                    Handles.Label(topo + Vector3.up * 45f, m.nome + "  " + m.altura.ToString("F0") + " m", estilo);
            }
            else
            {
                if (!praias) continue;
                Gizmos.DrawLine(pe + Vector3.down * 30f, pe + Vector3.up * 60f);
                DesenharCirculo(pe, 40f * escala);
                if (nomes) Handles.Label(pe + Vector3.up * 70f, m.nome, estilo);
            }
        }

        // linhas de proporcao: as medidas que voce disse ser dificil manter
        Gizmos.color = new Color(1f, 0.35f, 0.35f);
        LinhaEntre("Praia de Botafogo S", "Pao de Acucar");
        LinhaEntre("Praia de Botafogo N", "Praia de Botafogo S");
        LinhaEntre("Boca da enseada (Urca)", "Boca da enseada (Flamengo)");
        LinhaEntre("Morro da Urca", "Pao de Acucar");
    }

    private void LinhaEntre(string a, string b)
    {
        Vector3 pa = Vector3.zero, pb = Vector3.zero; bool o1 = false, o2 = false;
        foreach (var m in marcos)
        {
            if (m.nome == a) { pa = Projetar(m.lat, m.lon, 0f); o1 = true; }
            if (m.nome == b) { pb = Projetar(m.lat, m.lon, 0f); o2 = true; }
        }
        if (!o1 || !o2) return;
        Gizmos.DrawLine(pa, pb);
        var e = new GUIStyle(); e.normal.textColor = new Color(1f, 0.6f, 0.6f);
        Handles.Label((pa + pb) * 0.5f + Vector3.up * 20f,
            Vector2.Distance(new Vector2(pa.x, pa.z), new Vector2(pb.x, pb.z)).ToString("F0") + " m", e);
    }

    private void DesenharCirculo(Vector3 centro, float raio)
    {
        Vector3 ant = centro + new Vector3(raio, 0f, 0f);
        for (int i = 1; i <= 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            Vector3 p = centro + new Vector3(Mathf.Cos(a) * raio, 0f, Mathf.Sin(a) * raio);
            Gizmos.DrawLine(ant, p);
            ant = p;
        }
    }

    private void DesenharGrade()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.12f);
        float ext = 7000f * escala;
        for (float v = -ext; v <= ext; v += 1000f * escala)
        {
            Gizmos.DrawLine(new Vector3(v, nivelDoMar, -ext) + deslocamento, new Vector3(v, nivelDoMar, ext) + deslocamento);
            Gizmos.DrawLine(new Vector3(-ext, nivelDoMar, v) + deslocamento, new Vector3(ext, nivelDoMar, v) + deslocamento);
        }
    }
#endif
}
