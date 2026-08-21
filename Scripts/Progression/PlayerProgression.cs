using System;
using UnityEngine;

/// <summary>
/// Dinheiro, XP e nivel do player.
///
/// Dinheiro: desbloqueia armas / compra na loja (a loja vem depois).
/// XP: enche a barra; encheu, upa e a LevelUpUI abre a escolha de 3.
/// Curva de XP configuravel no Inspector.
/// </summary>
public class PlayerProgression : MonoBehaviour
{
    [Header("Curva de XP")]
    [Tooltip("XP pra ir do nivel 1 pro 2.")]
    [SerializeField] private int xpBase = 80;
    [Tooltip("Cada nivel pede este % a mais que o anterior. 35 = +35%.")]
    [SerializeField] private float crescimentoPercent = 35f;
    [Tooltip("Teto de XP por nivel (0 = sem teto).")]
    [SerializeField] private int xpTeto = 0;

    [Header("Estado (leitura)")]
    [SerializeField] private int nivel = 1;
    [SerializeField] private int xpAtual = 0;
    [SerializeField] private int dinheiro = 0;

    public int Nivel => nivel;
    public int XpAtual => xpAtual;
    public int Dinheiro => dinheiro;
    public int XpParaProximo { get; private set; }
    public float XpPercent => XpParaProximo <= 0 ? 0f : Mathf.Clamp01((float)xpAtual / XpParaProximo);

    /// <summary>(novo nivel). A LevelUpUI escuta isso.</summary>
    public event Action<int> OnLevelUp;
    public event Action<int> OnDinheiroMudou;
    public event Action OnXpMudou;

    private UpgradeInventory inv;

    private void Awake()
    {
        inv = GetComponent<UpgradeInventory>();
        XpParaProximo = CalcularXp(nivel);
    }

    private int CalcularXp(int nv)
    {
        float xp = xpBase * Mathf.Pow(1f + crescimentoPercent / 100f, nv - 1);
        int v = Mathf.RoundToInt(xp);
        return xpTeto > 0 ? Mathf.Min(v, xpTeto) : v;
    }

    public void AddDinheiro(int quantia)
    {
        EstatisticasRun.RegistrarDinheiro(quantia);
        if (quantia <= 0) return;
        float mult = inv != null ? inv.MoneyMult() : 1f;
        dinheiro += Mathf.RoundToInt(quantia * mult);
        OnDinheiroMudou?.Invoke(dinheiro);
    }

    public bool Gastar(int quantia)
    {
        if (quantia > dinheiro) return false;
        dinheiro -= quantia;
        OnDinheiroMudou?.Invoke(dinheiro);
        return true;
    }

    public void AddXp(int quantia)
    {
        if (quantia <= 0) return;
        float mult = inv != null ? inv.XpMult() : 1f;
        xpAtual += Mathf.RoundToInt(quantia * mult);

        // pode upar mais de um nivel de uma vez
        while (xpAtual >= XpParaProximo)
        {
            xpAtual -= XpParaProximo;
            nivel++;
            XpParaProximo = CalcularXp(nivel);
            OnLevelUp?.Invoke(nivel);
        }
        OnXpMudou?.Invoke();
    }
}
