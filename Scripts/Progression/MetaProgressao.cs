using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// META-PROGRESSAO - o que sobrevive entre partidas.
///
/// Modelo do Vampire Survivors: o dinheiro coletado na run vai pro BANCO
/// quando voce morre. No menu inicial esse banco compra:
///   - cartas de upgrade novas (entram na roleta do level up)
///   - montagens de arma (presets de attachments, equipa antes de jogar)
///
/// Persistencia via PlayerPrefs (JSON pra listas). Nada disso reseta ao
/// fechar o jogo.
/// </summary>
public static class MetaProgressao
{
    private const string ChaveDinheiro = "meta_dinheiro";
    private const string ChaveCartas = "meta_cartas";
    private const string ChavePresets = "meta_presets";
    private const string ChavePresetEquipado = "meta_preset_equipado";

    /// <summary>Cartas que ja comecam liberadas (o "kit basico").</summary>
    private static readonly string[] CartasGratis = new string[]
    {
        "UP_BalaPesada", "UP_GatilhoNervoso", "UP_PenteEstendido",
        "UP_MaosRapidas", "UP_CascaGrossa", "UP_CerebroGrande", "UP_Ganancia"
    };

    [System.Serializable]
    private class ListaNomes { public List<string> nomes = new List<string>(); }

    private static ListaNomes cartas;
    private static ListaNomes presets;

    private static ListaNomes Carregar(string chave)
    {
        string js = PlayerPrefs.GetString(chave, "");
        if (string.IsNullOrEmpty(js)) return new ListaNomes();
        var l = JsonUtility.FromJson<ListaNomes>(js);
        return l != null ? l : new ListaNomes();
    }

    private static void Salvar(string chave, ListaNomes lista)
    {
        PlayerPrefs.SetString(chave, JsonUtility.ToJson(lista));
        PlayerPrefs.Save();
    }

    private static ListaNomes Cartas
    {
        get { if (cartas == null) cartas = Carregar(ChaveCartas); return cartas; }
    }

    private static ListaNomes Presets
    {
        get { if (presets == null) presets = Carregar(ChavePresets); return presets; }
    }

    // ---------- banco ----------

    public static int Dinheiro => PlayerPrefs.GetInt(ChaveDinheiro, 0);

    public static void Depositar(int quantia)
    {
        if (quantia <= 0) return;
        PlayerPrefs.SetInt(ChaveDinheiro, Dinheiro + quantia);
        PlayerPrefs.Save();
    }

    public static bool Gastar(int quantia)
    {
        if (quantia > Dinheiro) return false;
        PlayerPrefs.SetInt(ChaveDinheiro, Dinheiro - quantia);
        PlayerPrefs.Save();
        return true;
    }

    // ---------- cartas ----------

    public static bool CartaLiberada(UpgradeData u)
    {
        if (u == null) return false;
        string nome = u.name;
        for (int i = 0; i < CartasGratis.Length; i++)
            if (CartasGratis[i] == nome) return true;
        return Cartas.nomes.Contains(nome);
    }

    public static bool CartaEhGratis(UpgradeData u)
    {
        if (u == null) return false;
        for (int i = 0; i < CartasGratis.Length; i++)
            if (CartasGratis[i] == u.name) return true;
        return false;
    }

    public static bool ComprarCarta(UpgradeData u, int custo)
    {
        if (u == null || CartaLiberada(u)) return false;
        if (!Gastar(custo)) return false;
        Cartas.nomes.Add(u.name);
        Salvar(ChaveCartas, Cartas);
        return true;
    }

    // ---------- montagens (presets de attachment) ----------

    public static bool PresetLiberado(PresetArma p)
    {
        return p != null && Presets.nomes.Contains(p.name);
    }

    public static bool ComprarPreset(PresetArma p, int custo)
    {
        if (p == null || PresetLiberado(p)) return false;
        if (!Gastar(custo)) return false;
        Presets.nomes.Add(p.name);
        Salvar(ChavePresets, Presets);
        return true;
    }

    public static string PresetEquipado
    {
        get { return PlayerPrefs.GetString(ChavePresetEquipado, ""); }
        set { PlayerPrefs.SetString(ChavePresetEquipado, value ?? ""); PlayerPrefs.Save(); }
    }

    // ---------- anexos individuais (o modelo novo) ----------

    private const string ChaveAnexos = "meta_anexos";
    private const string ChaveEquipado = "meta_anexo_slot_";   // + indice do slot

    private static ListaNomes anexos;
    private static ListaNomes Anexos
    {
        get { if (anexos == null) anexos = Carregar(ChaveAnexos); return anexos; }
    }

    /// <summary>Ja comprou este anexo?</summary>
    public static bool AnexoComprado(AnexoArma a)
    {
        return a != null && Anexos.nomes.Contains(a.id);
    }

    public static bool ComprarAnexo(AnexoArma a)
    {
        if (a == null || AnexoComprado(a)) return false;
        if (!Gastar(a.preco)) return false;
        Anexos.nomes.Add(a.id);
        Salvar(ChaveAnexos, Anexos);
        return true;
    }

    /// <summary>Id do anexo equipado neste slot. Vazio = slot livre.</summary>
    public static string AnexoEquipado(SlotAttach slot)
    {
        return PlayerPrefs.GetString(ChaveEquipado + ((int)slot), "");
    }

    /// <summary>Equipa (ou desequipa, passando null) o anexo no slot dele.</summary>
    public static void EquiparAnexo(AnexoArma a, SlotAttach slot)
    {
        PlayerPrefs.SetString(ChaveEquipado + ((int)slot), a != null ? a.id : "");
        PlayerPrefs.Save();
    }

    public static void DesequiparSlot(SlotAttach slot)
    {
        EquiparAnexo(null, slot);
    }

    /// <summary>Todos os anexos do projeto (carregados de Resources/Anexos).</summary>
    public static AnexoArma[] TodosOsAnexos()
    {
        if (cacheAnexos == null) cacheAnexos = Resources.LoadAll<AnexoArma>("Anexos");
        return cacheAnexos;
    }
    private static AnexoArma[] cacheAnexos;

    /// <summary>O anexo equipado neste slot, ja resolvido pro asset.</summary>
    public static AnexoArma ResolverEquipado(SlotAttach slot)
    {
        string id = AnexoEquipado(slot);
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var a in TodosOsAnexos())
            if (a != null && a.id == id) return a;
        return null;
    }

    /// <summary>ADMIN: libera todas as cartas do jogo de uma vez.</summary>
    public static void LiberarTodasCartas()
    {
        foreach (var u in Resources.LoadAll<UpgradeData>("Upgrades"))
        {
            if (u == null || CartaEhGratis(u) || Cartas.nomes.Contains(u.name)) continue;
            Cartas.nomes.Add(u.name);
        }
        Salvar(ChaveCartas, Cartas);
    }

    /// <summary>ADMIN: esquece todas as cartas compradas (o banco fica).</summary>
    public static void ResetarCartas()
    {
        Cartas.nomes.Clear();
        Salvar(ChaveCartas, Cartas);
    }

    /// <summary>Zera tudo. Util pra testar.</summary>
    public static void ApagarTudo()
    {
        PlayerPrefs.DeleteKey(ChaveDinheiro);
        PlayerPrefs.DeleteKey(ChaveCartas);
        PlayerPrefs.DeleteKey(ChavePresets);
        PlayerPrefs.DeleteKey(ChavePresetEquipado);
        PlayerPrefs.DeleteKey(ChaveAnexos);
        for (int i = 0; i < 6; i++) PlayerPrefs.DeleteKey(ChaveEquipado + i);
        PlayerPrefs.Save();
        cartas = null; presets = null; anexos = null;
    }
}
