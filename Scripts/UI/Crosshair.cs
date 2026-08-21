using UnityEngine;

/// <summary>
/// Mira desenhada em codigo (OnGUI) - placeholder, zero setup de Canvas.
/// Na Etapa 8 isso vira um sprite de verdade.
///
/// A mira ABRE conforme o espalhamento da arma: quando voce mira com o
/// botao direito, ela fecha. Isso comunica precisao sem precisar de texto.
/// </summary>
public class Crosshair : MonoBehaviour
{
    [SerializeField] private WeaponController weapon;
    [SerializeField] private float baseGap = 6f;
    [SerializeField] private float gapPerDegree = 9f;
    [SerializeField] private float length = 9f;
    [SerializeField] private float thickness = 2f;
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.85f);

    private Texture2D pixel;
    private float smoothGap;

    private void Awake()
    {
        pixel = new Texture2D(1, 1);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();

        if (weapon == null) weapon = FindAnyObjectByType<WeaponController>();
    }

    private void Update()
    {
        float spread = weapon != null ? weapon.CurrentSpread : 0f;
        float goal = baseGap + spread * gapPerDegree;
        smoothGap = Mathf.Lerp(smoothGap, goal, 12f * Time.deltaTime);
    }

    private void OnGUI()
    {
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        float g = smoothGap;

        Color old = GUI.color;
        GUI.color = color;

        // esquerda / direita
        GUI.DrawTexture(new Rect(cx - g - length, cy - thickness * 0.5f, length, thickness), pixel);
        GUI.DrawTexture(new Rect(cx + g, cy - thickness * 0.5f, length, thickness), pixel);
        // cima / baixo
        GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy - g - length, thickness, length), pixel);
        GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy + g, thickness, length), pixel);
        // ponto central
        GUI.DrawTexture(new Rect(cx - 1f, cy - 1f, 2f, 2f), pixel);

        GUI.color = old;
    }
}
