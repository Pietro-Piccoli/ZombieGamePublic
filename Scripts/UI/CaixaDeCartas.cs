using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CAIXA DE CARTAS - abertura estilo caixa de CS.
///
/// Voce paga UMA vez e a carta e SORTEADA: uma fita de cartas corre atras de
/// uma agulha central, desacelera em ease-out cubico e para na vencedora, que
/// e revelada em destaque com a cor da raridade.
///
/// Regras:
///  - O sorteio so tira de cartas que voce AINDA NAO TEM (nada de duplicata
///    frustrante); o peso do sorteio e o mesmo da roleta de level up, entao
///    comum sai mais que rara.
///  - A carta e CREDITADA no clique, antes da animacao - se o jogador fechar
///    o menu no meio, ja e dele.
///  - Roda com Time.unscaledDeltaTime: o menu vive com timeScale = 0.
/// </summary>
public class CaixaDeCartas : MonoBehaviour
{
    public const int Preco = 600;

    private const float TileW = 260f, TileH = 190f, Espaco = 14f;
    private const float Pitch = TileW + Espaco;
    private const int TilesNaFita = 50;
    private const int IndiceVencedor = 44;
    private const float Duracao = 6.2f;

    private System.Action aoMudarBanco;
    private RectTransform fita;
    private RectTransform viewport;
    private Button botaoAbrir;
    private TextMeshProUGUI txtBotao, txtRestantes;
    private GameObject overlayRevelacao;
    private readonly List<RectTransform> tiles = new List<RectTransform>();

    // animacao
    private bool rodando;
    private float t;
    private float xInicial, xFinal;
    private UpgradeData vencedora;
    private int tileDestacado = -1;
    private float redesenhoIdle;

    // ---------------- montagem ----------------

    public static CaixaDeCartas Montar(Transform pai, System.Action aoMudarBanco)
    {
        var go = new GameObject("CaixaDeCartas", typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var c = go.AddComponent<CaixaDeCartas>();
        c.aoMudarBanco = aoMudarBanco;
        c.Construir();
        return c;
    }

    private void Construir()
    {
        var painel = UIKit.PainelBordado(transform, "Fundo", UIKit.Painel, UIKit.RaioPainel);
        UIKit.Esticar(painel);
        var d = painel.transform.GetChild(0);

        var tit = UIKit.Texto3(d, "Tit", "CAIXA DE CARTAS", 26f, TextAlignmentOptions.Center, UIKit.Texto, true);
        UIKit.Por(tit, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(700f, 32f));
        tit.characterSpacing = 10f;

        var sub = UIKit.Texto3(d, "Sub", "Pague, gire e leve o que a sorte mandar. Sem duplicata: toda carta é nova.",
                               14f, TextAlignmentOptions.Center, UIKit.TextoFraco, false);
        UIKit.Por(sub, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(900f, 20f));

        // ---- viewport da fita ----
        var vpGo = new GameObject("Viewport", typeof(RectTransform));
        vpGo.transform.SetParent(d, false);
        viewport = (RectTransform)vpGo.transform;
        viewport.anchorMin = new Vector2(0f, 0.5f); viewport.anchorMax = new Vector2(1f, 0.5f);
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.offsetMin = new Vector2(24f, 20f); viewport.offsetMax = new Vector2(-24f, 20f);
        viewport.sizeDelta = new Vector2(viewport.sizeDelta.x, TileH + 44f);
        vpGo.AddComponent<RectMask2D>();
        var vpFundo = vpGo.AddComponent<Image>();
        vpFundo.color = new Color(0f, 0f, 0f, 0.35f);
        vpFundo.raycastTarget = false;

        var fitaGo = new GameObject("Fita", typeof(RectTransform));
        fitaGo.transform.SetParent(viewport, false);
        fita = (RectTransform)fitaGo.transform;
        fita.anchorMin = new Vector2(0.5f, 0.5f); fita.anchorMax = new Vector2(0.5f, 0.5f);
        fita.pivot = new Vector2(0f, 0.5f);
        fita.sizeDelta = new Vector2(TilesNaFita * Pitch, TileH);

        // ---- agulha central ----
        var agulha = UIKit.Caixa(d, "Agulha", UIKit.Destaque, 2);
        UIKit.Por(agulha, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(4f, TileH + 56f));
        agulha.raycastTarget = false;
        var pontaCima = UIKit.Texto3(d, "PC", "▼", 22f, TextAlignmentOptions.Center, UIKit.Destaque, true);
        UIKit.Por(pontaCima, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f + (TileH + 56f) / 2f + 12f), new Vector2(40f, 26f));
        var pontaBaixo = UIKit.Texto3(d, "PB", "▲", 22f, TextAlignmentOptions.Center, UIKit.Destaque, true);
        UIKit.Por(pontaBaixo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f - (TileH + 56f) / 2f - 12f), new Vector2(40f, 26f));

        // ---- botao ----
        botaoAbrir = MenuPrincipal.Botao(d, "ABRIR", Vector2.zero, 340f, 58f, UIKit.Destaque);
        var brt = (RectTransform)botaoAbrir.transform;
        brt.anchorMin = new Vector2(0.5f, 0f); brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0f, 26f);
        txtBotao = botaoAbrir.GetComponentInChildren<TextMeshProUGUI>();
        botaoAbrir.onClick.AddListener(Abrir);

        txtRestantes = UIKit.Texto3(d, "Rest", "", 13f, TextAlignmentOptions.Center, UIKit.TextoFraco, true);
        UIKit.Por(txtRestantes, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(600f, 18f));

        PreencherFitaIdle();
        AtualizarBotao();
    }

    // ---------------- dados ----------------

    private List<UpgradeData> Compraveis()
    {
        var r = new List<UpgradeData>();
        foreach (var u in Resources.LoadAll<UpgradeData>("Upgrades"))
            if (u != null && !MetaProgressao.CartaEhGratis(u)) r.Add(u);
        return r;
    }

    private List<UpgradeData> Bloqueadas()
    {
        var r = new List<UpgradeData>();
        foreach (var u in Compraveis())
            if (!MetaProgressao.CartaLiberada(u)) r.Add(u);
        return r;
    }

    private static UpgradeData SortearPorPeso(List<UpgradeData> pool)
    {
        float soma = 0f;
        foreach (var u in pool) soma += Mathf.Max(0.01f, u.weight);
        float x = Random.value * soma;
        foreach (var u in pool)
        {
            x -= Mathf.Max(0.01f, u.weight);
            if (x <= 0f) return u;
        }
        return pool[pool.Count - 1];
    }

    private static string RaridadeDe(UpgradeData u, out Color cor)
    {
        if (u.weight >= 9f) { cor = new Color(0.60f, 0.66f, 0.75f); return "COMUM"; }
        if (u.weight >= 6f) { cor = new Color(0.36f, 0.70f, 1.00f); return "INCOMUM"; }
        cor = new Color(0.85f, 0.45f, 1.00f); return "RARO";
    }

    // ---------------- fita ----------------

    private void PreencherFitaIdle()
    {
        var todas = Compraveis();
        if (todas.Count == 0) return;
        var seq = new List<UpgradeData>();
        for (int i = 0; i < TilesNaFita; i++) seq.Add(SortearPorPeso(todas));
        ReconstruirFita(seq);
        // centraliza num tile do meio
        fita.anchoredPosition = new Vector2(-(6 * Pitch) - TileW * 0.5f, 0f);
    }

    private void ReconstruirFita(List<UpgradeData> seq)
    {
        for (int i = fita.childCount - 1; i >= 0; i--) Destroy(fita.GetChild(i).gameObject);
        tiles.Clear();
        for (int i = 0; i < seq.Count; i++)
        {
            var rt = MontarTile(seq[i], i);
            tiles.Add(rt);
        }
    }

    private RectTransform MontarTile(UpgradeData u, int indice)
    {
        Color corRar; string rar = RaridadeDe(u, out corRar);

        var caixa = UIKit.Caixa(fita, "T" + indice, new Color(0.10f, 0.115f, 0.15f, 0.98f), 10);
        var rt = caixa.rectTransform;
        rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(indice * Pitch, 0f);
        rt.sizeDelta = new Vector2(TileW, TileH);
        caixa.raycastTarget = false;

        // faixa de raridade no rodape do tile (marca visual da sorte, estilo CS)
        var faixa = UIKit.Caixa(rt, "F", corRar, 4);
        UIKit.Por(faixa, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(TileW - 20f, 7f));

        var lr = UIKit.Texto3(rt, "R", rar, 11f, TextAlignmentOptions.Center, corRar, true);
        UIKit.Por(lr, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(TileW - 24f, 16f));
        lr.characterSpacing = 6f;

        var nome = UIKit.Texto3(rt, "N", u.displayName, 20f, TextAlignmentOptions.Center, UIKit.Texto, true);
        UIKit.Por(nome, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(TileW - 26f, 52f));
        nome.textWrappingMode = TextWrappingModes.Normal;
        nome.enableAutoSizing = true; nome.fontSizeMin = 13f; nome.fontSizeMax = 20f;

        var ef = UIKit.Texto3(rt, "E", u.DescricaoFormatada(), 12f, TextAlignmentOptions.Center, UIKit.TextoFraco, false);
        UIKit.Por(ef, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(TileW - 30f, 56f));
        ef.textWrappingMode = TextWrappingModes.Normal;

        return rt;
    }

    // ---------------- abertura ----------------

    private void AtualizarBotao()
    {
        var restam = Bloqueadas();
        txtRestantes.text = restam.Count + " carta" + (restam.Count == 1 ? "" : "s") + " ainda na caixa";

        if (restam.Count == 0)
        {
            txtBotao.text = "TUDO DESBLOQUEADO";
            txtBotao.color = UIKit.TextoFraco;
            botaoAbrir.interactable = false;
        }
        else if (MetaProgressao.Dinheiro < Preco)
        {
            txtBotao.text = "ABRIR  —  $ " + Preco;
            txtBotao.color = UIKit.Perigo;
            botaoAbrir.interactable = false;
        }
        else
        {
            txtBotao.text = "ABRIR  —  $ " + Preco;
            txtBotao.color = UIKit.Destaque;
            botaoAbrir.interactable = true;
        }
    }

    private void Abrir()
    {
        if (rodando) return;
        var restam = Bloqueadas();
        if (restam.Count == 0 || !MetaProgressao.Gastar(Preco)) { AtualizarBotao(); return; }

        // sorteia e JA CREDITA - fechar o menu no meio nao perde a carta
        vencedora = SortearPorPeso(restam);
        MetaProgressao.ComprarCarta(vencedora, 0);
        if (aoMudarBanco != null) aoMudarBanco();

        if (overlayRevelacao != null) { Destroy(overlayRevelacao); overlayRevelacao = null; }

        // fita nova: enchimento aleatorio, vencedora cravada no indice 44
        var todas = Compraveis();
        var seq = new List<UpgradeData>();
        for (int i = 0; i < TilesNaFita; i++) seq.Add(SortearPorPeso(todas));
        seq[IndiceVencedor] = vencedora;
        ReconstruirFita(seq);

        // percurso: comeca no tile 2, termina no 44 com desvio aleatorio DENTRO
        // do tile (a agulha nunca aponta pro vizinho) - e o suspense do CS
        float desvio = Random.Range(-TileW * 0.34f, TileW * 0.34f);
        xInicial = -(2 * Pitch) - TileW * 0.5f;
        xFinal = -(IndiceVencedor * Pitch) - TileW * 0.5f + desvio;
        fita.anchoredPosition = new Vector2(xInicial, 0f);

        t = 0f;
        rodando = true;
        botaoAbrir.interactable = false;
        txtBotao.text = "..."; 
    }

    private void Update()
    {
        if (rodando)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Duracao);
            float easing = 1f - Mathf.Pow(1f - k, 3f);   // ease-out cubico: arranca rapido, morre devagar
            float x = Mathf.Lerp(xInicial, xFinal, easing);
            fita.anchoredPosition = new Vector2(x, 0f);
            DestacarTileSobAgulha(x);

            if (k >= 1f)
            {
                rodando = false;
                Revelar();
            }
            return;
        }

        // idle: deriva lenta da fita pra vitrine nao ficar morta
        if (overlayRevelacao == null && tiles.Count > 0)
        {
            redesenhoIdle += Time.unscaledDeltaTime;
            var p = fita.anchoredPosition;
            p.x -= 6f * Time.unscaledDeltaTime;
            float limite = -((TilesNaFita - 8) * Pitch);
            if (p.x < limite) p.x = -(6 * Pitch);
            fita.anchoredPosition = p;
            DestacarTileSobAgulha(p.x);
        }
    }

    /// <summary>O tile sob a agulha cresce um tico - e o "tick" visual da roleta.</summary>
    private void DestacarTileSobAgulha(float xFita)
    {
        // centro da agulha em coordenadas da fita: -xFita
        int indice = Mathf.RoundToInt((-xFita - TileW * 0.5f) / Pitch);
        if (indice == tileDestacado) return;
        if (tileDestacado >= 0 && tileDestacado < tiles.Count && tiles[tileDestacado] != null)
            tiles[tileDestacado].localScale = Vector3.one;
        tileDestacado = indice;
        if (indice >= 0 && indice < tiles.Count && tiles[indice] != null)
            tiles[indice].localScale = Vector3.one * 1.07f;
    }

    // ---------------- revelacao ----------------

    private void Revelar()
    {
        Color corRar; string rar = RaridadeDe(vencedora, out corRar);

        overlayRevelacao = new GameObject("Revelacao", typeof(RectTransform));
        overlayRevelacao.transform.SetParent(transform, false);
        var ort = (RectTransform)overlayRevelacao.transform;
        ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
        ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;

        var veu = UIKit.Caixa(ort, "Veu", new Color(0.01f, 0.012f, 0.02f, 0.90f), 1);
        UIKit.Esticar(veu); veu.raycastTarget = true;

        // brilho da raridade atras da carta
        var glow = UIKit.Caixa(ort, "Glow", new Color(corRar.r, corRar.g, corRar.b, 0.13f), 28);
        UIKit.Por(glow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(470f, 540f));

        var carta = UIKit.PainelBordado(ort, "Carta", UIKit.PainelForte, 18);
        UIKit.Por(carta, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(420f, 480f));
        var d = carta.transform.GetChild(0);

        var faixa = UIKit.Caixa(d, "F", corRar, 14);
        UIKit.Por(faixa, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(416f, 96f));

        var lr = UIKit.Texto3(faixa.transform, "R", rar, 16f, TextAlignmentOptions.Center, new Color(0f, 0f, 0f, 0.75f), true);
        UIKit.Por(lr, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(380f, 22f));
        lr.characterSpacing = 10f;

        var nome = UIKit.Texto3(faixa.transform, "N", vencedora.displayName, 30f, TextAlignmentOptions.Center, Color.white, true);
        UIKit.Por(nome, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(390f, 52f));
        nome.textWrappingMode = TextWrappingModes.Normal;
        nome.enableAutoSizing = true; nome.fontSizeMin = 18f; nome.fontSizeMax = 30f;

        var ef = UIKit.Texto3(d, "E", vencedora.DescricaoFormatada(), 22f, TextAlignmentOptions.Center, UIKit.Destaque, true);
        UIKit.Por(ef, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(370f, 100f));
        ef.textWrappingMode = TextWrappingModes.Normal;

        if (!string.IsNullOrEmpty(vencedora.flavor))
        {
            var fl = UIKit.Texto3(d, "S", "<i>" + vencedora.flavor + "</i>", 16f, TextAlignmentOptions.Center,
                                  new Color(0.55f, 0.59f, 0.67f), false);
            UIKit.Por(fl, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(360f, 70f));
            fl.textWrappingMode = TextWrappingModes.Normal;
        }

        var pilha = UIKit.Texto3(d, "P", "até " + vencedora.maxStacks + "x na mesma partida", 13f,
                                 TextAlignmentOptions.Center, UIKit.TextoFraco, true);
        UIKit.Por(pilha, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(360f, 18f));

        var add = UIKit.Texto3(d, "A", "ADICIONADA AO SORTEIO DE LEVEL UP", 13f,
                               TextAlignmentOptions.Center, new Color(0.45f, 0.9f, 0.5f), true);
        UIKit.Por(add, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 66f), new Vector2(380f, 18f));
        add.characterSpacing = 3f;

        // botoes
        var bDeNovo = MenuPrincipal.Botao(ort, "ABRIR OUTRA  —  $ " + Preco, Vector2.zero, 320f, 52f, UIKit.Destaque);
        var r1 = (RectTransform)bDeNovo.transform;
        r1.anchorMin = new Vector2(0.5f, 0f); r1.anchorMax = new Vector2(0.5f, 0f); r1.pivot = new Vector2(0.5f, 0f);
        r1.anchoredPosition = new Vector2(-170f, 40f);
        bDeNovo.interactable = MetaProgressao.Dinheiro >= Preco && Bloqueadas().Count > 0;
        bDeNovo.onClick.AddListener(() => { Destroy(overlayRevelacao); overlayRevelacao = null; botaoAbrir.gameObject.SetActive(true); AtualizarBotao(); Abrir(); });

        var bFechar = MenuPrincipal.Botao(ort, "FECHAR", Vector2.zero, 320f, 52f, UIKit.Texto);
        var r2 = (RectTransform)bFechar.transform;
        r2.anchorMin = new Vector2(0.5f, 0f); r2.anchorMax = new Vector2(0.5f, 0f); r2.pivot = new Vector2(0.5f, 0f);
        r2.anchoredPosition = new Vector2(170f, 40f);
        bFechar.onClick.AddListener(() => { Destroy(overlayRevelacao); overlayRevelacao = null; botaoAbrir.gameObject.SetActive(true); AtualizarBotao(); });

        botaoAbrir.gameObject.SetActive(false);

        // pop de entrada da carta
        overlayRevelacao.AddComponent<PopDeEntrada>().Alvo(carta.rectTransform);
        AtualizarBotao();
    }
}

/// <summary>Escala a carta revelada de 0.7 -> 1 com overshoot. Tempo nao escalado.</summary>
public class PopDeEntrada : MonoBehaviour
{
    private RectTransform alvo;
    private float t;

    public void Alvo(RectTransform rt) { alvo = rt; alvo.localScale = Vector3.one * 0.7f; }

    private void Update()
    {
        if (alvo == null) return;
        t += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(t / 0.35f);
        // overshoot: passa de 1.0 e volta
        float s = 0.7f + 0.3f * (1f + 1.7f * Mathf.Pow(k - 1f, 3f) + 0.7f * Mathf.Pow(k - 1f, 2f));
        float over = 1f + 0.08f * Mathf.Sin(k * Mathf.PI);
        alvo.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, k) * over;
        if (k >= 1f) { alvo.localScale = Vector3.one; enabled = false; }
    }
}
