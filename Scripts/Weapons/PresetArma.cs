using System;
using UnityEngine;

/// <summary>Os pontos da arma onde cabe upgrade.</summary>
public enum SlotAttach
{
    Mira,           // topo do trilho
    Cano,           // ponta: freio, flash hider, supressor
    UnderBarrel,    // embaixo do guarda-mao: grip
    LateralEsq,     // laser / lanterna
    LateralDir,     // laser / lanterna
    Trilho          // trilhos extras
}

/// <summary>Uma peca montada num slot, com o ajuste fino de encaixe.</summary>
[Serializable]
public class PecaMontada
{
    public SlotAttach slot;
    public GameObject prefab;
    public Vector3 posicao;
    public Vector3 rotacao;
    public float escala = 1f;

    public PecaMontada Copia()
    {
        var c = new PecaMontada();
        c.slot = slot; c.prefab = prefab;
        c.posicao = posicao; c.rotacao = rotacao; c.escala = escala;
        return c;
    }
}

/// <summary>
/// PRESET DE MONTAGEM - a "build" da arma.
///
/// Guarda quais pecas estao em quais slots e o encaixe exato de cada uma.
/// E isto que os upgrades do roguelite vao entregar: em vez de mexer em
/// numero solto, o upgrade troca o preset e a arma muda visualmente tambem.
///
/// Monta na janela: Ferramentas > Bancada de Armas.
/// </summary>
[CreateAssetMenu(menuName = "Zombie/Preset de Arma", fileName = "NovoPreset")]
public class PresetArma : ScriptableObject
{
    [Tooltip("Nome que aparece pro jogador (ex: AK Silenciada).")]
    public string nomeExibicao = "Montagem";

    [Tooltip("Para qual arma este preset foi feito.")]
    public WeaponData arma;

    [Tooltip("As pecas montadas.")]
    public PecaMontada[] pecas = new PecaMontada[0];

    [Header("Efeito no jogo (opcional, pros upgrades)")]
    [Tooltip("Multiplica o espalhamento. 0.8 = 20% mais preciso.")]
    public float multEspalhamento = 1f;
    [Tooltip("Multiplica a cadencia.")]
    public float multCadencia = 1f;
    [Tooltip("Multiplica o dano.")]
    public float multDano = 1f;
}
