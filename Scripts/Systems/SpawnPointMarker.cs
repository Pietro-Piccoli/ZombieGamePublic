using UnityEngine;

/// <summary>
/// Marcador visual de ponto de spawn de zumbi. So gizmo de editor;
/// invisivel no jogo. Pode mover/duplicar/apagar a vontade - o
/// SpawnDirector le todos os filhos de SPAWN_POINTS a cada spawn.
/// </summary>
public class SpawnPointMarker : MonoBehaviour
{
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 1f, 1.0f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2.2f);
        UnityEditor.Handles.color = new Color(1f, 0.35f, 0.3f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.6f, "SPAWN");
    }
#endif
}
