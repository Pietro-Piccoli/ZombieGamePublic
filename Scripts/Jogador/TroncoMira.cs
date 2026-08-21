using UnityEngine;

/// <summary>
/// TRONCO NA MIRA - dois efeitos, ambos em rotacao LIMPA, depois do Animator:
///
///  1) GIRO LATERAL (yaw): alguns graus pra DIREITA enquanto mira, pra leitura
///     de que ele aponta pro centro da tela.
///
///  2) MIRA VERTICAL (pitch): a coluna, o peito e a cabeca acompanham o angulo
///     vertical da camera. Olhou pra cima, ele aponta pra cima; olhou pra
///     baixo, aponta pra baixo. Como a arma esta presa na mao, ela acompanha
///     junto - o cano segue a mira de verdade, sem sair da mao.
///
/// Os eixos sao separados de proposito: yaw no eixo vertical do MUNDO, pitch
/// no eixo lateral do CORPO. Nada de eixo livre - eixo livre injeta roll e era
/// isso que deixava o boneco corcunda nas tentativas antigas.
///
/// As PERNAS nao sao tocadas. Nenhum parametro de Animator e escrito.
/// </summary>
[DefaultExecutionOrder(70)]
public class TroncoMira : MonoBehaviour
{
    [Header("1) Giro lateral ao mirar")]
    [Tooltip("Graus que o tronco gira pra DIREITA quando esta mirando.")]
    [SerializeField] private float grausMirando = 7f;
    [Range(0f, 1f)]
    [SerializeField] private float pesoColunaYaw = 0.4f;
    [Range(0f, 1f)]
    [SerializeField] private float pesoPeitoYaw = 0.6f;

    [Header("2) Mira vertical (cima / baixo)")]
    [Tooltip("Quanto do angulo da camera o tronco acompanha MIRANDO. 1 = acompanha inteiro.")]
    [Range(0f, 1.5f)]
    [SerializeField] private float forcaMirando = 1f;
    [Tooltip("Quanto acompanha SEM mirar (0 = so mira quando aperta o botao direito).")]
    [Range(0f, 1f)]
    [SerializeField] private float forcaSemMirar = 0.25f;

    [Header("Distribuicao do angulo vertical (soma = 1)")]
    [Range(0f, 1f)]
    [SerializeField] private float pesoColuna = 0.35f;
    [Range(0f, 1f)]
    [SerializeField] private float pesoPeito = 0.45f;
    [Range(0f, 1f)]
    [SerializeField] private float pesoCabeca = 0.20f;

    [Header("3) Travar o tronco ao mirar")]
    [Tooltip("Desconta o balanco que a caminhada faz no quadril, pro tronco e a arma ficarem parados enquanto mira. Nao mexe nas pernas nem na mira vertical.")]
    [SerializeField] private bool travarAoMirar = true;
    [Range(0f, 1f)]
    [Tooltip("1 = tronco totalmente travado. Baixe se quiser sobrar um resto de balanco.")]
    [SerializeField] private float forcaTrava = 1f;
    [Tooltip("Quao rapido a referencia acompanha a direcao real do corpo. Menor = trava mais firme.")]
    [SerializeField] private float velReferencia = 1f;
    [Tooltip("Teto da correcao em graus, so por seguranca.")]
    [SerializeField] private float limiteTrava = 25f;

    [Header("4) Convergencia: cano sempre na mira")]
    [Tooltip("Mede pra onde o cano aponta de verdade e corrige a sobra. E o que faz a diagonal fechar.")]
    [SerializeField] private bool convergirNaMira = true;
    [Range(0f, 1f)]
    [Tooltip("1 = fecha o erro todo. Abaixe se quiser deixar folga proposital.")]
    [SerializeField] private float ganhoConvergencia = 1f;
    [Range(1, 4)]
    [Tooltip("Repeticoes por quadro. 2 ja fecha, 3 fecha com folga.")]
    [SerializeField] private int iteracoes = 3;
    [Tooltip("Teto anatomico da torcao lateral do tronco, em graus.")]
    [SerializeField] private float limiteYaw = 50f;
    [Tooltip("Teto anatomico da inclinacao do tronco, em graus.")]
    [SerializeField] private float limitePitch = 60f;

    [Header("Suavizacao")]
    [SerializeField] private float suavidade = 14f;

    /// <summary>Diagnostico.</summary>
    public float YawAtual { get { return yawAtual; } }
    public float PitchAtual { get { return pitchAtual; } }

    private Animator animator;
    private CameraJogo cameraJogo;
    private Transform coluna, peito, cabeca, raiz;
    private float yawAtual;
    private float pitchAtual;
    private Transform quadril;
    private Quaternion refQuadril = Quaternion.identity;
    private bool refIniciada;
    private WeaponVisuals visuais;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        cameraJogo = FindAnyObjectByType<CameraJogo>();
        raiz = transform.parent != null ? transform.parent : transform;
        if (animator != null && animator.isHuman)
        {
            coluna = animator.GetBoneTransform(HumanBodyBones.Spine);
            peito = animator.GetBoneTransform(HumanBodyBones.Chest);
            cabeca = animator.GetBoneTransform(HumanBodyBones.Head);
            quadril = animator.GetBoneTransform(HumanBodyBones.Hips);
        }
    }

    private void LateUpdate()
    {
        if (cameraJogo == null || coluna == null || peito == null) return;

        float blend = cameraJogo.AimBlend;
        // ---------- 0) TRAVA DO TRONCO (mata o balanco do passo) ----------
        // O peito ja recebe a rotacao LOCAL da pose de mira, mas ele PENDURA no
        // quadril, e o quadril gira ~13 graus a cada passo da caminhada. Por isso
        // a arma balanca mesmo com os bracos parados: nao e o braco animando, e a
        // bacia carregando o tronco inteiro.
        //
        // Aqui a gente mede o quadril no espaco da RAIZ (que nao balanca), separa
        // o BALANCO da direcao real do corpo, e desconta o balanco na coluna.
        // Medir em relacao a raiz e o truque: virar a camera gira raiz e quadril
        // juntos, entao virar NAO gera correcao nenhuma - so o passo gera.
        //
        // As PERNAS nao sao tocadas. A mira vertical continua sendo aplicada
        // logo abaixo, por cima disto, entao mirar pra cima/baixo segue igual.
        if (travarAoMirar && quadril != null && blend > 0.001f)
        {
            Quaternion rel = Quaternion.Inverse(raiz.rotation) * quadril.rotation;
            if (!refIniciada) { refQuadril = rel; refIniciada = true; }

            // referencia lenta = pra onde o corpo REALMENTE aponta
            refQuadril = Quaternion.Slerp(refQuadril, rel, 1f - Mathf.Exp(-velReferencia * Time.deltaTime));

            // desvio = o balanco do passo
            Quaternion desvio = rel * Quaternion.Inverse(refQuadril);
            float ang; Vector3 eixo;
            desvio.ToAngleAxis(out ang, out eixo);
            if (ang > 180f) ang -= 360f;

            if (!float.IsNaN(eixo.x) && Mathf.Abs(ang) > 0.01f)
            {
                float corrigir = Mathf.Clamp(-ang * forcaTrava * blend, -limiteTrava, limiteTrava);
                Vector3 eixoMundo = raiz.rotation * eixo;
                coluna.rotation = Quaternion.AngleAxis(corrigir, eixoMundo) * coluna.rotation;
            }
        }
        else refIniciada = false;


        // ---------- 1) GIRO LATERAL (yaw no eixo vertical do mundo) ----------
        float alvoYaw = grausMirando * blend;
        yawAtual = Mathf.Lerp(yawAtual, alvoYaw, 1f - Mathf.Exp(-suavidade * Time.deltaTime));
        if (Mathf.Abs(yawAtual) > 0.05f)
        {
            coluna.rotation = Quaternion.AngleAxis(yawAtual * pesoColunaYaw, Vector3.up) * coluna.rotation;
            peito.rotation = Quaternion.AngleAxis(yawAtual * pesoPeitoYaw, Vector3.up) * peito.rotation;
        }

        // ---------- 2) MIRA VERTICAL (pitch no eixo lateral do corpo) ----------
        // Pitch da camera: positivo = olhando pra BAIXO.
        // Girar positivo no eixo 'right' do corpo inclina o tronco pra frente/baixo.
        float forca = Mathf.Lerp(forcaSemMirar, forcaMirando, blend);
        float alvoPitch = cameraJogo.Pitch * forca;
        pitchAtual = Mathf.Lerp(pitchAtual, alvoPitch, 1f - Mathf.Exp(-suavidade * Time.deltaTime));
        if (Mathf.Abs(pitchAtual) > 0.05f)
        {
            Vector3 eixo = raiz.right;
            coluna.rotation = Quaternion.AngleAxis(pitchAtual * pesoColuna, eixo) * coluna.rotation;
            peito.rotation = Quaternion.AngleAxis(pitchAtual * pesoPeito, eixo) * peito.rotation;
            if (cabeca != null)
                cabeca.rotation = Quaternion.AngleAxis(pitchAtual * pesoCabeca, eixo) * cabeca.rotation;
        }

        // ---------- 3) CONVERGENCIA: poe o CANO exatamente na mira ----------
        // As secoes 1 e 2 sao MALHA ABERTA: chutam um angulo e torcem que de certo.
        // Fecha bem no vertical (erro 0,9 grau) e falha feio na diagonal: andando de
        // lado, a pose de strafe gira o tronco e o cano sai 13,1 graus da mira - 4,2
        // metros de desvio a 20 metros.
        //
        // Aqui e MALHA FECHADA: mede pra onde o cano REALMENTE aponta, compara com o
        // raio da mira e corrige a sobra. Como corrigir muda o cano, repete algumas
        // vezes ate fechar.
        //
        // Os dois eixos sao tratados SEPARADOS de proposito - lateral no vertical do
        // mundo, vertical no lateral do corpo. Eixo livre (FromToRotation) injeta
        // roll, e era isso que deixava o boneco corcunda nas tentativas antigas.
        if (convergirNaMira && blend > 0.01f)
        {
            Transform modelo = ModeloArma();
            if (modelo != null)
            {
                Ray raio = cameraJogo.GetAimRay();
                float g = ganhoConvergencia * blend;

                for (int passo = 0; passo < iteracoes; passo++)
                {
                    // --- LATERAL (yaw): so no eixo vertical do MUNDO ---
                    Vector3 cano = modelo.TransformDirection(Vector3.forward);
                    Vector3 canoH = new Vector3(cano.x, 0f, cano.z);
                    Vector3 miraH = new Vector3(raio.direction.x, 0f, raio.direction.z);
                    if (canoH.sqrMagnitude > 1e-6f && miraH.sqrMagnitude > 1e-6f)
                    {
                        float errYaw = Vector3.SignedAngle(canoH.normalized, miraH.normalized, Vector3.up);
                        errYaw = Mathf.Clamp(errYaw, -limiteYaw, limiteYaw) * g;
                        if (Mathf.Abs(errYaw) > 0.01f)
                        {
                            coluna.rotation = Quaternion.AngleAxis(errYaw * pesoColunaYaw, Vector3.up) * coluna.rotation;
                            peito.rotation = Quaternion.AngleAxis(errYaw * pesoPeitoYaw, Vector3.up) * peito.rotation;
                        }
                    }

                    // --- VERTICAL (pitch): so no eixo lateral do CORPO ---
                    cano = modelo.TransformDirection(Vector3.forward);
                    float canoPitch = -Mathf.Asin(Mathf.Clamp(cano.y, -1f, 1f)) * Mathf.Rad2Deg;
                    float miraPitch = -Mathf.Asin(Mathf.Clamp(raio.direction.y, -1f, 1f)) * Mathf.Rad2Deg;
                    float errPitch = Mathf.Clamp(miraPitch - canoPitch, -limitePitch, limitePitch) * g;
                    if (Mathf.Abs(errPitch) > 0.01f)
                    {
                        Vector3 eixoLat = raiz.right;
                        coluna.rotation = Quaternion.AngleAxis(errPitch * pesoColuna, eixoLat) * coluna.rotation;
                        peito.rotation = Quaternion.AngleAxis(errPitch * pesoPeito, eixoLat) * peito.rotation;
                    }
                }
            }
        }
    }

    /// <summary>Transform do modelo da arma equipada. O +Z dele e a linha do cano.</summary>
    private Transform ModeloArma()
    {
        if (visuais == null) visuais = FindAnyObjectByType<WeaponVisuals>();
        if (visuais == null || visuais.CurrentModel == null) return null;
        return visuais.CurrentModel.transform;
    }
}
