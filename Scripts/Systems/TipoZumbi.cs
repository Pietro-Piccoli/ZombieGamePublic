using UnityEngine;

/// <summary>
/// UM TIPO DE ZUMBI. O director compra tipos usando creditos, igual ao
/// Risk of Rain 2 - e por isso que la a luta muda de FORMA, nao so de numero.
///
/// Adicionar um zumbi novo no jogo = criar um asset destes e arrastar na lista
/// do WaveManager. Nenhuma linha de codigo precisa mudar.
///   Create > Zombie > Tipo de Zumbi
/// </summary>
[CreateAssetMenu(menuName = "Zombie/Tipo de Zumbi", fileName = "NovoTipoZumbi")]
public class TipoZumbi : ScriptableObject
{
    [Header("Identidade")]
    public string nomeExibicao = "Zumbi";
    [Tooltip("Vazio = usa o prefab padrao do WaveManager. Preencha quando tiver modelo proprio.")]
    public GameObject prefabProprio;

    [Header("Director")]
    [Tooltip("Quanto custa em creditos. Caro = raro e forte.")]
    public int custo = 10;
    [Tooltip("Peso no sorteio entre os que cabem no orcamento.")]
    public float peso = 10f;
    [Tooltip("So aparece a partir deste nivel de dificuldade.")]
    public float nivelMinimo = 1f;
    [Tooltip("Para de aparecer depois deste nivel. 0 = nunca para.")]
    public float nivelMaximo = 0f;

    [Header("Stats (multiplicam a base)")]
    public float multVida = 1f;
    public float multDano = 1f;
    public float multVelocidade = 1f;
    [Tooltip("Escala do corpo. Bruto maior, corredor menor.")]
    public float escala = 1f;

    [Header("Recompensa")]
    [Tooltip("Multiplica dinheiro e XP que ele larga.")]
    public float multRecompensa = 1f;

    [Header("Visual")]
    [Tooltip("Tinta aplicada no corpo pra dar leitura de longe. Alpha 0 = nao pinta.")]
    public Color tinta = new Color(1f, 1f, 1f, 0f);

    /// <summary>Este tipo pode aparecer neste nivel de dificuldade?</summary>
    public bool Liberado(float nivel)
    {
        if (nivel < nivelMinimo) return false;
        if (nivelMaximo > 0f && nivel > nivelMaximo) return false;
        return true;
    }
}
