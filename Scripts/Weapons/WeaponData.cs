using UnityEngine;

public enum GripType
{
    /// <summary>Duas maos, coronha no ombro.</summary>
    Rifle,
    /// <summary>Uma mao a frente (Taurus, Deagle, Python).</summary>
    Pistola
}

public enum FireMode
{
    /// <summary>Um tiro por clique.</summary>
    SemiAuto,
    /// <summary>Segurou, atirou.</summary>
    FullAuto
}

/// <summary>
/// FICHA da arma - um asset por arma, todos os numeros no Inspector.
/// Criar arma nova: Create > Zombie > Arma, preencher, arrastar no loadout.
/// </summary>
[CreateAssetMenu(menuName = "Zombie/Arma", fileName = "NovaArma")]
public class WeaponData : ScriptableObject
{
    [Header("Identidade")]
    public string displayName = "Arma";
    public FireMode fireMode = FireMode.SemiAuto;
    [Tooltip("Define o conjunto de animacoes: fuzil (2 maos) ou pistola.")]
    public GripType empunhadura = GripType.Rifle;

    [Header("Disparo")]
    [Tooltip("Tiros por segundo (no semi, e o teto de quao rapido da pra clicar).")]
    public float fireRate = 5f;
    [Tooltip("Dano POR PROJETIL. Escopeta com 10 pellets x 12 = 120 no alvo cheio.")]
    public int damage = 25;
    [Tooltip("Projeteis por tiro. 1 = bala. 6+ = escopeta.")]
    public int pellets = 1;
    public float range = 120f;

    [Header("Espalhamento (graus de meio-cone)")]
    [Tooltip("Atirando do quadril.")]
    public float hipSpread = 2f;
    [Tooltip("Mirando (botao direito). A recompensa do ADS.")]
    public float adsSpread = 0.4f;

    [Header("Impacto")]
    [Tooltip("Empurrao no ragdoll quando o tiro mata.")]
    public float killImpulse = 10f;

    [Header("Modelo 3D")]
    [Tooltip("Prefab/OBJ da arma em Assets/Art/Weapons.")]
    public GameObject modelPrefab;
    [Tooltip("Posicao local no socket da mao.")]
    public Vector3 modelPosition = Vector3.zero;
    [Tooltip("Rotacao local (graus).")]
    public Vector3 modelRotation = Vector3.zero;
    [Tooltip("Escala do modelo.")]
    public float modelScale = 1f;
    [Tooltip("Ponta do cano, em espaco local do modelo. So o rastro visual sai daqui.")]
    public Vector3 muzzleOffset = new Vector3(0f, 0f, 0.5f);
    [Tooltip("Janela do ferrolho: de onde a capsula pula. Espaco local do modelo.")]
    public Vector3 ejectionOffset = new Vector3(0.05f, 0.07f, 0.2f);


    [Header("Postura dos bracos (por cima da animacao)")]
    [Tooltip("Rotacoes extras por osso (ordem de PoseBracos.OSSOS). Ajuste na janela Ajuste de Arma > BRACOS.")]
    public Vector3[] poseBracos = new Vector3[0];

    [Header("Municao")]
    public bool infiniteAmmo = true;
    public int magazineSize = 30;
    public float reloadTime = 1.4f;

    [Header("Recuo")]
    [Tooltip("Quanto a MIRA sobe por tiro, em graus. Acumula na rajada.")]
    public float recuoVertical = 0.85f;
    [Tooltip("Quanto a MIRA vai pro lado por tiro, em graus. O lado e sorteado.")]
    public float recuoLateral = 0.35f;
    [Tooltip("Tendencia do lado: 0 = puro sorteio (se anula e some), +1 = sempre pra direita, -1 = sempre pra esquerda. E o que faz a arma ter PADRAO de recuo em vez de tremor aleatorio.")]
    [Range(-1f, 1f)]
    public float recuoTendenciaLado = 0.35f;
    [Tooltip("Fracao do recuo que a mira devolve sozinha. 1 = volta tudo (arcade), 0 = fica tudo (o jogador puxa).")]
    [Range(0f, 1f)]
    public float recuoRecuperacao = 0.8f;
    [Tooltip("Coice VISUAL: quanto a arma recua pra tras, em metros.")]
    public float coiceRecuo = 0.05f;
    [Tooltip("Coice VISUAL: quanto o cano levanta, em graus.")]
    public float coiceGiro = 7f;
}
