using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Vida do player: barra na tela, vinheta vermelha de dano, regeneracao
/// opcional, tela de morte com stats e restart no R.
/// Constroi a propria UI em codigo - zero montagem manual.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerVitals : MonoBehaviour
{
    [Header("Regeneracao")]
    [Tooltip("Vida volta sozinha depois de um tempo sem tomar dano.")]
    [SerializeField] private bool regenerate = true;
    [Tooltip("Segundos sem dano ate comecar a regenerar.")]
    [SerializeField] private float regenDelay = 5f;
    [SerializeField] private float regenPerSecond = 7f;

    [Header("Morte")]
    [Tooltip("Camera lenta ao morrer (1 = sem efeito).")]
    [Range(0.05f, 1f)]
    [SerializeField] private float deathSlowMotion = 0.3f;
    [Tooltip("Quanto tempo (real) a camera lenta da morte dura antes de voltar ao normal. Antes ela nao voltava nunca: o jogo ficava a 30% da velocidade na tela de game over ate apertar R.")]
    [SerializeField] private float duracaoCameraLenta = 1.2f;

    [Header("Barra de vida")]
    [SerializeField] private float barWidth = 320f;
    [SerializeField] private float barHeight = 20f;
    [SerializeField] private Color barBackground = new Color(0f, 0f, 0f, 0.5f);
    [SerializeField] private Color barHealthy = new Color(0.2f, 0.75f, 0.25f, 0.95f);
    [SerializeField] private Color barHurt = new Color(0.8f, 0.15f, 0.1f, 0.95f);

    [Header("Vinheta de dano")]
    [SerializeField] private Color vignetteColor = new Color(0.7f, 0f, 0f, 1f);
    [Tooltip("Alpha do flash ao tomar dano.")]
    [SerializeField] private float hitFlashAlpha = 0.5f;
    [SerializeField] private float vignetteFadeSpeed = 1.6f;
    [Tooltip("Abaixo desta fracao de vida, a vinheta pulsa constante.")]
    [SerializeField] private float lowHealthThreshold = 0.35f;

    private Health health;
    private bool dead;
    private float horaDaMorte;
    private float lastHurtTime;
    private float regenBuffer;
    private float flashAlpha;

    private Image hpFillImg;
    private TextMeshProUGUI hpText;
    private Image vignette;
    private GameObject gameOverPanel;
    private TextMeshProUGUI gameOverStats;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        health.OnDamaged += OnHurt;
        health.OnDeath += OnDeath;
    }

    private void OnDisable()
    {
        health.OnDamaged -= OnHurt;
        health.OnDeath -= OnDeath;
    }

    private void Start()
    {
        BuildUI();
    }

    private void Update()
    {
        if (dead)
        {
            if (InputReader.ReloadPressed) Restart();

            // A camera lenta da morte VOLTA sozinha. Antes ela ficava em 0,3
            // pra sempre (o Update sai fora aqui em cima quando 'dead'), entao
            // a tela de game over ficava toda arrastada ate apertar R.
            if (duracaoCameraLenta > 0.01f)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - horaDaMorte) / duracaoCameraLenta);
                Time.timeScale = Mathf.Lerp(deathSlowMotion, 1f, k * k);
            }
            else Time.timeScale = 1f;
            return;
        }
        // SEGURANCA: se o player nao esta morto, o jogo NAO pode ficar em camera
        // lenta. A slow motion da morte so era desfeita no Restart(); se o player
        // fosse curado/revivido por qualquer caminho, o jogo continuava a 30% da
        // velocidade e dava a sensacao de travar.
        if (Time.timeScale > 0f && Time.timeScale < 0.999f
            && Mathf.Abs(Time.timeScale - deathSlowMotion) < 0.01f)
        {
            Time.timeScale = 1f;
        }


        // regeneracao
        if (regenerate && !health.IsDead && health.Current < health.MaxHealth
            && Time.time - lastHurtTime >= regenDelay)
        {
            regenBuffer += regenPerSecond * Time.deltaTime;
            int whole = Mathf.FloorToInt(regenBuffer);
            if (whole > 0) { health.Heal(whole); regenBuffer -= whole; }
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        var progNv = GetComponent<PlayerProgression>();
        if (progNv != null) EstatisticasRun.RegistrarNivel(progNv.Nivel);
        if (hpFillImg == null) return;

        float pct = health.Percent;
        hpFillImg.fillAmount = Mathf.Lerp(hpFillImg.fillAmount, pct, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        hpFillImg.color = Color.Lerp(UIKit.VidaBaixa, UIKit.Vida, Mathf.Clamp01(pct / 0.5f));
        hpText.text = health.Current + " / " + health.MaxHealth;

        // vinheta: flash de dano decai; vida baixa pulsa constante
        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, vignetteFadeSpeed * Time.deltaTime);

        float lowPulse = 0f;
        if (pct < lowHealthThreshold)
        {
            float severity = 1f - pct / lowHealthThreshold;
            lowPulse = severity * 0.28f * (0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 4f));
        }

        Color c = vignetteColor;
        c.a = Mathf.Max(flashAlpha, lowPulse);
        vignette.color = c;
    }

    private void OnHurt(Vector3 dir)
    {
        // levar dano tem que ser sentido, nao so visto na barra
        ImpactoDeCamera.Tremer(0.30f);
        EstatisticasRun.RegistrarDanoRecebido(1);
        lastHurtTime = Time.time;
        regenBuffer = 0f;
        flashAlpha = hitFlashAlpha;
        ultimaPancada = dir;   // guardado pra escolher a animacao de morte certa
    }

    /// <summary>
    /// De onde veio a ultima porrada. Usado pra escolher entre as 6 animacoes de
    /// morte do pack (frente, costas, lado, agachado).
    /// </summary>
    private Vector3 ultimaPancada;

    private static readonly int HMorrer = Animator.StringToHash("Morrer");
    private static readonly int HMorto = Animator.StringToHash("Morto");
    private static readonly int HTipoMorte = Animator.StringToHash("TipoMorte");

    /// <summary>
    /// Escolhe qual das 6 mortes do pack tocar:
    ///   0 tiro de frente   1 headshot de frente
    ///   2 tiro nas costas  3 headshot nas costas
    ///   4 tiro de lado     5 morreu agachado
    /// </summary>
    private int EscolherMorte()
    {
        var agachar = GetComponent<Agachar>();
        if (agachar != null && agachar.Agachado) return 5;

        Vector3 d = new Vector3(ultimaPancada.x, 0f, ultimaPancada.z);
        if (d.sqrMagnitude < 1e-4f) return 0;
        // 'dir' aponta do agressor pro player, entao bater de frente significa
        // que a direcao esta CONTRA a frente do boneco
        float ang = Vector3.Angle(-d.normalized, transform.forward);
        if (ang < 60f) return 0;
        if (ang > 120f) return 2;
        return 4;
    }

    private void DesligarNaMorte<T>() where T : Behaviour
    {
        var c = GetComponent<T>();
        if (c != null) c.enabled = false;
    }


    private void OnDeath()
    {
        if (dead) return;
        dead = true;
        // meta-progressao: o dinheiro da run vai pro banco (estilo Vampire Survivors)
        var progMeta = GetComponent<PlayerProgression>();
        if (progMeta != null) MetaProgressao.Depositar(progMeta.Dinheiro);


        Time.timeScale = deathSlowMotion;
        horaDaMorte = Time.unscaledTime;

        // ---------- animacao de morte de verdade ----------
        // Antes o boneco so travava de pe com o controlador desligado. Agora ele
        // cai com um dos 6 clipes do pack, escolhido pela direcao do golpe.
        var anim = GetComponent<AnimacaoJogador>();
        var animator = anim != null ? anim.Animator : GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            // float, nao int: arvore de blend so aceita parametro float
            animator.SetFloat(HTipoMorte, EscolherMorte());
            animator.SetBool(HMorto, true);
            animator.SetTrigger(HMorrer);
        }

        // desliga TUDO que mexe em osso ou em input, senao a pose de mira e o IK
        // continuam puxando o corpo enquanto ele cai
        DesligarNaMorte<StarterAssets.ThirdPersonController>();
        DesligarNaMorte<WeaponController>();
        DesligarNaMorte<Crosshair>();
        DesligarNaMorte<PonteEntrada>();
        DesligarNaMorte<AnimacaoJogador>();
        DesligarNaMorte<Agachar>();
        DesligarNaMorte<EstadoPulo>();
        DesligarNaMorte<RecuoArma>();
        if (animator != null)
        {
            var tronco = animator.GetComponent<TroncoMira>();
            if (tronco != null) tronco.enabled = false;
            var mao = animator.GetComponent<MaoNaArma>();
            if (mao != null) mao.enabled = false;
            var pes = animator.GetComponent<PesNoChao>();
            if (pes != null) pes.enabled = false;
            var pose = animator.GetComponent<PoseBracos>();
            if (pose != null) pose.enabled = false;
        }


        var wm = WaveManager.Instance;
        if (gameOverStats != null && wm != null)
            gameOverStats.text = "Voce caiu na WAVE " + wm.CurrentWave
                + "\n" + wm.TotalKills + " zumbis abatidos"
                + "\n\nAperte R para recomecar";

        // painel antigo aposentado: a tela nova mostra o resumo completo da run
        TelaFimDeJogo.Mostrar();
        if (vignette != null)
        {
            Color c = vignetteColor; c.a = 0.55f; vignette.color = c;
        }
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ---------------- UI ----------------

    private void BuildUI()
    {
        var canvas = UIKit.NovoCanvas(transform, "PlayerHUD_Canvas", 60);

        // vinheta de dano (tela cheia, atras de tudo neste canvas)
        vignette = UIKit.Caixa(canvas.transform, "DamageVignette", new Color(0, 0, 0, 0), 1);
        UIKit.Esticar(vignette);

        // ---------- painel de vida (canto inferior esquerdo) ----------
        var painel = UIKit.PainelBordado(canvas.transform, "PainelVida", UIKit.Painel, UIKit.RaioPainel);
        UIKit.Por(painel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(22f, 22f), new Vector2(barWidth + 32f, 58f));
        var dentro = painel.transform.GetChild(0);

        var rotulo = UIKit.Texto3(dentro, "Rotulo", "VIDA", 13f, TextAlignmentOptions.Left, UIKit.TextoFraco, true);
        UIKit.Por(rotulo, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(120f, 18f));
        rotulo.characterSpacing = 8f;

        hpText = UIKit.Texto3(dentro, "HpText", "100 / 100", 15f, TextAlignmentOptions.Right, UIKit.Texto, true);
        UIKit.Por(hpText, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -8f), new Vector2(160f, 18f));

        hpFillImg = UIKit.Barra(dentro, "BarraVida", UIKit.Vida, 12f, UIKit.RaioBarra);
        var trilho = (RectTransform)hpFillImg.transform.parent;
        trilho.anchorMin = new Vector2(0f, 0f); trilho.anchorMax = new Vector2(1f, 0f);
        trilho.pivot = new Vector2(0.5f, 0f);
        trilho.offsetMin = new Vector2(16f, 12f); trilho.offsetMax = new Vector2(-16f, 0f);
        trilho.sizeDelta = new Vector2(trilho.sizeDelta.x, 12f);

        // ---------- painel de morte (desligado) ----------
        gameOverPanel = new GameObject("GameOverPanel");
        gameOverPanel.transform.SetParent(canvas.transform, false);
        var goRt = gameOverPanel.AddComponent<RectTransform>();
        goRt.anchorMin = Vector2.zero; goRt.anchorMax = Vector2.one; goRt.sizeDelta = Vector2.zero;

        var overlay = UIKit.Caixa(gameOverPanel.transform, "Overlay", new Color(0.02f, 0.01f, 0.015f, 0.80f), 1);
        UIKit.Esticar(overlay);

        var titulo = UIKit.Texto3(gameOverPanel.transform, "Titulo", "VOCÊ MORREU", 78f, TextAlignmentOptions.Center, new Color(0.86f, 0.14f, 0.14f), true);
        UIKit.Por(titulo, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 96f));
        titulo.characterSpacing = 10f;
        UIKit.Contornar(titulo, 0.2f);

        gameOverStats = UIKit.Texto3(gameOverPanel.transform, "Stats", "", 26f, TextAlignmentOptions.Center, UIKit.Texto, false);
        UIKit.Por(gameOverStats, new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 180f));
        gameOverStats.textWrappingMode = TextWrappingModes.Normal;
        gameOverStats.lineSpacing = 18f;

        gameOverPanel.SetActive(false);
    }
}
