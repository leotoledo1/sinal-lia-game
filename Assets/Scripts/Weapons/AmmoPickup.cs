using System.Linq;
using UnityEngine;

// Munição (ou arma nova) no chão: gira, flutua e é coletada ao encostar.
// Mostra o modelo da arma correspondente; arma nova ganha um feixe de luz pra ser vista de longe.
public class AmmoPickup : MonoBehaviour
{
    public WeaponData arma;
    public int quantidade = 12;
    public float raioColeta = 1.6f;
    [Tooltip("0 = nunca some")]
    public float tempoDeVida = 0f;
    [Tooltip("Ligado = é a própria arma (libera pra usar), não só munição")]
    public bool liberaArma;

    Vector3 basePos;
    float nascimento;
    Transform visual;

    public static Material MaterialArmas;   // paleta do pacote de armas (vem do PlayerWeapons)
    public static Material MaterialFeixe;   // feixe da arma nova

    public static AmmoPickup Criar(WeaponData arma, int qtd, Vector3 pos, float tempoDeVida, bool liberaArma = false)
    {
        var go = new GameObject((liberaArma ? "Arma_" : "Municao_") + arma.nome);
        go.transform.position = pos + Vector3.up * 0.5f;
        var p = go.AddComponent<AmmoPickup>();
        p.arma = arma;
        p.quantidade = qtd;
        p.tempoDeVida = tempoDeVida;
        p.liberaArma = liberaArma;
        p.MontarVisual();
        return p;
    }

    void MontarVisual()
    {
        float tam = liberaArma ? 1f : 0.65f;
        if (arma.modelo != null)
        {
            // "Giro" fica no centro; o modelo é centralizado dentro dele (gira sem orbitar)
            var giro = new GameObject("Giro").transform;
            giro.SetParent(transform, false);
            var m = Instantiate(arma.modelo, giro);
            foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.Euler(arma.rotacaoNaMao) ;
            var rends = m.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float maior = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (maior > 0.0001f) m.transform.localScale *= Mathf.Max(0.35f, arma.comprimentoModelo) * tam / maior;
                // centraliza no meio do pickup
                b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                m.transform.position += transform.position - b.center;
                foreach (var r in rends)
                {
                    if (MaterialArmas != null) r.sharedMaterials = Enumerable.Repeat(MaterialArmas, r.sharedMaterials.Length).ToArray();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            visual = giro;
        }
        else
        {
            var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(cubo.GetComponent<Collider>());
            cubo.transform.SetParent(transform, false);
            cubo.transform.localScale = new Vector3(0.45f, 0.3f, 0.3f) * tam;
            cubo.GetComponent<Renderer>().sharedMaterial = MaterialBase.Cor(arma.corMunicao);
            visual = cubo.transform;
        }

        // base colorida (cor da munição) pra saber de que arma é
        var baseCor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(baseCor.GetComponent<Collider>());
        baseCor.transform.SetParent(transform, false);
        baseCor.transform.localPosition = Vector3.down * 0.45f;
        baseCor.transform.localScale = new Vector3(0.7f * tam, 0.02f, 0.7f * tam);
        var rb = baseCor.GetComponent<Renderer>();
        rb.sharedMaterial = MaterialBase.Cor(arma.corMunicao);
        rb.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (liberaArma)
        {
            // feixe de luz vermelha (linha virada pra câmera, some no alto): dá pra ver de longe
            var feixe = new GameObject("Feixe");
            feixe.transform.SetParent(transform, false);
            var lr = feixe.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.down * 0.45f);
            lr.SetPosition(1, Vector3.up * 16f);
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, 0.3f));
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.5f, 0.3f), new GradientAlphaKey(0f, 1f) });
            lr.colorGradient = grad;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sharedMaterial = MaterialFeixe != null ? MaterialFeixe : MaterialBase.Cor(arma.corMunicao);
            // brilho vermelho no chão em volta da arma
            var luz = new GameObject("Brilho").AddComponent<Light>();
            luz.transform.SetParent(transform, false);
            luz.transform.localPosition = Vector3.up * 0.6f;
            luz.type = LightType.Point;
            luz.range = 4f;
            luz.intensity = 1.6f;
            luz.color = new Color(1f, 0.3f, 0.2f);
            luz.shadows = LightShadows.None;
        }
    }

    void Start()
    {
        basePos = transform.position;
        nascimento = Time.time;
    }

    void Update()
    {
        if (visual != null) visual.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.08f;
        if (tempoDeVida > 0f && Time.time - nascimento > tempoDeVida) { Destroy(gameObject); return; }

        var pw = PlayerWeapons.Instance;
        if (pw == null || arma == null) return;
        // só pega jogando (no menu, nas cenas e depois de morrer, não)
        var gm = GameManager.Instance;
        if (gm != null && !gm.JogoAtivo) return;
        if (PlayerHealth.Instance != null && !PlayerHealth.Instance.EstaVivo) return;
        Vector3 d = pw.transform.position - basePos;
        d.y = 0f;
        if (d.magnitude > raioColeta) return;
        if (liberaArma)
        {
            pw.Liberar(arma);
            Destroy(gameObject);
        }
        else if (pw.AdicionarMunicao(arma, quantidade))
        {
            int i = System.Array.IndexOf(pw.armas, arma);
            HUD.Aviso(pw.Liberada(i) ? $"+{quantidade} munição de {arma.nome}" : $"+{quantidade} munição de {arma.nome} (arma ainda não encontrada)");
            Destroy(gameObject);
        }
    }
}
