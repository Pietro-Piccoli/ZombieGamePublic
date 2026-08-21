using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sonda de performance: grava o tempo de cada frame num anel de 600 amostras
/// (~10s). Nasce sozinha em play mode, nao aparece no jogo, custo ~zero.
/// A leitura sai em milissegundos - FPS mente, frame time nao.
/// </summary>
public class FrameProbe : MonoBehaviour
{
    private static FrameProbe instancia;
    private readonly List<float> amostras = new List<float>(1024);
    private const int MAX = 600;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Nascer()
    {
        if (instancia != null) return;
        var go = new GameObject("_FrameProbe");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        instancia = go.AddComponent<FrameProbe>();
    }

    private readonly List<float> gpuMs = new List<float>(1024);
    private readonly FrameTiming[] timings = new FrameTiming[1];

    private void Update()
    {
        amostras.Add(Time.unscaledDeltaTime * 1000f);
        if (amostras.Count > MAX) amostras.RemoveAt(0);

        FrameTimingManager.CaptureFrameTimings();
        if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
        {
            gpuMs.Add((float)timings[0].gpuFrameTime);
            if (gpuMs.Count > MAX) gpuMs.RemoveAt(0);
        }
    }

    public static string RelatorioGpu()
    {
        if (instancia == null || instancia.gpuMs.Count < 30) return "(sem timings de GPU)";
        var s = new List<float>(instancia.gpuMs);
        s.Sort();
        return string.Format("GPU: p50={0:0.0}ms p95={1:0.0}ms pior={2:0.0}ms",
            s[s.Count/2], s[(int)(s.Count*0.95f)], s[s.Count-1]);
    }

    /// <summary>Zera as amostras (chame ao trocar de monitor).</summary>
    public static void Zerar()
    {
        if (instancia != null) instancia.amostras.Clear();
    }

    /// <summary>media / p50 / p95 / pior, em ms, das ultimas ~10s.</summary>
    public static string Relatorio()
    {
        if (instancia == null || instancia.amostras.Count < 30) return "poucas amostras ainda";
        var s = new List<float>(instancia.amostras);
        s.Sort();
        float media = 0f;
        for (int i = 0; i < s.Count; i++) media += s[i];
        media /= s.Count;
        float p50 = s[s.Count / 2];
        float p95 = s[(int)(s.Count * 0.95f)];
        float pior = s[s.Count - 1];
        int acima33 = 0;
        for (int i = 0; i < s.Count; i++) if (s[i] > 33f) acima33++;
        return string.Format(
            "amostras={0} | media={1:0.0}ms ({2:0} fps) | p50={3:0.0}ms | p95={4:0.0}ms | pior={5:0.0}ms | frames acima de 33ms: {6}",
            s.Count, media, 1000f / media, p50, p95, pior, acima33);
    }
}
