using System.Collections;
using UnityEngine;

// Móvel que o Marcos empurra (armário na frente do esconderijo da Lia): desliza devagar, arrastando.
public class Deslizar : MonoBehaviour
{
    [Tooltip("Quanto anda (no espaço do pai)")]
    public Vector3 deslocamento = new Vector3(-1.3f, 0f, 0f);
    public float duracao = 1.4f;
    public AudioClip som;
    public bool Movido { get; private set; }
    public bool Movendo { get; private set; }

    public void Empurrar()
    {
        if (Movido || Movendo) return;
        StartCoroutine(Mover());
    }

    IEnumerator Mover()
    {
        Movendo = true;
        Vector3 ini = transform.localPosition, fim = ini + deslocamento;
        float proxSom = 0f;
        for (float t = 0f; t < duracao; t += Time.deltaTime)
        {
            // arranca, desliza e para (com uns trancos de móvel pesado)
            float k = Mathf.SmoothStep(0f, 1f, t / duracao);
            k += Mathf.Sin(t * 22f) * 0.008f * (1f - k);
            transform.localPosition = Vector3.Lerp(ini, fim, Mathf.Clamp01(k));
            if (t >= proxSom && som != null && SomManager.Instance != null)
            {
                proxSom = t + 0.35f;
                SomManager.Instance.Tocar(som, transform.position + Vector3.up, 0.6f, 0.55f, 0.1f, 16f);
            }
            yield return null;
        }
        transform.localPosition = fim;
        Movendo = false;
        Movido = true;
    }
}
