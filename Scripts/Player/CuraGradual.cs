using UnityEngine;

/// <summary>
/// CURA AO LONGO DO TEMPO.
///
/// O kit do chao nao enche a barra num piscar: ele abre um credito de
/// vida que escorre nos segundos seguintes, com o circulo de cura aceso
/// em volta do jogador enquanto durar. E como Halo faz com o escudo e
/// Vermintide com a bandagem: da tempo de a coisa ser LIDA, e a decisao
/// de pegar o kit no meio da horda vira um risco de verdade, porque a
/// vida nao volta no mesmo quadro.
///
/// Pegar outro kit com a cura em andamento nao desperdica: soma no
/// credito que ainda falta e recomeca a contagem.
///
/// O componente se cria sozinho no jogador - ninguem precisa arrastar
/// nada na cena.
/// </summary>
public class CuraGradual : MonoBehaviour
{
    [Tooltip("Em quantos segundos o credito inteiro entra.")]
    [SerializeField] private float duracao = 3f;

    [Tooltip("Altura do circulo de cura em relacao aos pes.")]
    [SerializeField] private float alturaVfx = 0.03f;
    [Tooltip("O prefab 'Healing circle' do pack tem 8 m de diametro - e uma zona de cura de area, nao aura de personagem. 0,30 deixa ele com ~2,4 m, do tamanho do jogador.")]
    [SerializeField] private float escalaVfx = 0.30f;

    private Health vida;
    private GameObject vfx;

    private float credito;        // quanto de vida ainda falta entregar
    private float porSegundo;     // ritmo atual da entrega
    private float sobra;          // fracao que ainda nao virou 1 de vida
    private float acumuladoPopup;
    private float proximoPopup;

    public bool Curando { get { return credito > 0f; } }

    /// <summary>
    /// Abre um credito de cura no jogador. Cria o componente se ainda nao
    /// existir. pct e porcentagem da vida MAXIMA.
    /// </summary>
    public static void Aplicar(GameObject jogador, float pct)
    {
        if (jogador == null || pct <= 0f) return;
        var c = jogador.GetComponent<CuraGradual>();
        if (c == null) c = jogador.AddComponent<CuraGradual>();
        c.Somar(pct);
    }

    private void Awake()
    {
        if (vida == null) vida = GetComponent<Health>();
    }

    private void Somar(float pct)
    {
        if (vida == null) vida = GetComponent<Health>();
        if (vida == null || vida.IsDead) return;

        credito += vida.MaxHealth * pct / 100f;

        // o ritmo e recalculado do credito INTEIRO que sobrou: pegar um
        // segundo kit acelera a entrega em vez de so empilhar fila.
        porSegundo = credito / Mathf.Max(0.1f, duracao);

        LigarVfx();
    }

    private void LigarVfx()
    {
        if (vfx != null) return;
        var cat = CatalogoVfx.Instancia;
        if (cat == null || cat.reviver == null) return;

        vfx = Instantiate(cat.reviver, transform);
        vfx.transform.localPosition = new Vector3(0f, alturaVfx, 0f);
        vfx.transform.localRotation = Quaternion.identity;
        vfx.transform.localScale = Vector3.one * escalaVfx;

        // sem isto o sistema de particulas ignora a escala do pai e o
        // circulo sai do tamanho do prefab, nao do tamanho pedido aqui.
        foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var m = ps.main;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            m.loop = true;
        }
    }

    private void DesligarVfx()
    {
        if (vfx == null) return;
        // para de emitir e some junto com as particulas que ja estao no ar,
        // em vez de piscar pra fora de uma vez.
        foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        vfx.transform.SetParent(null, true);
        Destroy(vfx, 2f);
        vfx = null;
    }

    private void Update()
    {
        if (credito <= 0f) return;
        if (vida == null || vida.IsDead) { Cancelar(); return; }

        float passo = Mathf.Min(credito, porSegundo * Time.deltaTime);
        credito -= passo;
        sobra += passo;

        // Health.Heal so aceita inteiro. A sobra fica guardada pra nao se
        // perder vida por arredondamento a cada quadro.
        int inteiro = Mathf.FloorToInt(sobra);
        if (inteiro > 0)
        {
            sobra -= inteiro;
            int antes = vida.Current;
            vida.Heal(inteiro);
            acumuladoPopup += vida.Current - antes;
        }

        // um numero por meio segundo. Um por quadro viraria sopa de numero.
        if (acumuladoPopup >= 1f && Time.time >= proximoPopup)
        {
            proximoPopup = Time.time + 0.5f;
            DanoPopup.Mostrar(transform.position + Vector3.up * 2.1f,
                              Mathf.RoundToInt(acumuladoPopup), DanoPopup.Tipo.Cura, transform);
            acumuladoPopup = 0f;
        }

        if (credito <= 0.0001f)
        {
            credito = 0f;
            sobra = 0f;
            if (acumuladoPopup >= 1f)
                DanoPopup.Mostrar(transform.position + Vector3.up * 2.1f,
                                  Mathf.RoundToInt(acumuladoPopup), DanoPopup.Tipo.Cura, transform);
            acumuladoPopup = 0f;
            DesligarVfx();
        }
    }

    private void Cancelar()
    {
        credito = 0f; sobra = 0f; acumuladoPopup = 0f;
        DesligarVfx();
    }

    private void OnDisable() { Cancelar(); }
}
