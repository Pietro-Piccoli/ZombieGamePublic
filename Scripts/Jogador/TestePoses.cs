using System.Collections;
using System.Text;
using UnityEngine;

/// <summary>
/// BANCADA DE TESTE DAS ANIMACOES (so pra verificacao, nao entra no jogo).
///
/// Roda um roteiro de poses no play mode, mede numero por numero e tira foto de
/// cada uma. Serve pra provar que o sistema funciona em vez de achar que funciona:
///   - erro do cano em relacao a mira (graus)
///   - erro da mao esquerda ate a arma (cm)
///   - patinacao do pe plantado (m/s - o certo e perto de zero)
///   - altura da capsula e do pivo da camera
///   - quais clipes estao tocando e com que peso
///
/// Escreve _cap/relatorio.txt e um PNG por pose.
/// </summary>
public class TestePoses : MonoBehaviour
{
    public static string Relatorio = "";
    public static bool Rodando;

    private StringBuilder sb;
    private Transform player;
    private Animator an;
    private CameraJogo cam;
    private WeaponVisuals visuais;
    private MaoNaArma mao;
    private PesNoChao pes;
    private Agachar agachar;
    private EstadoPulo pulo;
    private CharacterController capsula;
    private string pasta;
    private Health vida;
    private float proximaLimpeza;

    /// <summary>
    /// O boneco nao pode morrer no meio do teste. Na primeira rodada ele morreu
    /// com uns 10 s: o jogo desligou o ThirdPersonController e deixou o
    /// Time.timeScale em 0,3 (camera lenta da morte), e metade das medidas saiu
    /// com o jogo desligado. Aqui ele fica imortal e o tempo fica travado em 1.
    /// </summary>
    private void Update()
    {
        Time.timeScale = 1f;
        if (vida != null && !vida.IsDead && vida.Current < vida.MaxHealth) vida.Heal(99999);
        if (Time.unscaledTime >= proximaLimpeza)
        {
            proximaLimpeza = Time.unscaledTime + 0.25f;
            if (player != null) LimparZumbis();
        }
    }


    private void Start()
    {
        StartCoroutine(Roteiro());
    }

    private IEnumerator Roteiro()
    {
        Rodando = true;
        sb = new StringBuilder();
        pasta = Application.dataPath + "/../_cap/";
        System.IO.Directory.CreateDirectory(pasta);

        player = GameObject.Find("Player").transform;
        vida = player.GetComponent<Health>();
        // pega o boneco ATIVO (o projeto tem mais de um filho com Animator)
        Transform mg = null;
        foreach (var a in player.GetComponentsInChildren<Animator>())
            if (a.gameObject.activeInHierarchy && a.isHuman) { mg = a.transform; break; }
        if (mg == null) { Relatorio = "nenhum boneco ativo com Animator humano"; Rodando = false; yield break; }
        an = mg.GetComponent<Animator>();
        mao = mg.GetComponent<MaoNaArma>();
        pes = mg.GetComponent<PesNoChao>();
        cam = FindAnyObjectByType<CameraJogo>();
        visuais = player.GetComponent<WeaponVisuals>();
        agachar = player.GetComponent<Agachar>();
        pulo = player.GetComponent<EstadoPulo>();
        capsula = player.GetComponent<CharacterController>();

        // ambiente limpo: sem zumbi e sem spawner
        var gd = GameObject.Find("GameDirector");
        if (gd != null)
            foreach (var mb in gd.GetComponents<MonoBehaviour>())
            {
                string n = mb.GetType().Name;
                if (n == "WaveManager" || n == "SpawnDirector") mb.enabled = false;
            }
        LimparZumbis();

        yield return new WaitForSeconds(1.2f);   // cai no chao
        LimparZumbis();

        yield return Pose("01_parado", 0.8f, false, 0f, Vector2.zero, false, false);
        yield return Pose("02_mira_reto", 0.8f, true, 0f, Vector2.zero, false, false);
        yield return Pose("03_mira_cima40", 0.8f, true, -40f, Vector2.zero, false, false);
        yield return Pose("04_mira_baixo30", 0.8f, true, 30f, Vector2.zero, false, false);
        yield return Pose("05_andando", 1.6f, false, 0f, new Vector2(0f, 1f), false, false);
        yield return Pose("06_andando_mira", 1.6f, true, 0f, new Vector2(0f, 1f), false, false);
        yield return Pose("07_strafe_dir", 1.6f, true, 0f, new Vector2(1f, 0f), false, false);
        yield return Pose("08_correndo", 1.6f, false, 0f, new Vector2(0f, 1f), true, false);
        yield return Pose("09_agachado", 1.2f, false, 0f, Vector2.zero, false, true);
        yield return Pose("10_agachado_mira", 1.2f, true, 0f, Vector2.zero, false, true);
        yield return Pose("11_agachado_andando", 1.6f, false, 0f, new Vector2(0f, 1f), false, true);
        yield return Levantar();
        yield return Pulo();
        yield return Girar();
        yield return Tiro();
        yield return Recarga();

        // devolve tudo ao normal
        PonteEntrada.debugMover = Vector2.zero;
        PonteEntrada.debugCorrer = false;
        CameraJogo.debugForcarMira = false;
        Agachar.debugAgachar = false;

        Relatorio = sb.ToString();
        System.IO.File.WriteAllText(pasta + "relatorio.txt", Relatorio);
        Rodando = false;
    }

    private void LimparZumbis()
    {
        foreach (var h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            if (h.gameObject != player.gameObject) Destroy(h.gameObject);
    }

    private IEnumerator Pose(string nome, float tempo, bool mirando, float pitch, Vector2 mover, bool correr, bool agachado)
    {
        CameraJogo.debugForcarMira = mirando;
        PonteEntrada.debugMover = mover;
        PonteEntrada.debugCorrer = correr;
        Agachar.debugAgachar = agachado;
        var f = typeof(CameraJogo).GetField("pitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(cam, pitch);

        LimparZumbis();
        yield return new WaitForSeconds(tempo * 0.6f);

        // amostra por 0,4 s pra medir patinacao e erro medio
        float t = 0f; int n = 0;
        float somaPatina = 0f, somaIK = 0f, somaV = 0f, somaH = 0f, maxIK = 0f;
        float melhorPlanta = 999f, distMao = 0f;
        Transform ossoE = an.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform ossoD = an.GetBoneTransform(HumanBodyBones.RightFoot);
        Vector3 antE = ossoE.position, antD = ossoD.position;
        bool temAnt = false;
        float somaSinal = 0f; int nSinal = 0;
        // janela longa de proposito: com 0,4 s a medida caia num pedaco
        // aleatorio do ciclo e o numero variava mais que o conserto
        while (t < 1.2f)
        {
            if (f != null) f.SetValue(cam, pitch);
            float patina = Mathf.Min(pes != null ? pes.VelEsq : 0f, pes != null ? pes.VelDir : 0f);
            // ESCORREGAO COM SINAL: + = o pe vai junto com o corpo (animacao
            // lenta demais), - = o pe vai pra tras (animacao rapida demais).
            if (temAnt && Time.deltaTime > 0.0001f)
            {
                Vector3 vE = (ossoE.position - antE) / Time.deltaTime;
                Vector3 vD = (ossoD.position - antD) / Time.deltaTime;
                Vector3 lento = new Vector3(vE.x, 0f, vE.z).magnitude < new Vector3(vD.x, 0f, vD.z).magnitude ? vE : vD;
                somaSinal += Vector3.Dot(new Vector3(lento.x, 0f, lento.z), player.forward);
                nSinal++;
            }
            antE = ossoE.position; antD = ossoD.position; temAnt = true;
            somaPatina += patina;
            if (patina < melhorPlanta) melhorPlanta = patina;
            if (mao != null) distMao = Mathf.Max(distMao, mao.DistanciaAlvoCm);
            if (mao != null) { somaIK += Mathf.Max(0f, mao.ErroCm); if (mao.ErroCm > maxIK) maxIK = mao.ErroCm; }
            float ev, eh; ErroCano(out ev, out eh);
            somaV += Mathf.Abs(ev); somaH += Mathf.Abs(eh);
            n++;
            t += Time.deltaTime;
            yield return null;
        }
        if (n < 1) n = 1;

        sb.AppendLine("== " + nome + "   [pedido: mira=" + mirando + " pitch=" + pitch + " mover=" + mover + " correr=" + correr + " agachar=" + agachado + "]");
        sb.AppendLine("   vivo=" + (vida != null ? (!vida.IsDead).ToString() : "?") + " timeScale=" + Time.timeScale.ToString("F2") + " controlador ligado=" + player.GetComponent<StarterAssets.ThirdPersonController>().enabled);
        sb.AppendLine("   escorregao com sinal = " + (nSinal > 0 ? (somaSinal / nSinal).ToString("F3") : "?") + " m/s  (+ animacao lenta demais / - rapida demais)");
        sb.AppendLine("   patinacao: media=" + (somaPatina / n).ToString("F3") + " m/s  MELHOR PLANTADA=" + melhorPlanta.ToString("F3") + " m/s   (a melhor tem que ser < 0,15)");
        sb.AppendLine("   alcance pedido pra mao esquerda = " + distMao.ToString("F1") + " cm (braco tem 56,8 cm)");
        sb.AppendLine("   mao esquerda na arma     = " + (somaIK / n).ToString("F2") + " cm (pico " + maxIK.ToString("F2") + ")");
        if (mirando) sb.AppendLine("   erro do cano             = " + (somaV / n).ToString("F2") + " vertical / " + (somaH / n).ToString("F2") + " lateral (graus)");
        sb.AppendLine("   capsula altura=" + capsula.height.ToString("F2") + " base=" + (capsula.center.y - capsula.height * 0.5f).ToString("F3") + "   pivoCamera=" + cam.OffsetPivo.y.ToString("F2"));
        sb.AppendLine("   VelX=" + an.GetFloat("VelX").ToString("F2") + " VelY=" + an.GetFloat("VelY").ToString("F2") + " Andando=" + an.GetFloat("Andando").ToString("F2") + " m/s | Mira=" + an.GetFloat("Mira").ToString("F2") + " Agachamento=" + an.GetFloat("Agachamento").ToString("F2") + " FasePulo=" + an.GetInteger("FasePulo"));
        for (int L = 0; L < an.layerCount; L++)
        {
            sb.Append("   camada " + an.GetLayerName(L) + " (peso " + an.GetLayerWeight(L).ToString("F2") + "): ");
            foreach (var ci in an.GetCurrentAnimatorClipInfo(L)) if (ci.weight > 0.02f) sb.Append(ci.clip.name + " " + ci.weight.ToString("F2") + "  ");
            sb.AppendLine();
        }
        sb.AppendLine("   pes: solaEsq=" + AlturaPe(HumanBodyBones.LeftFoot).ToString("F3") + " solaDir=" + AlturaPe(HumanBodyBones.RightFoot).ToString("F3") + " (m acima da base da capsula)");
        sb.AppendLine();

        Foto(nome);
    }

    private IEnumerator Levantar()
    {
        Agachar.debugAgachar = false;
        PonteEntrada.debugMover = Vector2.zero;
        yield return new WaitForSeconds(0.8f);
        sb.AppendLine("== 12_levantou");
        sb.AppendLine("   Agachado=" + agachar.Agachado + " capsula=" + capsula.height.ToString("F2") + " (esperado 1,75)");
        sb.AppendLine();
        Foto("12_levantou");
    }

    private IEnumerator Pulo()
    {
        LimparZumbis();
        EstadoPulo.debugPular = true;
        float t = 0f;
        int visto1 = 0, visto2 = 0, visto3 = 0;
        bool foto1 = false, foto2 = false, foto3 = false;
        float alturaMax = 0f;
        float y0 = player.position.y;
        while (t < 2.5f)
        {
            int f = pulo.Fase;
            if (f == 1) { visto1++; if (!foto1) { Foto("13_pulo_impulso"); foto1 = true; } }
            if (f == 2) { visto2++; if (!foto2 && t > 0.25f) { Foto("14_pulo_ar"); foto2 = true; } }
            if (f == 3) { visto3++; if (!foto3) { Foto("15_pulo_queda"); foto3 = true; } }
            float h = player.position.y - y0;
            if (h > alturaMax) alturaMax = h;
            t += Time.deltaTime;
            yield return null;
        }
        sb.AppendLine("== 13_15_pulo");
        sb.AppendLine("   quadros na fase 1=" + visto1 + " fase 2=" + visto2 + " fase 3=" + visto3 + "  (todas tem que ser > 0)");
        sb.AppendLine("   altura maxima do pulo = " + alturaMax.ToString("F2") + " m");
        sb.AppendLine("   fase final = " + pulo.Fase + " (tem que voltar a 0)");
        sb.AppendLine();
    }

    /// <summary>Rajada mirando: a arma tem que continuar colada nas duas maos e o cano nao pode subir.</summary>
    private IEnumerator Tiro()
    {
        LimparZumbis();
        CameraJogo.debugForcarMira = true;
        PonteEntrada.debugMover = Vector2.zero;
        Agachar.debugAgachar = false;
        var wc = player.GetComponent<WeaponController>();
        var mAtirar = typeof(WeaponController).GetMethod("Shoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        yield return new WaitForSeconds(0.5f);

        var mod = visuais.CurrentModel.transform;
        Vector3 baseLocal = mod.localPosition;
        float t = 0f, proximo = 0f, maxIK = 0f, maxSubida = 0f, maxDesloc = 0f, maxErroV = 0f;
        int tiros = 0;
        bool foto = false;
        while (t < 1.2f)
        {
            if (t >= proximo && mAtirar != null) { mAtirar.Invoke(wc, null); tiros++; proximo = t + 0.1f; }
            if (mao != null && mao.ErroCm > maxIK) maxIK = mao.ErroCm;
            Vector3 d = mod.localPosition - baseLocal;
            if (Mathf.Abs(d.magnitude) > maxDesloc) maxDesloc = d.magnitude;
            float ev, eh; ErroCano(out ev, out eh);
            if (Mathf.Abs(ev) > maxErroV) maxErroV = Mathf.Abs(ev);
            Vector3 cano = mod.TransformDirection(Vector3.forward);
            float subida = -Mathf.Asin(Mathf.Clamp(cano.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (Mathf.Abs(subida) > maxSubida) maxSubida = Mathf.Abs(subida);
            if (!foto && t > 0.4f) { Foto("17_tiro"); foto = true; }
            t += Time.deltaTime;
            yield return null;
        }
        sb.AppendLine("== 17_tiro (rajada mirando)");
        sb.AppendLine("   tiros disparados = " + tiros);
        sb.AppendLine("   mao esquerda solta da arma, pico = " + maxIK.ToString("F2") + " cm  (tem que ficar perto de 0)");
        sb.AppendLine("   arma saiu da mao, pico = " + (maxDesloc * 100f).ToString("F2") + " cm  (tem que ser 0: a arma anda com a mao)");
        sb.AppendLine("   cano subiu, pico = " + maxSubida.ToString("F2") + " graus  (o pedido foi: a ponta NAO sobe)");
        sb.AppendLine("   erro do cano em relacao a mira, pico = " + maxErroV.ToString("F2") + " graus");
        sb.AppendLine();
    }

    /// <summary>Na recarga o IK da mao esquerda tem que sair de cena (a mao vai no pente).</summary>
    private IEnumerator Recarga()
    {
        LimparZumbis();
        var wc = player.GetComponent<WeaponController>();
        var mRec = typeof(WeaponController).GetMethod("StartReload", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (mRec != null) mRec.Invoke(wc, null);
        yield return new WaitForSeconds(0.6f);
        float pesoNaRecarga = mao != null ? mao.PesoAtual : -1f;
        Foto("18_recarga");
        yield return new WaitForSeconds(3.0f);
        sb.AppendLine("== 18_recarga");
        sb.AppendLine("   peso do IK da mao esquerda durante a recarga = " + pesoNaRecarga.ToString("F2") + "  (tem que ser 0)");
        sb.AppendLine("   peso depois da recarga = " + (mao != null ? mao.PesoAtual.ToString("F2") : "?") + "  (tem que voltar a 1)");
        sb.AppendLine("   recarregando ainda? " + wc.IsReloading);
        sb.AppendLine();
    }

    private IEnumerator Girar()
    {
        LimparZumbis();
        PonteEntrada.debugMover = Vector2.zero;
        var fy = typeof(CameraJogo).GetField("yaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        float yaw = (float)fy.GetValue(cam);
        float t = 0f;
        bool viuGirar = false;
        string clipes = "";
        while (t < 1.6f)
        {
            yaw += 130f * Time.deltaTime;
            fy.SetValue(cam, yaw);
            if (t > 0.5f)
            {
                foreach (var ci in an.GetCurrentAnimatorClipInfo(0))
                    if (ci.clip.name.Contains("turn 90") && ci.weight > 0.2f) viuGirar = true;
                if (!viuGirar) { }
            }
            t += Time.deltaTime;
            yield return null;
        }
        foreach (var ci in an.GetCurrentAnimatorClipInfo(0)) if (ci.weight > 0.02f) clipes += ci.clip.name + " " + ci.weight.ToString("F2") + "  ";
        sb.AppendLine("== 16_girando_no_lugar");
        sb.AppendLine("   Giro=" + an.GetFloat("Giro").ToString("F0") + " deg/s   entrou no turn-in-place = " + viuGirar);
        sb.AppendLine("   clipes: " + clipes);
        sb.AppendLine();
        Foto("16_girando");
    }

    private float AlturaPe(HumanBodyBones osso)
    {
        Transform t = an.GetBoneTransform(osso);
        if (t == null) return 0f;
        float baseY = player.position.y + capsula.center.y - capsula.height * 0.5f;
        return t.position.y - baseY;
    }

    private void ErroCano(out float vertical, out float lateral)
    {
        vertical = 0f; lateral = 0f;
        if (visuais == null || visuais.CurrentModel == null || cam == null) return;
        Vector3 cano = visuais.CurrentModel.transform.TransformDirection(Vector3.forward);
        Ray r = cam.GetAimRay();
        float bp = -Mathf.Asin(Mathf.Clamp(cano.y, -1f, 1f)) * Mathf.Rad2Deg;
        float mp = -Mathf.Asin(Mathf.Clamp(r.direction.y, -1f, 1f)) * Mathf.Rad2Deg;
        vertical = mp - bp;
        Vector3 ch = new Vector3(cano.x, 0f, cano.z);
        Vector3 mh = new Vector3(r.direction.x, 0f, r.direction.z);
        if (ch.sqrMagnitude > 1e-6f && mh.sqrMagnitude > 1e-6f)
            lateral = Vector3.SignedAngle(ch.normalized, mh.normalized, Vector3.up);
    }

    private void Foto(string nome)
    {
        Vector3 alvo = player.position + Vector3.up * 0.95f;
        var go = new GameObject("__cap");
        var c = go.AddComponent<Camera>();
        c.fieldOfView = 38f;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.16f, 0.16f, 0.19f);
        go.transform.position = alvo + player.TransformVector(new Vector3(2.4f, 0.35f, 1.1f));
        go.transform.LookAt(alvo);
        var rt = new RenderTexture(1000, 800, 24, RenderTextureFormat.ARGB32);
        c.targetTexture = rt;
        c.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1000, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1000, 800), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        c.targetTexture = null;
        System.IO.File.WriteAllBytes(pasta + nome + ".png", tex.EncodeToPNG());
        DestroyImmediate(tex);
        rt.Release();
        DestroyImmediate(rt);
        DestroyImmediate(go);
    }
}
