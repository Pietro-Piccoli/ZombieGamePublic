using UnityEngine;

/// <summary>
/// A granada no ar. Voa com fisica, conta o pavio e estoura: dano em area com
/// queda pela distancia, empurrao no ragdoll e, se for incendiaria, deixa uma
/// zona de fogo no chao.
///
/// Montada em codigo pelo LancadorGranadas - nao precisa de prefab pronto.
/// </summary>
/// <summary>Multiplicadores vindos das cartas (GRANADEIRO, PAVIO CURTO...).</summary>
public struct GranadaMods
{
    public float pavio, raio, dano, fogo;
    public static GranadaMods Neutro { get { var m = new GranadaMods(); m.pavio = 1f; m.raio = 1f; m.dano = 1f; m.fogo = 1f; return m; } }
}

public class Granada : MonoBehaviour
{
    private GranadaData ficha;
    private float estouraEm;
    private bool jaEstourou;
    private LayerMask mascaraCenario;
    private GranadaMods mods = GranadaMods.Neutro;

    public static Granada Lancar(GranadaData f, Vector3 origem, Vector3 direcao, Vector3 velocidadeDono)
    {
        return Lancar(f, origem, direcao, velocidadeDono, GranadaMods.Neutro);
    }

    public static Granada Lancar(GranadaData f, Vector3 origem, Vector3 direcao, Vector3 velocidadeDono, GranadaMods mods)
    {
        var go = new GameObject("Granada_" + f.name);
        go.transform.position = origem;

        if (f.modelo != null)
        {
            var m = Instantiate(f.modelo, go.transform);
            m.transform.localPosition = Vector3.zero;
            m.transform.localScale = Vector3.one * Mathf.Max(0.01f, f.escalaModelo);
            foreach (var c in m.GetComponentsInChildren<Collider>(true)) Destroy(c);
        }

        var col = go.AddComponent<SphereCollider>();
        col.radius = 0.09f;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.4f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.angularVelocity = Random.insideUnitSphere * 12f;

        // Sobe a mira pra granada fazer arco em vez de linha reta. O sinal aqui
        // estava invertido: com anguloArco=12 ela saia 12 graus PRA BAIXO. Passava
        // despercebido porque ela nascia na altura da cabeca; nascendo da mao
        // (1,05 m) ela batia no chao a 5 m. Medido: elevacao -12 -> +12 graus.
        Vector3 dir = Quaternion.AngleAxis(f.anguloArco, Vector3.Cross(direcao, Vector3.up).normalized) * direcao;
        rb.linearVelocity = velocidadeDono + dir.normalized * f.forcaArremesso;

        // detrito: nao empurra o player nem os zumbis pelo caminho
        int camada = LayerMask.NameToLayer("Detritos");
        if (camada >= 0) go.layer = camada;

        var g = go.AddComponent<Granada>();
        g.ficha = f;
        g.mods = mods;
        g.estouraEm = Time.time + f.pavio * Mathf.Max(0.2f, mods.pavio);
        return g;
    }

    private void Update()
    {
        if (!jaEstourou && Time.time >= estouraEm) Estourar();
    }

    private void OnCollisionEnter(Collision c)
    {
        if (jaEstourou || ficha == null || !ficha.estouraNoContato) return;
        if (c.collider.GetComponentInParent<Health>() != null) Estourar();
    }

    private void Estourar()
    {
        jaEstourou = true;
        Vector3 ponto = transform.position;
        float raioFinal = ficha.raio * mods.raio;

        // ---- visual ----
        if (ficha.vfxExplosao != null)
        {
            var vfx = Instantiate(ficha.vfxExplosao, ponto, Quaternion.identity);
            vfx.transform.localScale = Vector3.one * Mathf.Max(0.01f, ficha.escalaVfx);
            Destroy(vfx, 4f);
        }
        FlashDeLuz(ponto, ficha.cor, ficha.raio);

        // Explosao sacode de verdade. O tremor cai com a distancia: estar a 20 m
        // de uma granada nao pode sacudir igual a estar em cima dela.
        var camImp = Camera.main;
        if (camImp != null)
        {
            float dist = Vector3.Distance(camImp.transform.position, ponto);
            float perto = Mathf.Clamp01(1f - dist / (raioFinal * 3.5f));
            ImpactoDeCamera.Tremer(0.75f * perto * perto);
            if (perto > 0.35f) ImpactoDeCamera.Congelar(0.06f);
        }

        // ---- dano em area, com queda pela distancia ----
        var atingidos = new System.Collections.Generic.HashSet<Health>();
        foreach (var c in Physics.OverlapSphere(ponto, raioFinal, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.GetComponent<Hitbox>() == null) continue;
            Health h = c.GetComponentInParent<Health>();
            if (h == null || h.IsDead || atingidos.Contains(h)) continue;
            atingidos.Add(h);

            float d = Vector3.Distance(ponto, c.transform.position);
            float k = Mathf.Lerp(1f, ficha.danoNaBorda, Mathf.Clamp01(d / raioFinal));
            int dano = Mathf.Max(1, Mathf.RoundToInt(ficha.dano * mods.dano * k));

            Vector3 dir = (c.transform.position - ponto).normalized;
            h.TakeDamage(dano, dir);
            DanoPopup.Mostrar(c.ClosestPoint(ponto), dano, DanoPopup.Tipo.Explosao, h.transform);
            EstatisticasRun.RegistrarDano(dano, DanoPopup.Tipo.Explosao);
            if (h.IsDead)
            {
                EstatisticasRun.RegistrarAbate(false);
                var rag = c.GetComponentInParent<ZombieRagdoll>();
                if (rag != null) rag.EnterRagdoll(c, c.ClosestPoint(ponto), dir, ficha.impulso);
            }
        }

        // ---- fogo residual ----
        if (ficha.deixaFogo) ZonaDeFogo.Criar(ficha, AcharChao(ponto), mods.fogo);

        Destroy(gameObject);
    }

    private Vector3 AcharChao(Vector3 p)
    {
        RaycastHit hit;
        int mascara = ~(1 << LayerMask.NameToLayer("Hitbox"));
        if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out hit, 6f, mascara, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * 0.03f;
        return p;
    }

    public static void FlashDeLuz(Vector3 ponto, Color cor, float raio)
    {
        var go = new GameObject("FlashExplosao");
        go.transform.position = ponto + Vector3.up * 0.4f;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = cor;
        l.range = raio * 2.4f;
        l.intensity = 14f;
        l.shadows = LightShadows.None;
        go.AddComponent<FlashSome>().Iniciar(l, 0.35f);
    }
}

/// <summary>Apaga a luz do estouro. Usa tempo NAO escalado pra nao congelar em pausa.</summary>
public class FlashSome : MonoBehaviour
{
    private Light luz;
    private float dur, t, inicial;

    public void Iniciar(Light l, float duracao) { luz = l; dur = duracao; inicial = l.intensity; }

    private void Update()
    {
        t += Time.unscaledDeltaTime;
        if (luz == null || t >= dur) { Destroy(gameObject); return; }
        luz.intensity = Mathf.Lerp(inicial, 0f, t / dur);
    }
}
