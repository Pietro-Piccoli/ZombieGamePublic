using UnityEngine;
using StarterAssets;

/// <summary>
/// AGACHAR - mecanica nova, feita pro pack de rifle (Crouch_Idle, CrouchWalk,
/// e os Aim Offsets agachado).
///
/// O que ele faz, na ordem certa:
///   1) le o Ctrl / C;
///   2) encolhe a CAPSULA mantendo os pes no mesmo lugar (o centro desce junto
///      com a altura, senao o boneco afunda no chao ou flutua);
///   3) baixa a VELOCIDADE do controlador oficial pro valor do clipe agachado,
///      pra pe nao patinar;
///   4) desce o pivo da CAMERA junto, senao a camera fica na altura do ombro
///      em pe e o tiro sai por cima da cabeca dele;
///   5) escreve 'Agachado' (bool) e 'Agachamento' (float 0..1) no Animator.
///
/// LEVANTAR TEM TETO: se tiver laje/cano em cima, ele NAO levanta. Sem essa
/// checagem o CharacterController cresce dentro do colisor e o motor cospe o
/// boneco pra fora do mapa.
/// </summary>
[DefaultExecutionOrder(-40)]
public class Agachar : MonoBehaviour
{
    [Header("Postura")]
    [Tooltip("Altura da capsula agachado, em metros. Em pe ela mede 1,75.")]
    [SerializeField] private float alturaAgachado = 1.15f;
    [Tooltip("Quao rapido entra e sai do agachamento (0..1 por segundo).")]
    [SerializeField] private float velTransicao = 6f;

    [Header("Velocidade")]
    [Tooltip("Velocidade andando agachado. Tem que casar com a passada do clipe pro pe nao patinar.")]
    [SerializeField] private float velAgachado = 1.0f;

    [Header("Camera")]
    [Tooltip("Quanto o pivo da camera desce quando agacha, em metros.")]
    [SerializeField] private float descerCamera = 0.55f;

    [Header("Teto")]
    [Tooltip("Camadas que contam como teto pra impedir levantar.")]
    [SerializeField] private LayerMask teto = ~0;
    [Tooltip("Folga extra exigida pra poder levantar, em metros.")]
    [SerializeField] private float folgaTeto = 0.05f;

    /// <summary>So pra teste automatizado: agacha sem apertar tecla.</summary>
    public static bool debugAgachar = false;

    /// <summary>Esta agachado agora (intencao, nao a transicao).</summary>
    public bool Agachado { get { return agachado; } }
    /// <summary>0 = em pe, 1 = agachado. E o que a arvore de blend usa.</summary>
    public float Blend { get { return blend; } }
    /// <summary>Diagnostico: bateu teto na ultima tentativa de levantar.</summary>
    public bool BloqueadoPorTeto { get; private set; }

    private CharacterController capsula;
    private ThirdPersonController controlador;
    private StarterAssetsInputs entrada;
    private AnimacaoJogador anim;
    private Animator animador;
    private CameraJogo cameraJogo;

    private float alturaEmPe;
    private Vector3 centroEmPe;
    private float velEmPe;
    private bool agachado;
    private float blend;
    private bool temParametro;

    private static readonly int HAgachado = Animator.StringToHash("Agachado");
    private static readonly int HAgachamento = Animator.StringToHash("Agachamento");

    private void Awake()
    {
        capsula = GetComponent<CharacterController>();
        controlador = GetComponent<ThirdPersonController>();
        entrada = GetComponent<StarterAssetsInputs>();
        anim = GetComponent<AnimacaoJogador>();
        animador = null;
        cameraJogo = FindAnyObjectByType<CameraJogo>();

        if (capsula != null) { alturaEmPe = capsula.height; centroEmPe = capsula.center; }
        if (controlador != null) velEmPe = controlador.MoveSpeed;

        temParametro = TemParametro("Agachado");
    }

    /// <summary>
    /// Acha o Animator do boneco ATIVO, e reacha se ele mudar.
    ///
    /// Nao da pra pegar isto no Awake e guardar: a ordem de execucao deste
    /// script (-40) roda ANTES do AnimacaoJogador (20), entao o campo dele
    /// ainda aponta pro boneco antigo. Quando o projeto trocou de MANNY pra
    /// MOTTA foi exatamente isso que aconteceu: a capsula agachava, a camera
    /// descia, e a ANIMACAO continuava em pe - porque o parametro estava sendo
    /// escrito num Animator desativado.
    /// </summary>
    private Animator Animador()
    {
        if (animador != null && animador.gameObject.activeInHierarchy
            && animador.runtimeAnimatorController != null) return animador;

        foreach (var a in GetComponentsInChildren<Animator>())
            if (a.gameObject.activeInHierarchy && a.isHuman && a.runtimeAnimatorController != null)
            {
                animador = a;
                temParametro = TemParametro("Agachado");
                return a;
            }
        return null;
    }

    private bool TemParametro(string nome)
    {
        if (animador == null || animador.runtimeAnimatorController == null) return false;
        foreach (var p in animador.parameters) if (p.name == nome) return true;
        return false;
    }

    private void Update()
    {
        if (capsula == null || controlador == null) return;


        // no ar nao agacha: sentar no meio do pulo bagunca a capsula e a fase do pulo
        bool quer = InputReader.Crouch || debugAgachar;
        if (!controlador.Grounded) quer = agachado;

        if (quer) agachado = true;
        else if (agachado)
        {
            if (TemEspacoPraLevantar()) { agachado = false; BloqueadoPorTeto = false; }
            else BloqueadoPorTeto = true;
        }

        blend = Mathf.MoveTowards(blend, agachado ? 1f : 0f, velTransicao * Time.deltaTime);

        // ---------- capsula: encolhe pelo TOPO, pe fica no lugar ----------
        // So escreve quando MUDA de verdade. Reescrever height/center do
        // CharacterController todo quadro faz o motor recriar a capsula e
        // reassentar o corpo no chao - com isso o impulso do pulo some.
        float h = Mathf.Lerp(alturaEmPe, alturaAgachado, blend);
        if (Mathf.Abs(capsula.height - h) > 0.0005f)
        {
            capsula.height = h;
            capsula.center = new Vector3(centroEmPe.x, h * 0.5f, centroEmPe.z);
        }

        // ---------- velocidade ----------
        controlador.MoveSpeed = Mathf.Lerp(velEmPe, velAgachado, blend);
        if (agachado && entrada != null) entrada.sprint = false;

        // ---------- camera ----------
        if (cameraJogo != null) cameraJogo.OffsetPivo = new Vector3(0f, -descerCamera * blend, 0f);

        // ---------- animator ----------
        var an = Animador();
        if (temParametro && an != null)
        {
            an.SetBool(HAgachado, agachado);
            an.SetFloat(HAgachamento, blend);
        }

        if (anim != null) anim.SetCrouching(agachado);
    }

    /// <summary>
    /// Cabe em pe? Sobe uma esfera do peito ate onde a cabeca ficaria. Se bater
    /// em alguma coisa, continua agachado.
    /// </summary>
    private bool TemEspacoPraLevantar()
    {
        float r = Mathf.Max(0.05f, capsula.radius - 0.01f);
        Vector3 de = transform.position + Vector3.up * (capsula.height - r);
        float subir = (alturaEmPe - capsula.height) + folgaTeto;
        if (subir <= 0.001f) return true;

        RaycastHit hit;
        return !Physics.SphereCast(de, r, Vector3.up, out hit, subir, teto, QueryTriggerInteraction.Ignore);
    }

    private void OnDisable()
    {
        // deixa tudo como estava: sem isto o jogador ressuscita anao
        if (capsula != null) { capsula.height = alturaEmPe; capsula.center = centroEmPe; }
        if (controlador != null) controlador.MoveSpeed = velEmPe;
        if (cameraJogo != null) cameraJogo.OffsetPivo = Vector3.zero;
    }
}
