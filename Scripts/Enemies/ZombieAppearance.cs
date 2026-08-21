using UnityEngine;

/// <summary>
/// Sorteia a aparencia do zumbi no nascimento.
///
/// O prefab de gameplay nao tem malha nenhuma: ele tem NavMeshAgent, Health e IA.
/// A malha entra em runtime, sorteada dessa lista. Assim uma horda de 80 zumbis
/// tem variedade visual sem precisar de 10 prefabs diferentes no spawner.
///
/// Cada variante ja vem com o Animator e o Avatar dela; aqui so plugamos o
/// controller e reapontamos a referencia que a IA usa.
/// </summary>
public class ZombieAppearance : MonoBehaviour
{
    [Header("Variantes")]
    [Tooltip("Prefabs em Assets/Prefabs/Zombies. Cada um com seu proprio Avatar.")]
    [SerializeField] private GameObject[] variants;

    [Tooltip("Controller aplicado a variante sorteada.")]
    [SerializeField] private RuntimeAnimatorController controller;

    [Header("Variacao visual")]
    [Tooltip("Escala minima e maxima. Quebra a sensacao de clone.")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.94f, 1.06f);
    [Tooltip("Desloca o inicio da animacao pra horda nao andar em sincronia.")]
    [SerializeField] private bool randomizeAnimationOffset = true;

    public Animator SpawnedAnimator { get; private set; }
    public GameObject SpawnedVisual { get; private set; }

    private void Awake()
    {
        if (variants == null || variants.Length == 0)
        {
            Debug.LogError("[ZombieAppearance] Sem variantes na lista.", this);
            return;
        }

        GameObject prefab = variants[Random.Range(0, variants.Length)];
        if (prefab == null)
        {
            Debug.LogError("[ZombieAppearance] Variante nula na lista.", this);
            return;
        }

        SpawnedVisual = Instantiate(prefab, transform.position, transform.rotation, transform);
        SpawnedVisual.name = "Visual";
        SpawnedVisual.transform.localPosition = Vector3.zero;
        SpawnedVisual.transform.localRotation = Quaternion.identity;

        float s = Random.Range(scaleRange.x, scaleRange.y);
        SpawnedVisual.transform.localScale = Vector3.one * s;

        int layer = gameObject.layer;
        foreach (Transform t in SpawnedVisual.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;

        SpawnedAnimator = SpawnedVisual.GetComponent<Animator>();
        if (SpawnedAnimator == null) SpawnedAnimator = SpawnedVisual.GetComponentInChildren<Animator>();

        if (SpawnedAnimator != null)
        {
            SpawnedAnimator.runtimeAnimatorController = controller;
            SpawnedAnimator.applyRootMotion = false;
            SpawnedAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            // Sem isso, uma horda inteira anda com o pe no mesmo tempo.
            // E o detalhe que mais denuncia "isso e um clone" sem ninguem saber por que.
            if (randomizeAnimationOffset)
                SpawnedAnimator.Play(0, 0, Random.value);
        }
    }
}
