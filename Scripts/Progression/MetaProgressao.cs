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

    // ---------- ARMAS ----------
    //
    // Modelo de referencia: Risk of Rain 2 e Vampire Survivors escolhem o
    // personagem/arma ANTES da run, no menu, e nao trocam no meio. Aqui e igual:
    // a arma comprada fica no banco, o jogador seleciona uma, e a partida
    // comeca com ela. Cada arma guarda a PROPRIA montagem de anexos.

    private const string ChaveArmas = "meta_armas";
    private const string ChaveArmaSel = "meta_arma_sel";

    private static ListaNomes armas;
    private static ListaNomes Armas
    {
        get { if (armas == null) armas = Carregar(ChaveArmas); return armas; }
    }

    private static WeaponData[] cacheArmas;

    /// <summary>Todas as fichas de arma do projeto (Resources/Armas), em ordem.</summary>
    public static WeaponData[] TodasAsArmas()
    {
        if (cacheArmas == null)
        {
            cacheArmas = Resources.LoadAll<WeaponData>("Armas");
            if (cacheArmas != null)
                System.Array.Sort(cacheArmas, delegate (WeaponData a, WeaponData b)
                {
                    if (a == null || b == null) return 0;
                    int c = a.ordem.CompareTo(b.ordem);
                    return c != 0 ? c : a.preco.CompareTo(b.preco);
                });
        }
        return cacheArmas != null ? cacheArmas : new WeaponData[0];
    }

    public static WeaponData ArmaPorId(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var todas = TodasAsArmas();
        for (int i = 0; i < todas.Length; i++)
            if (todas[i] != null && todas[i].Id == id) return todas[i];
        return null;
    }

    /// <summary>Arma de preco 0 = ja vem com o jogador, nao precisa comprar.</summary>
    public static bool ArmaComprada(WeaponData w)
    {
        if (w == null) return false;
        if (w.preco <= 0) return true;
        return Armas.nomes.Contains(w.Id);
    }

    public static bool ComprarArma(WeaponData w)
    {
        if (w == null || ArmaComprada(w)) return false;
        if (!Gastar(w.preco)) return false;
        Armas.nomes.Add(w.Id);
        Salvar(ChaveArmas, Armas);
        return true;
    }

    /// <summary>Id da arma que vai pra partida. Cai na primeira gratis se nada valido.</summary>
    public static string ArmaSelecionadaId
    {
        get
        {
            string id = PlayerPrefs.GetString(ChaveArmaSel, "");
            var w = ArmaPorId(id);
            if (w != null && ArmaComprada(w)) return w.Id;
            var todas = TodasAsArmas();
            for (int i = 0; i < todas.Length; i++)
                if (todas[i] != null && ArmaComprada(todas[i])) return todas[i].Id;
            return "";
        }
        set { PlayerPrefs.SetString(ChaveArmaSel, value != null ? value : ""); PlayerPrefs.Save(); }
    }

    /// <summary>A ficha da arma selecionada, ja resolvida.</summary>
    public static WeaponData ArmaSelecionada
    {
        get { return ArmaPorId(ArmaSelecionadaId); }
    }

    // ---------- anexos individuais (o modelo novo) ----------

    private const string ChaveAnexos = "meta_anexos";
    // chave NOVA: por arma. meta_ax_<idArma>_<slot>
    private const string ChaveEquipadoArma = "meta_ax_";
    // chave VELHA (uma montagem so, da epoca em que so existia a AK)
    private const string ChaveEquipado = "meta_anexo_slot_";
    private const string ChaveMigrou = "meta_ax_migrou";

    private static ListaNomes anexos;
    private static ListaNomes Anexos
    {
        get { if (anexos == null) anexos = Carregar(ChaveAnexos); return anexos; }
    }

    /// <summary>
    /// Leva a montagem antiga (chave unica) pra montagem da PRIMEIRA arma.
    /// Roda uma vez so: quem ja jogou nao perde os anexos que tinha na AK.
    /// </summary>
    private static void MigrarMontagemAntiga()
    {
        if (PlayerPrefs.GetInt(ChaveMigrou, 0) == 1) return;
        PlayerPrefs.SetInt(ChaveMigrou, 1);

        var todas = TodasAsArmas();
        string destino = "";
        for (int i = 0; i < todas.Length; i++)
            if (todas[i] != null && todas[i].preco <= 0) { destino = todas[i].Id; break; }
        if (string.IsNullOrEmpty(destino)) { PlayerPrefs.Save(); return; }

        for (int s = 0; s < 6; s++)
        {
            string velho = PlayerPrefs.GetString(ChaveEquipado + s, "");
            if (string.IsNullOrEmpty(velho)) continue;
            PlayerPrefs.SetString(ChaveEquipadoArma + destino + "_" + s, velho);
        }
        PlayerPrefs.Save();
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

    /// <summary>Id do anexo equipado neste slot DESTA arma. Vazio = slot livre.</summary>
    public static string AnexoEquipado(string idArma, SlotAttach slot)
    {
        MigrarMontagemAntiga();
        if (string.IsNullOrEmpty(idArma)) return "";
        return PlayerPrefs.GetString(ChaveEquipadoArma + idArma + "_" + ((int)slot), "");
    }

    /// <summary>Atalho: usa a arma selecionada.</summary>
    public static string AnexoEquipado(SlotAttach slot)
    {
        return AnexoEquipado(ArmaSelecionadaId, slot);
    }

    public static void EquiparAnexo(string idArma, AnexoArma a, SlotAttach slot)
    {
        if (string.IsNullOrEmpty(idArma)) return;
        PlayerPrefs.SetString(ChaveEquipadoArma + idArma + "_" + ((int)slot), a != null ? a.id : "");
        PlayerPrefs.Save();
    }

    public static void EquiparAnexo(AnexoArma a, SlotAttach slot)
    {
        EquiparAnexo(ArmaSelecionadaId, a, slot);
    }

    public static void DesequiparSlot(string idArma, SlotAttach slot) { EquiparAnexo(idArma, null, slot); }
    public static void DesequiparSlot(SlotAttach slot) { EquiparAnexo(null, slot); }

    /// <summary>Todos os anexos do projeto (carregados de Resources/Anexos).</summary>
    public static AnexoArma[] TodosOsAnexos()
    {
        if (cacheAnexos == null) cacheAnexos = Resources.LoadAll<AnexoArma>("Anexos");
        return cacheAnexos;
    }
    private static AnexoArma[] cacheAnexos;

    /// <summary>O anexo equipado neste slot DESTA arma, ja resolvido pro asset.</summary>
    public static AnexoArma ResolverEquipado(string idArma, SlotAttach slot)
    {
        string id = AnexoEquipado(idArma, slot);
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var a in TodosOsAnexos())
            if (a != null && a.id == id) return a;
        return null;
    }

    public static AnexoArma ResolverEquipado(SlotAttach slot)
    {
        return ResolverEquipado(ArmaSelecionadaId, slot);
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
        PlayerPrefs.DeleteKey(ChaveArmas);
        PlayerPrefs.DeleteKey(ChaveArmaSel);
        PlayerPrefs.DeleteKey(ChaveMigrou);
        for (int i = 0; i < 6; i++) PlayerPrefs.DeleteKey(ChaveEquipado + i);
        var todasArmas = TodasAsArmas();
        for (int a = 0; a < todasArmas.Length; a++)
        {
            if (todasArmas[a] == null) continue;
            for (int i = 0; i < 6; i++)
                PlayerPrefs.DeleteKey(ChaveEquipadoArma + todasArmas[a].Id + "_" + i);
        }
        PlayerPrefs.Save();
        cartas = null; presets = null; anexos = null; armas = null;
    }
}
