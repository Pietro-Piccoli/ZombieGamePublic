using UnityEngine;

/// <summary>
/// POSTURA por arma: rotacoes extras somadas POR CIMA da animacao, todo frame,
/// depois do Animator (LateUpdate). Sem IK, sem tocar controller/clipes.
/// Valores vem do WeaponData.poseBracos, editados na janela Ajuste de Arma > BRACOS
/// (botoes ou modo pose livre na cena, com captura).
/// </summary>
public class PoseBracos : MonoBehaviour
{
    /// <summary>Ossos ajustaveis. A ORDEM e o indice no array do WeaponData - so ADICIONAR no fim, nunca reordenar.</summary>
    public static readonly HumanBodyBones[] OSSOS =
    {
        // 0..9: principais (aparecem nos botoes da janela)
        HumanBodyBones.LeftShoulder,  HumanBodyBones.LeftUpperArm,  HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftHand,
        HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        HumanBodyBones.Spine,         HumanBodyBones.Chest,
        // 10..24: dedos esquerdos
        HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
        HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
        HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
        HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
        HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
        // 25..39: dedos direitos
        HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
        HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
        HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
        HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
        HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal
    };
    /// <summary>Nomes dos 10 principais (grade de botoes da janela). Dedos se editam no modo pose livre.</summary>
    public static readonly string[] NOMES =
    {
        "Ombro ESQ", "Braco ESQ", "Antebraco ESQ", "Mao ESQ",
        "Ombro DIR", "Braco DIR", "Antebraco DIR", "Mao DIR",
        "Coluna", "Peito"
    };

    private Animator animator;
    private WeaponVisuals visuals;
    private Transform[] ossos;
    private int idxArma = -1;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        visuals = GetComponentInParent<WeaponVisuals>();
        if (visuals == null) visuals = FindAnyObjectByType<WeaponVisuals>();
        ossos = new Transform[OSSOS.Length];
        if (animator != null && animator.isHuman)
            for (int i = 0; i < OSSOS.Length; i++) ossos[i] = animator.GetBoneTransform(OSSOS[i]);
        if (animator != null)
            for (int i = 0; i < animator.layerCount; i++)
                if (animator.GetLayerName(i) == "Arma") idxArma = i;
    }

    private void LateUpdate()
    {
        WeaponData d = visuals != null ? visuals.CurrentData : null;
        if (d == null || d.poseBracos == null || ossos == null) return;

        // A postura da arma so vale enquanto a CAMADA DE ARMA esta valendo.
        // Ao correr sem mirar essa camada sai, pra a animacao de corrida com
        // rifle tocar o corpo inteiro. Se estes offsets continuassem entrando,
        // eles torceriam os bracos por cima e desmontariam a corrida.
        float peso = 1f;
        if (animator != null && idxArma >= 0) peso = animator.GetLayerWeight(idxArma);
        if (peso <= 0.001f) return;

        int n = Mathf.Min(d.poseBracos.Length, ossos.Length);
        for (int i = 0; i < n; i++)
        {
            if (ossos[i] == null) continue;
            Vector3 r = d.poseBracos[i];
            if (r == Vector3.zero) continue;
            Quaternion extra = Quaternion.Euler(r);
            if (peso < 0.999f) extra = Quaternion.Slerp(Quaternion.identity, extra, peso);
            ossos[i].localRotation = ossos[i].localRotation * extra;
        }
    }
}
