using UnityEngine;

/// <summary>
/// FICHA DE GRANADA. Um asset por tipo, todos os numeros no Inspector.
/// Criar granada nova = Create > Zombie > Granada e arrastar na lista do player.
/// </summary>
[CreateAssetMenu(menuName = "Zombie/Granada", fileName = "NovaGranada")]
public class GranadaData : ScriptableObject
{
    [Header("Identidade")]
    public string nomeExibicao = "Granada";
    [Tooltip("Cor usada no HUD e na luz da explosao.")]
    public Color cor = new Color(1f, 0.55f, 0.15f);

    [Header("Modelo")]
    public GameObject modelo;
    public float escalaModelo = 1f;

    [Header("Arremesso")]
    [Tooltip("Forca do arremesso, em m/s.")]
    public float forcaArremesso = 16f;
    [Tooltip("Quanto a mira sobe no arremesso, em graus. Faz a granada descrever arco.")]
    public float anguloArco = 12f;
    [Tooltip("Segundos ate estourar depois de sair da mao.")]
    public float pavio = 2.4f;
    [Tooltip("Estoura ao encostar em zumbi, sem esperar o pavio.")]
    public bool estouraNoContato = false;

    [Header("Explosao")]
    public GameObject vfxExplosao;
    [Tooltip("Escala aplicada no VFX da explosao.")]
    public float escalaVfx = 1f;
    public float raio = 6f;
    public int dano = 220;
    [Tooltip("Dano no centro cai ate 35% na borda.")]
    [Range(0f, 1f)] public float danoNaBorda = 0.35f;
    [Tooltip("Empurrao aplicado nos ragdolls.")]
    public float impulso = 14f;

    [Header("Fogo residual (granada incendiaria)")]
    public bool deixaFogo = false;
    public GameObject vfxFogoNoChao;
    public float raioFogo = 5f;
    public float duracaoFogo = 8f;
    [Tooltip("Dano por segundo em quem ficar dentro do fogo.")]
    public float dpsFogo = 26f;
    [Tooltip("Ajuste fino do tamanho do fogo: escala = raioFogo x isto.")]
    public float escalaVfxFogo = 0.6f;

    [Header("Recarga")]
    [Tooltip("Segundos ate poder usar de novo.")]
    public float recarga = 60f;
}
