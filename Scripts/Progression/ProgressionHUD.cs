using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dinheiro (topo esquerdo) e barra de XP com nivel (rodape).
/// Estilo todo herdado do UIKit.
/// </summary>
[RequireComponent(typeof(PlayerProgression))]
public class ProgressionHUD : MonoBehaviour
{
    private PlayerProgression prog;
    private TextMeshProUGUI txtDinheiro, txtNivel;
    private Image barraXp;
    private int ultimoDinheiro = -1;
    private float pulso;

    private void Awake() { prog = GetComponent<PlayerProgression>(); }

    private void Start()
    {
        var canvas = UIKit.NovoCanvas(transform, "ProgressionHUD_Canvas", 56);

        // ---- chip de dinheiro (topo esquerdo) ----
        var chip = UIKit.PainelBordado(canvas.transform, "ChipDinheiro", UIKit.Painel, UIKit.RaioPainel);
        UIKit.Por(chip, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -18f), new Vector2(178f, 50f));

        txtDinheiro = UIKit.Texto3(chip.transform, "Dinheiro", "$ 0", 26f, TextAlignmentOptions.Left, UIKit.Destaque, true);
        UIKit.Por(txtDinheiro, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(150f, 34f));

        // ---- barra de XP (rodape, centralizada) ----
        barraXp = UIKit.Barra(canvas.transform, "BarraXp", UIKit.Xp, 8f, UIKit.RaioBarra);
        var trilho = (RectTransform)barraXp.transform.parent;
        trilho.anchorMin = new Vector2(0.5f, 0f); trilho.anchorMax = new Vector2(0.5f, 0f);
        trilho.pivot = new Vector2(0.5f, 0f);
        trilho.anchoredPosition = new Vector2(0f, 22f);
        trilho.sizeDelta = new Vector2(680f, 8f);
        barraXp.fillAmount = 0f;

        txtNivel = UIKit.Texto3(canvas.transform, "Nivel", "LV 1", 17f, TextAlignmentOptions.Center, UIKit.TextoFraco, true);
        UIKit.Por(txtNivel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(200f, 24f));
        txtNivel.characterSpacing = 5f;
    }

    private void Update()
    {
        if (prog == null || barraXp == null) return;

        if (prog.Dinheiro != ultimoDinheiro)
        {
            ultimoDinheiro = prog.Dinheiro;
            txtDinheiro.text = "$ " + prog.Dinheiro.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
            pulso = 1f;
        }
        if (pulso > 0f)
        {
            pulso = Mathf.Max(0f, pulso - Time.unscaledDeltaTime * 4f);
            float s = 1f + 0.12f * pulso;
            txtDinheiro.transform.localScale = new Vector3(s, s, 1f);
        }

        txtNivel.text = "LV " + prog.Nivel;
        barraXp.fillAmount = Mathf.Lerp(barraXp.fillAmount, prog.XpPercent, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
    }
}
