using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD da wave no topo: painel com numero da wave, contador de kills e
/// barra de progresso. O aviso de intervalo fica solto embaixo, com contorno.
/// Todo o estilo vem do UIKit.
/// </summary>
public class WaveHUD : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private float margemTopo = 18f;
    [SerializeField] private float largura = 300f;

    private TextMeshProUGUI txtWave, txtContador, txtIntervalo;
    private Image barra;

    private void Start() { Montar(); }

    private void Montar()
    {
        var canvas = UIKit.NovoCanvas(transform, "WaveHUD_Canvas", 50);

        var painel = UIKit.PainelBordado(canvas.transform, "PainelWave", UIKit.Painel, UIKit.RaioPainel);
        UIKit.Por(painel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -margemTopo), new Vector2(largura, 86f));

        txtWave = UIKit.Texto3(painel.transform, "Wave", "WAVE 1", 34f, TextAlignmentOptions.Left, UIKit.Texto, true);
        UIKit.Por(txtWave, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -12f), new Vector2(200f, 40f));
        txtWave.characterSpacing = 4f;

        txtContador = UIKit.Texto3(painel.transform, "Contador", "0 / 12", 22f, TextAlignmentOptions.Right, UIKit.TextoFraco, true);
        UIKit.Por(txtContador, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -18f), new Vector2(140f, 30f));

        barra = UIKit.Barra(painel.transform, "BarraWave", UIKit.Perigo, 6f, UIKit.RaioBarra);
        var brt = (RectTransform)barra.transform.parent;
        brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.offsetMin = new Vector2(20f, 16f); brt.offsetMax = new Vector2(-20f, 0f);
        brt.sizeDelta = new Vector2(brt.sizeDelta.x, 6f);

        txtIntervalo = UIKit.Texto3(canvas.transform, "Intervalo", "", 26f, TextAlignmentOptions.Center, UIKit.Destaque, true);
        UIKit.Por(txtIntervalo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -margemTopo - 98f), new Vector2(800f, 36f));
        txtIntervalo.characterSpacing = 3f;
        UIKit.Contornar(txtIntervalo, 0.22f);
    }

    private void Update()
    {
        var wm = WaveManager.Instance;
        if (wm == null || txtWave == null) return;

        if (wm.InBreak)
        {
            txtWave.text = wm.CurrentWave == 0 ? "PREPARE-SE" : "WAVE " + wm.CurrentWave;
            txtContador.text = wm.CurrentWave == 0 ? "" : "COMPLETA";
            txtIntervalo.text = "PRÓXIMA WAVE EM " + Mathf.CeilToInt(wm.BreakTimeLeft);
            barra.fillAmount = 1f;
            barra.color = UIKit.Destaque;
        }
        else
        {
            txtWave.text = "WAVE " + wm.CurrentWave;
            txtContador.text = wm.KillsThisWave + " / " + wm.KillQuota;
            txtIntervalo.text = "";
            barra.fillAmount = wm.WaveProgress;
            barra.color = UIKit.Perigo;
        }
    }
}
