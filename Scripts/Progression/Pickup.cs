using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orbe de drop (dinheiro ou XP). Nasce do zumbi morto, cai, quica no chao
/// DE VERDADE (por raycast - inclusive em laje, rampa e telhado) e quando o
/// player chega perto e sugado ate ele.
///
/// Fica no chao pra sempre por padrao. Pra nao acumular infinito e comer
/// memoria, existe um teto: passando dele, o orbe MAIS VELHO e coletado
/// automaticamente em vez de apagado - voce nunca perde o valor.
/// </summary>
public class Pickup : MonoBehaviour
{
    public enum Tipo { Dinheiro, Xp, Cura }

    [Header("O que e")]
    public Tipo tipo = Tipo.Dinheiro;
    public int valor = 5;

    [Tooltip("So pro KIT DE PRIMEIROS SOCORROS: quanto da vida MAXIMA ele devolve, em %.")]
    public float percentualDeCura = 30f;

    [Header("Ima")]
    [Tooltip("Distancia em que o orbe comeca a voar pro player.")]
    public float raioIma = 3.5f;
    [Tooltip("Velocidade maxima voando pro player.")]
    public float velocidadeIma = 14f;
    [Tooltip("Distancia em que coleta de vez.")]
    public float raioColeta = 0.5f;

    [Header("Queda")]
    [Tooltip("Camadas que contam como chao. Deixe SEM Player, Enemy e Hitbox.")]
    public LayerMask chaoMask = ~0;
    [Tooltip("Altura em que o orbe repousa acima da superficie.")]
    public float alturaRepouso = 0.14f;
    [Tooltip("Quanto da velocidade sobra a cada quique. 0 = nao quica.")]
    [Range(0f, 0.8f)] public float quique = 0.32f;
    public float gravidade = 14f;

    [Header("Duracao")]
    [Tooltip("Segundos ate sumir. 0 = FICA PRA SEMPRE (padrao).")]
    public float duracao = 0f;
    [Tooltip("Teto de orbes no mundo. Passando disso, o mais velho e coletado sozinho.")]
    public int tetoDeOrbes = 250;

    /// <summary>Multiplicador global do raio de coleta (carta IMA DE CAMPO).</summary>
    public static float MultRaioIma = 1f;

    private static Transform player;
    private static PlayerProgression prog;
    private static Mesh malhaCache;
    private static Material matDinheiro;
    private static Material matXp;
    private static Material matCura;
    private static GameObject modeloKit;
    private static bool modeloKitProcurado;
    private static readonly List<Pickup> vivos = new List<Pickup>();

    private Vector3 velocidade;
    private float nasceu;
    private float faseBob;
    private bool voando;
    private bool coletado;
    private bool assentou;
    private float alturaChao;

    /// <summary>Cria um orbe no mundo. Chamado pelo DropOnDeath.</summary>
    public static Pickup Spawn(Tipo tipo, int valor, Vector3 pos, LayerMask chao, float duracaoSeg, int teto)
    {
        string nome = tipo == Tipo.Dinheiro ? "Drop_Dinheiro" : (tipo == Tipo.Xp ? "Drop_XP" : "Drop_Kit");
        var go = new GameObject(nome);
        go.transform.position = pos;

        // MODELO TROCAVEL DO KIT
        // Se existir um prefab chamado "KitDeCura" dentro de qualquer pasta
        // Resources, ele e usado como corpo do kit. Enquanto nao existir, entra
        // a bolinha verde de sempre como marcador. Trocar o visual depois nao
        // pede mudanca de codigo nenhuma: e so por o prefab la.
        GameObject corpo = null;
        if (tipo == Tipo.Cura)
        {
            if (!modeloKitProcurado) { modeloKitProcurado = true; modeloKit = Resources.Load<GameObject>("KitDeCura"); }
            if (modeloKit != null)
            {
                corpo = Instantiate(modeloKit, go.transform);
                corpo.transform.localPosition = Vector3.zero;
                foreach (var c in corpo.GetComponentsInChildren<Collider>(true)) Destroy(c);
            }
        }

        if (corpo == null)
        {
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = Malha();
            mr.sharedMaterial = tipo == Tipo.Dinheiro ? MatDinheiro() : (tipo == Tipo.Xp ? MatXp() : MatCura());
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // o kit e maior de proposito: tem que ser visto do outro lado da rua.
        go.transform.localScale = Vector3.one *
            (tipo == Tipo.Dinheiro ? 0.16f : (tipo == Tipo.Xp ? 0.12f : 0.30f));

        var p = go.AddComponent<Pickup>();
        p.tipo = tipo;
        p.valor = valor;
        p.chaoMask = chao;
        p.duracao = duracaoSeg;
        p.tetoDeOrbes = teto;
        p.velocidade = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(2.4f, 3.6f), Random.Range(-1.2f, 1.2f));
        return p;
    }

    private static Mesh Malha()
    {
        if (malhaCache == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            malhaCache = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
        }
        return malhaCache;
    }

    private static Material NovoMat(Color cor)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        var m = new Material(sh);
        m.SetColor("_BaseColor", cor);
        if (m.HasProperty("_Color")) m.color = cor;
        return m;
    }

    private static Material MatDinheiro()
    {
        if (matDinheiro == null) matDinheiro = NovoMat(new Color(1f, 0.85f, 0.15f));
        return matDinheiro;
    }

    private static Material MatXp()
    {
        if (matXp == null) matXp = NovoMat(new Color(0.35f, 0.9f, 1f));
        return matXp;
    }

    private static Material MatCura()
    {
        if (matCura == null) matCura = NovoMat(new Color(0.35f, 1f, 0.5f));
        return matCura;
    }

    private void Start()
    {
        nasceu = Time.time;
        faseBob = Random.value * 6.28f;
        vivos.Add(this);

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) p = GameObject.Find("Player");
            if (p != null) { player = p.transform; prog = p.GetComponent<PlayerProgression>(); }
        }

        // estourou o teto: o mais velho vai pro bolso do player em vez de virar lixo.
        // ATENCAO: Coletar() PRECISA tirar da lista na hora. O Destroy da Unity e
        // diferido (so roda no fim do frame), entao se a remocao ficar no OnDestroy
        // este while nunca termina. Foi esse loop que estourou a memoria do sistema.
        int teto = Mathf.Max(20, tetoDeOrbes);
        int guarda = 0;
        while (vivos.Count > teto && guarda++ < 512)
        {
            Pickup velho = null;
            for (int i = 0; i < vivos.Count; i++)
                if (vivos[i] != null && vivos[i] != this && !vivos[i].coletado
                    && vivos[i].tipo != Tipo.Cura)   // kit nunca e recolhido sozinho: curar de longe seria absurdo
                { velho = vivos[i]; break; }
            if (velho == null) break;
            velho.Coletar();
        }
    }

    private void OnDestroy() { vivos.Remove(this); }

    private void Coletar()
    {
        if (coletado) return;      // Destroy e diferido: sem isso da pra coletar o mesmo orbe infinitas vezes
        coletado = true;
        vivos.Remove(this);        // AGORA, nao no OnDestroy

        if (tipo == Tipo.Cura)
        {
            // O kit nao enche a barra na hora: abre um credito que escorre nos
            // segundos seguintes, com o circulo de cura aceso enquanto durar.
            if (player != null) CuraGradual.Aplicar(player.gameObject, percentualDeCura);
        }
        else if (prog != null)
        {
            if (tipo == Tipo.Dinheiro) prog.AddDinheiro(valor);
            else prog.AddXp(valor);
        }
        Destroy(gameObject);
    }

    private void Update()
    {
        if (duracao > 0f && Time.time - nasceu > duracao) { coletado = true; vivos.Remove(this); Destroy(gameObject); return; }
        if (player == null) return;

        Vector3 alvo = player.position + Vector3.up * 1.0f;
        float dist = Vector3.Distance(transform.position, alvo);

        if (dist < raioColeta) { Coletar(); return; }
        if (dist < raioIma * MultRaioIma) voando = true;

        if (voando)
        {
            float v = Mathf.Lerp(velocidadeIma, velocidadeIma * 0.45f, dist / raioIma);
            transform.position = Vector3.MoveTowards(transform.position, alvo, v * Time.deltaTime);
        }
        else if (!assentou)
        {
            CairComQuique();
        }
        else
        {
            // flutuacao suave em cima da superficie onde parou
            Vector3 p = transform.position;
            p.y = alturaChao + alturaRepouso + Mathf.Sin(Time.time * 2.2f + faseBob) * 0.035f;
            transform.position = p;
        }

        transform.Rotate(0f, 110f * Time.deltaTime, 0f, Space.World);
    }

    /// <summary>
    /// Queda com deteccao real de superficie: raycast do ponto atual ate onde
    /// o orbe VAI parar neste frame. Assim ele nao atravessa laje fina nem
    /// telhado, que era o bug de usar altura fixa do mundo.
    /// </summary>
    private void CairComQuique()
    {
        velocidade.y -= gravidade * Time.deltaTime;
        Vector3 pos = transform.position;
        Vector3 novo = pos + velocidade * Time.deltaTime;

        if (velocidade.y < 0f)
        {
            float queda = (pos.y - novo.y) + alturaRepouso + 0.05f;
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 0.02f, Vector3.down, out hit, queda,
                                chaoMask, QueryTriggerInteraction.Ignore))
            {
                alturaChao = hit.point.y;
                novo.x = pos.x + velocidade.x * Time.deltaTime;
                novo.z = pos.z + velocidade.z * Time.deltaTime;
                novo.y = alturaChao + alturaRepouso;

                velocidade.y = -velocidade.y * quique;
                velocidade.x *= 0.55f;
                velocidade.z *= 0.55f;

                if (velocidade.y < 0.6f)   // parou de quicar
                {
                    velocidade = Vector3.zero;
                    assentou = true;
                }
            }
        }
        transform.position = novo;

        // rede de seguranca: caiu do mapa
        if (transform.position.y < -30f) Destroy(gameObject);
    }
}
