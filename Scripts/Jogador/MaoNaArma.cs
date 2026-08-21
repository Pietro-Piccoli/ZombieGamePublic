using UnityEngine;

/// <summary>
/// MAO ESQUERDA NA ARMA - IK de dois ossos, permanente.
///
/// POR QUE ISSO EXISTE
/// A arma fica PRESA no osso da mao direita (WeaponSocket). Entao a mao direita
/// esta sempre certa, em qualquer animacao. A mao ESQUERDA nao: ela vem do que o
/// animador desenhou, e cada pack desenha pra uma arma diferente. Medido no jogo
/// antes deste script: a mao esquerda estava a 22,7 cm do guarda-mao da AK - o
/// dedo passava no ar, sem tocar a arma.
///
/// Este componente resolve isso de uma vez: todo quadro, depois de TUDO (pose,
/// tronco na mira, recuo), ele leva a mao esquerda pro ponto de apoio da arma.
/// Com isso QUALQUER animacao de tronco passa a funcionar com QUALQUER arma -
/// e por isso que da pra usar as poses do pack novo sem refazer a mao na unha.
///
/// Ordem de execucao 95: depois de PoseBracos (0), TroncoMira (70) e RecuoArma
/// (80). Tem que ser o ultimo, senao alguem move a arma depois e a mao larga.
/// </summary>
[DefaultExecutionOrder(95)]
public class MaoNaArma : MonoBehaviour
{
    [Header("Onde a mao esquerda apoia")]
    [Tooltip("Pedaco do modelo da arma que serve de apoio. Busca por nome PARCIAL (ex: BarrelGuard acha AR_B_BarrelGuard).")]
    [SerializeField] private string nomeDaParte = "BarrelGuard";
    [Tooltip("Ajuste fino do ponto de apoio, no espaco LOCAL do modelo da arma.")]
    [SerializeField] private Vector3 ajusteLocal = Vector3.zero;
    [Tooltip("Rotacao do pulso em relacao a arma (euler). So vale com 'girarMao' ligado.")]
    [SerializeField] private Vector3 rotacaoMao = Vector3.zero;
    [Tooltip("Alinha o pulso com a arma. Desligue pra manter a rotacao da animacao.")]
    [SerializeField] private bool girarMao = true;

    [Header("Peso")]
    [Range(0f, 1f)]
    [Tooltip("1 = mao colada na arma. Abaixe pra deixar a animacao aparecer.")]
    [SerializeField] private float forca = 1f;
    [SerializeField] private float velPeso = 10f;
    [Tooltip("Enquanto o estado da camada de arma tiver um destes nomes, o IK sai (a recarga leva a mao no pente).")]
    [SerializeField] private string[] estadosSemIK = new string[] { "RecargaRifle", "RecargaPistola" };
    [Tooltip("Nome da camada de arma no Animator.")]
    [SerializeField] private string camadaArma = "Arma";

    [Header("Cotovelo")]
    [Tooltip("Aponta o cotovelo pra uma direcao fixa do corpo em vez de deixar onde a animacao largou.")]
    [SerializeField] private bool usarPolo = true;
    [Tooltip("Direcao do cotovelo no espaco do CORPO. Padrao: pra baixo e um pouco pra fora.")]
    [SerializeField] private Vector3 direcaoPolo = new Vector3(-0.35f, -1f, 0.1f);
    [Range(0f, 1f)]
    [SerializeField] private float forcaPolo = 1f;

    [Header("Seguranca")]
    [Tooltip("Fracao do braco esticado que o IK aceita. Acima disso o peso cai sozinho pra nao esticar o braco feito borracha.")]
    [Range(0.8f, 1f)]
    [SerializeField] private float alcanceMax = 1f;

    /// <summary>Distancia em cm entre o pulso e o alvo DEPOIS de resolver.</summary>
    public float ErroCm { get; private set; }
    /// <summary>Peso efetivo aplicado neste quadro.</summary>
    public float PesoAtual { get { return peso; } }
    /// <summary>Achou o ponto de apoio na arma?</summary>
    public bool TemAlvo { get; private set; }
    /// <summary>Quantas vezes o IK rodou de fato.</summary>
    public int Chamadas { get; private set; }
    /// <summary>Distancia ombro->apoio pedida neste quadro, em cm. Se passar do braco (56,8 cm) o IK cede.</summary>
    public float DistanciaAlvoCm { get; private set; }

    private Animator animador;
    private Transform raiz;
    private Transform ombro, cotovelo, mao;
    private WeaponVisuals visuais;
    private Transform apoio;
    private GameObject modeloAnterior;
    private int idxCamada = -1;
    private float peso;

    private void Awake()
    {
        raiz = transform.parent != null ? transform.parent : transform;
        GarantirOssos();
    }

    /// <summary>
    /// Resolve ossos e camada com preguica. O esqueleto TROCA quando o player
    /// morre (o jogo liga outro boneco), entao guardar so no Awake da null.
    /// </summary>
    private bool GarantirOssos()
    {
        if (mao != null && ombro != null && cotovelo != null && animador != null) return true;

        animador = GetComponent<Animator>();
        if (animador == null || !animador.isHuman) return false;

        ombro = animador.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        cotovelo = animador.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        mao = animador.GetBoneTransform(HumanBodyBones.LeftHand);

        idxCamada = -1;
        for (int i = 0; i < animador.layerCount; i++)
            if (animador.GetLayerName(i) == camadaArma) idxCamada = i;

        return mao != null && ombro != null && cotovelo != null;
    }

    /// <summary>Ponto de apoio na arma equipada agora. Recalcula quando troca de arma.</summary>
    private Transform Apoio()
    {
        if (visuais == null) visuais = FindAnyObjectByType<WeaponVisuals>();
        if (visuais == null || visuais.CurrentModel == null) { apoio = null; modeloAnterior = null; return null; }

        if (visuais.CurrentModel != modeloAnterior || apoio == null)
        {
            modeloAnterior = visuais.CurrentModel;
            apoio = null;
            Transform modelo = visuais.CurrentModel.transform;
            if (!string.IsNullOrEmpty(nomeDaParte))
            {
                foreach (Transform t in modelo.GetComponentsInChildren<Transform>(true))
                    if (t != modelo && t.name.IndexOf(nomeDaParte, System.StringComparison.OrdinalIgnoreCase) >= 0) { apoio = t; break; }
            }
            if (apoio == null) apoio = modelo;
        }
        return apoio;
    }

    private void LateUpdate()
    {
        TemAlvo = false;
        if (!GarantirOssos()) return;

        Transform ap = Apoio();
        Transform modelo = visuais != null && visuais.CurrentModel != null ? visuais.CurrentModel.transform : null;
        if (ap == null || modelo == null) { peso = Mathf.MoveTowards(peso, 0f, velPeso * Time.deltaTime); return; }

        Vector3 alvo = ap.position + modelo.TransformVector(ajusteLocal);
        TemAlvo = true;

        float querido = forca;

        if (idxCamada >= 0 && estadosSemIK != null && estadosSemIK.Length > 0)
        {
            var atual = animador.GetCurrentAnimatorStateInfo(idxCamada);
            var proximo = animador.GetNextAnimatorStateInfo(idxCamada);
            for (int i = 0; i < estadosSemIK.Length; i++)
            {
                string n = estadosSemIK[i];
                if (string.IsNullOrEmpty(n)) continue;
                if (atual.IsName(n) || proximo.IsName(n)) { querido = 0f; break; }
            }
        }

        float lab = Vector3.Distance(ombro.position, cotovelo.position);
        float lcb = Vector3.Distance(cotovelo.position, mao.position);
        float alcance = (lab + lcb) * alcanceMax;
        float dist = Vector3.Distance(ombro.position, alvo);
        DistanciaAlvoCm = dist * 100f;
        if (dist > alcance)
        {
            float sobra = (dist - alcance) / Mathf.Max(0.01f, alcance * 0.6f);
            querido *= Mathf.Clamp01(1f - sobra);
        }

        peso = Mathf.MoveTowards(peso, querido, velPeso * Time.deltaTime);
        if (peso <= 0.001f) { ErroCm = -1f; return; }

        Vector3 alvoPeso = Vector3.Lerp(mao.position, alvo, peso);
        Quaternion rotAntes = mao.rotation;

        Vector3 polo = Vector3.zero;
        bool temPolo = usarPolo && forcaPolo > 0.001f;
        if (temPolo)
        {
            Vector3 d = direcaoPolo.sqrMagnitude > 1e-6f ? direcaoPolo.normalized : Vector3.down;
            polo = ombro.position + raiz.rotation * d * (lab + lcb);
        }

        ResolverDoisOssos(ombro, cotovelo, mao, alvoPeso, temPolo, polo, forcaPolo * peso);
        Chamadas++;

        if (girarMao)
        {
            Quaternion alvoRot = modelo.rotation * Quaternion.Euler(rotacaoMao);
            mao.rotation = Quaternion.Slerp(rotAntes, alvoRot, peso);
        }

        ErroCm = Vector3.Distance(mao.position, alvo) * 100f;
    }

    /// <summary>
    /// IK analitico de dois ossos. Mantem o plano de dobra atual, e se pedirem
    /// polo gira o cotovelo em volta do eixo ombro->mao ate encarar o polo -
    /// girar nesse eixo move o COTOVELO sem mover a MAO.
    /// </summary>
    private static void ResolverDoisOssos(Transform raizB, Transform meio, Transform ponta, Vector3 alvo,
                                          bool temPolo, Vector3 polo, float pesoPolo)
    {
        Vector3 a = raizB.position, b = meio.position, c = ponta.position;
        float lab = Vector3.Distance(a, b);
        float lcb = Vector3.Distance(b, c);
        float lac = Vector3.Distance(a, c);
        if (lab < 0.0001f || lcb < 0.0001f) return;

        float lat = Mathf.Clamp(Vector3.Distance(a, alvo), 0.02f, lab + lcb - 0.001f);

        Vector3 eixo = Vector3.Cross(c - a, b - a);
        if (eixo.sqrMagnitude < 1e-8f) eixo = Vector3.Cross(alvo - a, Vector3.up);
        if (eixo.sqrMagnitude < 1e-8f) return;
        eixo = eixo.normalized;

        float angA0 = AnguloTriangulo(lab, lac, lcb);
        float angA1 = AnguloTriangulo(lab, lat, lcb);
        float angB0 = AnguloTriangulo(lab, lcb, lac);
        float angB1 = AnguloTriangulo(lab, lcb, lat);

        raizB.rotation = Quaternion.AngleAxis((angA1 - angA0) * Mathf.Rad2Deg, eixo) * raizB.rotation;
        meio.rotation = Quaternion.AngleAxis((angB1 - angB0) * Mathf.Rad2Deg, eixo) * meio.rotation;

        Vector3 dirAtual = ponta.position - a;
        Vector3 dirAlvo = alvo - a;
        if (dirAtual.sqrMagnitude > 1e-8f && dirAlvo.sqrMagnitude > 1e-8f)
            raizB.rotation = Quaternion.FromToRotation(dirAtual, dirAlvo) * raizB.rotation;

        if (temPolo && pesoPolo > 0.001f)
        {
            Vector3 eixoBraco = (ponta.position - a).normalized;
            if (eixoBraco.sqrMagnitude > 1e-8f)
            {
                Vector3 atual = Vector3.ProjectOnPlane(meio.position - a, eixoBraco);
                Vector3 querido = Vector3.ProjectOnPlane(polo - a, eixoBraco);
                if (atual.sqrMagnitude > 1e-8f && querido.sqrMagnitude > 1e-8f)
                {
                    float ang = Vector3.SignedAngle(atual.normalized, querido.normalized, eixoBraco) * pesoPolo;
                    if (Mathf.Abs(ang) > 0.01f)
                        raizB.rotation = Quaternion.AngleAxis(ang, eixoBraco) * raizB.rotation;
                }
            }
        }
    }

    private static float AnguloTriangulo(float a, float b, float oposto)
    {
        float v = (a * a + b * b - oposto * oposto) / (2f * a * b);
        return Mathf.Acos(Mathf.Clamp(v, -1f, 1f));
    }
}
