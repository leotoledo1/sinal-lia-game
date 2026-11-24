using UnityEngine;

// Helicóptero de resgate. Aparece no fim da noite final, voa até a clínica e fica pairando
// com o holofote apontado pro chão (hélice girando + som do rotor).
// Usa o modelo 3D (prefab "Helicoptero_Modelo", filho "Helice" gira); sem o prefab, monta um com formas simples.
public class Helicoptero : MonoBehaviour
{
    public float duracaoVoo = 16f;
    [Tooltip("Graus por segundo da hélice principal")]
    public float giroHelice = 900f;

    Vector3 inicio, destino, alvoPairar;
    float t;
    bool descendo;
    Transform helice, rotorCauda, luzPisca, holofoteT;
    AudioSource audioSrc;

    public bool Chegou => t >= duracaoVoo;

    Transform iluminar;

    // cena final: desce devagar até esse ponto no ar (na frente do Marcos e da Lia), holofote em cima deles
    public void Descer(Vector3 pontoNoAr, Transform holofoteEm = null)
    {
        descendo = true;
        alvoPairar = pontoNoAr;
        iluminar = holofoteEm;
    }

    public static Helicoptero Criar(Vector3 pousoAlvo, AudioClip som, GameObject modelo = null)
    {
        var go = new GameObject("Helicoptero");
        var h = go.AddComponent<Helicoptero>();
        h.destino = pousoAlvo + Vector3.up * 16f;
        Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        h.inicio = h.destino + dir * 130f + Vector3.up * 30f;
        go.transform.position = h.inicio;
        go.transform.rotation = Quaternion.LookRotation(-dir);
        h.Montar(som, modelo);
        return h;
    }

    // teste automático: já coloca pairando em cima da clínica
    public void PularVoo() => t = duracaoVoo;

    void Montar(AudioClip som, GameObject modelo)
    {
        Bounds b;
        if (modelo != null)
        {
            var m = Instantiate(modelo, transform, false);
            m.name = "Modelo";
            helice = m.transform.Find("Helice");
            rotorCauda = m.transform.Find("RotorCauda");
            foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
            b = LimitesLocais(m);
        }
        else b = MontarFormas();

        // holofote embaixo do nariz apontado pro chão, luz vermelha piscando na cauda
        var holofote = new GameObject("Holofote").AddComponent<Light>();
        holofote.transform.SetParent(transform, false);
        holofote.transform.localPosition = new Vector3(0f, b.min.y + 0.3f, b.center.z + b.extents.z * 0.55f);
        holofote.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
        holofote.type = LightType.Spot;
        holofote.range = 50f;
        holofote.spotAngle = 40f;
        holofote.intensity = 60f;
        holofote.color = new Color(1f, 0.97f, 0.88f);
        holofote.shadows = LightShadows.None;
        holofoteT = holofote.transform;
        var pisca = new GameObject("LuzPisca").AddComponent<Light>();
        pisca.transform.SetParent(transform, false);
        pisca.transform.localPosition = new Vector3(0f, b.center.y + b.extents.y * 0.6f, b.min.z + 0.6f);
        pisca.type = LightType.Point;
        pisca.range = 10f;
        pisca.intensity = 5f;
        pisca.color = Color.red;
        pisca.shadows = LightShadows.None;
        luzPisca = pisca.transform;

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.clip = som;
        audioSrc.loop = true;
        audioSrc.spatialBlend = 1f;
        audioSrc.rolloffMode = AudioRolloffMode.Linear;
        audioSrc.minDistance = 15f;
        audioSrc.maxDistance = 200f;
        audioSrc.dopplerLevel = 0f;
        audioSrc.volume = 0.9f;
        if (som != null) audioSrc.Play();
    }

    // limites do modelo no espaço do helicóptero (pra posicionar as luzes)
    Bounds LimitesLocais(GameObject m)
    {
        var rends = m.GetComponentsInChildren<Renderer>();
        var b = new Bounds(Vector3.zero, Vector3.one);
        bool primeiro = true;
        foreach (var r in rends)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var lb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var canto = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = transform.InverseTransformPoint(r.transform.TransformPoint(canto));
                if (primeiro) { b = new Bounds(p, Vector3.zero); primeiro = false; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    // reserva: helicóptero de formas simples (material base: não fica rosa na build)
    Bounds MontarFormas()
    {
        var verde = new Color(0.24f, 0.29f, 0.2f);
        var escuro = new Color(0.08f, 0.08f, 0.08f);
        var vidro = new Color(0.12f, 0.18f, 0.24f);
        Peca(PrimitiveType.Capsule, new Vector3(0f, 0f, 0f), new Vector3(2.2f, 2.4f, 1.8f), Quaternion.Euler(90f, 0f, 0f), verde);
        Peca(PrimitiveType.Sphere, new Vector3(0f, 0.15f, 1.7f), new Vector3(1.7f, 1.3f, 1.6f), Quaternion.identity, vidro);
        Peca(PrimitiveType.Cube, new Vector3(0f, 0.3f, -3.6f), new Vector3(0.35f, 0.4f, 4.2f), Quaternion.identity, verde);
        Peca(PrimitiveType.Cube, new Vector3(0f, 0.9f, -5.5f), new Vector3(0.12f, 1.4f, 0.8f), Quaternion.identity, verde);
        Peca(PrimitiveType.Cube, new Vector3(0f, 1.05f, 0f), new Vector3(0.5f, 0.35f, 0.8f), Quaternion.identity, escuro);
        foreach (float x in new[] { -0.9f, 0.9f })
            Peca(PrimitiveType.Cube, new Vector3(x, -1.25f, 0f), new Vector3(0.12f, 0.1f, 3.6f), Quaternion.identity, escuro);
        helice = new GameObject("Helice").transform;
        helice.SetParent(transform, false);
        helice.localPosition = new Vector3(0f, 1.35f, 0f);
        PecaEm(helice, new Vector3(9f, 0.05f, 0.35f), escuro);
        PecaEm(helice, new Vector3(0.35f, 0.05f, 9f), escuro);
        rotorCauda = new GameObject("RotorCauda").transform;
        rotorCauda.SetParent(transform, false);
        rotorCauda.localPosition = new Vector3(0.2f, 0.9f, -5.6f);
        PecaEm(rotorCauda, new Vector3(0.04f, 1.6f, 0.15f), escuro);
        PecaEm(rotorCauda, new Vector3(0.04f, 0.15f, 1.6f), escuro);
        return new Bounds(new Vector3(0f, 0f, -1.5f), new Vector3(2.2f, 2.8f, 9f));
    }

    void Update()
    {
        t += Time.deltaTime;
        // não cobre as falas do resgate
        if (audioSrc != null && SomManager.Instance != null) audioSrc.volume = 0.9f * Mathf.Clamp01(SomManager.Instance.FatorEfeitos);
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duracaoVoo));
        if (descendo) destino = Vector3.MoveTowards(destino, alvoPairar, 4f * Time.deltaTime);
        Vector3 pos = Vector3.Lerp(inicio, destino, k) + Vector3.up * Mathf.Sin(Time.time * 1.3f) * 0.3f;
        // holofote varrendo o chão procurando sobreviventes; na cena final, em cima do Marcos e da Lia
        if (holofoteT != null)
        {
            var varrer = transform.rotation * Quaternion.Euler(68f + Mathf.Sin(Time.time * 0.7f) * 10f, Mathf.Sin(Time.time * 0.45f) * 28f, 0f);
            Quaternion quer = descendo && iluminar != null
                ? Quaternion.LookRotation(iluminar.position + Vector3.up - holofoteT.position)
                : (descendo ? transform.rotation * Quaternion.Euler(84f, 0f, 0f) : varrer);
            holofoteT.rotation = descendo ? Quaternion.Slerp(holofoteT.rotation, quer, 2.5f * Time.deltaTime) : quer;
        }
        Vector3 mov = destino - inicio;
        mov.y = 0f;
        transform.position = pos;
        // inclina pra frente enquanto voa, nivela ao chegar
        if (mov.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(mov) * Quaternion.Euler(12f * (1f - k), 0f, 0f);
        if (helice != null) helice.Rotate(0f, giroHelice * Time.deltaTime, 0f, Space.Self);
        if (rotorCauda != null) rotorCauda.Rotate(2200f * Time.deltaTime, 0f, 0f, Space.Self);
        luzPisca.gameObject.SetActive(Mathf.Repeat(Time.time, 1f) < 0.15f);
    }

    void Peca(PrimitiveType forma, Vector3 pos, Vector3 escala, Quaternion rot, Color cor)
    {
        var go = GameObject.CreatePrimitive(forma);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = escala;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = MaterialBase.Cor(cor, r);
    }

    void PecaEm(Transform pai, Vector3 escala, Color cor)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(pai, false);
        go.transform.localScale = escala;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = MaterialBase.Cor(cor, r);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
