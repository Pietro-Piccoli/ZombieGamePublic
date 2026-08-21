
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// BASE DO RIO - gera o heightmap inicial da regiao da Enseada de Botafogo.
/// E RASCUNHO pra voce remodelar na mao depois. Roda uma vez e para.
///
/// Escala 1:1 (1 unidade = 1 m real). Origem = pico do Pao de Acucar.
/// Nivel do mar = y 0. Tiles ficam em y -100 com Terrain Height 1100,
/// entao da pra ter fundo de mar ate -100 e morro ate +1000.
///
/// Os MORROS estao em coordenada conferida em fonte. A LINHA DA COSTA e
/// aproximada - e justamente o que voce vai refinar.
/// </summary>
public class BaseRio : MonoBehaviour
{
    [Header("Grade de terrenos")]
    public int tileX0 = -3, tileX1 = 0;      // tiles de 1000 m, indice
    public int tileZ0 = -3, tileZ1 = 1;
    public int tamanhoTile = 1000;
    public int resolucao = 513;
    public float baseY = -100f;              // y do tile (fundo do heightmap)
    public float alturaTerreno = 1100f;      // Terrain Height

    [Header("Terra")]
    public float alturaPlanicie = 8f;        // cota dos bairros planos
    public float larguraPraia = 120f;        // rampa da agua ate a planicie
    public float profundidadeMar = 60f;      // fundo do mar longe da costa
    public float alcanceDoMar = 700f;        // em quantos metros chega no fundo
    [Range(0f, 30f)] public float ruido = 6f;

    [System.Serializable]
    public struct Morro
    {
        public string nome;
        public float x, z;        // metros, origem = pico do Pao
        public float altura;      // m acima do mar
        public float raio;        // m ate a base
        public float perfil;      // maior = parede mais ingreme
        public float topo;        // menor = topo mais pontudo
    }

    // posicoes conferidas em fonte (peakvisor / wikipedia / aroundus)
    public Morro[] morros = new Morro[]
    {
        new Morro { nome="Pao de Acucar",     x=    0f, z=    0f, altura=396f, raio=340f, perfil=3.6f, topo=0.50f },
        new Morro { nome="Morro da Urca",     x= -743f, z= -293f, altura=223f, raio=330f, perfil=3.0f, topo=0.55f },
        new Morro { nome="Morro Cara de Cao", x= -560f, z=  330f, altura=136f, raio=250f, perfil=2.6f, topo=0.60f },
        new Morro { nome="Morro da Babilonia",x=-1290f, z= -976f, altura=220f, raio=560f, perfil=2.0f, topo=0.70f },
        new Morro { nome="Morro do Leme",     x= -486f, z=-1651f, altura=150f, raio=380f, perfil=2.2f, topo=0.65f },
        new Morro { nome="Morro do Pasmado",  x=-2223f, z=  423f, altura= 70f, raio=200f, perfil=2.2f, topo=0.70f },
    };

    // planicies (bairros). Retangulos em metros: xMin,zMin,xMax,zMax
    public Vector4[] planicies = new Vector4[]
    {
        new Vector4(-1150f, -420f,  -820f,  380f),   // bairro da Urca (leste da enseada)
        new Vector4(-3400f, -250f, -2150f, 1500f),   // Botafogo / Flamengo (oeste da enseada)
        new Vector4(-2200f, -700f, -1050f, -250f),   // Av. Pasteur: fecha o fundo da enseada
        new Vector4(-3400f,-1200f, -2200f, -250f),   // Humaita / Botafogo sul
        new Vector4(-1150f,-2050f,  -450f,-1450f),   // Leme
        new Vector4(-1700f,-2600f,  -750f,-1950f),   // Copacabana norte
        new Vector4(-2400f,-3400f, -1300f,-2500f),   // Copacabana sul
    };

    /// <summary>Altura do terreno num ponto do mundo, em metros acima do mar.</summary>
    public float Altura(float wx, float wz)
    {
        // 1) morros - a parte que esta geograficamente certa
        float h = -9999f;
        for (int i = 0; i < morros.Length; i++)
        {
            Morro m = morros[i];
            float d = Mathf.Sqrt((wx - m.x) * (wx - m.x) + (wz - m.z) * (wz - m.z));
            if (d >= m.raio) continue;
            float t = d / m.raio;
            float f = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(t, m.perfil)), m.topo);
            float hm = m.altura * f;
            if (hm > h) h = hm;
        }

        // 2) planicie: distancia pra dentro do retangulo mais proximo
        float dentro = -9999f;
        for (int i = 0; i < planicies.Length; i++)
        {
            Vector4 p = planicies[i];
            float dx = Mathf.Min(wx - p.x, p.z - wx);      // p.x=xMin, p.z=xMax
            float dz = Mathf.Min(wz - p.y, p.w - wz);      // p.y=zMin, p.w=zMax
            float d = Mathf.Min(dx, dz);
            if (d > dentro) dentro = d;
        }
        if (dentro > -9999f)
        {
            float rampa = Mathf.Clamp01(dentro / larguraPraia);
            rampa = rampa * rampa * (3f - 2f * rampa);
            float hp = Mathf.Lerp(0f, alturaPlanicie, rampa);
            if (dentro > 0f && hp > h) h = hp;
        }

        // 3) nada de terra aqui: e mar
        if (h < -9998f)
        {
            float distTerra = DistanciaAteTerra(wx, wz);
            float t = Mathf.Clamp01(distTerra / alcanceDoMar);
            t = t * t * (3f - 2f * t);
            return -profundidadeMar * t;
        }

        // 4) rugosidade, so na terra alta
        if (h > 12f)
        {
            float n = Mathf.PerlinNoise(wx * 0.006f + 3.1f, wz * 0.006f + 7.7f) - 0.5f;
            h += n * ruido * Mathf.Clamp01(h / 60f);
        }
        return h;
    }

    private float DistanciaAteTerra(float wx, float wz)
    {
        float melhor = 9999f;
        for (int i = 0; i < morros.Length; i++)
        {
            Morro m = morros[i];
            float d = Mathf.Sqrt((wx - m.x) * (wx - m.x) + (wz - m.z) * (wz - m.z)) - m.raio;
            if (d < melhor) melhor = d;
        }
        for (int i = 0; i < planicies.Length; i++)
        {
            Vector4 p = planicies[i];
            float dx = Mathf.Max(0f, Mathf.Max(p.x - wx, wx - p.z));
            float dz = Mathf.Max(0f, Mathf.Max(p.y - wz, wz - p.w));
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < melhor) melhor = d;
        }
        return Mathf.Max(0f, melhor);
    }

#if UNITY_EDITOR
    /// <summary>Cria (ou refaz) UM tile. Chamado em lotes pra nao travar a Unity.</summary>
    public string GerarTile(int ix, int iz)
    {
        float ox = ix * tamanhoTile, oz = iz * tamanhoTile;
        string nome = "RIO_" + ix + "_" + iz;

        var existente = GameObject.Find(nome);
        Terrain ter;
        TerrainData td;
        if (existente != null)
        {
            ter = existente.GetComponent<Terrain>();
            td = ter.terrainData;
        }
        else
        {
            td = new TerrainData();
            td.heightmapResolution = resolucao;
            AssetDatabase.CreateAsset(td, "Assets/Terrenos/" + nome + ".asset");
            var go = Terrain.CreateTerrainGameObject(td);
            go.name = nome;
            if (transform != null) go.transform.SetParent(transform, true);
            ter = go.GetComponent<Terrain>();
        }
        td.heightmapResolution = resolucao;
        td.size = new Vector3(tamanhoTile, alturaTerreno, tamanhoTile);
        ter.transform.position = new Vector3(ox, baseY, oz);
        ter.groupingID = 0;
        ter.allowAutoConnect = true;

        int r = td.heightmapResolution;
        float[,] h = new float[r, r];
        float passo = (float)tamanhoTile / (r - 1);
        for (int j = 0; j < r; j++)
        {
            float wz = oz + j * passo;
            for (int i = 0; i < r; i++)
            {
                float wx = ox + i * passo;
                float metros = Altura(wx, wz);
                h[j, i] = Mathf.Clamp01((metros - baseY) / alturaTerreno);
            }
        }
        td.SetHeights(0, 0, h);
        EditorUtility.SetDirty(td);
        return nome;
    }
#endif
}
