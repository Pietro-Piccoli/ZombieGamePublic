using UnityEngine;

/// <summary>
/// Mostra o MODELO da arma equipada num socket na mao do player,
/// e move o Muzzle (ponta do cano) pro lugar certo de cada arma.
///
/// O WeaponController avisa quando troca de slot; aqui so troca o visual.
/// </summary>
public class WeaponVisuals : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Onde a arma fica presa. Se vazio, cria um socket automatico.")]
    [SerializeField] private Transform socket;
    [Tooltip("Transform do Muzzle que o WeaponController usa pro rastro.")]
    [SerializeField] private Transform muzzle;

    [Header("Prender na MAO do personagem")]
    [Tooltip("Liga quando houver um personagem riggado: a arma gruda no osso da mao direita.")]
    [SerializeField] private bool attachToHandBone = true;
    [Tooltip("Ajuste fino em cima do osso da mao.")]
    [SerializeField] private Vector3 handOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 handRotation = new Vector3(0f, 0f, 0f);

    [Header("Socket automatico (se nao houver um)")]
    [SerializeField] private Vector3 socketPosition = new Vector3(0.28f, 1.32f, 0.32f);
    [SerializeField] private Vector3 socketRotation = Vector3.zero;

    [Header("Camada de render")]
    [Tooltip("Layer aplicada ao modelo da arma (deixe em Player pra bala nao acertar a propria arma).")]
    [SerializeField] private string weaponLayer = "Player";

    private GameObject currentModel;
    private WeaponData currentData;

    public Transform Socket => socket;
    /// <summary>Modelo 3D equipado agora (a janela de ajuste usa isto).</summary>
    public GameObject CurrentModel => currentModel;
    /// <summary>Ficha da arma equipada agora.</summary>
    public WeaponData CurrentData => currentData;

    private void Awake()
    {
        // socket de boneco desativado nao vale: resolve de novo no personagem ativo
        if (socket == null || !socket.gameObject.activeInHierarchy) socket = ResolveSocket();

        if (muzzle == null)
        {
            Transform m = transform.Find("Muzzle");
            muzzle = m;
        }
    }

    /// <summary>
    /// Prefere o osso da MAO DIREITA do personagem riggado. Se nao houver
    /// (player ainda e capsula), cai num socket flutuante na frente do corpo.
    /// </summary>
    private Transform ResolveSocket()
    {
        if (attachToHandBone)
        {
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                Transform hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null)
                {
                    Transform existente = hand.Find("WeaponSocket");
                    if (existente != null) return existente;

                    var novo = new GameObject("WeaponSocket");
                    novo.transform.SetParent(hand, false);
                    novo.transform.localPosition = handOffset;
                    novo.transform.localEulerAngles = handRotation;
                    return novo.transform;
                }
            }
        }

        Transform found = transform.Find("WeaponSocket");
        if (found != null) return found;

        var go = new GameObject("WeaponSocket");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = socketPosition;
        go.transform.localEulerAngles = socketRotation;
        return go.transform;
    }

#if UNITY_EDITOR
    /// <summary>
    /// AJUSTE MANUAL DA ARMA NA MAO (roda em play, com o jogo pausado):
    ///   1. Play, equipa a arma, PAUSA
    ///   2. Hierarchy: Player > MOTTA > ... > mixamorig:RightHand > WeaponSocket > WeaponModel_X
    ///   3. Ajeita com os gizmos (W mover / E girar)
    ///   4. Seleciona o PLAYER, botao direito no titulo do WeaponVisuals -> 'Salvar arma na ficha'
    /// O valor vai pro asset e SOBREVIVE ao sair do play.
    /// </summary>
    [ContextMenu("Salvar arma na ficha")]
    private void SalvarAjusteNaFicha()
    {
        if (currentModel == null || currentData == null)
        {
            Debug.LogWarning("[WeaponVisuals] Nenhuma arma equipada pra salvar.");
            return;
        }
        currentData.modelPosition = currentModel.transform.localPosition;
        currentData.modelRotation = currentModel.transform.localEulerAngles;
        currentData.modelScale = currentModel.transform.localScale.x;
        UnityEditor.EditorUtility.SetDirty(currentData);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("[WeaponVisuals] SALVO em " + currentData.name + " | pos " + currentData.modelPosition
                  + " rot " + currentData.modelRotation + " esc " + currentData.modelScale);
    }

    /// <summary>Salva o SOCKET (vale pra todas as armas de uma vez).</summary>
    [ContextMenu("Salvar socket da mao (todas as armas)")]
    private void SalvarSocket()
    {
        if (socket == null) { Debug.LogWarning("[WeaponVisuals] Sem socket."); return; }
        handOffset = socket.localPosition;
        handRotation = socket.localEulerAngles;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[WeaponVisuals] SOCKET salvo | offset " + handOffset + " rot " + handRotation
                  + " -- agora copie o componente (botao direito no titulo > Copy Component) e cole no Player fora do play.");
    }

    [ContextMenu("Desfazer: reaplicar a ficha")]
    private void ReaplicarFicha()
    {
        if (currentModel == null || currentData == null) return;
        currentModel.transform.localPosition = currentData.modelPosition;
        currentModel.transform.localEulerAngles = currentData.modelRotation;
        currentModel.transform.localScale = Vector3.one * Mathf.Max(0.01f, currentData.modelScale);
    }
#endif

    /// <summary>Chamado pelo WeaponController ao equipar/trocar de arma.</summary>
    public void Equip(WeaponData data)
    {
        if (data == currentData) return;
        currentData = data;

        if (currentModel != null) Destroy(currentModel);
        currentModel = null;

        if (data == null || data.modelPrefab == null) return;

        // ordem de Awake nao e garantida: se o socket ainda nao foi resolvido
        // (ou aponta pra boneco desativado), resolve AGORA antes de instanciar.
        if (socket == null || !socket.gameObject.activeInHierarchy) socket = ResolveSocket();
        if (muzzle == null)
        {
            Transform m0 = transform.Find("Muzzle");
            muzzle = m0;
        }
        currentModel = Instantiate(data.modelPrefab, socket);
        currentModel.name = "WeaponModel_" + data.displayName;
        currentModel.transform.localPosition = data.modelPosition;
        currentModel.transform.localEulerAngles = data.modelRotation;
        currentModel.transform.localScale = Vector3.one * Mathf.Max(0.01f, data.modelScale);

        int layer = LayerMask.NameToLayer(weaponLayer);
        if (layer >= 0)
            foreach (Transform t in currentModel.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;

        // colliders do OBJ so atrapalham (a bala nao pode bater na propria arma)
        foreach (Collider c in currentModel.GetComponentsInChildren<Collider>(true))
            Destroy(c);

        // ponta do cano vai pro lugar certo desta arma
        if (muzzle != null)
        {
            muzzle.SetParent(currentModel.transform, false);
            muzzle.localPosition = data.muzzleOffset;
            muzzle.localRotation = Quaternion.identity;
        }
    }
}
