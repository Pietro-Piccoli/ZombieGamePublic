using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Leitor de input que funciona no Input System novo, no antigo, ou nos dois.
/// A escolha e feita em tempo de compilacao, entao nao depende de Project Settings.
///
/// Se o Input System novo estiver disponivel, ele tem prioridade.
/// </summary>
public static class InputReader
{
    /// <summary>Escala pra igualar o delta do mouse novo ao feel do antigo.</summary>
    private const float MouseDeltaScale = 0.1f;

#if ENABLE_INPUT_SYSTEM

    private static Keyboard K => Keyboard.current;
    private static Mouse M => Mouse.current;

    public static float MoveX
    {
        get
        {
            if (K == null) return 0f;
            float v = 0f;
            if (K.aKey.isPressed || K.leftArrowKey.isPressed) v -= 1f;
            if (K.dKey.isPressed || K.rightArrowKey.isPressed) v += 1f;
            return v;
        }
    }

    public static float MoveZ
    {
        get
        {
            if (K == null) return 0f;
            float v = 0f;
            if (K.sKey.isPressed || K.downArrowKey.isPressed) v -= 1f;
            if (K.wKey.isPressed || K.upArrowKey.isPressed) v += 1f;
            return v;
        }
    }

    public static float MouseX => M == null ? 0f : M.delta.ReadValue().x * MouseDeltaScale;
    public static float MouseY => M == null ? 0f : M.delta.ReadValue().y * MouseDeltaScale;

    public static bool Sprint => K != null && K.leftShiftKey.isPressed;
    public static bool FreeLook => K != null && K.leftAltKey.isPressed;

    /// <summary>Agachar: segurar Ctrl esquerdo ou C.</summary>
    public static bool Crouch => K != null && (K.leftCtrlKey.isPressed || K.cKey.isPressed);

    /// <summary>Pulo: espaco, so no quadro em que aperta.</summary>
    public static bool JumpPressed => K != null && K.spaceKey.wasPressedThisFrame;

    public static bool Aim => M != null && M.rightButton.isPressed;
    public static bool Fire => M != null && M.leftButton.isPressed;
    public static bool FirePressed => M != null && M.leftButton.wasPressedThisFrame;
    public static bool ReloadPressed => K != null && K.rKey.wasPressedThisFrame;

    /// <summary>Indice 0-5 da tecla 1-6 apertada neste frame, ou -1.</summary>
    public static int PressedWeaponSlot
    {
        get
        {
            if (K == null) return -1;
            if (K.digit1Key.wasPressedThisFrame) return 0;
            if (K.digit2Key.wasPressedThisFrame) return 1;
            if (K.digit3Key.wasPressedThisFrame) return 2;
            if (K.digit4Key.wasPressedThisFrame) return 3;
            if (K.digit5Key.wasPressedThisFrame) return 4;
            if (K.digit6Key.wasPressedThisFrame) return 5;
            return -1;
        }
    }

    /// <summary>Scroll do mouse: so o sinal importa (+1 sobe, -1 desce).</summary>
    public static float ScrollDelta => M == null ? 0f : M.scroll.ReadValue().y;

#else

    public static float MoveX => Input.GetAxisRaw("Horizontal");
    public static float MoveZ => Input.GetAxisRaw("Vertical");

    public static float MouseX => Input.GetAxisRaw("Mouse X");
    public static float MouseY => Input.GetAxisRaw("Mouse Y");

    public static bool Sprint => Input.GetKey(KeyCode.LeftShift);
    public static bool FreeLook => Input.GetKey(KeyCode.LeftAlt);

    /// <summary>Agachar: segurar Ctrl esquerdo ou C.</summary>
    public static bool Crouch => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);

    /// <summary>Pulo: espaco, so no quadro em que aperta.</summary>
    public static bool JumpPressed => Input.GetKeyDown(KeyCode.Space);

    public static bool Aim => Input.GetMouseButton(1);
    public static bool Fire => Input.GetMouseButton(0);
    public static bool FirePressed => Input.GetMouseButtonDown(0);
    public static bool ReloadPressed => Input.GetKeyDown(KeyCode.R);

    /// <summary>Indice 0-5 da tecla 1-6 apertada neste frame, ou -1.</summary>
    public static int PressedWeaponSlot
    {
        get
        {
            for (int i = 0; i < 6; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return i;
            return -1;
        }
    }

    /// <summary>Scroll do mouse: so o sinal importa (+1 sobe, -1 desce).</summary>
    public static float ScrollDelta => Input.mouseScrollDelta.y;

#endif

    /// <summary>Vetor de movimento ja normalizado (diagonal nao e mais rapida).</summary>
    public static Vector2 Move
    {
        get
        {
            Vector2 v = new Vector2(MoveX, MoveZ);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }
    }

    /// <summary>Qual backend esta ativo - util pra diagnostico.</summary>
    public static string Backend
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return "Input System (novo)";
#else
            return "Input Manager (antigo)";
#endif
        }
    }
}
