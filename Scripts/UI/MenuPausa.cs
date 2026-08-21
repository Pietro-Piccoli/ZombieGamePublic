using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// MENU DE PAUSA. ESC abre e fecha. Congela o jogo, solta o mouse e oferece
/// continuar / reiniciar / voltar pro menu / sair.
///
/// Se cria sozinho: nao precisa arrastar nada pra cena.
/// </summary>
public class MenuPausa : MonoBehaviour
{
    private static MenuPausa instancia;
    private GameObject painel;
    private bool pausado;

    public static bool Pausado { get { return instancia != null && instancia.pausado; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Registrar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AoCarregarCena;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AoCarregarCena;
        Nascer();
    }

    private static void AoCarregarCena(UnityEngine.SceneManagement.Scene c,
                                       UnityEngine.SceneManagement.LoadSceneMode m) { Nascer(); }

    private static void Nascer()
    {
        if (instancia != null) return;
        if (Object.FindAnyObjectByType<MenuPausa>() != null) return;
        var go = new GameObject("MenuPausa");
        instancia = go.AddComponent<MenuPausa>();
    }

    private void OnDestroy() { if (instancia == this) instancia = null; }

    private static bool EscApertado()
    {
#if ENABLE_INPUT_SYSTEM
        var k = UnityEngine.InputSystem.Keyboard.current;
        return k != null && k.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private void Update()
    {
        // nao abre por cima do menu inicial nem da tela de cartas
        if (MenuPrincipal.Aberto) return;
        if (AdminCheat.Aberto) return;
        if (!EscApertado()) return;
        if (pausado) Retomar(); else Pausar();
    }

    private void Pausar()
    {
        pausado = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Montar();
    }

    private void Retomar()
    {
        pausado = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (painel != null) Destroy(painel);
        painel = null;
    }

    private void Montar()
    {
        var canvas = UIKit.NovoCanvas(null, "Pausa_Canvas", 200);
        painel = canvas.gameObject;
        painel.AddComponent<GraphicRaycaster>();
        MenuPrincipal.GarantirEventSystem();

        var fundo = UIKit.Caixa(painel.transform, "Escurecer", new Color(0.01f, 0.012f, 0.02f, 0.82f), 1);
        UIKit.Esticar(fundo);
        fundo.raycastTarget = true;

        var caixa = UIKit.PainelBordado(painel.transform, "Caixa", UIKit.PainelForte, 18);
        UIKit.Por(caixa, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 470f));
        var dentro = caixa.transform.GetChild(0);

        var tit = UIKit.Texto3(dentro, "Tit", "PAUSADO", 34f, TextAlignmentOptions.Center, UIKit.Texto, true);
        UIKit.Por(tit, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(360f, 42f));
        tit.characterSpacing = 10f;

        string[] rotulos = new string[] { "CONTINUAR", "OPÇÕES", "REINICIAR", "MENU PRINCIPAL", "SAIR DO JOGO" };
        for (int i = 0; i < rotulos.Length; i++)
        {
            int k = i;
            var b = MenuPrincipal.Botao(dentro, rotulos[i], new Vector2(0f, -100f - i * 66f), 340f, 54f,
                                        i == 4 ? UIKit.Perigo : UIKit.Texto);
            b.onClick.AddListener(() => Acao(k));
        }
    }

    private void Acao(int i)
    {
        if (i == 0) { Retomar(); return; }
        if (i == 1) { AbrirOpcoes(); return; }
        Time.timeScale = 1f;
        if (i == 2 || i == 3)
        {
            pausado = false;
            if (painel != null) Destroy(painel);
            painel = null;
            MenuPrincipal.AbrirNoProximoCarregamento = (i == 3);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }
        Sair();
    }

    /// <summary>
    /// As opcoes abrem POR CIMA da pausa, com o jogo ainda parado. Assim da pra
    /// mexer na sensibilidade no meio da partida, testar, e voltar - que e o
    /// unico jeito de calibrar sensibilidade de verdade.
    /// </summary>
    private void AbrirOpcoes()
    {
        var canvas = UIKit.NovoCanvas(null, "PausaOpcoes_Canvas", 210);
        var go = canvas.gameObject;
        go.AddComponent<GraphicRaycaster>();

        var fundo = UIKit.Caixa(go.transform, "Escurecer", new Color(0.01f, 0.012f, 0.02f, 0.95f), 1);
        UIKit.Esticar(fundo); fundo.raycastTarget = true;

        var tit = UIKit.Texto3(go.transform, "Tit", "OPÇÕES", 34f, TextAlignmentOptions.Center, UIKit.Texto, true);
        UIKit.Por(tit, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(600f, 42f));
        tit.characterSpacing = 10f;

        PainelOpcoes.Montar(go.transform, null);

        var voltar = MenuPrincipal.Botao(go.transform, "VOLTAR", Vector2.zero, 300f, 48f, UIKit.Texto);
        var vrt = (RectTransform)voltar.transform;
        vrt.anchorMin = new Vector2(0.5f, 0f); vrt.anchorMax = new Vector2(0.5f, 0f);
        vrt.pivot = new Vector2(0.5f, 0f);
        vrt.anchoredPosition = new Vector2(0f, 40f);
        voltar.onClick.AddListener(() => Destroy(go));
    }

    public static void Sair()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
