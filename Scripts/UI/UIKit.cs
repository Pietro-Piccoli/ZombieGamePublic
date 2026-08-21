using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Regua unica de estilo da UI. Todo HUD monta a partir daqui, entao
/// mudar uma cor ou um raio aqui muda o jogo inteiro.
///
/// Nao precisa de nenhum sprite importado: o retangulo arredondado e
/// gerado em codigo, com anti-aliasing, e reaproveitado por raio.
/// </summary>
public static class UIKit
{
    // ---------------- paleta ----------------
    public static readonly Color Painel      = new Color(0.055f, 0.065f, 0.085f, 0.78f);
    public static readonly Color PainelForte = new Color(0.045f, 0.052f, 0.070f, 0.92f);
    public static readonly Color Borda       = new Color(1f, 1f, 1f, 0.10f);
    public static readonly Color Texto       = new Color(0.94f, 0.95f, 0.97f, 1f);
    public static readonly Color TextoFraco  = new Color(0.60f, 0.65f, 0.73f, 1f);
    public static readonly Color Destaque    = new Color(1.00f, 0.72f, 0.22f, 1f);
    public static readonly Color Vida        = new Color(0.88f, 0.24f, 0.26f, 1f);
    public static readonly Color VidaBaixa   = new Color(1.00f, 0.45f, 0.12f, 1f);
    public static readonly Color Xp          = new Color(0.34f, 0.78f, 1.00f, 1f);
    public static readonly Color Perigo      = new Color(0.90f, 0.20f, 0.22f, 1f);
    public static readonly Color Trilho      = new Color(1f, 1f, 1f, 0.09f);

    public const int RaioPainel = 12;
    public const int RaioBarra  = 5;

    // ---------------- fontes ----------------
    private static TMP_FontAsset fontePesada, fonteMedia;

    public static TMP_FontAsset FontePesada
    {
        get
        {
            if (fontePesada == null) fontePesada = Carregar("Roboto-Black SDF");
            return fontePesada;
        }
    }

    public static TMP_FontAsset FonteMedia
    {
        get
        {
            if (fonteMedia == null) fonteMedia = Carregar("Roboto-Medium SDF");
            return fonteMedia;
        }
    }

    private static TMP_FontAsset Carregar(string nome)
    {
        var f = Resources.Load<TMP_FontAsset>(nome);
#if UNITY_EDITOR
        if (f == null)
            f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Resources/" + nome + ".asset");
#endif
        if (f == null) f = TMP_Settings.defaultFontAsset;
        return f;
    }

    // ---------------- retangulo arredondado gerado em codigo ----------------
    private static readonly System.Collections.Generic.Dictionary<int, Sprite> cacheSprite =
        new System.Collections.Generic.Dictionary<int, Sprite>();

    /// <summary>Sprite 9-sliced de canto arredondado. Cacheado por raio.</summary>
    public static Sprite Arredondado(int raio)
    {
        if (raio < 1) raio = 1;
        if (cacheSprite.ContainsKey(raio) && cacheSprite[raio] != null) return cacheSprite[raio];

        int lado = raio * 2 + 4;
        var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        tex.name = "UIKit_Round_" + raio;
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var px = new Color32[lado * lado];
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                // distancia ao centro do canto mais proximo
                float cx = x < raio ? raio - 0.5f : (x >= lado - raio ? lado - raio - 0.5f : x);
                float cy = y < raio ? raio - 0.5f : (y >= lado - raio ? lado - raio - 0.5f : y);
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float a = Mathf.Clamp01(raio - d + 0.5f);   // ~1px de anti-aliasing
                px[y * lado + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);

        var sp = Sprite.Create(tex, new Rect(0, 0, lado, lado), new Vector2(0.5f, 0.5f), 100f, 0,
                               SpriteMeshType.FullRect, new Vector4(raio, raio, raio, raio));
        sp.name = "UIKit_Round_" + raio;
        cacheSprite[raio] = sp;
        return sp;
    }

    // ---------------- construtores ----------------

    public static Image Caixa(Transform pai, string nome, Color cor, int raio)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        var img = go.AddComponent<Image>();
        img.sprite = Arredondado(raio);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.color = cor;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Painel com borda de 1px: devolve o fundo (a borda fica atras).</summary>
    public static Image PainelBordado(Transform pai, string nome, Color cor, int raio)
    {
        var borda = Caixa(pai, nome, Borda, raio);
        var fundo = Caixa(borda.transform, "Fundo", cor, Mathf.Max(1, raio - 1));
        var rt = fundo.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(1.5f, 1.5f); rt.offsetMax = new Vector2(-1.5f, -1.5f);
        return borda;
    }

    public static TextMeshProUGUI Texto3(Transform pai, string nome, string txt, float tamanho,
                                         TextAlignmentOptions alinhamento, Color cor, bool pesada)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = pesada ? FontePesada : FonteMedia;
        t.text = txt;
        t.fontSize = tamanho;
        t.alignment = alinhamento;
        t.color = cor;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    /// <summary>
    /// Legibilidade pra texto solto sobre a cena. SEM contorno: usa sombra
    /// projetada (underlay), que separa da cena sem engrossar a letra.
    /// </summary>
    public static void Contornar(TextMeshProUGUI t, float forca)
    {
        t.fontMaterial = new Material(t.fontMaterial);
        t.fontMaterial.DisableKeyword("OUTLINE_ON");
        t.outlineWidth = 0f;
        t.fontMaterial.EnableKeyword("UNDERLAY_ON");
        t.fontMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.6f));
        t.fontMaterial.SetFloat("_UnderlayOffsetX", 0.5f);
        t.fontMaterial.SetFloat("_UnderlayOffsetY", -0.5f);
        t.fontMaterial.SetFloat("_UnderlayDilate", 0.1f);
        t.fontMaterial.SetFloat("_UnderlaySoftness", 0.35f);
    }

    /// <summary>Trilho + preenchimento arredondados. Devolve o preenchimento (use fillAmount).</summary>
    public static Image Barra(Transform pai, string nome, Color corFill, float altura, int raio)
    {
        var trilho = Caixa(pai, nome, Trilho, raio);
        var fill = Caixa(trilho.transform, "Fill", corFill, raio);
        var rt = fill.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 1f;
        return fill;
    }

    // ---------------- ancoragem ----------------

    public static RectTransform Por(Component c, Vector2 ancora, Vector2 pivo, Vector2 pos, Vector2 tam)
    {
        var rt = (RectTransform)c.transform;
        rt.anchorMin = ancora; rt.anchorMax = ancora; rt.pivot = pivo;
        rt.anchoredPosition = pos; rt.sizeDelta = tam;
        return rt;
    }

    public static void Esticar(Component c)
    {
        var rt = (RectTransform)c.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    /// <summary>Canvas overlay padrao, 1920x1080 de referencia.</summary>
    public static Canvas NovoCanvas(Transform pai, string nome, int ordem)
    {
        var go = new GameObject(nome);
        if (pai != null) go.transform.SetParent(pai, false);
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = ordem;
        var s = go.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920f, 1080f);
        s.matchWidthOrHeight = 0.5f;
        return c;
    }
}
