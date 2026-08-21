using UnityEngine;

/// <summary>
/// Poca de fogo que fica no chao depois da granada incendiaria. Quem entrar
/// pega BurnStatus - o mesmo sistema que a municao incendiaria ja usa, entao
/// o dano, o piscar da luz e o numero flutuante saem de graca e consistentes.
/// </summary>
public class ZonaDeFogo : MonoBehaviour
{
    private GranadaData ficha;
    private float acabaEm;
    private float proximaAplicacao;
    private float multFogo = 1f;

    public static ZonaDeFogo Criar(GranadaData f, Vector3 ponto) { return Criar(f, ponto, 1f); }

    public static ZonaDeFogo Criar(GranadaData f, Vector3 ponto, float multFogo)
    {
        var go = new GameObject("ZonaDeFogo");
        go.transform.position = ponto;

        if (f.vfxFogoNoChao != null)
        {
            var vfx = Instantiate(f.vfxFogoNoChao, go.transform);
            vfx.transform.localPosition = Vector3.zero;
            // Os sistemas do pack vem com scalingMode = Local, que IGNORA a escala
            // do pai - por isso redimensionar o objeto raiz nao fazia nada e o
            // fogo saia do tamanho de um isqueiro. Hierarchy faz a escala valer.
            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m = ps.main;
                m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            vfx.transform.localScale = Vector3.one * Mathf.Max(0.1f, f.raioFogo * f.escalaVfxFogo);
        }

        var z = go.AddComponent<ZonaDeFogo>();
        z.ficha = f;
        z.multFogo = Mathf.Max(0.1f, multFogo);
        z.acabaEm = Time.time + f.duracaoFogo * z.multFogo;
        return z;
    }

    private void Update()
    {
        if (Time.time >= acabaEm)
        {
            // deixa o VFX terminar as particulas em voo antes de sumir
            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, 2.5f);
            enabled = false;
            return;
        }

        // reaplica de meio em meio segundo: o BurnStatus renova a duracao sozinho
        if (Time.time < proximaAplicacao) return;
        proximaAplicacao = Time.time + 0.5f;

        foreach (var c in Physics.OverlapSphere(transform.position, ficha.raioFogo, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.GetComponent<Hitbox>() == null) continue;
            Health h = c.GetComponentInParent<Health>();
            if (h == null || h.IsDead) continue;
            BurnStatus.Aplicar(h, ficha.dpsFogo * multFogo, 1.2f, false);
        }
    }
}
