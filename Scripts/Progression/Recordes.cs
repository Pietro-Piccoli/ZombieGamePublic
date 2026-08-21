using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RECORDES E HISTORICO DE PARTIDAS.
///
/// O que faz voltar pra outra partida num roguelike nao e a partida em si,
/// e o PLACAR que ela deixa. Risk of Rain 2 guarda o registro de cada run,
/// Vampire Survivors termina te mostrando o que voce destravou, Balatro
/// tem historico com as maos de cada partida. Sem isso a run some quando
/// a tela de morte fecha, e nao sobra motivo pra fazer de novo.
///
/// Guarda tres coisas:
///   RECORDE   - o melhor de cada categoria, pra comparar na hora da morte
///   CARREIRA  - somatorio de tudo, que so cresce
///   HISTORICO - as ultimas 12 partidas, pra ver a evolucao
/// </summary>
public static class Recordes
{
    private const string K = "rec_";
    private const int MaxHistorico = 12;

    // ---------- recordes ----------
    public static int MelhorWave    { get { return PlayerPrefs.GetInt(K + "wave", 0); } }
    public static float MelhorTempo { get { return PlayerPrefs.GetFloat(K + "tempo", 0f); } }
    public static int MaisAbates    { get { return PlayerPrefs.GetInt(K + "abates", 0); } }
    public static int MaiorNivel    { get { return PlayerPrefs.GetInt(K + "nivel", 0); } }
    public static long MaiorDano    { get { return long.Parse(PlayerPrefs.GetString(K + "dano", "0")); } }
    public static int MaiorGolpe    { get { return PlayerPrefs.GetInt(K + "golpe", 0); } }
    public static int MaiorPicoDps  { get { return PlayerPrefs.GetInt(K + "dps", 0); } }

    // ---------- carreira ----------
    public static int Partidas      { get { return PlayerPrefs.GetInt(K + "partidas", 0); } }
    public static int AbatesTotais  { get { return PlayerPrefs.GetInt(K + "abtotal", 0); } }
    public static float TempoTotal  { get { return PlayerPrefs.GetFloat(K + "tempototal", 0f); } }

    /// <summary>Uma linha do historico.</summary>
    [System.Serializable]
    public class Partida
    {
        public int wave;
        public float tempo;
        public int abates;
        public int nivel;
        public string quando;
    }

    [System.Serializable]
    private class Lista { public List<Partida> itens = new List<Partida>(); }

    public static List<Partida> Historico()
    {
        string j = PlayerPrefs.GetString(K + "hist", "");
        if (string.IsNullOrEmpty(j)) return new List<Partida>();
        var l = JsonUtility.FromJson<Lista>(j);
        return l != null && l.itens != null ? l.itens : new List<Partida>();
    }

    /// <summary>O que foi batido nesta partida. A tela de morte usa pra piscar 'RECORDE'.</summary>
    public class Batidos
    {
        public bool wave, tempo, abates, nivel, dano, golpe, dps;
        public bool Algum { get { return wave || tempo || abates || nivel || dano || golpe || dps; } }
    }

    /// <summary>
    /// Fecha a partida: atualiza recorde, soma carreira, guarda no historico
    /// e devolve o que foi batido. Chamado uma vez, pela tela de fim de jogo.
    /// </summary>
    public static Batidos Fechar(EstatisticasRun e, int waveFinal)
    {
        var b = new Batidos();
        if (e == null) return b;

        int wave = Mathf.Max(waveFinal, e.WaveMaxima);
        float tempo = e.Duracao;

        if (wave > MelhorWave)          { b.wave = true;   PlayerPrefs.SetInt(K + "wave", wave); }
        if (tempo > MelhorTempo)        { b.tempo = true;  PlayerPrefs.SetFloat(K + "tempo", tempo); }
        if (e.Abates > MaisAbates)      { b.abates = true; PlayerPrefs.SetInt(K + "abates", e.Abates); }
        if (e.MaiorNivel > MaiorNivel)  { b.nivel = true;  PlayerPrefs.SetInt(K + "nivel", e.MaiorNivel); }
        if (e.DanoTotal > MaiorDano)    { b.dano = true;   PlayerPrefs.SetString(K + "dano", e.DanoTotal.ToString()); }
        if (e.MaiorGolpe > MaiorGolpe)  { b.golpe = true;  PlayerPrefs.SetInt(K + "golpe", e.MaiorGolpe); }
        if (e.PicoDps > MaiorPicoDps)   { b.dps = true;    PlayerPrefs.SetInt(K + "dps", e.PicoDps); }

        PlayerPrefs.SetInt(K + "partidas", Partidas + 1);
        PlayerPrefs.SetInt(K + "abtotal", AbatesTotais + e.Abates);
        PlayerPrefs.SetFloat(K + "tempototal", TempoTotal + tempo);

        var h = Historico();
        var nova = new Partida();
        nova.wave = wave; nova.tempo = tempo; nova.abates = e.Abates; nova.nivel = e.MaiorNivel;
        nova.quando = System.DateTime.Now.ToString("dd/MM HH:mm");
        h.Insert(0, nova);
        while (h.Count > MaxHistorico) h.RemoveAt(h.Count - 1);
        var l = new Lista(); l.itens = h;
        PlayerPrefs.SetString(K + "hist", JsonUtility.ToJson(l));

        PlayerPrefs.Save();
        return b;
    }

    public static string Relogio(float seg)
    {
        int t = Mathf.Max(0, Mathf.FloorToInt(seg));
        return (t / 60).ToString("00") + ":" + (t % 60).ToString("00");
    }

    public static void ApagarTudo()
    {
        foreach (string c in new string[]{"wave","tempo","abates","nivel","dano","golpe","dps","partidas","abtotal","tempototal","hist"})
            PlayerPrefs.DeleteKey(K + c);
        PlayerPrefs.Save();
    }
}
