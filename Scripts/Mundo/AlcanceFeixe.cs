using UnityEngine;

/// <summary>
/// Faz o feixe volumetrico do poste PARAR na primeira superficie que a luz
/// encontra, em vez de manter um alcance fixo.
///
/// Sem isto, o cone tem comprimento fixo (9 m). Se o chao esta a 7 m, ou uma
/// parede a 3 m, a nevoa continua existindo do outro lado — e de certos angulos
/// da pra ver o brilho atravessando o piso.
///
/// Usa MaterialPropertyBlock: cada poste tem seu proprio alcance sem duplicar
/// material (continua tudo num batch so).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class AlcanceFeixe : MonoBehaviour
{
    [Tooltip("Alcance maximo, quando a luz nao encontra nada.")]
    [SerializeField] private float alcanceMax = 9f;

    [Tooltip("Folga depois da batida, pra nevoa nao cortar exatamente rente ao chao.")]
    [SerializeField] private float folga = 0.35f;

    [Tooltip("Em que camadas o feixe para. Deixe tudo menos os inimigos/jogador.")]
    [SerializeField] private LayerMask camadas = ~0;

    [Tooltip("De quanto em quanto tempo remede (0 = so uma vez no Start).")]
    [SerializeField] private float intervalo = 0f;

    private static readonly int IdAlcance = Shader.PropertyToID("_Alcance");

    private MeshRenderer rend;
    private MaterialPropertyBlock bloco;
    private float proxima;

    private void OnEnable()
    {
        rend = GetComponent<MeshRenderer>();
        if (bloco == null) bloco = new MaterialPropertyBlock();
        Medir();
    }

    private void Update()
    {
        if (intervalo <= 0f) return;
        if (Time.unscaledTime < proxima) return;
        proxima = Time.unscaledTime + intervalo;
        Medir();
    }

    /// <summary>Raycast na direcao do facho; o alcance vira a distancia da batida.</summary>
    public void Medir()
    {
        if (rend == null) rend = GetComponent<MeshRenderer>();
        if (bloco == null) bloco = new MaterialPropertyBlock();

        // o feixe aponta em +Z local (mesmo eixo da luz, que e o pai)
        Vector3 origem = transform.position;
        Vector3 dir = transform.forward;

        float alcance = alcanceMax;
        RaycastHit hit;
        if (Physics.Raycast(origem, dir, out hit, alcanceMax, camadas, QueryTriggerInteraction.Ignore))
            alcance = Mathf.Min(alcanceMax, hit.distance + folga);

        rend.GetPropertyBlock(bloco);
        bloco.SetFloat(IdAlcance, alcance);
        rend.SetPropertyBlock(bloco);
    }
}
