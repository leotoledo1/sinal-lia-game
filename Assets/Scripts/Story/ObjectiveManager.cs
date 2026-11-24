using UnityEngine;

// Objetivo atual + alvo opcional (distância ou força do sinal do rádio)
public class ObjectiveManager : MonoBehaviour
{
    public static ObjectiveManager Instance { get; private set; }

    [Tooltip("Distância em que o sinal fica fraco (0 barras)")]
    public float distanciaSinalMax = 160f;

    public string Texto { get; private set; } = "";
    public Transform Alvo { get; private set; }
    public bool ModoSinal { get; private set; }
    // dentro da clínica o sinal vale em escala pequena (quente/frio)
    public float DistanciaSinal { get; private set; }

    void Awake() => Instance = this;

    // distSinal > 0: distância em que o sinal zera (vazio = a do mapa inteiro)
    public void Definir(string texto, Transform alvo, bool modoSinal, float distSinal = 0f)
    {
        Texto = texto;
        Alvo = alvo;
        ModoSinal = modoSinal;
        DistanciaSinal = distSinal > 0f ? distSinal : distanciaSinalMax;
    }

    public float Distancia(Vector3 de) => Alvo == null ? -1f : Vector3.Distance(de, Alvo.position);

    // 0..1 (1 = muito perto)
    public float ForcaSinal(Vector3 de)
    {
        if (Alvo == null) return 0f;
        float max = DistanciaSinal > 0f ? DistanciaSinal : distanciaSinalMax;
        return 1f - Mathf.Clamp01(Distancia(de) / max);
    }
}
