using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// IA do zumbi: a mais burra possivel, de proposito.
/// Anda ate o player pelo NavMesh. Chegou perto, ataca. Levou tiro, reage. Morreu, cai.
///
/// O NavMeshAgent move; o Animator so REAGE a velocidade dele.
/// Root motion fica desligado - senao a animacao briga com o agente e o zumbi patina.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class ZombieAI : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float moveSpeed = 1.6f;
    [SerializeField] private float speedVariation = 0.25f;
    [SerializeField] private float angularSpeed = 240f;

    [Header("Ataque")]
    [SerializeField] private float attackRange = 1.9f;
    [SerializeField] private float attackCooldown = 1.6f;
    [Tooltip("Atraso ate o dano sair, pra bater com o frame do soco.")]
    [SerializeField] private float damageDelay = 0.35f;
    [SerializeField] private int damage = 12;

    [Header("Pensamento")]
    [Tooltip("De quanto em quanto tempo recalcula a rota. Nao precisa ser todo frame - e o gargalo da horda.")]
    [SerializeField] private float repathInterval = 0.2f;

    [Header("Morte")]
    [SerializeField] private float corpseLifetime = 6f;
    [SerializeField] private float sinkDelay = 4f;
    [SerializeField] private float sinkSpeed = 0.4f;
    [Tooltip("Empurrao dado ao corpo quando a morte nao veio de um tiro (fogo, corrente, espinhos). So pra ele cair, nao pra voar.")]
    [SerializeField] private float impulsoMorteSemTiro = 5f;

    private NavMeshAgent agent;
    private Health health;
    private Animator animator;
    private Transform target;
    private Health targetHealth;

    private float nextRepath;
    private float nextAttack;
    private float pendingDamageAt = -1f;
    private bool isDead;
    private float sinkStart = -1f;
    private bool sinkFrozen;
    private ZombieRagdoll ragdoll;

    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashHit = Animator.StringToHash("Hit");
    private static readonly int HashDead = Animator.StringToHash("Dead");

    public bool IsDead { get { return isDead; } }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();

        agent.speed = moveSpeed * (1f + Random.Range(-speedVariation, speedVariation));
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = attackRange * 0.8f;
        agent.autoBraking = false;

        // Espalha o "pensamento" no tempo pra 80 zumbis nao recalcularem no mesmo frame.
        nextRepath = Time.time + Random.Range(0f, repathInterval);
    }

    private void OnEnable()
    {
        health.OnDamaged += HandleDamaged;
        health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        health.OnDamaged -= HandleDamaged;
        health.OnDeath -= HandleDeath;
    }

    private void Start()
    {
        // Animator so aqui: o ZombieAppearance cria a malha no Awake dele, e a
        // ordem de Awake entre componentes do mesmo objeto NAO e garantida.
        // No Start, todos os Awakes ja rodaram - a malha certamente existe.
        ZombieAppearance appearance = GetComponent<ZombieAppearance>();
        if (appearance != null && appearance.SpawnedAnimator != null)
            animator = appearance.SpawnedAnimator;
        else
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
            Debug.LogError("[ZombieAI] Sem Animator - zumbi vai deslizar sem animacao.", this);
        else
            animator.applyRootMotion = false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            target = player.transform;
            targetHealth = player.GetComponent<Health>();
        }
    }

    /// <summary>Chamado pelo spawner: cada wave manda zumbi mais rapido e mais duro.</summary>
    /// <summary>Chamado pelo WaveManager: cada wave manda zumbi mais rapido, mais duro e mais forte.</summary>
    public void Configure(float speed, int maxHp, int contactDmg)
    {
        moveSpeed = speed;
        damage = contactDmg;
        if (agent != null) agent.speed = speed * (1f + Random.Range(-speedVariation, speedVariation));
        health.SetMaxHealth(maxHp);
    }

    private void Update()
    {
        if (isDead) { UpdateSink(); return; }
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);

        if (Time.time >= nextRepath)
        {
            nextRepath = Time.time + repathInterval;
            if (agent.isOnNavMesh) agent.SetDestination(target.position);
        }

        if (animator != null)
            animator.SetFloat(HashSpeed, agent.velocity.magnitude, 0.1f, Time.deltaTime);

        if (dist <= attackRange)
        {
            FaceTarget();
            if (Time.time >= nextAttack) StartAttack();
        }

        if (pendingDamageAt > 0f && Time.time >= pendingDamageAt)
        {
            pendingDamageAt = -1f;
            ApplyAttackDamage();
        }
    }

    private void StartAttack()
    {
        nextAttack = Time.time + attackCooldown;
        pendingDamageAt = Time.time + damageDelay;
        if (animator != null) animator.SetTrigger(HashAttack);
    }

    private void ApplyAttackDamage()
    {
        if (isDead || target == null || targetHealth == null || targetHealth.IsDead) return;

        // So machuca se o player ainda estiver perto quando o soco chega.
        if (Vector3.Distance(transform.position, target.position) > attackRange * 1.3f) return;

        Vector3 dir = (target.position - transform.position).normalized;
        targetHealth.TakeDamage(damage, dir);
    }

    private void FaceTarget()
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(dir),
            angularSpeed * Time.deltaTime);
    }

    private void HandleDamaged(Vector3 dir)
    {
        if (isDead) return;
        if (animator != null) animator.SetTrigger(HashHit);
    }

    private void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        if (ragdoll == null) ragdoll = GetComponent<ZombieRagdoll>();

        // Sem ragdoll construido, cai na animacao de morte.
        if ((ragdoll == null || !ragdoll.IsBuilt) && animator != null)
            animator.SetBool(HashDead, true);
        else
            StartCoroutine(GarantirRagdoll());

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        sinkStart = Time.time + sinkDelay;
        Destroy(gameObject, corpseLifetime);

        if (WaveManager.Instance != null) WaveManager.Instance.NotifyZombieKilled();
    }

    /// <summary>
    /// REDE DE SEGURANCA DO RAGDOLL.
    ///
    /// Quem mata com tiro ou granada sabe o osso e o ponto exato do impacto e
    /// liga o ragdoll sozinho, logo depois deste evento, no mesmo quadro - por
    /// isso esperamos um quadro antes de agir, pra nao roubar o empurrao bom.
    ///
    /// So que fogo, corrente eletrica, espinhos, aura e o cheat de matar todos
    /// nao passam por esse caminho: eles chamam TakeDamage e pronto. Antes
    /// disso aqui, o zumbi morto por essas fontes ficava DURO EM PE, com o
    /// Animator congelado no ultimo quadro de andar, e depois descia devagar
    /// pra dentro do chao ainda de pe.
    ///
    /// Aqui o corpo cai de qualquer jeito, usando a direcao do ultimo golpe
    /// que o Health guardou.
    /// </summary>
    private System.Collections.IEnumerator GarantirRagdoll()
    {
        yield return null;
        if (ragdoll == null || !ragdoll.IsBuilt || ragdoll.IsRagdolled) yield break;

        // O BurnStatus manda Vector3.up como direcao (fogo nao vem de lugar
        // nenhum). Se isso passasse direto, o corpo subiria feito foguete.
        // Entao aqui so a parte horizontal conta, e um pouquinho de cima e
        // somado de volta pra ele tombar em vez de escorregar em pe.
        Vector3 dir = health != null ? health.UltimaDirecaoDeDano : Vector3.zero;
        Vector3 plano = new Vector3(dir.x, 0f, dir.z);
        if (plano.sqrMagnitude < 0.01f) plano = -transform.forward;
        dir = (plano.normalized + Vector3.up * 0.15f).normalized;

        ragdoll.EnterRagdoll(null, transform.position + Vector3.up * 1.1f, dir, impulsoMorteSemTiro);
    }

    /// <summary>Afunda o corpo no chao em vez de sumir do nada.</summary>
    private void UpdateSink()
    {
        if (sinkStart < 0f || Time.time < sinkStart) return;

        // Congela o ragdoll uma vez antes de afundar, senao a fisica
        // briga com o movimento e o corpo treme no chao.
        if (!sinkFrozen)
        {
            sinkFrozen = true;
            if (ragdoll == null) ragdoll = GetComponent<ZombieRagdoll>();
            if (ragdoll != null) ragdoll.FreezeForSink();
        }

        transform.position += Vector3.down * sinkSpeed * Time.deltaTime;
    }
}
