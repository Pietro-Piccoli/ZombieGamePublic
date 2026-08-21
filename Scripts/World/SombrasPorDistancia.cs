using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SOMBRA DE POSTE SO PERTO DO JOGADOR.
///
/// A favela tem 18 refletores com sombra em tempo real. Medido em jogo com
/// 105 zumbis, desligar todos economizava 0,50 ms por quadro, 423 draw calls
/// e 555 shadow casters - mas apagava a noite, que e metade do clima.
///
/// Entao em vez de escolher entre bonito e rapido: so os postes MAIS PERTO
/// projetam sombra. O jogador nunca ve a diferenca, porque a sombra que
/// importa e a que esta ao lado dele; poste do outro lado do mapa gasta
/// atlas de sombra pra nada.
///
/// O limite acompanha o maxAdditionalLightsCount do URP (4 por padrao):
/// manter mais luzes com sombra que isso e desperdicio garantido.
/// </summary>
[DefaultExecutionOrder(-50)]
public class SombrasPorDistancia : MonoBehaviour
{
    [Tooltip("Quantos postes podem ter sombra ao mesmo tempo.")]
    [SerializeField] private int quantos = 4;
    [Tooltip("Alem desta distancia nenhum poste projeta sombra.")]
    [SerializeField] private float alcance = 45f;
    [Tooltip("De quanto em quanto tempo reavalia. Nao precisa ser todo quadro.")]
    [SerializeField] private float intervalo = 0.3f;

    private readonly List<Light> candidatos = new List<Light>(32);
    private readonly List<Light> ligadas = new List<Light>(8);
    private Transform jogador;
    private float proxima;

    /// <summary>Se cria sozinho quando a cena abre - nada pra arrastar na mao.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Instalar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AoCarregar;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AoCarregar;
        Criar();
    }

    private static void AoCarregar(UnityEngine.SceneManagement.Scene c, UnityEngine.SceneManagement.LoadSceneMode m) { Criar(); }

    private static void Criar()
    {
        if (Object.FindAnyObjectByType<SombrasPorDistancia>() != null) return;
        var go = new GameObject("SombrasPorDistancia");
        go.AddComponent<SombrasPorDistancia>();
    }

    private void Start()
    {
        var pl = GameObject.FindGameObjectWithTag("Player");
        if (pl == null) pl = GameObject.Find("Player");
        if (pl != null) jogador = pl.transform;

        // captura quem JA nascia com sombra: postes. A direcional do sol nao
        // entra - ela e uma so e e a que da a forma geral da cena.
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) continue;
            if (l.shadows == LightShadows.None) continue;
            candidatos.Add(l);
            l.shadows = LightShadows.None;   // todos comecam sem; o Update acende os certos
        }
    }

    private void Update()
    {
        if (jogador == null || candidatos.Count == 0) return;
        if (Time.time < proxima) return;
        proxima = Time.time + intervalo;

        Vector3 p = jogador.position;
        float lim = alcance * alcance;

        // apaga as de antes
        for (int i = 0; i < ligadas.Count; i++)
            if (ligadas[i] != null) ligadas[i].shadows = LightShadows.None;
        ligadas.Clear();

        // seleciona as N mais proximas por insercao direta - com 18 candidatos
        // isso e mais barato que ordenar a lista toda, e nao aloca nada.
        for (int i = 0; i < candidatos.Count; i++)
        {
            var l = candidatos[i];
            if (l == null || !l.isActiveAndEnabled) continue;
            float d = (l.transform.position - p).sqrMagnitude;
            if (d > lim) continue;

            int onde = ligadas.Count;
            while (onde > 0 && Dist(ligadas[onde - 1], p) > d) onde--;
            if (onde >= quantos) continue;
            ligadas.Insert(onde, l);
            if (ligadas.Count > quantos) ligadas.RemoveAt(ligadas.Count - 1);
        }

        for (int i = 0; i < ligadas.Count; i++)
            if (ligadas[i] != null) ligadas[i].shadows = LightShadows.Soft;
    }

    private static float Dist(Light l, Vector3 p)
    {
        return l == null ? float.MaxValue : (l.transform.position - p).sqrMagnitude;
    }

    /// <summary>Quantos estao projetando sombra agora. Usado pra conferir em teste.</summary>
    public int Acesas { get { return ligadas.Count; } }
    public int Candidatos { get { return candidatos.Count; } }
}
