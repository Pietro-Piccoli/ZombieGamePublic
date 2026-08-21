using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Decide ONDE o zumbi nasce. Quem decide quantos/quando e o WaveManager.
///
/// MODELO: pontos de spawn FIXOS (filhos de SPAWN_POINTS, becos sem saida).
/// Um ponto so pode ser usado se o player estiver LONGE dele (minPlayerDistance)
/// - nada de zumbi nascendo nas costas - e se existir caminho completo de
/// NavMesh ate o player. Entre os validos, sorteia com preferencia pelos
/// mais proximos (chegam logo, mas nunca na cara).
/// </summary>
public class SpawnDirector : MonoBehaviour
{
    [Header("Pontos fixos")]
    [Tooltip("Pai dos marcadores de spawn. Pode mover/adicionar/apagar filhos a vontade.")]
    [SerializeField] private Transform pointsRoot;

    [Header("Regras de justica")]
    [Tooltip("Ponto so ativa se o player estiver a MAIS que isso dele. Nada de spawn nas costas.")]
    [SerializeField] private float minPlayerDistance = 16f;
    [Tooltip("Preferencia: pontos ate esta distancia tem mais chance no sorteio.")]
    [SerializeField] private float preferredMaxDistance = 55f;
    [Tooltip("Espalhamento ao redor do ponto (horda nao nasce empilhada).")]
    [SerializeField] private float jitter = 2.5f;

    [Header("NavMesh")]
    [SerializeField] private float navSampleRadius = 3f;

    private Transform player;
    private static readonly System.Collections.Generic.List<Transform> validos = new System.Collections.Generic.List<Transform>();

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        if (pointsRoot == null)
        {
            GameObject sp = GameObject.Find("SPAWN_POINTS");
            if (sp != null) pointsRoot = sp.transform;
        }
        if (pointsRoot == null || pointsRoot.childCount == 0)
            Debug.LogError("[SpawnDirector] Sem pontos de spawn! Crie filhos em SPAWN_POINTS.", this);
    }

    /// <summary>Tenta achar um ponto de spawn valido. false = tenta de novo depois.</summary>
    public bool TryGetSpawnPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (player == null || pointsRoot == null) return false;

        NavMeshHit playerHit;
        if (!NavMesh.SamplePosition(player.position, out playerHit, 3f, NavMesh.AllAreas))
            return false;

        // junta os pontos validos deste instante
        validos.Clear();
        float melhorDist = float.MaxValue;
        for (int i = 0; i < pointsRoot.childCount; i++)
        {
            Transform t = pointsRoot.GetChild(i);
            if (!t.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(t.position, player.position);
            if (d < minPlayerDistance) continue;          // player perto demais: ponto travado
            validos.Add(t);
            if (d < melhorDist) melhorDist = d;
        }
        if (validos.Count == 0) return false;

        // sorteio com preferencia pelos proximos (peso duplo)
        for (int tentativa = 0; tentativa < 8; tentativa++)
        {
            Transform escolhido = validos[Random.Range(0, validos.Count)];
            float d = Vector3.Distance(escolhido.position, player.position);
            if (d > preferredMaxDistance && Random.value < 0.6f && validos.Count > 1)
                continue;   // longe demais: da outra chance ao sorteio

            Vector2 j = Random.insideUnitCircle * jitter;
            Vector3 candidato = escolhido.position + new Vector3(j.x, 0f, j.y);

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(candidato, out hit, navSampleRadius, NavMesh.AllAreas))
                continue;

            // so vale com caminho COMPLETO ate o player (nada de ilha)
            NavMeshPath caminho = new NavMeshPath();
            if (!NavMesh.CalculatePath(hit.position, playerHit.position, NavMesh.AllAreas, caminho) ||
                caminho.status != NavMeshPathStatus.PathComplete)
                continue;

            point = hit.position;
            return true;
        }
        return false;
    }
}
