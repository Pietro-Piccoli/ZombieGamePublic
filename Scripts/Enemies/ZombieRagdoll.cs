using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ragdoll construido EM CODIGO a partir dos ossos do Avatar humanoide.
/// Como os 10 zumbis compartilham o mesmo esqueleto, um script serve pra todos.
///
/// Em vida: cada osso ganha um collider cinematico (hitbox) - a bala sabe
/// onde acertou. Na morte: Animator desliga, fisica assume, e um impulso e
/// aplicado no osso atingido, na direcao do tiro. A "animacao" de morte
/// emerge da fisica - tiro no ombro esquerdo joga o ombro esquerdo pra tras.
/// </summary>
public class ZombieRagdoll : MonoBehaviour
{
    [Header("Fisica")]
    [SerializeField] private float totalMass = 60f;
    [Tooltip("Freio do corpo. Sobe se o boneco voar longe demais.")]
    [SerializeField] private float linearDamping = 0.25f;
    [SerializeField] private float angularDamping = 0.8f;

    [Header("Hitboxes")]
    [Tooltip("Raio da hitbox da cabeca. Sobe pra headshot ficar mais facil.")]
    [SerializeField] private float headRadius = 0.14f;

    [Header("Dano por regiao")]
    [SerializeField] private float headMultiplier = 2f;
    [SerializeField] private float limbMultiplier = 0.7f;

    public bool IsRagdolled { get; private set; }
    public bool IsBuilt { get; private set; }

    private Animator animator;
    private readonly List<Rigidbody> bodies = new List<Rigidbody>();
    private readonly List<Collider> hitboxes = new List<Collider>();
    private Rigidbody hips;
    private static int hitboxLayer = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SetupLayerCollisions()
    {
        int h = LayerMask.NameToLayer("Hitbox");
        if (h < 0) return;

        // Hitbox so colide com o cenario (pra cair no chao).
        // Nunca com player, inimigos ou outras hitboxes.
        int player = LayerMask.NameToLayer("Player");
        int enemy = LayerMask.NameToLayer("Enemy");
        if (player >= 0) Physics.IgnoreLayerCollision(h, player, true);
        if (enemy >= 0) Physics.IgnoreLayerCollision(h, enemy, true);
        Physics.IgnoreLayerCollision(h, h, true);
    }

    private void Start()
    {
        hitboxLayer = LayerMask.NameToLayer("Hitbox");

        ZombieAppearance app = GetComponent<ZombieAppearance>();
        animator = app != null && app.SpawnedAnimator != null
            ? app.SpawnedAnimator
            : GetComponentInChildren<Animator>();

        if (animator == null || !animator.isHuman)
        {
            Debug.LogError("[ZombieRagdoll] Sem Animator humanoide - ragdoll desativado.", this);
            return;
        }

        Build();
    }

    // ---------- construcao ----------

    private void Build()
    {
        Transform hipsT = Bone(HumanBodyBones.Hips);
        Transform chestT = Bone(HumanBodyBones.Chest);
        if (chestT == null) chestT = Bone(HumanBodyBones.Spine);
        Transform headT = Bone(HumanBodyBones.Head);
        if (hipsT == null || chestT == null || headT == null)
        {
            Debug.LogError("[ZombieRagdoll] Ossos essenciais faltando.", this);
            return;
        }

        // tronco e cabeca - offsets em MUNDO (pra cima), convertidos pro espaco
        // do osso na hora. Eixo local de osso de biped aponta pra qualquer lado;
        // confiar nele foi o bug que deixava a hitbox da cabeca no pescoco.
        hips = AddBox(hipsT, new Vector3(0.30f, 0.24f, 0.24f), 0.02f, totalMass * 0.26f, 1f, null);
        Rigidbody chest = AddBox(chestT, new Vector3(0.30f, 0.32f, 0.24f), 0.10f, totalMass * 0.26f, 1f, hips);
        Rigidbody head = AddSphere(headT, headRadius, 0.08f, totalMass * 0.09f, headMultiplier, chest);

        // bracos
        BuildLimb(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.055f, totalMass * 0.035f, chest,
            out Rigidbody lUpArm);
        BuildLimb(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.045f, totalMass * 0.025f, lUpArm, out _);
        BuildLimb(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.055f, totalMass * 0.035f, chest,
            out Rigidbody rUpArm);
        BuildLimb(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.045f, totalMass * 0.025f, rUpArm, out _);

        // pernas
        BuildLimb(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.075f, totalMass * 0.10f, hips,
            out Rigidbody lUpLeg);
        BuildLimb(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.06f, totalMass * 0.055f, lUpLeg, out _);
        BuildLimb(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.075f, totalMass * 0.10f, hips,
            out Rigidbody rUpLeg);
        BuildLimb(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.06f, totalMass * 0.055f, rUpLeg, out _);

        IsBuilt = true;
    }

    private Transform Bone(HumanBodyBones b) => animator.GetBoneTransform(b);

    private void BuildLimb(HumanBodyBones bone, HumanBodyBones childBone, float radius, float mass,
        Rigidbody parent, out Rigidbody rb)
    {
        rb = null;
        Transform t = Bone(bone);
        Transform child = Bone(childBone);
        if (t == null || child == null) return;

        // capsula apontando do osso ate o filho, no espaco local do osso
        Vector3 local = t.InverseTransformPoint(child.position);
        float len = local.magnitude;

        var cap = t.gameObject.AddComponent<CapsuleCollider>();
        cap.radius = radius;
        cap.height = len + radius * 2f;
        cap.center = local * 0.5f;
        cap.direction = LargestAxis(local);

        rb = Register(t, cap, mass, limbMultiplier, parent);
    }

    private Rigidbody AddBox(Transform t, Vector3 size, float worldUpOffset, float mass, float mult, Rigidbody parent)
    {
        var box = t.gameObject.AddComponent<BoxCollider>();
        box.size = size;
        // offset "pra cima" definido em mundo e convertido pro espaco do osso
        box.center = t.InverseTransformPoint(t.position + Vector3.up * worldUpOffset);
        return Register(t, box, mass, mult, parent);
    }

    private Rigidbody AddSphere(Transform t, float radius, float worldUpOffset, float mass, float mult, Rigidbody parent)
    {
        var s = t.gameObject.AddComponent<SphereCollider>();
        s.radius = radius;
        s.center = t.InverseTransformPoint(t.position + Vector3.up * worldUpOffset);
        return Register(t, s, mass, mult, parent);
    }

    private Rigidbody Register(Transform t, Collider col, float mass, float mult, Rigidbody parent)
    {
        if (hitboxLayer >= 0) t.gameObject.layer = hitboxLayer;

        var rb = t.gameObject.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.isKinematic = true;              // em vida: so hitbox, fisica dorme
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        rb.maxDepenetrationVelocity = 3f;   // evita o corpo "explodir" preso no chao

        if (parent != null)
        {
            var joint = t.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.enablePreprocessing = false;
        }

        var hb = t.gameObject.AddComponent<Hitbox>();
        hb.Owner = this;
        hb.Body = rb;
        hb.DamageMultiplier = mult;

        bodies.Add(rb);
        hitboxes.Add(col);
        return rb;
    }

    private static int LargestAxis(Vector3 v)
    {
        float x = Mathf.Abs(v.x), y = Mathf.Abs(v.y), z = Mathf.Abs(v.z);
        if (x >= y && x >= z) return 0;
        return y >= z ? 1 : 2;
    }

    // ---------- morte ----------

    /// <summary>Solta a fisica e empurra o osso atingido na direcao do tiro.</summary>
    public void EnterRagdoll(Collider hitCollider, Vector3 hitPoint, Vector3 direction, float impulse)
    {
        if (IsRagdolled || !IsBuilt) return;
        IsRagdolled = true;

        if (animator != null) animator.enabled = false;

        foreach (var rb in bodies)
            if (rb != null) rb.isKinematic = false;

        Rigidbody alvo = hips;
        if (hitCollider != null)
        {
            var hb = hitCollider.GetComponent<Hitbox>();
            if (hb != null && hb.Body != null) alvo = hb.Body;
        }

        Vector3 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;

        // o osso atingido leva o soco principal; o quadril leva metade,
        // senao um tiro no braco derruba so o braco e o corpo fica em pe parado.
        if (alvo != null) alvo.AddForceAtPosition(dir * impulse, hitPoint, ForceMode.Impulse);
        if (hips != null && alvo != hips) hips.AddForce(dir * impulse * 0.5f, ForceMode.Impulse);
    }

    /// <summary>Congela o corpo pra afundar no chao sem a fisica brigar.</summary>
    public void FreezeForSink()
    {
        foreach (var rb in bodies)
            if (rb != null) rb.isKinematic = true;
        foreach (var c in hitboxes)
            if (c != null) c.enabled = false;
    }
}
