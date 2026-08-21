using System;
using UnityEngine;

/// <summary>
/// Vida generica. Serve pro player E pro zumbi.
/// Quem quiser reagir (barra, flash, morte) escuta os eventos.
/// </summary>
public class Health : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float invulnerabilityTime = 0f;

    public int MaxHealth => maxHealth;
    public int Current { get; private set; }
    public bool IsDead => Current <= 0;
    public float Percent => maxHealth <= 0 ? 0f : (float)Current / maxHealth;

    /// <summary>(atual, maximo)</summary>
    public event Action<int, int> OnChanged;
    /// <summary>Direcao de onde veio o golpe.</summary>
    public event Action<Vector3> OnDamaged;
    public event Action OnDeath;

    /// <summary>
    /// Filtro aplicado ao dano ANTES de descontar. Recebe o dano cru e devolve
    /// o dano final (0 anula). E por aqui que as cartas de defesa agem:
    /// PLACA DE CERAMICA reduz, REFLEXO FANTASMA zera na janela, SEGUNDA
    /// CHANCE intercepta o golpe letal e revive.
    /// </summary>
    public Func<int, int> FiltroDano;

    /// <summary>
    /// Direcao do ultimo golpe recebido, ja normalizada. Quem mata por tiro ou
    /// granada tem o ponto exato do impacto e liga o ragdoll por conta propria;
    /// fogo, corrente eletrica, espinhos e cheat nao tem. Este campo e a rede
    /// de seguranca pra esses casos - sem ele o zumbi morria em pe, duro, e
    /// afundava no chao ainda de pe.
    /// </summary>
    public Vector3 UltimaDirecaoDeDano { get; private set; }

    /// <summary>
    /// Disparado quando QUALQUER Health morre. E o gancho central das cartas
    /// de abate (QUEIMA ARQUIVO, ADRENALINA, REPOSICAO TATICA): um so evento
    /// cobre tiro, explosao, fogo, acido e granada, sem tocar em cada caminho.
    /// </summary>
    public static event Action<Health> QualquerMorte;

    private float invulnerableUntil;
    private bool deathFired;

    private void Awake() => Current = maxHealth;

    private void Start() => OnChanged?.Invoke(Current, maxHealth);

    public void SetMaxHealth(int value, bool healToFull = true)
    {
        maxHealth = Mathf.Max(1, value);
        Current = healToFull ? maxHealth : Mathf.Min(Current, maxHealth);
        deathFired = false;
        OnChanged?.Invoke(Current, maxHealth);
    }

    public void TakeDamage(int amount, Vector3 hitDirection = default)
    {
        if (IsDead || amount <= 0) return;
        if (Time.time < invulnerableUntil) return;

        if (FiltroDano != null)
        {
            amount = FiltroDano(amount);
            if (amount <= 0) return;
        }

        if (invulnerabilityTime > 0f)
            invulnerableUntil = Time.time + invulnerabilityTime;

        Current = Mathf.Max(0, Current - amount);
        if (hitDirection.sqrMagnitude > 0.0001f) UltimaDirecaoDeDano = hitDirection.normalized;

        OnChanged?.Invoke(Current, maxHealth);
        OnDamaged?.Invoke(hitDirection);

        if (Current == 0 && !deathFired)
        {
            deathFired = true;
            QualquerMorte?.Invoke(this);
            OnDeath?.Invoke();
        }
    }

    /// <summary>Usado pela SEGUNDA CHANCE: volta dos mortos com esta vida.</summary>
    public void Reviver(int vida)
    {
        Current = Mathf.Clamp(vida, 1, maxHealth);
        deathFired = false;
        OnChanged?.Invoke(Current, maxHealth);
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        Current = Mathf.Min(maxHealth, Current + amount);
        OnChanged?.Invoke(Current, maxHealth);
    }
}
