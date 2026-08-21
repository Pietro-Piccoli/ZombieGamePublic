using UnityEngine;
using StarterAssets;

/// <summary>
/// PONTE entre o input do projeto e o ThirdPersonController OFICIAL da Unity.
///
/// Regra de SHOOTER: o controller oficial gira o boneco pra direcao do
/// MOVIMENTO (bom pra aventura, ruim pra tiro: ao parar ele fica torto).
/// Aqui o corpo segue a CAMERA e as pernas fazem strafe.
///
/// Mirando: o corpo ganha alguns graus a mais pra DIREITA, pra leitura de
/// que ele aponta pro centro da tela (o tronco complementa via TroncoParaMira).
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(StarterAssetsInputs))]
public class PonteEntrada : MonoBehaviour
{
    [Header("Postura de shooter")]
    [SerializeField] private bool corpoSegueCamera = true;
    [SerializeField] private float giroCorpo = 720f;
    [Tooltip("Graus a mais pra DIREITA quando esta mirando.")]
    [SerializeField] private float grausExtraMirando = 5f;

    [Header("Passada")]
    [Tooltip("Velocidade natural medida do clipe de ANDAR (m/s).")]
    [SerializeField] private float passadaAndar = 1.61f;
    [Tooltip("Velocidade natural medida do clipe de CORRER (m/s).")]
    [SerializeField] private float passadaCorrer = 3.87f;
    [Tooltip("Velocidade natural medida do clipe de AGACHADO andando (m/s).")]
    [SerializeField] private float passadaAgachado = 1.0f;

    public static Vector2 debugMover = Vector2.zero;
    public static bool debugMira = false;
    /// <summary>So pra teste: liga a corrida sem precisar segurar Shift.</summary>
    public static bool debugCorrer = false;

    private StarterAssetsInputs entrada;
    private ThirdPersonController controlador;
    private CameraJogo cameraJogo;
    [Tooltip("Quanto o modelo desce ao correr, em metros. Os clipes de corrida do pack sao ~9 cm mais altos que os de caminhada, e sem isto o boneco corre flutuando.")]
    [SerializeField] private float descerNaCorrida = 0.087f;
    [Tooltip("Quao rapido essa descida entra e sai, em m/s.")]
    [SerializeField] private float velDescida = 0.6f;
    private float baseAlturaY;
    private bool baseAlturaOk;
    private float descidaAtual;
    private CharacterController capsula;
    private Animator animador;
    private bool correndo;
    private Agachar agachar;

    private static readonly int HMirando = Animator.StringToHash("Mirando");
    private static readonly int HCorrendo = Animator.StringToHash("Correndo");
    private static readonly int HMotionSpeed = Animator.StringToHash("MotionSpeed");

    private void Awake()
    {
        entrada = GetComponent<StarterAssetsInputs>();
        controlador = GetComponent<ThirdPersonController>();
        capsula = GetComponent<CharacterController>();
        cameraJogo = FindAnyObjectByType<CameraJogo>();
        animador = GetComponentInChildren<Animator>();
        agachar = GetComponent<Agachar>();
    }

    private void Update()
    {
        Vector2 mover = InputReader.Move + debugMover;
        if (mover.sqrMagnitude > 1f) mover.Normalize();

        bool mirando = (cameraJogo != null && cameraJogo.IsAiming) || debugMira;
        bool querCorrer = (InputReader.Sprint || debugCorrer) && !mirando && mover.sqrMagnitude > 0.05f;

        entrada.MoveInput(mover);
        entrada.SprintInput(querCorrer);
        entrada.AimInput(mirando);
        entrada.ShootInput(InputReader.Fire);
        entrada.analogMovement = false;

        correndo = querCorrer;

        if (controlador == null) return;

        if (corpoSegueCamera && cameraJogo != null)
        {
            controlador.SetRotateOnMove(false);
            float alvoYaw = cameraJogo.Yaw + (mirando ? grausExtraMirando : 0f);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.Euler(0f, alvoYaw, 0f),
                giroCorpo * Time.deltaTime);
        }
        else
        {
            controlador.SetRotateOnMove(true);
        }

        EscreverAnimator();
    }

    private void LateUpdate()
    {
        AssentarNaCorrida();
    }

    /// <summary>
    /// Escreve no Animator DENTRO DO UPDATE, de proposito.
    ///
    /// Isto estava no LateUpdate e por isso nao valia nada: a Unity avalia o
    /// Animator ENTRE o Update e o LateUpdate, entao o valor escrito depois so
    /// seria lido no quadro seguinte - e o ThirdPersonController, que roda no
    /// Update, sobrescrevia MotionSpeed com 1 antes disso. Resultado medido: a
    /// passada tocava sempre em 1,00 enquanto o corpo andava a 2,42 m/s, ou
    /// seja, sobra de 0,8 m/s de pe escorregando no chao.
    /// </summary>
    private void EscreverAnimator()
    {
        if (animador == null || animador.runtimeAnimatorController == null) return;

        animador.SetBool(HCorrendo, correndo);

        // CADENCIA DA PASSADA = velocidade real / passada natural do clipe em uso.
        //
        // A velocidade vem do CONTROLADOR, nao de CharacterController.velocity:
        // a capsula estava reportando ZERO com o boneco correndo a 4 m/s, entao
        // isto travava no piso do clamp (0.35) e as pernas quase nao se mexiam
        // enquanto o corpo voava. CurrentSpeed e o numero que o proprio
        // ThirdPersonController usa pra andar - nao tem como divergir.
        if (controlador != null)
        {
            bool agachado = agachar != null && agachar.Agachado;
            float referencia = agachado ? passadaAgachado : (correndo ? passadaCorrer : passadaAndar);
            if (referencia > 0.01f)
                animador.SetFloat(HMotionSpeed, Mathf.Clamp(controlador.CurrentSpeed / referencia, 0.35f, 2.2f));
        }
    }

    /// <summary>
    /// CORRIDA FLUTUANDO - conserto de altura.
    ///
    /// Os clipes de corrida do pack foram feitos com o boneco mais alto que os de
    /// caminhada. Medido rodando o Animator o ciclo inteiro: o pe mais baixo fica
    /// em 0,105 correndo contra 0,018 andando, e o quadril 11 cm mais alto. Ou
    /// seja, ele corre no ar - e nao tem ajuste de blend tree que resolva, porque
    /// o desvio esta dentro da animacao.
    ///
    /// Aqui o MODELO desce enquanto a corrida vale, e volta ao normal andando. So
    /// o visual desce: capsula, fisica e mira nao sao tocadas.
    /// </summary>
    private void AssentarNaCorrida()
    {
        if (animador == null) return;
        Transform modelo = animador.transform;

        if (!baseAlturaOk) { baseAlturaY = modelo.localPosition.y; baseAlturaOk = true; }

        float alvo = correndo ? -descerNaCorrida : 0f;
        descidaAtual = Mathf.MoveTowards(descidaAtual, alvo, velDescida * Time.deltaTime);

        Vector3 lp = modelo.localPosition;
        lp.y = baseAlturaY + descidaAtual;
        modelo.localPosition = lp;
    }
}
