using UnityEngine;

/// <summary>
/// Zumbi morreu -> dropa orbes de dinheiro e XP.
/// Vai no mesmo objeto do Health do zumbi. Tudo configuravel.
/// </summary>
[RequireComponent(typeof(Health))]
public class DropOnDeath : MonoBehaviour
{
    [Header("Dinheiro")]
    [SerializeField] private int dinheiroMin = 3;
    [SerializeField] private int dinheiroMax = 8;
    [Tooltip("Quantos orbes dourados (o valor e dividido entre eles).")]
    [SerializeField] private int orbesDinheiro = 2;

    [Header("XP")]
    [SerializeField] private int xpMin = 8;
    [SerializeField] private int xpMax = 14;
    [SerializeField] private int orbesXp = 3;

    [Header("Chance")]
    [Range(0f, 1f)]
    [Tooltip("1 = todo zumbi dropa dinheiro. Baixe pra ficar raro.")]
    [SerializeField] private float chanceDinheiro = 1f;

    [Header("Kit de primeiros socorros")]
    [Range(0f, 1f)]
    [Tooltip("Chance de o zumbi largar um kit. 0,04 = 1 a cada 25 mortes.")]
    [SerializeField] private float chanceKit = 0.04f;
    [Tooltip("Quanto da vida MAXIMA o kit devolve, em %. A cura entra aos poucos, nao de uma vez.")]
    [SerializeField] private float curaDoKit = 30f;
    [Tooltip("Ligado, o kit so cai se o jogador estiver machucado - nao desperdica drop com a barra cheia. Desligado, cai sempre pela chance.")]
    [SerializeField] private bool soSeFerido = false;
    [Tooltip("Com 'so se ferido' ligado, quanto de vida o jogador precisa ter perdido pro kit poder cair.")]
    [Range(0f, 1f)]
    [SerializeField] private float vidaMaximaPraDropar = 0.9f;

    [Header("Chao (pro orbe nao atravessar laje)")]
    [Tooltip("Camadas que contam como chao. NAO marque Player, Enemy nem Hitbox.")]
    [SerializeField] private LayerMask chaoMask = ~0;

    [Header("Duracao")]
    [Tooltip("Segundos ate o orbe sumir. 0 = FICA PRA SEMPRE.")]
    [SerializeField] private float duracaoOrbe = 0f;
    [Tooltip("Teto de orbes no mundo. Passando disso o mais velho e coletado sozinho (nao se perde valor).")]
    [SerializeField] private int tetoDeOrbes = 250;

    private Health health;
    private bool dropou;

    private void Awake()
    {
        health = GetComponent<Health>();
        health.OnDeath += Dropar;

        // se ninguem configurou, monta uma mascara segura: tudo menos o que se mexe
        if (chaoMask == ~0)
        {
            int excluir = 0;
            foreach (string n in new string[] { "Player", "Enemy", "Hitbox", "Ignore Raycast" })
            {
                int l = LayerMask.NameToLayer(n);
                if (l >= 0) excluir |= 1 << l;
            }
            chaoMask = ~excluir;
        }
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDeath -= Dropar;
    }

    private float multRecompensa = 1f;

    /// <summary>O director chama isto no spawn pra recompensa acompanhar a dificuldade.</summary>
    public void EscalarRecompensa(float mult)
    {
        multRecompensa = Mathf.Max(0.01f, mult);
    }

    private void Dropar()
    {
        if (dropou) return;
        dropou = true;

        Vector3 pos = transform.position + Vector3.up * 1.1f;

        if (Random.value <= chanceDinheiro)
        {
            int total = Mathf.RoundToInt(Random.Range(dinheiroMin, dinheiroMax + 1) * multRecompensa);
            int n = Mathf.Max(1, orbesDinheiro);
            for (int i = 0; i < n; i++)
                Pickup.Spawn(Pickup.Tipo.Dinheiro, Mathf.Max(1, total / n), pos, chaoMask, duracaoOrbe, tetoDeOrbes);
        }

        int xp = Mathf.RoundToInt(Random.Range(xpMin, xpMax + 1) * multRecompensa);
        int nx = Mathf.Max(1, orbesXp);
        for (int i = 0; i < nx; i++)
            Pickup.Spawn(Pickup.Tipo.Xp, Mathf.Max(1, xp / nx), pos, chaoMask, duracaoOrbe, tetoDeOrbes);

        // KIT DE PRIMEIROS SOCORROS
        // Raro de proposito: em Left 4 Dead e Killing Floor a vida no chao e o
        // que faz a horda pesar - se cair toda hora, tomar dano deixa de custar.
        if (Random.value <= chanceKit && PodeLargarKit())
        {
            var kit = Pickup.Spawn(Pickup.Tipo.Cura, 0, pos, chaoMask, duracaoOrbe, tetoDeOrbes);
            if (kit != null) kit.percentualDeCura = curaDoKit;
        }
    }

    /// <summary>
    /// Com 'so se ferido' desligado (padrao) o kit cai so pela chance.
    /// Ligado, olha a vida do jogador antes - e o que Doom Eternal e Halo fazem
    /// pra o drop nao ser jogado fora com a barra cheia.
    /// </summary>
    private bool PodeLargarKit()
    {
        if (!soSeFerido) return true;
        var alvo = GameObject.FindGameObjectWithTag("Player");
        if (alvo == null) alvo = GameObject.Find("Player");
        if (alvo == null) return true;
        var h = alvo.GetComponent<Health>();
        return h == null || h.Percent <= vidaMaximaPraDropar;
    }
}
