using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Ambientação: acampamento do Marcos (início), árvores mortas e pedras na faixa entre a cidade e o muro,
// silhueta de cidade além do muro (o horizonte deixa de ser "o fim do mundo"), feixes de luz macios e fogo melhor.
public static partial class SinalDaLiaSetup
{
    const string NATUREZA = "Assets/Stylized Nature MegaKit[Standard]/FBX (Unity)/";
    const string NATUREZA_TEX = "Assets/Stylized Nature MegaKit[Standard]/Textures/";
    const string HORIZONTE = "Horizonte_v13";

    // acampamento do Marcos (onde o jogo começa)

    // fogueira com pedras e lenha, barraca de lona, saco de dormir, caixote com o rádio militar e sacos de areia.
    // Fica na faixa de terra atrás do Marcos (do lado oposto da escola): aparece no menu e quando ele olha pra trás.
    static void MontarAbrigo(Transform raiz, Vector3 posInicio, Vector3 posEscola, Material matFogo, Material matFumaca, AudioClip somFogo)
    {
        Vector3 longe = posInicio - posEscola;
        longe.y = 0f;
        longe = longe.sqrMagnitude > 0.01f ? longe.normalized : Vector3.back;
        Vector3 lado = new Vector3(longe.z, 0f, -longe.x);
        Vector3 centro = posInicio + longe * 9.5f;
        var abrigo = new GameObject("Abrigo_Marcos");
        abrigo.transform.SetParent(raiz, false);
        float Yaw(Vector3 d) => Quaternion.LookRotation(d).eulerAngles.y;

        var lona = Mat("Lona", new Color(0.29f, 0.34f, 0.22f));
        var madeira = Mat("Madeira", new Color(0.42f, 0.29f, 0.17f));
        var pedra = Mat("PedraFogueira", new Color(0.36f, 0.35f, 0.33f));
        var tecido = Mat("SacoDormir", new Color(0.5f, 0.14f, 0.1f));
        var radioMat = Mat("RadioMilitar", new Color(0.18f, 0.22f, 0.17f));

        // fogueira: anel de pedras + lenha cruzada + fogo + luz
        var fogueira = new GameObject("Fogueira");
        fogueira.transform.SetParent(abrigo.transform, false);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 8f;
            var p = Peca(fogueira, pedra, new Vector3(Mathf.Cos(a) * 0.62f, 0.09f, Mathf.Sin(a) * 0.62f), new Vector3(0.3f, 0.18f, 0.24f));
            p.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 15f * (i % 3), 0f);
        }
        for (int i = 0; i < 3; i++)
            Peca(fogueira, madeira, new Vector3(0f, 0.12f, 0f), new Vector3(0.12f, 0.45f, 0.12f), PrimitiveType.Cylinder)
                .transform.localRotation = Quaternion.Euler(80f, i * 60f, 0f);
        fogueira.transform.position = centro;
        var colF = fogueira.AddComponent<BoxCollider>();
        colF.center = new Vector3(0f, 0.15f, 0f);
        colF.size = new Vector3(1.5f, 0.3f, 1.5f);
        var fogo = new GameObject("Fogo");
        fogo.transform.SetParent(fogueira.transform, false);
        fogo.transform.SetPositionAndRotation(centro + Vector3.up * 0.15f, Quaternion.Euler(-90f, 0f, 0f));
        ParticulasFogo(fogo, matFogo, 0.85f);
        Brasas(fogo, matFogo, 0.85f);
        FumacaDoFogo(fogo, matFumaca, 0.85f);
        var luzGo = new GameObject("Luz");
        luzGo.transform.SetParent(fogueira.transform, false);
        luzGo.transform.position = centro + Vector3.up * 0.8f;
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Point;
        ConfigLuz(luz, 4.5f, 1.6f, 0.45f, false, new Color(1f, 0.58f, 0.25f), 10f);
        var som = fogo.AddComponent<AudioSource>();
        som.clip = somFogo;
        som.loop = true;
        som.playOnAwake = true;
        som.spatialBlend = 1f;
        som.rolloffMode = AudioRolloffMode.Linear;
        som.minDistance = 1.5f;
        som.maxDistance = 12f;
        som.volume = 0.45f;
        som.dopplerLevel = 0f;

        // barraca de lona (duas abas em "A"), aberta pro lado da fogueira
        var barraca = new GameObject("Barraca");
        barraca.transform.SetParent(abrigo.transform, false);
        float L = 2.4f, W = 2.1f, H = 1.35f;
        float aba = Mathf.Sqrt(W * W / 4f + H * H), ang = Mathf.Atan2(H, W / 2f) * Mathf.Rad2Deg;
        foreach (float s in new[] { -1f, 1f })
        {
            var p = Peca(barraca, lona, new Vector3(s * W / 4f, H / 2f, 0f), new Vector3(aba, 0.04f, L));
            p.transform.localRotation = Quaternion.Euler(0f, 0f, -s * ang);
        }
        Peca(barraca, madeira, new Vector3(0f, H / 2f, L / 2f), new Vector3(0.06f, H, 0.06f));
        Peca(barraca, madeira, new Vector3(0f, H / 2f, -L / 2f), new Vector3(0.06f, H, 0.06f));
        Peca(barraca, tecido, new Vector3(0f, 0.06f, 0f), new Vector3(0.75f, 0.12f, 1.9f)); // saco de dormir lá dentro
        var colB = barraca.AddComponent<BoxCollider>();
        colB.center = new Vector3(0f, H / 2f, 0f);
        colB.size = new Vector3(W, H, L);
        Colocar(barraca, centro + longe * 3.2f, Yaw(-longe));

        // caixote com o rádio militar (a luzinha vermelha pisca)
        var caixote = new GameObject("Caixote_Radio");
        caixote.transform.SetParent(abrigo.transform, false);
        Peca(caixote, madeira, new Vector3(0f, 0.27f, 0f), new Vector3(0.75f, 0.54f, 0.55f));
        Peca(caixote, radioMat, new Vector3(0.05f, 0.65f, 0f), new Vector3(0.38f, 0.22f, 0.18f));
        Peca(caixote, Mat("Antena", new Color(0.1f, 0.1f, 0.1f)), new Vector3(0.2f, 0.95f, 0.04f), new Vector3(0.015f, 0.4f, 0.015f), PrimitiveType.Cylinder);
        var luzRadio = new GameObject("LuzRadio").AddComponent<Light>();
        luzRadio.transform.SetParent(caixote.transform, false);
        luzRadio.transform.localPosition = new Vector3(-0.08f, 0.78f, -0.12f);
        luzRadio.type = LightType.Point;
        ConfigLuz(luzRadio, 0.9f, 0.6f, 0f, true, new Color(1f, 0.15f, 0.1f), 1.4f);
        var colC = caixote.AddComponent<BoxCollider>();
        colC.center = new Vector3(0f, 0.38f, 0f);
        colC.size = new Vector3(0.75f, 0.76f, 0.55f);
        Colocar(caixote, centro - lado * 1.7f + longe * 0.4f, Yaw(lado));

        // saco de dormir perto do fogo (onde ele passou a noite) + pallets como banco
        var saco = new GameObject("SacoDormir");
        saco.transform.SetParent(abrigo.transform, false);
        Peca(saco, tecido, new Vector3(0f, 0.08f, 0f), new Vector3(0.78f, 0.16f, 1.95f));
        Peca(saco, Mat("Travesseiro", new Color(0.62f, 0.6f, 0.55f)), new Vector3(0f, 0.13f, 0.82f), new Vector3(0.55f, 0.14f, 0.32f));
        var colS = saco.AddComponent<BoxCollider>();
        colS.center = new Vector3(0f, 0.1f, 0f);
        colS.size = new Vector3(0.8f, 0.2f, 2f);
        Colocar(saco, centro + lado * 1.8f + longe * 0.3f, Yaw(longe) + 15f);
        if (TemQuaternius)
        {
            var pallet = InstQ("Pallet/Pallet.fbx", 1.25f, abrigo.transform);
            if (pallet != null) Colocar(pallet, centro - longe * 1.6f + lado * 0.8f, Yaw(lado) + 10f);
        }

        // sacos de areia em volta (proteção do acampamento)
        foreach (float g in new[] { -55f, 55f, 0f })
        {
            Vector3 d = Quaternion.Euler(0f, g, 0f) * longe;
            var s = SacosDeAreia(abrigo.transform, Mat("SacoAreia", new Color(0.55f, 0.5f, 0.35f)));
            Colocar(s, centro + d * (g == 0f ? 5.4f : 4.2f), Yaw(d) + 90f);
        }
    }

    // natureza na faixa entre a cidade e o muro

    // árvores mortas (troncos retorcidos) e pedras: tiram a cara de "estacionamento vazio" da borda do mapa
    static void MontarNatureza(Transform raiz, Vector3 origem, Vector2 tamanho, float meioTile, System.Random r)
    {
        var casca = Mat("Natureza_TroncoMorto", Color.white, false, AssetDatabase.LoadAssetAtPath<Texture2D>(NATUREZA_TEX + "Bark_DeadTree.png"));
        var rocha = Mat("Natureza_Pedra", new Color(0.82f, 0.8f, 0.78f), false, AssetDatabase.LoadAssetAtPath<Texture2D>(NATUREZA_TEX + "Rocks_Diffuse.png"));
        if (casca.HasProperty("_Smoothness")) casca.SetFloat("_Smoothness", 0.05f);
        if (rocha.HasProperty("_Smoothness")) rocha.SetFloat("_Smoothness", 0.1f);
        float Rnd(float a, float b) => a + (float)r.NextDouble() * (b - a);

        // o retângulo da cidade (bordas das ruas de fora) e a faixa até o muro
        float x0 = origem.x - meioTile, x1 = origem.x + tamanho.x + meioTile;
        float z0 = origem.z - meioTile, z1 = origem.z + tamanho.y + meioTile;
        float faixa = MARGEM_MURO - 2f - meioTile - 1.5f; // até perto do muro
        int arvores = 0, pedras = 0;
        for (int lado = 0; lado < 4; lado++)
        {
            bool horizontal = lado < 2;
            float comprimento = horizontal ? x1 - x0 + faixa * 2f : z1 - z0 + faixa * 2f;
            for (float s = Rnd(2f, 8f); s < comprimento; s += Rnd(7f, 13f))
            {
                float dentro = Rnd(3.5f, faixa - 1.5f); // distância da borda da cidade
                Vector3 p = horizontal
                    ? new Vector3(x0 - faixa + s, origem.y, lado == 0 ? z0 - dentro : z1 + dentro)
                    : new Vector3(lado == 2 ? x0 - dentro : x1 + dentro, origem.y, z0 - faixa + s);
                if (r.NextDouble() < 0.62)
                {
                    var arv = InstNatureza("DeadTree_" + (1 + r.Next(5)), Rnd(5.5f, 9f), raiz, casca, true);
                    if (arv != null && Colocar(arv, p, Rnd(0f, 360f))) arvores++;
                }
                if (r.NextDouble() < 0.55)
                {
                    var ped = InstNatureza("Rock_Medium_" + (1 + r.Next(3)), Rnd(0.7f, 1.8f), raiz, rocha, false);
                    Vector3 pp = p + new Vector3(Rnd(-2.5f, 2.5f), 0f, Rnd(-2.5f, 2.5f));
                    if (ped != null && Colocar(ped, pp, Rnd(0f, 360f))) pedras++;
                }
            }
        }
        Debug.Log($"[Sinal da Lia] Natureza: {arvores} árvores mortas, {pedras} pedras");
    }

    // modelo do Stylized Nature MegaKit com material próprio (garante a cor certa na build), pé no chão
    static GameObject InstNatureza(string nome, float altura, Transform pai, Material mat, bool tronco)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(NATUREZA + nome + ".fbx");
        if (asset == null) return null;
        var raizN = new GameObject(nome);
        raizN.transform.SetParent(pai, false);
        var m = (GameObject)PrefabUtility.InstantiatePrefab(asset, raizN.transform);
        foreach (var rd in m.GetComponentsInChildren<Renderer>())
            rd.sharedMaterials = Enumerable.Repeat(mat, rd.sharedMaterials.Length).ToArray();
        var b = Limites(m);
        if (b.size.y > 0.01f) m.transform.localScale *= altura / b.size.y;
        b = Limites(m);
        var p = raizN.transform.position;
        m.transform.position += new Vector3(p.x - b.center.x, p.y - b.min.y, p.z - b.center.z);
        if (tronco)
        {
            var c = raizN.AddComponent<CapsuleCollider>();
            c.radius = 0.32f;
            c.height = 3f;
            c.center = new Vector3(0f, 1.5f, 0f);
        }
        else
        {
            b = Limites(m);
            var c = raizN.AddComponent<BoxCollider>();
            c.center = raizN.transform.InverseTransformPoint(b.center);
            c.size = b.size * 0.85f;
        }
        return raizN;
    }

    // silhueta de cidade além do muro

    // prédios simples (sem colisor, fora do mapa) que a neblina deixa azulados: dá a sensação de cidade grande em quarentena
    static void MontarHorizonte(GeradorCidade g, Vector3 centro, Vector2 tamanho)
    {
        var velho = g.transform.Find(HORIZONTE);
        if (velho != null) Object.DestroyImmediate(velho.gameObject);
        var raiz = new GameObject(HORIZONTE).transform;
        raiz.SetParent(g.transform, false);
        var r = new System.Random(g.seed + 404);
        float Rnd(float a, float b) => a + (float)r.NextDouble() * (b - a);
        var mats = new[]
        {
            Mat("PredioDistante_1", new Color(0.4f, 0.41f, 0.44f)),
            Mat("PredioDistante_2", new Color(0.34f, 0.35f, 0.38f)),
            Mat("PredioDistante_3", new Color(0.46f, 0.44f, 0.42f)),
        };
        float muroX = tamanho.x / 2f + MARGEM_MURO - 2f, muroZ = tamanho.y / 2f + MARGEM_MURO - 2f;
        int n = 0;
        for (int lado = 0; lado < 4; lado++)
        {
            bool eixoX = lado < 2; // muro paralelo ao eixo X (norte/sul)
            float sinal = lado % 2 == 0 ? -1f : 1f;
            float comprimento = (eixoX ? muroX : muroZ) + 70f;
            for (int fileira = 0; fileira < 2; fileira++)
            {
                for (float s = -comprimento; s < comprimento; s += Rnd(10f, 18f))
                {
                    if (r.NextDouble() < 0.18) continue; // buraco entre prédios
                    float fora = (fileira == 0 ? Rnd(9f, 22f) : Rnd(34f, 60f));
                    float alt = fileira == 0 ? Rnd(9f, 26f) : Rnd(18f, 48f);
                    float larg = Rnd(8f, 15f), prof = Rnd(8f, 16f);
                    Vector3 p = eixoX
                        ? centro + new Vector3(s, 0f, sinal * (muroZ + fora + prof / 2f))
                        : centro + new Vector3(sinal * (muroX + fora + prof / 2f), 0f, s);
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(go.GetComponent<Collider>());
                    go.name = "PredioDistante";
                    go.transform.SetParent(raiz, false);
                    go.transform.position = p + Vector3.up * (alt / 2f - 0.5f);
                    go.transform.localScale = eixoX ? new Vector3(larg, alt, prof) : new Vector3(prof, alt, larg);
                    var rd = go.GetComponent<Renderer>();
                    rd.sharedMaterial = mats[r.Next(mats.Length)];
                    rd.shadowCastingMode = ShadowCastingMode.Off;
                    rd.receiveShadows = false;
                    n++;
                    // caixa d'água / antena em cima de alguns
                    if (r.NextDouble() < 0.25)
                    {
                        var topo = GameObject.CreatePrimitive(r.NextDouble() < 0.5 ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                        Object.DestroyImmediate(topo.GetComponent<Collider>());
                        topo.transform.SetParent(raiz, false);
                        topo.transform.position = p + Vector3.up * (alt + 1.2f);
                        topo.transform.localScale = new Vector3(Rnd(2f, 4f), 1.4f, Rnd(2f, 4f));
                        var rt = topo.GetComponent<Renderer>();
                        rt.sharedMaterial = mats[1];
                        rt.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
            }
        }
        // caixas d'água do pacote, bem visíveis por cima do muro
        if (TemQuaternius)
            for (int i = 0; i < 3; i++)
            {
                var wt = InstQ("Water Tower/WaterTower.fbx", 16f, raiz, false);
                if (wt == null) continue;
                float ang = 35f + i * 120f;
                Vector3 d = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                float alcance = Mathf.Max(muroX, muroZ) + 14f;
                wt.transform.position = centro + new Vector3(Mathf.Clamp(d.x * alcance * 1.5f, -muroX - 14f, muroX + 14f), 0f, Mathf.Clamp(d.z * alcance * 1.5f, -muroZ - 14f, muroZ + 14f));
                foreach (var rd in wt.GetComponentsInChildren<Renderer>()) rd.shadowCastingMode = ShadowCastingMode.Off;
            }
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        Debug.Log($"[Sinal da Lia] Horizonte: {n} prédios distantes");
    }

    // chão de terra

    // textura de terra que repete sem emenda (ruído "periódico" em várias escalas + pedrinhas escuras)
    static Texture2D TexturaTerra()
    {
        const string path = "Assets/Textures/Gerado/terra.png";
        var existente = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existente != null) return existente;
        Pasta("Assets/Textures/Gerado");
        const int N = 256;
        var r = new System.Random(7);
        var grades = new[] { Grade(6, r), Grade(12, r), Grade(24, r), Grade(48, r) };
        var pesos = new[] { 0.45f, 0.27f, 0.17f, 0.11f };
        Color escuro = new Color(0.2f, 0.185f, 0.16f), claro = new Color(0.37f, 0.335f, 0.285f);
        var t = new Texture2D(N, N, TextureFormat.RGB24, false);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N, n = 0f;
                for (int k = 0; k < grades.Length; k++) n += pesos[k] * ValorPeriodico(grades[k], u, v);
                var c = Color.Lerp(escuro, claro, Mathf.SmoothStep(0f, 1f, n));
                if (r.NextDouble() < 0.012) c *= 0.7f; // pedrinha
                t.SetPixel(x, y, c);
            }
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.mipmapEnabled = true;
        imp.anisoLevel = 4;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float[,] Grade(int n, System.Random r)
    {
        var g = new float[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++) g[i, j] = (float)r.NextDouble();
        return g;
    }

    // ruído de valor que "dá a volta" nas bordas (a textura repete sem emenda)
    static float ValorPeriodico(float[,] g, float u, float v)
    {
        int n = g.GetLength(0);
        float x = u * n, y = v * n;
        int x0 = Mathf.FloorToInt(x) % n, y0 = Mathf.FloorToInt(y) % n;
        int x1 = (x0 + 1) % n, y1 = (y0 + 1) % n;
        float fx = Mathf.SmoothStep(0f, 1f, x - Mathf.Floor(x)), fy = Mathf.SmoothStep(0f, 1f, y - Mathf.Floor(y));
        return Mathf.Lerp(Mathf.Lerp(g[x0, y0], g[x1, y0], fx), Mathf.Lerp(g[x0, y1], g[x1, y1], fx), fy);
    }

    // luzes e efeitos

    // feixe de luz macio (linha virada pra câmera, aditiva): forte embaixo, some no céu
    static Material MatFeixe(string nome, Color cor) => MatParticulaTransparente(nome, cor, TexturaGerada("feixe", AlfaFeixe), true);

    static float AlfaFeixe(float u, float v) => Mathf.Pow(Mathf.Clamp01(1f - v * v), 1.6f);

    static void LinhaFeixe(GameObject go, Material mat, float altura, float larguraBase, float larguraTopo, float alfaBase)
    {
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.SetPosition(0, Vector3.zero);
        lr.SetPosition(1, Vector3.up * altura);
        lr.widthMultiplier = 1f;
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, larguraBase), new Keyframe(1f, larguraTopo));
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(alfaBase, 0f), new GradientAlphaKey(alfaBase * 0.55f, 0.3f), new GradientAlphaKey(0f, 1f) });
        lr.colorGradient = grad;
        lr.textureMode = LineTextureMode.Stretch;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sharedMaterial = mat;
    }

    // faíscas subindo da fogueira
    static void Brasas(GameObject pai, Material mat, float escala)
    {
        var go = new GameObject("Brasas");
        go.transform.SetParent(pai.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f * escala, 2.2f * escala);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.4f, 0.1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;
        main.gravityModifier = -0.04f;
        var em = ps.emission;
        em.rateOverTime = 7f * escala;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 18f;
        forma.radius = 0.25f * escala;
        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.9f;
        ruido.frequency = 0.8f;
        ruido.quality = ParticleSystemNoiseQuality.Low;
        var cor = ps.colorOverLifetime;
        cor.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.35f, 0.1f), 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        cor.color = grad;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = mat;
        pr.shadowCastingMode = ShadowCastingMode.Off;
        pr.receiveShadows = false;
    }

    // fumaça escura saindo do fogo
    static void FumacaDoFogo(GameObject pai, Material matFumaca, float escala)
    {
        var go = new GameObject("FumacaFogo");
        go.transform.SetParent(pai.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.55f * escala); // (o pai está virado pra cima: Z local = pra cima)
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * escala, 0.9f * escala);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f * escala, 0.9f * escala);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.15f, 0.14f), new Color(0.26f, 0.25f, 0.24f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 18;
        var em = ps.emission;
        em.rateOverTime = 3.5f * escala;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 8f;
        forma.radius = 0.15f * escala;
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 2.4f)));
        var cor = ps.colorOverLifetime;
        cor.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.15f), new GradientAlphaKey(0f, 1f) });
        cor.color = grad;
        var vento = ps.velocityOverLifetime;
        vento.enabled = true;
        vento.space = ParticleSystemSimulationSpace.World;
        vento.x = new ParticleSystem.MinMaxCurve(0.35f);
        vento.y = new ParticleSystem.MinMaxCurve(0f);
        vento.z = new ParticleSystem.MinMaxCurve(0.15f);
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = matFumaca;
        pr.sortMode = ParticleSystemSortMode.Distance;
        pr.shadowCastingMode = ShadowCastingMode.Off;
        pr.receiveShadows = false;
    }

    // lampião no esconderijo da Lia (dá pra ver ela na cena do reencontro, já no fim da tarde)
    static void LampiaoLia(Transform raiz, Vector3 posLia, Vector3? deslocamento = null)
    {
        var go = new GameObject("Lampiao_Lia");
        go.transform.SetParent(raiz, false);
        go.transform.position = posLia + (deslocamento ?? new Vector3(0.7f, 0f, -0.5f));
        Peca(go, Mat("LampiaoBase", new Color(0.15f, 0.15f, 0.14f)), new Vector3(0f, 0.04f, 0f), new Vector3(0.18f, 0.04f, 0.18f), PrimitiveType.Cylinder);
        var vidro = Peca(go, Mat("LampiaoLuz", new Color(1f, 0.85f, 0.45f), true), new Vector3(0f, 0.2f, 0f), new Vector3(0.13f, 0.12f, 0.13f), PrimitiveType.Cylinder);
        vidro.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var luz = new GameObject("Luz").AddComponent<Light>();
        luz.transform.SetParent(go.transform, false);
        luz.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        luz.type = LightType.Point;
        ConfigLuz(luz, 2.6f, 1.1f, 0.12f, false, new Color(1f, 0.74f, 0.42f), 5.5f);
    }
}
