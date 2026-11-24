using System.Collections.Generic;
using UnityEngine;

// Luz do cenário (poste, fogo) controlada pelo DayNightController. Pode piscar (fogo / poste com defeito).
[RequireComponent(typeof(Light))]
public class LuzNoturna : MonoBehaviour
{
    static readonly List<LuzNoturna> todas = new List<LuzNoturna>();
    static bool noiteAtual;

    public float intensidadeNoite = 2.5f;
    [Tooltip("0 = apagada durante o dia")]
    public float intensidadeDia = 0f;
    [Tooltip("0 = luz firme; fogo ~0.35; poste com defeito ~1")]
    public float piscar = 0f;
    public bool defeito; // poste que apaga e acende do nada

    Light luz;
    float semente;

    void Awake()
    {
        luz = GetComponent<Light>();
        semente = Random.value * 100f;
    }

    void OnEnable() { todas.Add(this); Aplicar(noiteAtual); }
    void OnDisable() => todas.Remove(this);

    public static void AplicarTodas(bool noite)
    {
        noiteAtual = noite;
        foreach (var l in todas) l.Aplicar(noite);
    }

    float Base => noiteAtual ? intensidadeNoite : intensidadeDia;

    void Aplicar(bool noite)
    {
        if (luz == null) luz = GetComponent<Light>();
        luz.enabled = (noite ? intensidadeNoite : intensidadeDia) > 0f;
        luz.intensity = noite ? intensidadeNoite : intensidadeDia;
    }

    void Update()
    {
        if (!luz.enabled || (piscar <= 0f && !defeito)) return;
        float t = Time.time + semente;
        float k = 1f;
        if (piscar > 0f) k -= piscar * Mathf.PerlinNoise(t * 9f, semente);
        if (defeito && Mathf.PerlinNoise(t * 1.7f, semente * 2f) > 0.62f) k *= 0.05f;
        luz.intensity = Base * k;
    }
}
