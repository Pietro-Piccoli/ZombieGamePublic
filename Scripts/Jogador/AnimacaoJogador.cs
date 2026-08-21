using System.Collections.Generic;
using UnityEngine;
using StarterAssets;

/// <summary>
/// LIGACAO ENTRE O JOGO E O ANIMATOR (Pro Rifle Pack, personagem MOTTA).
///
/// A locomocao agora e UMA arvore 2D Cartesiana com 25 clipes (parado + 8
/// andando + 8 correndo + 8 sprintando), e cada clipe fica na arvore na sua
/// VELOCIDADE REAL medida do proprio arquivo (root motion do Mixamo). Ex.:
/// 'run forward' anda 4,35 m/s, 'sprint left' anda 6,89 m/s.
///
/// Por isso aqui nao se escreve mais direcao normalizada + multiplicador de
/// tempo: escreve-se a VELOCIDADE EM METROS POR SEGUNDO em VelX/VelY, e a
/// arvore escolhe sozinha a mistura de clipes que anda exatamente naquela
/// velocidade. E assim que se mata patinacao de pe de verdade - nao esticando
/// o tempo de um clipe so.
///
/// Parametros escritos aqui: VelX, VelY, Andando, Mira, Pistola, Giro,
/// Recarregar, e o peso da camada de arma.
/// </summary>
[DefaultExecutionOrder(20)]
public class AnimacaoJogador : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Animator animator;

    [Header("Camada de arma")]
    [SerializeField] private string nomeCamadaArma = "Arma";
    [SerializeField] private string nomeCamadaGranada = "Granada";
    [Tooltip("Quao rapido a pose de mira entra e sai.")]
    [SerializeField] private float velPesoArma = 12f;
    [Tooltip("Amortecimento da velocidade que alimenta a arvore. Baixo demais treme, alto demais atrasa a virada.")]
    [SerializeField] private float amortecimento = 0.08f;

    private WeaponController armas;
    private CameraJogo cameraJogo;
    private StarterAssetsInputs entrada;
    private ThirdPersonController controlador;
    private int idxArma = -1;
    private float pesoArma;
    private int idxGranada = -1;
    private float pesoGranada;
    private float fimGranada = -99f;
    private Transform maoGranada;

    private static readonly int HVelX = Animator.StringToHash("VelX");
    private static readonly int HVelY = Animator.StringToHash("VelY");
    private static readonly int HAndando = Animator.StringToHash("Andando");
    private static readonly int HMira = Animator.StringToHash("Mira");
    private static readonly int HPistola = Animator.StringToHash("Pistola");
    private static readonly int HRecarregar = Animator.StringToHash("Recarregar");
    private static readonly int HGiro = Animator.StringToHash("Giro");
    private static readonly int HMotionSpeed = Animator.StringToHash("MotionSpeed");
    private static readonly int HGranada = Animator.StringToHash("Granada");
    private static readonly int HArremesso = Animator.StringToHash("Arremesso");

    // giro do corpo por segundo, pro turn-in-place
    [Header("Passada casada com a velocidade")]
    [Tooltip("Teto do ajuste de cadencia. 0,3 = a animacao pode acelerar ou frear ate 30% pra casar com a velocidade real.")]
    [SerializeField] private float tetoAntiPatinacao = 0.3f;

    private readonly List<AnimatorClipInfo> infoClipes = new List<AnimatorClipInfo>();

    private float yawAnterior;
    private float giroSuave;
    private bool yawIniciado;

    public Animator Animator { get { return animator; } }

    private void Awake()
    {
        if (animator == null || !animator.gameObject.activeInHierarchy)
        {
            foreach (var a in GetComponentsInChildren<Animator>())
                if (a.gameObject.activeInHierarchy) { animator = a; break; }
        }
        armas = GetComponent<WeaponController>();
        entrada = GetComponent<StarterAssetsInputs>();
        controlador = GetComponent<ThirdPersonController>();
        cameraJogo = FindAnyObjectByType<CameraJogo>();

        if (animator != null)
        {
            animator.applyRootMotion = false;
            for (int i = 0; i < animator.layerCount; i++)
                if (animator.GetLayerName(i) == nomeCamadaArma) idxArma = i;
            for (int i = 0; i < animator.layerCount; i++)
                if (animator.GetLayerName(i) == nomeCamadaGranada) idxGranada = i;
        }
    }

    private void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        // ---------- VELOCIDADE EM M/S NO ESPACO DO CORPO ----------
        // A direcao vem do INPUT e o modulo vem do CONTROLADOR.
        // CharacterController.velocity reporta ZERO neste projeto mesmo com o
        // boneco a 4 m/s - ja custou caro confiar nele uma vez.
        // Como o corpo segue a camera, o input ja esta no espaco do corpo.
        Vector2 dir = entrada != null ? entrada.move : Vector2.zero;
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        float vel = controlador != null ? controlador.CurrentSpeed : 0f;
        Vector2 v = dir * vel;

        animator.SetFloat(HVelX, v.x, amortecimento, Time.deltaTime);
        animator.SetFloat(HVelY, v.y, amortecimento, Time.deltaTime);
        animator.SetFloat(HAndando, v.magnitude, amortecimento, Time.deltaTime);

        animator.SetFloat(HMotionSpeed, PassadaCasada(v));

        // ---------- GIRO DO CORPO -> turn in place ----------
        // Parado girando a camera, o corpo roda junto e os pes escorregavam.
        // Agora as pernas fazem o 'turn 90' do pack. So leitura: nao mexe na
        // rotacao de ninguem.
        float yawAgora = transform.eulerAngles.y;
        if (!yawIniciado) { yawAnterior = yawAgora; yawIniciado = true; }
        float dGiro = Mathf.DeltaAngle(yawAnterior, yawAgora);
        yawAnterior = yawAgora;
        float giroBruto = Time.deltaTime > 0.0001f ? dGiro / Time.deltaTime : 0f;
        giroSuave = Mathf.Lerp(giroSuave, giroBruto, 1f - Mathf.Exp(-10f * Time.deltaTime));
        animator.SetFloat(HGiro, Mathf.Clamp(giroSuave, -220f, 220f));

        if (armas != null && armas.CurrentWeapon != null)
            animator.SetBool(HPistola, armas.CurrentWeapon.empunhadura == GripType.Pistola);

        // ---------- CAMADA DE ARMA ----------
        // Os clipes do pack JA seguram o rifle andando, correndo e agachado.
        // Entao esta camada nao serve mais pra 'colocar a arma na mao': ela so
        // entra pra POSE DE MIRA, e com peso igual ao quanto se esta mirando.
        // (Antes ela ficava ligada sempre e precisava ser desligada na corrida
        //  pra o boneco nao correr de tronco duro.)
        if (idxArma >= 0)
        {
            float blend = cameraJogo != null ? cameraJogo.AimBlend : 0f;
            bool temArma = armas != null && armas.CurrentWeapon != null;
            float alvo = temArma ? blend : 0f;
            pesoArma = Mathf.MoveTowards(pesoArma, alvo, velPesoArma * Time.deltaTime);
            animator.SetLayerWeight(idxArma, pesoArma);
            animator.SetFloat(HMira, blend, 0.04f, Time.deltaTime);
        }

        // ---------- CAMADA DE GRANADA ----------
        // Entra rapido (0,06 s) pra o arremesso nao parecer atrasado e sai
        // mais devagar (0,18 s) pra a arma voltar pra pose de mira macio.
        if (idxGranada >= 0)
        {
            bool jogando = Arremessando;
            float velG = jogando ? 1f / 0.06f : 1f / 0.18f;
            pesoGranada = Mathf.MoveTowards(pesoGranada, jogando ? 1f : 0f, velG * Time.deltaTime);
            animator.SetLayerWeight(idxGranada, pesoGranada);
        }
    }

    /// <summary>
    /// PASSADA CASADA COM A VELOCIDADE (conta direta, nao malha fechada).
    ///
    /// A arvore mistura clipes de comprimentos diferentes (andar 1,0 s, correr
    /// 0,5 s). A Unity sincroniza os filhos pelo tempo NORMALIZADO: todos passam
    /// a durar o mesmo tanto, que e a media dos comprimentos pesada pelos pesos.
    /// Efeito colateral: cada clipe passa a andar a velocidade_dele *
    /// (comprimento_dele / duracao_da_mistura), e a soma disso NAO da a
    /// velocidade que o jogo pediu. A 2,42 m/s sobrava ~0,27 m/s de pe
    /// escorregando por mais bem posicionados que os clipes estivessem.
    ///
    /// Aqui essa conta e feita explicitamente com os pesos que a arvore esta
    /// usando neste quadro, e o resto vira multiplicador de velocidade.
    ///
    /// (A primeira versao media a patinacao e corrigia por integrador. Ficava
    ///  presa no teto: a realimentacao tem dois equilibrios e ela caia no
    ///  errado. Conta direta nao oscila.)
    /// </summary>
    private float PassadaCasada(Vector2 velLocal)
    {
        float velCorpo = velLocal.magnitude;
        if (velCorpo < 0.3f) return 1f;

        animator.GetCurrentAnimatorClipInfo(0, infoClipes);
        if (infoClipes.Count == 0) return 1f;

        float duracaoMistura = 0f;
        for (int i = 0; i < infoClipes.Count; i++)
        {
            var c = infoClipes[i].clip;
            if (c != null) duracaoMistura += infoClipes[i].weight * c.length;
        }
        if (duracaoMistura < 0.0001f) return 1f;

        float velAnim = 0f;
        for (int i = 0; i < infoClipes.Count; i++)
        {
            var c = infoClipes[i].clip;
            if (c == null) continue;
            Vector3 s = c.averageSpeed; s.y = 0f;
            velAnim += infoClipes[i].weight * s.magnitude * (c.length / duracaoMistura);
        }
        if (velAnim < 0.15f) return 1f;

        return Mathf.Clamp(velCorpo / velAnim, 1f - tetoAntiPatinacao, 1f + tetoAntiPatinacao);
    }


    public void SetPistol(bool v)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetBool(HPistola, v);
    }

    public void SetCrouching(bool v)
    {
    }

    /// <summary>
    /// Nao ha estado de tiro: o recuo e procedural (RecuoArma) e foi afinado do
    /// jeito que o Pietro pediu - a arma nao sobe, o cotovelo vai pra tras.
    /// Uma animacao de tiro brigaria com ele.
    /// </summary>
    public void PlayShoot()
    {
    }

    public void PlayReload()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetTrigger(HRecarregar);
    }

    // ---------------------------------------------------------------
    // ARREMESSO DE GRANADA
    //
    // QUEM ARREMESSA E A MAO ESQUERDA. A direita segura a AK (o socket da
    // arma esta em hand_r). Medindo as duas maos quadro a quadro no arquivo,
    // o pico de velocidade pra frente e 4,56 m/s na ESQUERDA contra 1,60 m/s
    // na direita - o clipe nao espelhado e canhoto de proposito, que e o que
    // serve pra quem esta de fuzil na mao direita.
    //
    // O clipe (Anim_TossGrenade_UE5M, 2,667 s) tem preparacao longa demais pra
    // um jogo de horda: o gesto de verdade so comeca em t=1,25 s (mao desce
    // pro lado), o braco arma por cima do ombro ate t=1,80 s e a granada deixa
    // a mao no pico da chicotada, t=1,90 s. Quase dois segundos de espera.
    //
    // Entao o clipe entra JA em t=1,22 s e roda 1,55x. Sobra 0,44 s ate soltar
    // e 0,93 s de animacao inteira - a mesma janela de Call of Duty e Gears of
    // War, que ficam entre 0,45 s e 0,6 s ate a granada sair da mao.
    //
    // Roda em camada propria (MascaraTroncoGranada) que leva SO OS BRACOS E
    // DEDOS - sem cabeca, sem tronco, sem pernas.
    //
    // Isso nao e preciosismo: no clipe o boneco esta com o quadril girado -70
    // graus e a cabeca -33, ou seja, olhando 37 graus a direita do proprio
    // corpo. A camada nao leva o Root junto (o corpo tem que continuar virado
    // pra onde a camera aponta), entao esse giro nao se cancela: medido em
    // jogo, a cabeca ia a +96 graus e o boneco arremessava olhando pro lado.
    // Tirando a cabeca ainda sobravam 36 graus no quadro do arremesso, porque
    // a cabeca herda o giro do peito. Tirando tambem o tronco, a cabeca fica
    // nos mesmos 7,6 graus do estado parado do comeco ao fim, e o braco ainda
    // faz a chicotada inteira. As pernas continuam correndo pela Base Layer.
    // ---------------------------------------------------------------

    private const float ClipeDuracao = 2.667f;
    private const float ClipeInicio = 1.22f;   // onde o gesto de fato comeca
    private const float ClipeSoltar = 1.90f;   // medido: pico da mao esquerda
    private const float ClipeVeloc = 1.55f;    // = speed do estado Arremesso

    /// <summary>Segundos entre apertar a tecla e a granada sair da mao.</summary>
    public static float TempoAteSoltar
    {
        get { return (ClipeSoltar - ClipeInicio) / ClipeVeloc; }
    }

    /// <summary>Duracao total da animacao de arremesso.</summary>
    public static float TempoDoArremesso
    {
        get { return (ClipeDuracao - ClipeInicio) / ClipeVeloc; }
    }

    public bool Arremessando { get { return Time.time < fimGranada; } }

    /// <summary>
    /// Mao que solta a granada: a ESQUERDA. A direita esta ocupada com a arma.
    /// Serve pra granada nascer exatamente de onde o braco soltou.
    /// </summary>
    public Transform MaoDaGranada
    {
        get
        {
            if (maoGranada == null && animator != null && animator.isHuman)
                maoGranada = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            return maoGranada;
        }
    }

    /// <summary>Dispara a animacao. false = nao deu (sem animator ou sem camada).</summary>
    public bool PlayGranada()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        if (idxGranada < 0 || !animator.HasState(idxGranada, HArremesso)) return false;

        animator.SetTrigger(HGranada);
        // duracao do fundido em unidades normalizadas do estado de destino:
        // 0,10 s / (2,667 s / 1,55) = 0,058
        animator.CrossFade(HArremesso, 0.058f, idxGranada, ClipeInicio / ClipeDuracao);
        fimGranada = Time.time + TempoDoArremesso;
        return true;
    }
}
