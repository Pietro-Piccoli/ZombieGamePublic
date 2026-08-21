using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Os upgrades que o player JA pegou (e quantos stacks de cada).
/// O WeaponController pergunta aqui na hora do tiro.
///
/// Upgrade generico soma pra toda arma; upgrade de classe so soma
/// quando a arma equipada e a dona dele.
/// </summary>
public class UpgradeInventory : MonoBehaviour
{
    [Header("Pool de upgrades sorteaveis")]
    [Tooltip("Arraste TODAS as fichas de upgrade aqui. A roleta sorteia destas.")]
    [SerializeField] private UpgradeData[] pool;

    private readonly Dictionary<UpgradeData, int> stacks = new Dictionary<UpgradeData, int>();
    private WeaponController weapons;
    private Health health;

    public IReadOnlyDictionary<UpgradeData, int> Stacks => stacks;
    public UpgradeData[] Pool => pool;

    private void Awake()
    {
        weapons = GetComponent<WeaponController>();
        health = GetComponent<Health>();
    }

    public int StacksDe(UpgradeData u) => u != null && stacks.ContainsKey(u) ? stacks[u] : 0;

    public void Aplicar(UpgradeData u)
    {
        if (u == null) return;
        stacks[u] = StacksDe(u) + 1;
        EstatisticasRun.RegistrarCarta();

        // KIT DE CAMPO: escolher carta cura
        var efeitos = GetComponent<EfeitosJogador>();
        if (efeitos != null) efeitos.AoEscolherCarta();

        // vida maxima aplica na hora (nao depende de tiro)
        if (u.kind == UpgradeKind.MaxHealthPercent && health != null)
        {
            float mult = 1f + Soma(UpgradeKind.MaxHealthPercent, null) / 100f;
            int novoMax = Mathf.RoundToInt(100 * mult);
            health.SetMaxHealth(novoMax, false);
            health.Heal(Mathf.RoundToInt(novoMax * u.valuePerStack / 100f));
        }
    }

    /// <summary>ADMIN: tira uma pilha da carta. Espelho do Aplicar.</summary>
    public void RemoverPilha(UpgradeData u)
    {
        if (u == null || StacksDe(u) <= 0) return;
        int novo = stacks[u] - 1;
        if (novo <= 0) stacks.Remove(u); else stacks[u] = novo;

        // vida maxima recalcula na hora (mesma conta do Aplicar)
        if (u.kind == UpgradeKind.MaxHealthPercent && health != null)
        {
            float mult = 1f + Soma(UpgradeKind.MaxHealthPercent, null) / 100f;
            health.SetMaxHealth(Mathf.RoundToInt(100 * mult), false);
        }
    }

    /// <summary>Sorteia ate N upgrades validos (respeita maxStacks e arma no loadout), sem repetir.</summary>
    public List<UpgradeData> Sortear(int quantos)
    {
        var candidatos = new List<UpgradeData>();
        var pesos = new List<float>();
        if (pool != null)
        {
            foreach (UpgradeData u in pool)
            {
                if (!MetaProgressao.CartaLiberada(u)) continue; // carta ainda presa na loja
                if (u == null) continue;
                if (StacksDe(u) >= u.maxStacks) continue;
                if (u.EhDeClasse && !ArmaNoLoadout(u)) continue;
                candidatos.Add(u);
                pesos.Add(Mathf.Max(0.01f, u.weight));
            }
        }

        var resultado = new List<UpgradeData>();
        for (int k = 0; k < quantos && candidatos.Count > 0; k++)
        {
            float total = 0f;
            for (int i = 0; i < pesos.Count; i++) total += pesos[i];
            float r = Random.value * total;
            int escolhido = candidatos.Count - 1;
            for (int i = 0; i < candidatos.Count; i++)
            {
                r -= pesos[i];
                if (r <= 0f) { escolhido = i; break; }
            }
            resultado.Add(candidatos[escolhido]);
            candidatos.RemoveAt(escolhido);
            pesos.RemoveAt(escolhido);
        }
        return resultado;
    }

    private bool ArmaNoLoadout(UpgradeData u)
    {
        if (weapons == null) return true;
        foreach (WeaponData w in u.somenteParaArmas)
            if (w != null && weapons.TemNoLoadout(w)) return true;
        return false;
    }

    // ---------- consultas que o tiro faz ----------

    private float Soma(UpgradeKind kind, WeaponData arma)
    {
        float soma = 0f;
        foreach (KeyValuePair<UpgradeData, int> kv in stacks)
        {
            if (kv.Key.kind != kind) continue;
            if (arma != null && !kv.Key.AplicaEm(arma)) continue;
            soma += kv.Key.valuePerStack * kv.Value;
        }
        return soma;
    }

    private float Secundario(UpgradeKind kind, WeaponData arma)
    {
        float melhor = 0f;
        foreach (KeyValuePair<UpgradeData, int> kv in stacks)
        {
            if (kv.Key.kind != kind) continue;
            if (arma != null && !kv.Key.AplicaEm(arma)) continue;
            if (kv.Key.secondaryValue > melhor) melhor = kv.Key.secondaryValue;
        }
        return melhor;
    }

    public float DamageMult(WeaponData w)   => 1f + Soma(UpgradeKind.DamagePercent, w) / 100f;
    public float FireRateMult(WeaponData w) => 1f + Soma(UpgradeKind.FireRatePercent, w) / 100f;
    public float MagazineMult(WeaponData w) => 1f + Soma(UpgradeKind.MagazinePercent, w) / 100f;
    public float ReloadMult(WeaponData w)   => 1f / (1f + Soma(UpgradeKind.ReloadSpeedPercent, w) / 100f);
    public int   Pierce(WeaponData w)       => Mathf.RoundToInt(Soma(UpgradeKind.Pierce, w));
    public int   Ricochet(WeaponData w)     => Mathf.RoundToInt(Soma(UpgradeKind.Ricochet, w));
    public float PelletsMult(WeaponData w)  => 1f + Soma(UpgradeKind.PelletsPercent, w) / 100f;
    public float HeadshotMult(WeaponData w) => 1f + Soma(UpgradeKind.HeadshotPercent, w) / 100f;

    public float ExplosionRadius(WeaponData w)  => Soma(UpgradeKind.ExplosiveRounds, w);
    public float ExplosionDmgPercent(WeaponData w) => Mathf.Max(1f, Secundario(UpgradeKind.ExplosiveRounds, w));

    public float FireDps(WeaponData w)      => Soma(UpgradeKind.IncendiaryRounds, w);
    public float FireDuration(WeaponData w) => Secundario(UpgradeKind.IncendiaryRounds, w);
    public float AcidDps(WeaponData w)      => Soma(UpgradeKind.AcidRounds, w);
    public float AcidDuration(WeaponData w) => Secundario(UpgradeKind.AcidRounds, w);

    // ---------- consultas genericas (leva 2 de cartas) ----------

    /// <summary>Soma dos valores de todas as pilhas deste efeito. 0 = nao tem.</summary>
    public float Valor(UpgradeKind kind) => Soma(kind, null);

    /// <summary>Maior valor secundario entre as pilhas deste efeito.</summary>
    public float Sec(UpgradeKind kind) => Secundario(kind, null);

    /// <summary>Quantas pilhas deste efeito o jogador tem (qualquer carta).</summary>
    public int PilhasDe(UpgradeKind kind)
    {
        int n = 0;
        foreach (KeyValuePair<UpgradeData, int> kv in stacks)
            if (kv.Key.kind == kind) n += kv.Value;
        return n;
    }

    public float MoneyMult() => 1f + Soma(UpgradeKind.MoneyGainPercent, null) / 100f;
    public float XpMult()    => 1f + Soma(UpgradeKind.XpGainPercent, null) / 100f;
}
