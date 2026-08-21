using UnityEngine;

/// <summary>
/// SENSACAO DE IMPACTO: tremor de camera e congelamento de quadro.
///
/// Duas tecnicas, as duas emprestadas de jogos que acertaram nisso:
///
/// 1) TREMOR POR TRAUMA. Em vez de somar tremores (que empilham e viram
///    epilepsia numa horda), existe um unico valor 'trauma' de 0 a 1 que
///    cai sozinho com o tempo. O deslocamento e trauma AO QUADRADO - e o
///    modelo classico do Squirrel Eiserloh, usado em Enter the Gungeon e
///    Nuclear Throne. Elevar ao quadrado faz o tremor forte ser MUITO mais
///    forte que o fraco, e faz o fim do tremor sumir macio em vez de
///    cortar seco.
///
///    O ruido e Perlin, nao Random: Random pula pra todo lado e treme como
///    defeito de imagem; Perlin desenha uma curva continua e treme como
///    camera de verdade na mao de alguem.
///
/// 2) CONGELAMENTO DE QUADRO (hit stop). Ao matar, o tempo para por 40 a
///    90 milesimos. E o truque que Dead Cells, Hades e Doom Eternal usam
///    mais que qualquer particula: o cerebro le a pausa como peso do
///    golpe. Sem isso o tiro nao morde, por melhor que esteja o resto.
///
/// As duas passam por Intensidade, que o menu de opcoes controla - tremor
/// de tela e gatilho de enjoo em muita gente, entao desligar tem que ser
/// possivel.
/// </summary>
[DefaultExecutionOrder(200)]   // depois do CameraJogo, senao ele sobrescreve o tremor
public class ImpactoDeCamera : MonoBehaviour
{
    [Header("Tremor")]
    [Tooltip("Quanto de trauma some por segundo. Alto demais corta seco, baixo demais fica balancando.")]
    [SerializeField] private float decaimento = 1.7f;
    [Tooltip("Deslocamento maximo em metros, com trauma cheio.")]
    [SerializeField] private float deslocMax = 0.26f;
    [Tooltip("Rotacao maxima em graus, com trauma cheio.")]
    [SerializeField] private float giroMax = 2.6f;
    [Tooltip("Velocidade do ruido. Alto = vibracao, baixo = balanco.")]
    [SerializeField] private float frequencia = 24f;

    /// <summary>0 = sem tremor nenhum, 1 = normal. O menu de opcoes escreve aqui.</summary>
    public static float Intensidade = 1f;
    /// <summary>Congelar quadro pode ser desligado por quem nao gosta.</summary>
    public static bool CongelarLigado = true;

    private static ImpactoDeCamera instancia;
    private float trauma;
    private float semente;
    private Transform alvo;

    // congelamento
    private static float descongelarEm = -1f;
    private static bool congelando;

    private void Awake()
    {
        instancia = this;
        semente = Random.value * 100f;
        alvo = transform;
    }

    private void OnDestroy() { if (instancia == this) instancia = null; }

    /// <summary>Soma trauma. 0,05 = arranhao; 0,3 = abate; 0,6 = explosao.</summary>
    public static void Tremer(float quanto)
    {
        if (instancia == null || Intensidade <= 0f) return;
        // soma mas nao passa de 1: numa horda, cem tiros nao podem virar terremoto
        instancia.trauma = Mathf.Min(1f, instancia.trauma + quanto * Intensidade);
    }

    /// <summary>Para o tempo por alguns milesimos. Nao empilha: fica o maior.</summary>
    public static void Congelar(float segundos)
    {
        if (!CongelarLigado || segundos <= 0f) return;
        if (MenuPausa.Pausado || MenuPrincipal.Aberto) return;   // nao brigar com quem ja parou o jogo
        float ate = Time.realtimeSinceStartup + segundos;
        if (ate <= descongelarEm) return;
        descongelarEm = ate;
        if (!congelando)
        {
            congelando = true;
            Time.timeScale = 0f;
        }
    }

    /// <summary>
    /// O relogio do congelamento roda em tempo REAL e num Update qualquer,
    /// porque com timeScale em zero um Update comum ainda roda mas o tempo de
    /// jogo nao anda. Sem isso o jogo congelaria pra sempre.
    /// </summary>
    private void Update()
    {
        if (congelando && Time.realtimeSinceStartup >= descongelarEm)
        {
            congelando = false;
            descongelarEm = -1f;
            // so devolve se ninguem mais mexeu (pausa, menu, camera lenta de morte)
            if (!MenuPausa.Pausado && !MenuPrincipal.Aberto && Time.timeScale == 0f) Time.timeScale = 1f;
        }
    }

    private void LateUpdate()
    {
        if (trauma <= 0f) return;

        // decai em tempo real: senao o tremor congela junto com o hit stop
        trauma = Mathf.Max(0f, trauma - decaimento * Time.unscaledDeltaTime);
        float f = trauma * trauma;                       // o quadrado e o segredo
        float t = Time.unscaledTime * frequencia;

        float x = (Mathf.PerlinNoise(semente, t) * 2f - 1f) * deslocMax * f;
        float y = (Mathf.PerlinNoise(semente + 17f, t) * 2f - 1f) * deslocMax * f;
        float r = (Mathf.PerlinNoise(semente + 41f, t) * 2f - 1f) * giroMax * f;

        alvo.position += alvo.right * x + alvo.up * y;
        alvo.Rotate(0f, 0f, r, Space.Self);
    }

    /// <summary>Sair da partida com o tempo congelado deixaria o menu travado.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Zerar()
    {
        congelando = false; descongelarEm = -1f; instancia = null;
    }
}
