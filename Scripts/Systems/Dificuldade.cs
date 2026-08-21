using UnityEngine;

/// <summary>
/// DIFICULDADE CONTINUA, no molde do Risk of Rain 2.
///
/// O problema do modelo antigo era ser LINEAR COM TETO: vida, dano, velocidade
/// e ritmo de spawn batiam no maximo e a partir dali o jogo era identico pra
/// sempre. Aqui o coeficiente nunca para de crescer.
///
/// Formula do RoR2:
///     coeff = (fatorJogador + minutos * fatorTempo) * fatorEstagio ^ wavesFeitas
///     nivel = 1 + (coeff - fatorJogador) / 0.33
/// Cada nivel da +30% de vida e +20% de dano sobre a base. A vida sobe mais
/// rapido que o dano de proposito: o inimigo fica DURO antes de ficar LETAL,
/// o que pressiona o dano do jogador em vez de mata-lo de surpresa.
/// </summary>
public class Dificuldade : MonoBehaviour
{
    public static Dificuldade Instancia { get; private set; }

    [Header("Curva (padrao RoR2)")]
    [Tooltip("Base. No RoR2 e 1 + 0.3*(jogadores-1); solo = 1.")]
    [SerializeField] private float fatorJogador = 1f;
    [Tooltip("Quanto o coeff cresce POR MINUTO. RoR2: 0.0506 * dificuldade.")]
    [SerializeField] private float ganhoPorMinuto = 0.1012f;   // equivalente a Rainstorm
    [Tooltip("Multiplicador exponencial por wave concluida. RoR2 usa 1.15 por ESTAGIO; como sua wave e bem mais curta que um estagio, o valor certo aqui e menor.")]
    [SerializeField] private float fatorPorWave = 1.015f;

    [Header("Efeito por nivel")]
    [SerializeField] private float vidaPorNivel = 0.30f;
    [SerializeField] private float danoPorNivel = 0.20f;
    [Tooltip("Velocidade cresce bem devagar: zumbi rapido demais fica injusto.")]
    [SerializeField] private float velocidadePorNivel = 0.012f;
    [SerializeField] private float velocidadeMaxima = 4.2f;

    [Header("Recompensa")]
    [Tooltip("Dinheiro e XP tambem escalam com o coeff, senao a economia fica pra tras.")]
    [SerializeField] private float recompensaPorNivel = 0.16f;

    private float tempoDecorrido;
    private int wavesFeitas;

    /// <summary>Segundos desde o inicio da run.</summary>
    public float Tempo { get { return tempoDecorrido; } }
    public int WavesFeitas { get { return wavesFeitas; } }

    /// <summary>O coeficiente cru. Nunca para de subir.</summary>
    public float Coeff
    {
        get
        {
            float minutos = tempoDecorrido / 60f;
            return (fatorJogador + minutos * ganhoPorMinuto) * Mathf.Pow(fatorPorWave, wavesFeitas);
        }
    }

    /// <summary>Nivel dos inimigos, na escala do RoR2.</summary>
    [Header("Teto")]
    [Tooltip("Nivel maximo. O RoR2 para em 99 - passando disso os numeros deixam de significar algo.")]
    [SerializeField] private float nivelMaximo = 99f;

    public float Nivel { get { return Mathf.Min(nivelMaximo, 1f + (Coeff - fatorJogador) / 0.33f); } }
    public int NivelInteiro { get { return Mathf.Max(1, Mathf.FloorToInt(Nivel)); } }

    public float MultVida { get { return 1f + vidaPorNivel * (Nivel - 1f); } }
    public float MultDano { get { return 1f + danoPorNivel * (Nivel - 1f); } }
    public float MultRecompensa { get { return 1f + recompensaPorNivel * (Nivel - 1f); } }
    public float Velocidade(float baseVel)
    {
        return Mathf.Min(velocidadeMaxima, baseVel * (1f + velocidadePorNivel * (Nivel - 1f)));
    }

    // ---------------- faixas nomeadas (o medidor do canto) ----------------

    private static readonly string[] Faixas = new string[]
    {
        "FÁCIL", "NORMAL", "DIFÍCIL", "MUITO DIFÍCIL", "INSANO",
        "IMPOSSÍVEL", "ESTOU TE VENDO", "ESTOU INDO ATRÁS DE VOCÊ", "HAHAHAHA"
    };

    private static readonly Color[] CoresFaixa = new Color[]
    {
        new Color(0.45f, 0.85f, 0.45f),
        new Color(0.55f, 0.85f, 1.00f),
        new Color(1.00f, 0.82f, 0.25f),
        new Color(1.00f, 0.58f, 0.18f),
        new Color(1.00f, 0.32f, 0.22f),
        new Color(0.85f, 0.20f, 0.35f),
        new Color(0.75f, 0.25f, 0.85f),
        new Color(0.55f, 0.20f, 0.95f),
        new Color(0.95f, 0.10f, 0.10f)
    };

    /// <summary>Cada faixa tem 3 entalhes, igual ao RoR2.</summary>
    private const int EntalhesPorFaixa = 3;
    private const float NiveisPorEntalhe = 2.2f;

    public int IndiceFaixa
    {
        get
        {
            int entalhe = Mathf.FloorToInt((Nivel - 1f) / NiveisPorEntalhe);
            return Mathf.Clamp(entalhe / EntalhesPorFaixa, 0, Faixas.Length - 1);
        }
    }

    public string NomeFaixa { get { return Faixas[IndiceFaixa]; } }
    public Color CorFaixa { get { return CoresFaixa[IndiceFaixa]; } }

    /// <summary>0..1 de quanto falta pra proxima faixa (enche a barra).</summary>
    public float ProgressoFaixa
    {
        get
        {
            float porFaixa = NiveisPorEntalhe * EntalhesPorFaixa;
            float dentro = (Nivel - 1f) - IndiceFaixa * porFaixa;
            return Mathf.Clamp01(dentro / porFaixa);
        }
    }

    /// <summary>Entalhe atual dentro da faixa (0,1,2) - os tracinhos da barra.</summary>
    public int EntalheNaFaixa
    {
        get { return Mathf.Clamp(Mathf.FloorToInt(ProgressoFaixa * EntalhesPorFaixa), 0, EntalhesPorFaixa - 1); }
    }

    // ---------------- ciclo ----------------

    private void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(this); return; }
        Instancia = this;
    }

    private void OnDestroy() { if (Instancia == this) Instancia = null; }

    private void Update()
    {
        // nao conta tempo com o jogo pausado nem no menu
        if (Time.timeScale <= 0f) return;
        tempoDecorrido += Time.deltaTime;
    }

    public void RegistrarWaveConcluida() { wavesFeitas++; }

    public string TempoFormatado
    {
        get
        {
            int t = Mathf.FloorToInt(tempoDecorrido);
            return (t / 60).ToString("00") + ":" + (t % 60).ToString("00");
        }
    }
}
