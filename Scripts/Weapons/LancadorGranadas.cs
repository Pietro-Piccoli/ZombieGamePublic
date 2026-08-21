using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// LANCADOR DE GRANADAS. Cada ficha ocupa um slot com recarga propria.
/// Por padrao: G = primeira granada, H = segunda.
///
/// Tambem monta o HUD de recarga no canto inferior direito.
/// </summary>
public class LancadorGranadas : MonoBehaviour
{
    [Header("Granadas")]
    [Tooltip("Ordem importa: a primeira e o G, a segunda o H.")]
    [SerializeField] private GranadaData[] granadas;

    [Header("Arremesso")]
    [Tooltip("De onde a granada sai, relativo a camera.")]
    [SerializeField] private Vector3 deslocamentoMao = new Vector3(0.35f, -0.15f, 0.6f);

    [Tooltip("Onde o modelo da granada fica na MAO ESQUERDA durante o gesto (a direita segura a AK). Eixos medidos no rig: +Y desce pelos dedos, +Z aponta pro polegar.")]
    [SerializeField] private Vector3 posicaoNaMao = new Vector3(-0.006f, 0.082f, 0.032f);
    [SerializeField] private Vector3 rotacaoNaMao = Vector3.zero;
    [Tooltip("O modelo do pacote tem 27 cm - tamanho de bola de futebol. Na mao ele precisa do tamanho real de uma granada, ~11 cm.")]
    [SerializeField] private float escalaNaMao = 0.42f;

    private CameraJogo mira;
    private CharacterController cc;
    private UpgradeInventory inv;
    private AnimacaoJogador anim;
    private GameObject naMao;
    private float[] prontaEm;
    private float[] recargaAplicada;

    private Image[] iconeFundo;
    private Image[] iconePreenche;
    private TextMeshProUGUI[] iconeTexto;

    private void Start()
    {
        mira = GetComponent<CameraJogo>();
        if (mira == null) mira = Object.FindAnyObjectByType<CameraJogo>();
        cc = GetComponent<CharacterController>();
        inv = GetComponent<UpgradeInventory>();
        anim = GetComponent<AnimacaoJogador>();
        if (anim == null) anim = GetComponentInChildren<AnimacaoJogador>();

        int n = granadas != null ? granadas.Length : 0;
        prontaEm = new float[n];
        recargaAplicada = new float[n];
        for (int i = 0; i < n; i++) recargaAplicada[i] = granadas[i] != null ? granadas[i].recarga : 60f;
        if (n > 0) MontarHud(n);
    }

    private static bool Apertou(int slot)
    {
#if ENABLE_INPUT_SYSTEM
        var k = UnityEngine.InputSystem.Keyboard.current;
        if (k == null) return false;
        if (slot == 0) return k.gKey.wasPressedThisFrame;
        if (slot == 1) return k.hKey.wasPressedThisFrame;
        return false;
#else
        if (slot == 0) return Input.GetKeyDown(KeyCode.G);
        if (slot == 1) return Input.GetKeyDown(KeyCode.H);
        return false;
#endif
    }

    private void Update()
    {
        if (granadas == null) return;
        if (MenuPrincipal.Aberto || MenuPausa.Pausado) return;

        for (int i = 0; i < granadas.Length; i++)
        {
            AtualizarIcone(i);
            if (granadas[i] == null) continue;
            if (!Apertou(i)) continue;
            if (Time.time < prontaEm[i]) continue;
            if (anim != null && anim.Arremessando) continue;
            Arremessar(i);
        }
    }

    private void Arremessar(int i)
    {
        GranadaData f = granadas[i];

        // cartas de granada entram aqui
        var mods = GranadaMods.Neutro;
        float recarga = f.recarga;
        if (inv != null)
        {
            mods.pavio = 1f - Mathf.Min(0.7f, inv.Valor(UpgradeKind.GranadaPavioPercent) / 100f);
            mods.raio  = 1f + inv.Valor(UpgradeKind.GranadaRaioPercent) / 100f;
            mods.dano  = 1f + inv.Valor(UpgradeKind.GranadaDanoPercent) / 100f;
            mods.fogo  = 1f + inv.Valor(UpgradeKind.GranadaFogoPercent) / 100f;
            recarga    = f.recarga / (1f + inv.Valor(UpgradeKind.GranadaRecargaPercent) / 100f);
        }

        // A recarga conta a partir do APERTO, nao do momento em que a granada
        // sai da mao. Se contasse do arremesso, o gesto viraria uma janela de
        // meio segundo pra martelar a tecla.
        recargaAplicada[i] = recarga;
        prontaEm[i] = Time.time + recarga;

        // Sem animacao (boneco sem Animator, camada faltando), volta ao
        // comportamento antigo: a granada sai no mesmo quadro.
        if (anim == null || !anim.PlayGranada())
        {
            Soltar(f, mods);
            return;
        }
        PorNaMao(f);
        StartCoroutine(SoltarNoQuadroCerto(f, mods));
    }

    /// <summary>
    /// A granada so deixa a mao no quadro do arremesso, como em Call of Duty,
    /// Gears of War e Battlefield: o objeto nasce quando o braco solta, nao
    /// quando a tecla e apertada. A mira e lida NA HORA DE SOLTAR, entao girar
    /// a camera durante o gesto ainda corrige o alvo.
    /// </summary>
    private System.Collections.IEnumerator SoltarNoQuadroCerto(GranadaData f, GranadaMods mods)
    {
        yield return new WaitForSeconds(AnimacaoJogador.TempoAteSoltar);
        if (!isActiveAndEnabled) yield break;
        Soltar(f, mods);
    }

    private void Soltar(GranadaData f, GranadaMods mods)
    {
        TirarDaMao();
        Ray raio = mira != null ? mira.GetAimRay() : new Ray(transform.position + Vector3.up * 1.5f, transform.forward);

        // De onde a granada nasce: a MAO ESQUERDA do boneco, que ja esta no
        // lugar certo justamente porque esperamos o quadro do arremesso - no
        // pico da chicotada ela esta a 1,59 m do chao e 0,37 m a frente, bem
        // acima do ombro. Um empurrao de 18 cm na direcao da mira garante que
        // ela nao encoste na cabeca. Sem boneco, cai no deslocamento antigo
        // relativo a camera.
        Vector3 origem;
        Transform mao = anim != null ? anim.MaoDaGranada : null;
        if (mao != null)
        {
            origem = mao.position + raio.direction.normalized * 0.18f;
        }
        else
        {
            origem = raio.origin
                   + raio.direction * deslocamentoMao.z
                   + Vector3.Cross(Vector3.up, raio.direction).normalized * -deslocamentoMao.x
                   + Vector3.up * deslocamentoMao.y;
        }

        Vector3 vDono = cc != null ? cc.velocity : Vector3.zero;
        Granada.Lancar(f, origem, raio.direction, vDono, mods);
    }

    /// <summary>
    /// Poe o modelo da granada na mao durante o gesto. Sem isso o boneco faz
    /// o movimento com a mao vazia e a granada aparece do nada no ar - e o
    /// mesmo detalhe que CoD e Battlefield resolvem com a granada presa a mao
    /// ate o quadro do arremesso.
    /// </summary>
    private void PorNaMao(GranadaData f)
    {
        TirarDaMao();
        if (f == null || f.modelo == null) return;
        Transform mao = anim != null ? anim.MaoDaGranada : null;
        if (mao == null) return;

        naMao = Instantiate(f.modelo, mao);
        naMao.transform.localPosition = posicaoNaMao;
        naMao.transform.localEulerAngles = rotacaoNaMao;
        naMao.transform.localScale = Vector3.one * Mathf.Max(0.01f, f.escalaModelo) * Mathf.Max(0.01f, escalaNaMao);
        foreach (var c in naMao.GetComponentsInChildren<Collider>(true)) Destroy(c);
    }

    private void TirarDaMao()
    {
        if (naMao != null) { Destroy(naMao); naMao = null; }
    }

    private void OnDisable()
    {
        TirarDaMao();
    }

    /// <summary>REPOSICAO TATICA: cada abate tira segundos da recarga de TODAS as granadas.</summary>
    public void ReduzirRecarga(float segundos)
    {
        if (prontaEm == null) return;
        for (int i = 0; i < prontaEm.Length; i++)
            if (prontaEm[i] > Time.time) prontaEm[i] -= segundos;
    }

    // ---------------- HUD ----------------

    private void MontarHud(int n)
    {
        var canvas = UIKit.NovoCanvas(transform, "Granadas_Canvas", 57);
        iconeFundo = new Image[n];
        iconePreenche = new Image[n];
        iconeTexto = new TextMeshProUGUI[n];

        for (int i = 0; i < n; i++)
        {
            var fundo = UIKit.PainelBordado(canvas.transform, "G" + i, UIKit.Painel, 10);
            UIKit.Por(fundo, new Vector2(1f, 0f), new Vector2(1f, 0f),
                      new Vector2(-24f - (n - 1 - i) * 84f, 96f), new Vector2(74f, 74f));
            var d = fundo.transform.GetChild(0);

            // preenchimento circular = quanto falta da recarga
            var cheio = UIKit.Caixa(d, "Cheio", granadas[i] != null ? granadas[i].cor : UIKit.Destaque, 8);
            UIKit.Esticar(cheio);
            cheio.type = Image.Type.Filled;
            cheio.fillMethod = Image.FillMethod.Radial360;
            cheio.fillOrigin = 2;
            cheio.fillClockwise = false;
            cheio.color = new Color(cheio.color.r, cheio.color.g, cheio.color.b, 0.28f);
            iconePreenche[i] = cheio;
            iconeFundo[i] = fundo;

            var tecla = UIKit.Texto3(d, "T", i == 0 ? "G" : "H", 22f, TextAlignmentOptions.Center, UIKit.Texto, true);
            UIKit.Por(tecla, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(60f, 26f));

            var txt = UIKit.Texto3(d, "V", "PRONTA", 12f, TextAlignmentOptions.Center,
                                   granadas[i] != null ? granadas[i].cor : UIKit.Destaque, true);
            UIKit.Por(txt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(70f, 18f));
            iconeTexto[i] = txt;
        }
    }

    private void AtualizarIcone(int i)
    {
        if (iconeTexto == null || i >= iconeTexto.Length || iconeTexto[i] == null) return;
        float falta = prontaEm[i] - Time.time;
        if (falta <= 0f)
        {
            iconeTexto[i].text = "PRONTA";
            iconePreenche[i].fillAmount = 1f;
            var c = iconePreenche[i].color; c.a = 0.30f; iconePreenche[i].color = c;
        }
        else
        {
            iconeTexto[i].text = Mathf.CeilToInt(falta) + "s";
            float total = recargaAplicada != null && i < recargaAplicada.Length ? recargaAplicada[i] : 60f;
            iconePreenche[i].fillAmount = 1f - falta / total;
            var c = iconePreenche[i].color; c.a = 0.14f; iconePreenche[i].color = c;
        }
    }
}
