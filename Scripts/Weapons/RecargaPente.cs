using UnityEngine;

/// <summary>
/// RECARGA COM PENTE VISIVEL.
///
/// O pack traz a arma em pecas separadas (AR_B_Mag e um objeto proprio), entao
/// da pra fazer a recarga de verdade em vez de so esperar um timer:
///
///   1. O pente da arma SOME e um pente-copia aparece no lugar, solto no mundo,
///      caindo com fisica (e o pente velho que voce jogou fora).
///   2. Um segundo depois o pente da arma volta, subindo e encaixando no poco
///      do carregador (o pente novo entrando).
///
/// Tudo dura exatamente o 'reloadTime' da ficha, entao o visual sempre bate com
/// o momento em que a arma volta a atirar.
///
/// Achando o pente: procura um filho do modelo com "mag" no nome (funciona pra
/// qualquer arma do pack: AR_A_Mag, AR_B_Mag, AR_C_Mag...).
/// </summary>
public class RecargaPente : MonoBehaviour
{
    [Header("Tempos (fracao do tempo de recarga)")]
    [Tooltip("Quando o pente velho cai. 0.15 = logo no comeco.")]
    [Range(0f, 1f)]
    [SerializeField] private float momentoSoltar = 0.15f;
    [Tooltip("Quando o pente novo comeca a subir pro encaixe.")]
    [Range(0f, 1f)]
    [SerializeField] private float momentoEncaixar = 0.55f;
    [Tooltip("Quanto dura o movimento de encaixe (fracao do tempo total).")]
    [Range(0.05f, 0.6f)]
    [SerializeField] private float duracaoEncaixe = 0.3f;

    [Header("Pente caindo")]
    [Tooltip("Distancia que o pente novo percorre ao encaixar (metros).")]
    [SerializeField] private float cursoEncaixe = 0.12f;
    [SerializeField] private float forcaQueda = 0.8f;
    [SerializeField] private float segundosNoChao = 8f;

    private WeaponController armas;
    private WeaponVisuals visuais;

    private bool recarregando;
    private float tInicio;
    private float duracao;
    private Transform penteArma;
    private Vector3 pentePosOriginal;
    private bool penteEscondido;
    private bool jaSoltou;

    private void Awake()
    {
        armas = GetComponent<WeaponController>();
        visuais = GetComponent<WeaponVisuals>();
    }

    /// <summary>Acha o objeto do pente dentro do modelo da arma equipada.</summary>
    private Transform AcharPente()
    {
        if (visuais == null || visuais.CurrentModel == null) return null;
        foreach (Transform t in visuais.CurrentModel.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLower();
            if (n.Contains("mag") && !n.Contains("magazine_release")) return t;
        }
        return null;
    }

    private void Update()
    {
        if (armas == null) return;

        // comecou a recarregar?
        if (armas.IsReloading && !recarregando)
        {
            penteArma = AcharPente();
            if (penteArma != null)
            {
                recarregando = true;
                jaSoltou = false;
                penteEscondido = false;
                tInicio = Time.time;
                duracao = armas.CurrentWeapon != null ? Mathf.Max(0.3f, armas.CurrentWeapon.reloadTime) : 1.5f;
                pentePosOriginal = penteArma.localPosition;
            }
        }

        if (!recarregando) return;

        // a arma pode ser trocada/destruida no meio da recarga
        if (penteArma == null)
        {
            recarregando = false;
            return;
        }

        float t = (Time.time - tInicio) / duracao;

        // 1) solta o pente velho
        if (!jaSoltou && t >= momentoSoltar)
        {
            jaSoltou = true;
            SoltarPenteVelho();
            EsconderPente(true);
        }

        // 2) pente novo sobe e encaixa
        if (t >= momentoEncaixar)
        {
            if (penteEscondido) EsconderPente(false);
            float k = Mathf.Clamp01((t - momentoEncaixar) / Mathf.Max(0.01f, duracaoEncaixe));
            // desce no eixo Y local da arma e sobe ate o lugar
            float suave = 1f - (1f - k) * (1f - k);   // ease-out: entra firme e assenta
            penteArma.localPosition = pentePosOriginal + Vector3.down * (cursoEncaixe * (1f - suave));
        }

        // fim
        if (!armas.IsReloading || t >= 1f)
        {
            penteArma.localPosition = pentePosOriginal;
            EsconderPente(false);
            recarregando = false;
        }
    }

    private void EsconderPente(bool esconder)
    {
        penteEscondido = esconder;
        foreach (var r in penteArma.GetComponentsInChildren<Renderer>(true)) r.enabled = !esconder;
    }

    /// <summary>Cria uma copia do pente solta no mundo, caindo.</summary>
    private void SoltarPenteVelho()
    {
        var copia = new GameObject("PenteDescartado");
        copia.transform.position = penteArma.position;
        copia.transform.rotation = penteArma.rotation;
        copia.transform.localScale = penteArma.lossyScale;

        var origem = penteArma.GetComponent<MeshFilter>();
        var origemR = penteArma.GetComponent<MeshRenderer>();
        if (origem != null && origemR != null)
        {
            copia.AddComponent<MeshFilter>().sharedMesh = origem.sharedMesh;
            copia.AddComponent<MeshRenderer>().sharedMaterials = origemR.sharedMaterials;
        }

        var bc = copia.AddComponent<BoxCollider>();
        var rb = copia.AddComponent<Rigidbody>();
        rb.mass = 0.25f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var cc = GetComponent<CharacterController>();
        Vector3 vJogador = cc != null ? cc.velocity : Vector3.zero;
        rb.linearVelocity = vJogador + Vector3.down * forcaQueda + Random.insideUnitSphere * 0.3f;
        rb.angularVelocity = Random.insideUnitSphere * 4f;

        int ignorar = LayerMask.NameToLayer("Ignore Raycast");
        if (ignorar >= 0) copia.layer = ignorar;

        Destroy(copia, segundosNoChao);
    }
}
