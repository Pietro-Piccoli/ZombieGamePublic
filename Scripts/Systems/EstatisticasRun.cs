using UnityEngine;

/// <summary>
/// ESTATISTICAS DA PARTIDA. Tudo que a tela de fim de jogo mostra e coletado
/// aqui, ao vivo, pelos pontos que ja existem (tiro, explosao, DoT, morte).
///
/// Zera sozinha quando a cena recarrega, entao cada run tem os proprios numeros.
/// </summary>
public class EstatisticasRun : MonoBehaviour
{
    private static EstatisticasRun instancia;

    public static EstatisticasRun Atual
    {
        get
        {
            if (instancia == null)
            {
                var go = new GameObject("EstatisticasRun");
                instancia = go.AddComponent<EstatisticasRun>();
            }
            return instancia;
        }
    }

    private void Awake() { if (instancia != null && instancia != this) { Destroy(this); return; } instancia = this; }
    private void OnDestroy() { if (instancia == this) instancia = null; }

    // ---------------- acumuladores ----------------
    public long DanoTotal { get; private set; }
    public int MaiorGolpe { get; private set; }
    public int Abates { get; private set; }
    public int AbatesNaCabeca { get; private set; }
    public int TirosDados { get; private set; }
    public int TirosQueAcertaram { get; private set; }
    public int DinheiroGanho { get; private set; }
    public int MaiorNivel { get; private set; }
    public int CartasPegas { get; private set; }
    public int MaisZumbisDeUmaVez { get; private set; }
    public int WaveMaxima { get; private set; }
    public float DanoRecebido { get; private set; }

    /// <summary>Dano por tipo, pra tela mostrar de onde veio o estrago.</summary>
    public long DanoTiro { get; private set; }
    public long DanoCabeca { get; private set; }
    public long DanoExplosao { get; private set; }
    public long DanoFogo { get; private set; }
    public long DanoAcido { get; private set; }

    // pico de DPS: janela deslizante de 1 segundo
    private const float Janela = 1f;
    private readonly System.Collections.Generic.Queue<float> tempos = new System.Collections.Generic.Queue<float>();
    private readonly System.Collections.Generic.Queue<int> valores = new System.Collections.Generic.Queue<int>();
    private int somaJanela;
    public int PicoDps { get; private set; }

    public float Duracao { get { return Dificuldade.Instancia != null ? Dificuldade.Instancia.Tempo : Time.timeSinceLevelLoad; } }

    public float Precisao
    {
        get { return TirosDados <= 0 ? 0f : (float)TirosQueAcertaram / TirosDados; }
    }
    public float FracaoCabeca
    {
        get { return Abates <= 0 ? 0f : (float)AbatesNaCabeca / Abates; }
    }
    public float DpsMedio
    {
        get { return Duracao <= 1f ? DanoTotal : DanoTotal / Duracao; }
    }

    // ---------------- registro ----------------

    public static void RegistrarDano(int valor, DanoPopup.Tipo tipo)
    {
        var e = Atual;
        e.DanoTotal += valor;
        if (valor > e.MaiorGolpe) e.MaiorGolpe = valor;

        switch (tipo)
        {
            case DanoPopup.Tipo.Critico:  e.DanoCabeca += valor; break;
            case DanoPopup.Tipo.Explosao: e.DanoExplosao += valor; break;
            case DanoPopup.Tipo.Fogo:     e.DanoFogo += valor; break;
            case DanoPopup.Tipo.Acido:    e.DanoAcido += valor; break;
            default:                      e.DanoTiro += valor; break;
        }

        // janela de 1s pro pico de DPS
        float agora = Time.time;
        e.tempos.Enqueue(agora);
        e.valores.Enqueue(valor);
        e.somaJanela += valor;
        while (e.tempos.Count > 0 && agora - e.tempos.Peek() > Janela)
        {
            e.tempos.Dequeue();
            e.somaJanela -= e.valores.Dequeue();
        }
        if (e.somaJanela > e.PicoDps) e.PicoDps = e.somaJanela;
    }

    public static void RegistrarTiro() { Atual.TirosDados++; }
    public static void RegistrarAcerto() { Atual.TirosQueAcertaram++; }
    public static void RegistrarAbate(bool naCabeca)
    {
        var e = Atual;
        e.Abates++;
        if (naCabeca) e.AbatesNaCabeca++;
    }
    public static void RegistrarDinheiro(int v) { Atual.DinheiroGanho += v; }
    public static void RegistrarNivel(int n) { var e = Atual; if (n > e.MaiorNivel) e.MaiorNivel = n; }
    public static void RegistrarCarta() { Atual.CartasPegas++; }
    public static void RegistrarDanoRecebido(int v) { Atual.DanoRecebido += v; }

    private void Update()
    {
        var wm = WaveManager.Instance;
        if (wm == null) return;
        if (wm.ZombiesAlive > MaisZumbisDeUmaVez) MaisZumbisDeUmaVez = wm.ZombiesAlive;
        if (wm.CurrentWave > WaveMaxima) WaveMaxima = wm.CurrentWave;
    }

    public string DuracaoFormatada
    {
        get { int t = Mathf.FloorToInt(Duracao); return (t / 60).ToString("00") + ":" + (t % 60).ToString("00"); }
    }
}
