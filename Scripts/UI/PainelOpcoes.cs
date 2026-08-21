using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TELA DE OPCOES. Monta-se dentro de qualquer pai - serve tanto pro menu
/// principal quanto pra pausa, sem duplicar codigo.
///
/// Toda linha aplica NA HORA e salva na hora. Nada de botao 'aplicar':
/// mexer na sensibilidade e ver o efeito imediatamente e o que deixa ela
/// calibravel de verdade, e e como Deep Rock Galactic e Hades fazem.
/// </summary>
public static class PainelOpcoes
{
    private const float LinhaAlt = 38f;
    private const float Larg = 620f;

    public static void Montar(Transform pai, Action aoFechar)
    {
        var scroll = new GameObject("Opcoes_Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        scroll.transform.SetParent(pai, false);
        scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        var srt = (RectTransform)scroll.transform;
        // preenche a area que o pai deu, com margem. Largura fixa centrada estourava
        // a mascara e cortava os rotulos nas duas pontas.
        srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.offsetMin = new Vector2(30f, 84f);
        srt.offsetMax = new Vector2(-30f, -24f);

        var conteudo = new GameObject("Conteudo", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        conteudo.transform.SetParent(scroll.transform, false);
        var crt = (RectTransform)conteudo.transform;
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        // RectTransform novo nasce com sizeDelta (100,100). Com a ancora esticada
        // na horizontal, esses 100 viram 50 px sobrando de CADA lado - era o que
        // fazia a mascara cortar os rotulos nas duas pontas.
        crt.sizeDelta = new Vector2(0f, 0f);
        crt.anchoredPosition = Vector2.zero;
        var vlg = conteudo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f; vlg.padding = new RectOffset(10, 10, 6, 20);
        vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        conteudo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sr = scroll.GetComponent<ScrollRect>();
        sr.content = crt; sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 28f;

        Transform c = conteudo.transform;

        Secao(c, "JOGO");
        Deslizante(c, "SENSIBILIDADE DO MOUSE", 0.4f, 8f, OpcoesJogo.Sensibilidade, "F2",
            v => { OpcoesJogo.Sensibilidade = v; Aplicar(); });
        Deslizante(c, "SENSIBILIDADE MIRANDO", 0.2f, 1.2f, OpcoesJogo.SensNaMira, "F2",
            v => { OpcoesJogo.SensNaMira = v; Aplicar(); });
        Deslizante(c, "CAMPO DE VISÃO", 55f, 95f, OpcoesJogo.Fov, "F0",
            v => { OpcoesJogo.Fov = v; Aplicar(); });
        Chave(c, "INVERTER EIXO Y", OpcoesJogo.InverterY,
            v => { OpcoesJogo.InverterY = v; Aplicar(); });

        Secao(c, "VÍDEO");
        Ciclo(c, "QUALIDADE", new string[]{"BAIXO","MÉDIO","ALTO"}, OpcoesJogo.Qualidade,
            v => { OpcoesJogo.Qualidade = v; Aplicar(); });
        Ciclo(c, "SINCRONIA VERTICAL", new string[]{"DESLIGADA","LIGADA"}, Mathf.Clamp(OpcoesJogo.Vsync,0,1),
            v => { OpcoesJogo.Vsync = v; Aplicar(); });
        int[] fpsOps = new int[]{0, 60, 120, 144, 240};
        int idxFps = 0; for (int i = 0; i < fpsOps.Length; i++) if (fpsOps[i] == OpcoesJogo.LimiteFps) idxFps = i;
        Ciclo(c, "LIMITE DE QUADROS", new string[]{"SEM LIMITE","60","120","144","240"}, idxFps,
            v => { OpcoesJogo.LimiteFps = fpsOps[v]; Aplicar(); });
        Chave(c, "TELA CHEIA", OpcoesJogo.TelaCheia, v => {
            OpcoesJogo.TelaCheia = v;
            Screen.fullScreenMode = v ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Aplicar();
        });

        Secao(c, "ÁUDIO");
        Deslizante(c, "VOLUME GERAL", 0f, 1f, OpcoesJogo.VolumeGeral, "P0",
            v => { OpcoesJogo.VolumeGeral = v; Aplicar(); });
        Aviso(c, "o jogo ainda não tem som — o controle já fica pronto");

        Secao(c, "ACESSIBILIDADE");
        Deslizante(c, "TREMOR DE TELA", 0f, 1f, OpcoesJogo.TremorTela, "P0",
            v => { OpcoesJogo.TremorTela = v; Aplicar(); });
        Chave(c, "CONGELAR QUADRO NO ABATE", OpcoesJogo.CongelarQuadro,
            v => { OpcoesJogo.CongelarQuadro = v; Aplicar(); });
        Chave(c, "MARCADOR DE ACERTO", OpcoesJogo.MarcadorDeAcerto,
            v => { OpcoesJogo.MarcadorDeAcerto = v; Aplicar(); });
        Chave(c, "NÚMEROS DE DANO", OpcoesJogo.NumerosDeDano,
            v => { OpcoesJogo.NumerosDeDano = v; Aplicar(); });
        Aviso(c, "tremor de tela causa enjoo em muita gente — dá pra zerar");

        var restaurar = MenuPrincipal.Botao(c, "RESTAURAR PADRÕES", Vector2.zero, Larg, 40f, UIKit.TextoFraco);
        var lr = restaurar.gameObject.AddComponent<LayoutElement>();
        lr.preferredHeight = 40f;
        restaurar.onClick.AddListener(() => {
            OpcoesJogo.Restaurar();
            // remonta pra os controles mostrarem os valores novos
            for (int i = pai.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(pai.GetChild(i).gameObject);
            Montar(pai, aoFechar);
        });
    }

    private static void Aplicar() { OpcoesJogo.Aplicar(); OpcoesJogo.Salvar(); }

    // ---------------- pecas ----------------

    private static void Secao(Transform pai, string titulo)
    {
        var t = UIKit.Texto3(pai, "S_" + titulo, titulo, 13f, TextAlignmentOptions.Left, UIKit.Destaque, true);
        t.characterSpacing = 8f;
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 34f;
        t.margin = new Vector4(4f, 12f, 0f, 0f);
    }

    private static void Aviso(Transform pai, string txt)
    {
        var t = UIKit.Texto3(pai, "A", txt, 11f, TextAlignmentOptions.Left, new Color(0.45f, 0.49f, 0.56f), false);
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 18f;
        t.margin = new Vector4(6f, 0f, 0f, 0f);
    }

    private static Transform Linha(Transform pai, string rotulo)
    {
        var caixa = UIKit.PainelBordado(pai, "L_" + rotulo, UIKit.Painel, 8);
        var le = caixa.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = LinhaAlt;
        var d = caixa.transform.GetChild(0);
        var lbl = UIKit.Texto3(d, "L", rotulo, 14f, TextAlignmentOptions.Left, UIKit.Texto, false);
        UIKit.Por(lbl, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(300f, 20f));
        return d;
    }

    private static void Deslizante(Transform pai, string rotulo, float min, float max, float valor, string fmt, Action<float> aoMudar)
    {
        var d = Linha(pai, rotulo);

        var val = UIKit.Texto3(d, "V", "", 14f, TextAlignmentOptions.Right, UIKit.Destaque, true);
        UIKit.Por(val, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(70f, 22f));

        var trilho = UIKit.Caixa(d, "Trilho", UIKit.Trilho, 3);
        var trt = UIKit.Por(trilho, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-96f, 0f), new Vector2(210f, 6f));

        var preench = UIKit.Caixa(trilho.transform, "Fill", UIKit.Destaque, 3);
        var frt = (RectTransform)preench.transform;
        frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(1f, 1f);
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;

        var alca = UIKit.Caixa(trilho.transform, "Alca", UIKit.Texto, 8);
        var art = UIKit.Por(alca, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));

        var sl = trilho.gameObject.AddComponent<Slider>();
        sl.fillRect = frt; sl.handleRect = art; sl.targetGraphic = alca;
        sl.direction = Slider.Direction.LeftToRight;
        sl.minValue = min; sl.maxValue = max; sl.wholeNumbers = false;
        sl.SetValueWithoutNotify(valor);
        val.text = Formatar(valor, fmt);
        sl.onValueChanged.AddListener(v => { val.text = Formatar(v, fmt); aoMudar(v); });
    }

    private static string Formatar(float v, string fmt)
    {
        if (fmt == "P0") return Mathf.RoundToInt(v * 100f) + "%";
        return v.ToString(fmt);
    }

    private static void Chave(Transform pai, string rotulo, bool valor, Action<bool> aoMudar)
    {
        var d = Linha(pai, rotulo);
        bool estado = valor;

        var b = MenuPrincipal.Botao(d, estado ? "LIGADO" : "DESLIGADO", Vector2.zero, 130f, 26f,
                                    estado ? UIKit.Destaque : UIKit.TextoFraco);
        var brt = (RectTransform)b.transform;
        brt.anchorMin = new Vector2(1f, 0.5f); brt.anchorMax = new Vector2(1f, 0.5f);
        brt.pivot = new Vector2(1f, 0.5f);
        brt.anchoredPosition = new Vector2(-16f, 0f);
        var txt = b.GetComponentInChildren<TextMeshProUGUI>();
        b.onClick.AddListener(() => {
            estado = !estado;
            if (txt != null) { txt.text = estado ? "LIGADO" : "DESLIGADO"; txt.color = estado ? UIKit.Destaque : UIKit.TextoFraco; }
            aoMudar(estado);
        });
    }

    private static void Ciclo(Transform pai, string rotulo, string[] opcoes, int indice, Action<int> aoMudar)
    {
        var d = Linha(pai, rotulo);
        int idx = Mathf.Clamp(indice, 0, opcoes.Length - 1);

        var val = UIKit.Texto3(d, "V", opcoes[idx], 14f, TextAlignmentOptions.Center, UIKit.Destaque, true);
        UIKit.Por(val, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-50f, 0f), new Vector2(86f, 22f));

        var esq = MenuPrincipal.Botao(d, "◀", Vector2.zero, 28f, 26f, UIKit.Texto);
        var ert = (RectTransform)esq.transform;
        ert.anchorMin = new Vector2(1f, 0.5f); ert.anchorMax = new Vector2(1f, 0.5f);
        ert.pivot = new Vector2(1f, 0.5f); ert.anchoredPosition = new Vector2(-142f, 0f);

        var dir = MenuPrincipal.Botao(d, "▶", Vector2.zero, 28f, 26f, UIKit.Texto);
        var drt = (RectTransform)dir.transform;
        drt.anchorMin = new Vector2(1f, 0.5f); drt.anchorMax = new Vector2(1f, 0.5f);
        drt.pivot = new Vector2(1f, 0.5f); drt.anchoredPosition = new Vector2(-16f, 0f);

        esq.onClick.AddListener(() => { idx = (idx - 1 + opcoes.Length) % opcoes.Length; val.text = opcoes[idx]; aoMudar(idx); });
        dir.onClick.AddListener(() => { idx = (idx + 1) % opcoes.Length; val.text = opcoes[idx]; aoMudar(idx); });
    }
}
