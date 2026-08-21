using UnityEngine;

/// <summary>
/// Faz um VFX que nasceu em LOOP se comportar como estouro: emite forte por um
/// instante e para, deixando as particulas em voo terminarem.
/// Serve pra reaproveitar prefabs de fogo como explosao.
/// </summary>
public class VfxCurto : MonoBehaviour
{
    [Tooltip("Segundos emitindo antes de cortar.")]
    public float tempoEmitindo = 0.35f;
    [Tooltip("Multiplica a taxa de emissao no estouro.")]
    public float rajada = 6f;

    private float t;
    private bool cortado;

    private void Start()
    {
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            // sem Hierarchy a escala do objeto nao chega nas particulas
            var m = ps.main;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var e = ps.emission;
            e.rateOverTimeMultiplier *= rajada;
            ps.Play(true);
        }
    }

    private void Update()
    {
        if (cortado) return;
        t += Time.deltaTime;
        if (t < tempoEmitindo) return;
        cortado = true;
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
