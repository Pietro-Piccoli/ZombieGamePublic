using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Arma hitscan orientada a FICHA (WeaponData): o controller e um so,
/// as armas sao assets. Troca com 1-6 e scroll do mouse.
///
/// O tiro e um raio que sai do CENTRO DA TELA (nao do cano). O rastro
/// visual sai do cano.
///
/// ROGUELITE: antes de cada tiro o controller pergunta ao UpgradeInventory
/// o que o player ja pegou - dano, cadencia, perfuracao, ricochete,
/// explosao, fogo, acido, pellets extras. Nenhum upgrade mexe na ficha
/// da arma: a ficha e o BASE, o inventario e o MULTIPLICADOR.
/// </summary>
public class WeaponController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CameraJogo cameraRig;
    [Tooltip("Ponta do cano - so pro rastro visual.")]
    [SerializeField] private Transform muzzle;
    private RecuoArma recuo;

    [Header("Loadout (fichas em Assets/Data/Weapons)")]
    [SerializeField] private WeaponData[] loadout;
    [SerializeField] private int startingSlot = 0;

    [Header("Colisao")]
    [Tooltip("Tudo menos Player e Enemy (a bala atravessa a capsula e acerta as Hitboxes).")]
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Perfuracao")]
    [Range(0.1f, 1f)]
    [Tooltip("Dano que sobra a cada inimigo atravessado. 0.7 = terceiro alvo toma 49%.")]
    [SerializeField] private float pierceFalloff = 0.7f;

    [Header("HUD da arma")]
    [SerializeField] private bool showWeaponHud = true;
    [SerializeField] private int hudFontSize = 24;

    public WeaponData CurrentWeapon { get; private set; }
    public int CurrentSlot { get; private set; }
    public int Ammo => CurrentSlot >= 0 && ammo != null ? ammo[CurrentSlot] : 0;
    public bool IsReloading { get; private set; }
    public bool InfiniteAmmo => CurrentWeapon == null || CurrentWeapon.infiniteAmmo;
    public WeaponData[] Loadout => loadout;

    /// <summary>Espalhamento atual (o Crosshair le isso pra abrir/fechar).</summary>
    public float CurrentSpread
    {
        get
        {
            if (CurrentWeapon == null) return 0f;
            float blend = cameraRig != null ? cameraRig.AimBlend : 0f;
            return Mathf.Lerp(CurrentWeapon.hipSpread, CurrentWeapon.adsSpread, blend);
        }
    }

    private int[] ammo;
    private float nextShotTime;
    private float reloadEndTime;
    private Text hudText;
    private Font font;
    private MontagemArma montagem;
    private EfeitosJogador efeitos;
    private WeaponVisuals visuals;
    private CapsulasEjetadas capsulas;
    private AnimacaoJogador anim;
    private UpgradeInventory inv;

    private void Awake()
    {
        if (cameraRig == null) cameraRig = FindAnyObjectByType<CameraJogo>();
        inv = GetComponent<UpgradeInventory>();
        if (muzzle == null)
        {
            Transform m = transform.Find("Muzzle");
            muzzle = m != null ? m : transform;
        }

        if (loadout == null || loadout.Length == 0)
        {
            Debug.LogError("[WeaponController] Loadout vazio - arraste as fichas WeaponData.", this);
            enabled = false;
            return;
        }

        ammo = new int[loadout.Length];
        for (int i = 0; i < loadout.Length; i++)
            ammo[i] = loadout[i] != null ? loadout[i].magazineSize : 0;

        SelectSlot(Mathf.Clamp(startingSlot, 0, loadout.Length - 1));
    }

    private void Start()
    {
        if (showWeaponHud) BuildHud();
    }

    /// <summary>O UpgradeInventory usa isto pra saber se oferece habilidade de classe.</summary>
    public bool TemNoLoadout(WeaponData w)
    {
        if (loadout == null) return false;
        for (int i = 0; i < loadout.Length; i++)
            if (loadout[i] == w) return true;
        return false;
    }

    /// <summary>Tamanho do carregador JA com upgrades.</summary>
    public int MagSize(WeaponData w)
    {
        float mult = inv != null ? inv.MagazineMult(w) : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(w.magazineSize * mult));
    }

    public void SelectSlot(int slot)
    {
        if (loadout == null || slot < 0 || slot >= loadout.Length || loadout[slot] == null) return;

        CurrentSlot = slot;
        CurrentWeapon = loadout[slot];
        IsReloading = false;
        nextShotTime = Time.time + 0.15f;

        if (anim == null) anim = GetComponent<AnimacaoJogador>();
        if (anim != null) anim.SetPistol(CurrentWeapon.empunhadura == GripType.Pistola);

        if (visuals == null) visuals = GetComponent<WeaponVisuals>();
        if (visuals != null)
        {
            visuals.Equip(CurrentWeapon);
            Transform m = FindDeep(transform, "Muzzle");
            if (m != null) muzzle = m;
        }
    }

    private static Transform FindDeep(Transform root, string nome)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == nome) return t;
        return null;
    }

    private void Update()
    {
        if (cameraRig == null || CurrentWeapon == null) return;

        HandleSwitching();

        if (IsReloading)
        {
            if (Time.time >= reloadEndTime)
            {
                IsReloading = false;
                ammo[CurrentSlot] = MagSize(CurrentWeapon);
            }
            UpdateHud();
            return;
        }

        if (InputReader.ReloadPressed) StartReload();

        bool wants = CurrentWeapon.fireMode == FireMode.FullAuto
            ? InputReader.Fire
            : InputReader.FirePressed;

        if (wants && Time.time >= nextShotTime)
        {
            if (!CurrentWeapon.infiniteAmmo && ammo[CurrentSlot] <= 0) StartReload();
            else Shoot();
        }

        UpdateHud();
    }

    private void HandleSwitching()
    {
        int slot = InputReader.PressedWeaponSlot;
        if (slot >= 0) SelectSlot(slot);

        float scroll = InputReader.ScrollDelta;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            int dir = scroll > 0f ? -1 : 1;
            int next = CurrentSlot;
            for (int i = 0; i < loadout.Length; i++)
            {
                next = (next + dir + loadout.Length) % loadout.Length;
                if (loadout[next] != null) { SelectSlot(next); break; }
            }
        }
    }

    private void Shoot()
    {
        WeaponData w = CurrentWeapon;

        if (montagem == null) montagem = GetComponent<MontagemArma>();
        if (efeitos == null) efeitos = GetComponent<EfeitosJogador>();
        float fireRate = w.fireRate * (inv != null ? inv.FireRateMult(w) : 1f)
                       * (montagem != null ? montagem.MultCadencia : 1f)
                       * (efeitos != null ? efeitos.MultAdrenalina : 1f);
        nextShotTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);

        Ray aimRay = cameraRig.GetAimRay();
        float spread = CurrentSpread * (montagem != null ? montagem.MultEspalhamento : 1f);

        int pellets = Mathf.Max(1, Mathf.RoundToInt(w.pellets * (inv != null ? inv.PelletsMult(w) : 1f)));
        int pierce = inv != null ? inv.Pierce(w) : 0;
        int ricochet = inv != null ? inv.Ricochet(w) : 0;

        // GATILHO DUPLO: chance de um projetil a mais neste disparo
        if (efeitos != null && Random.value < efeitos.ChanceProjetilExtra) pellets++;
        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = ApplySpread(aimRay.direction, spread);
            FirePellet(aimRay.origin, dir, muzzle != null ? muzzle.position : aimRay.origin,
                       w, pierce, ricochet);
        }

        if (anim == null) anim = GetComponent<AnimacaoJogador>();
        if (anim != null) anim.PlayShoot();
        EjetarCapsula();
        AplicarRecuo(w);


        EstatisticasRun.RegistrarTiro();
        if (!w.infiniteAmmo) ammo[CurrentSlot]--;
    }

    /// <summary>
    /// Um projetil: anda pelo mundo perfurando ate 'pierce' inimigos e
    /// rebatendo em parede ate 'ricochet' vezes. Cada perna do caminho
    /// ganha um tracer proprio.
    /// </summary>
    private void FirePellet(Vector3 origin, Vector3 dir, Vector3 tracerStart,
                            WeaponData w, int pierce, int ricochet)
    {
        if (efeitos == null) efeitos = GetComponent<EfeitosJogador>();
        float alcanceRestante = w.range * (efeitos != null ? efeitos.MultAlcance : 1f);
        if (montagem == null) montagem = GetComponent<MontagemArma>();
        float danoMult = (inv != null ? inv.DamageMult(w) : 1f)
                       * (montagem != null ? montagem.MultDano : 1f)
                       * (efeitos != null ? efeitos.MultSangueFrio : 1f)
                       * (efeitos != null && cameraRig != null ? efeitos.MultDanoMira(cameraRig.AimBlend) : 1f);
        var jaAtingidos = new HashSet<Health>();

        float raioExplosao = inv != null ? inv.ExplosionRadius(w) : 0f;
        float fogoDps = inv != null ? inv.FireDps(w) : 0f;
        float acidoDps = inv != null ? inv.AcidDps(w) : 0f;

        for (int seg = 0; seg < 1 + ricochet; seg++)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, alcanceRestante,
                hitMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Vector3 fimSegmento = origin + dir * alcanceRestante;
            bool bateuParede = false;
            RaycastHit parede = default;
            bool parouNoCorpo = false;

            foreach (RaycastHit hit in hits)
            {
                Hitbox hb = hit.collider.GetComponent<Hitbox>();

                if (hb == null)
                {
                    fimSegmento = hit.point;
                    bateuParede = true;
                    parede = hit;
                    break;
                }

                Health health = hit.collider.GetComponentInParent<Health>();
                if (health == null) continue;
                if (jaAtingidos.Contains(health)) continue;
                jaAtingidos.Add(health);

                ProcessarAcerto(hit, dir, w, danoMult, hb, health);

                if (raioExplosao > 0f) Explodir(hit.point, w, danoMult, raioExplosao, jaAtingidos);
                if (fogoDps > 0f) BurnStatus.Aplicar(health, fogoDps, inv.FireDuration(w), false);
                if (acidoDps > 0f) BurnStatus.Aplicar(health, acidoDps, inv.AcidDuration(w), true);

                if (jaAtingidos.Count > pierce)
                {
                    fimSegmento = hit.point;
                    parouNoCorpo = true;
                    break;
                }
                danoMult *= pierceFalloff;
            }

            ImpactFX.SpawnTracer(tracerStart, fimSegmento);

            if (parouNoCorpo || !bateuParede) return;

            ImpactFX.SpawnImpact(parede.point, parede.normal);
            if (raioExplosao > 0f) Explodir(parede.point, w, danoMult, raioExplosao, jaAtingidos);

            if (seg >= ricochet) return;

            alcanceRestante = Mathf.Max(4f, alcanceRestante - parede.distance);
            dir = Vector3.Reflect(dir, parede.normal);
            origin = parede.point + dir * 0.02f;
            tracerStart = origin;
            jaAtingidos.Clear();
        }
    }

    /// <summary>Dano + feridas + sangue + ragdoll num acerto. Igual pro tiro normal e perfurado.</summary>
    private void ProcessarAcerto(RaycastHit hit, Vector3 dir, WeaponData w, float danoMult, Hitbox hb, Health health)
    {
        ZombieWounds wounds = hit.collider.GetComponentInParent<ZombieWounds>();
        if (wounds != null)
        {
            wounds.AddWound(hit.point, hit.collider.transform);

            RaycastHit[] volta = Physics.RaycastAll(
                hit.point + dir * 0.7f, -dir, 0.68f, hitMask, QueryTriggerInteraction.Ignore);
            float melhor = float.MaxValue;
            RaycastHit saida = default;
            bool achou = false;
            foreach (RaycastHit rh in volta)
            {
                if (rh.collider.GetComponent<Hitbox>() == null) continue;
                if (rh.collider.GetComponentInParent<ZombieWounds>() != wounds) continue;
                if (rh.distance < melhor) { melhor = rh.distance; saida = rh; achou = true; }
            }
            if (achou && Vector3.Distance(saida.point, hit.point) > 0.08f)
                wounds.AddWound(saida.point, saida.collider.transform);

            BloodFX.HitPuff(hit.point, dir);

            int envMask = hitMask & ~(1 << LayerMask.NameToLayer("Hitbox"));
            RaycastHit atras;
            if (Physics.Raycast(hit.point + dir * 0.1f, dir, out atras,
                    5f, envMask, QueryTriggerInteraction.Ignore))
            {
                BloodFX.WallSplatter(atras.point, atras.normal,
                    Mathf.Lerp(0.45f, 0.18f, atras.distance / 5f));
            }
        }

        if (!health.IsDead)
        {
            float multParte = hb != null ? hb.DamageMultiplier : 1f;
            // upgrade de headshot amplifica so o BONUS da parte, nao o dano base
            if (multParte > 1f && inv != null)
                multParte = 1f + (multParte - 1f) * inv.HeadshotMult(w);
            int dmg = Mathf.Max(1, Mathf.RoundToInt(w.damage * danoMult * multParte));
            health.TakeDamage(dmg, dir);
            DanoPopup.Mostrar(hit.point, dmg,
                multParte > 1f ? DanoPopup.Tipo.Critico : DanoPopup.Tipo.Normal, health.transform);
            EstatisticasRun.RegistrarDano(dmg, multParte > 1f ? DanoPopup.Tipo.Critico : DanoPopup.Tipo.Normal);
            EstatisticasRun.RegistrarAcerto();
            // SENSACAO DE IMPACTO. Valores calibrados na faixa que Dead Cells e
            // Enter the Gungeon usam: o tiro que so acerta quase nao treme, o
            // abate treme e congela, e a cabeca e o dobro disso.
            bool cabeca = multParte > 1f;
            if (health.IsDead)
            {
                EstatisticasRun.RegistrarAbate(cabeca);
                Crosshair.MarcarAbate(cabeca);
                ImpactoDeCamera.Tremer(cabeca ? 0.26f : 0.16f);
                ImpactoDeCamera.Congelar(cabeca ? 0.075f : 0.045f);
            }
            else
            {
                Crosshair.MarcarAcerto(cabeca);
                ImpactoDeCamera.Tremer(cabeca ? 0.07f : 0.035f);
            }

            // TIRO DE MISERICORDIA: abaixo do limiar, morre agora
            if (efeitos != null && !health.IsDead && efeitos.LimiarExecucao > 0f
                && health.Percent < efeitos.LimiarExecucao)
            {
                int resto = health.Current;
                health.TakeDamage(resto + 10, dir);
                DanoPopup.Mostrar(hit.point + Vector3.up * 0.3f, resto, DanoPopup.Tipo.Critico, health.transform);
                EstatisticasRun.RegistrarDano(resto, DanoPopup.Tipo.Critico);
                if (health.IsDead) { EstatisticasRun.RegistrarAbate(false);
                    var ragEx = hit.collider.GetComponentInParent<ZombieRagdoll>();
                    if (ragEx != null) ragEx.EnterRagdoll(hit.collider, hit.point, dir, w.killImpulse); }
            }

            // ARCO VOLTAICO: salta pro vizinho
            if (efeitos != null) efeitos.TentarCorrente(health, dmg, hit.point);

            if (health.IsDead)
            {
                ZombieRagdoll rag = hit.collider.GetComponentInParent<ZombieRagdoll>();
                if (rag != null) rag.EnterRagdoll(hit.collider, hit.point, dir, w.killImpulse);
            }
        }
    }

    /// <summary>Dano em area no ponto. Nao re-acerta quem a bala ja pegou neste tiro.</summary>
    private void Explodir(Vector3 ponto, WeaponData w, float danoMult, float raio, HashSet<Health> jaAtingidos)
    {
        float pct = inv != null ? inv.ExplosionDmgPercent(w) : 50f;
        int dano = Mathf.Max(1, Mathf.RoundToInt(w.damage * danoMult * pct / 100f));

        Collider[] perto = Physics.OverlapSphere(ponto, raio, hitMask, QueryTriggerInteraction.Ignore);
        var atingidos = new HashSet<Health>();
        foreach (Collider c in perto)
        {
            if (c.GetComponent<Hitbox>() == null) continue;
            Health h = c.GetComponentInParent<Health>();
            if (h == null || h.IsDead || atingidos.Contains(h) || jaAtingidos.Contains(h)) continue;
            atingidos.Add(h);
            Vector3 dirExp = (c.transform.position - ponto).normalized;
            h.TakeDamage(dano, dirExp);
            DanoPopup.Mostrar(c.ClosestPoint(ponto), dano, DanoPopup.Tipo.Explosao, h.transform);
            EstatisticasRun.RegistrarDano(dano, DanoPopup.Tipo.Explosao);
            if (h.IsDead) EstatisticasRun.RegistrarAbate(false);
            BloodFX.HitPuff(c.ClosestPoint(ponto), dirExp);
            if (h.IsDead)
            {
                ZombieRagdoll rag = c.GetComponentInParent<ZombieRagdoll>();
                if (rag != null) rag.EnterRagdoll(c, c.ClosestPoint(ponto), dirExp, w.killImpulse * 1.4f);
            }
        }
        StartCoroutine(FlashExplosao(ponto, raio));
    }

    private IEnumerator FlashExplosao(Vector3 ponto, float raio)
    {
        var go = new GameObject("ExplosaoFlash");
        go.transform.position = ponto + Vector3.up * 0.2f;
        var luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.6f, 0.2f);
        luz.range = raio * 3f;
        float t = 0f;
        while (t < 0.18f)
        {
            luz.intensity = Mathf.Lerp(8f, 0f, t / 0.18f);
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(go);
    }

    /// <summary>
    /// Joga a capsula pela janela do ferrolho. A posicao e os eixos vem do
    /// MODELO da arma equipada, entao funciona pra qualquer arma: basta a ficha
    /// ter o 'ejectionOffset' apontando pra janela.
    /// </summary>
    /// <summary>
    /// RECUO de um disparo: coice visual na arma + tranco na mira.
    ///
    /// O coice da arma e o tranco da mira sao coisas separadas de proposito. O
    /// coice e so a arma pulando na mao; o tranco mexe na MIRA, e como a arma
    /// segue a mira, o corpo inteiro acompanha. Os dois crescem juntos na rajada
    /// (o multiplicador vem do RecuoArma, que conta os tiros seguidos).
    /// </summary>
    private void AplicarRecuo(WeaponData w)
    {
        if (w == null) return;

        if (recuo == null) recuo = GetComponent<RecuoArma>();

        float mult = 1f;
        if (recuo != null) mult = recuo.Disparar(w.coiceRecuo, w.coiceGiro);

        if (cameraRig != null)
        {
            // Sorteio PURO se anula ao longo da rajada (medido: 0,01 grau em 10
            // tiros) e o recuo lateral simplesmente some. A tendencia da um lado
            // preferido, entao a arma passa a ter PADRAO - a AK sobe puxando pra
            // direita, igual arma de verdade.
            float t = Mathf.Clamp(w.recuoTendenciaLado, -1f, 1f);
            float lado = (Random.Range(-1f, 1f) * (1f - Mathf.Abs(t)) + t) * w.recuoLateral * mult;
            cameraRig.AplicarRecuo(w.recuoVertical * mult, lado, w.recuoRecuperacao);
        }
    }

    private void EjetarCapsula()
    {
        if (capsulas == null)
        {
            capsulas = GetComponent<CapsulasEjetadas>();
            if (capsulas == null) return;
        }
        if (visuals == null) visuals = GetComponent<WeaponVisuals>();
        if (visuals == null || visuals.CurrentModel == null || CurrentWeapon == null) return;

        Transform m = visuals.CurrentModel.transform;
        Vector3 janela = m.TransformPoint(CurrentWeapon.ejectionOffset);

        // eixos DO MODELO: +X = lado direito da arma, +Y = topo
        Vector3 direita = m.TransformDirection(Vector3.right);
        Vector3 cima = m.TransformDirection(Vector3.up);

        // herda a velocidade do jogador pra capsula nao ficar pra tras quando corre
        var cc = GetComponent<CharacterController>();
        Vector3 vJogador = cc != null ? cc.velocity : Vector3.zero;

        capsulas.Ejetar(janela, direita, cima, vJogador);
    }

    private static Vector3 ApplySpread(Vector3 direction, float degrees)
    {
        if (degrees <= 0f) return direction;

        Vector3 forward = direction.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        right.Normalize();
        Vector3 up = Vector3.Cross(forward, right);

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(Random.value) * Mathf.Tan(degrees * Mathf.Deg2Rad);

        return (forward + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius).normalized;
    }

    private void StartReload()
    {
        if (CurrentWeapon.infiniteAmmo || IsReloading) return;
        if (ammo[CurrentSlot] == MagSize(CurrentWeapon)) return;

        IsReloading = true;
        float tempo = CurrentWeapon.reloadTime * (inv != null ? inv.ReloadMult(CurrentWeapon) : 1f);
        reloadEndTime = Time.time + tempo;

        if (anim == null) anim = GetComponent<AnimacaoJogador>();
        if (anim != null) anim.PlayReload();
    }

    // ---------------- HUD da arma ----------------

    private void BuildHud()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

        var canvasGo = new GameObject("WeaponHUD_Canvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 55;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var go = new GameObject("WeaponText");
        go.transform.SetParent(canvasGo.transform, false);
        hudText = go.AddComponent<Text>();
        hudText.font = font;
        hudText.fontSize = hudFontSize;
        hudText.fontStyle = FontStyle.Bold;
        hudText.color = Color.white;
        hudText.alignment = TextAnchor.LowerRight;
        hudText.horizontalOverflow = HorizontalWrapMode.Overflow;
        hudText.verticalOverflow = VerticalWrapMode.Overflow;
        hudText.raycastTarget = false;

        var rt = hudText.rectTransform;
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-28f, 28f);
        rt.sizeDelta = new Vector2(500f, 70f);
    }

    private void UpdateHud()
    {
        if (hudText == null || CurrentWeapon == null) return;

        string modo = CurrentWeapon.fireMode == FireMode.FullAuto ? "AUTO" : "SEMI";
        string municao = IsReloading ? "RECARREGANDO..."
            : (CurrentWeapon.infiniteAmmo ? "∞" : ammo[CurrentSlot] + " / " + MagSize(CurrentWeapon));

        hudText.text = "[" + (CurrentSlot + 1) + "] " + CurrentWeapon.displayName.ToUpper()
            + "  <" + modo + ">\n" + municao;
    }
}
