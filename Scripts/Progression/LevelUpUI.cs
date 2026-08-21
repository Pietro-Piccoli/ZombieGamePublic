using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Upou -> pausa e mostra 3 cartas. Clicou, aplica e volta.
/// Canvas montado em codigo com o estilo do UIKit.
/// Se upar varias vezes seguidas, faz fila e mostra uma escolha por vez.
/// </summary>
[RequireComponent(typeof(PlayerProgression))]
[RequireComponent(typeof(UpgradeInventory))]
public class LevelUpUI : MonoBehaviour
{
    [Header("Cartas")]
    [SerializeField] private int opcoes = 3;
    [SerializeField] private int larguraCarta = 300;
    [SerializeField] private int alturaCarta = 372;

    private PlayerProgression prog;
    private UpgradeInventory inv;
    private GameObject canvasGo;
    private int escolhasPendentes;
    private bool aberto;

    private void Awake()
    {
        prog = GetComponent<PlayerProgression>();
        inv = GetComponent<UpgradeInventory>();
        prog.OnLevelUp += _ => { escolhasPendentes++; };
    }

    private void Update()
    {
        if (!aberto && escolhasPendentes > 0) Abrir();
    }

    /// <summary>COMUM / INCOMUM / RARO deduzido do peso no sorteio.</summary>
    private static string Raridade(UpgradeData u, out Color cor)
    {
        if (u.weight >= 9f)  { cor = new Color(0.60f, 0.66f, 0.75f); return "COMUM"; }
        if (u.weight >= 6f)  { cor = new Color(0.36f, 0.70f, 1.00f); return "INCOMUM"; }
        cor = new Color(0.85f, 0.45f, 1.00f); return "RARO";
    }

    private void Abrir()
    {
        List<UpgradeData> sorteados = inv.Sortear(opcoes);
        if (sorteados.Count == 0) { escolhasPendentes = 0; return; }

        aberto = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var canvas = UIKit.NovoCanvas(null, "LevelUp_Canvas", 90);
        canvasGo = canvas.gameObject;
        canvasGo.AddComponent<GraphicRaycaster>();

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        var fundo = UIKit.Caixa(canvasGo.transform, "Escurecer", new Color(0.01f, 0.012f, 0.02f, 0.86f), 1);
        UIKit.Esticar(fundo);

        var titulo = UIKit.Texto3(canvasGo.transform, "Titulo", "NÍVEL " + prog.Nivel, 52f, TextAlignmentOptions.Center, UIKit.Texto, true);
        UIKit.Por(titulo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(1200f, 62f));
        titulo.characterSpacing = 8f;

        var sub = UIKit.Texto3(canvasGo.transform, "Sub", "ESCOLHA UMA CARTA", 20f, TextAlignmentOptions.Center, UIKit.TextoFraco, true);
        UIKit.Por(sub, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -166f), new Vector2(1200f, 28f));
        sub.characterSpacing = 10f;

        float passo = larguraCarta + 36f;
        float x0 = -passo * (sorteados.Count - 1) / 2f;
        for (int i = 0; i < sorteados.Count; i++)
            MontarCarta(sorteados[i], new Vector2(x0 + passo * i, -30f));
    }

    private void MontarCarta(UpgradeData u, Vector2 pos)
    {
        Color corRar;
        string rar = Raridade(u, out corRar);

        var carta = UIKit.PainelBordado(canvasGo.transform, "Carta_" + u.name, UIKit.PainelForte, 16);
        UIKit.Por(carta, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(larguraCarta, alturaCarta));
        carta.raycastTarget = true;

        var conteudo = carta.transform.GetChild(0);   // o Fundo do PainelBordado

        // faixa de raridade no topo
        var faixa = UIKit.Caixa(conteudo, "Faixa", u.cardColor, 14);
        var frt = UIKit.Por(faixa, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -0f), new Vector2(larguraCarta - 4f, 76f));

        var lblRar = UIKit.Texto3(faixa.transform, "Raridade", rar, 15f, TextAlignmentOptions.Center, corRar, true);
        UIKit.Por(lblRar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(larguraCarta - 40f, 22f));
        lblRar.characterSpacing = 8f;

        var lblNome = UIKit.Texto3(faixa.transform, "Nome", u.displayName, 25f, TextAlignmentOptions.Center, UIKit.Texto, true);
        UIKit.Por(lblNome, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(larguraCarta - 36f, 40f));
        lblNome.textWrappingMode = TextWrappingModes.Normal;
        lblNome.enableAutoSizing = true;
        lblNome.fontSizeMin = 14f; lblNome.fontSizeMax = 23f;

        // efeito
        var lblDesc = UIKit.Texto3(conteudo, "Efeito", u.DescricaoFormatada(), 20f, TextAlignmentOptions.Center, UIKit.Texto, false);
        UIKit.Por(lblDesc, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(larguraCarta - 44f, 104f));
        lblDesc.textWrappingMode = TextWrappingModes.Normal;

        // sabor
        if (!string.IsNullOrEmpty(u.flavor))
        {
            var lblFlavor = UIKit.Texto3(conteudo, "Sabor", "<i>" + u.flavor + "</i>", 15f, TextAlignmentOptions.Center, new Color(0.55f, 0.59f, 0.67f), false);
            UIKit.Por(lblFlavor, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(larguraCarta - 48f, 76f));
            lblFlavor.textWrappingMode = TextWrappingModes.Normal;
        }

        // pilha
        int stack = inv.StacksDe(u);
        var pilha = UIKit.Caixa(conteudo, "Pilha", new Color(1f, 1f, 1f, 0.07f), 10);
        UIKit.Por(pilha, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(84f, 30f));
        var lblPilha = UIKit.Texto3(pilha.transform, "PilhaTxt", stack + " / " + u.maxStacks, 16f, TextAlignmentOptions.Center, UIKit.TextoFraco, true);
        UIKit.Esticar(lblPilha);

        // clique + hover
        var btn = carta.gameObject.AddComponent<Button>();
        btn.targetGraphic = carta;
        var cores = btn.colors;
        cores.normalColor = Color.white;
        cores.highlightedColor = new Color(2.6f, 2.6f, 2.6f, 1f);
        cores.pressedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
        cores.fadeDuration = 0.08f;
        btn.colors = cores;
        UpgradeData escolhido = u;
        btn.onClick.AddListener(() => Escolher(escolhido));

        var hover = carta.gameObject.AddComponent<CartaHover>();
        hover.alvo = carta.rectTransform;
    }

    private void Escolher(UpgradeData u)
    {
        if (!aberto) return;
        aberto = false;
        inv.Aplicar(u);
        escolhasPendentes = Mathf.Max(0, escolhasPendentes - 1);
        Fechar();
    }

    private void Fechar()
    {
        if (canvasGo != null) Destroy(canvasGo);
        canvasGo = null;
        if (escolhasPendentes <= 0)
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}

/// <summary>Levanta e cresce um tico a carta sob o mouse. Roda em tempo nao escalado (o jogo esta pausado).</summary>
public class CartaHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform alvo;
    private bool dentro;
    private Vector2 posBase;
    private bool guardou;

    public void OnPointerEnter(PointerEventData e) { dentro = true; }
    public void OnPointerExit(PointerEventData e) { dentro = false; }

    private void Update()
    {
        if (alvo == null) return;
        if (!guardou) { posBase = alvo.anchoredPosition; guardou = true; }
        float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        float escalaAlvo = dentro ? 1.05f : 1f;
        float subidaAlvo = dentro ? 14f : 0f;
        alvo.localScale = Vector3.Lerp(alvo.localScale, Vector3.one * escalaAlvo, k);
        alvo.anchoredPosition = Vector2.Lerp(alvo.anchoredPosition, posBase + new Vector2(0f, subidaAlvo), k);
    }
}
