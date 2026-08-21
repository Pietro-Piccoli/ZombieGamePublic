using UnityEngine;

/// <summary>
/// Marca um collider de osso do zumbi. A bala usa isso pra saber
/// ONDE acertou (multiplicador de dano) e QUAL osso empurrar no ragdoll.
/// Criado automaticamente pelo ZombieRagdoll - nao precisa configurar na mao.
/// </summary>
public class Hitbox : MonoBehaviour
{
    public ZombieRagdoll Owner;
    public Rigidbody Body;
    public float DamageMultiplier = 1f;
}
