using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sangue procedural - sem asset, tudo criado em codigo.
///
///  HitPuff:      respingo curto no ponto do impacto (estilo PUBG).
///  WallSplatter: mancha de sangue na superficie ATRAS do alvo.
///  NeckFountain: esguicho continuo (decapitacao), preso no osso.
///
/// Particulas sao quadradinhos sem textura - combina com o low-poly.
/// Manchas tem orcamento: passou de MaxDecals, a mais velha some.
/// </summary>
public static class BloodFX
{
    private const int MaxDecals = 60;

    private static readonly Color BloodBright = new Color(0.55f, 0.03f, 0.03f, 1f);
    private static readonly Color BloodDark = new Color(0.25f, 0.01f, 0.01f, 1f);

    private static Material particleMat;
    private static Material decalMat;
    private static readonly Queue<GameObject> decals = new Queue<GameObject>();

    private static Material ParticleMat
    {
        get
        {
            if (particleMat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
                if (s == null) s = Shader.Find("Sprites/Default");
                particleMat = new Material(s);
                particleMat.color = BloodBright;
            }
            return particleMat;
        }
    }

    private static Material DecalMat
    {
        get
        {
            if (decalMat == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Unlit");
                if (s == null) s = Shader.Find("Sprites/Default");
                decalMat = new Material(s);
                decalMat.color = BloodDark;
            }
            return decalMat;
        }
    }

    // ---------------- respingo de impacto ----------------

    public static void HitPuff(Vector3 position, Vector3 bulletDir)
    {
        var ps = CreateSystem("BloodPuff", position);
        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.gravityModifier = 1.2f;
        main.startColor = BloodBright;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.02f;

        // espirra de volta, contra a bala (sangue sai pro lado de quem atirou)
        ps.transform.rotation = Quaternion.LookRotation(-bulletDir);

        ps.Play();
        Object.Destroy(ps.gameObject, 1.2f);
    }

    // ---------------- mancha na parede ----------------

    public static void WallSplatter(Vector3 point, Vector3 normal, float size)
    {
        int spots = Random.Range(2, 4);
        for (int i = 0; i < spots; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BloodDecal";
            Object.Destroy(go.GetComponent<Collider>());

            // cubo achatado colado na superficie: visivel de qualquer lado,
            // sem o problema de quad virado pro lado errado
            float s = size * Random.Range(0.4f, 1f);
            Vector3 tangent = Vector3.Cross(normal, Random.onUnitSphere).normalized;
            if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.Cross(normal, Vector3.up).normalized;

            go.transform.position = point + normal * 0.008f + tangent * Random.Range(0f, size * 0.5f);
            go.transform.rotation = Quaternion.LookRotation(normal) *
                                    Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            go.transform.localScale = new Vector3(s, s * Random.Range(0.5f, 1f), 0.004f);

            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = DecalMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            decals.Enqueue(go);
            Object.Destroy(go, 40f);
        }

        while (decals.Count > MaxDecals)
        {
            var velho = decals.Dequeue();
            if (velho != null) Object.Destroy(velho);
        }
    }

    // ---------------- esguicho de pescoco ----------------

    public static void NeckFountain(Transform bone)
    {
        if (bone == null) return;

        var ps = CreateSystem("BloodFountain", bone.position);
        ps.transform.SetParent(bone, true);
        ps.transform.rotation = Quaternion.LookRotation(Vector3.up);

        var main = ps.main;
        main.duration = 3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.08f);
        main.gravityModifier = 1.5f;
        main.startColor = BloodBright;

        var em = ps.emission;
        em.rateOverTime = 70f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) }); // o "pop" inicial

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 0.03f;

        ps.Play();
        Object.Destroy(ps.gameObject, 5f);
    }

    // ---------------- base ----------------

    private static ParticleSystem CreateSystem(string name, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = ParticleMat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return ps;
    }
}
