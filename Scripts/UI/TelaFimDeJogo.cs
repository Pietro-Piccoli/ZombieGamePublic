using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// TELA DE FIM DE PARTIDA. Mostra o resumo da run em cartoes, com os numeros
/// subindo de zero e os cartoes entrando escalonados - e a contagem que da
/// prazer de ver, nao a tabela parada.
/// </summary>
public class TelaFimDeJogo : MonoBehaviour
{
    private class Cartao
    {
        public TextMeshProUGUI valor;
        public float alvo;
        public string sufixo;
        public bool inteiro;
        public float atraso;
        public RectTransform rt;
        public Vector2 posFinal;
    }

    private readonly List<Cartao> cartoes = new List<Cartao>();
    private float t;
    private GameObject canvasGo;

    public static void Mostrar()
    {
        if (Object.FindAnyObjectByType<TelaFimDeJogo>() != null) return;
        var go = new GameObject("TelaFimDeJogo");
        go.AddComponent<TelaFimDeJogo>();
    }

    private void Start()
    {
        var e = EstatisticasRun.Atual;

        // O recorde ANTERIOR precisa ser lido antes de Fechar(), senao a
        // comparacao na tela vira 'seu recorde e voce mesmo agora'.
        int recWave   = Recordes.MelhorWave;
        int recAbates = Recordes.MaisAbates;
        int recGolpe  = Recordes.MaiorGolpe;
        int recDps    = Recordes.MaiorPicoDps;
        long recDano  = Recordes.MaiorDano;
        var bat = Recordes.Fechar(e, e.WaveMaxima);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var canvas = UIKit.NovoCanvas(null, "FimDeJogo_Canvas", 180);
        canvasGo = canvas.gameObject;
        canvasGo.AddComponent<GraphicRaycaster>();
        MenuPrincipal.GarantirEventSystem();

        var veu = UIKit.Caixa(canvasGo.transform, "Veu", new Color(0.02f, 0.012f, 0.02f, 0.93f), 1);
        UIKit.Esticar(veu); veu.raycastTarget = true;

        // ---------- cabecalho ----------
        var tit = UIKit.Texto3(canvasGo.transform, "Tit", "VOCÊ MORREU", 66f, TextAlignmentOptions.Center,
                               new Color(0.88f, 0.15f, 0.16f), true);
        UIKit.Por(tit, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(1400f, 76f));
        tit.characterSpacing = 12f;

        var sub = UIKit.Texto3(canvasGo.transform, "Sub",
            "SOBREVIVEU  " + e.DuracaoFormatada + "   ·   WAVE " + e.WaveMaxima + "   ·   NÍVEL " + Mathf.Max(1, e.MaiorNivel)
            + (recWave > 0 ? (bat.wave ? "   ·   <color=#FFD23A>NOVO RECORDE</color>" : "   ·   SEU RECORDE: WAVE " + recWave) : ""),
            20f, TextAlignmentOptions.Center, UIKit.TextoFraco, true);
        UIKit.Por(sub, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(1400f, 26f));
        sub.characterSpacing = 8f;

        // ---------- cartoes ----------
        // rotulo | valor | sufixo | inteiro | cor | nota
        object[][] dados = new object[][] {
            new object[]{ "ZUMBIS ABATIDOS", (float)e.Abates, "", true, UIKit.Destaque, e.MaisZumbisDeUmaVez + " de uma vez só", bat.abates, recAbates },
            new object[]{ "DANO TOTAL", (float)e.DanoTotal, "", true, new Color(1.00f,0.47f,0.16f), Mathf.RoundToInt(e.DpsMedio) + " por segundo, em média", bat.dano, (int)recDano },
            new object[]{ "PICO DE DANO", (float)e.PicoDps, "/s", true, new Color(1.00f,0.30f,0.30f), "seu melhor segundo", bat.dps, recDps },
            new object[]{ "MAIOR GOLPE", (float)e.MaiorGolpe, "", true, new Color(1.00f,0.84f,0.18f), "num único tiro", bat.golpe, recGolpe },
            new object[]{ "PRECISÃO", e.Precisao * 100f, "%", false, new Color(0.34f,0.78f,1.00f), e.TirosQueAcertaram + " de " + e.TirosDados + " tiros", false, 0 },
            new object[]{ "NA CABEÇA", e.FracaoCabeca * 100f, "%", false, new Color(1.00f,0.84f,0.18f), e.AbatesNaCabeca + " abates", false, 0 },
            new object[]{ "DINHEIRO", (float)e.DinheiroGanho, "", true, UIKit.Destaque, "foi tudo pro banco", false, 0 },
            new object[]{ "CARTAS PEGAS", (float)e.CartasPegas, "", true, new Color(0.75f,0.45f,1.00f), "escolhidas no level up", false, 0 }
        };

        const float LG = 300f, AL = 118f, PX = 320f, PY = 132f;
        float x0 = -(PX * 3f) / 2f;
        for (int i = 0; i < dados.Length; i++)
        {
            int col = i % 4, lin = i / 4;
            var pos = new Vector2(x0 + col * PX, 128f - lin * PY);
            MontarCartao(canvasGo.transform, (string)dados[i][0], (float)dados[i][1], (string)dados[i][2],
                         (bool)dados[i][3], (Color)dados[i][4], (string)dados[i][5], pos, LG, AL, 0.06f * i,
                         (bool)dados[i][6], (int)dados[i][7]);
        }

        // ---------- de onde veio o dano ----------
        MontarBarraDeDano(canvasGo.transform, e);

        // ---------- botoes ----------
        string[] rot = new string[]{ "JOGAR DE NOVO", "MENU PRINCIPAL", "SAIR" };
        for (int i = 0; i < rot.Length; i++)
        {
            int k = i;
            var b = MenuPrincipal.Botao(canvasGo.transform, rot[i], Vector2.zero, 300f, 52f,
                                        i == 2 ? UIKit.Perigo : UIKit.Texto);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2((i - 1) * 320f, 48f);
            b.onClick.AddListener(() => Acao(k));
        }
    }

    private void MontarCartao(Transform pai, string rotulo, float valor, string sufixo, bool inteiro,
                              Color cor, string nota, Vector2 pos, float lg, float al, float atraso,
                              bool recorde, int recordeAnterior)
    {
        // Nota vira comparacao quando existe recorde anterior. Numero sozinho nao
        // diz nada; numero contra o teu melhor diz se voce esta melhorando.
        if (recorde && recordeAnterior > 0) nota = "antes era " + recordeAnterior;
        else if (!recorde && recordeAnterior > 0) nota = "seu recorde: " + recordeAnterior;
        var caixa = UIKit.PainelBordado(pai, "C_" + rotulo, UIKit.PainelForte, 12);
        var rt = UIKit.Por(caixa, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos + new Vector2(0f, -18f), new Vector2(lg, al));
        var d = caixa.transform.GetChild(0);

        var faixa = UIKit.Caixa(d, "F", cor, 3);
        UIKit.Por(faixa, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(4f, al - 22f));

        var lbl = UIKit.Texto3(d, "L", rotulo, 13f, TextAlignmentOptions.Left, UIKit.TextoFraco, true);
        UIKit.Por(lbl, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -12f), new Vector2(lg - 40f, 18f));
        lbl.characterSpacing = 5f;

        var val = UIKit.Texto3(d, "V", "0", 38f, TextAlignmentOptions.Left, cor, true);
        UIKit.Por(val, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -34f), new Vector2(lg - 40f, 46f));

        var nt = UIKit.Texto3(d, "N", nota, 12f, TextAlignmentOptions.Left, new Color(0.52f, 0.56f, 0.63f), false);
        UIKit.Por(nt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(22f, 10f), new Vector2(lg - 40f, 18f));

        if (recorde)
        {
            // selo curto e no canto: tem que ser notado sem roubar o numero
            var selo = UIKit.Caixa(d, "Selo", new Color(1f, 0.82f, 0.23f), 4);
            UIKit.Por(selo, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(74f, 18f));
            var st = UIKit.Texto3(selo.transform, "T", "RECORDE", 10f, TextAlignmentOptions.Center, new Color(0.08f, 0.06f, 0.02f), true);
            UIKit.Esticar(st);
            st.characterSpacing = 3f;
        }

        cartoes.Add(new Cartao {
            valor = val, alvo = valor, sufixo = sufixo, inteiro = inteiro,
            atraso = atraso, rt = rt, posFinal = pos
        });
    }

    /// <summary>Barra empilhada: quanto do dano veio de cada fonte.</summary>
    private void MontarBarraDeDano(Transform pai, EstatisticasRun e)
    {
        long total = System.Math.Max(1, e.DanoTiro + e.DanoCabeca + e.DanoExplosao + e.DanoFogo + e.DanoAcido);

        var painel = UIKit.PainelBordado(pai, "Fontes", UIKit.Painel, 12);
        UIKit.Por(painel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -124f), new Vector2(1240f, 84f));
        var d = painel.transform.GetChild(0);

        var t2 = UIKit.Texto3(d, "T", "DE ONDE VEIO O DANO", 12f, TextAlignmentOptions.Left, UIKit.TextoFraco, true);
        UIKit.Por(t2, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(400f, 16f));
        t2.characterSpacing = 6f;

        string[] nomes = new string[]{ "TIRO", "CABEÇA", "EXPLOSÃO", "FOGO", "ÁCIDO" };
        long[] vals = new long[]{ e.DanoTiro, e.DanoCabeca, e.DanoExplosao, e.DanoFogo, e.DanoAcido };
        Color[] cores = new Color[]{
            new Color(0.86f,0.88f,0.92f), new Color(1.00f,0.84f,0.18f), new Color(1.00f,0.47f,0.16f),
            new Color(1.00f,0.62f,0.10f), new Color(0.55f,1.00f,0.22f) };

        float larguraTotal = 1200f;
        float x = 20f;
        for (int i = 0; i < vals.Length; i++)
        {
            if (vals[i] <= 0) continue;
            float w = larguraTotal * ((float)vals[i] / total);
            var seg = UIKit.Caixa(d, "S" + i, cores[i], 3);
            UIKit.Por(seg, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, 34f), new Vector2(Mathf.Max(2f, w - 2f), 14f));

            if (w > 70f)
            {
                var pct = UIKit.Texto3(d, "P" + i, nomes[i] + "  " + Mathf.RoundToInt(100f * vals[i] / total) + "%",
                                       11f, TextAlignmentOptions.Left, cores[i], true);
                UIKit.Por(pct, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, 14f), new Vector2(220f, 16f));
            }
            x += w;
        }
    }

    private void Update()
    {
        t += Time.unscaledDeltaTime;
        for (int i = 0; i < cartoes.Count; i++)
        {
            var c = cartoes[i];
            float local = Mathf.Clamp01((t - c.atraso) / 0.75f);
            // suavizacao: rapido no comeco, freia no fim
            float k = 1f - Mathf.Pow(1f - local, 3f);

            float v = c.alvo * k;
            c.valor.text = (c.inteiro ? Mathf.RoundToInt(v).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))
                                      : v.ToString("0.0")) + c.sufixo;

            // cartao entra subindo e aparecendo
            if (c.rt != null)
            {
                c.rt.anchoredPosition = Vector2.Lerp(c.posFinal + new Vector2(0f, -18f), c.posFinal, k);
                var cg = c.rt.GetComponent<CanvasGroup>();
                if (cg == null) cg = c.rt.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = k;
            }
        }
    }

    private void Acao(int i)
    {
        Time.timeScale = 1f;
        if (i == 2) { MenuPausa.Sair(); return; }
        MenuPrincipal.AbrirNoProximoCarregamento = (i == 1);
        if (canvasGo != null) Destroy(canvasGo);
        Destroy(gameObject);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
