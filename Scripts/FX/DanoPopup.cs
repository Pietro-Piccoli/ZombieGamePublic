using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Numeros de dano flutuantes, no molde do Risk of Rain 2.
///
/// DECISOES DE PERFORMANCE (o motivo de cada uma):
///  - TextMeshPro 3D (MeshRenderer), NAO TextMeshProUGUI. Texto em Canvas
///    forca rebuild de layout a cada mudanca; em 3D e so uma malha, e todas
///    compartilham um material -> batcham em poucas draw calls.
///  - UM unico Update, aqui no gerenciador, varrendo um array. Nada de um
///    MonoBehaviour por numero: 64 Updates por frame custam caro e nao fazem
///    nada que um laco nao faca.
///  - Pool fixo. Zero Instantiate/Destroy durante a partida, zero lixo pro GC.
///  - Billboard calculado UMA vez por frame e reaproveitado por todos.
///  - Fusao por alvo: acerto novo no mesmo zumbi dentro da janela SOMA no
///    numero que ja esta na tela em vez de criar outro. Sem isso, a AK a 600
///    tiros/min vira uma sopa ilegivel. E o unico desvio consciente do RoR2,
///    que usa armas bem mais lentas.
/// </summary>
[DefaultExecutionOrder(-50)]
public class DanoPopup : MonoBehaviour
{
    public enum Tipo { Normal, Critico, Explosao, Fogo, Acido, Cura, Eletrico }

    // ---------------- ajustes ----------------
    [Header("Pool")]
    [Tooltip("Teto de numeros na tela. Passando disso, o mais velho e reciclado.")]
    [SerializeField] private int capacidade = 64;

    [Header("Tempo")]
    [SerializeField] private float duracao = 0.9f;
    [Tooltip("Critico fica um pouco mais tempo na tela.")]
    [SerializeField] private float duracaoCritico = 1.15f;
    [Tooltip("Acertos no mesmo alvo dentro desta janela somam no mesmo numero.")]
    [SerializeField] private float janelaFusao = 0.18f;
    [Tooltip("Janela maior pro fogo/acido: o tick e continuo, entao um numero unico que cresce le muito melhor que 8 por segundo.")]
    [SerializeField] private float janelaFusaoDot = 0.7f;

    [Header("Movimento")]
    [SerializeField] private float subidaInicial = 2.1f;
    [SerializeField] private float gravidade = 3.4f;
    [SerializeField] private float espalhamentoLateral = 0.5f;

    [Header("Tamanho")]
    [SerializeField] private float tamanhoBase = 2.6f;
    [SerializeField] private float escalaCritico = 1.55f;
    [Tooltip("Distancia em que o numero para de encolher (fica legivel de longe).")]
    [SerializeField] private float distanciaReferencia = 9f;
    [SerializeField] private float distanciaMaxima = 70f;

    [Header("Cores")]
    [SerializeField] private Color corNormal   = new Color(1.00f, 1.00f, 1.00f);
    [SerializeField] private Color corCritico  = new Color(1.00f, 0.84f, 0.18f);
    [SerializeField] private Color corExplosao = new Color(1.00f, 0.47f, 0.16f);
    [SerializeField] private Color corFogo     = new Color(1.00f, 0.62f, 0.10f);
    [SerializeField] private Color corAcido    = new Color(0.55f, 1.00f, 0.22f);
    [SerializeField] private Color corCura     = new Color(0.35f, 1.00f, 0.45f);
    [SerializeField] private Color corEletrico = new Color(0.45f, 0.75f, 1.00f);

    // ---------------- estado ----------------
    private struct Num
    {
        public TextMeshPro txt;
        public Transform tr;
        public Vector3 pos;
        public Vector3 vel;
        public float idade;
        public float vida;
        public float escala;
        public Color cor;
        public int valor;
        public bool ativo;
        public int chaveAlvo;      // instanceID do alvo, pra fusao
        public Tipo tipo;
    }

    private Num[] nums;
    private int proximo;
    private Transform cam;
    private Quaternion giroBillboard;
    private readonly Dictionary<long, int> fusao = new Dictionary<long, int>();

    private static DanoPopup instancia;

    // ---------------- API ----------------

    /// <summary>Mostra um numero de dano no mundo. Chame de qualquer lugar.</summary>
    public static void Mostrar(Vector3 mundo, int valor, Tipo tipo, Transform alvo)
    {
        if (valor <= 0) return;
        if (instancia == null) instancia = FindAnyObjectByType<DanoPopup>();   // sobreviveu ao reload?
        if (instancia == null) Criar();
        if (instancia != null) instancia.Empurrar(mundo, valor, tipo, alvo);
    }

    public static void Mostrar(Vector3 mundo, int valor, Tipo tipo)
    {
        Mostrar(mundo, valor, tipo, null);
    }

    private static void Criar()
    {
        var go = new GameObject("DanoPopup");
        instancia = go.AddComponent<DanoPopup>();
    }

    // ---------------- ciclo ----------------

    private void Awake()
    {
        if (instancia != null && instancia != this) { Destroy(gameObject); return; }
        instancia = this;
        MontarPool();
    }

    /// <summary>
    /// Monta o pool. Pode ser chamado mais de uma vez: recompilar script com o
    /// jogo rodando zera todo campo nao serializado (o array vira null) e o
    /// Awake NAO roda de novo, entao quem usa o pool precisa saber remonta-lo.
    /// </summary>
    private void MontarPool()
    {
        for (int k = transform.childCount - 1; k >= 0; k--) DestroyImmediate(transform.GetChild(k).gameObject);

        var fonte = UIKit.FontePesada;
        nums = new Num[Mathf.Max(8, capacidade)];

        for (int i = 0; i < nums.Length; i++)
        {
            var go = new GameObject("Num" + i);
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshPro>();
            t.font = fonte;
            t.fontSize = tamanhoBase;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.isOrthographic = false;
            // sem contorno (o Pietro nao curtiu): sombra projetada separa o numero
            // do fundo sem engrossar a letra
            t.fontMaterial = new Material(t.fontMaterial);
            t.fontMaterial.EnableKeyword("UNDERLAY_ON");
            t.fontMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.55f));
            t.fontMaterial.SetFloat("_UnderlayOffsetX", 0.6f);
            t.fontMaterial.SetFloat("_UnderlayOffsetY", -0.6f);
            t.fontMaterial.SetFloat("_UnderlayDilate", 0.1f);
            t.fontMaterial.SetFloat("_UnderlaySoftness", 0.3f);
            t.outlineWidth = 0f;
            t.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.GetComponent<MeshRenderer>().receiveShadows = false;
            go.SetActive(false);

            nums[i].txt = t;
            nums[i].tr = go.transform;
        }
    }

    private void OnDestroy() { if (instancia == this) instancia = null; }

    private Color CorDe(Tipo t)
    {
        switch (t)
        {
            case Tipo.Critico:  return corCritico;
            case Tipo.Explosao: return corExplosao;
            case Tipo.Fogo:     return corFogo;
            case Tipo.Acido:    return corAcido;
            case Tipo.Cura:     return corCura;
            case Tipo.Eletrico: return corEletrico;
            default:            return corNormal;
        }
    }

    private void Empurrar(Vector3 mundo, int valor, Tipo tipo, Transform alvo)
    {
        if (nums == null) MontarPool();
        if (cam == null)
        {
            Camera c = Camera.main;
            if (c == null) return;
            cam = c.transform;
        }

        // fora de alcance ou atras da camera: nem gasta slot
        Vector3 delta = mundo - cam.position;
        float dist = delta.magnitude;
        if (dist > distanciaMaxima) return;
        if (Vector3.Dot(delta, cam.forward) <= 0f) return;

        // ---- fusao: mesmo alvo + mesmo tipo dentro da janela soma no numero existente
        long chave = -1;
        if (alvo != null)
        {
            chave = ((long)alvo.GetInstanceID() << 4) | (long)(int)tipo;
            int idx;
            if (fusao.TryGetValue(chave, out idx) && idx >= 0 && idx < nums.Length)
            {
                float janela = (tipo == Tipo.Fogo || tipo == Tipo.Acido) ? janelaFusaoDot : janelaFusao;
                if (nums[idx].ativo && nums[idx].chaveAlvo == alvo.GetInstanceID()
                    && nums[idx].tipo == tipo && nums[idx].idade < janela)
                {
                    nums[idx].valor += valor;
                    nums[idx].idade = 0f;                 // renova a janela e o fade
                    nums[idx].escala = 1.28f;             // repica pra chamar atencao
                    // re-ancora no alvo: sem isso um zumbi queimando por 5s
                    // manda o numero pro ceu, porque a posicao continua integrando
                    nums[idx].pos = mundo + new Vector3(Random.Range(-0.12f, 0.12f), 0f, Random.Range(-0.12f, 0.12f));
                    nums[idx].vel = new Vector3(0f, subidaInicial * 0.7f, 0f);
                    nums[idx].txt.text = nums[idx].valor.ToString();
                    return;
                }
            }
        }

        int slot = Reservar();
        ref Num n = ref nums[slot];
        n.ativo = true;
        n.tipo = tipo;
        n.valor = valor;
        n.idade = 0f;
        n.vida = tipo == Tipo.Critico ? duracaoCritico : duracao;
        n.cor = CorDe(tipo);
        n.escala = 0.45f;
        n.chaveAlvo = alvo != null ? alvo.GetInstanceID() : 0;
        n.pos = mundo + new Vector3(Random.Range(-0.18f, 0.18f), Random.Range(0f, 0.2f), Random.Range(-0.18f, 0.18f));
        n.vel = new Vector3(Random.Range(-espalhamentoLateral, espalhamentoLateral), subidaInicial, Random.Range(-espalhamentoLateral, espalhamentoLateral));

        n.txt.text = valor.ToString();
        n.txt.color = n.cor;
        n.tr.position = n.pos;
        n.tr.gameObject.SetActive(true);

        if (chave >= 0) fusao[chave] = slot;
    }

    /// <summary>Pega um slot livre; se nao tiver, recicla o mais velho.</summary>
    private int Reservar()
    {
        for (int k = 0; k < nums.Length; k++)
        {
            int i = (proximo + k) % nums.Length;
            if (!nums[i].ativo) { proximo = (i + 1) % nums.Length; return i; }
        }
        int velho = 0; float maior = -1f;
        for (int i = 0; i < nums.Length; i++)
            if (nums[i].idade > maior) { maior = nums[i].idade; velho = i; }
        return velho;
    }

    private void LateUpdate()
    {
        if (nums == null) { MontarPool(); return; }
        if (cam == null)
        {
            Camera c = Camera.main;
            if (c == null) return;
            cam = c.transform;
        }

        // billboard: uma conta por frame, nao uma por numero
        giroBillboard = Quaternion.LookRotation(cam.forward, cam.up);

        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // jogo pausado (level up): congela os numeros junto

        for (int i = 0; i < nums.Length; i++)
        {
            if (!nums[i].ativo) continue;

            nums[i].idade += dt;
            float t = nums[i].idade / nums[i].vida;

            if (t >= 1f)
            {
                nums[i].ativo = false;
                nums[i].tr.gameObject.SetActive(false);
                continue;
            }

            // sobe desacelerando
            nums[i].vel.y -= gravidade * dt;
            nums[i].vel.x *= 1f - 2.2f * dt;
            nums[i].vel.z *= 1f - 2.2f * dt;
            nums[i].pos += nums[i].vel * dt;

            // estouro de escala na entrada, depois assenta em 1
            float alvoEscala = nums[i].tipo == Tipo.Critico ? escalaCritico : 1f;
            nums[i].escala = Mathf.Lerp(nums[i].escala, alvoEscala, 1f - Mathf.Exp(-16f * dt));

            // some nos ultimos 45% da vida
            float alfa = t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;

            // compensa distancia pra continuar legivel longe, com teto
            float dist = Vector3.Distance(nums[i].pos, cam.position);
            float escalaDist = Mathf.Max(1f, dist / distanciaReferencia);

            nums[i].tr.position = nums[i].pos;
            nums[i].tr.rotation = giroBillboard;
            nums[i].tr.localScale = Vector3.one * (nums[i].escala * escalaDist);

            Color c2 = nums[i].cor; c2.a = alfa;
            nums[i].txt.color = c2;
        }
    }
}
