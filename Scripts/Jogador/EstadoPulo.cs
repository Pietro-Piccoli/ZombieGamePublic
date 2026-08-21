using UnityEngine;
using StarterAssets;

/// <summary>
/// PULO EM 3 FASES (impulso / no ar / aterrissagem) + queda.
///
/// ACHADO NA AUDITORIA: o pulo NAO EXISTIA. O ThirdPersonController le
/// '_input.jump', mas ninguem no projeto chamava JumpInput() - a PonteEntrada so
/// mandava mover, correr, mirar e atirar. O estado 'Pulo' e o parametro
/// 'Pulando' estavam no Animator sem nada pra dispara-los. Ou seja: apertar
/// espaco nao fazia absolutamente nada.
///
/// Aqui o pulo passa a existir de verdade, e usa os tres clipes do pack:
///   fase 1 = W2_Stand_Aim_Jump_Start  (agacha e impulsiona)
///   fase 2 = W2_Stand_Aim_Jump_Air    (loop no ar - serve pra queda tambem)
///   fase 3 = W2_Stand_Aim_Jump_End    (absorve o impacto)
///   fase 0 = no chao, locomocao normal
///
/// A fase e um INT no Animator, nao um trigger: trigger se perde em transicao e
/// deixa o boneco presfo no ar. Int e estado, da pra ler e verificar.
/// </summary>
[DefaultExecutionOrder(-30)]
public class EstadoPulo : MonoBehaviour
{
    [Header("Tempos")]
    [Tooltip("Quanto o impulso (fase 1) segura antes de virar 'no ar', em segundos.")]
    [SerializeField] private float tempoImpulso = 0.12f;
    [Tooltip("Teto de seguranca da fase 1: passou disso, vai pro ar de qualquer jeito.")]
    [SerializeField] private float tetoImpulso = 0.45f;
    [Tooltip("Quanto dura a aterrissagem (fase 3) antes de voltar a andar.")]
    [SerializeField] private float tempoAterrissar = 0.28f;
    [Tooltip("Quanto tempo sem chao ate considerar QUEDA (sem ter pulado).")]
    [SerializeField] private float tempoQueda = 0.18f;

    [Header("Regras")]
    [Tooltip("Agachado nao pula.")]
    [SerializeField] private bool bloquearAgachado = true;

    /// <summary>0 chao / 1 impulso / 2 ar / 3 aterrissando.</summary>
    /// <summary>So pra teste automatizado: pula sem apertar tecla (some sozinho).</summary>
    public static bool debugPular = false;

    public int Fase { get { return fase; } }

    private ThirdPersonController controlador;
    private StarterAssetsInputs entrada;
    private Agachar agachar;
    private Animator animador;
    private AnimacaoJogador anim;

    private int fase;
    private float tFase;
    private float semChao;
    private bool temParametro;

    private static readonly int HFasePulo = Animator.StringToHash("FasePulo");

    private void Awake()
    {
        controlador = GetComponent<ThirdPersonController>();
        entrada = GetComponent<StarterAssetsInputs>();
        agachar = GetComponent<Agachar>();
        anim = GetComponent<AnimacaoJogador>();
        animador = null;
        temParametro = TemParametro("FasePulo");
    }

    /// <summary>
    /// Animator do boneco ATIVO. Guardar no Awake nao serve: este script roda
    /// antes do AnimacaoJogador, e quando o projeto trocou de personagem a
    /// referencia velha apontava pro boneco desativado.
    /// </summary>
    private Animator Animador()
    {
        if (animador != null && animador.gameObject.activeInHierarchy
            && animador.runtimeAnimatorController != null) return animador;

        foreach (var a in GetComponentsInChildren<Animator>())
            if (a.gameObject.activeInHierarchy && a.isHuman && a.runtimeAnimatorController != null)
            {
                animador = a;
                temParametro = TemParametro("FasePulo");
                return a;
            }
        return null;
    }

    private bool TemParametro(string nome)
    {
        if (animador == null || animador.runtimeAnimatorController == null) return false;
        foreach (var p in animador.parameters) if (p.name == nome) return true;
        return false;
    }

    private void Update()
    {
        if (controlador == null || entrada == null) return;

        float dt = Time.deltaTime;
        bool chao = controlador.Grounded;
        semChao = chao ? 0f : semChao + dt;

        switch (fase)
        {
            case 0:
                if ((InputReader.JumpPressed || debugPular) && chao && !(bloquearAgachado && agachar != null && agachar.Agachado))
                {
                    entrada.JumpInput(true);
                    debugPular = false;
                    fase = 1; tFase = 0f;
                }
                else if (semChao > tempoQueda)
                {
                    // caiu de um degrau sem pular: entra direto no loop de ar
                    fase = 2; tFase = 0f;
                }
                break;

            case 1:
                tFase += dt;
                if (tFase > 0.05f) entrada.JumpInput(false);
                if (tFase >= tempoImpulso && !chao) { fase = 2; tFase = 0f; }
                else if (tFase >= tetoImpulso) { fase = chao ? 0 : 2; tFase = 0f; }
                break;

            case 2:
                tFase += dt;
                if (chao) { fase = 3; tFase = 0f; }
                break;

            case 3:
                tFase += dt;
                if (!chao && tFase > 0.08f) { fase = 2; tFase = 0f; }
                else if (tFase >= tempoAterrissar) { fase = 0; tFase = 0f; }
                break;
        }

        var an = Animador();
        if (temParametro && an != null) an.SetInteger(HFasePulo, fase);
    }
}
