using UnityEngine;

/// <summary>
/// COICE VISUAL DA ARMA - a arma pula pra tras e o cano levanta a cada tiro,
/// e volta com mola.
///
/// ORDEM DE EXECUCAO, e a parte que importa:
///   Update()            -> devolve a arma pra posicao base (limpa o coice)
///   LateUpdate ordem 70 -> TroncoMira mede o cano e converge na mira
///   LateUpdate ordem 80 -> ESTE script poe o coice por cima
///
/// Se o coice ficasse aplicado quando o TroncoMira mede, a convergencia leria o
/// coice como erro de mira e cancelaria ele - a arma nem tremia. Por isso o
/// coice e limpo antes e reposto depois.
///
/// O tranco da MIRA nao esta aqui: quem faz e CameraJogo.AplicarRecuo, porque a
/// arma segue a mira. Aqui e so o movimento da arma na mao.
/// </summary>
[DefaultExecutionOrder(80)]
public class RecuoArma : MonoBehaviour
{
    [Header("Mola")]
    [Tooltip("Rigidez: maior = volta mais rapido e mais seco.")]
    [SerializeField] private float rigidez = 140f;
    [Tooltip("Amortecimento. 1 = volta sem repicar. Abaixo de 1 a arma balanca.")]
    [SerializeField] private float amortecimento = 0.85f;

    [Header("Acumulo na rajada")]
    [Tooltip("Quanto o coice cresce a cada tiro seguido.")]
    [SerializeField] private float crescePorTiro = 0.06f;
    [Tooltip("Teto do crescimento.")]
    [SerializeField] private float tetoCrescimento = 1.7f;
    [Tooltip("Segundos sem atirar pra rajada zerar.")]
    [SerializeField] private float tempoZerar = 0.35f;

    [Header("Absorcao do corpo")]
    [Tooltip("Graus que o TRONCO recua por CENTIMETRO de empurrao pra tras.")]
    [SerializeField] private float absorverTronco = 0.55f;
    [Tooltip("Quanto a MAO anda pra tras, como fracao do coice. 1 = o curso inteiro.")]
    [SerializeField] private float cursoMao = 1f;
    [Tooltip("ABRE o cotovelo pro lado, girando ele em torno do eixo ombro-mao. Deixe em 0: o cotovelo ja recua sozinho puxado pela mao, e qualquer valor aqui joga ele pra CIMA (asa de galinha).")]
    [SerializeField] private float girarCotovelo = 0f;
    [Tooltip("Marque se o cotovelo abrir pro lado errado neste rig.")]
    [SerializeField] private bool inverterDobra = false;
    [Tooltip("Segura o cano na mira enquanto o braco recua. Desmarque se quiser que o cano suba junto.")]
    [SerializeField] private bool manterCanoNaMira = true;
    [Tooltip("Cola a mao esquerda no guarda-mao por IK, pra ela nao desgrudar da arma no coice.")]
    [SerializeField] private bool colarMaoEsquerda = true;

    private Animator animador;
    private Transform raiz, peito;
    private Transform ombroE, cotoveloE, maoE;
    private Transform ombroD, cotoveloD, maoD;

    private WeaponVisuals visuais;

    private GameObject modeloAnterior;
    private Vector3 basePos;
    private Quaternion baseRot;
    private bool baseOk;

    private float desloc, velDesloc;   // metros pra tras
    private float giro, velGiro;       // graus pra cima

    private int tirosSeguidos;
    private float ultimoTiro = -99f;

    /// <summary>Multiplicador atual da rajada (diagnostico).</summary>
    public float MultiplicadorRajada { get { return Mathf.Min(1f + tirosSeguidos * crescePorTiro, tetoCrescimento); } }

    private void Awake()
    {
        visuais = GetComponent<WeaponVisuals>();
        if (visuais == null) visuais = FindAnyObjectByType<WeaponVisuals>();

        GarantirOssos();
    }

    /// <summary>
    /// Acha os ossos, e reacha se tiverem sumido.
    ///
    /// Pegar so no Awake nao aguenta a vida real: quando o jogador morre o
    /// esqueleto e trocado e as referencias viram null, e recompilar com o jogo
    /// rodando tambem zera tudo. Nos dois casos o recuo simplesmente parava de
    /// funcionar em silencio. Aqui ele se conserta sozinho.
    /// </summary>
    private bool GarantirOssos()
    {
        if (peito != null && cotoveloE != null && maoE != null
            && cotoveloD != null && maoD != null && ombroE != null && ombroD != null)
            return true;

        if (animador == null) animador = GetComponentInChildren<Animator>();
        if (animador == null || !animador.isHuman) return false;

        raiz = transform;
        peito = animador.GetBoneTransform(HumanBodyBones.Chest);
        ombroE = animador.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        cotoveloE = animador.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        maoE = animador.GetBoneTransform(HumanBodyBones.LeftHand);
        ombroD = animador.GetBoneTransform(HumanBodyBones.RightUpperArm);
        cotoveloD = animador.GetBoneTransform(HumanBodyBones.RightLowerArm);
        maoD = animador.GetBoneTransform(HumanBodyBones.RightHand);

        return peito != null && cotoveloD != null && maoD != null;
    }

    /// <summary>Um disparo. Devolve o multiplicador da rajada pra quem chamou usar na mira.</summary>
    public float Disparar(float recuoMetros, float giroGraus)
    {
        if (Time.time - ultimoTiro > tempoZerar) tirosSeguidos = 0;
        ultimoTiro = Time.time;

        float mult = MultiplicadorRajada;
        tirosSeguidos++;

        velDesloc += recuoMetros * rigidez * 0.10f * mult;
        velGiro += giroGraus * rigidez * 0.10f * mult;
        return mult;
    }

    private void Update()
    {
        // rajada esfriou
        if (Time.time - ultimoTiro > tempoZerar) tirosSeguidos = 0;

        // devolve a arma pra base ANTES do TroncoMira medir
        Transform t = Modelo();
        if (t == null) { baseOk = false; return; }
        if (!baseOk) return;
        t.localPosition = basePos;
        t.localRotation = baseRot;
    }

    private void LateUpdate()
    {
        Transform t = Modelo();
        if (t == null) return;

        // arma trocada: rememoriza a pose base da ficha
        if (visuais.CurrentModel != modeloAnterior)
        {
            modeloAnterior = visuais.CurrentModel;
            basePos = t.localPosition;
            baseRot = t.localRotation;
            baseOk = true;
            desloc = velDesloc = giro = velGiro = 0f;
        }
        if (!baseOk) return;

        // mola: acelera de volta pro zero, com atrito
        float dt = Time.deltaTime;
        if (dt > 0f)
        {
            float c = 2f * Mathf.Sqrt(Mathf.Max(0.01f, rigidez)) * amortecimento;

            velDesloc += (-rigidez * desloc - c * velDesloc) * dt;
            desloc += velDesloc * dt;

            velGiro += (-rigidez * giro - c * velGiro) * dt;
            giro += velGiro * dt;
        }

        // aplica por cima da base: SO pra tras na linha do cano.
        // A ARMA NAO SAI DA MAO. Antes eu deslocava o modelo pra tras no espaco da
        // mao: a arma andava 10 cm e a mao so 5, entao ela literalmente escapava
        // das maos e sobrava mao tremendo do lado. Agora o modelo fica cravado na
        // base e QUEM recua e o braco - a arma vai junto porque esta presa nele.
        t.localPosition = basePos;
        t.localRotation = baseRot * Quaternion.Euler(-giro, 0f, 0f);

        // e o CORPO absorve junto - senao fica arma pulando e braco de estatua
        AbsorverNoCorpo();
    }

    /// <summary>
    /// O corpo aguentando o coice: tronco recua e cotovelos dobram, tudo puxado
    /// pelo EMPURRAO PRA TRAS (desloc), nao pela subida do cano.
    ///
    /// A ideia e ler como "o tiro empurrou o braco" e nao como "a arma pulou":
    /// quem se mexe e o braco, e o cano fica na mira.
    ///
    /// Roda DEPOIS do TroncoMira (ordem 80 contra 70) de proposito: se rodasse
    /// antes, a convergencia leria isto como erro de mira e apagaria o efeito.
    ///
    /// Nao precisa desfazer nada: os ossos sao reescritos pelo Animator no comeco
    /// do quadro seguinte. Quem precisa de limpeza e so o modelo da arma, que nao
    /// e animado - isso acontece no Update().
    /// </summary>
    private void AbsorverNoCorpo()
    {
        if (!GarantirOssos()) return;

        float cm = desloc * 100f;
        if (Mathf.Abs(cm) < 0.01f) return;

        Transform modelo = Modelo();
        if (modelo == null || maoD == null || ombroD == null || cotoveloD == null) return;

        // ---- fotografa tudo ANTES de mexer ----
        Vector3 canoAntes = modelo.TransformDirection(Vector3.forward);
        Vector3 maoDposAntes = maoD.position;
        Quaternion maoDrotAntes = maoD.rotation;
        Vector3 maoEnaArma = maoE != null ? modelo.InverseTransformPoint(maoE.position) : Vector3.zero;
        Quaternion maoErotNaArma = maoE != null ? Quaternion.Inverse(modelo.rotation) * maoE.rotation : Quaternion.identity;

        // ---- tronco cede ----
        if (peito != null && raiz != null)
            peito.rotation = Quaternion.AngleAxis(-cm * absorverTronco, raiz.right) * peito.rotation;

        // ---- a mao vai PRA TRAS, e SO pra tras ----
        // Antes eu girava ossos soltos e aceitava a direcao que desse: o braco
        // acabava levando a arma pra CIMA junto. Agora a mao recebe um ALVO
        // explicito, atras dela na linha do cano, com a altura TRAVADA - entao nao
        // tem como subir. O braco e resolvido por IK ate chegar la.
        Vector3 tras = new Vector3(canoAntes.x, 0f, canoAntes.z);
        if (tras.sqrMagnitude < 1e-6f) tras = -raiz.forward; else tras = -tras.normalized;

        Vector3 alvoMao = maoDposAntes + tras * (desloc * cursoMao);
        alvoMao.y = maoDposAntes.y;                    // trava a altura: zero subida
        ResolverDoisOssos(ombroD, cotoveloD, maoD, alvoMao);

        // ---- o COTOVELO abre pra tras ----
        // Giro em torno do eixo ombro->mao: a mao esta EM CIMA desse eixo, entao
        // ela nao sai do lugar, so o cotovelo passeia. E o movimento que se ve
        // quando alguem segura o coice de verdade.
        Vector3 eixoBraco = maoD.position - ombroD.position;
        if (eixoBraco.sqrMagnitude > 1e-6f)
            ombroD.rotation = Quaternion.AngleAxis(cm * girarCotovelo * (inverterDobra ? -1f : 1f),
                                                   eixoBraco.normalized) * ombroD.rotation;

        // ---- o cano fica EXATAMENTE como estava ----
        // Devolver a rotacao original da mao ja garante isso, sem conta de angulo:
        // a arma e filha da mao, entao mesma rotacao = mesma direcao de cano.
        if (manterCanoNaMira) maoD.rotation = maoDrotAntes;

        // ---- mao ESQUERDA gruda de volta na arma ----
        if (colarMaoEsquerda && maoE != null && ombroE != null && cotoveloE != null)
        {
            Vector3 alvoE = modelo.TransformPoint(maoEnaArma);
            ResolverDoisOssos(ombroE, cotoveloE, maoE, alvoE);
            maoE.rotation = modelo.rotation * maoErotNaArma;
        }
    }

    /// <summary>Dobra o cotovelo no eixo perpendicular ao plano braco-antebraco.</summary>
    private void DobrarCotovelo(Transform ombro, Transform cotovelo, Transform mao, float graus)
    {
        if (ombro == null || cotovelo == null || mao == null) return;
        Vector3 eixo = Vector3.Cross(cotovelo.position - ombro.position, mao.position - cotovelo.position);
        if (eixo.sqrMagnitude < 1e-8f) return;
        cotovelo.rotation = Quaternion.AngleAxis(graus, eixo.normalized) * cotovelo.rotation;
    }

    private static float AnguloTriangulo(float a, float b, float oposto)
    {
        return Mathf.Acos(Mathf.Clamp((a * a + b * b - oposto * oposto) / (2f * a * b), -1f, 1f)) * Mathf.Rad2Deg;
    }

    /// <summary>IK de dois ossos: leva a ponta ate o alvo dobrando no plano atual do cotovelo.</summary>
    private static void ResolverDoisOssos(Transform raizB, Transform meio, Transform ponta, Vector3 alvo)
    {
        Vector3 a = raizB.position, b = meio.position, c = ponta.position;
        float lab = Vector3.Distance(a, b);
        float lcb = Vector3.Distance(b, c);
        float lac = Vector3.Distance(a, c);
        if (lab < 1e-5f || lcb < 1e-5f) return;
        float lat = Mathf.Clamp(Vector3.Distance(a, alvo), 0.02f, lab + lcb - 0.001f);

        Vector3 eixo = Vector3.Cross(c - a, b - a);
        if (eixo.sqrMagnitude < 1e-8f) eixo = Vector3.Cross(c - a, Vector3.up);
        if (eixo.sqrMagnitude < 1e-8f) return;
        eixo.Normalize();

        float angA0 = AnguloTriangulo(lab, lac, lcb);
        float angA1 = AnguloTriangulo(lab, lat, lcb);
        float angB0 = AnguloTriangulo(lab, lcb, lac);
        float angB1 = AnguloTriangulo(lab, lcb, lat);

        raizB.rotation = Quaternion.AngleAxis(angA1 - angA0, eixo) * raizB.rotation;
        meio.rotation = Quaternion.AngleAxis(angB1 - angB0, eixo) * meio.rotation;
        raizB.rotation = Quaternion.FromToRotation(ponta.position - raizB.position, alvo - raizB.position) * raizB.rotation;
    }


    private Transform Modelo()
    {
        if (visuais == null) visuais = FindAnyObjectByType<WeaponVisuals>();
        if (visuais == null || visuais.CurrentModel == null) return null;
        return visuais.CurrentModel.transform;
    }
}
