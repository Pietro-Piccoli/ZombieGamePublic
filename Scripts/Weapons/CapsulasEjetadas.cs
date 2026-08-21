using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CAPSULAS EJETADAS - o casquinho que pula da arma a cada tiro.
///
/// Usa POOL: as capsulas sao criadas uma vez e reaproveitadas, entao atirar em
/// full-auto nao gera lixo de memoria nem engasgo (Instantiate/Destroy a cada
/// tiro num fuzil de 10 tiros por segundo custa caro).
///
/// A capsula sai pela JANELA DO FERROLHO (lado direito da arma), com giro
/// aleatorio, bate no chao com fisica e some depois de um tempo.
///
/// Chamado pelo WeaponController a cada disparo.
/// </summary>
public class CapsulasEjetadas : MonoBehaviour
{
    [Header("Modelo")]
    [Tooltip("Prefab da capsula. Se vazio, usa um cilindro simples dourado.")]
    [SerializeField] private GameObject prefabCapsula;
    [Tooltip("Escala da capsula. 0.45 com o modelo do pack = ~3.9cm (7.62 real).")]
    [SerializeField] private float escala = 0.45f;

    [Header("Ejecao")]
    [Tooltip("Forca lateral (pra direita da arma).")]
    [SerializeField] private float forcaLateral = 2.2f;
    [Tooltip("Forca pra cima.")]
    [SerializeField] private float forcaCima = 1.6f;
    [Tooltip("Variacao aleatoria da forca (0.2 = 20%).")]
    [Range(0f, 0.6f)]
    [SerializeField] private float variacao = 0.25f;
    [SerializeField] private float giroAleatorio = 900f;

    [Header("Vida")]
    [SerializeField] private float segundosAteSumir = 6f;
    [SerializeField] private float segundosParaEncolher = 0.6f;
    [Tooltip("Quantas capsulas no pool. Passou disso, reaproveita a mais antiga.")]
    [SerializeField] private int tamanhoPool = 24;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigurarColisoes()
    {
        int d = LayerMask.NameToLayer("Detritos");
        if (d < 0) return;
        foreach (string n in new string[] { "Player", "Enemy", "Hitbox" })
        {
            int l = LayerMask.NameToLayer(n);
            if (l >= 0) Physics.IgnoreLayerCollision(d, l, true);
        }
        Physics.IgnoreLayerCollision(d, d, true);
    }

    private readonly List<Rigidbody> pool = new List<Rigidbody>();
    private readonly List<float> nascimento = new List<float>();
    private readonly List<Vector3> escalaBase = new List<Vector3>();
    private int proxima;
    private Transform raizPool;

    private void Awake()
    {
        var go = new GameObject("__CapsulasPool__");
        raizPool = go.transform;

        for (int i = 0; i < tamanhoPool; i++)
        {
            GameObject c = CriarCapsula();
            c.transform.SetParent(raizPool, false);
            c.SetActive(false);
            var rb = c.GetComponent<Rigidbody>();
            pool.Add(rb);
            nascimento.Add(-999f);
            escalaBase.Add(c.transform.localScale);
        }
    }

    private GameObject CriarCapsula()
    {
        GameObject c;
        if (prefabCapsula != null)
        {
            c = Instantiate(prefabCapsula);
            foreach (var col in c.GetComponentsInChildren<Collider>(true)) Destroy(col);
        }
        else
        {
            c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(c.GetComponent<Collider>());
            var mr = c.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.83f, 0.66f, 0.22f));
            mat.SetFloat("_Metallic", 0.9f);
            mat.SetFloat("_Smoothness", 0.7f);
            mr.sharedMaterial = mat;
        }
        c.name = "Capsula";
        // O modelo do pack (Bullet_A) tem 8.7cm cru; uma capsula 7.62 real tem
        // ~3.9cm. O cilindro gerado tem 2m. Por isso a escala e diferente pra cada.
        c.transform.localScale = Vector3.one * (prefabCapsula != null ? escala : escala * 0.027f);

        var capsCol = c.AddComponent<CapsuleCollider>();
            // O collider vive em espaco LOCAL, entao precisa ser dividido pela escala.
            // Sem isso o raio 0.5 / altura 2.0 viravam 22cm / 90cm no mundo - uma
            // capsula do tamanho de um degrau deitada no chao, que era exatamente
            // onde o player tropecava e 'subia em cima de nada'.
            float escMundo = Mathf.Max(0.0001f, c.transform.localScale.x);
            capsCol.radius = 0.008f / escMundo;   // ~8mm de raio no mundo
            capsCol.height = 0.040f / escMundo;   // ~4cm de comprimento no mundo
        capsCol.direction = 1;

        var rb = c.AddComponent<Rigidbody>();
        rb.mass = 0.012f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

            // 'Ignore Raycast' so tira de raycast, NAO tira de colisao: o player
            // continuava esbarrando. 'Detritos' ignora Player, Enemy e Hitbox.
            int layerIgnorar = LayerMask.NameToLayer("Detritos");
            if (layerIgnorar < 0) layerIgnorar = LayerMask.NameToLayer("Ignore Raycast");
        if (layerIgnorar >= 0) c.layer = layerIgnorar;   // bala nao acerta a propria capsula
        return c;
    }

    /// <summary>
    /// Ejeta uma capsula. 'posicao' = janela do ferrolho, 'direita' = lado
    /// direito da arma, 'cima' = topo da arma.
    /// </summary>
    public void Ejetar(Vector3 posicao, Vector3 direita, Vector3 cima, Vector3 velocidadeDoJogador)
    {
        if (pool.Count == 0) return;

        int i = proxima % pool.Count;
        proxima++;

        var rb = pool[i];
        if (rb == null) return;
        var go = rb.gameObject;

        go.SetActive(true);
        go.transform.localScale = escalaBase[i];
        go.transform.position = posicao;
        go.transform.rotation = Random.rotation;

        float v = 1f + Random.Range(-variacao, variacao);
        Vector3 impulso = direita.normalized * (forcaLateral * v)
                        + cima.normalized * (forcaCima * (1f + Random.Range(-variacao, variacao)));

        rb.isKinematic = false;
        rb.linearVelocity = velocidadeDoJogador + impulso;
        rb.angularVelocity = Random.insideUnitSphere * (giroAleatorio * Mathf.Deg2Rad);

        nascimento[i] = Time.time;
    }

    private void Update()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (nascimento[i] < 0f) continue;
            var rb = pool[i];
            if (rb == null || !rb.gameObject.activeSelf) continue;

            float idade = Time.time - nascimento[i];
            if (idade >= segundosAteSumir)
            {
                float t = (idade - segundosAteSumir) / Mathf.Max(0.01f, segundosParaEncolher);
                if (t >= 1f)
                {
                    rb.gameObject.SetActive(false);
                    nascimento[i] = -999f;
                }
                else
                {
                    rb.transform.localScale = Vector3.Lerp(escalaBase[i], Vector3.zero, t);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (raizPool != null) Destroy(raizPool.gameObject);
    }
}
