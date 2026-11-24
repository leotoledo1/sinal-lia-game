using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Algo que o Marcos usa com a tecla E (porta, mochila, chave, cortina, armário...).
// Chegando perto aparece a dica "[E] ..." (só a do objeto mais perto). O que acontece ao usar fica
// no roteiro (EscolaInterior / ClinicaInterior), que escuta o evento Usado e olha o "id".
public class Interativo : MonoBehaviour
{
    static readonly List<Interativo> todos = new List<Interativo>();

    [Tooltip("Nome usado pelo roteiro (ex.: mochila, chave, porta4B)")]
    public string id;
    [Tooltip("Dica na tela, ex.: [E] Pegar a chave")]
    public string texto = "[E] Usar";
    public float raio = 1.8f;
    [Tooltip("Desligado = não aparece nem pode ser usado")]
    public bool ativo = true;
    [Tooltip("Ponto de referência da distância (vazio = este objeto)")]
    public Transform ponto;

    // qualquer Interativo usado (o roteiro filtra pelo id)
    public static event System.Action<Interativo> Usado;
    // o mais perto do Marcos neste quadro (dica na tela)
    public static Interativo Perto { get; private set; }
    public static int QuadroUsado { get; private set; } = -10;

    public Vector3 Posicao => ponto != null ? ponto.position : transform.position;

    void OnEnable() { if (!todos.Contains(this)) todos.Add(this); }
    void OnDisable() { todos.Remove(this); if (Perto == this) Perto = null; }

    public void Usar()
    {
        if (!ativo) return;
        QuadroUsado = Time.frameCount;
        Usado?.Invoke(this);
    }

    // chamado pelo InteracaoJogador: acha o mais perto (distância na horizontal, mesmo andar)
    public static Interativo MaisPerto(Vector3 pos)
    {
        Interativo melhor = null;
        float melhorD = float.MaxValue;
        foreach (var i in todos)
        {
            if (i == null || !i.ativo || !i.isActiveAndEnabled) continue;
            Vector3 d = i.Posicao - pos;
            if (Mathf.Abs(d.y) > 2.2f) continue;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist <= i.raio && dist < melhorD)
            {
                melhorD = dist;
                melhor = i;
            }
        }
        return melhor;
    }

    internal static void DefinirPerto(Interativo i) => Perto = i;
}
