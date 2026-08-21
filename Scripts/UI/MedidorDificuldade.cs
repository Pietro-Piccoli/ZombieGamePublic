using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// MEDIDOR DE DIFICULDADE, canto superior esquerdo, no molde do Risk of Rain 2:
/// cronometro da run + barra com entalhes que enche e troca de nome/cor conforme
/// a dificuldade sobe (FÁCIL -> ... -> HAHAHAHA).
///
/// Se cria sozinho junto com o resto do HUD.
/// </summary>
public class MedidorDificuldade : MonoBehaviour
{
    [SerializeField] private float largura = 240f;
    [SerializeField] private float alturaBarra = 10f;
    private const int Entalhes = 3;

    private Dificuldade dif;
    private TextMeshProUGUI txtTempo, txtFaixa, txtNivel;
    private Image[] pedacos;
    private Image[] fundos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Registrar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AoCarregar;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AoCarregar;
        Nascer();
    }

    private static void AoCarregar(UnityEngine.SceneManagement.Scene c,
                                   UnityEngine.SceneManagement.LoadSceneMode m) { Nascer(); }

    private static void Nascer()
    {
        if (Object.FindAnyObjectByType<MedidorDificuldade>() != null) return;
        var go = new GameObject("MedidorDificuldade");
        go.AddComponent<MedidorDificuldade>();
    }

    private void Start()
    {
        var canvas = UIKit.NovoCanvas(transform, "Dificuldade_Canvas", 58);

        var painel = UIKit.PainelBordado(canvas.transform, "Painel", UIKit.Painel, UIKit.RaioPainel);
        UIKit.Por(painel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -18f), new Vector2(largura + 32f, 84f));
        var dentro = painel.transform.GetChild(0);

        // cronometro grande: no RoR2 o tempo E a dificuldade
        txtTempo = UIKit.Texto3(dentro, "Tempo", "00:00", 30f, TextAlignmentOptions.Left, UIKit.Texto, true);
        UIKit.Por(txtTempo, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(150f, 34f));

        txtNivel = UIKit.Texto3(dentro, "Nivel", "Nv 1", 15f, TextAlignmentOptions.Right, UIKit.TextoFraco, true);
        UIKit.Por(txtNivel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(110f, 22f));

        txtFaixa = UIKit.Texto3(dentro, "Faixa", "FÁCIL", 15f, TextAlignmentOptions.Left, UIKit.Texto, true);
        UIKit.Por(txtFaixa, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -42f), new Vector2(largura, 20f));
        txtFaixa.characterSpacing = 5f;

        // barra dividida em entalhes, igual ao medidor do RoR2
        pedacos = new Image[Entalhes];
        fundos = new Image[Entalhes];
        float larguraPedaco = (largura - (Entalhes - 1) * 4f) / Entalhes;
        for (int i = 0; i < Entalhes; i++)
        {
            var fundo = UIKit.Caixa(dentro, "Trilho" + i, new Color(1f, 1f, 1f, 0.10f), 3);
            UIKit.Por(fundo, new Vector2(0f, 0f), new Vector2(0f, 0f),
                      new Vector2(16f + i * (larguraPedaco + 4f), 12f), new Vector2(larguraPedaco, alturaBarra));
            fundos[i] = fundo;

            var cheio = UIKit.Caixa(fundo.transform, "Cheio", Color.white, 3);
            var rt = cheio.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            cheio.type = Image.Type.Filled;
            cheio.fillMethod = Image.FillMethod.Horizontal;
            cheio.fillOrigin = 0;
            cheio.fillAmount = 0f;
            pedacos[i] = cheio;
        }
    }

    private void Update()
    {
        if (dif == null) { dif = Dificuldade.Instancia; if (dif == null) return; }
        if (txtTempo == null) return;

        txtTempo.text = dif.TempoFormatado;
        txtNivel.text = "Nv " + dif.NivelInteiro;

        Color cor = dif.CorFaixa;
        txtFaixa.text = dif.NomeFaixa;
        txtFaixa.color = cor;

        // reparte o progresso da faixa entre os entalhes
        float prog = dif.ProgressoFaixa * Entalhes;
        for (int i = 0; i < Entalhes; i++)
        {
            float f = Mathf.Clamp01(prog - i);
            pedacos[i].fillAmount = f;
            pedacos[i].color = cor;
        }
    }
}
