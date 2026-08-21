using UnityEngine;

/// <summary>Tipo de efeito que o upgrade aplica.</summary>
public enum UpgradeKind
{
    /// <summary>+X% de dano.</summary>
    DamagePercent,
    /// <summary>+X% de cadencia.</summary>
    FireRatePercent,
    /// <summary>+X% de balas no carregador.</summary>
    MagazinePercent,
    /// <summary>Recarga X% mais rapida.</summary>
    ReloadSpeedPercent,
    /// <summary>A bala atravessa +X inimigos.</summary>
    Pierce,
    /// <summary>A bala rebate +X vezes na parede.</summary>
    Ricochet,
    /// <summary>+X% de projeteis por tiro (escopetas amam isso).</summary>
    PelletsPercent,
    /// <summary>Bala explode: valor = raio em metros. secondaryValue = % do dano na area.</summary>
    ExplosiveRounds,
    /// <summary>Bala incendiaria: valor = dano por segundo. secondaryValue = duracao.</summary>
    IncendiaryRounds,
    /// <summary>Municao acida: valor = dano por segundo. secondaryValue = duracao. Acido tambem derrete: ignora o multiplicador de parte do corpo.</summary>
    AcidRounds,
    /// <summary>+X% de dinheiro dropado.</summary>
    MoneyGainPercent,
    /// <summary>+X% de XP ganho.</summary>
    XpGainPercent,
    /// <summary>+X% de vida maxima do player.</summary>
    MaxHealthPercent,
    /// <summary>+X% no BONUS de acerto na cabeca. 100 = dobra o bonus, nao o dano.</summary>
    HeadshotPercent,

    // ---- leva 2 (25 cartas novas). SEMPRE acrescentar no FIM: os assets ----
    // ---- guardam o enum como int, inserir no meio corrompe todos.       ----

    /// <summary>Granada recarrega X% mais rapido.</summary>
    GranadaRecargaPercent,
    /// <summary>Pavio da granada X% mais curto.</summary>
    GranadaPavioPercent,
    /// <summary>+X% de raio na explosao da granada.</summary>
    GranadaRaioPercent,
    /// <summary>+X% de dano na granada.</summary>
    GranadaDanoPercent,
    /// <summary>Fogo residual da incendiaria: +X% duracao e +X% dps.</summary>
    GranadaFogoPercent,
    /// <summary>Cada abate tira X segundos da recarga da granada.</summary>
    RecargaGranadaAoMatar,
    /// <summary>Voce pega fogo: X dps em inimigos a secondaryValue metros.</summary>
    AuraDeFogo,
    /// <summary>Devolve X de dano em quem te acerta (ate secondaryValue m).</summary>
    Espinhos,
    /// <summary>X% do dano causado volta como vida.</summary>
    VampirismoPercent,
    /// <summary>Regenera X de vida por segundo.</summary>
    RegeneracaoPorSegundo,
    /// <summary>Reduz X% do dano recebido.</summary>
    ReducaoDanoPercent,
    /// <summary>Fica X segundos invulneravel depois de levar dano.</summary>
    FantasmaSegundos,
    /// <summary>Revive uma vez com X% da vida. Consome ao usar.</summary>
    SegundaChance,
    /// <summary>Inimigo morto explode: X de dano em secondaryValue metros.</summary>
    ExplosaoAoMatar,
    /// <summary>Tiro salta um arco eletrico pro inimigo mais proximo: X% do dano.</summary>
    CorrenteEletrica,
    /// <summary>Inimigos abaixo de X% de vida morrem na hora.</summary>
    Executar,
    /// <summary>Ate +X% de dano quanto menos vida voce tiver.</summary>
    SangueFrio,
    /// <summary>Abate da +X% de cadencia por secondaryValue segundos.</summary>
    AdrenalinaPercent,
    /// <summary>+X% de alcance da arma.</summary>
    AlcancePercent,
    /// <summary>X% de chance de disparar um projetil extra.</summary>
    ChanceProjetilExtra,
    /// <summary>+X% de velocidade de movimento.</summary>
    VelocidadePercent,
    /// <summary>+X% no raio de coleta dos orbes.</summary>
    ImaPercent,
    /// <summary>Cura X de vida ao completar uma wave.</summary>
    CuraPorWave,
    /// <summary>Escolher uma carta cura X% da vida maxima.</summary>
    CuraAoEscolherCarta,
    /// <summary>+X% de dano enquanto mira (ADS).</summary>
    DanoMirandoPercent
}

/// <summary>
/// FICHA de upgrade - mesmo esquema das armas: um asset por upgrade,
/// todos os numeros no Inspector. Criar upgrade novo = preencher ficha.
///
/// Deixe "somenteParaArmas" vazio pra ser um upgrade GENERICO.
/// Preencha com uma arma pra virar habilidade DE CLASSE daquela arma
/// (so aparece na roleta se a arma estiver no loadout, e so afeta ela).
/// </summary>
[CreateAssetMenu(menuName = "Zombie/Upgrade", fileName = "NovoUpgrade")]
public class UpgradeData : ScriptableObject
{
    [Header("Identidade")]
    public string displayName = "Upgrade";
    [TextArea]
    [Tooltip("Descricao mostrada na carta. Use {0} pro valor por stack.")]
    public string description = "+{0}% de dano";
    [TextArea]
    [Tooltip("Frase de sabor mostrada embaixo, em italico. Opcional.")]
    public string flavor = "";
    [Tooltip("Cor da carta na tela de level up.")]
    public Color cardColor = new Color(0.25f, 0.28f, 0.34f);

    [Header("Efeito")]
    public UpgradeKind kind = UpgradeKind.DamagePercent;
    [Tooltip("Valor POR STACK. Percentuais em pontos (25 = +25%). Pierce/Ricochet em unidades.")]
    public float valuePerStack = 25f;
    [Tooltip("Valor secundario: duracao do fogo/acido, % do dano da explosao...")]
    public float secondaryValue = 0f;

    [Header("Roleta")]
    [Tooltip("Quantas vezes da pra pegar. Depois disso nao aparece mais.")]
    public int maxStacks = 3;
    [Tooltip("Peso no sorteio. Maior = mais comum.")]
    public float weight = 10f;

    [Header("Habilidade de classe (vazio = generico)")]
    [Tooltip("Se preenchido, este upgrade e EXCLUSIVO destas armas.")]
    public WeaponData[] somenteParaArmas;

    /// <summary>Este upgrade vale pra esta arma?</summary>
    public bool AplicaEm(WeaponData w)
    {
        if (somenteParaArmas == null || somenteParaArmas.Length == 0) return true;
        for (int i = 0; i < somenteParaArmas.Length; i++)
            if (somenteParaArmas[i] == w) return true;
        return false;
    }

    public bool EhDeClasse => somenteParaArmas != null && somenteParaArmas.Length > 0;

    public string DescricaoFormatada()
    {
        return string.Format(description, valuePerStack, secondaryValue);
    }
}
