using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

// Peças para montar os interiores: paredes com portas/janelas (malha com UV "do mundo": o piso e a
// pintura continuam de uma peça pra outra), móveis dos pacotes com tamanho real, texturas geradas (granilite,
// tinta, forro, azulejo), letreiros (texto renderizado numa textura), desenhos de criança e luzes.
public static partial class SinalDaLiaSetup
{
    const string MAT_INT = INTERIORES + "Materiais/";
    const string GERADO_INT = INTERIORES + "Gerado/";
    const string TEX_INT = "Assets/Textures/Gerado/Interiores/";

    static readonly StaticEditorFlags FlagsEstatico = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

    // materiais

    static Material MatI(string nome, Color cor, float liso = 0.12f, Texture tex = null, bool unlit = false, float metal = 0f)
    {
        Pasta(MAT_INT.TrimEnd('/'));
        string path = MAT_INT + nome + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader) m.shader = shader;
        m.SetColor("_BaseColor", cor);
        m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", liso);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        return m;
    }

    // material do móvel com a cor do material do FBX (os do FBX às vezes saíam desbotados);
    // Kenney (cores pastel) fica mais escuro e encardido pra combinar com o apocalipse
    static readonly Dictionary<string, Material> matsMovel = new Dictionary<string, Material>();
    static Material MatMovel(Material orig, string pacote)
    {
        if (orig == null) return null;
        string nomeLimpo = new string(orig.name.Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
        string chave = pacote + "_" + nomeLimpo;
        if (matsMovel.TryGetValue(chave, out var m) && m != null) return m;
        Color c = orig.HasProperty("_BaseColor") ? orig.GetColor("_BaseColor") : (orig.HasProperty("_Color") ? orig.color : Color.gray);
        if (pacote == "Kenney")
        {
            float g = c.grayscale;
            c = Color.Lerp(c, new Color(g, g, g), 0.35f) * 0.6f;
        }
        else c *= 0.92f;
        c.a = 1f;
        string n = orig.name.ToLower();
        bool metal = n.Contains("metal") || n.Contains("gold");
        bool vidro = n.Contains("glass") || n.Contains("mirror");
        m = MatI(chave, c, metal ? 0.42f : (vidro ? 0.82f : 0.1f), null, false, metal ? 0.55f : 0f);
        matsMovel[chave] = m;
        return m;
    }

    // caixas com UV do mundo

    static Mesh conteinerMalhas;
    static string pathMalhas;
    static int nMalhas;

    // todas as malhas geradas de um interior ficam num arquivo só (sub-assets)
    static void IniciarMalhas(string nome)
    {
        Pasta(GERADO_INT.TrimEnd('/'));
        pathMalhas = GERADO_INT + nome + ".asset";
        AssetDatabase.DeleteAsset(pathMalhas);
        conteinerMalhas = null;
        nMalhas = 0;
    }

    static void GuardarMalha(Mesh m)
    {
        m.name = $"{m.name}_{nMalhas++}";
        if (conteinerMalhas == null)
        {
            AssetDatabase.CreateAsset(m, pathMalhas);
            conteinerMalhas = m;
        }
        else AssetDatabase.AddObjectToAsset(m, conteinerMalhas);
    }

    // caixa centrada na origem; UV = posição no interior / uvMetros (a textura continua entre caixas vizinhas)
    static Mesh MalhaCaixa(Vector3 centro, Vector3 tam, float uvMetros)
    {
        var h = tam / 2f;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        void Face(Vector3 n, Vector3 u, Vector3 v, float hn, float hu, float hv)
        {
            int i0 = verts.Count;
            var cs = new[] { n * hn - u * hu - v * hv, n * hn + u * hu - v * hv, n * hn + u * hu + v * hv, n * hn - u * hu + v * hv };
            foreach (var p in cs)
            {
                verts.Add(p);
                Vector3 w = centro + p;
                Vector2 uv = Mathf.Abs(n.x) > 0.5f ? new Vector2(w.z, w.y) : (Mathf.Abs(n.y) > 0.5f ? new Vector2(w.x, w.z) : new Vector2(w.x, w.y));
                uvs.Add(uv / uvMetros);
            }
            // a face tem que "olhar" pra fora (ordem horária vista de fora)
            bool certo = Vector3.Dot(Vector3.Cross(cs[2] - cs[0], cs[1] - cs[0]), n) > 0f;
            if (certo) tris.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            else tris.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
        }
        Face(Vector3.right, Vector3.forward, Vector3.up, h.x, h.z, h.y);
        Face(Vector3.left, Vector3.forward, Vector3.up, h.x, h.z, h.y);
        Face(Vector3.up, Vector3.right, Vector3.forward, h.y, h.x, h.z);
        Face(Vector3.down, Vector3.right, Vector3.forward, h.y, h.x, h.z);
        Face(Vector3.forward, Vector3.right, Vector3.up, h.z, h.x, h.y);
        Face(Vector3.back, Vector3.right, Vector3.up, h.z, h.x, h.y);
        var m = new Mesh { name = "Caixa" };
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateTangents();
        m.RecalculateBounds();
        GuardarMalha(m);
        return m;
    }

    // construtor (paredes, pisos, portas)

    internal class Construtor
    {
        public Transform raiz;
        public float pe = 3.2f;     // altura do teto
        public float barra = 1.2f;  // altura da barra pintada embaixo
        public Material piso, teto, paredeBaixo, paredeCima, faixa, batente;
        public float uvPiso = 1f, uvParede = 2f, uvTeto = 1.25f;
        public float uvParedeBaixo = -1f; // azulejo usa outra escala (vazio = a da parede)
    }

    struct Vao
    {
        public float centro, largura, baseY, topoY;
        public static Vao Porta(float centro, float largura = 1.05f, float altura = 2.15f) => new Vao { centro = centro, largura = largura, baseY = 0f, topoY = altura };
        public static Vao Janela(float centro, float largura = 1.7f, float baseY = 1.0f, float topoY = 2.3f) => new Vao { centro = centro, largura = largura, baseY = baseY, topoY = topoY };
    }

    static GameObject CaixaInt(Construtor c, string nome, Vector3 centro, Vector3 tam, Material m, float uv, bool colisor = true, bool fora = false)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(c.raiz, false);
        go.transform.localPosition = centro;
        go.AddComponent<MeshFilter>().sharedMesh = MalhaCaixa(centro, tam, uv);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        if (colisor) go.AddComponent<BoxCollider>().size = tam;
        // teto e coisas por fora não entram no NavMesh (senão vira "chão" em cima do teto)
        if (fora) go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        GameObjectUtility.SetStaticEditorFlags(go, FlagsEstatico);
        return go;
    }

    // parede reta de a até b (alinhada a X ou a Z), espessura e, com vãos (porta/janela) medidos a partir de "a"
    static void ParedeInt(Construtor c, Vector3 a, Vector3 b, float e, params Vao[] vaos)
    {
        Vector3 dir = b - a;
        float comp = dir.magnitude;
        dir /= comp;
        bool eixoX = Mathf.Abs(dir.x) > 0.5f;
        float cursor = 0f;
        foreach (var v in vaos.OrderBy(v => v.centro))
        {
            float ini = v.centro - v.largura / 2f, fim = v.centro + v.largura / 2f;
            if (ini > cursor + 0.001f) TrechoParede(c, a, dir, eixoX, cursor, ini, 0f, c.pe, e);
            if (v.baseY > 0.001f) TrechoParede(c, a, dir, eixoX, ini, fim, 0f, v.baseY, e);
            if (v.topoY < c.pe - 0.001f) TrechoParede(c, a, dir, eixoX, ini, fim, v.topoY, c.pe, e);
            Batentes(c, a, dir, eixoX, ini, fim, v.baseY, v.topoY, e);
            cursor = fim;
        }
        if (comp > cursor + 0.001f) TrechoParede(c, a, dir, eixoX, cursor, comp, 0f, c.pe, e);
    }

    static void TrechoParede(Construtor c, Vector3 a, Vector3 dir, bool eixoX, float s0, float s1, float y0, float y1, float e)
    {
        float comp = s1 - s0;
        if (comp < 0.005f || y1 - y0 < 0.005f) return;
        Vector3 p = a + dir * ((s0 + s1) / 2f);
        Vector3 Tam(float alt, float esp) => eixoX ? new Vector3(comp, alt, esp) : new Vector3(esp, alt, comp);
        if (y0 < c.barra)
        {
            float topo = Mathf.Min(y1, c.barra);
            CaixaInt(c, "Parede", p + Vector3.up * ((y0 + topo) / 2f), Tam(topo - y0, e), c.paredeBaixo, c.uvParedeBaixo > 0f ? c.uvParedeBaixo : c.uvParede);
        }
        if (y1 > c.barra)
        {
            float b0 = Mathf.Max(y0, c.barra);
            CaixaInt(c, "Parede", p + Vector3.up * ((b0 + y1) / 2f), Tam(y1 - b0, e), c.paredeCima, c.uvParede);
        }
        // faixa escura na divisa das duas cores (um pouco mais grossa que a parede)
        if (c.faixa != null && y0 < c.barra && y1 > c.barra)
            CaixaInt(c, "Faixa", p + Vector3.up * c.barra, Tam(0.05f, e + 0.03f), c.faixa, 1f, false);
        // rodapé
        if (c.faixa != null && y0 < 0.01f)
            CaixaInt(c, "Rodape", p + Vector3.up * 0.05f, Tam(0.1f, e + 0.03f), c.faixa, 1f, false);
    }

    // moldura de porta/janela (batentes dos lados + em cima; peitoril na janela)
    static void Batentes(Construtor c, Vector3 a, Vector3 dir, bool eixoX, float ini, float fim, float y0, float y1, float e)
    {
        if (c.batente == null) return;
        const float L = 0.07f;
        float esp = e + 0.06f;
        Vector3 T(float largura, float alt) => eixoX ? new Vector3(largura, alt, esp) : new Vector3(esp, alt, largura);
        CaixaInt(c, "Batente", a + dir * (ini + L / 2f) + Vector3.up * ((y0 + y1) / 2f), T(L, y1 - y0), c.batente, 1f, false);
        CaixaInt(c, "Batente", a + dir * (fim - L / 2f) + Vector3.up * ((y0 + y1) / 2f), T(L, y1 - y0), c.batente, 1f, false);
        CaixaInt(c, "Batente", a + dir * ((ini + fim) / 2f) + Vector3.up * (y1 - L / 2f), T(fim - ini, L), c.batente, 1f, false);
        if (y0 > 0.01f) CaixaInt(c, "Peitoril", a + dir * ((ini + fim) / 2f) + Vector3.up * (y0 + 0.025f), eixoX ? new Vector3(fim - ini, 0.05f, e + 0.12f) : new Vector3(e + 0.12f, 0.05f, fim - ini), c.batente, 1f, false);
    }

    static void PisoTeto(Construtor c, float x0, float z0, float x1, float z1, Material piso = null, Material teto = null)
    {
        CaixaInt(c, "Piso", new Vector3((x0 + x1) / 2f, -0.05f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.1f, z1 - z0), piso ?? c.piso, c.uvPiso);
        CaixaInt(c, "Teto", new Vector3((x0 + x1) / 2f, c.pe + 0.05f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.1f, z1 - z0), teto ?? c.teto, c.uvTeto, true, true);
    }

    // móveis dos pacotes

    static readonly Dictionary<string, GameObject> cacheModelos = new Dictionary<string, GameObject>();

    static GameObject ModeloInt(string arquivo)
    {
        if (!cacheModelos.TryGetValue(arquivo, out var g) || g == null)
            cacheModelos[arquivo] = g = AssetDatabase.LoadAssetAtPath<GameObject>(INTERIORES + arquivo + ".fbx");
        if (g == null) Debug.LogWarning("[Sinal da Lia] Interiores: modelo não encontrado: " + arquivo);
        return g;
    }

    // móvel com tamanho real: "alvo" = medida (m) no eixo (0 largura X, 1 altura Y, 2 profundidade Z) depois do giroModelo.
    // tamanho != null: escala cada eixo pra caber exatamente (portas, janelas). Pé no chão, centrado em "pos".
    // giroModelo = pra onde o modelo olha (corrige a frente pra +Z); yaw = rotação no cômodo
    static GameObject MovelI(Transform pai, string arquivo, Vector3 pos, float yaw, float alvo, int eixo = 1, float giroModelo = 0f,
        bool colisor = true, Vector3? tamanho = null, bool estatico = true)
    {
        var asset = ModeloInt(arquivo);
        if (asset == null) return null;
        // monta na origem, sem pai (mede certo mesmo se o pai estiver girado) e depois encaixa no lugar
        var raizM = new GameObject(Path.GetFileName(arquivo));
        var escala = new GameObject("Escala").transform;
        escala.SetParent(raizM.transform, false);
        var m = (GameObject)PrefabUtility.InstantiatePrefab(asset, escala);
        // mantém a rotação que o FBX já traz (alguns vêm "deitados" e a raiz endireita): só soma o giro
        m.transform.localRotation = Quaternion.Euler(0f, giroModelo, 0f) * m.transform.localRotation;
        string pacote = arquivo.Split('/')[0];
        foreach (var r in m.GetComponentsInChildren<Renderer>())
            r.sharedMaterials = r.sharedMaterials.Select(x => MatMovel(x, pacote)).ToArray();
        var b = Limites(m);
        if (tamanho.HasValue)
        {
            var t = tamanho.Value;
            escala.localScale = new Vector3(t.x / Mathf.Max(0.001f, b.size.x), t.y / Mathf.Max(0.001f, b.size.y), t.z / Mathf.Max(0.001f, b.size.z));
        }
        else escala.localScale = Vector3.one * (alvo / Mathf.Max(0.001f, b.size[eixo]));
        b = Limites(m);
        escala.position -= new Vector3(b.center.x, b.min.y, b.center.z);
        if (colisor)
        {
            b = Limites(m);
            var col = raizM.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size;
        }
        raizM.transform.SetParent(pai, false);
        raizM.transform.localPosition = pos;
        raizM.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        if (estatico)
            foreach (var tr in raizM.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(tr.gameObject, FlagsEstatico);
        return raizM;
    }

    // tamanho atual (caixa no mundo) de um objeto montado
    static Vector3 Tamanho(GameObject go) => Limites(go).size;

    // peças simples (primitivas)

    // cubo sem colisor, filho de "pai", posição/escala locais
    static GameObject Bloquinho(Transform pai, Material m, Vector3 pos, Vector3 tam, string nome = "Peca", PrimitiveType tipo = PrimitiveType.Cube)
    {
        var p = GameObject.CreatePrimitive(tipo);
        Object.DestroyImmediate(p.GetComponent<Collider>());
        p.name = nome;
        p.transform.SetParent(pai, false);
        p.transform.localPosition = pos;
        p.transform.localScale = tam;
        p.GetComponent<Renderer>().sharedMaterial = m;
        return p;
    }

    // junta as peças (primitivas) de "temp" numa malha só (uma sub-malha por material) e salva como prefab
    static GameObject CombinarPrefab(GameObject temp, string nome, bool colisor = true)
    {
        Pasta("Assets/Prefabs/Interiores");
        Pasta(GERADO_INT.TrimEnd('/'));
        var porMat = new Dictionary<Material, List<CombineInstance>>();
        foreach (var mf in temp.GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null || mr.sharedMaterial == null) continue;
            if (!porMat.TryGetValue(mr.sharedMaterial, out var lista)) porMat[mr.sharedMaterial] = lista = new List<CombineInstance>();
            lista.Add(new CombineInstance { mesh = mf.sharedMesh, transform = temp.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix });
        }
        var partes = new List<CombineInstance>();
        var mats = new List<Material>();
        foreach (var kv in porMat)
        {
            var sub = new Mesh();
            sub.CombineMeshes(kv.Value.ToArray(), true, true);
            partes.Add(new CombineInstance { mesh = sub, transform = Matrix4x4.identity });
            mats.Add(kv.Key);
        }
        var malha = new Mesh { name = nome };
        malha.CombineMeshes(partes.ToArray(), false, false);
        malha.RecalculateBounds();
        foreach (var p in partes) Object.DestroyImmediate(p.mesh);
        string pathMalha = GERADO_INT + "Prefab_" + nome + ".asset";
        AssetDatabase.DeleteAsset(pathMalha);
        AssetDatabase.CreateAsset(malha, pathMalha);
        var go = new GameObject(nome);
        go.AddComponent<MeshFilter>().sharedMesh = malha;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
        if (colisor)
        {
            var col = go.AddComponent<BoxCollider>();
            col.center = malha.bounds.center;
            col.size = malha.bounds.size;
        }
        string path = "Assets/Prefabs/Interiores/" + nome + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(temp);
        return prefab;
    }

    static GameObject Por(GameObject prefab, Transform pai, Vector3 pos, float yaw, bool estatico = true)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        if (estatico) GameObjectUtility.SetStaticEditorFlags(go, FlagsEstatico);
        return go;
    }

    // texturas geradas

    static Texture2D TexturaRGB(string nome, int L, int A, System.Func<int, int, Color> pixel, bool repetir = true)
    {
        Pasta(TEX_INT.TrimEnd('/'));
        string path = TEX_INT + nome + ".png";
        var existente = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existente != null) return existente;
        var t = new Texture2D(L, A, TextureFormat.RGB24, false);
        var px = new Color[L * A];
        for (int y = 0; y < A; y++)
            for (int x = 0; x < L; x++) px[y * L + x] = pixel(x, y);
        t.SetPixels(px);
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = repetir ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.mipmapEnabled = true;
        imp.anisoLevel = 4;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float Ruido(float x, float y, float escala, int semente) => Mathf.PerlinNoise(x * escala + semente * 13.1f, y * escala + semente * 7.7f);

    // granilite (piso de escola pública): pedrinhas de várias cores + junta a cada 1 m (a textura = 1 m)
    static Texture2D TexGranilite()
    {
        var r = new System.Random(11);
        var pedras = new float[256 * 256];
        var cores = new Color[256 * 256];
        for (int i = 0; i < 2600; i++)
        {
            int cx = r.Next(256), cy = r.Next(256), rad = 1 + r.Next(3);
            double s = r.NextDouble();
            Color c = s < 0.45 ? new Color(0.3f, 0.3f, 0.29f) : (s < 0.8 ? new Color(0.86f, 0.85f, 0.8f) : new Color(0.55f, 0.36f, 0.3f));
            for (int dy = -rad; dy <= rad; dy++)
                for (int dx = -rad; dx <= rad; dx++)
                    if (dx * dx + dy * dy <= rad * rad)
                    {
                        int k = ((cy + dy + 256) % 256) * 256 + (cx + dx + 256) % 256;
                        pedras[k] = 1f;
                        cores[k] = c;
                    }
        }
        return TexturaRGB("granilite", 256, 256, (x, y) =>
        {
            float u = x / 256f, v = y / 256f;
            Color baseC = new Color(0.58f, 0.57f, 0.53f) * (0.9f + 0.18f * Ruido(u, v, 3f, 1));
            int k = y * 256 + x;
            Color c = pedras[k] > 0f ? Color.Lerp(baseC, cores[k], 0.75f) : baseC;
            if (x < 2 || y < 2) c = new Color(0.36f, 0.33f, 0.28f); // junta
            c *= 0.82f + 0.25f * Ruido(u, v, 1.3f, 9); // sujeira
            return c;
        });
    }

    // tinta de parede (branca, a cor vem do material): manchas, escorridos e arranhões (a textura = 2 m)
    static Texture2D TexTinta()
    {
        var r = new System.Random(5);
        var risco = new float[256 * 256];
        for (int i = 0; i < 40; i++)
        {
            int x = r.Next(256), y = r.Next(256), len = 8 + r.Next(40);
            for (int k = 0; k < len; k++) risco[((y - k + 256) % 256) * 256 + (x + k / 6) % 256] = 0.25f + (float)r.NextDouble() * 0.3f;
        }
        return TexturaRGB("tinta", 256, 256, (x, y) =>
        {
            float u = x / 256f, v = y / 256f;
            float n = 0.86f + 0.1f * Ruido(u, v, 2f, 3) + 0.05f * Ruido(u, v, 9f, 4);
            float escorrido = Mathf.Pow(Ruido(u * 6f, v * 0.4f, 1f, 8), 3f) * 0.18f;
            n -= escorrido + risco[y * 256 + x] * 0.35f;
            return new Color(n, n, n);
        });
    }

    // forro de placas (0,625 m) com furinhos (a textura = 1,25 m: 2×2 placas)
    static Texture2D TexForro() => TexturaRGB("forro", 256, 256, (x, y) =>
    {
        float n = 0.82f + 0.08f * Ruido(x / 256f, y / 256f, 4f, 2);
        if (x % 128 < 2 || y % 128 < 2) n = 0.45f;
        else if (x % 9 == 4 && y % 9 == 4) n -= 0.18f;
        float mancha = Mathf.Pow(Ruido(x / 256f, y / 256f, 1.7f, 6), 4f) * 0.5f; // infiltração
        return new Color(n - mancha * 0.6f, n - mancha * 0.7f, n - mancha);
    });

    // piso de cerâmica (0,5 m) com rejunte (a textura = 1 m)
    static Texture2D TexPisoClinica() => TexturaRGB("piso_clinica", 256, 256, (x, y) =>
    {
        int tx = x / 128, ty = y / 128;
        float n = 0.8f + 0.05f * ((tx * 7 + ty * 3) % 3) + 0.06f * Ruido(x / 256f, y / 256f, 5f, 3);
        if (x % 128 < 2 || y % 128 < 2) n = 0.42f;
        n *= 0.85f + 0.2f * Ruido(x / 256f, y / 256f, 1.2f, 7);
        return new Color(n, n * 0.99f, n * 0.97f);
    });

    // azulejo (15 cm) com rejunte (a textura = 0,6 m: 4×4); a cor vem do material
    static Texture2D TexAzulejo() => TexturaRGB("azulejo", 256, 256, (x, y) =>
    {
        float n = 0.92f + 0.05f * Ruido(x / 256f, y / 256f, 6f, 5);
        if (x % 64 < 2 || y % 64 < 2) n = 0.55f;
        float brilho = (x % 64 > 8 && x % 64 < 14) ? 0.04f : 0f;
        return new Color(n + brilho, n + brilho, n + brilho);
    });

    // cortiça do mural
    static Texture2D TexCortica() => TexturaRGB("cortica", 128, 128, (x, y) =>
    {
        float n = 0.5f + 0.35f * Ruido(x / 128f, y / 128f, 18f, 1) + ((x * 31 + y * 17) % 7 == 0 ? -0.15f : 0f);
        return new Color(0.62f * n + 0.18f, 0.45f * n + 0.12f, 0.28f * n + 0.06f);
    });

    // texto (letreiros, quadro-negro)

    const int CAMADA_TEXTO = 31;

    // renderiza o texto numa textura (TextMesh + câmera só dessa camada): o letreiro vira um material comum,
    // iluminado pela cena e escondido atrás das paredes (TextMesh direto na cena ficava "por cima" de tudo)
    static Texture2D TexturaTexto(string id, string texto, int L, int A, Color fundo, Color cor, bool negrito = true, float margem = 0.1f, int fonte = 96)
    {
        Pasta(TEX_INT + "Texto");
        string path = $"{TEX_INT}Texto/{id}_{Hash(texto + fundo + cor + L + A + negrito):x8}.png";
        var existente = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existente != null) return existente;

        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go = new GameObject("TextoTemp");
        go.layer = CAMADA_TEXTO;
        go.transform.position = new Vector3(0f, 9000f, 0f);
        var tm = go.AddComponent<TextMesh>();
        tm.font = f;
        tm.text = texto;
        tm.fontSize = fonte;
        tm.characterSize = 1f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = negrito ? FontStyle.Bold : FontStyle.Normal;
        tm.color = cor;
        go.GetComponent<MeshRenderer>().sharedMaterial = f.material;
        var cam = new GameObject("CamTexto").AddComponent<Camera>();
        cam.cullingMask = 1 << CAMADA_TEXTO;
        cam.orthographic = true;
        cam.orthographicSize = A / 2f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = fundo;
        cam.transform.position = go.transform.position - Vector3.forward * 50f;
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 100f;

        // 1ª foto mede o texto; a 2ª já com o tamanho que cabe com margem
        var px = Renderizar(cam, L, A);
        int x0 = L, x1 = -1, y0 = A, y1 = -1;
        for (int y = 0; y < A; y++)
            for (int x = 0; x < L; x++)
            {
                Color p = px[y * L + x];
                if (Mathf.Abs(p.r - fundo.r) + Mathf.Abs(p.g - fundo.g) + Mathf.Abs(p.b - fundo.b) < 0.08f) continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
        if (x1 > x0 && y1 > y0)
        {
            float k = Mathf.Min(L * (1f - 2f * margem) / (x1 - x0 + 1), A * (1f - 2f * margem) / (y1 - y0 + 1));
            Vector2 centro = new Vector2((x0 + x1) / 2f - L / 2f, (y0 + y1) / 2f - A / 2f);
            go.transform.localScale = Vector3.one * k;
            go.transform.position -= new Vector3(centro.x, centro.y, 0f) * k;
            px = Renderizar(cam, L, A);
        }
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(cam.gameObject);
        var t = new Texture2D(L, A, TextureFormat.RGB24, false);
        t.SetPixels(px);
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.mipmapEnabled = true;
        imp.anisoLevel = 4;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // hash que não muda entre sessões (FNV-1a)
    static uint Hash(string s)
    {
        uint h = 2166136261;
        foreach (char ch in s) { h ^= ch; h *= 16777619; }
        return h;
    }

    // ângulo pra porta abrir pro lado "lado" (dir = direção da parede, a dobradiça fica no começo do vão)
    static float Abre(Vector3 dir, Vector3 lado, float graus = 95f) => (Vector3.Cross(dir, lado).y < 0f ? -1f : 1f) * graus;

    static Color[] Renderizar(Camera cam, int L, int A)
    {
        var rt = RenderTexture.GetTemporary(L, A, 24);
        cam.targetTexture = rt;
        cam.aspect = L / (float)A;
        cam.Render();
        cam.targetTexture = null;
        var ativo = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(L, A, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, L, A), 0, 0);
        tex.Apply();
        RenderTexture.active = ativo;
        RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels();
        Object.DestroyImmediate(tex);
        return px;
    }

    // placa fina (quadro, letreiro, papel) olhando pra "normal"; com textura de texto ou cor
    static GameObject Placa(Transform pai, Vector3 pos, Vector3 normal, float largura, float altura, Material m, string nome = "Placa", float espessura = 0.02f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = nome;
        go.transform.SetParent(pai, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.LookRotation(normal, Vector3.up);
        go.transform.localScale = new Vector3(largura, altura, espessura);
        go.GetComponent<Renderer>().sharedMaterial = m;
        GameObjectUtility.SetStaticEditorFlags(go, FlagsEstatico);
        return go;
    }

    // a face +Z do cubo mostra a textura de cabeça pra baixo/espelhada? Quad resolve: frente = -Z do quad
    static GameObject Cartaz(Transform pai, Vector3 pos, Vector3 normal, float largura, float altura, Material m, string nome = "Cartaz")
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = nome;
        go.transform.SetParent(pai, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.LookRotation(-normal, Vector3.up); // o Quad é visto pelo lado -Z
        go.transform.localScale = new Vector3(largura, altura, 1f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(go, FlagsEstatico);
        return go;
    }

    // letreiro: placa de fundo + texto renderizado na frente
    static GameObject Letreiro(Transform pai, string id, string texto, Vector3 pos, Vector3 normal, float largura, float altura, Color fundo, Color cor, int L = 768)
    {
        int A = Mathf.Max(64, Mathf.RoundToInt(L * altura / largura));
        var tex = TexturaTexto(id, texto, L, A, fundo, cor);
        var go = Cartaz(pai, pos + normal * 0.012f, normal, largura, altura, MatI("Texto_" + id, Color.white, 0.2f, tex), "Letreiro_" + id);
        Placa(pai, pos, normal, largura + 0.06f, altura + 0.06f, MatI("Moldura_Letreiro", new Color(0.12f, 0.12f, 0.13f)), "Moldura", 0.02f);
        return go;
    }

    // desenhos de criança

    static Texture2D DesenhoCrianca(int semente)
    {
        const int W = 256, H = 200;
        Pasta(TEX_INT.TrimEnd('/'));
        string path = $"{TEX_INT}desenho_crianca_{semente}.png";
        var existente = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existente != null) return existente;
        var r = new System.Random(semente);
        float R(float a, float b) => a + (float)r.NextDouble() * (b - a);
        var px = new Color[W * H];
        Color papel = new Color(0.95f, 0.94f, 0.88f);
        for (int i = 0; i < px.Length; i++) px[i] = papel * (0.96f + 0.04f * Mathf.PerlinNoise(i % W * 0.05f, i / W * 0.05f));
        Color[] giz = { new Color(0.15f, 0.3f, 0.8f), new Color(0.9f, 0.25f, 0.55f), new Color(1f, 0.75f, 0.1f), new Color(0.2f, 0.62f, 0.25f),
                        new Color(0.85f, 0.2f, 0.15f), new Color(0.45f, 0.25f, 0.12f), new Color(0.55f, 0.3f, 0.75f) };
        void Ponto(float x, float y, float raio, Color c)
        {
            for (int dy = -(int)raio; dy <= raio; dy++)
                for (int dx = -(int)raio; dx <= raio; dx++)
                {
                    int xx = (int)x + dx, yy = (int)y + dy;
                    if (xx < 0 || yy < 0 || xx >= W || yy >= H || dx * dx + dy * dy > raio * raio) continue;
                    if (r.NextDouble() < 0.2) continue; // giz de cera: falha
                    px[yy * W + xx] = Color.Lerp(px[yy * W + xx], c, 0.85f);
                }
        }
        void Linha(float x0, float y0, float x1, float y1, float esp, Color c)
        {
            int n = Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) * 1.5f) + 1;
            for (int i = 0; i <= n; i++) Ponto(Mathf.Lerp(x0, x1, i / (float)n) + R(-0.6f, 0.6f), Mathf.Lerp(y0, y1, i / (float)n) + R(-0.6f, 0.6f), esp, c);
        }
        void Circulo(float cx, float cy, float rr, float esp, Color c)
        {
            for (float a = 0f; a < Mathf.PI * 2f; a += 0.05f) Ponto(cx + Mathf.Cos(a) * rr, cy + Mathf.Sin(a) * rr, esp, c);
        }
        void Boneco(float x, float chao, float alt, Color c)
        {
            float cab = alt * 0.16f;
            Circulo(x, chao + alt - cab, cab, 1.6f, c);
            Linha(x, chao + alt - cab * 2f, x, chao + alt * 0.38f, 1.8f, c);
            Linha(x, chao + alt * 0.38f, x - alt * 0.16f, chao, 1.8f, c);
            Linha(x, chao + alt * 0.38f, x + alt * 0.16f, chao, 1.8f, c);
            Linha(x - alt * 0.22f, chao + alt * 0.62f, x + alt * 0.22f, chao + alt * 0.62f, 1.8f, c);
        }
        // grama, sol, casa ou árvore e a família de bonequinhos
        float chaoY = R(18f, 30f);
        for (float x = 0; x < W; x += 3f) Linha(x, chaoY - R(4f, 10f), x + R(-2f, 2f), chaoY + R(0f, 8f), 1.2f, giz[3]);
        float sx = r.Next(2) == 0 ? R(25f, 55f) : R(200f, 230f), sy = R(150f, 175f);
        Circulo(sx, sy, 14f, 3f, giz[2]);
        for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4f; Linha(sx + Mathf.Cos(a) * 18f, sy + Mathf.Sin(a) * 18f, sx + Mathf.Cos(a) * 28f, sy + Mathf.Sin(a) * 28f, 1.6f, giz[2]); }
        if (r.Next(2) == 0)
        {
            float hx = R(110f, 160f);
            Linha(hx - 32, chaoY, hx - 32, chaoY + 55, 2f, giz[4]); Linha(hx + 32, chaoY, hx + 32, chaoY + 55, 2f, giz[4]);
            Linha(hx - 32, chaoY + 55, hx + 32, chaoY + 55, 2f, giz[4]); Linha(hx - 40, chaoY + 55, hx, chaoY + 88, 2.2f, giz[5]); Linha(hx, chaoY + 88, hx + 40, chaoY + 55, 2.2f, giz[5]);
            Linha(hx - 8, chaoY, hx - 8, chaoY + 25, 1.6f, giz[5]); Linha(hx + 8, chaoY, hx + 8, chaoY + 25, 1.6f, giz[5]); Linha(hx - 8, chaoY + 25, hx + 8, chaoY + 25, 1.6f, giz[5]);
        }
        else
        {
            float tx = R(150f, 200f);
            Linha(tx, chaoY, tx, chaoY + 50, 4f, giz[5]);
            Circulo(tx, chaoY + 70, 22f, 5f, giz[3]);
        }
        int pessoas = 2 + r.Next(3);
        for (int k = 0; k < pessoas; k++) Boneco(R(30f, 110f) + k * 18f, chaoY, R(40f, 75f), giz[r.Next(giz.Length)]);
        var t = new Texture2D(W, H, TextureFormat.RGB24, false);
        t.SetPixels(px);
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // luzes

    // luz que fica sempre acesa (dia e noite) e pode piscar; controlada pelo LuzNoturna (aquecimento, etc.)
    static Light LuzInt(Transform pai, Vector3 pos, Color cor, float intensidade, float alcance, float piscar = 0f, bool defeito = false, LightType tipo = LightType.Point)
    {
        var go = new GameObject(tipo == LightType.Spot ? "LuzJanela" : "Luz");
        go.transform.SetParent(pai, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = tipo;
        l.color = cor;
        l.range = alcance;
        l.intensity = intensidade;
        l.shadows = LightShadows.None;
        l.lightmapBakeType = LightmapBakeType.Realtime;
        var ln = go.AddComponent<LuzNoturna>();
        ln.intensidadeNoite = intensidade;
        ln.intensidadeDia = intensidade;
        ln.piscar = piscar;
        ln.defeito = defeito;
        return l;
    }

    // luminária de emergência (caixinha branca com 2 faróis) presa na parede, olhando pra "normal"
    static Light LuzEmergencia(Transform pai, Vector3 pos, Vector3 normal, bool defeito, Color? corLuz = null)
    {
        var go = new GameObject("LuzEmergencia");
        go.transform.SetParent(pai, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.LookRotation(normal, Vector3.up);
        var corpo = MatI("Emergencia_Corpo", new Color(0.82f, 0.82f, 0.8f), 0.3f);
        var lampada = MatI("Emergencia_Lampada", new Color(2.2f, 2.1f, 1.9f), 0f, null, true);
        Bloquinho(go.transform, corpo, new Vector3(0f, 0f, 0.05f), new Vector3(0.34f, 0.12f, 0.1f));
        foreach (float x in new[] { -0.1f, 0.1f })
            Bloquinho(go.transform, lampada, new Vector3(x, -0.02f, 0.105f), new Vector3(0.08f, 0.05f, 0.02f), "Lampada");
        var l = LuzInt(go.transform, new Vector3(0f, -0.15f, 0.4f), corLuz ?? new Color(1f, 0.96f, 0.88f), 2.2f, 7.5f, defeito ? 0.25f : 0.04f, defeito);
        foreach (var t in go.GetComponentsInChildren<Transform>())
            if (t.GetComponent<Light>() == null) GameObjectUtility.SetStaticEditorFlags(t.gameObject, FlagsEstatico);
        return l;
    }

    // janela com luz fria entrando: modelo da janela no vão, "céu" claro do lado de fora, luz spot pra dentro,
    // feixe de luz (poeira no ar) até o chão. centro = meio do vão (no meio da espessura da parede); dentro = normal pra dentro
    static void JanelaLuz(Transform pai, Vector3 centro, Vector3 dentro, float largura, float altura, float e, Color corCeu, Color corLuz, float intensidade = 4.5f, bool feixe = true)
    {
        var go = new GameObject("Janela");
        go.transform.SetParent(pai, false);
        go.transform.localPosition = centro;
        go.transform.localRotation = Quaternion.LookRotation(dentro, Vector3.up);
        // modelo (Quaternius Window_Large1: caixilho branco + vidro)
        var jan = MovelI(go.transform, "Quaternius_Interior/Window_Large1", new Vector3(0f, -altura / 2f, 0f), 0f, 0f, 1, 0f, false,
            new Vector3(largura, altura, 0.1f));
        if (jan != null) jan.transform.localPosition = new Vector3(0f, -altura / 2f, 0f);
        // céu lá fora (brilha: dá o contraluz da janela)
        var ceu = Cartaz(go.transform, new Vector3(0f, 0f, -e / 2f - 0.35f), Vector3.forward, largura + 0.6f, altura + 0.5f, MatI("CeuJanela_" + ColorUtility.ToHtmlStringRGB(corCeu), corCeu, 0f, null, true), "CeuLaFora");
        ceu.GetComponent<Renderer>().receiveShadows = false;
        // luz entrando, inclinada pra baixo
        var l = LuzInt(go.transform, new Vector3(0f, altura * 0.3f, 0.25f), corLuz, intensidade, 9f, 0f, false, LightType.Spot);
        l.transform.localRotation = Quaternion.Euler(38f, 0f, 0f);
        l.spotAngle = 75f;
        l.innerSpotAngle = 35f;
        if (feixe)
        {
            // feixe macio da janela até o chão (some no fim) + poeira flutuando nele
            var fx = new GameObject("FeixeJanela");
            fx.transform.SetParent(go.transform, false);
            Vector3 ini = new Vector3(0f, 0f, 0.1f);
            float alturaChao = centro.y; // distância até o chão
            Vector3 fim = new Vector3(0f, -alturaChao + 0.05f, alturaChao * 1.25f);
            fx.transform.localPosition = ini;
            fx.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (fim - ini).normalized);
            LinhaFeixe(fx, MatFeixe("Feixe_Janela", new Color(0.55f, 0.62f, 0.75f) * 0.55f), Vector3.Distance(ini, fim), largura * 0.9f, largura * 1.25f, 0.5f);
            Poeira(fx.transform, Vector3.Distance(ini, fim), largura);
        }
    }

    // poeira brilhando no feixe de luz (partículas bem pequenas, devagar)
    static void Poeira(Transform pai, float comprimento, float largura)
    {
        var go = new GameObject("Poeira");
        go.transform.SetParent(pai, false);
        go.transform.localPosition = Vector3.up * comprimento * 0.5f;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.97f, 0.9f, 0.7f), new Color(0.8f, 0.85f, 1f, 0.5f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 40;
        var em = ps.emission;
        em.rateOverTime = 5f;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Box;
        forma.scale = new Vector3(largura * 0.8f, comprimento * 0.9f, 0.6f);
        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.05f;
        ruido.frequency = 0.3f;
        ruido.quality = ParticleSystemNoiseQuality.Low;
        var cor = ps.colorOverLifetime;
        cor.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        cor.color = grad;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = MatParticulaTransparente("Poeira", new Color(1f, 1f, 1f, 0.8f), TexturaGerada("fumaca", AlfaFumaca), true);
        pr.shadowCastingMode = ShadowCastingMode.Off;
        pr.receiveShadows = false;
    }

    // portas

    // folha de porta no vão: a dobradiça fica na ponta "ini" do vão; girar abre. Fechada = NavMeshObstacle corta o NavMesh.
    // a = começo da parede, dir = direção da parede; anguloAberta: sinal escolhe pra que lado abre
    static PortaInterior PortaNoVao(Construtor c, string modelo, Vector3 a, Vector3 dir, float centroVao, float largura, float altura,
        float anguloAberta, bool aberta, bool trancavel = false)
    {
        var dob = new GameObject("Porta");
        dob.transform.SetParent(c.raiz, false);
        dob.transform.localPosition = a + dir * (centroVao - largura / 2f + 0.07f);
        dob.transform.localRotation = Quaternion.FromToRotation(Vector3.right, dir);
        float w = largura - 0.16f;
        var folha = MovelI(dob.transform, modelo, Vector3.zero, 0f, 0f, 1, 0f, true, new Vector3(w, altura - 0.04f, 0.09f), false);
        if (folha != null)
        {
            folha.transform.localPosition = new Vector3(w / 2f, 0.01f, 0f);
            folha.transform.localRotation = Quaternion.identity;
            folha.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            if (trancavel)
            {
                var obs = folha.AddComponent<NavMeshObstacle>();
                obs.shape = NavMeshObstacleShape.Box;
                var bc = folha.GetComponent<BoxCollider>();
                obs.center = bc.center;
                obs.size = bc.size + new Vector3(0.1f, 0f, 0.3f);
                obs.carving = true;
                obs.carveOnlyStationary = false;
            }
        }
        var p = dob.AddComponent<PortaInterior>();
        p.anguloAberta = anguloAberta;
        p.aberta = aberta;
        if (aberta) dob.transform.localRotation *= Quaternion.Euler(0f, anguloAberta, 0f);
        return p;
    }

    // zumbis fixos

    static GameObject PrefabZumbiQ(string tipo) => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Zumbis/Q_{tipo}.prefab");

    // zumbi parado num ponto (vira pro "olhar"); raio = distância em que ele percebe o Marcos
    static ZombieAI ZumbiFixo(Transform grupo, string tipo, Vector3 posLocal, float yaw, float raio)
    {
        var prefab = PrefabZumbiQ(tipo) ?? PrefabZumbiQ("Comum");
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grupo);
        go.name = "Zumbi_" + tipo;
        Vector3 mundo = grupo.TransformPoint(posLocal);
        if (NavMesh.SamplePosition(mundo, out var h, 1.5f, NavMesh.AllAreas)) mundo = h.position;
        else Debug.LogWarning($"[Sinal da Lia] Zumbi fixo fora do NavMesh em {posLocal} ({grupo.name})");
        go.transform.position = mundo;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var ai = go.GetComponent<ZombieAI>();
        ai.ficarParado = true;
        ai.voltarAoPool = false;
        ai.raioDeteccao = raio;
        EditorUtility.SetDirty(ai);
        return ai;
    }

    // interativos

    static Interativo Interacao(Transform pai, string id, string texto, Vector3 pos, float raio = 1.7f)
    {
        var go = new GameObject("E_" + id);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = pos;
        var i = go.AddComponent<Interativo>();
        i.id = id;
        i.texto = texto;
        i.raio = raio;
        return i;
    }

    static Transform Ponto(Transform pai, string nome, Vector3 pos, float yaw = 0f)
    {
        var t = new GameObject(nome).transform;
        t.SetParent(pai, false);
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    // NavMesh dos interiores

    const string NAVMESH_INTERIORES = "Assets/Scenes/Game_NavMesh_Interiores.asset";

    static void BakeNavMeshInteriores(GameObject raiz)
    {
        var surface = GetOrAdd<NavMeshSurface>(raiz);
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.buildHeightMesh = true;
        if (surface.navMeshData != null) surface.RemoveData();
        surface.navMeshData = null;
        AssetDatabase.DeleteAsset(NAVMESH_INTERIORES);
        Physics.SyncTransforms();
        surface.BuildNavMesh();
        if (surface.navMeshData != null) AssetDatabase.CreateAsset(surface.navMeshData, NAVMESH_INTERIORES);
        EditorUtility.SetDirty(surface);
    }
}
