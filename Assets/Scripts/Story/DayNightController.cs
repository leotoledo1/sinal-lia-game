using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Clima de cada fase: céu (shader SinalDaLia/Ceu), sol/lua, luz ambiente em 3 tons, neblina e "cor do filme"
// (pós-processamento). Dia nublado -> entardecer (fim do Dia 1 e Dia 2 chegando perto da Lia) -> noite azulada.
// A neblina tem a MESMA cor do horizonte do céu: a cidade some no horizonte sem "corte".
public class DayNightController : MonoBehaviour
{
    public static DayNightController Instance { get; private set; }

    public Light sol;
    public Camera cam;

    [System.Serializable]
    public class Clima
    {
        [Header("Céu")]
        public Color topo, horizonte, chao, nuvem, nuvemSombra;
        [Range(0f, 1f)] public float nuvens, estrelas;
        public float solTamanho, solBrilho, luaTamanho;
        [Header("Sol / lua")]
        public Color corLuz;
        public float intensidade;
        public Vector3 rotacao;
        [Header("Luz ambiente (vinda do céu / do lado / do chão)")]
        public Color ambCeu, ambMeio, ambChao;
        [Header("Neblina (a cor é a do horizonte)")]
        public float neblina, distanciaVisao;
        [Header("Cor do filme (pós-processamento)")]
        public float exposicao, contraste, saturacao, temperatura, vinheta, bloom;

        public Clima Copia() => (Clima)MemberwiseClone();
    }

    public Clima dia = DiaPadrao();
    public Clima tarde = TardePadrao();
    public Clima noite = NoitePadrao();

    public static Clima DiaPadrao() => new Clima
    {
        topo = new Color(0.38f, 0.44f, 0.52f), horizonte = new Color(0.68f, 0.68f, 0.65f), chao = new Color(0.33f, 0.32f, 0.31f),
        nuvem = new Color(0.78f, 0.78f, 0.77f), nuvemSombra = new Color(0.47f, 0.49f, 0.53f), nuvens = 0.62f, estrelas = 0f,
        solTamanho = 0.03f, solBrilho = 0.35f, luaTamanho = 0f,
        corLuz = new Color(1f, 0.95f, 0.87f), intensidade = 1.2f, rotacao = new Vector3(48f, -30f, 0f),
        ambCeu = new Color(0.56f, 0.6f, 0.68f), ambMeio = new Color(0.46f, 0.47f, 0.47f), ambChao = new Color(0.25f, 0.24f, 0.22f),
        neblina = 0.0095f, distanciaVisao = 220f,
        exposicao = 0.1f, contraste = 14f, saturacao = -12f, temperatura = -6f, vinheta = 0.28f, bloom = 0.35f,
    };

    public static Clima TardePadrao() => new Clima
    {
        topo = new Color(0.22f, 0.25f, 0.42f), horizonte = new Color(0.86f, 0.55f, 0.37f), chao = new Color(0.26f, 0.21f, 0.19f),
        nuvem = new Color(0.95f, 0.6f, 0.45f), nuvemSombra = new Color(0.36f, 0.29f, 0.36f), nuvens = 0.5f, estrelas = 0f,
        solTamanho = 0.045f, solBrilho = 1.6f, luaTamanho = 0f,
        corLuz = new Color(1f, 0.58f, 0.32f), intensidade = 1.25f, rotacao = new Vector3(9f, 250f, 0f),
        ambCeu = new Color(0.42f, 0.4f, 0.56f), ambMeio = new Color(0.58f, 0.42f, 0.33f), ambChao = new Color(0.2f, 0.16f, 0.14f),
        neblina = 0.011f, distanciaVisao = 220f,
        exposicao = 0.05f, contraste = 18f, saturacao = -4f, temperatura = 16f, vinheta = 0.33f, bloom = 0.55f,
    };

    public static Clima NoitePadrao() => new Clima
    {
        topo = new Color(0.012f, 0.018f, 0.045f), horizonte = new Color(0.055f, 0.075f, 0.12f), chao = new Color(0.02f, 0.025f, 0.035f),
        nuvem = new Color(0.1f, 0.115f, 0.16f), nuvemSombra = new Color(0.03f, 0.035f, 0.055f), nuvens = 0.38f, estrelas = 1f,
        solTamanho = 0f, solBrilho = 0f, luaTamanho = 0.032f,
        corLuz = new Color(0.55f, 0.66f, 1f), intensidade = 0.38f, rotacao = new Vector3(38f, 150f, 0f),
        ambCeu = new Color(0.085f, 0.11f, 0.19f), ambMeio = new Color(0.055f, 0.065f, 0.1f), ambChao = new Color(0.02f, 0.022f, 0.03f),
        neblina = 0.026f, distanciaVisao = 110f,
        exposicao = 0.35f, contraste = 20f, saturacao = -16f, temperatura = -16f, vinheta = 0.42f, bloom = 0.75f,
    };

    public float Entardecer { get; private set; }
    public bool Noite { get; private set; }
    // Marcos dentro da escola/clínica (sem sol, luz do interior, lanterna ligada)
    public bool Interno => interior != null;

    Interior interior;
    Clima pedido; // clima de fora pedido enquanto estava dentro (volta ao sair)

    Light lanterna, volta;
    Material ceu;
    LightShadows sombraDia = LightShadows.Soft;
    ColorAdjustments ajusteCor;
    WhiteBalance balanco;
    Vignette vinheta;
    Bloom bloom;
    readonly Clima atual = new Clima();
    float entardecerAplicado = -1f;

    void Awake()
    {
        Instance = this;
        if (cam == null) cam = Camera.main;
        if (sol != null) sombraDia = sol.shadows == LightShadows.None ? LightShadows.Soft : sol.shadows;
        // cópia do céu pra não alterar o asset original
        if (RenderSettings.skybox != null)
        {
            ceu = new Material(RenderSettings.skybox);
            RenderSettings.skybox = ceu;
        }
        if (sol != null) RenderSettings.sun = sol;
        // "cor do filme": cópia do perfil do Volume (vol.profile já devolve uma cópia em Play)
        var vol = FindAnyObjectByType<Volume>();
        if (vol != null && vol.sharedProfile != null)
        {
            var perfil = vol.profile;
            perfil.TryGet(out ajusteCor);
            perfil.TryGet(out balanco);
            perfil.TryGet(out vinheta);
            perfil.TryGet(out bloom);
        }
        if (cam != null)
        {
            // a lanterna sai do ombro do Marcos (não da câmera: parede perto da câmera "estourava" de branco)
            var go = new GameObject("Lanterna");
            go.transform.SetParent(transform, false);
            lanterna = go.AddComponent<Light>();
            lanterna.type = LightType.Spot;
            lanterna.range = 32f;
            lanterna.spotAngle = 48f;
            lanterna.innerSpotAngle = 20f;
            lanterna.intensity = 24f;
            lanterna.color = new Color(1f, 0.95f, 0.86f);
            lanterna.shadows = LightShadows.None; // sombra de lanterna é cara
            lanterna.enabled = false;
            var go2 = new GameObject("LanternaVolta");
            go2.transform.SetParent(transform, false);
            volta = go2.AddComponent<Light>();
            volta.type = LightType.Point;
            volta.range = 6.5f;
            volta.intensity = 1.1f;
            volta.color = new Color(1f, 0.95f, 0.86f);
            volta.shadows = LightShadows.None;
            volta.enabled = false;
        }
    }

    // lanterna: do ombro do Marcos até onde a mira aponta; nas cenas fica fraquinha (o holofote/lampião ilumina)
    void LateUpdate()
    {
        if (volta != null && !Interno) volta.enabled = false;
        if (lanterna == null || !lanterna.enabled || cam == null) return;
        var ph = PlayerHealth.Instance;
        Vector3 frente = cam.transform.forward;
        Vector3 origem = cam.transform.position;
        if (ph != null)
        {
            Vector3 plano = new Vector3(frente.x, 0f, frente.z);
            plano = plano.sqrMagnitude > 0.001f ? plano.normalized : ph.transform.forward;
            origem = ph.transform.position + Vector3.up * 1.45f + cam.transform.right * 0.25f + plano * 0.35f;
        }
        if (volta != null)
        {
            volta.enabled = Interno;
            if (Interno) volta.transform.position = origem + Vector3.up * 0.35f;
        }
        Vector3 alvo = cam.transform.position + frente * 30f;
        lanterna.transform.SetPositionAndRotation(origem, Quaternion.LookRotation(alvo - origem));
        var gm = GameManager.Instance;
        lanterna.intensity = Mathf.MoveTowards(lanterna.intensity, gm != null && gm.EmCena ? 6f : 24f, 40f * Time.unscaledDeltaTime);
    }

    // fase de dia (nublado) ou de noite
    public void Aplicar(bool ehNoite)
    {
        Noite = ehNoite;
        Entardecer = 0f;
        entardecerAplicado = -1f;
        Usar(ehNoite ? noite : dia);
        if (sol != null) sol.shadows = ehNoite ? LightShadows.None : sombraDia; // luar quase não faz sombra visível: desempenho nas hordas
        if (lanterna != null) lanterna.enabled = ehNoite || Interno;
        LuzNoturna.AplicarTodas(ehNoite);
        if (SomManager.Instance != null) SomManager.Instance.Ambiente(ehNoite);
    }

    // 0 = dia nublado, 1 = pôr do sol alaranjado (o sol desce enquanto o Marcos se aproxima da Lia)
    public void AplicarEntardecer(float t)
    {
        if (Noite) return;
        Entardecer = t = Mathf.Clamp01(t);
        if (Mathf.Abs(t - entardecerAplicado) < 0.002f) return; // só atualiza quando muda de verdade
        entardecerAplicado = t;
        Misturar(dia, tarde, Mathf.SmoothStep(0f, 1f, t), atual);
        Usar(atual);
    }

    // fim da última noite: o céu clareia (0 = noite, 1 = nascer do sol alaranjado). Postes continuam acesos
    public void AplicarAurora(float t)
    {
        t = Mathf.Clamp01(t);
        Misturar(noite, tarde, Mathf.SmoothStep(0f, 1f, t), atual);
        Usar(atual);
        if (sol != null) sol.shadows = t > 0.35f ? sombraDia : LightShadows.None;
    }

    // interiores

    // entrou na escola/clínica: sem sol, escuro com a neblina da cor do interior, lanterna ligada
    public void EntrarInterior(Interior i)
    {
        if (i == null) return;
        interior = i;
        if (sol != null) sol.enabled = false;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = i.ambienteCeu;
        RenderSettings.ambientEquatorColor = i.ambienteMeio;
        RenderSettings.ambientGroundColor = i.ambienteChao;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = i.corNeblina;
        RenderSettings.fogDensity = i.neblina;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = i.corNeblina;
            cam.farClipPlane = i.distanciaVisao;
        }
        if (ajusteCor != null)
        {
            ajusteCor.postExposure.Override(i.exposicao);
            ajusteCor.contrast.Override(i.contraste);
            ajusteCor.saturation.Override(i.saturacao);
        }
        if (balanco != null) balanco.temperature.Override(i.temperatura);
        if (vinheta != null) vinheta.intensity.Override(i.vinheta);
        if (bloom != null) bloom.intensity.Override(i.bloom);
        if (lanterna != null) lanterna.enabled = true;
    }

    // saiu: volta o clima de fora (o que a fase pediu enquanto ele estava lá dentro)
    public void SairInterior()
    {
        if (interior == null) return;
        interior = null;
        if (sol != null) sol.enabled = true;
        Usar(pedido ?? (Noite ? noite : dia));
        if (lanterna != null) lanterna.enabled = Noite;
    }

    void Usar(Clima c)
    {
        pedido = c;
        if (Interno) return; // dentro: guarda e aplica quando sair
        if (sol != null)
        {
            sol.color = c.corLuz;
            sol.intensity = c.intensidade;
            sol.transform.rotation = Quaternion.Euler(c.rotacao);
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = c.ambCeu;
        RenderSettings.ambientEquatorColor = c.ambMeio;
        RenderSettings.ambientGroundColor = c.ambChao;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = c.horizonte;
        RenderSettings.fogDensity = c.neblina;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = c.distanciaVisao; // a neblina esconde o horizonte: não precisa desenhar longe
            cam.backgroundColor = c.horizonte;
        }
        Ceu(c);
        if (ajusteCor != null)
        {
            ajusteCor.postExposure.Override(c.exposicao);
            ajusteCor.contrast.Override(c.contraste);
            ajusteCor.saturation.Override(c.saturacao);
            ajusteCor.colorFilter.Override(Color.white);
        }
        if (balanco != null) balanco.temperature.Override(c.temperatura);
        if (vinheta != null) vinheta.intensity.Override(c.vinheta);
        if (bloom != null) bloom.intensity.Override(c.bloom);
    }

    void Ceu(Clima c)
    {
        if (ceu == null) return;
        // sol e lua no lugar de onde vem a luz (de noite a "luz" é o luar; no amanhecer a lua encolhe e o sol cresce)
        Vector3 dirLuz = sol != null ? -sol.transform.forward : Vector3.up;
        ceu.SetColor("_CorTopo", c.topo);
        ceu.SetColor("_CorHorizonte", c.horizonte);
        ceu.SetColor("_CorChao", c.chao);
        ceu.SetColor("_NuvemCor", c.nuvem);
        ceu.SetColor("_NuvemSombra", c.nuvemSombra);
        ceu.SetFloat("_Nuvens", c.nuvens);
        ceu.SetFloat("_Estrelas", c.estrelas);
        ceu.SetFloat("_SolTamanho", c.solTamanho);
        ceu.SetFloat("_SolBrilho", c.solBrilho);
        ceu.SetColor("_SolCor", c.corLuz);
        ceu.SetVector("_SolDir", dirLuz);
        ceu.SetFloat("_LuaTamanho", c.luaTamanho);
        ceu.SetVector("_LuaDir", dirLuz);
    }

    static void Misturar(Clima a, Clima b, float t, Clima r)
    {
        r.topo = Color.Lerp(a.topo, b.topo, t);
        r.horizonte = Color.Lerp(a.horizonte, b.horizonte, t);
        r.chao = Color.Lerp(a.chao, b.chao, t);
        r.nuvem = Color.Lerp(a.nuvem, b.nuvem, t);
        r.nuvemSombra = Color.Lerp(a.nuvemSombra, b.nuvemSombra, t);
        r.nuvens = Mathf.Lerp(a.nuvens, b.nuvens, t);
        r.estrelas = Mathf.Lerp(a.estrelas, b.estrelas, t);
        r.solTamanho = Mathf.Lerp(a.solTamanho, b.solTamanho, t);
        r.solBrilho = Mathf.Lerp(a.solBrilho, b.solBrilho, t);
        r.luaTamanho = Mathf.Lerp(a.luaTamanho, b.luaTamanho, t);
        r.corLuz = Color.Lerp(a.corLuz, b.corLuz, t);
        r.intensidade = Mathf.Lerp(a.intensidade, b.intensidade, t);
        r.rotacao = Quaternion.Slerp(Quaternion.Euler(a.rotacao), Quaternion.Euler(b.rotacao), t).eulerAngles;
        r.ambCeu = Color.Lerp(a.ambCeu, b.ambCeu, t);
        r.ambMeio = Color.Lerp(a.ambMeio, b.ambMeio, t);
        r.ambChao = Color.Lerp(a.ambChao, b.ambChao, t);
        r.neblina = Mathf.Lerp(a.neblina, b.neblina, t);
        r.distanciaVisao = Mathf.Lerp(a.distanciaVisao, b.distanciaVisao, t);
        r.exposicao = Mathf.Lerp(a.exposicao, b.exposicao, t);
        r.contraste = Mathf.Lerp(a.contraste, b.contraste, t);
        r.saturacao = Mathf.Lerp(a.saturacao, b.saturacao, t);
        r.temperatura = Mathf.Lerp(a.temperatura, b.temperatura, t);
        r.vinheta = Mathf.Lerp(a.vinheta, b.vinheta, t);
        r.bloom = Mathf.Lerp(a.bloom, b.bloom, t);
    }
}
