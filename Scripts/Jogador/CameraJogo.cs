using UnityEngine;

/// <summary>
/// CAMERA DE TERCEIRA PESSOA (sistema novo, refeito do zero).
///
/// Orbita um pivo no peito do player. Botao direito = mira (ADS):
/// aproxima, muda o ombro e fecha o FOV. Colisao por SphereCast.
///
/// Mantem a MESMA API publica que o resto do jogo usa:
/// Yaw, Pitch, MinPitch, MaxPitch, IsAiming, AimBlend, GetAimRay(), SetTarget().
/// </summary>
[DefaultExecutionOrder(40)]
public class CameraJogo : MonoBehaviour
{
    [Header("Alvo")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 alturaPivo = new Vector3(0f, 1.55f, 0f);
    [SerializeField] private float seguirRapidez = 22f;

    [Header("Mouse")]
    [SerializeField] private float sensibilidade = 2.2f;
    [SerializeField] private float pitchMin = -40f;
    [SerializeField] private float pitchMax = 65f;
    [Tooltip("Quao rapido a mira assenta depois do tranco do tiro.")]
    [SerializeField] private float velVoltaRecuo = 9f;
    [SerializeField] private bool travarCursor = true;

    [Header("Quadril (sem mirar)")]
    [SerializeField] private float distQuadril = 4.3f;
    [SerializeField] private Vector3 ombroQuadril = new Vector3(0.65f, 0f, 0f);
    [SerializeField] private float fovQuadril = 68f;

    [Header("Mira (ADS)")]
    [SerializeField] private float distMira = 1.6f;
    [SerializeField] private Vector3 ombroMira = new Vector3(0.5f, 0.08f, 0f);
    [SerializeField] private float fovMira = 42f;
    [Range(0.1f, 1f)]
    [SerializeField] private float sensNaMira = 0.55f;
    [SerializeField] private bool inverterY = false;
    [SerializeField] private float velBlendMira = 12f;

    [Header("Colisao (Ground e Obstacle; nunca Player/Enemy)")]
    [SerializeField] private LayerMask mascaraColisao = ~0;
    [SerializeField] private float raioCamera = 0.25f;
    [SerializeField] private float folgaParede = 0.2f;
    [SerializeField] private float velVoltarParede = 8f;

    [Header("Corpo colado na camera vira so-sombra")]
    [SerializeField] private float distEsconderCorpo = 0.85f;

    /// <summary>Teste automatizado: forca a mira sem apertar o mouse.</summary>
    public static bool debugForcarMira = false;

    public float Yaw => yaw;
    public float Pitch => pitch + recuoPitch;
    public float MinPitch => pitchMin;
    public float MaxPitch => pitchMax;
    public bool IsAiming { get; private set; }
    public float AimBlend => blendMira;

    /// <summary>
    /// Deslocamento extra do pivo. Agachar usa isto pra descer a camera junto
    /// com o corpo - sem isso a camera fica na altura do ombro em pe enquanto o
    /// boneco esta la embaixo, e a mira sai por cima da cabeca dele.
    /// </summary>
    public Vector3 OffsetPivo { get; set; }

    private Camera cam;
    private Transform camT;
    private float yaw;
    private float pitch;
    private float recuoPitch;
    private float recuoYaw;
    private float blendMira;
    private float distAtual;
    private Vector3 pivoSuave;
    private Renderer[] renderersCorpo;
    private bool corpoEscondido;

    private void Awake()
    {
        cam = GetComponentInChildren<Camera>();
        if (cam == null)
        {
            Debug.LogError("[CameraJogo] A Main Camera precisa ser FILHA deste objeto.", this);
            enabled = false;
            return;
        }
        camT = cam.transform;
        distAtual = distQuadril;
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            pivoSuave = target.position + alturaPivo;
            renderersCorpo = target.GetComponentsInChildren<SkinnedMeshRenderer>();
        }
        if (travarCursor && Application.isPlaying)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        IsAiming = InputReader.Aim || debugForcarMira;
        blendMira = Mathf.MoveTowards(blendMira, IsAiming ? 1f : 0f, velBlendMira * Time.deltaTime);
        cam.fieldOfView = Mathf.Lerp(fovQuadril, fovMira, blendMira);

        float sens = sensibilidade * Mathf.Lerp(1f, sensNaMira, blendMira);
        yaw += InputReader.MouseX * sens;
        pitch = Mathf.Clamp(pitch - InputReader.MouseY * sens * (inverterY ? -1f : 1f), pitchMin, pitchMax);

        // ---------- RECUO: o tranco volta sozinho ----------
        // A parte que NAO volta ja foi somada no pitch/yaw de verdade la em
        // AplicarRecuo, entao o jogador precisa puxar pra baixo numa rajada longa.
        // Isto aqui e so a parte que a arma "assenta" de volta.
        float k = 1f - Mathf.Exp(-velVoltaRecuo * Time.deltaTime);
        recuoPitch = Mathf.Lerp(recuoPitch, 0f, k);
        recuoYaw = Mathf.Lerp(recuoYaw, 0f, k);
    }

    private void LateUpdate()
    {
        if (target == null || camT == null) return;

        Vector3 alvoPivo = target.position + alturaPivo + OffsetPivo;
        pivoSuave = Vector3.Lerp(pivoSuave, alvoPivo, 1f - Mathf.Exp(-seguirRapidez * Time.deltaTime));
        transform.position = pivoSuave;

        Quaternion rot = Quaternion.Euler(pitch + recuoPitch, yaw + recuoYaw, 0f);
        transform.rotation = rot;

        Vector3 ombro = Vector3.Lerp(ombroQuadril, ombroMira, blendMira);
        float distAlvo = Mathf.Lerp(distQuadril, distMira, blendMira);

        Vector3 pivoMundo = transform.position + rot * ombro;
        Vector3 dirTras = rot * Vector3.back;

        float distPermitida = distAlvo;
        RaycastHit hit;
        if (Physics.SphereCast(pivoMundo, raioCamera, dirTras, out hit, distAlvo,
                mascaraColisao, QueryTriggerInteraction.Ignore))
            distPermitida = Mathf.Max(0f, hit.distance - folgaParede);

        distAtual = distPermitida < distAtual
            ? distPermitida
            : Mathf.Lerp(distAtual, distPermitida, velVoltarParede * Time.deltaTime);

        camT.position = pivoMundo + dirTras * distAtual;
        camT.rotation = rot;

        bool esconder = distAtual < distEsconderCorpo;
        if (esconder != corpoEscondido && renderersCorpo != null)
        {
            corpoEscondido = esconder;
            foreach (var r in renderersCorpo)
                if (r != null) r.shadowCastingMode = esconder
                    ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                    : UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }

    /// <summary>Raio do CENTRO DA TELA. Bala e mira convergente usam isto.</summary>
    public Ray GetAimRay()
    {
        return cam != null
            ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : new Ray(transform.position, transform.forward);
    }

    /// <summary>
    /// Tranco do tiro na MIRA. Chamado pelo WeaponController a cada disparo.
    ///
    /// O recuo e dividido em duas partes:
    ///   - a que VOLTA sozinha  -> vai pro offset recuoPitch/recuoYaw (so visual)
    ///   - a que NAO volta      -> entra no pitch/yaw de verdade, entao a mira
    ///     realmente sobe e o jogador tem que puxar pra baixo numa rajada.
    ///
    /// Como a arma segue a mira (a convergencia do TroncoMira), mexer aqui faz o
    /// corpo inteiro acompanhar o coice - por isso o tranco entra na camera e nao
    /// direto no tronco.
    /// </summary>
    public void AplicarRecuo(float grausCima, float grausLado, float recuperacao)
    {
        recuperacao = Mathf.Clamp01(recuperacao);
        float fica = 1f - recuperacao;

        pitch = Mathf.Clamp(pitch - grausCima * fica, pitchMin, pitchMax);
        yaw += grausLado * fica;

        recuoPitch -= grausCima * recuperacao;
        recuoYaw += grausLado * recuperacao;
    }

    /// <summary>Chamado pelo menu de opcoes. Sensibilidade e mira sao do jogador, nao minhas.</summary>
    public void AplicarOpcoes(float sens, float sensMira, bool inverterY, float fovQuadrilNovo)
    {
        sensibilidade = sens;
        sensNaMira = sensMira;
        this.inverterY = inverterY;
        float delta = fovQuadrilNovo - fovQuadril;
        fovQuadril = fovQuadrilNovo;
        fovMira = Mathf.Clamp(fovMira + delta, 20f, 100f);   // a mira acompanha o campo de visao
    }


    public void SetTarget(Transform t)
    {
        target = t;
        if (t != null) renderersCorpo = t.GetComponentsInChildren<SkinnedMeshRenderer>();
    }
}
