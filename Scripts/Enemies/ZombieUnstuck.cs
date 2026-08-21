using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Vigia anti-preso: se o zumbi ficar sem caminho completo ate o player
/// (ou parado no lugar) por tempo demais, ele e TELETRANSPORTADO pra um
/// ponto de spawn valido. Resolve os que ja nasceram em ilha de NavMesh
/// e garante que a wave nunca trava por zumbi entalado.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ZombieUnstuck : MonoBehaviour
{
    [Header("Deteccao")]
    [Tooltip("De quanto em quanto tempo checa (segundos).")]
    [SerializeField] private float intervalo = 3f;
    [Tooltip("Quantas checagens seguidas ruins antes de resgatar.")]
    [SerializeField] private int falhasParaResgate = 2;
    [Tooltip("Andou menos que isso entre checagens = considerado parado.")]
    [SerializeField] private float movimentoMinimo = 0.6f;
    [Tooltip("Perto do player que isso nao conta como preso (esta atacando).")]
    [SerializeField] private float distanciaAtaque = 3.5f;

    private NavMeshAgent agent;
    private Health health;
    private Transform player;
    private SpawnDirector director;
    private float proximaChecagem;
    private int falhas;
    private Vector3 posAnterior;

    // Um NavMeshPath por zumbi, criado uma vez. Antes nascia um a cada checagem:
    // com 100 zumbis checando a cada 3 s, sao ~33 objetos jogados fora por segundo
    // so pra serem coletados depois.
    private NavMeshPath caminho;

    // Um NavMeshPath por zumbi, criado uma vez. Antes nascia um a cada checagem:
    // com 100 zumbis checando a cada 3 s, sao ~33 objetos jogados fora por
    // segundo so pra serem coletados depois.

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        if (health == null) health = GetComponentInParent<Health>();
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) p = GameObject.Find("Player");
        if (p != null) player = p.transform;
        director = FindAnyObjectByType<SpawnDirector>();
        proximaChecagem = Time.time + intervalo + Random.value * intervalo; // espalha as checagens
        posAnterior = transform.position;
        caminho = new NavMeshPath();
    }

    private void Update()
    {
        if (Time.time < proximaChecagem) return;
        proximaChecagem = Time.time + intervalo;

        if (player == null || agent == null || !agent.enabled) return;
        if (health != null && health.IsDead) return;
        if (Vector3.Distance(transform.position, player.position) <= distanciaAtaque) { falhas = 0; return; }

        bool ruim = false;

        // caminho quebrado?
        NavMeshHit ph;
        if (NavMesh.SamplePosition(player.position, out ph, 3f, NavMesh.AllAreas))
        {
            bool ok = NavMesh.CalculatePath(transform.position, ph.position, NavMesh.AllAreas, caminho)
                      && caminho.status == NavMeshPathStatus.PathComplete;
            if (!ok) ruim = true;
        }

        // parado no lugar mesmo com caminho? (entalado em quina/props)
        if (!ruim && Vector3.Distance(transform.position, posAnterior) < movimentoMinimo)
            ruim = true;

        posAnterior = transform.position;

        if (!ruim) { falhas = 0; return; }

        falhas++;
        if (falhas < falhasParaResgate) return;
        falhas = 0;

        // RESGATE: renasce num ponto de spawn valido (fora da vista)
        Vector3 destino;
        if (director != null && director.TryGetSpawnPoint(out destino))
            agent.Warp(destino);
    }
}
