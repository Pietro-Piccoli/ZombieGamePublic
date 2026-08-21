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

/// <summary>Como a arma entrega o dano.</summary>
public enum TipoDisparo
{
    /// <summary>Raio instantaneo (fuzil, escopeta).</summary>
    Hitscan,
    /// <summary>Projetil que voa e estoura no impacto (RPG).</summary>
    Projetil
}

/// <summary>
/// FICHA da arma - um asset por arma, todos os numeros no Inspector.
/// Criar arma nova: Create > Zombie > Arma, preencher, arrastar no loadout.
/// </summary>
[CreateAssetMenu(menuName = "Zombie/Arma", fileName = "NovaArma")]
public class WeaponData : ScriptableObject
{
    [Header("Identidade")]
    [Tooltip("Chave usada no save. NAO mude depois de publicado.")]
    public string id = "arma";
    public string displayName = "Arma";
    [TextArea] public string descricao = "";
    [Tooltip("Rotulo curto de estilo, so pra UI: ALCANCE, PERTO, EXPLOSIVO.")]
    public string estilo = "";
    [Tooltip("Preco na armaria. 0 = ja vem com o jogador.")]
    public int preco = 0;
    [Tooltip("Ordem na lista da armaria.")]
    public int ordem = 0;
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

    [Header("Queda de dano por distancia")]
    [Tooltip("Ate esta distancia o dano e cheio. E o que separa a escopeta do fuzil: Left 4 Dead e CoD fazem exatamente isto.")]
    public float quedaInicio = 999f;
    [Tooltip("Daqui pra frente o dano e o minimo. Entre os dois, interpola.")]
    public float quedaFim = 999f;
    [Tooltip("Fracao do dano que sobra na distancia longa. 1 = sem queda.")]
    [Range(0f, 1f)]
    public float danoMinimo = 1f;

    [Header("Perfuracao")]
    [Tooltip("Quantos inimigos EXTRAS o projetil atravessa, sem depender de carta. E o que faz a escopeta abrir buraco numa fila de zumbis: Killing Floor usa o mesmo recurso.")]
    public int perfuracaoBase = 0;

    [Header("Impacto")]
    [Tooltip("Empurrao no ragdoll quando o tiro mata.")]
    public float killImpulse = 10f;

    [Header("Projetil (so quando tipoDisparo = Projetil)")]
    public TipoDisparo tipoDisparo = TipoDisparo.Hitscan;
    [Tooltip("Modelo do foguete. Vazio = uma capsula simples montada em codigo.")]
    public GameObject projetilModelo;
    public float projetilEscala = 1f;
    [Tooltip("Metros por segundo. RPG-7 real faz ~115; em jogo o padrao e bem mais lento pra dar pra desviar.")]
    public float projetilVelocidade = 32f;
    [Tooltip("Gravidade aplicada ao foguete. 0 = voa reto.")]
    public float projetilGravidade = 0f;
    public float explosaoRaio = 5.5f;
    public int explosaoDano = 260;
    [Tooltip("Fracao do dano na borda do estouro.")]
    [Range(0f, 1f)]
    public float explosaoDanoBorda = 0.35f;
    public float explosaoImpulso = 26f;
    [Tooltip("Raio dentro do qual o proprio jogador se machuca. 0 = nao machuca.")]
    public float explosaoRaioProprio = 0f;

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

    [Header("Pontos de montagem desta arma")]
    [Tooltip("Ligue pra arma que nao aceita anexo nenhum (rojao).")]
    public bool semAnexos = false;
    [Tooltip("Quais slots esta arma aceita. Vazio = os cinco padrao.")]
    public SlotAttach[] slots = new SlotAttach[0];
    [Tooltip("Ligue pra esta arma ter encaixes proprios em vez dos da AK.")]
    public bool encaixesProprios = false;
    [Tooltip("Posicao de encaixe por slot, na ordem do enum SlotAttach (6 posicoes).")]
    public Vector3[] encaixes = new Vector3[0];

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

    // ---------------- ajudas ----------------

    private static readonly SlotAttach[] SlotsPadrao = new SlotAttach[]
    { SlotAttach.Mira, SlotAttach.Cano, SlotAttach.UnderBarrel, SlotAttach.LateralEsq, SlotAttach.LateralDir };

    /// <summary>Os slots que esta arma realmente aceita.</summary>
    private static readonly SlotAttach[] SlotsNenhum = new SlotAttach[0];

    public SlotAttach[] SlotsDaArma
    {
        get
        {
            if (semAnexos) return SlotsNenhum;
            return (slots != null && slots.Length > 0) ? slots : SlotsPadrao;
        }
    }

    public bool AceitaSlot(SlotAttach s)
    {
        var ss = SlotsDaArma;
        for (int i = 0; i < ss.Length; i++) if (ss[i] == s) return true;
        return false;
    }

    /// <summary>Chave de save. Se o asset nao tem id preenchido, cai no nome do arquivo.</summary>
    public string Id { get { return string.IsNullOrEmpty(id) || id == "arma" ? name : id; } }

    /// <summary>
    /// Quanto do dano sobra a esta distancia. E o unico mecanismo que faz a
    /// escopeta e o fuzil ocuparem espacos diferentes sem um ser melhor.
    /// </summary>
    public float MultQueda(float distancia)
    {
        if (danoMinimo >= 1f || quedaFim <= quedaInicio) return 1f;
        if (distancia <= quedaInicio) return 1f;
        if (distancia >= quedaFim) return danoMinimo;
        float t = (distancia - quedaInicio) / (quedaFim - quedaInicio);
        return Mathf.Lerp(1f, danoMinimo, t);
    }
}
