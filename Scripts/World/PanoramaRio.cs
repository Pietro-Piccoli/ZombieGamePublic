
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gera o cenario do RIO DE JANEIRO visto da favela: o terreno, os morros
/// (Pao de Acucar, Urca, Corcovado, Dois Irmaos...), a Baia de Guanabara,
/// as praias, milhares de predios, os barcos e o Cristo no Corcovado.
///
/// Isto e CENARIO, nao mapa jogavel: fica alem da area de jogo, nao tem
/// colisao e nao entra em nenhuma fisica. Tudo sai de codigo a partir das
/// coordenadas reais dos morros, entao a relacao entre eles fica certa -
/// o Pao de Acucar fica mesmo a leste, o Corcovado mesmo atras.
///
/// O ponto de vista e um morro de Botafogo (tipo o Santa Marta).
///
/// Botao direito no componente > Gerar panorama.
/// </summary>
public class PanoramaRio : MonoBehaviour
{
    [Header("Enquadramento")]
    [Tooltip("Pra que lado do mundo fica o Pao de Acucar. 0 = +Z, 90 = +X.")]
    public float direcaoDaVista = 0f;
    [Tooltip("Centro da vista (o alto da favela).")]
    public Vector3 centro = new Vector3(0f, 0f, 20f);
    [Tooltip("Quanto o nivel do mar fica ABAIXO da favela. E o que da a sensacao de estar no alto.")]
    public float alturaDoMorro = 95f;

    [Header("Escala do cenario")]
    [Tooltip("Distancias reais x isto. Nao muda o tamanho aparente dos morros (altura comprime junto): muda so quanta nevoa cada um pega.")]
    [Range(0.2f, 1f)] public float compressaoDistancia = 0.5f;
    [Tooltip("Alturas dos morros x isto. ESTE e o botao que deixa a vista mais imponente.")]
    [Range(0.8f, 3.5f)] public float exageroAltura = 2.2f;
    [Tooltip("Menor = flanco mais ingreme, silhueta mais recortada. Maior = morro redondo de desenho.")]
    [Range(0.3f, 0.9f)] public float perfilMorro = 0.7f;
    [Tooltip("O exagero so vale pros morros longe. Perto do jogador o relevo fica real, senao o Corcovado vira uma parede.")]
    public float distanciaDoExagero = 1600f;
    [Tooltip("Onde fica o mapa jogavel, em coordenadas LOCAIS do panorama (X,Z). O plato e entalhado no morro deste ponto.")]
    public Vector2 posicaoDoMapa = Vector2.zero;
    [Tooltip("Raio do plato plano onde o mapa senta. Tem que cobrir a meia-diagonal do mapa.")]
    public float raioInterno = 55f;
    [Tooltip("Onde o entalhe do plato acaba e o morro volta a ser ele mesmo. MENOR que o raio do morro, senao o entalhe come o morro inteiro.")]
    public float raioSaia = 120f;
    [Tooltip("Altura do plato no CENTRO do mapa.")]
    public float alturaDoPlato = 0f;
    [Tooltip("Quanto o plato desce por metro em +Z. Acompanha a descida do mapa.")]
    public float inclinacaoDoPlato = 0f;
    [Tooltip("Ate onde o cenario vai.")]
    public float raioExterno = 7000f;
    [Tooltip("Largura da faixa de areia na costa, em metros reais.")]
    public float larguraPraia = 90f;

    [Header("Malha do terreno")]
    [Range(96, 384)] public int fatias = 256;
    [Range(40, 140)] public int aneis = 96;

    [Header("Cidade")]
    public bool gerarPredios = true;
    [Range(500, 12000)] public int quantidadePredios = 6000;
    public float alturaPredioMin = 12f;
    public float alturaPredioMax = 46f;
    [Tooltip("A que distancia da encosta a cidade comeca (x raioSaia). Baixo demais e a primeira fileira tapa a baia.")]
    [Range(1f, 4f)] public float inicioDaCidade = 1.7f;
    [Tooltip("Alguns arranha-ceus mais altos, como no Centro/Flamengo.")]
    public float alturaTorre = 110f;
    [Range(0f, 0.06f)] public float chanceTorre = 0.02f;

    [Header("Detalhes")]
    public bool gerarBarcos = true;
    [Range(0, 120)] public int quantidadeBarcos = 40;
    public bool gerarCristo = true;
    [Tooltip("O Cristo real tem 38 m. Neste tamanho ele sumiria: isto aumenta ele so o suficiente pra ler a silhueta.")]
    [Range(1f, 8f)] public float escalaCristo = 3f;
    [Tooltip("Parede invisivel no raio interno, pro jogador nao andar pra dentro do cenario.")]
    public bool paredeInvisivel = true;

    [Header("Cores")]
    public Color corMar = new Color(0.13f, 0.27f, 0.40f);
    public Color corAreia = new Color(0.86f, 0.80f, 0.66f);
    public Color corMata = new Color(0.15f, 0.28f, 0.14f);
    public Color corMataAlta = new Color(0.11f, 0.21f, 0.12f);
    public Color corGranito = new Color(0.38f, 0.375f, 0.37f);
    public Color corCidade = new Color(0.55f, 0.54f, 0.50f);

    [Header("Luz do cenario (independente do sol do jogo)")]
    public Color corSol = new Color(1f, 0.98f, 0.95f);
    public Vector3 direcaoSol = new Vector3(0.5f, 0.72f, -0.48f);
    [Range(0f, 1f)] public float ambiente = 0.45f;
    [Tooltip("0 = sombra dura entre a face no sol e a face na sombra. E o contraste que faz predio parecer predio.")]
    [Range(0f, 1f)] public float luzEnvolvente = 0.30f;
    [Tooltip("Grao procedural por pixel. E o que tira a cara de pintura borrada nos morros e na cidade.")]
    [Range(0f, 1f)] public float detalheTextura = 0.7f;
    [Range(0.2f, 2f)] public float exposicao = 1f;

    [Header("Nevoa (o que vende distancia)")]
    public Color corNevoa = new Color(0.66f, 0.76f, 0.87f);
    public float nevoaInicio = 400f;
    public float nevoaFim = 7600f;
    [Tooltip("1 = a borda do mar dissolve 100% no ceu. Menos que isso deixa uma faixa visivel no horizonte.")]
    [Range(0f, 1f)] public float nevoaMax = 1f;
    [Range(0.3f, 3f)] public float nevoaCurva = 1.1f;

    [Header("Ceu")]
    [Tooltip("Troca o skybox por um degrade cujo horizonte e exatamente a cor da nevoa.")]
    public bool trocarCeu = true;
    public Color ceuTopo = new Color(0.20f, 0.40f, 0.76f);
    public Color ceuMeio = new Color(0.50f, 0.68f, 0.90f);

    // --------- os morros de verdade, em metros a partir do mirante ---------
    private struct Morro
    {
        public string nome;
        public float norte, leste, altura, raio, arredondado, rocha;
        public Morro(string n, float no, float le, float h, float r, float a, float ro)
        { nome = n; norte = no; leste = le; altura = h; raio = r; arredondado = a; rocha = ro; }
    }

    // rocha = de que fracao da altura pra cima e pedra pelada em vez de mata.
    // O Pao e pedra quase inteiro (0.15). A Serra da Carioca e mata ate em
    // cima (0.95). E isso que diferencia a silhueta de um e do outro.
    private static readonly Morro[] MORROS = new Morro[]
    {
        //          nome                norte    leste   altura raio  perfil rocha
        new Morro("Pao de Acucar",      -690f,   3951f,  396f,  330f,  3.4f, 0.15f),
        new Morro("Morro da Urca",     -1112f,   3183f,  220f,  300f,  3.0f, 0.25f),
        new Morro("Babilonia",         -2058f,   2362f,  180f,  420f,  2.0f, 0.80f),
        new Morro("Corcovado",          -990f,  -1790f,  710f,  400f,  1.3f, 0.90f),
        new Morro("Serra da Carioca",  -2101f,  -3703f,  760f, 1200f,  1.6f, 0.95f),
        new Morro("Cantagalo",         -3561f,      5f,  150f,  330f,  2.2f, 0.75f),
        new Morro("Dois Irmaos",       -5565f,  -3993f,  533f,  460f,  3.0f, 0.45f),
        new Morro("Niteroi",            1448f,   7488f,  310f, 1400f,  1.5f, 0.85f),
        new Morro("Ilha do Governador", 5199f,   4497f,   90f, 1200f,  1.4f, 0.90f),
        // morrinhos de dentro do bairro: o Rio nao tem planicie limpa,
        // tem morro de mata brotando entre os predios o tempo todo.
        new Morro("Morro da Viuva",    -1261f,   1677f,  110f,  260f,  2.4f, 0.85f),
        new Morro("Santa Teresa",        179f,  -1423f,  260f,  760f,  1.8f, 0.92f),
        new Morro("Sao Joao",          -1821f,    677f,  180f,  380f,  2.2f, 0.88f),
        new Morro("Morro dos Cabritos",-3101f,   -723f,  290f,  520f,  2.0f, 0.86f),
    };

    private Material matPanorama;
    private Transform raiz;

    /// <summary>
    /// O projeto roda em espaco LINEAR. Cor de vertice e cor mandada por
    /// SetColor nao sao convertidas pela Unity: vao cruas pro shader e sao
    /// lidas como se ja fossem lineares. Sem esta conversao todo o Rio sai
    /// ~1.4x mais claro e lavado do que a cor escolhida no Inspector.
    /// </summary>
    private static Color Lin(Color c)
    {
        return QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
    }

    [ContextMenu("Gerar panorama")]
    public void Gerar()
    {
        LimparAntigo();
        raiz = new GameObject("PanoramaRio_Gerado").transform;
        raiz.SetParent(transform, false);

        PrepararMaterial();
        if (trocarCeu) PrepararCeu();
        GerarTerreno();
        if (gerarPredios) GerarCidade();
        if (gerarBarcos) GerarBarcos();
        if (gerarCristo) GerarCristo();
        if (paredeInvisivel) GerarParede();
        AjustarCameraENevoa();

        Debug.Log("[PanoramaRio] pronto. Filhos: " + raiz.childCount);
    }

    [ContextMenu("Limpar panorama")]
    public void LimparAntigo()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c.name.StartsWith("PanoramaRio_"))
            {
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }
        }
    }

    private void PrepararMaterial()
    {
        Shader sh = Shader.Find("Zombie/PanoramaVertexColor");
        if (sh == null) { Debug.LogError("[PanoramaRio] shader do panorama nao achado."); return; }
        matPanorama = new Material(sh);
        matPanorama.name = "PanoramaRio";
        AplicarParametrosDoMaterial();
    }

    /// <summary>Manda os valores do Inspector pro material. Pode chamar sozinho pra balancear sem regerar a malha.</summary>
    [ContextMenu("So atualizar cores e nevoa")]
    public void AplicarParametrosDoMaterial()
    {
        if (matPanorama == null)
        {
            var mr0 = transform.GetComponentInChildren<MeshRenderer>();
            if (mr0 != null) matPanorama = mr0.sharedMaterial;
            if (matPanorama == null) return;
        }
        matPanorama.SetColor("_CorSol", Lin(corSol));
        matPanorama.SetVector("_DirSol", direcaoSol.normalized);
        matPanorama.SetFloat("_Ambiente", ambiente);
        matPanorama.SetFloat("_Envolvente", luzEnvolvente);
        matPanorama.SetFloat("_Detalhe", detalheTextura);
        matPanorama.SetFloat("_Exposicao", exposicao);
        matPanorama.SetColor("_CorNevoa", Lin(corNevoa));
        matPanorama.SetFloat("_NevoaInicio", nevoaInicio);
        matPanorama.SetFloat("_NevoaFim", nevoaFim);
        matPanorama.SetFloat("_NevoaMax", nevoaMax);
        matPanorama.SetFloat("_NevoaCurva", nevoaCurva);
        AtualizarCeu();
    }

    private Material matCeu;

    private void PrepararCeu()
    {
        Shader sh = Shader.Find("Zombie/CeuDegrade");
        if (sh == null) { Debug.LogWarning("[PanoramaRio] shader do ceu nao achado, mantendo o skybox atual."); return; }
        matCeu = new Material(sh);
        matCeu.name = "CeuRio";
        RenderSettings.skybox = matCeu;
        AtualizarCeu();
        DynamicGI.UpdateEnvironment();
    }

    private void AtualizarCeu()
    {
        if (matCeu == null && RenderSettings.skybox != null &&
            RenderSettings.skybox.shader != null &&
            RenderSettings.skybox.shader.name == "Zombie/CeuDegrade")
            matCeu = RenderSettings.skybox;
        if (matCeu == null) return;
        matCeu.SetColor("_CorTopo", Lin(ceuTopo));
        matCeu.SetColor("_CorMeio", Lin(ceuMeio));
        // o horizonte do ceu e a MESMA cor da nevoa: e isso que faz a borda
        // do mar sumir em vez de virar uma faixa cinza.
        matCeu.SetColor("_CorHorizonte", Lin(corNevoa));
    }

    // ---------------- geografia ----------------

    /// <summary>Converte (norte, leste) reais pro mundo do jogo, ja com giro e compressao.</summary>
    private Vector2 ParaMundo(float norte, float leste)
    {
        float k = compressaoDistancia;
        float a = direcaoDaVista * Mathf.Deg2Rad;
        float ang = a + Mathf.PI * 0.5f;   // leste -> +Z (o Pao fica NA FRENTE)
        float x = (leste * Mathf.Cos(ang) - norte * Mathf.Sin(ang)) * k;
        float z = (leste * Mathf.Sin(ang) + norte * Mathf.Cos(ang)) * k;
        return new Vector2(x, z);
    }

    /// <summary>Quanto exagero um morro leva, pela distancia. Longe = exagero cheio; colado = real.</summary>
    private float ExageroEm(float distanciaMundo)
    {
        float t = Mathf.Clamp01(distanciaMundo / Mathf.Max(1f, distanciaDoExagero));
        return Mathf.Lerp(1f, exageroAltura, t * t * (3f - 2f * t));
    }

    /// <summary>Distancia da costa em cada direcao. Define onde acaba a terra.</summary>
    private float DistanciaDaCosta(float bearingGraus)
    {
        float b = Mathf.Repeat(bearingGraus, 360f);
        float[] ang = { 0f,  40f,  80f, 120f, 180f, 240f, 300f, 360f };
        float[] dst = { 2600f, 3200f, 4200f, 6000f, 8500f, 8500f, 4500f, 2600f };
        for (int i = 0; i < ang.Length - 1; i++)
        {
            if (b >= ang[i] && b <= ang[i + 1])
            {
                float t = Mathf.InverseLerp(ang[i], ang[i + 1], b);
                t = t * t * (3f - 2f * t);
                return Mathf.Lerp(dst[i], dst[i + 1], t) * compressaoDistancia;
            }
        }
        return 2000f * compressaoDistancia;
    }

    /// <summary>Altura do terreno num ponto do mundo (relativo ao centro).</summary>
    private float Altura(float x, float z, out float tipo)
    {
        // tipo: 0 = mar, 1 = areia, 2 = cidade, 3 = mata, 4 = granito, 5 = mata alta
        Vector2 p = new Vector2(x, z);
        float r = p.magnitude;
        float bearing = Mathf.Atan2(x, z) * Mathf.Rad2Deg - direcaoDaVista;

        float mar = -alturaDoMorro;

        // 1) terreno natural: mar, praia e planicie da cidade.
        // O centro do mundo NAO tem mais plato proprio: o mapa jogavel agora
        // e entalhado num morro de verdade (etapa 5), entao aqui volta a ser
        // cidade normal, plana e com casas.
        float costa = DistanciaDaCosta(bearing);
        bool naAgua = r > costa;
        float y;
        if (naAgua)
        {
            float prof = Mathf.Clamp01((r - costa) / (260f * compressaoDistancia));
            y = mar - 4f - prof * 22f;
        }
        else
        {
            // a planicie desce ate quase o nivel do mar chegando na costa:
            // sem isto a praia fica sorteada pelo ruido em vez de acompanhar
            // a linha da agua, e sai areia no meio do bairro.
            float ondula = Mathf.PerlinNoise(x * 0.0016f + 11.3f, z * 0.0016f + 4.7f);
            float rampa = Mathf.Clamp01((costa - r) / (500f * compressaoDistancia));
            rampa = rampa * rampa * (3f - 2f * rampa);
            y = mar + Mathf.Lerp(1.5f, 5f + ondula * 16f, rampa);
        }

        // 3) os morros. Guarda QUAL morro ganhou e que fracao da altura dele
        //    esse ponto esta, pra decidir depois se e pedra pelada ou mata.
        int quemGanhou = -1;
        float fracaoDoPico = 0f;
        for (int i = 0; i < MORROS.Length; i++)
        {
            Morro m = MORROS[i];
            Vector2 c = ParaMundo(m.norte, m.leste);
            float raio = m.raio * compressaoDistancia * 1.35f;
            float d = Vector2.Distance(p, c);
            if (d >= raio) continue;

            float t = d / raio;
            // perfil de domo: parede ingreme, topo redondo
            float f = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(t, m.arredondado)), perfilMorro);
            // a altura comprime junto com a distancia, senao o morro vira parede
            float pico = m.altura * compressaoDistancia * ExageroEm(c.magnitude);
            float h = pico * f;

            // saia insignificante nao mexe na agua: senao o mar em volta do Pao
            // sobe num montinho e a rocha parece nascer de um banco de areia.
            if (h < 3f) continue;

            if (mar + h > y)
            {
                y = mar + h;
                quemGanhou = i;
                fracaoDoPico = pico > 0.001f ? h / pico : 0f;
            }
        }

        // 4) classificacao pra cor
        if (quemGanhou >= 0)
        {
            tipo = fracaoDoPico >= MORROS[quemGanhou].rocha ? 4f
                 : (fracaoDoPico > 0.45f ? 5f : 3f);
        }
        else if (naAgua) tipo = 0f;
        else if ((costa - r) < larguraPraia * compressaoDistancia) tipo = 1f;   // faixa de areia
        else tipo = 2f;

        // 5) O PLATO do mapa jogavel, entalhado no topo do morro escolhido.
        // Dentro de raioInterno e um plano inclinado que acompanha a descida
        // do mapa; ate raioSaia ele volta pro morro natural. Fora disso o
        // morro fica intacto - por isso raioSaia e menor que o raio dele.
        float rf = Vector2.Distance(p, posicaoDoMapa);
        if (rf < raioSaia)
        {
            float yPlato = alturaDoPlato + inclinacaoDoPlato * (z - posicaoDoMapa.y);
            float tp = Mathf.Clamp01(Mathf.InverseLerp(raioInterno, raioSaia, rf));
            tp = tp * tp * (3f - 2f * tp);
            y = Mathf.Lerp(yPlato, y, tp);
            if (rf < raioSaia * 0.8f) tipo = 3f;   // encosta de mata em volta da favela
        }

        return y;
    }

    // ---------------- terreno ----------------

    private void GerarTerreno()
    {
        int NT = fatias, NR = aneis;
        var verts = new List<Vector3>((NT + 1) * (NR + 1));
        var cores = new List<Color>((NT + 1) * (NR + 1));
        var tris = new List<int>();

        float rIn = raioInterno * 0.92f;
        for (int ir = 0; ir <= NR; ir++)
        {
            float f = (float)ir / NR;
            float r = rIn * Mathf.Pow(raioExterno / rIn, f);   // anel exponencial: denso perto
            for (int it = 0; it <= NT; it++)
            {
                float a = 2f * Mathf.PI * it / NT;
                float x = r * Mathf.Sin(a);
                float z = r * Mathf.Cos(a);
                float tipo;
                float y = Altura(x, z, out tipo);
                verts.Add(new Vector3(x, y, z));
                cores.Add(CorDoTipo(tipo, y, x, z));
            }
        }
        for (int ir = 0; ir < NR; ir++)
        for (int it = 0; it < NT; it++)
        {
            int a = ir * (NT + 1) + it;
            int b = a + 1;
            int c = a + (NT + 1);
            int d = c + 1;
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
        }

        var malha = new Mesh();
        malha.name = "TerrenoRio";
        malha.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        malha.SetVertices(verts);
        malha.SetColors(cores);
        malha.SetTriangles(tris, 0);
        malha.RecalculateNormals();
        malha.RecalculateBounds();

        NovoPedaco("Terreno", malha, centro);
        GerarAgua();
    }

    private void GerarAgua()
    {
        int NT = 128;
        var verts = new List<Vector3>();
        var cores = new List<Color>();
        var tris = new List<int>();
        verts.Add(Vector3.zero); cores.Add(Lin(corMar));
        for (int i = 0; i <= NT; i++)
        {
            float a = 2f * Mathf.PI * i / NT;
            verts.Add(new Vector3(Mathf.Sin(a) * raioExterno * 1.4f, 0f, Mathf.Cos(a) * raioExterno * 1.4f));
            cores.Add(Lin(corMar * 0.92f));
        }
        for (int i = 1; i <= NT; i++) { tris.Add(0); tris.Add(i); tris.Add(i + 1); }

        var malha = new Mesh();
        malha.name = "Baia";
        malha.SetVertices(verts); malha.SetColors(cores); malha.SetTriangles(tris, 0);
        malha.RecalculateNormals(); malha.RecalculateBounds();

        NovoPedaco("Agua", malha, centro + Vector3.down * (alturaDoMorro - 1.5f));
    }

    private Color CorDoTipo(float tipo, float y, float x, float z)
    {
        float ruido = Mathf.PerlinNoise(x * 0.02f, z * 0.02f);
        if (tipo < 0.5f) return Lin(corMar * Mathf.Lerp(0.85f, 1.05f, ruido));
        if (tipo < 1.5f) return Lin(corAreia * Mathf.Lerp(0.94f, 1.06f, ruido));
        if (tipo < 2.5f)
        {
            // mancha de mata dentro do bairro: sem isto a cidade vira um
            // campo bege liso, que de longe parece deserto e nao Rio.
            float verde = Mathf.PerlinNoise(x * 0.0035f + 31.7f, z * 0.0035f + 8.2f);
            verde = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.78f, verde)) * 0.85f;
            Color c = Color.Lerp(corCidade * Mathf.Lerp(0.80f, 1.10f, ruido),
                                 corMata * Mathf.Lerp(0.8f, 1.2f, ruido), verde);
            return Lin(c);
        }
        if (tipo < 3.5f) return Lin(corMata * Mathf.Lerp(0.7f, 1.3f, ruido));
        if (tipo < 4.5f) return Lin(corGranito * Mathf.Lerp(0.88f, 1.08f, ruido));
        return Lin(corMataAlta * Mathf.Lerp(0.8f, 1.25f, ruido));
    }

    // ---------------- cidade ----------------

    private void GerarCidade()
    {
        var rnd = new System.Random(20260807);
        var blocos = new List<CombineInstance>();
        int porMalha = 1200;
        int feitos = 0, chunk = 0;

        var cubo = MalhaCubo();
        var quadrasFeitas = new HashSet<long>();

        for (int i = 0; i < quantidadePredios * 3 && feitos < quantidadePredios; i++)
        {
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float f = Mathf.Pow((float)rnd.NextDouble(), 1.35f);
            float r = Mathf.Lerp(40f, raioExterno * 0.42f, f);
            float x = Mathf.Sin(ang) * r;
            float z = Mathf.Cos(ang) * r;

            // clareira em volta do MAPA (o morro da favela), nao em volta do
            // centro do mundo: assim o lugar antigo volta a ter casas.
            float dMapa = Vector2.Distance(new Vector2(x, z), posicaoDoMapa);
            if (dMapa < raioSaia * inicioDaCidade) continue;

            float tipo;
            float y = Altura(x, z, out tipo);
            if (tipo < 1.6f) continue;          // mar ou areia: nao constroi
            if (tipo > 2.5f) continue;          // morro: nao constroi

            float quadra = 34f * compressaoDistancia;
            float gx = Mathf.Round(x / quadra) * quadra;
            float gz = Mathf.Round(z / quadra) * quadra;
            if (Mathf.Abs(x - gx) > quadra * 0.40f || Mathf.Abs(z - gz) > quadra * 0.40f) continue;

            // uma laje escura por quadra: quebra o chao liso em quarteiroes
            // e da o contraste claro-sobre-escuro que faz parecer cidade.
            long chave = ((long)Mathf.RoundToInt(gx / quadra) << 32) | (uint)Mathf.RoundToInt(gz / quadra);
            if (quadrasFeitas.Add(chave))
            {
                var laje = new CombineInstance();
                laje.mesh = CuboColorido(cubo, new Color(0.22f, 0.22f, 0.225f));
                laje.transform = Matrix4x4.TRS(new Vector3(gx, y + 0.8f, gz), Quaternion.identity,
                                               new Vector3(quadra * 0.90f, 1.6f, quadra * 0.90f));
                blocos.Add(laje);
            }

            // gabarito sobe com a distancia: junto da encosta e casa baixa,
            // o predio grande fica pro fundo. Senao a primeira fileira
            // vira um muro e tapa a baia inteira.
            float longe = Mathf.Clamp01((dMapa - raioSaia * inicioDaCidade) / (raioExterno * 0.30f));
            longe = longe * longe * (3f - 2f * longe);
            float teto = Mathf.Lerp(alturaPredioMin * 1.6f, alturaPredioMax, longe);
            bool torre = rnd.NextDouble() < chanceTorre * longe;
            float h = torre
                ? Mathf.Lerp(alturaTorre * 0.6f, alturaTorre, (float)rnd.NextDouble())
                : Mathf.Lerp(alturaPredioMin, teto, Mathf.Pow((float)rnd.NextDouble(), 1.6f));
            float larg = Mathf.Lerp(10f, 26f, (float)rnd.NextDouble()) * compressaoDistancia * 1.6f;
            float prof = Mathf.Lerp(10f, 26f, (float)rnd.NextDouble()) * compressaoDistancia * 1.6f;

            float tomVal = 0.42f + (float)rnd.NextDouble() * 0.50f;
            Color tom;
            double dado = rnd.NextDouble();
            if (dado < 0.16)                      // telhado / parede de tijolo
                tom = new Color(tomVal * 0.95f, tomVal * 0.62f, tomVal * 0.50f);
            else if (dado < 0.30)                 // predio escuro, vidro / sombra
                tom = new Color(tomVal * 0.55f, tomVal * 0.58f, tomVal * 0.62f);
            else                                  // concreto
                tom = new Color(tomVal, tomVal * 0.985f, tomVal * 0.945f);

            var ci = new CombineInstance();
            ci.mesh = CuboColorido(cubo, tom);
            ci.transform = Matrix4x4.TRS(new Vector3(x, y + h * 0.5f, z), Quaternion.identity,
                                         new Vector3(larg, h, prof));
            blocos.Add(ci);
            feitos++;

            if (blocos.Count >= porMalha) { FecharChunk(blocos, "Predios_" + (chunk++)); blocos.Clear(); }
        }
        if (blocos.Count > 0) FecharChunk(blocos, "Predios_" + (chunk++));
        Debug.Log("[PanoramaRio] predios: " + feitos + " em " + chunk + " malhas");
    }

    // ---------------- barcos ----------------

    private void GerarBarcos()
    {
        var rnd = new System.Random(777);
        var cubo = MalhaCubo();
        var blocos = new List<CombineInstance>();
        float mar = -alturaDoMorro + 1.5f;
        int feitos = 0;

        for (int i = 0; i < quantidadeBarcos * 12 && feitos < quantidadeBarcos; i++)
        {
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Lerp(raioSaia * 1.4f, raioExterno * 0.55f, Mathf.Pow((float)rnd.NextDouble(), 0.7f));
            float x = Mathf.Sin(ang) * r;
            float z = Mathf.Cos(ang) * r;
            float tipo;
            Altura(x, z, out tipo);
            if (tipo > 0.5f) continue;          // so na agua

            float esc = Mathf.Lerp(0.6f, 2.2f, (float)rnd.NextDouble()) * Mathf.Lerp(1f, 2.4f, r / raioExterno);
            float giro = (float)rnd.NextDouble() * 360f;
            var rot = Quaternion.Euler(0f, giro, 0f);
            var branco = new Color(0.88f, 0.88f, 0.86f);

            // casco
            var casco = new CombineInstance();
            casco.mesh = CuboColorido(cubo, branco * 0.86f);
            casco.transform = Matrix4x4.TRS(new Vector3(x, mar + 1.2f * esc, z), rot,
                                            new Vector3(4f * esc, 2.4f * esc, 14f * esc));
            blocos.Add(casco);

            // cabine
            var cab = new CombineInstance();
            cab.mesh = CuboColorido(cubo, branco);
            cab.transform = Matrix4x4.TRS(new Vector3(x, mar + 3.6f * esc, z), rot,
                                          new Vector3(3f * esc, 2.6f * esc, 5f * esc));
            blocos.Add(cab);

            // esteira de espuma atras
            Vector3 tras = rot * new Vector3(0f, 0f, -1f);
            var esp = new CombineInstance();
            esp.mesh = CuboColorido(cubo, new Color(0.80f, 0.86f, 0.90f));
            esp.transform = Matrix4x4.TRS(new Vector3(x, mar + 0.35f, z) + tras * 22f * esc, rot,
                                          new Vector3(5f * esc, 0.4f, 40f * esc));
            blocos.Add(esp);

            feitos++;
        }
        if (blocos.Count > 0) FecharChunk(blocos, "Barcos");
        Debug.Log("[PanoramaRio] barcos: " + feitos);
    }

    // ---------------- Cristo Redentor ----------------

    private void GerarCristo()
    {
        // acha o Corcovado na tabela e planta o Cristo no pico dele
        int idx = -1;
        for (int i = 0; i < MORROS.Length; i++) if (MORROS[i].nome == "Corcovado") idx = i;
        if (idx < 0) return;

        Vector2 c = ParaMundo(MORROS[idx].norte, MORROS[idx].leste);
        float tipo;
        float yPico = Altura(c.x, c.y, out tipo);

        // 38 m reais. Nesta escala ele some, entao leva um empurraozinho.
        float H = 38f * compressaoDistancia * escalaCristo;
        // gira o Cristo pra ele ficar de frente pra favela (bracos abertos
        // vistos de frente, nao de perfil)
        var rotC = Quaternion.Euler(0f, Mathf.Atan2(-c.x, -c.y) * Mathf.Rad2Deg, 0f);
        var cubo = MalhaCubo();
        var blocos = new List<CombineInstance>();
        var pedra = new Color(0.72f, 0.71f, 0.68f);

        // pedestal
        var ped = new CombineInstance();
        ped.mesh = CuboColorido(cubo, pedra * 0.8f);
        ped.transform = Matrix4x4.TRS(new Vector3(c.x, yPico + H * 0.10f, c.y), rotC,
                                      new Vector3(H * 0.30f, H * 0.20f, H * 0.30f));
        blocos.Add(ped);

        // corpo (o manto e mais largo embaixo: dois blocos ja leem)
        var corpo = new CombineInstance();
        corpo.mesh = CuboColorido(cubo, pedra);
        corpo.transform = Matrix4x4.TRS(new Vector3(c.x, yPico + H * 0.55f, c.y), rotC,
                                        new Vector3(H * 0.16f, H * 0.70f, H * 0.11f));
        blocos.Add(corpo);

        // cabeca
        var cab = new CombineInstance();
        cab.mesh = CuboColorido(cubo, pedra);
        cab.transform = Matrix4x4.TRS(new Vector3(c.x, yPico + H * 0.94f, c.y), rotC,
                                      new Vector3(H * 0.09f, H * 0.11f, H * 0.09f));
        blocos.Add(cab);

        // bracos abertos, virados pra cidade (pro jogador, que esta no +Z do Corcovado)
        var bracos = new CombineInstance();
        bracos.mesh = CuboColorido(cubo, pedra);
        bracos.transform = Matrix4x4.TRS(new Vector3(c.x, yPico + H * 0.78f, c.y), rotC,
                                         new Vector3(H * 0.62f, H * 0.09f, H * 0.09f));
        blocos.Add(bracos);

        FecharChunk(blocos, "Cristo");
    }

    // ---------------- parede invisivel ----------------

    private void GerarParede()
    {
        var go = new GameObject("PanoramaRio_Parede");
        go.transform.SetParent(raiz, false);
        go.transform.localPosition = centro;
        int N = 24;
        for (int i = 0; i < N; i++)
        {
            float a = 2f * Mathf.PI * i / N;
            var seg = new GameObject("seg_" + i);
            seg.transform.SetParent(go.transform, false);
            float sx = posicaoDoMapa.x + Mathf.Sin(a) * raioInterno;
            float sz = posicaoDoMapa.y + Mathf.Cos(a) * raioInterno;
            float st; float sy = Altura(sx, sz, out st);
            seg.transform.localPosition = new Vector3(sx, sy + 14f, sz);
            seg.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            var bc = seg.AddComponent<BoxCollider>();
            float largura = 2f * Mathf.PI * raioInterno / N * 1.25f;
            bc.size = new Vector3(largura, 34f, 1f);
        }
    }

    // ---------------- utilidades de malha ----------------

    private void FecharChunk(List<CombineInstance> blocos, string nome)
    {
        var m = new Mesh();
        m.name = nome;
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.CombineMeshes(blocos.ToArray(), true, true);
        m.RecalculateNormals();
        m.RecalculateBounds();
        NovoPedaco(nome, m, centro);
    }

    private void NovoPedaco(string nome, Mesh m, Vector3 posLocal)
    {
        // o shader do panorama le so posicao, normal e cor. UV e tangente
        // eram peso morto na memoria.
        m.uv = null; m.uv2 = null; m.tangents = null;
        m.UploadMeshData(false);

        var go = new GameObject("PanoramaRio_" + nome);
        go.transform.SetParent(raiz, false);
        go.transform.localPosition = posLocal;
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = matPanorama;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    private Mesh MalhaCubo()
    {
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh m = Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
        if (Application.isPlaying) Destroy(tmp); else DestroyImmediate(tmp);
        return m;
    }

    private Mesh CuboColorido(Mesh baseCubo, Color cor)
    {
        var m = Instantiate(baseCubo);
        var v = m.vertices;
        var c = new Color[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            float faixa = Mathf.Repeat((v[i].y + 0.5f) * 7f, 1f) < 0.42f ? 0.86f : 1f;
            // oclusao fake: a base do bloco e mais escura que o topo. E a
            // 'sombra de contato' que ancora o predio no chao de gratis.
            float ao = Mathf.Lerp(0.66f, 1f, Mathf.Clamp01(v[i].y + 0.5f));
            c[i] = Lin(cor * faixa * ao);
        }
        m.colors = c;
        return m;
    }

    // ---------------- camera e nevoa ----------------

    private void AjustarCameraENevoa()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, raioExterno * 2.2f);
            cam.nearClipPlane = Mathf.Max(cam.nearClipPlane, 0.25f);
        }
        RenderSettings.fog = false;   // a nevoa e do shader, com curva propria
    }
}
