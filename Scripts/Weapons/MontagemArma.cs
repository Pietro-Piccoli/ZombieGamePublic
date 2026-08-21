using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MONTAGEM DA ARMA EM JOGO.
///
/// Pega um PresetArma e instala as pecas no modelo da arma equipada. Como as
/// pecas viram filhas do modelo, elas acompanham a arma em tudo: recuo, mira,
/// recarga, o que for.
///
/// Se a peca de cano for um SUPRESSOR, o ponto de saida da bala (muzzle) e
/// empurrado pra ponta dele automaticamente - senao o rastro sairia de dentro
/// do supressor.
///
/// Trocar de preset em runtime (upgrade pego no jogo) e so chamar Aplicar().
/// </summary>
public class MontagemArma : MonoBehaviour
{
    [Tooltip("Preset usado ao equipar a arma. Vazio = arma limpa.")]
    [SerializeField] private PresetArma presetInicial;

    private WeaponVisuals visuais;
    private readonly List<GameObject> instaladas = new List<GameObject>();
    private PresetArma atual;
    private GameObject modeloAnterior;

    public PresetArma PresetAtual { get { return atual; } }

    private void Awake()
    {
        visuais = GetComponent<WeaponVisuals>();
        atual = presetInicial;
    }

    private void LateUpdate()
    {
        // arma trocada / reequipada -> remonta
        GameObject modelo = visuais != null ? visuais.CurrentModel : null;
        if (modelo != modeloAnterior)
        {
            modeloAnterior = modelo;
            Remontar();
        }
    }

    /// <summary>Troca o preset em runtime (e o que o upgrade vai chamar).</summary>
    public void Aplicar(PresetArma preset)
    {
        atual = preset;
        Remontar();
    }

    /// <summary>Chame depois de mexer na armaria pra a arma refletir na hora.</summary>
    public void Recarregar() { Remontar(); }

    public void Limpar()
    {
        for (int i = 0; i < instaladas.Count; i++)
            if (instaladas[i] != null) Destroy(instaladas[i]);
        instaladas.Clear();
    }

    private void Remontar()
    {
        Limpar();

        if (visuais == null || visuais.CurrentModel == null) return;
        Transform modelo = visuais.CurrentModel.transform;

        // ---- fonte da verdade: o que o jogador equipou na armaria ----
        // (o preset antigo continua funcionando como fallback, mas o normal
        //  agora e comecar limpo e montar peca por peca)
        multEspalhamento = 1f; multCadencia = 1f; multDano = 1f; multRecuo = 1f;

        // A montagem e POR ARMA: cada arma guarda a propria configuracao, e
        // uma peca so entra se ela serve nessa arma e se a arma tem o slot.
        WeaponData arma = visuais.CurrentData;
        string idArma = arma != null ? arma.Id : "";

        for (int s = 0; s < 6; s++)
        {
            SlotAttach slot = (SlotAttach)s;
            if (arma != null && !arma.AceitaSlot(slot)) continue;
            AnexoArma a = MetaProgressao.ResolverEquipado(idArma, slot);
            if (a == null || a.prefab == null) continue;
            if (!a.ServePara(idArma)) continue;
            Instalar(a.prefab, a.slot, a.PosicaoNa(arma), a.RotacaoFinal, a.EscalaFinal, modelo);
            multEspalhamento *= a.multEspalhamento;
            multCadencia     *= a.multCadencia;
            multDano         *= a.multDano;
            multRecuo        *= a.multRecuo;
        }

        // fallback: se nao tem NADA equipado e existe um preset legado, usa ele
        if (instaladas.Count == 0 && atual != null && atual.pecas != null)
        {
            foreach (var pc in atual.pecas)
            {
                if (pc == null || pc.prefab == null) continue;
                Instalar(pc.prefab, pc.slot, pc.posicao, pc.rotacao, Mathf.Max(0.01f, pc.escala), modelo);
            }
            multEspalhamento = atual.multEspalhamento;
            multCadencia = atual.multCadencia;
            multDano = atual.multDano;
        }
    }

    /// <summary>Multiplicadores vindos das pecas montadas. O tiro consulta aqui.</summary>
    public float MultEspalhamento { get { return multEspalhamento; } }
    public float MultCadencia { get { return multCadencia; } }
    public float MultDano { get { return multDano; } }
    public float MultRecuo { get { return multRecuo; } }

    private float multEspalhamento = 1f, multCadencia = 1f, multDano = 1f, multRecuo = 1f;

    private void Instalar(GameObject prefab, SlotAttach slot, Vector3 pos, Vector3 rot, float esc, Transform modelo)
    {
        var go = Instantiate(prefab, modelo);
        go.name = "Attach_" + slot + "_" + prefab.name;
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = rot;
        go.transform.localScale = Vector3.one * esc;

        foreach (var tt in go.GetComponentsInChildren<Transform>(true))
            tt.gameObject.layer = modelo.gameObject.layer;
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            Destroy(c);

        instaladas.Add(go);
        if (slot == SlotAttach.Cano) AjustarMuzzle(go, modelo);
    }

    /// <summary>Leva o Muzzle pra ponta da peca de cano instalada.</summary>
    private void AjustarMuzzle(GameObject peca, Transform modelo)
    {
        Transform muzzle = null;
        foreach (Transform t in modelo.GetComponentsInChildren<Transform>(true))
            if (t.name == "Muzzle") { muzzle = t; break; }
        if (muzzle == null) return;

        var filtros = peca.GetComponentsInChildren<MeshFilter>();
        if (filtros.Length == 0) return;

        // Mede a peca no espaco LOCAL DO MODELO da arma. Nao da pra usar
        // Renderer.bounds (AABB do mundo): o modelo esta rotacionado na mao, e
        // a caixa do mundo fica maior que a peca, jogando o muzzle longe demais.
        float maiorZ = float.NegativeInfinity;
        float somaX = 0f, somaY = 0f; int n = 0;

        foreach (var mf in filtros)
        {
            if (mf.sharedMesh == null) continue;
            Bounds lb = mf.sharedMesh.bounds;
            Vector3 c = lb.center; Vector3 e = lb.extents;

            for (int i = 0; i < 8; i++)
            {
                Vector3 canto = c + new Vector3(
                    ((i & 1) == 0 ? -e.x : e.x),
                    ((i & 2) == 0 ? -e.y : e.y),
                    ((i & 4) == 0 ? -e.z : e.z));

                Vector3 p = modelo.InverseTransformPoint(mf.transform.TransformPoint(canto));
                if (p.z > maiorZ) maiorZ = p.z;
                somaX += p.x; somaY += p.y; n++;
            }
        }
        if (n == 0) return;

        // eixo do cano da peca, na ponta dela
        muzzle.localPosition = new Vector3(somaX / n, somaY / n, maiorZ);
    }
}
