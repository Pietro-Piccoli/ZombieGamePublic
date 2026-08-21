using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// TELA INICIAL.
///
/// Estrutura de referencia: menu moderno de shooter - fundo da propria cena,
/// navegacao vertical a esquerda, conteudo a direita. A ARMARIA segue o
/// GUNSMITH do Call of Duty: lista de slots, modelo 3D da arma no centro
/// girando, e as pecas do slot escolhido a direita, com o efeito de cada uma.
/// Comprou, equipou, o modelo atualiza na hora.
/// </summary>
public class MenuPrincipal : MonoBehaviour
{
    public static bool Aberto { get; private set; }
    public static bool AbrirNoProximoCarregamento = true;

    private GameObject canvasGo;
    private Transform conteudo;
    private TextMeshProUGUI txtBanco;
    private PreviewArma preview;
    private SlotAttach slotAtivo = SlotAttach.Mira;
    // SELECAO: a peca que o jogador esta olhando. Nao muda nada de verdade
    // ate ele apertar COMPRAR ou EQUIPAR.
    private AnexoArma selecionado;
    private bool selecaoAtiva;
    private int abaAtiva = 0;
    private readonly List<Button> abas = new List<Button>();

    // ---------------- nascimento ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Registrar()
    {
        // RuntimeInitializeOnLoadMethod so roda UMA vez, quando o runtime sobe.
        // Recarregar a cena NAO dispara de novo - era por isso que "MENU PRINCIPAL"
        // se comportava igual a "REINICIAR". Entao aqui eu assino o evento de
        // cena carregada, que dispara sempre.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AoCarregarCena;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AoCarregarCena;
        Nascer();
    }

    private static void AoCarregarCena(UnityEngine.SceneManagement.Scene cena,
                                       UnityEngine.SceneManagement.LoadSceneMode modo)
    {
        Nascer();
    }

    private static void Nascer()
    {
        if (!AbrirNoProximoCarregamento) { AbrirNoProximoCarregamento = true; return; }
        if (Object.FindAnyObjectByType<MenuPrincipal>() != null) return;   // ja tem um
        var go = new GameObject("MenuPrincipal");
        go.AddComponent<MenuPrincipal>();
    }

    public static void GarantirEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    private void Start() { Abrir(); }

    private void Update()
    {
        if (Aberto && Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // ---------------- montagem ----------------

    private void Abrir()
    {
        Aberto = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var canvas = UIKit.NovoCanvas(null, "Menu_Canvas", 150);
        canvasGo = canvas.gameObject;
        canvasGo.AddComponent<GraphicRaycaster>();
        GarantirEventSystem();

        // fundo: escurece a cena e mantem ela viva atras (nao e tela chapada)
        var veu = UIKit.Caixa(canvasGo.transform, "Veu", new Color(0.015f, 0.02f, 0.035f, 0.90f), 1);
        UIKit.Esticar(veu); veu.raycastTarget = true;

        // faixa lateral escura pra ancorar a navegacao
        var faixa = UIKit.Caixa(canvasGo.transform, "Faixa", new Color(0f, 0f, 0f, 0.42f), 1);
        var frt = (RectTransform)faixa.transform;
        frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(0f, 1f);
        frt.pivot = new Vector2(0f, 0.5f);
        frt.anchoredPosition = Vector2.zero; frt.sizeDelta = new Vector2(430f, 0f);

        // ---- titulo ----
        var tit = UIKit.Texto3(canvasGo.transform, "Titulo", "IMPRÓPRIO", 68f, TextAlignmentOptions.Left, UIKit.Texto, true);
        UIKit.Por(tit, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -74f), new Vector2(420f, 76f));
        tit.characterSpacing = 6f;

        var tit2 = UIKit.Texto3(canvasGo.transform, "Titulo2", "PARA CONSUMO", 30f, TextAlignmentOptions.Left, UIKit.Destaque, true);
        UIKit.Por(tit2, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(58f, -140f), new Vector2(420f, 36f));
        tit2.characterSpacing = 12f;

        // ---- banco (canto superior direito) ----
        var chip = UIKit.PainelBordado(canvasGo.transform, "Banco", UIKit.Painel, UIKit.RaioPainel);
        UIKit.Por(chip, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -34f), new Vector2(230f, 58f));
        txtBanco = UIKit.Texto3(chip.transform, "BancoTxt", "", 28f, TextAlignmentOptions.Center, UIKit.Destaque, true);
        UIKit.Esticar(txtBanco);
        AtualizarBanco();

        // ---- navegacao ----
        string[] nomes = new string[] { "JOGAR", "ARMARIA", "CARTAS", "RECORDES", "OPÇÕES", "SAIR" };
        abas.Clear();
        for (int i = 0; i < nomes.Length; i++)
        {
            int k = i;
            var b = Botao(canvasGo.transform, nomes[i], Vector2.zero, 320f, 60f, i == 5 ? UIKit.Perigo : UIKit.Texto);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(56f, -240f - i * 74f);
            b.onClick.AddListener(() => Aba(k));
            abas.Add(b);
        }

        var rodape = UIKit.Texto3(canvasGo.transform, "Rodape", "ESC pausa durante a partida", 15f, TextAlignmentOptions.Left, UIKit.TextoFraco, false);
        UIKit.Por(rodape, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(58f, 34f), new Vector2(380f, 22f));

        // ---- area de conteudo ----
        var area = new GameObject("Conteudo", typeof(RectTransform));
        area.transform.SetParent(canvasGo.transform, false);
        var art = (RectTransform)area.transform;
        art.anchorMin = new Vector2(0f, 0f); art.anchorMax = new Vector2(1f, 1f);
        art.offsetMin = new Vector2(470f, 40f); art.offsetMax = new Vector2(-40f, -110f);
        conteudo = area.transform;

        // aba padrao = ARMARIA. NAO pode ser 0: a 0 e JOGAR, que fecha o menu.
        Aba(1);
        EsconderHudDoJogo(true);
    }

    private void AtualizarBanco()
    {
        if (txtBanco != null)
            txtBanco.text = "$ " + MetaProgressao.Dinheiro.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
    }

    private void LimparConteudo()
    {
        for (int i = conteudo.childCount - 1; i >= 0; i--) Destroy(conteudo.GetChild(i).gameObject);
    }

    private void Aba(int i)
    {
        if (i == 0) { Jogar(); return; }
        if (i == 5) { MenuPausa.Sair(); return; }   // SAIR agora e a ultima aba

        abaAtiva = i;
        for (int k = 0; k < abas.Count; k++)
        {
            var img = abas[k].GetComponent<Image>();
            if (img != null) img.color = (k == i) ? new Color(1f, 1f, 1f, 0.16f) : UIKit.Borda;
        }

        LimparConteudo();
        if (i == 1) MontarArmaria();
        else if (i == 2) MontarCartas();
        else if (i == 3) MontarRecordes();
        else if (i == 4) PainelOpcoes.Montar(conteudo, null);
    }

    private void Jogar()
    {
        Aberto = false;
        EsconderHudDoJogo(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (preview != null) Destroy(preview.gameObject);
        if (canvasGo != null) Destroy(canvasGo);
        Destroy(gameObject);
    }

    // ---------------- botao padrao ----------------

    public static Button Botao(Transform pai, string rotulo, Vector2 pos, float largura, float altura, Color cor)
    {
        var caixa = UIKit.Caixa(pai, "Btn_" + rotulo, UIKit.Borda, 10);
        caixa.raycastTarget = true;
        var rt = caixa.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(largura, altura);

        var txt = UIKit.Texto3(caixa.transform, "Txt", rotulo, 21f, TextAlignmentOptions.Center, cor, true);
        UIKit.Esticar(txt);
        txt.characterSpacing = 6f;

        var b = caixa.gameObject.AddComponent<Button>();
        b.targetGraphic = caixa;
        var c = b.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(2.4f, 2.4f, 2.4f, 1f);
        c.pressedColor = new Color(1.5f, 1.5f, 1.5f, 1f);
        c.fadeDuration = 0.08f;
        b.colors = c;
        return b;
    }

    // ---------------- ARMARIA (padrao Gunsmith) ----------------
    //
    // Agora a armaria tem duas camadas. Em cima, a FAIXA DE ARMAS: o jogador
    // escolhe com o que vai jogar a run, como se escolhe personagem em Risk of
    // Rain 2 - uma decisao por partida, sem troca no meio. Embaixo, a bancada
    // de sempre: slots a esquerda, modelo no meio, pecas a direita.
    //
    // Cada arma guarda a PROPRIA montagem, e uma peca so aparece na lista se
    // ela serve naquela arma. Trilho de escopeta nao encaixa em fuzil.

    /// <summary>A arma que o jogador esta OLHANDO. Pode ser uma que ele ainda nao comprou.</summary>
    private WeaponData armaVista;

    private WeaponData ArmaDaTela
    {
        get
        {
            var todas = MetaProgressao.TodasAsArmas();
            if (armaVista != null)
                for (int i = 0; i < todas.Length; i++)
                    if (todas[i] == armaVista) return armaVista;
            armaVista = MetaProgressao.ArmaSelecionada;
            if (armaVista == null && todas.Length > 0) armaVista = todas[0];
            return armaVista;
        }
    }

    private void MontarArmaria()
    {
        WeaponData arma = ArmaDaTela;
        bool temAArma = arma != null && MetaProgressao.ArmaComprada(arma);
        const float Topo = -104f;   // altura reservada pra faixa de armas

        MontarFaixaDeArmas(arma);

        // o slot em foco precisa existir NESTA arma
        SlotAttach[] slots = arma != null ? arma.SlotsDaArma : new SlotAttach[0];
        bool temSlot = false;
        for (int i = 0; i < slots.Length; i++) if (slots[i] == slotAtivo) temSlot = true;
        if (!temSlot) { slotAtivo = slots.Length > 0 ? slots[0] : SlotAttach.Mira; selecaoAtiva = false; selecionado = null; }

        // ---- coluna 1: slots ----
        var colSlots = UIKit.PainelBordado(conteudo, "ColSlots", UIKit.Painel, UIKit.RaioPainel);
        var srt = (RectTransform)colSlots.transform;
        srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(0f, 1f);
        srt.pivot = new Vector2(0f, 1f);
        srt.anchoredPosition = new Vector2(0f, Topo); srt.sizeDelta = new Vector2(300f, Topo);
        var dSlots = colSlots.transform.GetChild(0);

        var tSlots = UIKit.Texto3(dSlots, "T", "PONTOS DE MONTAGEM", 15f, TextAlignmentOptions.Center, UIKit.TextoFraco, true);
        UIKit.Por(tSlots, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(270f, 22f));
        tSlots.characterSpacing = 5f;

        if (slots.Length == 0)
        {
            var nada = UIKit.Texto3(dSlots, "Nada", "Esta arma não aceita anexo.\nÉ um tubo com um foguete dentro.",
                                    14f, TextAlignmentOptions.Center, UIKit.TextoFraco, false);
            UIKit.Por(nada, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(260f, 60f));
            nada.textWrappingMode = TextWrappingModes.Normal;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            SlotAttach s = slots[i];
            AnexoArma eq = arma != null ? MetaProgressao.ResolverEquipado(arma.Id, s) : null;
            bool ativo = s == slotAtivo;

            var linha = UIKit.Caixa(dSlots, "S" + i, ativo ? new Color(1f, 1f, 1f, 0.14f) : new Color(1f, 1f, 1f, 0.05f), 10);
            UIKit.Por(linha, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f - i * 74f), new Vector2(268f, 66f));
            linha.raycastTarget = true;

            var nome = UIKit.Texto3(linha.transform, "N", AnexoArma.NomeSlot(s), 14f, TextAlignmentOptions.Left, UIKit.TextoFraco, true);
            UIKit.Por(nome, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(230f, 18f));
            nome.characterSpacing = 4f;

            var val = UIKit.Texto3(linha.transform, "V", eq != null ? eq.nomeExibicao : "— vazio —", 18f,
                                   TextAlignmentOptions.Left, eq != null ? UIKit.Destaque : new Color(0.62f, 0.67f, 0.75f), true);
            UIKit.Por(val, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -32f), new Vector2(230f, 24f));

            SlotAttach cap = s;
            var bs = linha.gameObject.AddComponent<Button>();
            bs.targetGraphic = linha;
            var cs = bs.colors; cs.highlightedColor = new Color(2f, 2f, 2f, 1f); cs.fadeDuration = 0.08f; bs.colors = cs;
            bs.onClick.AddListener(() =>
            {
                // trocar de slot limpa a selecao: senao a peca escolhida num
                // slot era simulada no slot novo, e as barras mentiam
                slotAtivo = cap;
                selecaoAtiva = false; selecionado = null;
                if (preview != null) preview.LimparPrevisao();
                Aba(1);
            });
        }

        // ---- coluna 2: preview 3D ----
        var colPrev = UIKit.PainelBordado(conteudo, "ColPrev", UIKit.Painel, UIKit.RaioPainel);
        var prt = (RectTransform)colPrev.transform;
        prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(1f, 1f);
        prt.offsetMin = new Vector2(316f, 0f); prt.offsetMax = new Vector2(-436f, Topo);
        var dPrev = colPrev.transform.GetChild(0);

        if (preview == null && arma != null) preview = PreviewArma.Criar(arma, 900, 640);
        else if (preview != null) { preview.TrocarArma(arma); preview.Remontar(); }

        if (preview != null)
        {
            var img = new GameObject("Preview3D", typeof(RectTransform)).AddComponent<RawImage>();
            img.transform.SetParent(dPrev, false);
            img.texture = preview.Textura;
            img.raycastTarget = false;
            var irt = img.rectTransform;
            irt.anchorMin = new Vector2(0f, 0f); irt.anchorMax = new Vector2(1f, 1f);
            irt.offsetMin = new Vector2(10f, 92f); irt.offsetMax = new Vector2(-10f, -84f);
        }

        var nomeArma = UIKit.Texto3(dPrev, "NomeArma", arma != null ? arma.displayName.ToUpper() : "—", 26f,
                                    TextAlignmentOptions.Center, temAArma ? UIKit.Texto : UIKit.TextoFraco, true);
        UIKit.Por(nomeArma, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(520f, 32f));
        nomeArma.characterSpacing = 8f;

        MontarFichaDaArma(dPrev, arma);
        MontarBarrasDeStatus(dPrev);

        // ---- coluna 3: pecas do slot ----
        var colPecas = UIKit.PainelBordado(conteudo, "ColPecas", UIKit.Painel, UIKit.RaioPainel);
        var crt = (RectTransform)colPecas.transform;
        crt.anchorMin = new Vector2(1f, 0f); crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(0f, Topo); crt.sizeDelta = new Vector2(420f, Topo);
        var dPecas = colPecas.transform.GetChild(0);

        var tPecas = UIKit.Texto3(dPecas, "T", slots.Length > 0 ? AnexoArma.NomeSlot(slotAtivo) : "SEM ANEXOS",
                                  17f, TextAlignmentOptions.Center, UIKit.Destaque, true);
        UIKit.Por(tPecas, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(380f, 24f));
        tPecas.characterSpacing = 6f;

        MontarRodapeAcao(dPecas);

        if (arma == null || slots.Length == 0) return;

        if (!temAArma)
        {
            var aviso = UIKit.Texto3(dPecas, "Aviso", "Compre a arma primeiro.\nDepois ela abre a bancada dela.",
                                     15f, TextAlignmentOptions.Center, UIKit.TextoFraco, false);
            UIKit.Por(aviso, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(340f, 60f));
            aviso.textWrappingMode = TextWrappingModes.Normal;
            return;
        }

        var lista = new List<AnexoArma>();
        foreach (var an in MetaProgressao.TodosOsAnexos())
            if (an != null && an.slot == slotAtivo && an.ServePara(arma.Id)) lista.Add(an);
        lista.Sort((x, y) => x.preco.CompareTo(y.preco));

        // Com 11 opticas na lista nao cabe tudo na tela: a coluna rola.
        Transform rolo = MontarRoloDePecas(dPecas, lista.Count + 1);

        int linhaN = 0;
        MontarLinhaPeca(rolo, null, linhaN++);
        foreach (var an in lista) MontarLinhaPeca(rolo, an, linhaN++);
    }

    /// <summary>
    /// Os numeros DA ARMA (nao dos anexos), numa fita de fichas embaixo do nome.
    /// Sem isto o jogador escolhe entre tres armas no escuro: as barras de baixo
    /// so falam do que os anexos mudam, nunca do que a arma e.
    /// </summary>
    private void MontarFichaDaArma(Transform pai, WeaponData w)
    {
        if (w == null) return;

        string[] rotulos, valores;
        if (w.tipoDisparo == TipoDisparo.Projetil)
        {
            rotulos = new string[] { "DANO DO ESTOURO", "RAIO", "CADÊNCIA", "RECARGA", "ESTILO" };
            valores = new string[] {
                w.explosaoDano.ToString(),
                w.explosaoRaio.ToString("0.0") + " m",
                w.fireRate.ToString("0.0") + "/s",
                w.reloadTime.ToString("0.0") + " s",
                string.IsNullOrEmpty(w.estilo) ? "—" : w.estilo };
        }
        else
        {
            string dano = w.pellets > 1
                ? (w.damage * w.pellets) + "  (" + w.pellets + " × " + w.damage + ")"
                : w.damage.ToString();
            string alcance = w.danoMinimo >= 1f
                ? "longo"
                : w.quedaInicio.ToString("0") + " m";
            rotulos = new string[] { "DANO POR TIRO", "CADÊNCIA", "PENTE", "DANO CHEIO ATÉ", "ESTILO" };
            valores = new string[] {
                dano,
                w.fireRate.ToString("0.0") + "/s",
                w.magazineSize + "  ·  " + w.reloadTime.ToString("0.0") + " s",
                alcance,
                string.IsNullOrEmpty(w.estilo) ? "—" : w.estilo };
        }

        int n = rotulos.Length;
        float larg = 150f, folga = 8f;
        float total = n * larg + (n - 1) * folga;
        for (int i = 0; i < n; i++)
        {
            var chip = UIKit.Caixa(pai, "Ficha" + i, new Color(1f, 1f, 1f, 0.05f), 8);
            UIKit.Por(chip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                      new Vector2(-total * 0.5f + larg * 0.5f + i * (larg + folga), -52f),
                      new Vector2(larg, 40f));

            var lr = UIKit.Texto3(chip.transform, "R", rotulos[i], 10f, TextAlignmentOptions.Center, UIKit.TextoFraco, true);
            UIKit.Por(lr, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(larg - 8f, 13f));
            lr.characterSpacing = 2f;

            var lv = UIKit.Texto3(chip.transform, "V", valores[i], 15f, TextAlignmentOptions.Center, UIKit.Destaque, true);
            UIKit.Por(lv, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(larg - 8f, 20f));
        }

        if (!string.IsNullOrEmpty(w.descricao))
        {
            var d = UIKit.Texto3(pai, "DescArma", w.descricao, 13f, TextAlignmentOptions.Center, UIKit.TextoFraco, false);
            UIKit.Por(d, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 108f), new Vector2(620f, 40f));
            d.textWrappingMode = TextWrappingModes.Normal;
        }
    }

    /// <summary>Area rolavel pras pecas. Devolve o 'conteudo' onde as linhas entram.</summary>
    private Transform MontarRoloDePecas(Transform pai, int quantasLinhas)
    {
        var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        vpGo.transform.SetParent(pai, false);
        var vrt = (RectTransform)vpGo.transform;
        vrt.anchorMin = new Vector2(0f, 0f); vrt.anchorMax = new Vector2(1f, 1f);
        vrt.offsetMin = new Vector2(8f, 150f); vrt.offsetMax = new Vector2(-8f, -40f);
        var vimg = vpGo.GetComponent<Image>();
        vimg.color = new Color(0f, 0f, 0f, 0f); vimg.raycastTarget = true;

        var ctGo = new GameObject("Conteudo", typeof(RectTransform));
        ctGo.transform.SetParent(vpGo.transform, false);
        var crt = (RectTransform)ctGo.transform;
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(0.5f, 1f);
        crt.anchoredPosition = Vector2.zero;
        // 92 = passo de uma linha (84 de caixa + 8 de folga), o mesmo de MontarLinhaPeca
        crt.sizeDelta = new Vector2(0f, Mathf.Max(0f, 20f + quantasLinhas * 92f));

        var sr = vpGo.AddComponent<ScrollRect>();
        sr.content = crt; sr.viewport = vrt;
        sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 34f;
        return ctGo.transform;
    }

    /// <summary>A faixa de armas no topo da armaria.</summary>
    private void MontarFaixaDeArmas(WeaponData arma)
    {
        var todas = MetaProgressao.TodasAsArmas();
        string idEquipada = MetaProgressao.ArmaSelecionadaId;

        float larg = 250f, alt = 84f, folga = 12f;
        for (int i = 0; i < todas.Length; i++)
        {
            WeaponData w = todas[i];
            if (w == null) continue;
            bool comprada = MetaProgressao.ArmaComprada(w);
            bool equipada = comprada && w.Id == idEquipada;
            bool olhando = w == arma;

            Color fundo = olhando ? new Color(0.34f, 0.78f, 1f, 0.18f)
                        : (equipada ? new Color(1f, 0.72f, 0.22f, 0.14f) : new Color(1f, 1f, 1f, 0.05f));
            var caixa = UIKit.Caixa(conteudo, "A" + i, fundo, 10);
            UIKit.Por(caixa, new Vector2(0f, 1f), new Vector2(0f, 1f),
                      new Vector2(i * (larg + folga), 0f), new Vector2(larg, alt));
            caixa.rectTransform.pivot = new Vector2(0f, 1f);
            caixa.raycastTarget = true;

            var nome = UIKit.Texto3(caixa.transform, "N", w.displayName.ToUpper(), 18f, TextAlignmentOptions.Left,
                                    comprada ? UIKit.Texto : new Color(0.55f, 0.59f, 0.66f), true);
            UIKit.Por(nome, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(220f, 22f));
            nome.characterSpacing = 3f;

            var est = UIKit.Texto3(caixa.transform, "E", w.estilo, 12f, TextAlignmentOptions.Left, UIKit.TextoFraco, true);
            UIKit.Por(est, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -34f), new Vector2(140f, 16f));
            est.characterSpacing = 4f;

            string etq = equipada ? "NA MÃO" : (comprada ? "NA MOCHILA" : "$ " + w.preco);
            Color cor = equipada ? UIKit.Destaque
                      : (comprada ? new Color(0.55f, 0.60f, 0.68f)
                      : (MetaProgressao.Dinheiro >= w.preco ? UIKit.Destaque : UIKit.Perigo));
            var e2 = UIKit.Texto3(caixa.transform, "Q", etq, 14f, TextAlignmentOptions.Left, cor, true);
            UIKit.Por(e2, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -56f), new Vector2(220f, 18f));

            WeaponData cap = w;
            var bt = caixa.gameObject.AddComponent<Button>();
            bt.targetGraphic = caixa;
            var cc2 = bt.colors; cc2.highlightedColor = new Color(2.1f, 2.1f, 2.1f, 1f); cc2.fadeDuration = 0.08f; bt.colors = cc2;
            bt.onClick.AddListener(() => EscolherArma(cap));
        }
    }

    /// <summary>Clicou numa arma da faixa: passa a olhar ela e, se ja for dele, equipa.</summary>
    private void EscolherArma(WeaponData w)
    {
        armaVista = w;
        selecaoAtiva = false; selecionado = null;
        if (MetaProgressao.ArmaComprada(w)) MetaProgressao.ArmaSelecionadaId = w.Id;
        if (preview != null) preview.LimparPrevisao();
        AvisarArmaDoJogador();
        Aba(1);
    }

    private GameObject MontarLinhaPeca(Transform pai, AnexoArma a, int indice)
    {
        WeaponData arma = ArmaDaTela;
        string idArma = arma != null ? arma.Id : "";
        bool comprado = a == null || MetaProgressao.AnexoComprado(a);
        bool equipado = (a == null)
            ? string.IsNullOrEmpty(MetaProgressao.AnexoEquipado(idArma, slotAtivo))
            : MetaProgressao.AnexoEquipado(idArma, slotAtivo) == a.id;

        bool sel = selecaoAtiva && selecionado == a;
        Color corFundo = sel ? new Color(0.34f, 0.78f, 1f, 0.20f)
                       : (equipado ? new Color(1f, 0.72f, 0.22f, 0.14f) : new Color(1f, 1f, 1f, 0.05f));
        var caixa = UIKit.Caixa(pai, "P" + indice, corFundo, 10);
        UIKit.Por(caixa, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f - indice * 92f), new Vector2(388f, 84f));
        caixa.raycastTarget = true;

        var nome = UIKit.Texto3(caixa.transform, "N", a == null ? "SEM ANEXO" : a.nomeExibicao, 19f,
                                TextAlignmentOptions.Left, comprado ? UIKit.Texto : new Color(0.5f, 0.54f, 0.6f), true);
        UIKit.Por(nome, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(280f, 24f));

        string sub = a == null ? "Ponto de montagem livre." : a.descricao;
        var desc = UIKit.Texto3(caixa.transform, "D", sub, 13f, TextAlignmentOptions.TopLeft, UIKit.TextoFraco, false);
        UIKit.Por(desc, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -34f), new Vector2(268f, 40f));
        desc.textWrappingMode = TextWrappingModes.Normal;

        string etiqueta = equipado ? "EQUIPADO" : (comprado ? "NA MOCHILA" : "$ " + a.preco);
        Color corEt = equipado ? UIKit.Destaque
                    : (comprado ? new Color(0.55f, 0.60f, 0.68f)
                    : (a != null && MetaProgressao.Dinheiro >= a.preco ? UIKit.Destaque : UIKit.Perigo));
        var et = UIKit.Texto3(caixa.transform, "E", etiqueta, 14f, TextAlignmentOptions.Center, corEt, true);
        UIKit.Por(et, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(96f, 26f));

        var bb = caixa.gameObject.AddComponent<Button>();
        bb.targetGraphic = caixa;
        var cc3 = bb.colors; cc3.highlightedColor = new Color(2.2f, 2.2f, 2.2f, 1f); cc3.fadeDuration = 0.08f; bb.colors = cc3;
        AnexoArma cap = a;
        bb.onClick.AddListener(() => Selecionar(cap));
        return caixa.gameObject;
    }

    /// <summary>
    /// Clicou numa peca: so SELECIONA. Mostra no modelo 3D e nas barras o que
    /// ela faria, sem tirar um centavo. Comprar/equipar so no botao de baixo.
    /// </summary>
    private void Selecionar(AnexoArma a)
    {
        selecionado = a;
        selecaoAtiva = true;
        if (preview != null) preview.Prever(a != null ? a.slot : slotAtivo, a);
        Aba(1);
    }

    /// <summary>O botao de acao: compra se falta, equipa se ja tem, remove se e a atual.</summary>
    private void Confirmar()
    {
        WeaponData arma = ArmaDaTela;
        if (arma == null) return;

        // arma ainda nao comprada: o botao vira a compra da ARMA
        if (!MetaProgressao.ArmaComprada(arma))
        {
            if (!MetaProgressao.ComprarArma(arma)) return;
            MetaProgressao.ArmaSelecionadaId = arma.Id;
            AtualizarBanco();
            AvisarArmaDoJogador();
            Aba(1);
            return;
        }

        if (!selecaoAtiva) return;
        string idArma = arma.Id;

        if (selecionado == null)
        {
            MetaProgressao.DesequiparSlot(idArma, slotAtivo);
        }
        else if (!MetaProgressao.AnexoComprado(selecionado))
        {
            if (!MetaProgressao.ComprarAnexo(selecionado)) return;   // sem dinheiro
            MetaProgressao.EquiparAnexo(idArma, selecionado, selecionado.slot);
        }
        else if (MetaProgressao.AnexoEquipado(idArma, selecionado.slot) == selecionado.id)
        {
            MetaProgressao.DesequiparSlot(idArma, selecionado.slot);
        }
        else
        {
            MetaProgressao.EquiparAnexo(idArma, selecionado, selecionado.slot);
        }

        selecaoAtiva = false;
        selecionado = null;
        AtualizarBanco();
        if (preview != null) preview.LimparPrevisao();
        AvisarArmaDoJogador();
        Aba(1);
    }

    /// <summary>Se a arma do player ja existe na cena, remonta ela na hora.</summary>
    private static void AvisarArmaDoJogador()
    {
        var wc = Object.FindAnyObjectByType<WeaponController>();
        var nova = MetaProgressao.ArmaSelecionada;
        if (wc != null && nova != null && wc.CurrentWeapon != nova) wc.EquiparDaArmaria(nova);
        var m = Object.FindAnyObjectByType<MontagemArma>();
        if (m != null) m.Recarregar();
    }

    // ---------------- barras comparativas ----------------

    private void Multiplicadores(AnexoArma substituto, bool usarSubstituto, out float esp, out float cad, out float dano, out float rec)
    {
        esp = 1f; cad = 1f; dano = 1f; rec = 1f;
        WeaponData arma = ArmaDaTela;
        if (arma == null) return;
        string idArma = arma.Id;
        for (int s = 0; s < 6; s++)
        {
            SlotAttach slot = (SlotAttach)s;
            if (!arma.AceitaSlot(slot)) continue;
            AnexoArma a = MetaProgressao.ResolverEquipado(idArma, slot);
            if (usarSubstituto && slot == slotAtivo) a = substituto;
            if (a == null) continue;
            esp *= a.multEspalhamento; cad *= a.multCadencia; dano *= a.multDano; rec *= a.multRecuo;
        }
    }

    private void MontarBarrasDeStatus(Transform pai)
    {
        float e0, c0, d0, r0;
        Multiplicadores(null, false, out e0, out c0, out d0, out r0);

        bool prevendo = selecaoAtiva;
        float e1 = e0, c1 = c0, d1 = d0, r1 = r0;
        if (prevendo) Multiplicadores(selecionado, true, out e1, out c1, out d1, out r1);

        string[] nomes = new string[] { "PRECISÃO", "CADÊNCIA", "DANO", "CONTROLE" };
        float[] atual = new float[] { e0, c0, d0, r0 };
        float[] prev  = new float[] { e1, c1, d1, r1 };
        // espalhamento e recuo: MENOR e melhor. cadencia e dano: MAIOR e melhor.
        bool[] menorMelhor = new bool[] { true, false, false, true };

        for (int i = 0; i < nomes.Length; i++)
        {
            float y = 24f + (3 - i) * 20f;

            var lbl = UIKit.Texto3(pai, "L" + i, nomes[i], 12f, TextAlignmentOptions.Left, UIKit.TextoFraco, true);
            UIKit.Por(lbl, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, y), new Vector2(110f, 16f));
            lbl.characterSpacing = 3f;

            float fAtual = Preenchimento(atual[i], menorMelhor[i]);
            float fPrev  = Preenchimento(prev[i],  menorMelhor[i]);
            bool melhorou = menorMelhor[i] ? prev[i] < atual[i] - 0.001f : prev[i] > atual[i] + 0.001f;
            bool piorou   = menorMelhor[i] ? prev[i] > atual[i] + 0.001f : prev[i] < atual[i] - 0.001f;

            // barra fantasma: mostra pra onde vai
            if (prevendo && (melhorou || piorou))
            {
                var fantasma = UIKit.Barra(pai, "F" + i, melhorou ? new Color(0.35f, 0.92f, 0.45f) : UIKit.Perigo, 7f, UIKit.RaioBarra);
                var ft = (RectTransform)fantasma.transform.parent;
                ft.anchorMin = new Vector2(0f, 0f); ft.anchorMax = new Vector2(0f, 0f); ft.pivot = new Vector2(0f, 0f);
                ft.anchoredPosition = new Vector2(132f, y + 4f); ft.sizeDelta = new Vector2(230f, 7f);
                fantasma.fillAmount = Mathf.Max(fAtual, fPrev);
                // some com o trilho do fantasma pra nao dobrar o fundo
                var img = ft.GetComponent<Image>(); if (img != null) img.color = new Color(0f, 0f, 0f, 0f);
            }

            var barra = UIKit.Barra(pai, "B" + i, UIKit.Destaque, 7f, UIKit.RaioBarra);
            var trilho = (RectTransform)barra.transform.parent;
            trilho.anchorMin = new Vector2(0f, 0f); trilho.anchorMax = new Vector2(0f, 0f); trilho.pivot = new Vector2(0f, 0f);
            trilho.anchoredPosition = new Vector2(132f, y + 4f); trilho.sizeDelta = new Vector2(230f, 7f);
            barra.fillAmount = prevendo ? Mathf.Min(fAtual, fPrev) : fAtual;
            barra.color = UIKit.Destaque;

            string txt = Formatar(atual[i]);
            Color corTxt = UIKit.TextoFraco;
            if (prevendo && (melhorou || piorou))
            {
                txt = Formatar(atual[i]) + "  →  " + Formatar(prev[i]);
                corTxt = melhorou ? new Color(0.45f, 0.95f, 0.55f) : UIKit.Perigo;
            }
            var num = UIKit.Texto3(pai, "V" + i, txt, 12f, TextAlignmentOptions.Left, corTxt, true);
            UIKit.Por(num, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(372f, y), new Vector2(220f, 16f));
        }
    }

    private static float Preenchimento(float mult, bool menorMelhor)
    {
        return Mathf.Clamp01(menorMelhor ? (2f - mult) * 0.5f : mult * 0.5f);
    }

    private static string Formatar(float m)
    {
        return (m >= 0.999f && m <= 1.001f) ? "—" : "×" + m.ToString("0.00");
    }

    // ---------------- RECORDES ----------------
    //
    // O que segura alguem num roguelike nao e a partida, e o placar que ela
    // deixa. Risk of Rain 2 guarda o registro de cada run; Balatro tem
    // historico. Aqui sao tres blocos: o melhor de cada categoria, o
    // somatorio da carreira, e as ultimas doze partidas em ordem.

    private void MontarRecordes()
    {
        bool vazio = Recordes.Partidas == 0;

        if (vazio)
        {
            var aviso = UIKit.Texto3(conteudo, "Vazio", "NENHUMA PARTIDA AINDA\n<size=16>jogue uma vez e seus recordes aparecem aqui</size>",
                                     26f, TextAlignmentOptions.Center, UIKit.TextoFraco, true);
            UIKit.Esticar(aviso);
            return;
        }

        // ---- melhores marcas ----
        var tituloA = UIKit.Texto3(conteudo, "TA", "MELHORES MARCAS", 13f, TextAlignmentOptions.Left, UIKit.Destaque, true);
        UIKit.Por(tituloA, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(400f, 20f));
        tituloA.characterSpacing = 8f;

        string[][] marcas = new string[][] {
            new string[]{ "MELHOR WAVE",   Recordes.MelhorWave.ToString() },
            new string[]{ "MAIS TEMPO",    Recordes.Relogio(Recordes.MelhorTempo) },
            new string[]{ "MAIS ABATES",   Recordes.MaisAbates.ToString() },
            new string[]{ "MAIOR NÍVEL",   Recordes.MaiorNivel.ToString() },
            new string[]{ "MAIOR DANO",    Recordes.MaiorDano.ToString() },
            new string[]{ "MAIOR GOLPE",   Recordes.MaiorGolpe.ToString() },
            new string[]{ "PICO DE DANO",  Recordes.MaiorPicoDps + "/s" }
        };
        for (int i = 0; i < marcas.Length; i++)
        {
            int col = i % 4, lin = i / 4;
            CartaoMarca(marcas[i][0], marcas[i][1], new Vector2(24f + col * 176f, -48f - lin * 92f), UIKit.Destaque);
        }

        // ---- carreira ----
        var tituloB = UIKit.Texto3(conteudo, "TB", "CARREIRA", 13f, TextAlignmentOptions.Left, new Color(0.55f,0.78f,1f), true);
        UIKit.Por(tituloB, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -240f), new Vector2(400f, 20f));
        tituloB.characterSpacing = 8f;

        CartaoMarca("PARTIDAS",     Recordes.Partidas.ToString(),                   new Vector2(24f, -270f),  new Color(0.55f,0.78f,1f));
        CartaoMarca("ABATES TOTAIS", Recordes.AbatesTotais.ToString(),              new Vector2(200f, -270f), new Color(0.55f,0.78f,1f));
        CartaoMarca("TEMPO TOTAL",  Recordes.Relogio(Recordes.TempoTotal),          new Vector2(376f, -270f), new Color(0.55f,0.78f,1f));
        CartaoMarca("MÉDIA/PARTIDA", (Recordes.AbatesTotais / Mathf.Max(1, Recordes.Partidas)) + " abates", new Vector2(552f, -270f), new Color(0.55f,0.78f,1f));

        // ---- historico ----
        var tituloC = UIKit.Texto3(conteudo, "TC", "ÚLTIMAS PARTIDAS", 13f, TextAlignmentOptions.Left, UIKit.TextoFraco, true);
        UIKit.Por(tituloC, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -388f), new Vector2(400f, 20f));
        tituloC.characterSpacing = 8f;

        var hist = Recordes.Historico();
        int melhorWave = Recordes.MelhorWave;
        for (int i = 0; i < hist.Count && i < 8; i++)
        {
            var h = hist[i];
            bool ehMelhor = h.wave >= melhorWave && melhorWave > 0;
            var linha = UIKit.PainelBordado(conteudo, "H" + i, ehMelhor ? UIKit.PainelForte : UIKit.Painel, 8);
            UIKit.Por(linha, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -416f - i * 34f), new Vector2(700f, 30f));
            var d = linha.transform.GetChild(0);

            // a mais recente em cima; a melhor de todas recebe a barra dourada
            if (ehMelhor)
            {
                var faixa = UIKit.Caixa(d, "F", UIKit.Destaque, 2);
                UIKit.Por(faixa, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(3f, 20f));
            }

            var quando = UIKit.Texto3(d, "Q", h.quando, 12f, TextAlignmentOptions.Left, UIKit.TextoFraco, false);
            UIKit.Por(quando, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(90f, 18f));

            var txt = "WAVE <b>" + h.wave + "</b>     " + Recordes.Relogio(h.tempo) + "     " + h.abates + " abates     nível " + h.nivel;
            var linhaTxt = UIKit.Texto3(d, "T", txt, 13f, TextAlignmentOptions.Left, ehMelhor ? UIKit.Texto : UIKit.TextoFraco, false);
            UIKit.Por(linhaTxt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(116f, 0f), new Vector2(560f, 18f));
        }
    }

    private void CartaoMarca(string rotulo, string valor, Vector2 pos, Color cor)
    {
        var caixa = UIKit.PainelBordado(conteudo, "M_" + rotulo, UIKit.Painel, 10);
        UIKit.Por(caixa, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(168f, 78f));
        var d = caixa.transform.GetChild(0);

        var lbl = UIKit.Texto3(d, "L", rotulo, 11f, TextAlignmentOptions.Left, UIKit.TextoFraco, true);
        UIKit.Por(lbl, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(146f, 16f));
        lbl.characterSpacing = 4f;

        var val = UIKit.Texto3(d, "V", valor, 26f, TextAlignmentOptions.Left, cor, true);
        UIKit.Por(val, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(13f, -28f), new Vector2(146f, 34f));
    }

    // ---------------- CARTAS ----------------

    private void MontarCartas()
    {
        // a aba de cartas virou CAIXA: paga, gira a roleta e leva o que sair.
        CaixaDeCartas.Montar(conteudo, AtualizarBanco);
    }

    /// <summary>Raridade deduzida do peso no sorteio (mesma regra da tela de level up).</summary>
    private static string RaridadeDe(UpgradeData u, out Color cor)
    {
        if (u.weight >= 9f) { cor = new Color(0.60f, 0.66f, 0.75f); return "COMUM"; }
        if (u.weight >= 6f) { cor = new Color(0.36f, 0.70f, 1.00f); return "INCOMUM"; }
        cor = new Color(0.85f, 0.45f, 1.00f); return "RARO";
    }

    private static int PrecoCarta(UpgradeData u)
    {
        if (u.weight < 6f) return 1200;      // raro
        if (u.weight < 9f) return 600;       // incomum
        return 350;                           // comum
    }

    // ---------------- HUD do jogo ----------------

    private readonly List<Canvas> hudEscondido = new List<Canvas>();
    private readonly List<Crosshair> miraEscondida = new List<Crosshair>();

    /// <summary>
    /// Some com o HUD da partida enquanto o menu esta aberto. Sem isso a barra
    /// de vida, a wave e a mira aparecem por tras dos paineis.
    /// Criterio: qualquer canvas com ordem MENOR que a do menu.
    /// </summary>
    private void EsconderHudDoJogo(bool esconder)
    {
        if (esconder)
        {
            hudEscondido.Clear();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c == null || c.sortingOrder >= 150) continue;
                if (!c.enabled) continue;
                c.enabled = false;
                hudEscondido.Add(c);
            }
            // a mira desenha por OnGUI, entao nao e pega pelo laco de Canvas
            foreach (var cr in Object.FindObjectsByType<Crosshair>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (cr != null) { cr.enabled = false; miraEscondida.Add(cr); }
        }
        else
        {
            foreach (var c in hudEscondido) if (c != null) c.enabled = true;
            hudEscondido.Clear();
            foreach (var cr in miraEscondida) if (cr != null) cr.enabled = true;
            miraEscondida.Clear();
        }
    }

    /// <summary>
    /// Rodape da coluna de pecas: mostra o que esta SELECIONADO e o botao que
    /// efetiva. Enquanto nada e apertado aqui, nada foi comprado nem equipado.
    /// </summary>
    private void MontarRodapeAcao(Transform pai)
    {
        var caixa = UIKit.Caixa(pai, "Rodape", new Color(0f, 0f, 0f, 0.35f), 12);
        var rt = caixa.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 14f);
        rt.sizeDelta = new Vector2(388f, 128f);

        WeaponData arma = ArmaDaTela;

        // ---- caso 1: a ARMA ainda nao e dele. O rodape vira a compra da arma. ----
        if (arma != null && !MetaProgressao.ArmaComprada(arma))
        {
            bool paga = MetaProgressao.Dinheiro >= arma.preco;

            var nA = UIKit.Texto3(caixa.transform, "N", arma.displayName.ToUpper(), 18f, TextAlignmentOptions.Center, UIKit.Texto, true);
            UIKit.Por(nA, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(360f, 24f));

            var sA = UIKit.Texto3(caixa.transform, "S", paga ? "Custa $ " + arma.preco : "Faltam $ " + (arma.preco - MetaProgressao.Dinheiro),
                                  13f, TextAlignmentOptions.Center, paga ? UIKit.TextoFraco : UIKit.Perigo, false);
            UIKit.Por(sA, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(360f, 20f));

            var bA = Botao(caixa.transform, paga ? "COMPRAR  $ " + arma.preco : "SEM DINHEIRO",
                           new Vector2(0f, -62f), 340f, 46f, paga ? UIKit.Destaque : UIKit.Perigo);
            bA.interactable = paga;
            if (paga) bA.onClick.AddListener(Confirmar);
            return;
        }

        // ---- caso 2: arma sem pontos de montagem (rojao) ----
        if (arma != null && arma.SlotsDaArma.Length == 0)
        {
            var d0 = UIKit.Texto3(caixa.transform, "Dica", "Nada pra montar aqui.\nEsta arma já está pronta.",
                                  14f, TextAlignmentOptions.Center, UIKit.TextoFraco, false);
            UIKit.Esticar(d0);
            d0.textWrappingMode = TextWrappingModes.Normal;
            return;
        }

        string idArma = arma != null ? arma.Id : "";

        if (!selecaoAtiva)
        {
            var dica = UIKit.Texto3(caixa.transform, "Dica", "Clique numa peça pra ver como ela fica\ne o que ela muda — sem gastar nada.",
                                    14f, TextAlignmentOptions.Center, UIKit.TextoFraco, false);
            UIKit.Esticar(dica);
            dica.textWrappingMode = TextWrappingModes.Normal;
            return;
        }

        bool ehVazio = selecionado == null;
        bool comprado = ehVazio || MetaProgressao.AnexoComprado(selecionado);
        bool equipado = ehVazio
            ? string.IsNullOrEmpty(MetaProgressao.AnexoEquipado(idArma, slotAtivo))
            : MetaProgressao.AnexoEquipado(idArma, selecionado.slot) == selecionado.id;
        int preco = ehVazio ? 0 : selecionado.preco;
        bool podePagar = comprado || MetaProgressao.Dinheiro >= preco;

        var nome = UIKit.Texto3(caixa.transform, "N", ehVazio ? "SEM ANEXO" : selecionado.nomeExibicao,
                                18f, TextAlignmentOptions.Center, UIKit.Texto, true);
        UIKit.Por(nome, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(360f, 24f));

        string sub;
        if (equipado) sub = "Já está montada na arma.";
        else if (comprado) sub = "Você já tem esta peça.";
        else sub = podePagar ? "Custa $ " + preco : "Faltam $ " + (preco - MetaProgressao.Dinheiro);
        var s = UIKit.Texto3(caixa.transform, "S", sub, 13f, TextAlignmentOptions.Center,
                             (!comprado && !podePagar) ? UIKit.Perigo : UIKit.TextoFraco, false);
        UIKit.Por(s, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(360f, 20f));

        string rotulo;
        Color cor;
        if (equipado && !ehVazio) { rotulo = "REMOVER"; cor = UIKit.Perigo; }
        else if (equipado && ehVazio) { rotulo = "JÁ ESTÁ VAZIO"; cor = UIKit.TextoFraco; }
        else if (comprado) { rotulo = "EQUIPAR"; cor = UIKit.Texto; }
        else if (podePagar) { rotulo = "COMPRAR  $ " + preco; cor = UIKit.Destaque; }
        else { rotulo = "SEM DINHEIRO"; cor = UIKit.Perigo; }

        var b = Botao(caixa.transform, rotulo, new Vector2(0f, -62f), 340f, 46f, cor);
        bool ativo = !(equipado && ehVazio) && (comprado || podePagar);
        b.interactable = ativo;
        if (ativo) b.onClick.AddListener(Confirmar);
    }
}
