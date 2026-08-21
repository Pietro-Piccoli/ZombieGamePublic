using UnityEngine;

/// <summary>
/// Dano-ao-longo-do-tempo no zumbi: fogo (laranja) ou acido (verde).
/// O WeaponController adiciona isso quando a municao especial acerta.
/// Reaplicar renova a duracao e usa o maior DPS.
/// </summary>
public class BurnStatus : MonoBehaviour
{
    private Health health;
    private float dps;
    private float acabaEm;
    private bool acido;
    private float acumulado;
    private float proximoPuff;
    private Light luz;

    /// <summary>Aplica (ou renova) o efeito no objeto que tem o Health.</summary>
    public static void Aplicar(Health alvo, float dps, float duracao, bool acido)
    {
        if (alvo == null || alvo.IsDead || dps <= 0f || duracao <= 0f) return;
        BurnStatus b = alvo.GetComponent<BurnStatus>();
        if (b == null) b = alvo.gameObject.AddComponent<BurnStatus>();
        b.health = alvo;
        b.dps = Mathf.Max(b.dps, dps);
        b.acabaEm = Time.time + duracao;
        b.acido = acido;
    }

    private void Start()
    {
        // luzinha pra LER o efeito de longe (barata: uma point light)
        var go = new GameObject(acido ? "AcidoLuz" : "FogoLuz");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 1.2f;
        luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.range = 2.6f;
        luz.intensity = 2.2f;
        luz.color = acido ? new Color(0.4f, 1f, 0.25f) : new Color(1f, 0.55f, 0.12f);
    }

    private void Update()
    {
        if (health == null || health.IsDead || Time.time > acabaEm)
        {
            Destroy(luz != null ? luz.gameObject : null);
            Destroy(this);
            return;
        }

        luz.color = acido ? new Color(0.4f, 1f, 0.25f) : new Color(1f, 0.55f, 0.12f);
        luz.intensity = 1.6f + Mathf.PingPong(Time.time * 6f, 1.2f);

        // dano fracionado vira inteiro acumulando
        acumulado += dps * Time.deltaTime;
        if (acumulado >= 1f)
        {
            int dmg = Mathf.FloorToInt(acumulado);
            acumulado -= dmg;
            health.TakeDamage(dmg, Vector3.up);
            DanoPopup.Mostrar(transform.position + Vector3.up * 1.7f, dmg,
                acido ? DanoPopup.Tipo.Acido : DanoPopup.Tipo.Fogo, transform);
            EstatisticasRun.RegistrarDano(dmg, acido ? DanoPopup.Tipo.Acido : DanoPopup.Tipo.Fogo);
            if (health.IsDead) EstatisticasRun.RegistrarAbate(false);
        }

        if (Time.time >= proximoPuff)
        {
            proximoPuff = Time.time + 0.35f;
            BloodFX.HitPuff(transform.position + Vector3.up * Random.Range(0.6f, 1.5f), Vector3.up);
        }
    }
}
