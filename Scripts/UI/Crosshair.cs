using UnityEngine;

/// <summary>
/// Mira desenhada em codigo (OnGUI).
///
/// A mira ABRE conforme o espalhamento da arma: mirando com o botao
/// direito, ela fecha. Comunica precisao sem texto.
///
/// MARCADOR DE ACERTO: quatro riscos em X que piscam quando a bala pega
/// alguem. E o retorno mais barato e mais importante de um shooter - sem
/// ele o jogador nao sabe se acertou, so supoe. Segue a convencao que todo
/// mundo ja aprendeu em Call of Duty e Destiny:
///   branco    = acertou
///   amarelo   = acertou a cabeca
///   vermelho  = matou           (o X abre mais e demora mais pra sumir)
/// </summary>
public class Crosshair : MonoBehaviour
{
    [SerializeField] private WeaponController weapon;
    [SerializeField] private float baseGap = 6f;
    [SerializeField] private float gapPerDegree = 9f;
    [SerializeField] private float length = 9f;
    [SerializeField] private float thickness = 2f;
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.85f);

    [Header("Marcador de acerto")]
    [SerializeField] private float duracaoAcerto = 0.12f;
    [SerializeField] private float duracaoAbate = 0.32f;
    [SerializeField] private Color corAcerto = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Color corCabeca = new Color(1f, 0.85f, 0.25f, 0.95f);
    [SerializeField] private Color corAbate = new Color(1f, 0.25f, 0.18f, 1f);

    /// <summary>Ligavel/desligavel pelo menu de opcoes.</summary>
    public static bool MarcadorLigado = true;

    private static Crosshair instancia;
    private Texture2D pixel;
    private float smoothGap;

    private float marcadorAte;
    private float marcadorDur;
    private Color marcadorCor;
    private bool marcadorAbate;

    private void Awake()
    {
        instancia = this;
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();

        if (weapon == null) weapon = FindAnyObjectByType<WeaponController>();
    }

    private void OnDestroy() { if (instancia == this) instancia = null; }

    public static void MarcarAcerto(bool cabeca)
    {
        if (instancia == null || !MarcadorLigado) return;
        // abate ja marcado tem prioridade: nao deixa um acerto fraco apagar
        if (instancia.marcadorAbate && Time.unscaledTime < instancia.marcadorAte) return;
        instancia.Marcar(cabeca ? instancia.corCabeca : instancia.corAcerto, instancia.duracaoAcerto, false);
    }

    public static void MarcarAbate(bool cabeca)
    {
        if (instancia == null || !MarcadorLigado) return;
        instancia.Marcar(instancia.corAbate, instancia.duracaoAbate, true);
    }

    private void Marcar(Color c, float dur, bool abate)
    {
        // tempo NAO escalado: senao o marcador congela junto com o hit stop
        marcadorCor = c; marcadorDur = dur; marcadorAte = Time.unscaledTime + dur; marcadorAbate = abate;
    }

    private void Update()
    {
        float spread = weapon != null ? weapon.CurrentSpread : 0f;
        float goal = baseGap + spread * gapPerDegree;
        smoothGap = Mathf.Lerp(smoothGap, goal, 12f * Time.unscaledDeltaTime);
    }

    private void OnGUI()
    {
        // O IMGUI chama OnGUI VARIAS VEZES por quadro - Layout, Repaint, e um
        // evento por tecla/clique. Desenhar em todos eles e refazer o mesmo
        // desenho de 3 a 5 vezes por quadro de graca. So Repaint pinta pixel.
        if (Event.current.type != EventType.Repaint) return;

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        float g = smoothGap;

        Color old = GUI.color;
        GUI.color = color;

        GUI.DrawTexture(new Rect(cx - g - length, cy - thickness * 0.5f, length, thickness), pixel);
        GUI.DrawTexture(new Rect(cx + g, cy - thickness * 0.5f, length, thickness), pixel);
        GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy - g - length, thickness, length), pixel);
        GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy + g, thickness, length), pixel);
        GUI.DrawTexture(new Rect(cx - 1f, cy - 1f, 2f, 2f), pixel);

        DesenharMarcador(cx, cy);

        GUI.color = old;
    }

    /// <summary>
    /// O X do marcador. Ele nasce um pouco aberto e FECHA enquanto some -
    /// o movimento pra dentro puxa o olho pro centro da tela, que e onde a
    /// informacao esta. Marcador que so pisca parado nao le tao bem.
    /// </summary>
    private void DesenharMarcador(float cx, float cy)
    {
        float restante = marcadorAte - Time.unscaledTime;
        if (restante <= 0f || marcadorDur <= 0f) return;

        float k = restante / marcadorDur;            // 1 no comeco, 0 no fim
        float alpha = k * k;                         // some macio
        float dentro = Mathf.Lerp(4f, 9f, k);        // fecha enquanto some
        float comp = marcadorAbate ? 9f : 6f;
        float esp = marcadorAbate ? 2.5f : 2f;

        Color c = marcadorCor; c.a *= alpha;
        GUI.color = c;

        // quatro riscos diagonais, girados 45 graus em torno do centro
        for (int i = 0; i < 4; i++)
        {
            float ang = 45f + i * 90f;
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(ang, new Vector2(cx, cy));
            GUI.DrawTexture(new Rect(cx - esp * 0.5f, cy - dentro - comp, esp, comp), pixel);
            GUI.matrix = m;
        }
    }
}
