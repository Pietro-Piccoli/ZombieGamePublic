using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LISTA VIVA DE INIMIGOS.
///
/// Antes disso, toda carta de area (aura de fogo, espinhos, corrente,
/// explosao ao matar) achava os inimigos com FindObjectsByType&lt;Health&gt;.
/// Medido em jogo com 95 Health na cena: 0,141 ms POR CHAMADA, e cada
/// chamada ainda alocava um vetor novo de 95 posicoes. A aura sozinha
/// chama 2x por segundo; espinhos a cada dano recebido; corrente a cada
/// tiro que encadeia. Com tres cartas dessas ligadas, isso virava lixo
/// de memoria a cada quadro - o coletor rodava no meio da partida e
/// cada coleta e um engasgo.
///
/// Aqui o zumbi se cadastra ao nascer e sai ao morrer. A busca deixa de
/// existir: as cartas so percorrem quem esta vivo, sem alocar nada.
/// E o mesmo padrao que qualquer jogo de horda usa - manter a lista, nao
/// procurar a lista.
/// </summary>
public static class RegistroInimigos
{
    private static readonly List<ZombieAI> vivos = new List<ZombieAI>(128);

    /// <summary>Somente leitura. NAO guarde referencia: a lista muda sozinha.</summary>
    public static IReadOnlyList<ZombieAI> Vivos { get { return vivos; } }

    public static int Contagem { get { return vivos.Count; } }

    public static void Entrar(ZombieAI z)
    {
        if (z == null || vivos.Contains(z)) return;
        vivos.Add(z);
    }

    public static void Sair(ZombieAI z)
    {
        if (z == null) return;
        vivos.Remove(z);
    }

    /// <summary>
    /// Preenche 'saida' com quem esta dentro do raio e ainda tem vida.
    /// Recebe a lista de fora pra nao alocar nada por chamada - quem chama
    /// reaproveita a mesma lista todo tique.
    /// </summary>
    public static void DentroDoRaio(Vector3 centro, float raio, List<Health> saida, Health ignorar)
    {
        saida.Clear();
        float r2 = raio * raio;
        for (int i = vivos.Count - 1; i >= 0; i--)
        {
            var z = vivos[i];
            if (z == null) { vivos.RemoveAt(i); continue; }   // rede: morreu sem avisar
            var h = z.Vida;
            if (h == null || h == ignorar || h.IsDead) continue;
            if ((z.transform.position - centro).sqrMagnitude > r2) continue;
            saida.Add(h);
        }
    }

    /// <summary>O vivo mais proximo do ponto, dentro do raio. null se nao houver.</summary>
    public static Health MaisProximo(Vector3 ponto, float raio, Health ignorarA, Health ignorarB)
    {
        Health melhor = null;
        float d2 = raio * raio;
        for (int i = vivos.Count - 1; i >= 0; i--)
        {
            var z = vivos[i];
            if (z == null) { vivos.RemoveAt(i); continue; }
            var h = z.Vida;
            if (h == null || h == ignorarA || h == ignorarB || h.IsDead) continue;
            float d = (z.transform.position - ponto).sqrMagnitude;
            if (d < d2) { d2 = d; melhor = h; }
        }
        return melhor;
    }

    /// <summary>Trocar de cena zera tudo - senao ficam referencias mortas.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Zerar() { vivos.Clear(); }
}
