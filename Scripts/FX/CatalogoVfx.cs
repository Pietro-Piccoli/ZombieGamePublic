using UnityEngine;

/// <summary>
/// Referencias de VFX usadas pelas cartas. Vive em Resources pra funcionar no
/// jogo compilado (AssetDatabase nao existe em build). As referencias diretas
/// serializam normalmente, entao os prefabs do pack nao precisam ser copiados.
/// </summary>
[CreateAssetMenu(menuName = "Zombie/Catalogo de VFX", fileName = "CatalogoVfx")]
public class CatalogoVfx : ScriptableObject
{
    [Header("Cartas")]
    [Tooltip("CAMISA EM CHAMAS: fogo preso no corpo do jogador.")]
    public GameObject fogoNoCorpo;
    [Tooltip("QUEIMA ARQUIVO: estouro no lugar do zumbi morto.")]
    public GameObject explosaoAoMatar;
    [Tooltip("ARCO VOLTAICO: impacto eletrico no inimigo encadeado.")]
    public GameObject impactoEletrico;
    [Tooltip("PLANO DE SAUDE / KIT DE CAMPO: cura instantanea.")]
    public GameObject curaInstantanea;
    [Tooltip("REFLEXO FANTASMA: escudo enquanto invulneravel.")]
    public GameObject escudoFantasma;
    [Tooltip("ADRENALINA: aura enquanto o bonus dura.")]
    public GameObject auraBuff;
    [Tooltip("SEGUNDA CHANCE: circulo de cura ao reviver.")]
    public GameObject reviver;

    private static CatalogoVfx cache;
    public static CatalogoVfx Instancia
    {
        get
        {
            if (cache == null) cache = Resources.Load<CatalogoVfx>("CatalogoVfx");
            return cache;
        }
    }
}
