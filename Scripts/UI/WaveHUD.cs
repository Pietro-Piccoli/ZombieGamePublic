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

    // Cada atribuicao de .text no TextMeshPro forca RECONSTRUCAO DA MALHA do
    // texto, mesmo quando a string e identica a de antes. Escrever os tres
    // campos todo quadro era reconstruir tres malhas 200 vezes por segundo pra
    // mostrar o mesmo 'WAVE 7'. Guardar o ultimo valor e so escrever quando muda
    // e o conserto padrao de HUD em Unity.
    private int ultWave = -1, ultKills = -1, ultQuota = -1, ultSegundos = -1;
    private bool ultBreak;
    private bool primeira = true;

    private void Update()
    {
        var wm = WaveManager.Instance;
        if (wm == null || txtWave == null) return;

        if (wm.InBreak)
        {
            int seg = Mathf.CeilToInt(wm.BreakTimeLeft);
            if (primeira || !ultBreak || wm.CurrentWave != ultWave)
            {
                txtWave.text = wm.CurrentWave == 0 ? "PREPARE-SE" : "WAVE " + wm.CurrentWave;
                txtContador.text = wm.CurrentWave == 0 ? "" : "COMPLETA";
                barra.fillAmount = 1f;
                barra.color = UIKit.Destaque;
            }
            if (primeira || seg != ultSegundos)
            {
                txtIntervalo.text = "PRÓXIMA WAVE EM " + seg;   // muda 1x por segundo, nao 200
                ultSegundos = seg;
            }
        }
        else
        {
            if (primeira || ultBreak || wm.CurrentWave != ultWave)
            {
                txtWave.text = "WAVE " + wm.CurrentWave;
                txtIntervalo.text = "";
                barra.color = UIKit.Perigo;
            }
            if (primeira || wm.KillsThisWave != ultKills || wm.KillQuota != ultQuota)
            {
                txtContador.text = wm.KillsThisWave + " / " + wm.KillQuota;
                ultKills = wm.KillsThisWave; ultQuota = wm.KillQuota;
            }
            barra.fillAmount = wm.WaveProgress;   // Image.fillAmount nao reconstroi malha
        }

        ultWave = wm.CurrentWave;
        ultBreak = wm.InBreak;
        primeira = false;
    }
}
