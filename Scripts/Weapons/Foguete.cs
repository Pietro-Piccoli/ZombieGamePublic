using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// O FOGUETE do lanca-rojao.
///
/// Diferente da granada, que voa com Rigidbody e conta pavio, o foguete anda
/// em linha e estoura no primeiro contato. E o comportamento do RPG em
/// Battlefield e no Left 4 Dead: sai reto, viaja rapido mas visivel, e o
/// tempo de voo e parte do custo de usar a arma - nao e hitscan.
///
/// O movimento e feito na mao com SphereCast em vez de fisica. Com 40 m/s e
/// dt de 16 ms o foguete anda 64 cm por quadro; um collider comum atravessaria
/// zumbi magro sem registrar. O SphereCast varre o trecho inteiro do quadro.
/// </summary>
public class Foguete : MonoBehaviour
{
    private WeaponData ficha;
    private Vector3 direcao;
    private float velocidade;
    private float multDano = 1f;
    private float morreEm;
    private bool jaEstourou;
    private Transform dono;
    private int mascara;

    // lista reaproveitada: o estouro nao pode alocar
    private static readonly List<Health> alvos = new List<Health>(64);

    public static Foguete Lancar(WeaponData f, Vector3 origem, Vector3 dir, float multDano, Transform dono)
    {
        var go = new GameObject("Foguete_" + f.name);
        go.transform.position = origem;
        go.transform.rotation = Quaternion.LookRotation(dir);

        if (f.projetilModelo != null)
        {
            var m = Instantiate(f.projetilModelo, go.transform);
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;
            m.transform.localScale = Vector3.one * Mathf.Max(0.01f, f.projetilEscala);
            foreach (var c in m.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var t in m.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = go.layer;
        }

        // rastro: da pra ver de onde veio e pra onde vai
        var tr = go.AddComponent<TrailRenderer>();
        tr.time = 0.35f;
        tr.startWidth = 0.16f; tr.endWidth = 0.02f;
        tr.numCapVertices = 2;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh != null) tr.material = new Material(sh);
        tr.startColor = new Color(1f, 0.75f, 0.35f, 0.85f);
        tr.endColor = new Color(0.5f, 0.5f, 0.5f, 0f);

        var luz = go.AddComponent<Light>();
        luz.type = LightType.Point; luz.range = 6f; luz.intensity = 3.2f;
        luz.color = new Color(1f, 0.72f, 0.34f); luz.shadows = LightShadows.None;

        var fg = go.AddComponent<Foguete>();
        fg.ficha = f;
        fg.direcao = dir.normalized;
        fg.velocidade = Mathf.Max(4f, f.projetilVelocidade);
        fg.multDano = multDano;
        fg.dono = dono;
        fg.morreEm = Time.time + 6f;
        // tudo menos a camada do proprio jogador: o foguete nao pode estourar na
        // arma de quem atirou
        int camadaPlayer = LayerMask.NameToLayer("Player");
        fg.mascara = camadaPlayer >= 0 ? ~(1 << camadaPlayer) : ~0;
        return fg;
    }

    private void Update()
    {
        if (jaEstourou) return;
        if (Time.time >= morreEm) { Estourar(transform.position, Vector3.up); return; }

        if (ficha.projetilGravidade > 0f)
            direcao = (direcao * velocidade + Vector3.down * ficha.projetilGravidade * Time.deltaTime).normalized;

        float passo = velocidade * Time.deltaTime;
        RaycastHit hit;
        if (Physics.SphereCast(transform.position, 0.09f, direcao, out hit, passo,
                               mascara, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point - direcao * 0.05f;
            Estourar(hit.point, hit.normal);
            return;
        }
        transform.position += direcao * passo;
        transform.rotation = Quaternion.LookRotation(direcao);
    }

    private void Estourar(Vector3 ponto, Vector3 normal)
    {
        jaEstourou = true;
        float raio = Mathf.Max(0.5f, ficha.explosaoRaio);

        Granada.FlashDeLuz(ponto, new Color(1f, 0.7f, 0.35f), raio);
        ImpactFX.SpawnImpact(ponto, normal);

        var cam = Camera.main;
        if (cam != null)
        {
            float dist = Vector3.Distance(cam.transform.position, ponto);
            float perto = Mathf.Clamp01(1f - dist / (raio * 3.5f));
            ImpactoDeCamera.Tremer(0.9f * perto * perto);
            if (perto > 0.3f) ImpactoDeCamera.Congelar(0.07f);
        }

        // ---- dano em area. Usa o registro de inimigos, nao OverlapSphere:
        // com 13 hitboxes por zumbi um Overlap devolve centenas de colliders.
        RegistroInimigos.DentroDoRaio(ponto, raio, alvos, null);
        for (int i = 0; i < alvos.Count; i++)
        {
            Health h = alvos[i];
            if (h == null || h.IsDead) continue;
            Vector3 centro = h.transform.position + Vector3.up * 1.0f;
            float d = Vector3.Distance(ponto, centro);
            float k = Mathf.Lerp(1f, ficha.explosaoDanoBorda, Mathf.Clamp01(d / raio));
            int dano = Mathf.Max(1, Mathf.RoundToInt(ficha.explosaoDano * multDano * k));
            Vector3 dir = (centro - ponto).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;

            h.TakeDamage(dano, dir);
            DanoPopup.Mostrar(centro, dano, DanoPopup.Tipo.Explosao, h.transform);
            EstatisticasRun.RegistrarDano(dano, DanoPopup.Tipo.Explosao);
            if (h.IsDead)
            {
                EstatisticasRun.RegistrarAbate(false);
                var rag = h.GetComponentInChildren<ZombieRagdoll>();
                if (rag == null) rag = h.GetComponentInParent<ZombieRagdoll>();
                if (rag != null) rag.EnterRagdoll(null, centro, dir, ficha.explosaoImpulso);
            }
        }

        // ---- coice em quem atirou de perto demais ----
        if (ficha.explosaoRaioProprio > 0f && dono != null)
        {
            float d = Vector3.Distance(dono.position + Vector3.up, ponto);
            if (d < ficha.explosaoRaioProprio)
            {
                var hp = dono.GetComponentInParent<Health>();
                if (hp != null && !hp.IsDead)
                {
                    float k = 1f - d / ficha.explosaoRaioProprio;
                    int dano = Mathf.Max(1, Mathf.RoundToInt(ficha.explosaoDano * 0.25f * k));
                    hp.TakeDamage(dano, (dono.position - ponto).normalized);
                }
            }
        }

        Destroy(gameObject);
    }
}
