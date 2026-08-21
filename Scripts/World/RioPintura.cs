#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Pinta os terrenos do Rio por regra geografica:
/// rocha nos paredoes, floresta profunda nos morros, grama clara na
/// cidade, areia nas praias e no fundo raso do mar.
/// Bordas organicas: os limiares ganham ruido espacial.
/// Camadas: 0=Areia 1=Grama 2=Floresta 3=Rocha
/// </summary>
public static class RioPintura
{
    static float S(float t) { return Mathf.Clamp01(t) * Mathf.Clamp01(t) * (3f - 2f * Mathf.Clamp01(t)); }

    public static void PintarTile(Terrain ter, TerrainLayer[] camadas)
    {
        var td = ter.terrainData;
        td.terrainLayers = camadas;
        if (td.alphamapResolution != 512) td.alphamapResolution = 512;
        int R = td.alphamapResolution;
        Vector3 pos = ter.transform.position, tam = td.size;
        float[,,] a = new float[R, R, 4];

        for (int j = 0; j < R; j++)
        {
            float v = j / (float)(R - 1);
            for (int i = 0; i < R; i++)
            {
                float u = i / (float)(R - 1);
                float h = pos.y + td.GetInterpolatedHeight(u, v);
                float s = td.GetSteepness(u, v);
                float wx = pos.x + u * tam.x, wz = pos.z + v * tam.z;

                // ruidos para borda organica (fases diferentes)
                float j1 = Mathf.PerlinNoise(wx * 0.011f + 3.7f, wz * 0.011f + 9.2f);
                float j2 = Mathf.PerlinNoise(wx * 0.0045f + 71f, wz * 0.0045f + 23f);
                float j3 = Mathf.PerlinNoise(wx * 0.021f + 133f, wz * 0.021f + 57f);

                // ROCHA: paredoes ingremes (limiar 30..42 graus conforme ruido)
                float rocha = S((s - (30f + 12f * j1)) / 9f);
                // pico pelado acima de ~250 m tambem vira pedra
                rocha = Mathf.Max(rocha, S((h - (250f + 60f * j2)) / 60f) * S((s - 18f) / 10f));

                // FLORESTA: morro acima de ~15..35 m, onde nao e rocha
                float flo = S((h - (14f + 22f * j2)) / 26f) * (1f - rocha);

                // AREIA: baixo e plano (praia) + fundo raso do mar
                float areia = S(((5.5f + 3f * (j3 - 0.5f)) - h) / 3.5f) * S((17f - s) / 9f) * (1f - rocha);
                if (h < -1.5f) areia = Mathf.Max(areia, (1f - rocha) * S((14f - s) / 8f));

                float grama = Mathf.Max(0f, 1f - rocha - flo - areia);
                float soma = rocha + flo + areia + grama;
                a[j, i, 0] = areia / soma;
                a[j, i, 1] = grama / soma;
                a[j, i, 2] = flo / soma;
                a[j, i, 3] = rocha / soma;
            }
        }
        td.SetAlphamaps(0, 0, a);
        EditorUtility.SetDirty(td);
    }

    public static string PintarFaixa(int iz)
    {
        var camadas = new TerrainLayer[] {
            AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Art/Textures/Terreno/L_Areia.terrainlayer"),
            AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Art/Textures/Terreno/L_Grama.terrainlayer"),
            AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Art/Textures/Terreno/L_Floresta.terrainlayer"),
            AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Art/Textures/Terreno/L_Rocha.terrainlayer") };
        foreach (var c in camadas) if (c == null) return "camada nula!";
        int n = 0;
        var raiz = GameObject.Find("RIO");
        if (raiz == null) return "sem RIO";
        foreach (Transform f in raiz.transform)
        {
            var ter = f.GetComponent<Terrain>();
            if (ter == null) continue;
            if (!f.name.EndsWith("_" + iz)) continue;
            PintarTile(ter, camadas);
            n++;
        }
        AssetDatabase.SaveAssets();
        return "faixa z=" + iz + ": " + n + " tiles pintados";
    }
}
#endif
