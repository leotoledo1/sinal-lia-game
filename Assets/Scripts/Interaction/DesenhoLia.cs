using UnityEngine;
using UnityEngine.InputSystem;

// Desenho da Lia (Dia 1). fica na carteira dela, na sala do 4ºB (dentro da escola).
// Chegando perto aparece "[E] Pegar o desenho" (Interativo); ao pegar, o desenho é mostrado na tela (HUD).
public class DesenhoLia : MonoBehaviour
{
    public float raio = 2.4f;
    public bool Pego { get; private set; }

    Interativo interativo;

    void Awake()
    {
        MontarVisual();
        interativo = GetComponent<Interativo>();
        if (interativo == null) interativo = gameObject.AddComponent<Interativo>();
        interativo.id = "desenho";
        interativo.texto = "[E] Pegar o desenho da Lia";
        interativo.raio = raio;
    }

    void OnEnable() => Interativo.Usado += AoUsar;
    void OnDisable() => Interativo.Usado -= AoUsar;

    void AoUsar(Interativo i)
    {
        var gm = GameManager.Instance;
        if (i != interativo || Pego || gm == null || gm.FaseAtual != Fase.Dia1) return;
        Pegar();
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (interativo != null) interativo.ativo = !Pego && gm != null && gm.FaseAtual == Fase.Dia1;
    }

    public void Pegar()
    {
        if (Pego) return;
        Pego = true;
        if (SomManager.Instance != null) SomManager.Instance.Tocar2D(SomManager.Instance.pegarItem, 0.6f);
        if (GameManager.Instance != null) GameManager.Instance.AbrirDesenho();
        gameObject.SetActive(false);
    }

    // folha de papel no chão com rabiscos de giz de cera + luz fraca pra chamar atenção
    void MontarVisual()
    {
        var papel = Peca(new Vector3(0f, 0.012f, 0f), new Vector3(0.6f, 0.006f, 0.42f), new Color(0.97f, 0.95f, 0.86f));
        papel.transform.localRotation = Quaternion.Euler(0f, 17f, 0f);
        var giz = new[] { new Color(0.15f, 0.25f, 0.75f), new Color(0.9f, 0.3f, 0.6f), new Color(1f, 0.75f, 0.1f), new Color(0.2f, 0.6f, 0.25f) };
        // bonequinhos: pai (alto) e filha (baixa), sol e chão
        Risco(papel.transform, new Vector3(-0.12f, 0f, 0.02f), new Vector3(0.02f, 1f, 0.45f), giz[0]);
        Risco(papel.transform, new Vector3(0.1f, 0f, -0.06f), new Vector3(0.02f, 1f, 0.3f), giz[1]);
        Risco(papel.transform, new Vector3(-0.35f, 0f, 0.3f), new Vector3(0.12f, 1f, 0.15f), giz[2]);
        Risco(papel.transform, new Vector3(0f, 0f, -0.38f), new Vector3(0.9f, 1f, 0.03f), giz[3]);
        Risco(papel.transform, new Vector3(-0.01f, 0f, 0.0f), new Vector3(0.22f, 1f, 0.02f), giz[0]);

        var luz = new GameObject("LuzDesenho").AddComponent<Light>();
        luz.transform.SetParent(transform, false);
        luz.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        luz.type = LightType.Point;
        luz.range = 3f;
        luz.intensity = 1.5f;
        luz.color = new Color(1f, 0.9f, 0.6f);
        luz.shadows = LightShadows.None;
    }

    GameObject Peca(Vector3 pos, Vector3 escala, Color cor)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.name = "Papel";
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = escala;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = MaterialBase.Cor(cor);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    // risco em cima do papel (posição/escala relativas ao papel)
    static void Risco(Transform papel, Vector3 pos, Vector3 escala, Color cor)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(papel, false);
        go.transform.localPosition = pos + Vector3.up * 0.6f;
        go.transform.localScale = new Vector3(escala.x, 0.3f, escala.z);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = MaterialBase.Cor(cor);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
