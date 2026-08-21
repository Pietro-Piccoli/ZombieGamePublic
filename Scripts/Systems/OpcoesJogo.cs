using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// AJUSTES DO JOGADOR, guardados no PlayerPrefs e aplicados na abertura.
///
/// Tres coisas aqui nao sao capricho, sao o minimo que qualquer jogo
/// vendido espera ter:
///
/// SENSIBILIDADE e pessoal. A que esta calibrada pra quem fez o jogo vai
/// parecer quebrada pra metade das pessoas.
///
/// QUALIDADE aqui mexe no que realmente custa quadro em URP - escala de
/// renderizacao, distancia e resolucao de sombra, cascatas - em vez de
/// so trocar um rotulo. Num notebook mais fraco a diferenca e a de rodar
/// ou nao rodar.
///
/// ACESSIBILIDADE: tremor de tela e gatilho de enjoo em muita gente, e
/// congelamento de quadro incomoda quem tem sensibilidade a estimulo
/// visual. Os dois desligam. Celeste, Hades e Deep Rock Galactic todos
/// tem esse controle, e e o tipo de coisa que aparece em analise.
/// </summary>
public static class OpcoesJogo
{
    // ---------- valores ----------
    public static float Sensibilidade = 2.2f;      // graus por pixel
    public static float SensNaMira = 0.55f;        // multiplicador mirando
    public static bool InverterY = false;
    public static float VolumeGeral = 0.8f;
    public static int Qualidade = 2;               // 0 baixo, 1 medio, 2 alto
    public static int Vsync = 0;
    public static int LimiteFps = 0;               // 0 = sem limite
    public static float Fov = 68f;
    public static float TremorTela = 1f;           // 0 a 1
    public static bool CongelarQuadro = true;
    public static bool MarcadorDeAcerto = true;
    public static bool NumerosDeDano = true;
    public static bool TelaCheia = true;

    private const string K = "opt_";
    private static bool carregado;

    /// <summary>Roda sozinho antes da cena abrir - antes de qualquer Awake.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void CarregarEAplicar()
    {
        Carregar();
        Aplicar();
    }

    public static void Carregar()
    {
        Sensibilidade     = PlayerPrefs.GetFloat(K + "sens", 2.2f);
        SensNaMira        = PlayerPrefs.GetFloat(K + "sensmira", 0.55f);
        InverterY         = PlayerPrefs.GetInt(K + "invy", 0) == 1;
        VolumeGeral       = PlayerPrefs.GetFloat(K + "vol", 0.8f);
        Qualidade         = PlayerPrefs.GetInt(K + "qual", 2);
        Vsync             = PlayerPrefs.GetInt(K + "vsync", 0);
        LimiteFps         = PlayerPrefs.GetInt(K + "fps", 0);
        Fov               = PlayerPrefs.GetFloat(K + "fov", 68f);
        TremorTela        = PlayerPrefs.GetFloat(K + "tremor", 1f);
        CongelarQuadro    = PlayerPrefs.GetInt(K + "congelar", 1) == 1;
        MarcadorDeAcerto  = PlayerPrefs.GetInt(K + "marcador", 1) == 1;
        NumerosDeDano     = PlayerPrefs.GetInt(K + "numeros", 1) == 1;
        TelaCheia         = PlayerPrefs.GetInt(K + "telacheia", 1) == 1;
        carregado = true;
    }

    public static void Salvar()
    {
        PlayerPrefs.SetFloat(K + "sens", Sensibilidade);
        PlayerPrefs.SetFloat(K + "sensmira", SensNaMira);
        PlayerPrefs.SetInt(K + "invy", InverterY ? 1 : 0);
        PlayerPrefs.SetFloat(K + "vol", VolumeGeral);
        PlayerPrefs.SetInt(K + "qual", Qualidade);
        PlayerPrefs.SetInt(K + "vsync", Vsync);
        PlayerPrefs.SetInt(K + "fps", LimiteFps);
        PlayerPrefs.SetFloat(K + "fov", Fov);
        PlayerPrefs.SetFloat(K + "tremor", TremorTela);
        PlayerPrefs.SetInt(K + "congelar", CongelarQuadro ? 1 : 0);
        PlayerPrefs.SetInt(K + "marcador", MarcadorDeAcerto ? 1 : 0);
        PlayerPrefs.SetInt(K + "numeros", NumerosDeDano ? 1 : 0);
        PlayerPrefs.SetInt(K + "telacheia", TelaCheia ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>Empurra os valores pra quem de fato usa cada um.</summary>
    public static void Aplicar()
    {
        if (!carregado) Carregar();

        AudioListener.volume = Mathf.Clamp01(VolumeGeral);
        ImpactoDeCamera.Intensidade = Mathf.Clamp01(TremorTela);
        ImpactoDeCamera.CongelarLigado = CongelarQuadro;
        Crosshair.MarcadorLigado = MarcadorDeAcerto;
        DanoPopup.Ligado = NumerosDeDano;

        QualitySettings.vSyncCount = Vsync;
        Application.targetFrameRate = LimiteFps <= 0 ? -1 : LimiteFps;

        AplicarQualidade(Qualidade);
        AplicarNaCamera();
    }

    /// <summary>
    /// Em URP quem custa quadro e escala de renderizacao, sombra e cascata -
    /// nao o nome do preset. Entao o preset mexe nisso direto.
    /// </summary>
    public static void AplicarQualidade(int nivel)
    {
        Qualidade = Mathf.Clamp(nivel, 0, 2);
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (rp == null) return;

        if (Qualidade == 0)          // BAIXO - pra notebook fraco
        {
            rp.renderScale = 0.72f;
            rp.shadowDistance = 35f;
            rp.shadowCascadeCount = 1;
            rp.mainLightShadowmapResolution = 1024;
            rp.additionalLightsShadowmapResolution = 512;
            rp.msaaSampleCount = 1;
        }
        else if (Qualidade == 1)     // MEDIO
        {
            rp.renderScale = 0.9f;
            rp.shadowDistance = 65f;
            rp.shadowCascadeCount = 2;
            rp.mainLightShadowmapResolution = 2048;
            rp.additionalLightsShadowmapResolution = 2048;
            rp.msaaSampleCount = 2;
        }
        else                          // ALTO - como o projeto estava
        {
            rp.renderScale = 1f;
            rp.shadowDistance = 110f;
            rp.shadowCascadeCount = 4;
            rp.mainLightShadowmapResolution = 4096;
            rp.additionalLightsShadowmapResolution = 4096;
            rp.msaaSampleCount = 1;
        }
    }

    public static string NomeQualidade(int n)
    {
        if (n == 0) return "BAIXO";
        if (n == 1) return "MEDIO";
        return "ALTO";
    }

    /// <summary>A camera pode nao existir ainda quando isso roda; entao tem quem chame de novo.</summary>
    public static void AplicarNaCamera()
    {
        var cj = Object.FindAnyObjectByType<CameraJogo>();
        if (cj != null) cj.AplicarOpcoes(Sensibilidade, SensNaMira, InverterY, Fov);
    }

    public static void Restaurar()
    {
        Sensibilidade = 2.2f; SensNaMira = 0.55f; InverterY = false;
        VolumeGeral = 0.8f; Qualidade = 2; Vsync = 0; LimiteFps = 0; Fov = 68f;
        TremorTela = 1f; CongelarQuadro = true; MarcadorDeAcerto = true;
        NumerosDeDano = true; TelaCheia = true;
        Salvar(); Aplicar();
    }
}
