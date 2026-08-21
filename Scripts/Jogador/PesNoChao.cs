using UnityEngine;

/// <summary>
/// PE NO CHAO (Foot IK).
///
/// PROBLEMA: cada clipe traz o pe numa altura propria e nenhuma bate com o
/// terreno real. Medindo os VERTICES DEFORMADOS da malha neste projeto, o Walk
/// do Voyager enterra o pe 8,6 cm no chao, o Run enterra 3,6 cm e o agachar do
/// pack novo flutua 8 cm. Em escada e ladeira, que e o que a favela tem, erra
/// sempre e erra mais.
///
/// COMO SABER SE O PE ESTA PLANTADO
/// Essa e a parte que decide se o sistema fica bom ou vira desastre, e eu errei
/// duas vezes antes de acertar:
///   1a tentativa: corrigir todo pe que achasse chao. O pe que esta NO AR no meio
///      do passo tambem acha chao la embaixo, entao ele era puxado pra baixo e
///      levava o corpo junto - o personagem afundava 4,6 cm.
///   2a tentativa: considerar plantado o pe a menos de 12 cm do chao. No meio do
///      passo o pe passa por essa faixa, entao o problema so diminuiu (-2,8 cm).
///   Esta versao usa o sinal certo: PE PLANTADO FICA PARADO NO MUNDO enquanto o
///   corpo anda. O pe de balanco viaja varios m/s; o plantado fica perto de zero.
///   A separacao e enorme e nao depende de altura nenhuma.
///
/// Pe ENTERRADO sempre e corrigido, plantado ou nao - atravessar o chao nunca
/// esta certo. Pe no ar so e abaixado se estiver plantado (degrau abaixo).
///
/// No ar (pulo) o sistema sai de cena inteiro.
///
/// Precisa de "IK Pass" na camada do Animator - o script liga sozinho no Awake.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PesNoChao : MonoBehaviour
{
    [Header("Liga/desliga")]
    [SerializeField] private bool ativo = true;

    [Header("Terreno")]
    [Tooltip("O que conta como chao. Igual ao GroundLayers do controlador.")]
    [SerializeField] private LayerMask chao = ~0;
    [SerializeField] private float alturaRaio = 0.7f;
    [SerializeField] private float alcanceRaio = 1.2f;

    [Header("Ajuste")]
    [Tooltip("Distancia da sola ate o osso do tornozelo. Medido neste rig: 0,117 m.")]
    [SerializeField] private float alturaTornozelo = 0.117f;
    [Tooltip("Velocidade do pe NO MUNDO abaixo da qual ele conta como plantado (m/s).")]
    [SerializeField] private float velPlantado = 0.6f;
    [Tooltip("Correcao maxima em um pe, em metros.")]
    [SerializeField] private float limite = 0.5f;
    [Tooltip("Quanto o quadril pode descer pra alcancar um degrau abaixo. 0 desliga.")]
    [SerializeField] private float limiteQuadril = 0.25f;
    [SerializeField] private float suavidadePe = 15f;
    [SerializeField] private float suavidadeQuadril = 8f;
    [Tooltip("Inclina o pe conforme a rampa.")]
    [SerializeField] private bool acompanharRampa = true;
    [SerializeField] private float rampaMax = 45f;
    [Tooltip("Pe LEVANTADO de proposito pela animacao (calcanhar no ar, joelho no chao). Acima disto, no mesmo plano, o IK NAO abaixa o pe. Sem isto o agachado do pack era achatado e o corpo afundava 19 cm.")]
    [SerializeField] private float descidaMax = 0.06f;
    [Tooltip("Quanto o chao embaixo do pe pode diferir da base da capsula e ainda contar como MESMO plano.")]
    [SerializeField] private float toleranciaPlano = 0.06f;

    /// <summary>Diagnostico.</summary>
    public int chamadasIK;
    public int achouChao;
    public float UltimoEsq { get { return pesoEsq; } }
    public float UltimoDir { get { return pesoDir; } }
    public float UltimoQuadril { get { return descidaQuadril; } }
    public float DeltaEsq { get { return deltaEsq; } }
    public float DeltaDir { get { return deltaDir; } }
    public float VelEsq { get { return velEsq; } }
    public float VelDir { get { return velDir; } }

    private Animator animator;
    private CharacterController capsula;

    private float pesoEsq, pesoDir;
    private float deltaEsq, deltaDir;
    private float velEsq, velDir;
    private bool corrigeEsq, corrigeDir;
    private bool plantadoEsq, plantadoDir;
    private Vector3 alvoEsq, alvoDir;
    private Quaternion giroEsq = Quaternion.identity, giroDir = Quaternion.identity;
    private Vector3 anteriorEsq, anteriorDir;
    private bool temAnterior;
    private float descidaQuadril;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        capsula = GetComponentInParent<CharacterController>();
        GarantirPassoIK();
    }

    /// <summary>
    /// Sem "IK Pass" na camada o Unity nem chama OnAnimatorIK, e o script fica
    /// mudo sem erro nenhum no console - facil de perder horas nisso.
    /// </summary>
    private void GarantirPassoIK()
    {
#if UNITY_EDITOR
        var ctrl = animator != null ? animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController : null;
        if (ctrl == null) return;
        var camadas = ctrl.layers;
        bool mudou = false;
        for (int i = 0; i < camadas.Length; i++)
            if (!camadas[i].iKPass) { camadas[i].iKPass = true; mudou = true; }
        if (mudou) { ctrl.layers = camadas; UnityEditor.EditorUtility.SetDirty(ctrl); }
#endif
    }

    private void OnAnimatorIK(int camada)
    {
        if (!ativo || animator == null || !animator.isHuman) return;
        if (camada != 0) return;
        chamadasIK++;

        bool noChao = capsula == null || capsula.isGrounded;
        // altura do CHAO em que a capsula esta de fato apoiada
        float baseY = capsula != null
            ? capsula.transform.position.y + capsula.center.y - capsula.height * 0.5f
            : transform.position.y;

        Medir(HumanBodyBones.LeftFoot, ref anteriorEsq, ref velEsq, ref deltaEsq,
              ref plantadoEsq, ref corrigeEsq, ref alvoEsq, ref giroEsq, noChao, baseY);
        Medir(HumanBodyBones.RightFoot, ref anteriorDir, ref velDir, ref deltaDir,
              ref plantadoDir, ref corrigeDir, ref alvoDir, ref giroDir, noChao, baseY);
        temAnterior = true;

        // ---- QUADRIL ----
        // So desce, e so por causa de pe PLANTADO que nao alcanca o degrau
        // abaixo. Pe de balanco nao tem voz aqui: era isso que afundava o
        // personagem nas versoes anteriores.
        float precisa = 0f;
        if (corrigeEsq && plantadoEsq && deltaEsq < precisa) precisa = deltaEsq;
        if (corrigeDir && plantadoDir && deltaDir < precisa) precisa = deltaDir;
        float alvoDescida = noChao ? Mathf.Clamp(precisa, -limiteQuadril, 0f) : 0f;

        descidaQuadril = Mathf.Lerp(descidaQuadril, alvoDescida, 1f - Mathf.Exp(-suavidadeQuadril * Time.deltaTime));
        if (Mathf.Abs(descidaQuadril) > 0.0005f)
            animator.bodyPosition = animator.bodyPosition + Vector3.up * descidaQuadril;

        Aplicar(AvatarIKGoal.LeftFoot, corrigeEsq, alvoEsq, giroEsq, ref pesoEsq);
        Aplicar(AvatarIKGoal.RightFoot, corrigeDir, alvoDir, giroDir, ref pesoDir);
    }

    private void Medir(HumanBodyBones osso, ref Vector3 anterior, ref float vel, ref float delta,
                       ref bool plantado, ref bool corrige, ref Vector3 alvo, ref Quaternion giro, bool noChao, float baseY)
    {
        plantado = false; corrige = false; delta = 0f;
        Transform t = animator.GetBoneTransform(osso);
        if (t == null) return;

        // velocidade do pe NO MUNDO - o sinal de que ele esta plantado
        Vector3 agora = t.position;
        if (temAnterior && Time.deltaTime > 0.0001f)
        {
            Vector3 d = agora - anterior; d.y = 0f;
            vel = d.magnitude / Time.deltaTime;
        }
        anterior = agora;

        if (!noChao) return;                       // no ar o sistema sai de cena

        RaycastHit hit;
        Vector3 de = agora + Vector3.up * alturaRaio;
        if (!Physics.Raycast(de, Vector3.down, out hit, alturaRaio + alcanceRaio, chao, QueryTriggerInteraction.Ignore)) return;
        if (capsula != null && hit.collider == capsula) return;
        achouChao++;

        float alvoY = hit.point.y + alturaTornozelo;
        delta = alvoY - agora.y;                   // + = pe enterrado
        plantado = vel < velPlantado;

        // Enterrado SEMPRE corrige (atravessar o chao nunca esta certo).
        // Levantado do chao so corrige se estiver plantado - senao estragaria o passo.
        corrige = delta > 0.001f || plantado;

        // PE LEVANTADO DE PROPOSITO: no MESMO plano do chao da capsula e bem
        // acima dele, quem levantou foi a ANIMACAO (agachado apoia no joelho e
        // deixa o calcanhar no ar). Forcar esse pe pra baixo achata a pose e
        // derruba o corpo inteiro - medido: -19,3 cm no agachado do pack.
        // Se o chao ali embaixo esta em OUTRA altura (degrau, ladeira), corrige.
        if (corrige && delta < -descidaMax && Mathf.Abs(hit.point.y - baseY) < toleranciaPlano)
        {
            corrige = false;
            return;
        }
        if (!corrige) return;

        alvo = new Vector3(agora.x, alvoY, agora.z);

        if (acompanharRampa && Vector3.Angle(hit.normal, Vector3.up) <= rampaMax)
        {
            Vector3 frente = Vector3.ProjectOnPlane(t.forward, hit.normal);
            giro = frente.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(frente, hit.normal) : t.rotation;
        }
        else giro = t.rotation;
    }

    private void Aplicar(AvatarIKGoal meta, bool corrige, Vector3 alvo, Quaternion giro, ref float peso)
    {
        peso = Mathf.Lerp(peso, corrige ? 1f : 0f, 1f - Mathf.Exp(-suavidadePe * Time.deltaTime));
        if (peso <= 0.002f)
        {
            animator.SetIKPositionWeight(meta, 0f);
            animator.SetIKRotationWeight(meta, 0f);
            return;
        }

        Vector3 atual = animator.GetIKPosition(meta);
        Vector3 destino = atual;                   // so a ALTURA muda; o passo continua da animacao
        destino.y = Mathf.Clamp(alvo.y, atual.y - limite, atual.y + limite);

        animator.SetIKPositionWeight(meta, peso);
        animator.SetIKPosition(meta, destino);

        if (acompanharRampa)
        {
            animator.SetIKRotationWeight(meta, peso);
            animator.SetIKRotation(meta, giro);
        }
        else animator.SetIKRotationWeight(meta, 0f);
    }
}
