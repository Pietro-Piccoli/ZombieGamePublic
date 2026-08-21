using UnityEngine;

/// <summary>
/// UM anexo da arma. Cada peca e um asset proprio: o que ela e, onde encaixa,
/// quanto custa e o que muda no tiro.
///
/// Substitui o modelo antigo de PRESET (build fechada). Agora o jogador compra
/// peca por peca e monta a arma dele, que e como CoD e Tarkov fazem.
///
/// COMPATIBILIDADE: uma peca so aparece na armaria das armas listadas em
/// 'armas'. Vazio = serve pra qualquer arma. Trilho de escopeta nao encaixa
/// em fuzil, e o jogo nao deve nem oferecer.
/// </summary>
[CreateAssetMenu(menuName = "Zombie/Anexo de Arma", fileName = "NovoAnexo")]
public class AnexoArma : ScriptableObject
{
    [Header("Identidade")]
    [Tooltip("Chave usada no save. NAO mude depois de publicado.")]
    public string id = "anexo";
    public string nomeExibicao = "Anexo";
    [TextArea] public string descricao = "";

    [Header("Encaixe")]
    public SlotAttach slot = SlotAttach.Mira;
    public GameObject prefab;
    [Tooltip("Deixe zerado pra usar o encaixe padrao do slot NA ARMA equipada.")]
    public bool encaixeProprio;
    public Vector3 posicao;
    public Vector3 rotacao;
    public float escala = 1f;

    [Header("Compatibilidade")]
    [Tooltip("Ids das armas que aceitam esta peca. Vazio = todas.")]
    public string[] armas = new string[0];

    [Header("Loja")]
    public int preco = 400;

    [Header("Efeito no tiro (1 = neutro)")]
    [Tooltip("Multiplica o espalhamento. 0.85 = 15% mais preciso.")]
    public float multEspalhamento = 1f;
    [Tooltip("Multiplica a cadencia. 1.05 = 5% mais rapido.")]
    public float multCadencia = 1f;
    [Tooltip("Multiplica o dano.")]
    public float multDano = 1f;
    [Tooltip("Multiplica a forca do recuo. 0.8 = coice 20% menor.")]
    public float multRecuo = 1f;

    /// <summary>Esta peca serve na arma de id informado?</summary>
    public bool ServePara(string idArma)
    {
        if (armas == null || armas.Length == 0) return true;
        if (string.IsNullOrEmpty(idArma)) return false;
        for (int i = 0; i < armas.Length; i++)
            if (armas[i] == idArma) return true;
        return false;
    }

    /// <summary>Encaixe padrao por slot, medido e validado na AK47_LowPoly.</summary>
    public static Vector3 PosicaoPadrao(SlotAttach s)
    {
        switch (s)
        {
            case SlotAttach.Mira:        return new Vector3(0f, 0.116f, 0.111f);
            // 0.0375 = eixo REAL do cano, medido no histograma de vertices da ponta
            // (o furo fica entre y 0.030 e 0.045). Antes estava em 0.069, que e o
            // topo da massa de mira - por isso o supressor flutuava acima do cano.
            case SlotAttach.Cano:        return new Vector3(0f, 0.0375f, 0.797f);
            case SlotAttach.UnderBarrel: return new Vector3(0f, 0.002f, 0.368f);
            case SlotAttach.LateralEsq:  return new Vector3(-0.045f, 0.045f, 0.42f);
            case SlotAttach.LateralDir:  return new Vector3(0.045f, 0.045f, 0.42f);
            default:                     return Vector3.zero;
        }
    }

    /// <summary>
    /// Encaixe padrao do slot NA ARMA dada. Cada arma tem geometria propria:
    /// o trilho da escopeta nao esta na mesma altura do da AK.
    /// </summary>
    public static Vector3 PosicaoPadrao(SlotAttach s, WeaponData arma)
    {
        if (arma != null && arma.encaixesProprios && arma.encaixes != null && arma.encaixes.Length > (int)s)
            return arma.encaixes[(int)s];
        return PosicaoPadrao(s);
    }

    public Vector3 PosicaoFinal { get { return encaixeProprio ? posicao : PosicaoPadrao(slot); } }
    public Vector3 RotacaoFinal { get { return encaixeProprio ? rotacao : Vector3.zero; } }
    public float   EscalaFinal  { get { return Mathf.Max(0.01f, escala); } }

    /// <summary>Encaixe final considerando a arma equipada.</summary>
    public Vector3 PosicaoNa(WeaponData arma)
    {
        return encaixeProprio ? posicao : PosicaoPadrao(slot, arma);
    }

    /// <summary>Nome legivel do slot, pra UI.</summary>
    public static string NomeSlot(SlotAttach s)
    {
        switch (s)
        {
            case SlotAttach.Mira:        return "MIRA";
            case SlotAttach.Cano:        return "CANO";
            case SlotAttach.UnderBarrel: return "EMPUNHADURA";
            case SlotAttach.LateralEsq:  return "LATERAL ESQ";
            case SlotAttach.LateralDir:  return "LATERAL DIR";
            default:                     return "TRILHO";
        }
    }
}
