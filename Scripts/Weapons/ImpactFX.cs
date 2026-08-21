using UnityEngine;

/// <summary>
/// Efeitos de tiro criados em codigo - sem prefab, sem wiring.
/// E proposital: na Etapa 7 isso vira pool + particula de verdade,
/// mas por enquanto o que importa e ver o tiro acontecendo.
/// </summary>
public static class ImpactFX
{
    private static Material sharedMat;

    private static Material Mat
    {
        get
        {
            if (sharedMat == null)
            {
                // Shader que existe tanto em URP quanto em Built-in.
                Shader s = Shader.Find("Universal Render Pipeline/Unlit");
                if (s == null) s = Shader.Find("Unlit/Color");
                if (s == null) s = Shader.Find("Sprites/Default");
                sharedMat = new Material(s);
            }
            return sharedMat;
        }
    }

    /// <summary>Marca de impacto na superficie.</summary>
    public static void SpawnImpact(Vector3 point, Vector3 normal, float life = 4f)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Impact";
        Object.Destroy(go.GetComponent<Collider>());

        go.transform.position = point + normal * 0.01f;
        go.transform.localScale = Vector3.one * 0.09f;

        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = Mat;
        r.material.color = new Color(0.12f, 0.12f, 0.12f);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        Object.Destroy(go, life);
    }

    /// <summary>Rastro da bala. Curto de proposito - so o suficiente pra ler.</summary>
    public static void SpawnTracer(Vector3 from, Vector3 to, float life = 0.035f)
    {
        GameObject go = new GameObject("Tracer");
        var lr = go.AddComponent<LineRenderer>();

        lr.material = Mat;
        lr.startColor = new Color(1f, 0.85f, 0.4f, 1f);
        lr.endColor = new Color(1f, 0.7f, 0.2f, 0f);
        lr.startWidth = 0.035f;
        lr.endWidth = 0.008f;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        Object.Destroy(go, life);
    }
}
