using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MENU INICIAL com loja de meta-progressao (modelo Vampire Survivors):
/// o dinheiro banco (acumulado entre partidas) compra cartas de upgrade
/// novas pra roleta e montagens de arma (attachments).
///
/// Abre ao carregar a cena com o tempo PAUSADO. "JOGAR" fecha e comeca a run.
/// Canvas todo montado em codigo, mesmo esquema da LevelUpUI.
/// </summary>
public class MenuInicial : MonoBehaviour
{
    [Header("Loja de montagens")]
    [Tooltip("Presets compraveis na ARMARIA (ordem casa com custosPresets).")]
    [SerializeField] private PresetArma[] presetsLoja;
    [SerializeField] private int[] custosPresets;

    private Canvas canvas;
    private GameObject canvasGo;
    private Font font;
    private UpgradeInventory inv;
    private Text txtBanco;
    private GameObject painelPrincipal, painelLoja, painelArmaria;

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void Start()
    {
        var invGo = FindAnyObjectByType<UpgradeInventory>();
        inv = invGo;
        Abrir();
    }

    private void Update()
    {
        // scripts de camera relockam o cursor no OnEnable; enquanto o menu
        // estiver aberto, a gente ganha essa briga todo frame
        if (canvasGo != null && Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // ---------------- fluxo ----------------

    private void Abrir()
    {
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        MontarCanvas();
        MostrarPainel(painelPrincipal);
    }

    private void Jogar()
    {
        // aplica a montagem equipada na arma antes de comecar
        var mont = FindAnyObjectByType<MontagemArma>();
        if (mont != null && presetsLoja != null)
        {
            string nome = MetaProgressao.PresetEquipado;
            for (int i = 0; i < presetsLoja.Length; i++)
                if (presetsLoja[i] != null && presetsLoja[i].name == nome &&
                    MetaProgressao.PresetLiberado(presetsLoja[i]))
                    mont.Aplicar(presetsLoja[i]);
        }

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (canvasGo != null) Destroy(canvasGo);
        Destroy(this); // menu so vive ate a run comecar; volta no reload da cena
    }

    private void MostrarPainel(GameObject qual)
    {
        painelPrincipal.SetActive(qual == painelPrincipal);
        painelLoja.SetActive(qual == painelLoja);
        painelArmaria.SetActive(qual == painelArmaria);
        AtualizarBanco();
    }

    private void AtualizarBanco()
    {
        if (txtBanco != null) txtBanco.text = "$ " + MetaProgressao.Dinheiro;
    }

    private int CustoDaCarta(UpgradeData u)
    {
        if (u == null) return 0;
        if (u.EhDeClasse) return 900;
        if (u.kind == UpgradeKind.ExplosiveRounds || u.kind == UpgradeKind.IncendiaryRounds ||
            u.kind == UpgradeKind.AcidRounds || u.kind == UpgradeKind.Pierce ||
            u.kind == UpgradeKind.Ricochet) return 600;
        return 350;
    }

    // ---------------- UI ----------------

    private void MontarCanvas()
    {
        canvasGo = new GameObject("MenuInicial_Canvas");
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var ev = new GameObject("EventSystem");
            ev.AddComponent<UnityEngine.EventSystems.EventSystem>();
            ev.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // fundo escurecido
        var fundo = NovoPainel(canvasGo.transform, new Color(0.02f, 0.03f, 0.05f, 0.93f));
        Estica(fundo.GetComponent<RectTransform>());

        // banco no topo direito (compartilhado entre paineis)
        var banco = NovoTexto(canvasGo.transform, "$ 0", 34, new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleRight);
        Ancora(banco.GetComponent<RectTransform>(),new Vector2(-240, -40), new Vector2(400, 50), new Vector2(1f, 1f));
        txtBanco = banco.GetComponent<Text>();

        painelPrincipal = MontarPrincipal();
        painelLoja = MontarLoja();
        painelArmaria = MontarArmaria();
    }

    private GameObject MontarPrincipal()
    {
        var p = NovoPainel(canvasGo.transform, Color.clear);
        Estica(p.GetComponent<RectTransform>());

        var titulo = NovoTexto(p.transform, "ZUMBIS NO MORRO", 72, new Color(1f, 0.45f, 0.30f), TextAnchor.MiddleCenter);
        Ancora(titulo.GetComponent<RectTransform>(), new Vector2(0, 260), new Vector2(1200, 100), new Vector2(0.5f, 0.5f));

        var sub = NovoTexto(p.transform, "noite de lua cheia na favela", 26, new Color(0.65f, 0.72f, 0.95f), TextAnchor.MiddleCenter);
        Ancora(sub.GetComponent<RectTransform>(), new Vector2(0, 195), new Vector2(900, 40), new Vector2(0.5f, 0.5f));

        NovoBotao(p.transform, "JOGAR", new Vector2(0, 60), new Vector2(420, 84),
            new Color(0.16f, 0.45f, 0.20f), Jogar, 40);
        NovoBotao(p.transform, "LOJA DE CARTAS", new Vector2(0, -50), new Vector2(420, 70),
            new Color(0.22f, 0.26f, 0.40f), () => MostrarPainel(painelLoja), 30);
        NovoBotao(p.transform, "ARMARIA", new Vector2(0, -140), new Vector2(420, 70),
            new Color(0.40f, 0.28f, 0.16f), () => MostrarPainel(painelArmaria), 30);

        var dica = NovoTexto(p.transform, "mate zumbis, morra, o dinheiro fica no banco - gaste aqui", 20,
            new Color(0.6f, 0.6f, 0.65f), TextAnchor.MiddleCenter);
        Ancora(dica.GetComponent<RectTransform>(), new Vector2(0, -240), new Vector2(1000, 34), new Vector2(0.5f, 0.5f));
        return p;
    }

    private GameObject MontarLoja()
    {
        var p = NovoPainel(canvasGo.transform, Color.clear);
        Estica(p.GetComponent<RectTransform>());

        var titulo = NovoTexto(p.transform, "LOJA DE CARTAS - libere upgrades pra roleta do level up", 34,
            Color.white, TextAnchor.MiddleCenter);
        Ancora(titulo.GetComponent<RectTransform>(), new Vector2(0, -60), new Vector2(1600, 50), new Vector2(0.5f, 1f));

        UpgradeData[] pool = inv != null ? inv.Pool : new UpgradeData[0];
        int col = 0, lin = 0, porLinha = 6;
        float w = 260f, h = 190f, gx = 24f, gy = 24f;
        float x0 = -((porLinha - 1) * (w + gx)) * 0.5f;
        for (int i = 0; i < pool.Length; i++)
        {
            var u = pool[i];
            if (u == null) continue;
            float x = x0 + col * (w + gx);
            float y = 120f - lin * (h + gy);
            MontarCartaLoja(p.transform, u, new Vector2(x, y), new Vector2(w, h));
            col++;
            if (col >= porLinha) { col = 0; lin++; }
        }

        NovoBotao(p.transform, "< VOLTAR", new Vector2(0, -440), new Vector2(300, 62),
            new Color(0.35f, 0.20f, 0.20f), () => MostrarPainel(painelPrincipal), 26);
        return p;
    }

    private void MontarCartaLoja(Transform pai, UpgradeData u, Vector2 pos, Vector2 tam)
    {
        bool liberada = MetaProgressao.CartaLiberada(u);
        var carta = NovoPainel(pai, liberada ? new Color(0.10f, 0.20f, 0.12f, 0.97f) : new Color(0.10f, 0.11f, 0.15f, 0.97f));
        Ancora(carta.GetComponent<RectTransform>(), pos, tam, new Vector2(0.5f, 0.5f));

        var nome = NovoTexto(carta.transform, u.displayName, 22,
            liberada ? new Color(0.65f, 1f, 0.7f) : Color.white, TextAnchor.MiddleCenter);
        Ancora(nome.GetComponent<RectTransform>(), new Vector2(0, -28), new Vector2(tam.x - 16, 52), new Vector2(0.5f, 1f));

        string desc = u.description;
        try { desc = string.Format(u.description, u.valuePerStack); } catch { }
        var dtxt = NovoTexto(carta.transform, desc, 17, new Color(0.75f, 0.75f, 0.8f), TextAnchor.UpperCenter);
        Ancora(dtxt.GetComponent<RectTransform>(), new Vector2(0, -66), new Vector2(tam.x - 20, 70), new Vector2(0.5f, 1f));

        if (liberada)
        {
            var ok = NovoTexto(carta.transform, MetaProgressao.CartaEhGratis(u) ? "DE FABRICA" : "LIBERADA", 20,
                new Color(0.5f, 0.95f, 0.55f), TextAnchor.MiddleCenter);
            Ancora(ok.GetComponent<RectTransform>(), new Vector2(0, 24), new Vector2(tam.x - 20, 40), new Vector2(0.5f, 0f));
        }
        else
        {
            int custo = CustoDaCarta(u);
            NovoBotao(carta.transform, "$ " + custo, new Vector2(0, 36), new Vector2(tam.x - 40, 52),
                MetaProgressao.Dinheiro >= custo ? new Color(0.75f, 0.58f, 0.12f) : new Color(0.30f, 0.28f, 0.22f),
                () =>
                {
                    if (MetaProgressao.ComprarCarta(u, custo))
                    {
                        MostrarPainel(painelLoja); // nada de reconstruir na mao: refaz o painel
                        RefazerLoja();
                    }
                }, 24, new Vector2(0.5f, 0f));
        }
    }

    private void RefazerLoja()
    {
        var velho = painelLoja;
        painelLoja = MontarLoja();
        Destroy(velho);
        MostrarPainel(painelLoja);
    }

    private GameObject MontarArmaria()
    {
        var p = NovoPainel(canvasGo.transform, Color.clear);
        Estica(p.GetComponent<RectTransform>());

        var titulo = NovoTexto(p.transform, "ARMARIA - montagens de attachment pra sua AK", 34,
            Color.white, TextAnchor.MiddleCenter);
        Ancora(titulo.GetComponent<RectTransform>(), new Vector2(0, -60), new Vector2(1600, 50), new Vector2(0.5f, 1f));

        int n = presetsLoja != null ? presetsLoja.Length : 0;
        float w = 380f, h = 260f, gx = 30f;
        float x0 = -((n - 1) * (w + gx)) * 0.5f;
        for (int i = 0; i < n; i++)
        {
            if (presetsLoja[i] == null) continue;
            MontarCartaPreset(p.transform, presetsLoja[i],
                custosPresets != null && i < custosPresets.Length ? custosPresets[i] : 800,
                new Vector2(x0 + i * (w + gx), 20f), new Vector2(w, h));
        }

        NovoBotao(p.transform, "< VOLTAR", new Vector2(0, -400), new Vector2(300, 62),
            new Color(0.35f, 0.20f, 0.20f), () => MostrarPainel(painelPrincipal), 26);
        return p;
    }

    private void MontarCartaPreset(Transform pai, PresetArma preset, int custo, Vector2 pos, Vector2 tam)
    {
        bool liberado = MetaProgressao.PresetLiberado(preset);
        bool equipado = liberado && MetaProgressao.PresetEquipado == preset.name;

        var carta = NovoPainel(pai, equipado ? new Color(0.22f, 0.18f, 0.08f, 0.97f)
            : liberado ? new Color(0.12f, 0.16f, 0.11f, 0.97f) : new Color(0.10f, 0.11f, 0.15f, 0.97f));
        Ancora(carta.GetComponent<RectTransform>(), pos, tam, new Vector2(0.5f, 0.5f));

        var nome = NovoTexto(carta.transform, preset.nomeExibicao, 26,
            equipado ? new Color(1f, 0.85f, 0.4f) : Color.white, TextAnchor.MiddleCenter);
        Ancora(nome.GetComponent<RectTransform>(), new Vector2(0, -30), new Vector2(tam.x - 16, 50), new Vector2(0.5f, 1f));

        var sbDesc = new System.Text.StringBuilder();
        if (preset.pecas != null)
            foreach (var pc in preset.pecas)
                if (pc != null && pc.prefab != null) sbDesc.Append("- ").Append(pc.prefab.name.Replace("_", " ")).Append("\n");
        if (preset.multEspalhamento < 1f) sbDesc.Append("+").Append(Mathf.RoundToInt((1f - preset.multEspalhamento) * 100f)).Append("% precisao\n");
        if (preset.multCadencia > 1f) sbDesc.Append("+").Append(Mathf.RoundToInt((preset.multCadencia - 1f) * 100f)).Append("% cadencia\n");
        if (preset.multDano > 1f) sbDesc.Append("+").Append(Mathf.RoundToInt((preset.multDano - 1f) * 100f)).Append("% dano\n");
        var dtxt = NovoTexto(carta.transform, sbDesc.ToString(), 19, new Color(0.75f, 0.78f, 0.82f), TextAnchor.UpperLeft);
        Ancora(dtxt.GetComponent<RectTransform>(), new Vector2(0, -78), new Vector2(tam.x - 40, 120), new Vector2(0.5f, 1f));

        if (!liberado)
        {
            NovoBotao(carta.transform, "$ " + custo, new Vector2(0, 24), new Vector2(tam.x - 60, 56),
                MetaProgressao.Dinheiro >= custo ? new Color(0.75f, 0.58f, 0.12f) : new Color(0.30f, 0.28f, 0.22f),
                () =>
                {
                    if (MetaProgressao.ComprarPreset(preset, custo)) RefazerArmaria();
                }, 24, new Vector2(0.5f, 0f));
        }
        else if (!equipado)
        {
            NovoBotao(carta.transform, "EQUIPAR", new Vector2(0, 24), new Vector2(tam.x - 60, 56),
                new Color(0.18f, 0.40f, 0.22f),
                () => { MetaProgressao.PresetEquipado = preset.name; RefazerArmaria(); }, 24, new Vector2(0.5f, 0f));
        }
        else
        {
            NovoBotao(carta.transform, "EQUIPADA  (tirar)", new Vector2(0, 24), new Vector2(tam.x - 60, 56),
                new Color(0.45f, 0.38f, 0.12f),
                () => { MetaProgressao.PresetEquipado = ""; RefazerArmaria(); }, 22, new Vector2(0.5f, 0f));
        }
    }

    private void RefazerArmaria()
    {
        var velho = painelArmaria;
        painelArmaria = MontarArmaria();
        Destroy(velho);
        MostrarPainel(painelArmaria);
    }

    // ---------------- fabrica de UI ----------------

    private GameObject NovoPainel(Transform pai, Color cor)
    {
        var go = new GameObject("Painel");
        go.transform.SetParent(pai, false);
        var img = go.AddComponent<Image>();
        img.color = cor;
        return go;
    }

    private GameObject NovoTexto(Transform pai, string txt, int tamanho, Color cor, TextAnchor anc)
    {
        var go = new GameObject("Texto");
        go.transform.SetParent(pai, false);
        var t = go.AddComponent<Text>();
        t.text = txt; t.font = font; t.fontSize = tamanho; t.color = cor;
        t.alignment = anc;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return go;
    }

    private void NovoBotao(Transform pai, string rotulo, Vector2 pos, Vector2 tam, Color cor,
                           UnityEngine.Events.UnityAction acao, int fonte)
    {
        NovoBotao(pai, rotulo, pos, tam, cor, acao, fonte, new Vector2(0.5f, 0.5f));
    }

    private void NovoBotao(Transform pai, string rotulo, Vector2 pos, Vector2 tam, Color cor,
                           UnityEngine.Events.UnityAction acao, int fonte, Vector2 anc)
    {
        var go = new GameObject("Botao_" + rotulo);
        go.transform.SetParent(pai, false);
        var img = go.AddComponent<Image>();
        img.color = cor;
        var b = go.AddComponent<Button>();
        b.onClick.AddListener(acao);
        var cores = b.colors;
        cores.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        b.colors = cores;
        Ancora(go.GetComponent<RectTransform>(), pos, tam, anc);

        var t = NovoTexto(go.transform, rotulo, fonte, Color.white, TextAnchor.MiddleCenter);
        Estica(t.GetComponent<RectTransform>());
    }

    private void Estica(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private void Ancora(RectTransform rt, Vector2 pos, Vector2 tam, Vector2 anc)
    {
        rt.anchorMin = anc; rt.anchorMax = anc;
        rt.sizeDelta = tam;
        rt.anchoredPosition = pos;
    }
}
