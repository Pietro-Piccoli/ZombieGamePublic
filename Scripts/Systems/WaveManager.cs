using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Waves por COTA DE KILLS: cada wave tem um alvo de abates. Os zumbis nao
/// param de nascer (respeitando a trava de vivos); bateu a cota, sobe a wave
/// e a dificuldade. Progressao acontece MATANDO, nao esperando.
/// Todo o balanceamento exposto no Inspector.
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private SpawnDirector spawner;
    [SerializeField] private GameObject zombiePrefab;

    [Header("Cota de kills por wave")]
    [Tooltip("Abates pra fechar a wave 1.")]
    [SerializeField] private int baseKillQuota = 12;
    [Tooltip("Quanto a cota sobe por wave.")]
    [SerializeField] private int quotaPerWave = 6;
    [Tooltip("Cota maxima, nao importa a wave.")]
    [SerializeField] private int maxQuota = 100;

    [Header("Ritmo do spawn")]
    [Tooltip("Segundos entre spawns na wave 1.")]
    [SerializeField] private float baseSpawnInterval = 1.1f;
    [Tooltip("Multiplicador por wave. 0.92 = spawns 8% mais rapidos a cada wave.")]
    [SerializeField] private float spawnIntervalDecay = 0.92f;
    [Tooltip("Intervalo minimo entre spawns, nao importa a wave.")]
    [SerializeField] private float minSpawnInterval = 0.15f;
    [Tooltip("TRAVA DE PERFORMANCE: maximo de zumbis vivos ao mesmo tempo.")]
    [SerializeField] private int maxAliveAtOnce = 60;

    [Header("Velocidade do zumbi")]
    [SerializeField] private float baseSpeed = 1.6f;
    [SerializeField] private float speedPerWave = 0.12f;
    [SerializeField] private float maxSpeed = 4.5f;

    [Header("Vida do zumbi")]
    [SerializeField] private int baseHealth = 60;
    [SerializeField] private int healthPerWave = 10;
    [SerializeField] private int maxHealth = 400;

    [Header("Dano do zumbi")]
    [SerializeField] private int baseDamage = 10;
    [SerializeField] private int damagePerWave = 2;
    [SerializeField] private int maxDamage = 40;

    [Header("Director por creditos (modelo RoR2)")]
    [Tooltip("Os tipos que o director pode comprar. Adicionar zumbi novo = criar o asset e arrastar aqui.")]
    [SerializeField] private TipoZumbi[] tiposDeZumbi;
    [Tooltip("Creditos por segundo na dificuldade 1. Cresce junto com o coeficiente.")]
    [SerializeField] private float creditosPorSegundo = 9f;
    [Tooltip("Teto de creditos guardados: evita despejar 30 zumbis de uma vez depois de uma pausa.")]
    [SerializeField] private float creditosMaximos = 260f;
    [Tooltip("Quantos zumbis o director pode soltar no mesmo instante.")]
    [SerializeField] private int comprasPorTique = 3;

    [Header("Pausas")]
    [Tooltip("Segundos antes da wave 1 comecar.")]
    [SerializeField] private float timeBeforeFirstWave = 4f;
    [Tooltip("Descanso entre waves. Os zumbis que sobraram continuam vivos.")]
    [SerializeField] private float timeBetweenWaves = 6f;

    [Header("Debug")]
    [Tooltip("HUD provisorio via OnGUI (desliga quando o Canvas estiver na cena).")]
    [SerializeField] private bool showDebugHud = false;

    public int CurrentWave { get; private set; }
    public int ZombiesAlive { get; private set; }
    public int KillsThisWave { get; private set; }
    public int KillQuota { get; private set; }
    public int TotalKills { get; private set; }
    public bool InBreak { get; private set; }
    public float BreakTimeLeft { get; private set; }
    /// <summary>0..1 do progresso da wave atual (pra barrinha).</summary>
    public float WaveProgress => KillQuota <= 0 ? 0f : Mathf.Clamp01((float)KillsThisWave / KillQuota);

    public event Action<int> OnWaveStarted;
    public event Action<int, float> OnBreakStarted;
    public event Action OnWaveCleared;

    private float creditos;
    private Dificuldade dif;
    /// <summary>Creditos guardados agora (diagnostico).</summary>
    public float Creditos { get { return creditos; } }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        dif = Dificuldade.Instancia;
        if (dif == null) dif = gameObject.AddComponent<Dificuldade>();
        if (spawner == null) spawner = FindAnyObjectByType<SpawnDirector>();
        if (zombiePrefab == null)
        {
            Debug.LogError("[WaveManager] Falta arrastar o prefab do zumbi.", this);
            return;
        }
        StartCoroutine(RunWaves());
    }

    private IEnumerator RunWaves()
    {
        yield return Break(timeBeforeFirstWave, 1);

        while (true)
        {
            CurrentWave++;
            yield return RunSingleWave(CurrentWave);

            if (dif != null) dif.RegistrarWaveConcluida();
            OnWaveCleared?.Invoke();
            yield return Break(timeBetweenWaves, CurrentWave + 1);
        }
    }

    private IEnumerator RunSingleWave(int wave)
    {
        KillQuota = QuotaForWave(wave);
        KillsThisWave = 0;
        float interval = SpawnIntervalForWave(wave);

        OnWaveStarted?.Invoke(wave);

        // O director acumula credito proporcional ao coeficiente e COMPRA zumbis.
        // E isso que faz a composicao da luta mudar: no comeco so da pra pagar
        // zumbi comum; mais tarde o mesmo orcamento compra um Bruto ou tres
        // Corredores, e o director escolhe.
        while (KillsThisWave < KillQuota)
        {
            float coeff = dif != null ? dif.Coeff : 1f;
            creditos = Mathf.Min(creditosMaximos, creditos + creditosPorSegundo * coeff * interval);

            for (int i = 0; i < comprasPorTique; i++)
            {
                if (ZombiesAlive >= maxAliveAtOnce) break;
                if (!TentarComprarZumbi()) break;
            }

            yield return new WaitForSeconds(interval);
        }
    }

    /// <summary>
    /// O director escolhe UM tipo que cabe no orcamento e no nivel atual,
    /// paga o custo e o coloca no mundo. Devolve false quando nao da pra
    /// comprar nada (sem credito, sem ponto de spawn ou sem tipo liberado).
    /// </summary>
    private bool TentarComprarZumbi()
    {
        if (tiposDeZumbi == null || tiposDeZumbi.Length == 0) return false;

        float nivel = dif != null ? dif.Nivel : 1f;

        // candidatos: liberados no nivel E que cabem no credito guardado
        TipoZumbi escolhido = null;
        float somaPesos = 0f;
        for (int i = 0; i < tiposDeZumbi.Length; i++)
        {
            var tz = tiposDeZumbi[i];
            if (tz == null || !tz.Liberado(nivel) || tz.custo > creditos) continue;
            somaPesos += Mathf.Max(0.01f, tz.peso);
        }
        if (somaPesos <= 0f) return false;

        float r = UnityEngine.Random.value * somaPesos;
        for (int i = 0; i < tiposDeZumbi.Length; i++)
        {
            var tz = tiposDeZumbi[i];
            if (tz == null || !tz.Liberado(nivel) || tz.custo > creditos) continue;
            r -= Mathf.Max(0.01f, tz.peso);
            if (r <= 0f) { escolhido = tz; break; }
        }
        if (escolhido == null) return false;

        Vector3 ponto;
        if (spawner == null || !spawner.TryGetSpawnPoint(out ponto)) return false;

        creditos -= escolhido.custo;
        Nascer(escolhido, ponto);
        return true;
    }

    private void Nascer(TipoZumbi tipo, Vector3 ponto)
    {
        GameObject prefab = tipo.prefabProprio != null ? tipo.prefabProprio : zombiePrefab;
        GameObject z = Instantiate(prefab, ponto, Quaternion.identity);

        float multVida = dif != null ? dif.MultVida : 1f;
        float multDano = dif != null ? dif.MultDano : 1f;

        int vida = Mathf.Max(1, Mathf.RoundToInt(baseHealth * multVida * tipo.multVida));
        int dano = Mathf.Max(1, Mathf.RoundToInt(baseDamage * multDano * tipo.multDano));
        float vel = dif != null ? dif.Velocidade(baseSpeed * tipo.multVelocidade)
                                : baseSpeed * tipo.multVelocidade;

        ZombieAI ai = z.GetComponent<ZombieAI>();
        if (ai != null) ai.Configure(vel, vida, dano);

        if (Mathf.Abs(tipo.escala - 1f) > 0.001f)
            z.transform.localScale = z.transform.localScale * tipo.escala;

        // recompensa acompanha a dificuldade E o tipo, senao a economia fica pra tras
        var drop = z.GetComponent<DropOnDeath>();
        if (drop != null)
            drop.EscalarRecompensa((dif != null ? dif.MultRecompensa : 1f) * tipo.multRecompensa);

        if (tipo.tinta.a > 0.01f) Pintar(z, tipo.tinta);

        ZombiesAlive++;
    }

    /// <summary>Tinge o corpo pra dar leitura de longe de que tipo e aquele.</summary>
    private static void Pintar(GameObject alvo, Color tinta)
    {
        var bloco = new MaterialPropertyBlock();
        foreach (var r in alvo.GetComponentsInChildren<Renderer>(true))
        {
            r.GetPropertyBlock(bloco);
            bloco.SetColor("_BaseColor", Color.Lerp(Color.white, tinta, tinta.a));
            r.SetPropertyBlock(bloco);
        }
    }

    private IEnumerator Break(float duration, int nextWave)
    {
        InBreak = true;
        BreakTimeLeft = duration;
        OnBreakStarted?.Invoke(nextWave, duration);

        while (BreakTimeLeft > 0f)
        {
            BreakTimeLeft -= Time.deltaTime;
            yield return null;
        }

        BreakTimeLeft = 0f;
        InBreak = false;
    }

    /// <summary>Chamado pelo ZombieAI quando um zumbi morre.</summary>
    public void NotifyZombieKilled()
    {
        ZombiesAlive = Mathf.Max(0, ZombiesAlive - 1);
        TotalKills++;
        if (!InBreak) KillsThisWave++;
    }

    // ---- curvas de balanceamento ----

    public int QuotaForWave(int w) =>
        Mathf.Min(maxQuota, baseKillQuota + quotaPerWave * (w - 1));

    public float SpawnIntervalForWave(int w) =>
        Mathf.Max(minSpawnInterval, baseSpawnInterval * Mathf.Pow(spawnIntervalDecay, w - 1));

    public float SpeedForWave(int w) =>
        Mathf.Min(maxSpeed, baseSpeed + speedPerWave * (w - 1));

    public int HealthForWave(int w) =>
        Mathf.Min(maxHealth, baseHealth + healthPerWave * (w - 1));

    public int DamageForWave(int w) =>
        Mathf.Min(maxDamage, baseDamage + damagePerWave * (w - 1));

    private void OnGUI()
    {
        // nao desenha o debug por cima do menu nem da pausa
        if (MenuPrincipal.Aberto || MenuPausa.Pausado) return;
        if (!showDebugHud) return;
        GUI.Label(new Rect(12, 8, 420, 50),
            (InBreak ? "PROXIMA EM " + Mathf.CeilToInt(BreakTimeLeft) + "s"
                     : "WAVE " + CurrentWave + "  " + KillsThisWave + "/" + KillQuota)
            + "  vivos: " + ZombiesAlive + "  kills: " + TotalKills);
    }
}
