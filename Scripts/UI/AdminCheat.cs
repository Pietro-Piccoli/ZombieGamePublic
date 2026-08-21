using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// PAINEL DE ADMIN / CHEATS - aperta SHIFT+C pra abrir e fechar.
///
/// Pausa o jogo enquanto esta aberto (mexer em buff com zumbi mordendo nao da).
/// Secoes:
///   BANCO   - dinheiro do meta, liberar/resetar cartas
///   PARTIDA - dinheiro da run, level up, vida, god, matar horda, granadas
///   CARTAS  - lista com TODAS as cartas: bota e tira pilha ao vivo
///
/// AVISO: remover antes de qualquer build publica - ou trocar o F1 por um
/// atalho secreto. Por enquanto e ferramenta de teste do Pietro.
/// </summary>
public class AdminCheat : MonoBehaviour
{
    public static bool Aberto { get; private set; }

    private GameObject painel;
    private bool god;
    private Transform listaCartas;
    private TextMeshProUGUI txtGod;
    private float pausaAnterior = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Registrar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AoCarregar;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AoCarregar;
        Nascer();
    }

    private static void AoCarregar(UnityEngine.SceneManagement.Scene c, UnityEngine.SceneManagement.LoadSceneMode m) { Nascer(); }

    private static void Nascer()
    {
        if (Object.FindAnyObjectByType<AdminCheat>() != null) return;
        var go = new GameObject("AdminCheat");
        go.AddComponent<AdminCheat>();
    }

    private static bool AtalhoApertado()
    {
#if ENABLE_INPUT_SYSTEM
        var k = UnityEngine.InputSystem.Keyboard.current;
        return k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed) && k.cKey.wasPressedThisFrame;
#else
        return (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.C);
#endif
    }

    private void Update()
    {
        if (AtalhoApertado())
        {
            if (Aberto) Fechar(); else Abrir();
        }

        // god: mantem a vida cheia todo frame (efeito de imortal sem mexer no filtro de dano)
        if (god)
        {
            var pl = GameObject.FindGameObjectWithTag("Player");
            var vida = pl != null ? pl.GetComponent<Health>() : null;
            if (vida != null && !vida.IsDead) vida.Heal(9999);
        }
    }

    // ---------------- abrir / fechar ----------------

    private void Abrir()
    {
        Aberto = true;
        pausaAnterior = Time.timeScale;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Montar();
    }

    private void Fechar()
    {
        Aberto = false;
        if (painel != null) Destroy(painel);
        painel = null;
        // so devolve o jogo se nenhum menu estiver aberto
        if (!MenuPrincipal.Aberto && !MenuPausa.Pausado)
        {
            Time.timeScale = pausaAnterior <= 0f ? 1f : pausaAnterior;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ---------------- montagem ----------------

    private void Montar()
    {
        var canvas = UIKit.NovoCanvas(null, "Admin_Canvas", 300);
        painel = canvas.gameObject;
        painel.AddComponent<GraphicRaycaster>();
        MenuPrincipal.GarantirEventSystem();

        var caixa = UIKit.PainelBordado(painel.transform, "Caixa", UIKit.PainelForte, 14);
        var rt = (RectTransform)caixa.transform;
        rt.anchorMin = new Vector2(1f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-16f, 0f);
        rt.sizeDelta = new Vector2(600f, 1000f);
        var d = caixa.transform.GetChild(0);

        var tit = UIKit.Texto3(d, "T", "ADMIN  ·  SHIFT+C fecha", 20f, TextAlignmentOptions.Center, UIKit.Destaque, true);
        UIKit.Por(tit, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(560f, 26f));
        tit.characterSpacing = 6f;

        float y = -52f;

        // ---------- BANCO ----------
        y = Secao(d, "BANCO (entre partidas)", y);
        y = LinhaBotoes(d, y, new string[] { "+ $10.000", "LIBERAR TODAS AS CARTAS" },
            new System.Action[] {
                () => { MetaProgressao.Depositar(10000); Refrescar(); },
                () => { MetaProgressao.LiberarTodasCartas(); Refrescar(); }
            });
        y = LinhaBotoes(d, y, new string[] { "RESETAR CARTAS", "RESETAR TUDO (banco+cartas)" },
            new System.Action[] {
                () => { MetaProgressao.ResetarCartas(); Refrescar(); },
                () => { MetaProgressao.ApagarTudo(); Refrescar(); }
            });

        // ---------- PARTIDA ----------
        y = Secao(d, "PARTIDA (agora)", y);
        y = LinhaBotoes(d, y, new string[] { "+ $1.000 NA RUN", "SUBIR DE NÍVEL" },
            new System.Action[] { () => DinheiroRun(1000), SubirNivel });
        y = LinhaBotoes(d, y, new string[] { "VIDA CHEIA", "GOD: OFF" },
            new System.Action[] { VidaCheia, AlternarGod });
        y = LinhaBotoes(d, y, new string[] { "MATAR TODOS OS ZUMBIS", "GRANADAS PRONTAS" },
            new System.Action[] { MatarTodos, GranadasProntas });

        // guarda referencia do texto do GOD pra alternar o rotulo
        foreach (var t in painel.GetComponentsInChildren<TextMeshProUGUI>())
            if (t.text.StartsWith("GOD:")) txtGod = t;
        if (txtGod != null) txtGod.text = god ? "GOD: ON" : "GOD: OFF";

        // ---------- CARTAS DA RUN ----------
        y = Secao(d, "BUFFS DA RUN  (põe e tira pilha ao vivo)", y);
        MontarListaCartas(d, y);
    }

    private float Secao(Transform pai, string nome, float y)
    {
        var t = UIKit.Texto3(pai, "S", nome, 13f, TextAlignmentOptions.Left, UIKit.TextoFraco, true);
        UIKit.Por(t, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), new Vector2(540f, 18f));
        t.characterSpacing = 4f;
        return y - 26f;
    }

    private float LinhaBotoes(Transform pai, float y, string[] rotulos, System.Action[] acoes)
    {
        float larg = (560f - 12f * (rotulos.Length - 1)) / rotulos.Length;
        for (int i = 0; i < rotulos.Length; i++)
        {
            var b = BotaoMini(pai, rotulos[i], new Vector2(20f + i * (larg + 12f), y), larg);
            int k = i;
            b.onClick.AddListener(() => acoes[k]());
        }
        return y - 46f;
    }

    private Button BotaoMini(Transform pai, string rotulo, Vector2 pos, float larg)
    {
        var caixa = UIKit.Caixa(pai, "B_" + rotulo, new Color(1f, 1f, 1f, 0.09f), 8);
        caixa.raycastTarget = true;
        var rt = caixa.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(larg, 38f);
        var txt = UIKit.Texto3(caixa.transform, "T", rotulo, 13f, TextAlignmentOptions.Center, UIKit.Texto, true);
        UIKit.Esticar(txt);
        txt.enableAutoSizing = true; txt.fontSizeMin = 9f; txt.fontSizeMax = 13f;
        var b = caixa.gameObject.AddComponent<Button>();
        b.targetGraphic = caixa;
        var c = b.colors; c.highlightedColor = new Color(2f, 2f, 2f, 1f); c.fadeDuration = 0.06f; b.colors = c;
        return b;
    }

    // ---------------- lista de cartas ----------------

    private void MontarListaCartas(Transform pai, float yTopo)
    {
        // viewport com mascara + conteudo alto + ScrollRect (roda do mouse)
        var vpGo = new GameObject("VP", typeof(RectTransform));
        vpGo.transform.SetParent(pai, false);
        var vp = (RectTransform)vpGo.transform;
        vp.anchorMin = new Vector2(0f, 0f); vp.anchorMax = new Vector2(1f, 1f);
        vp.offsetMin = new Vector2(14f, 14f);
        vp.offsetMax = new Vector2(-14f, yTopo);
        vpGo.AddComponent<RectMask2D>();
        var fundo = vpGo.AddComponent<Image>();
        fundo.color = new Color(0f, 0f, 0f, 0.3f);

        var contGo = new GameObject("Conteudo", typeof(RectTransform));
        contGo.transform.SetParent(vp, false);
        var cont = (RectTransform)contGo.transform;
        cont.anchorMin = new Vector2(0f, 1f); cont.anchorMax = new Vector2(1f, 1f);
        cont.pivot = new Vector2(0.5f, 1f);
        listaCartas = cont;

        var scroll = vpGo.AddComponent<ScrollRect>();
        scroll.content = cont;
        scroll.viewport = vp;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        PreencherLista();
    }

    private void PreencherLista()
    {
        for (int i = listaCartas.childCount - 1; i >= 0; i--) Destroy(listaCartas.GetChild(i).gameObject);

        var pl = GameObject.FindGameObjectWithTag("Player");
        var inv = pl != null ? pl.GetComponent<UpgradeInventory>() : null;
        if (inv == null)
        {
            var aviso = UIKit.Texto3(listaCartas, "A", "(sem player na cena)", 14f, TextAlignmentOptions.Center, UIKit.Perigo, true);
            UIKit.Por(aviso, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(400f, 20f));
            return;
        }

        var todas = new List<UpgradeData>(Resources.LoadAll<UpgradeData>("Upgrades"));
        todas.Sort((a, b) => a.displayName.CompareTo(b.displayName));

        const float RowH = 36f;
        ((RectTransform)listaCartas).sizeDelta = new Vector2(0f, todas.Count * RowH + 8f);

        for (int i = 0; i < todas.Count; i++)
        {
            var u = todas[i];
            float y = -4f - i * RowH;
            int pilhas = inv.StacksDe(u);

            var fundo = UIKit.Caixa(listaCartas, "R" + i, new Color(1f, 1f, 1f, i % 2 == 0 ? 0.03f : 0.06f), 4);
            var rrt = fundo.rectTransform;
            rrt.anchorMin = new Vector2(0f, 1f); rrt.anchorMax = new Vector2(1f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.anchoredPosition = new Vector2(0f, y);
            rrt.sizeDelta = new Vector2(-8f, RowH - 3f);

            var nome = UIKit.Texto3(fundo.transform, "N", u.displayName, 13f, TextAlignmentOptions.Left,
                                    pilhas > 0 ? UIKit.Destaque : UIKit.Texto, true);
            UIKit.Por(nome, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(330f, 20f));
            nome.enableAutoSizing = true; nome.fontSizeMin = 9f; nome.fontSizeMax = 13f;

            var qt = UIKit.Texto3(fundo.transform, "Q", pilhas + " / " + u.maxStacks, 13f, TextAlignmentOptions.Center,
                                  pilhas > 0 ? UIKit.Destaque : UIKit.TextoFraco, true);
            UIKit.Por(qt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-96f, 0f), new Vector2(60f, 20f));

            UpgradeData cap = u;
            var menos = BotaoMini(fundo.transform, "–", Vector2.zero, 36f);
            var mrt = (RectTransform)menos.transform;
            mrt.anchorMin = new Vector2(1f, 0.5f); mrt.anchorMax = new Vector2(1f, 0.5f); mrt.pivot = new Vector2(1f, 0.5f);
            mrt.anchoredPosition = new Vector2(-48f, 0f); mrt.sizeDelta = new Vector2(36f, 28f);
            menos.onClick.AddListener(() => { inv.RemoverPilha(cap); PreencherLista(); });

            var mais = BotaoMini(fundo.transform, "+", Vector2.zero, 36f);
            var prt = (RectTransform)mais.transform;
            prt.anchorMin = new Vector2(1f, 0.5f); prt.anchorMax = new Vector2(1f, 0.5f); prt.pivot = new Vector2(1f, 0.5f);
            prt.anchoredPosition = new Vector2(-8f, 0f); prt.sizeDelta = new Vector2(36f, 28f);
            mais.onClick.AddListener(() => { if (inv.StacksDe(cap) < cap.maxStacks) inv.Aplicar(cap); PreencherLista(); });
        }
    }

    private void Refrescar()
    {
        // banco pode ter mudado; o menu principal mostra sozinho quando reabrir
        if (painel != null) { Destroy(painel); Montar(); }
    }

    // ---------------- acoes da partida ----------------

    private void DinheiroRun(int quanto)
    {
        var pl = GameObject.FindGameObjectWithTag("Player");
        var prog = pl != null ? pl.GetComponent<PlayerProgression>() : null;
        if (prog != null) prog.AddDinheiro(quanto);
    }

    private void SubirNivel()
    {
        var pl = GameObject.FindGameObjectWithTag("Player");
        var prog = pl != null ? pl.GetComponent<PlayerProgression>() : null;
        if (prog == null) return;
        int alvo = prog.Nivel;
        int guarda = 0;
        while (prog.Nivel == alvo && guarda++ < 2000) prog.AddXp(25);
    }

    private void VidaCheia()
    {
        var pl = GameObject.FindGameObjectWithTag("Player");
        var vida = pl != null ? pl.GetComponent<Health>() : null;
        if (vida != null) vida.Heal(99999);
    }

    private void AlternarGod()
    {
        god = !god;
        if (txtGod != null) txtGod.text = god ? "GOD: ON" : "GOD: OFF";
    }

    private void MatarTodos()
    {
        var pl = GameObject.FindGameObjectWithTag("Player");
        foreach (var h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
        {
            if (pl != null && h.gameObject == pl) continue;
            if (h.GetComponent<ZombieAI>() == null || h.IsDead) continue;
            h.TakeDamage(999999, Vector3.up);
        }
    }

    private void GranadasProntas()
    {
        var pl = GameObject.FindGameObjectWithTag("Player");
        var lg = pl != null ? pl.GetComponent<LancadorGranadas>() : null;
        if (lg != null) lg.ReduzirRecarga(99999f);
    }
}
