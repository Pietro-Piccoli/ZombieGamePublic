using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracado urbano da zona sul: arterias REAIS (coordenadas conferidas) +
/// parametros da malha de quadras de cada bairro (orientacao e espacamento
/// reais). Tudo em lat/lon, convertido pelos mesmos P() do RioDados.
/// </summary>
public static class RioCidadeDados
{
    // ---------- ARTERIAS REAIS ----------
    // largura em metros; pontos lat,lon em sequencia
    public class Via { public string nome; public float larg; public double[] ll; public bool poste; }

    public static readonly Via[] ARTERIAS = new Via[]
    {
        new Via{ nome="Av. Atlantica", larg=14, poste=true, ll=new double[]{
            -22.96395,-43.16505, -22.96650,-43.16720, -22.97080,-43.17060,
            -22.97560,-43.17470, -22.98080,-43.17910, -22.98620,-43.18480, -22.98750,-43.18800 }},
        new Via{ nome="Av. N.S. de Copacabana", larg=11, poste=true, ll=new double[]{
            -22.96410,-43.16740, -22.96690,-43.16960, -22.97120,-43.17300,
            -22.97600,-43.17720, -22.98110,-43.18160, -22.98590,-43.18690 }},
        new Via{ nome="R. Barata Ribeiro / Tonelero", larg=9, poste=true, ll=new double[]{
            -22.96450,-43.16930, -22.96760,-43.17170, -22.97190,-43.17510,
            -22.97670,-43.17930, -22.98140,-43.18370, -22.98510,-43.18820 }},
        new Via{ nome="Av. Princesa Isabel", larg=14, poste=true, ll=new double[]{
            -22.96390,-43.16780, -22.96280,-43.17120, -22.96190,-43.17420 }},
        new Via{ nome="Av. Praia de Botafogo", larg=16, poste=true, ll=new double[]{
            -22.93780,-43.17590, -22.94240,-43.17830, -22.94700,-43.18100,
            -22.95050,-43.18280, -22.95350,-43.18220, -22.95520,-43.17920 }},
        new Via{ nome="R. Sao Clemente", larg=10, poste=true, ll=new double[]{
            -22.94980,-43.18280, -22.95050,-43.18630, -22.95120,-43.19000,
            -22.95160,-43.19350, -22.95120,-43.19700, -22.95000,-43.19980 }},
        new Via{ nome="R. Voluntarios da Patria", larg=10, poste=true, ll=new double[]{
            -22.95210,-43.18400, -22.95330,-43.18780, -22.95440,-43.19180, -22.95500,-43.19560 }},
        new Via{ nome="R. Sao Joao Batista / Real Grandeza", larg=8, poste=false, ll=new double[]{
            -22.95350,-43.18500, -22.95590,-43.18680, -22.95830,-43.18900 }},
        new Via{ nome="Av. Pasteur", larg=12, poste=true, ll=new double[]{
            -22.95370,-43.17430, -22.95300,-43.17100, -22.95190,-43.16760, -22.94990,-43.16480 }},
        new Via{ nome="Av. Portugal (Urca)", larg=9, poste=true, ll=new double[]{
            -22.94990,-43.16480, -22.94800,-43.16400, -22.94570,-43.16350 }},
        new Via{ nome="Av. Sao Sebastiao (Urca)", larg=8, poste=false, ll=new double[]{
            -22.94900,-43.16650, -22.94680,-43.16580, -22.94480,-43.16480 }},
        new Via{ nome="Praia do Flamengo", larg=16, poste=true, ll=new double[]{
            -22.92150,-43.17240, -22.92700,-43.17420, -22.93250,-43.17560, -22.93700,-43.17600 }},
        new Via{ nome="R. Marques de Abrantes", larg=10, poste=true, ll=new double[]{
            -22.93190,-43.17840, -22.93440,-43.18010, -22.93690,-43.18190 }},
        new Via{ nome="R. Senador Vergueiro", larg=9, poste=false, ll=new double[]{
            -22.92880,-43.17650, -22.93280,-43.17920, -22.93630,-43.18120 }},
        new Via{ nome="R. das Laranjeiras / Cosme Velho", larg=10, poste=true, ll=new double[]{
            -22.93180,-43.18000, -22.93400,-43.18440, -22.93650,-43.18880,
            -22.93950,-43.19330, -22.94210,-43.19790, -22.94340,-43.20250 }},
        new Via{ nome="R. Gal. Glicerio / Alice", larg=8, poste=false, ll=new double[]{
            -22.93650,-43.18880, -22.93950,-43.18980, -22.94250,-43.19120 }},
    };

    // ---------- MALHA DE QUADRAS POR BAIRRO ----------
    // eixo = direcao das ruas "paralelas" (em metros, normalizado no gerador)
    public class Malha
    {
        public int bairro;           // indice em RioDados.BAIRRO_NOME
        public double eixoX, eixoZ;  // direcao das paralelas
        public float espPar;         // distancia entre paralelas
        public float espCruz;        // distancia entre transversais
        public float larg;           // largura das ruas da malha
        public float altMin, altMax; // faixa de altura dos predios (m)
        public float frenteMin, frenteMax; // largura de fachada
        public bool casas;           // bairro de casas (baixo)
    }

    public static readonly Malha[] MALHAS = new Malha[]
    {
        // Flamengo: quadras paralelas a orla, predios altos densos
        new Malha{ bairro=0, eixoX=-0.32, eixoZ=-0.95, espPar=135, espCruz=120, larg=8,
                   altMin=24, altMax=45, frenteMin=16, frenteMax=34, casas=false },
        // Botafogo: malha voltada pra enseada, mistura 4-14 andares
        new Malha{ bairro=1, eixoX=-0.45, eixoZ=-0.89, espPar=100, espCruz=95, larg=8,
                   altMin=15, altMax=45, frenteMin=14, frenteMax=30, casas=false },
        // Urca: CASAS 2-3 andares, malha curta
        new Malha{ bairro=2, eixoX=-0.90, eixoZ=0.44, espPar=90, espCruz=80, larg=7,
                   altMin=6, altMax=10, frenteMin=9, frenteMax=16, casas=true },
        // Leme: igual Copacabana
        new Malha{ bairro=3, eixoX=-0.66, eixoZ=-0.76, espPar=170, espCruz=110, larg=8,
                   altMin=27, altMax=39, frenteMin=18, frenteMax=36, casas=false },
        // Copacabana: paredao de 11-13 andares, travessas a cada ~110 m
        new Malha{ bairro=4, eixoX=-0.66, eixoZ=-0.76, espPar=175, espCruz=110, larg=8,
                   altMin=27, altMax=42, frenteMin=18, frenteMax=38, casas=false },
        // Laranjeiras / Cosme Velho: vale, 3-8 andares + casas
        new Malha{ bairro=5, eixoX=-0.86, eixoZ=-0.50, espPar=130, espCruz=125, larg=8,
                   altMin=9, altMax=24, frenteMin=12, frenteMax=26, casas=false },
    };

    // ---------- FAVELA (Santa Marta) ----------
    public const float FAV_CASA_MIN = 4.0f, FAV_CASA_MAX = 7.5f;  // lado
    public const float FAV_ALT_MIN = 3.0f, FAV_ALT_MAX = 6.5f;    // altura
    public const float FAV_PASSO = 9.5f;                          // aneis
}
