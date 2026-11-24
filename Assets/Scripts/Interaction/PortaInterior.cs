using System.Collections;
using UnityEngine;

// Porta que abre girando na dobradiça (este objeto É a dobradiça; a folha é filha dele).
// Fechada, um NavMeshObstacle (carve) na folha corta o NavMesh: zumbi não atravessa porta trancada.
public class PortaInterior : MonoBehaviour
{
    public float anguloAberta = 100f;
    public float duracao = 0.8f;
    public bool aberta;
    public AudioClip som;

    Quaternion fechada;

    void Awake()
    {
        // a cena já guarda a porta aberta girada: a posição "fechada" é desfazendo esse giro
        fechada = aberta ? transform.localRotation * Quaternion.Euler(0f, -anguloAberta, 0f) : transform.localRotation;
    }

    public void Abrir()
    {
        if (aberta) return;
        aberta = true;
        foreach (var o in GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>()) o.enabled = false;
        if (som != null && SomManager.Instance != null) SomManager.Instance.Tocar(som, transform.position + Vector3.up, 0.9f, 0.8f, 0.05f, 18f);
        StartCoroutine(Girar());
    }

    IEnumerator Girar()
    {
        Quaternion fim = fechada * Quaternion.Euler(0f, anguloAberta, 0f);
        Quaternion ini = transform.localRotation;
        for (float t = 0f; t < duracao; t += Time.deltaTime)
        {
            // abre rápido no começo e freia no fim (empurrão)
            float k = 1f - Mathf.Pow(1f - t / duracao, 3f);
            transform.localRotation = Quaternion.Slerp(ini, fim, k);
            yield return null;
        }
        transform.localRotation = fim;
    }
}
