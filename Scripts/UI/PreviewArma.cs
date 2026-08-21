using UnityEngine;

/// <summary>
/// PALCO DE PREVIEW DA ARMA - o "Gunsmith".
///
/// Monta a arma equipada num palco isolado, longe da cena, e renderiza numa
/// RenderTexture pra UI mostrar. Sempre que a armaria muda, chama Remontar()
/// e o modelo aparece atualizado na hora.
///
/// O palco fica em Y = -5000 pra nao aparecer em nenhuma camera do jogo, e a
/// camera do palco tem cullingMask so da layer dele.
/// </summary>
public class PreviewArma : MonoBehaviour
{
    private const float AlturaPalco = -5000f;

    private Camera cam;
    private Transform pivo;
    private GameObject modelo;
    private WeaponData arma;
    private RenderTexture rt;
    private float giro;

    public RenderTexture Textura { get { return rt; } }

    // ---- sobreposicao: mostra uma peca que o jogador SELECIONOU mas ainda
    // nao comprou/equipou. E o que deixa ele ver antes de gastar.
    private bool temSobreposicao;
    private SlotAttach slotSobre;
    private AnexoArma anexoSobre;

    /// <summary>Previsualiza uma peca no slot dela. anexo == null limpa o slot.</summary>
    public void Prever(SlotAttach slot, AnexoArma anexo)
    {
        temSobreposicao = true; slotSobre = slot; anexoSobre = anexo;
        Remontar();
    }

    public void LimparPrevisao()
    {
        temSobreposicao = false; anexoSobre = null;
        Remontar();
    }
    [Tooltip("Velocidade da rotacao automatica, em graus por segundo.")]
    public float velocidadeGiro = 18f;
    public bool girando = true;

    public static PreviewArma Criar(WeaponData armaAlvo, int largura, int altura)
    {
        var go = new GameObject("PreviewArma");
        var p = go.AddComponent<PreviewArma>();
        p.arma = armaAlvo;
        p.Montar(largura, altura);
        return p;
    }

    private void Montar(int largura, int altura)
    {
        transform.position = new Vector3(0f, AlturaPalco, 0f);

        rt = new RenderTexture(largura, altura, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        rt.Create();

        var camGo = new GameObject("CamPreview");
        camGo.transform.SetParent(transform, false);
        cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);   // fundo transparente
        cam.orthographic = false;
        cam.fieldOfView = 28f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 20f;
        cam.targetTexture = rt;
        cam.cullingMask = ~0;   // o palco esta sozinho la embaixo, nada mais alcanca

        // luz chave + preenchimento: da volume a arma sem depender da cena
        var luzA = new GameObject("LuzChave");
        luzA.transform.SetParent(transform, false);
        var lA = luzA.AddComponent<Light>();
        lA.type = LightType.Directional; lA.intensity = 2.4f;
        lA.color = new Color(1f, 0.96f, 0.90f);
        luzA.transform.rotation = Quaternion.Euler(28f, -140f, 0f);

        var luzB = new GameObject("LuzPreenche");
        luzB.transform.SetParent(transform, false);
        var lB = luzB.AddComponent<Light>();
        lB.type = LightType.Directional; lB.intensity = 1.1f;
        lB.color = new Color(0.55f, 0.68f, 0.95f);
        luzB.transform.rotation = Quaternion.Euler(-12f, 55f, 0f);

        pivo = new GameObject("Pivo").transform;
        pivo.SetParent(transform, false);

        Remontar();
    }

    /// <summary>Refaz o modelo com o que estiver equipado agora.</summary>
    public void Remontar()
    {
        if (modelo != null) DestroyImmediate(modelo);
        if (arma == null || arma.modelPrefab == null) return;

        modelo = Instantiate(arma.modelPrefab, pivo);
        modelo.transform.localPosition = Vector3.zero;
        modelo.transform.localRotation = Quaternion.identity;
        modelo.transform.localScale = Vector3.one * Mathf.Max(0.01f, arma.modelScale);

        foreach (var c in modelo.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);

        // instala os anexos equipados, com o MESMO encaixe que o jogo usa
        for (int s = 0; s < 6; s++)
        {
            AnexoArma a = MetaProgressao.ResolverEquipado((SlotAttach)s);
            if (temSobreposicao && (SlotAttach)s == slotSobre) a = anexoSobre;   // o que ele esta olhando
            if (a == null || a.prefab == null) continue;
            var peca = Instantiate(a.prefab, modelo.transform);
            peca.transform.localPosition = a.PosicaoFinal;
            peca.transform.localEulerAngles = a.RotacaoFinal;
            peca.transform.localScale = Vector3.one * a.EscalaFinal;
            foreach (var c in peca.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        }

        Enquadrar();
    }

    /// <summary>Poe a camera na distancia certa pra arma preencher o quadro.</summary>
    private void Enquadrar()
    {
        if (modelo == null) return;

        Bounds b = new Bounds(modelo.transform.position, Vector3.zero);
        bool primeiro = true;
        foreach (var r in modelo.GetComponentsInChildren<Renderer>())
        {
            if (primeiro) { b = r.bounds; primeiro = false; }
            else b.Encapsulate(r.bounds);
        }
        if (primeiro) return;

        // centraliza o pivo na arma pra ela girar em torno dela mesma
        Vector3 desloc = pivo.position - b.center;
        modelo.transform.position += desloc;

        float raio = b.extents.magnitude;
        float dist = raio / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.12f;

        cam.transform.position = pivo.position + new Vector3(0f, 0.12f * raio, -dist);
        cam.transform.LookAt(pivo.position);
    }

    private void Update()
    {
        if (pivo == null) return;
        if (girando)
        {
            giro += velocidadeGiro * Time.unscaledDeltaTime;
            pivo.localRotation = Quaternion.Euler(0f, giro, 0f);
        }
    }

    /// <summary>Arrasto do mouse gira a arma na mao do jogador.</summary>
    public void Arrastar(float deltaX)
    {
        girando = false;
        giro += deltaX * 0.35f;
        pivo.localRotation = Quaternion.Euler(0f, giro, 0f);
    }

    private void OnDestroy()
    {
        if (rt != null) { rt.Release(); Destroy(rt); }
    }
}
