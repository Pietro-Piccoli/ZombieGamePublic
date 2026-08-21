using UnityEngine;

/// <summary>
/// EFEITOS DE CARTA NO JOGADOR - leva 2.
///
/// Um unico componente concentra as mecanicas que nao vivem no tiro:
/// aura de fogo, espinhos, vampirismo, regeneracao, reducao de dano, janela
/// fantasma, segunda chance, explosao ao matar, adrenalina, velocidade, ima,
/// cura por wave e o arco eletrico (chamado pelo WeaponController).
///
/// DECISOES:
///  - Vampirismo NAO tem gancho proprio no dano: ele le o delta de
///    EstatisticasRun.DanoTotal por frame. Todo dano do jogo ja passa por la
///    (tiro, explosao, DoT, granada), entao um poll cobre tudo sem tocar em
///    cada caminho de dano.
///  - Abates chegam por Health.QualquerMorte - um evento central em vez de
///    um hook por arma.
///  - Defesa (placa/fantasma/reviver) entra por Health.FiltroDano, que roda
///    ANTES do desconto: reduz, zera ou intercepta o golpe letal.
/// </summary>
[DefaultExecutionOrder(-10)]
public class EfeitosJogador : MonoBehaviour
{
    private UpgradeInventory inv;
    private Health vida;
    private LancadorGranadas granadas;
    private StarterAssets.ThirdPersonController controlador;

    // aura de fogo
    private GameObject fogoCorpo;
    private float proximoTickAura;

    // vampirismo
    private long danoVisto;

    // regeneracao
    private float regenAcumulada;

    // fantasma
    private float fantasmaAte;
    private GameObject escudoVfx;

    // segunda chance
    private int revivesUsados;

    // adrenalina
    private float adrenalinaAte;
    private GameObject buffVfx;

    // velocidade
    private float baseMove = -1f, baseSprint = -1f;
    private int pilhasVelAplicadas = -1;

    // explosao ao matar: trava de reentrada + teto de VFX simultaneo
    private static int explosoesEmVoo;
    private bool explodindo;

    private void Awake()
    {
        inv = GetComponent<UpgradeInventory>();
        vida = GetComponent<Health>();
        granadas = GetComponent<LancadorGranadas>();
        controlador = GetComponent<StarterAssets.ThirdPersonController>();

        if (vida != null) vida.FiltroDano = FiltrarDanoRecebido;
        Health.QualquerMorte += AoMorrerAlguem;
        if (vida != null) vida.OnDamaged += AoLevarDano;

        var wm = WaveManager.Instance;
        // OnWaveCleared pode ainda nao existir; assino no Start tambem
    }

    private void Start()
    {
        var wm = WaveManager.Instance;
        if (wm != null) wm.OnWaveCleared += AoFecharWave;
    }

    private void OnDestroy()
    {
        Health.QualquerMorte -= AoMorrerAlguem;
        var wm = WaveManager.Instance;
        if (wm != null) wm.OnWaveCleared -= AoFecharWave;
        Pickup.MultRaioIma = 1f;
    }

    private float V(UpgradeKind k) { return inv != null ? inv.Valor(k) : 0f; }
    private float S(UpgradeKind k) { return inv != null ? inv.Sec(k) : 0f; }

    // ================= consultas que o tiro faz =================

    /// <summary>SANGUE FRIO: ate +X% de dano conforme a vida cai.</summary>
    public float MultSangueFrio
    {
        get
        {
            float max = V(UpgradeKind.SangueFrio);
            if (max <= 0f || vida == null) return 1f;
            return 1f + (max / 100f) * (1f - Mathf.Clamp01(vida.Percent));
        }
    }

    /// <summary>ADRENALINA: cadencia extra enquanto o bonus dura.</summary>
    public float MultAdrenalina
    {
        get { return Time.time < adrenalinaAte ? 1f + V(UpgradeKind.AdrenalinaPercent) / 100f : 1f; }
    }

    /// <summary>MIRA CALIBRADA: bonus proporcional ao quanto esta mirando.</summary>
    public float MultDanoMira(float aimBlend)
    {
        float v = V(UpgradeKind.DanoMirandoPercent);
        return v <= 0f ? 1f : 1f + (v / 100f) * Mathf.Clamp01(aimBlend);
    }

    public float MultAlcance { get { return 1f + V(UpgradeKind.AlcancePercent) / 100f; } }
    public float ChanceProjetilExtra { get { return V(UpgradeKind.ChanceProjetilExtra) / 100f; } }
    public float LimiarExecucao { get { return V(UpgradeKind.Executar) / 100f; } }

    /// <summary>ARCO VOLTAICO: chamado pelo tiro depois de acertar.</summary>
    public void TentarCorrente(Health vitima, int danoBase, Vector3 ponto)
    {
        float pct = V(UpgradeKind.CorrenteEletrica);
        if (pct <= 0f || vitima == null) return;

        Health alvo = null; float melhor = 8f * 8f;
        foreach (var h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
        {
            if (h == vitima || h == vida || h.IsDead) continue;
            if (h.GetComponent<ZombieAI>() == null) continue;
            float d = (h.transform.position - ponto).sqrMagnitude;
            if (d < melhor) { melhor = d; alvo = h; }
        }
        if (alvo == null) return;

        int dano = Mathf.Max(1, Mathf.RoundToInt(danoBase * pct / 100f));
        Vector3 pAlvo = alvo.transform.position + Vector3.up * 1.2f;
        alvo.TakeDamage(dano, (pAlvo - ponto).normalized);
        DanoPopup.Mostrar(pAlvo, dano, DanoPopup.Tipo.Eletrico, alvo.transform);
        EstatisticasRun.RegistrarDano(dano, DanoPopup.Tipo.Eletrico);
        if (alvo.IsDead) EstatisticasRun.RegistrarAbate(false);

        var cat = CatalogoVfx.Instancia;
        if (cat != null && cat.impactoEletrico != null)
        {
            var fx = Instantiate(cat.impactoEletrico, pAlvo, Quaternion.identity);
            fx.transform.localScale = Vector3.one * 0.7f;
            Destroy(fx, 1.4f);
        }
    }

    // ================= defesa =================

    private int FiltrarDanoRecebido(int dano)
    {
        // REFLEXO FANTASMA: dentro da janela, nada entra
        if (Time.time < fantasmaAte) return 0;

        // PLACA DE CERAMICA
        float red = V(UpgradeKind.ReducaoDanoPercent);
        if (red > 0f) dano = Mathf.Max(1, Mathf.RoundToInt(dano * (1f - Mathf.Min(0.75f, red / 100f))));

        // SEGUNDA CHANCE: intercepta o golpe letal
        if (vida != null && dano >= vida.Current)
        {
            int pilhas = inv != null ? inv.PilhasDe(UpgradeKind.SegundaChance) : 0;
            if (pilhas > revivesUsados)
            {
                revivesUsados++;
                float pct = Mathf.Max(25f, V(UpgradeKind.SegundaChance));
                vida.Reviver(Mathf.RoundToInt(vida.MaxHealth * pct / 100f / Mathf.Max(1, pilhas)));
                fantasmaAte = Time.time + 2f;   // folego pra sair do meio
                var cat0 = CatalogoVfx.Instancia;
                if (cat0 != null && cat0.reviver != null)
                {
                    var fx = Instantiate(cat0.reviver, transform.position, Quaternion.identity);
                    Destroy(fx, 3f);
                }
                return 0;
            }
        }
        return dano;
    }

    private void AoLevarDano(Vector3 dir)
    {
        // ESPINHOS: devolve em quem esta perto
        float esp = V(UpgradeKind.Espinhos);
        if (esp > 0f)
        {
            float raio = Mathf.Max(2.2f, S(UpgradeKind.Espinhos));
            foreach (var h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (h == vida || h.IsDead || h.GetComponent<ZombieAI>() == null) continue;
                if ((h.transform.position - transform.position).sqrMagnitude > raio * raio) continue;
                int d = Mathf.Max(1, Mathf.RoundToInt(esp));
                h.TakeDamage(d, (h.transform.position - transform.position).normalized);
                DanoPopup.Mostrar(h.transform.position + Vector3.up * 1.4f, d, DanoPopup.Tipo.Normal, h.transform);
                EstatisticasRun.RegistrarDano(d, DanoPopup.Tipo.Normal);
                if (h.IsDead) EstatisticasRun.RegistrarAbate(false);
            }
        }

        // REFLEXO FANTASMA arma a janela DEPOIS do golpe que entrou
        float fant = V(UpgradeKind.FantasmaSegundos);
        if (fant > 0f && Time.time >= fantasmaAte)
        {
            fantasmaAte = Time.time + fant;
            var cat = CatalogoVfx.Instancia;
            if (cat != null && cat.escudoFantasma != null && escudoVfx == null)
            {
                escudoVfx = Instantiate(cat.escudoFantasma, transform);
                escudoVfx.transform.localPosition = Vector3.up * 1.0f;
                escudoVfx.transform.localScale = Vector3.one * 0.85f;
                Destroy(escudoVfx, fant);
            }
        }
    }

    // ================= abates =================

    private void AoMorrerAlguem(Health morto)
    {
        if (morto == null || morto == vida) return;
        if (morto.GetComponent<ZombieAI>() == null) return;

        // ADRENALINA
        if (V(UpgradeKind.AdrenalinaPercent) > 0f)
        {
            adrenalinaAte = Time.time + Mathf.Max(1.5f, S(UpgradeKind.AdrenalinaPercent));
            var cat = CatalogoVfx.Instancia;
            if (cat != null && cat.auraBuff != null && buffVfx == null)
            {
                buffVfx = Instantiate(cat.auraBuff, transform);
                buffVfx.transform.localPosition = Vector3.zero;
                buffVfx.transform.localScale = Vector3.one * 0.8f;
            }
        }

        // REPOSICAO TATICA
        float refund = V(UpgradeKind.RecargaGranadaAoMatar);
        if (refund > 0f && granadas != null) granadas.ReduzirRecarga(refund);

        // QUEIMA ARQUIVO (estilo Will-o'-the-wisp do RoR2)
        float danoExp = V(UpgradeKind.ExplosaoAoMatar);
        if (danoExp > 0f && !explodindo)
        {
            explodindo = true;   // o proprio estouro pode matar e disparar de novo; isso e desejado, mas um por vez
            try
            {
                Vector3 ponto = morto.transform.position + Vector3.up * 0.8f;
                float raio = Mathf.Max(2.5f, S(UpgradeKind.ExplosaoAoMatar));

                if (explosoesEmVoo < 6)
                {
                    var cat = CatalogoVfx.Instancia;
                    if (cat != null && cat.explosaoAoMatar != null)
                    {
                        explosoesEmVoo++;
                        var fx = Instantiate(cat.explosaoAoMatar, ponto, Quaternion.identity);
                        fx.transform.localScale = Vector3.one * 0.55f;
                        var vc = fx.GetComponent<VfxCurto>();
                        if (vc == null) { vc = fx.AddComponent<VfxCurto>(); vc.rajada = 1f; vc.tempoEmitindo = 0.3f; }
                        Destroy(fx, 1.8f);
                        Agendador.Depois(1.8f, () => { explosoesEmVoo = Mathf.Max(0, explosoesEmVoo - 1); });
                    }
                    Granada.FlashDeLuz(ponto, new Color(1f, 0.55f, 0.2f), raio);
                }

                foreach (var h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
                {
                    if (h == morto || h == vida || h.IsDead || h.GetComponent<ZombieAI>() == null) continue;
                    if ((h.transform.position - ponto).sqrMagnitude > raio * raio) continue;
                    int d = Mathf.Max(1, Mathf.RoundToInt(danoExp));
                    Vector3 dir = (h.transform.position - ponto).normalized;
                    h.TakeDamage(d, dir);
                    DanoPopup.Mostrar(h.transform.position + Vector3.up * 1.4f, d, DanoPopup.Tipo.Explosao, h.transform);
                    EstatisticasRun.RegistrarDano(d, DanoPopup.Tipo.Explosao);
                    if (h.IsDead)
                    {
                        EstatisticasRun.RegistrarAbate(false);
                        var rag = h.GetComponentInParent<ZombieRagdoll>();
                        if (rag != null) rag.EnterRagdoll(null, h.transform.position, dir, 8f);
                    }
                }
            }
            finally { explodindo = false; }
        }
    }

    private void AoFecharWave()
    {
        float cura = V(UpgradeKind.CuraPorWave);
        if (cura <= 0f || vida == null || vida.IsDead) return;
        vida.Heal(Mathf.RoundToInt(cura));
        DanoPopup.Mostrar(transform.position + Vector3.up * 2.1f, Mathf.RoundToInt(cura), DanoPopup.Tipo.Cura, transform);
        var cat = CatalogoVfx.Instancia;
        if (cat != null && cat.curaInstantanea != null)
        {
            var fx = Instantiate(cat.curaInstantanea, transform.position, Quaternion.identity);
            Destroy(fx, 2.5f);
        }
    }

    /// <summary>KIT DE CAMPO: o UpgradeInventory chama ao aplicar uma carta.</summary>
    public void AoEscolherCarta()
    {
        float pct = V(UpgradeKind.CuraAoEscolherCarta);
        if (pct <= 0f || vida == null || vida.IsDead) return;
        int cura = Mathf.RoundToInt(vida.MaxHealth * pct / 100f);
        vida.Heal(cura);
        DanoPopup.Mostrar(transform.position + Vector3.up * 2.1f, cura, DanoPopup.Tipo.Cura, transform);
    }

    // ================= por frame =================

    private void Update()
    {
        if (vida == null || vida.IsDead) return;
        if (Time.timeScale <= 0f) return;

        AtualizarAuraDeFogo();
        AtualizarVampirismo();
        AtualizarRegeneracao();
        AtualizarVelocidade();
        AtualizarIma();

        if (buffVfx != null && Time.time >= adrenalinaAte) { Destroy(buffVfx); buffVfx = null; }
    }

    private void AtualizarAuraDeFogo()
    {
        float dps = V(UpgradeKind.AuraDeFogo);
        bool ligada = dps > 0f;

        if (ligada && fogoCorpo == null)
        {
            var cat = CatalogoVfx.Instancia;
            if (cat != null && cat.fogoNoCorpo != null)
            {
                // o fogo preso no tronco - o "personagem pegando fogo"
                fogoCorpo = Instantiate(cat.fogoNoCorpo, transform);
                fogoCorpo.transform.localPosition = new Vector3(0f, 0.85f, 0f);
                fogoCorpo.transform.localScale = Vector3.one * 0.62f;
                foreach (var ps in fogoCorpo.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var m = ps.main; m.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
            }
        }
        else if (!ligada && fogoCorpo != null) { Destroy(fogoCorpo); fogoCorpo = null; }

        if (!ligada || Time.time < proximoTickAura) return;
        proximoTickAura = Time.time + 0.5f;

        float raio = Mathf.Max(2.5f, S(UpgradeKind.AuraDeFogo));
        int dano = Mathf.Max(1, Mathf.RoundToInt(dps * 0.5f));
        foreach (var h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
        {
            if (h == vida || h.IsDead || h.GetComponent<ZombieAI>() == null) continue;
            if ((h.transform.position - transform.position).sqrMagnitude > raio * raio) continue;
            h.TakeDamage(dano, (h.transform.position - transform.position).normalized);
            DanoPopup.Mostrar(h.transform.position + Vector3.up * 1.6f, dano, DanoPopup.Tipo.Fogo, h.transform);
            EstatisticasRun.RegistrarDano(dano, DanoPopup.Tipo.Fogo);
            if (h.IsDead) EstatisticasRun.RegistrarAbate(false);
        }
    }

    private void AtualizarVampirismo()
    {
        float pct = V(UpgradeKind.VampirismoPercent);
        long total = EstatisticasRun.Atual.DanoTotal;
        if (pct <= 0f) { danoVisto = total; return; }
        long delta = total - danoVisto;
        danoVisto = total;
        if (delta <= 0) return;
        regenAcumulada += (float)delta * pct / 100f;
        DescarregarCura();
    }

    private void AtualizarRegeneracao()
    {
        float porSeg = V(UpgradeKind.RegeneracaoPorSegundo);
        if (porSeg <= 0f) return;
        regenAcumulada += porSeg * Time.deltaTime;
        DescarregarCura();
    }

    private void DescarregarCura()
    {
        if (regenAcumulada < 1f) return;
        int cura = Mathf.FloorToInt(regenAcumulada);
        regenAcumulada -= cura;
        vida.Heal(cura);
    }

    private void AtualizarVelocidade()
    {
        if (controlador == null) return;
        int pilhas = inv != null ? inv.PilhasDe(UpgradeKind.VelocidadePercent) : 0;
        if (pilhas == pilhasVelAplicadas) return;
        if (baseMove < 0f) { baseMove = controlador.MoveSpeed; baseSprint = controlador.SprintSpeed; }
        float mult = 1f + V(UpgradeKind.VelocidadePercent) / 100f;
        controlador.MoveSpeed = baseMove * mult;
        controlador.SprintSpeed = baseSprint * mult;
        pilhasVelAplicadas = pilhas;
    }

    private void AtualizarIma()
    {
        Pickup.MultRaioIma = 1f + V(UpgradeKind.ImaPercent) / 100f;
    }
}

/// <summary>Agendazinha estatica pra callbacks com atraso sem coroutine solta.</summary>
public class Agendador : MonoBehaviour
{
    private static Agendador instancia;
    private class Item { public float quando; public System.Action acao; }
    private readonly System.Collections.Generic.List<Item> fila = new System.Collections.Generic.List<Item>();

    public static void Depois(float segundos, System.Action acao)
    {
        if (instancia == null)
        {
            var go = new GameObject("Agendador");
            instancia = go.AddComponent<Agendador>();
        }
        instancia.fila.Add(new Item { quando = Time.time + segundos, acao = acao });
    }

    private void Update()
    {
        for (int i = fila.Count - 1; i >= 0; i--)
        {
            if (Time.time < fila[i].quando) continue;
            var a = fila[i].acao;
            fila.RemoveAt(i);
            if (a != null) a();
        }
    }
}
