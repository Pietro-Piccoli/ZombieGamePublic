using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Feridas de bala - versao 4: buraco + limpeza de ilhas.
///
/// Por pellet (em espaco de bind, via o osso atingido):
///  1. DELETA os triangulos no nucleo -> buraco vazado.
///  2. AFUNDA os vertices ao redor -> borda de cratera.
///  3. Depois do corte, acha grupos de triangulos que ficaram DESCONECTADOS
///     do corpo e deleta junto - nada de triangulozinho flutuando.
///
/// A conectividade e por POSICAO (vertices soldados), porque malha low-poly
/// duplica vertices nas arestas duras. Partes que ja nasceram separadas
/// (dente, olho) sao reconhecidas no init e nunca tratadas como ilha.
/// </summary>
public class ZombieWounds : MonoBehaviour
{
    [Header("Buraco (triangulos removidos)")]
    [Tooltip("Raio do nucleo onde a malha e ARRANCADA, por pellet.")]
    [SerializeField] private float holeRadius = 0.06f;

    [Header("Cratera (vertices afundados na borda)")]
    [SerializeField] private float craterRadius = 0.16f;
    [SerializeField] private float craterDepth = 0.06f;
    [SerializeField] private float maxTotalDepth = 0.12f;

    [Header("Decapitacao")]
    [Tooltip("Triangulos da cabeca removidos pra disparar o esguicho de pescoco.")]
    [SerializeField] private int headTrisToDecapitate = 10;

    [Header("Limpeza de ilhas")]
    [Tooltip("Pedaco desconectado com MENOS triangulos que isso e deletado.")]
    [SerializeField] private int minIslandTriangles = 12;

    public int WoundedVertexCount { get; private set; }
    public int RemovedTriangleCount { get; private set; }
    public int IslandsRemoved { get; private set; }

    private struct Wound { public Vector3 bindPoint; public bool isHead; }

    private readonly List<Wound> queue = new List<Wound>();

    private SkinnedMeshRenderer smr;
    private Mesh instanceMesh;
    private Vector3[] originalVerts;
    private Vector3[] currentVerts;
    private Vector3[] baseNormals;
    private float[] accumulated;
    private Matrix4x4[] inverseBindposes;
    private Dictionary<Transform, int> boneIndex;
    private List<int>[] aliveTris;

    // conectividade
    private int[] weldOf;              // indice do vertice -> id soldado (por posicao)
    private int weldCount;
    private int[] origCompOfWeld;      // id soldado -> componente ORIGINAL
    private Dictionary<int, int> origCompSize; // componente original -> qtd de triangulos
    private int[] uf;                  // union-find reutilizavel

    private bool initialized;
    private bool initFailed;
    private int headTrisRemoved;
    private bool decapitated;
    private Transform headBone;

    public void AddWound(Vector3 worldPoint, Transform hitBone)
    {
        if (hitBone == null || !EnsureInit()) return;

        // Corpo ja destruido / malha liberada: ignora em silencio.
        if (instanceMesh == null || boneIndex == null || inverseBindposes == null) return;

        // cabeca? (a Hitbox da cabeca tem multiplicador de dano maior)
        bool isHead = false;
        Hitbox hb = hitBone.GetComponent<Hitbox>();
        if (hb != null && hb.DamageMultiplier > 1.5f)
        {
            isHead = true;
            headBone = hitBone;
        }

        int idx;
        if (!boneIndex.TryGetValue(hitBone, out idx))
        {
            if (hitBone.parent == null || !boneIndex.TryGetValue(hitBone.parent, out idx))
                return;
            hitBone = hitBone.parent;
        }

        if (idx < 0 || idx >= inverseBindposes.Length) return;

        Vector3 boneLocal = hitBone.InverseTransformPoint(worldPoint);
        Vector3 bindPoint = inverseBindposes[idx].MultiplyPoint3x4(boneLocal);
        queue.Add(new Wound { bindPoint = bindPoint, isHead = isHead });
    }

    private void LateUpdate()
    {
        if (queue.Count == 0 || !initialized) return;

        bool vertsChanged = false;
        bool trisChanged = false;

        for (int q = 0; q < queue.Count; q++)
        {
            Vector3 p = queue[q].bindPoint;
            bool isHead = queue[q].isHead;

            for (int i = 0; i < originalVerts.Length; i++)
            {
                float d = Vector3.Distance(originalVerts[i], p);
                if (d >= craterRadius) continue;

                float room = maxTotalDepth - accumulated[i];
                if (room <= 0f) continue;

                float amount = Mathf.Min(craterDepth * (1f - d / craterRadius), room);
                if (accumulated[i] <= 0f) WoundedVertexCount++;
                accumulated[i] += amount;
                currentVerts[i] -= baseNormals[i] * amount;
                vertsChanged = true;
            }

            float holeSqr = holeRadius * holeRadius;
            for (int s = 0; s < aliveTris.Length; s++)
            {
                List<int> tris = aliveTris[s];
                for (int t = tris.Count - 3; t >= 0; t -= 3)
                {
                    Vector3 c = (originalVerts[tris[t]] + originalVerts[tris[t + 1]] + originalVerts[tris[t + 2]]) / 3f;
                    if ((c - p).sqrMagnitude >= holeSqr) continue;

                    tris.RemoveRange(t, 3);
                    RemovedTriangleCount++;
                    if (isHead) headTrisRemoved++;
                    trisChanged = true;
                }
            }
        }

        queue.Clear();

        if (trisChanged) trisChanged |= RemoveIslands();

        if (vertsChanged) instanceMesh.vertices = currentVerts;
        if (trisChanged)
        {
            for (int s = 0; s < aliveTris.Length; s++)
                instanceMesh.SetTriangles(aliveTris[s], s, false);
        }

        // cabeca destruida -> esguicho saindo do toco do pescoco
        if (!decapitated && headTrisRemoved >= headTrisToDecapitate)
        {
            decapitated = true;
            BloodFX.NeckFountain(headBone);
        }
    }

    /// <summary>Deleta grupos de triangulos que ficaram soltos do corpo.</summary>
    private bool RemoveIslands()
    {
        // union-find sobre os vertices soldados dos triangulos vivos
        for (int i = 0; i < weldCount; i++) uf[i] = i;

        for (int s = 0; s < aliveTris.Length; s++)
        {
            List<int> tris = aliveTris[s];
            for (int t = 0; t < tris.Count; t += 3)
            {
                int a = weldOf[tris[t]];
                int b = weldOf[tris[t + 1]];
                int c = weldOf[tris[t + 2]];
                Union(a, b);
                Union(b, c);
            }
        }

        // tamanho (em triangulos) de cada componente ATUAL
        var compSize = new Dictionary<int, int>();
        for (int s = 0; s < aliveTris.Length; s++)
        {
            List<int> tris = aliveTris[s];
            for (int t = 0; t < tris.Count; t += 3)
            {
                int root = Find(weldOf[tris[t]]);
                int v;
                compSize.TryGetValue(root, out v);
                compSize[root] = v + 1;
            }
        }

        // ilha = componente pequeno E menor que o shell original dele
        // (dente/olho que ja nasceu pequeno continua do tamanho original -> fica)
        bool removed = false;
        for (int s = 0; s < aliveTris.Length; s++)
        {
            List<int> tris = aliveTris[s];
            for (int t = tris.Count - 3; t >= 0; t -= 3)
            {
                int w = weldOf[tris[t]];
                int root = Find(w);
                int sizeNow = compSize[root];
                if (sizeNow >= minIslandTriangles) continue;

                int sizeOrig;
                origCompSize.TryGetValue(origCompOfWeld[w], out sizeOrig);
                if (sizeNow >= sizeOrig) continue; // shell original pequeno, intacto

                tris.RemoveRange(t, 3);
                RemovedTriangleCount++;
                removed = true;
            }
        }

        if (removed) IslandsRemoved++;
        return removed;
    }

    private int Find(int x)
    {
        while (uf[x] != x) { uf[x] = uf[uf[x]]; x = uf[x]; }
        return x;
    }

    private void Union(int a, int b)
    {
        int ra = Find(a), rb = Find(b);
        if (ra != rb) uf[ra] = rb;
    }

    private bool EnsureInit()
    {
        if (initialized) return true;
        if (initFailed) return false;

        smr = GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null || smr.sharedMesh == null || !smr.sharedMesh.isReadable)
        {
            if (smr != null && smr.sharedMesh != null && !smr.sharedMesh.isReadable)
                Debug.LogError("[ZombieWounds] Malha sem Read/Write no importador.", this);
            initFailed = true;
            return false;
        }

        instanceMesh = Instantiate(smr.sharedMesh);
        instanceMesh.name = smr.sharedMesh.name + "_wounded";
        smr.sharedMesh = instanceMesh;

        originalVerts = instanceMesh.vertices;
        currentVerts = instanceMesh.vertices;
        baseNormals = instanceMesh.normals;
        accumulated = new float[originalVerts.Length];

        Matrix4x4[] binds = instanceMesh.bindposes;
        inverseBindposes = new Matrix4x4[binds.Length];
        for (int i = 0; i < binds.Length; i++)
            inverseBindposes[i] = binds[i].inverse;

        Transform[] bones = smr.bones;
        boneIndex = new Dictionary<Transform, int>(bones.Length);
        for (int i = 0; i < bones.Length && i < binds.Length; i++)
            if (bones[i] != null && !boneIndex.ContainsKey(bones[i]))
                boneIndex[bones[i]] = i;

        aliveTris = new List<int>[instanceMesh.subMeshCount];
        for (int s = 0; s < instanceMesh.subMeshCount; s++)
            aliveTris[s] = new List<int>(instanceMesh.GetTriangles(s));

        // solda vertices por posicao (grade de 0.1mm)
        weldOf = new int[originalVerts.Length];
        var weldMap = new Dictionary<Vector3Int, int>(originalVerts.Length);
        for (int i = 0; i < originalVerts.Length; i++)
        {
            Vector3 v = originalVerts[i];
            var key = new Vector3Int(
                Mathf.RoundToInt(v.x * 10000f),
                Mathf.RoundToInt(v.y * 10000f),
                Mathf.RoundToInt(v.z * 10000f));
            int id;
            if (!weldMap.TryGetValue(key, out id))
            {
                id = weldMap.Count;
                weldMap[key] = id;
            }
            weldOf[i] = id;
        }
        weldCount = weldMap.Count;
        uf = new int[weldCount];

        // Normal MEDIA por ponto soldado: vertices duplicados (arestas duras)
        // afundam juntos, na mesma direcao - sem isso a cratera RASGA a malha,
        // porque cada duplicata ia pra um lado seguindo a propria normal.
        Vector3[] weldNormal = new Vector3[weldCount];
        for (int i = 0; i < originalVerts.Length; i++)
            weldNormal[weldOf[i]] += baseNormals[i];
        for (int i = 0; i < weldCount; i++)
            weldNormal[i] = weldNormal[i].sqrMagnitude > 0.0001f ? weldNormal[i].normalized : Vector3.up;
        for (int i = 0; i < originalVerts.Length; i++)
            baseNormals[i] = weldNormal[weldOf[i]];

        // componentes ORIGINAIS (pra reconhecer shells que ja nasceram separados)
        for (int i = 0; i < weldCount; i++) uf[i] = i;
        for (int s = 0; s < aliveTris.Length; s++)
        {
            List<int> tris = aliveTris[s];
            for (int t = 0; t < tris.Count; t += 3)
            {
                Union(weldOf[tris[t]], weldOf[tris[t + 1]]);
                Union(weldOf[tris[t + 1]], weldOf[tris[t + 2]]);
            }
        }

        origCompOfWeld = new int[weldCount];
        for (int i = 0; i < weldCount; i++) origCompOfWeld[i] = Find(i);

        origCompSize = new Dictionary<int, int>();
        for (int s = 0; s < aliveTris.Length; s++)
        {
            List<int> tris = aliveTris[s];
            for (int t = 0; t < tris.Count; t += 3)
            {
                int root = origCompOfWeld[weldOf[tris[t]]];
                int v;
                origCompSize.TryGetValue(root, out v);
                origCompSize[root] = v + 1;
            }
        }

        initialized = true;
        return true;
    }

    private void OnDestroy()
    {
        if (instanceMesh != null) Destroy(instanceMesh);
    }
}
