using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Interiores (escola e clínica). Parte 1: pacotes de móveis (CC0) em Assets/Models/Interiores/
//   Quaternius_Interior (123 modelos), Quaternius_Moveis (20) e Kenney (Furniture Kit, 140).
// Os zips baixados ficaram em _backup_assets/_Import_v14 (fora do projeto: OBJ/DAE/GLB/STL/prévias não são usados).
// Menu "Sinal da Lia > Interiores: inventário dos móveis": medidas + fotos de todos os modelos (Logs/Fotos/inv_*.png).
public static partial class SinalDaLiaSetup
{
    internal const string INTERIORES = "Assets/Models/Interiores/";
    static readonly string[] PacotesInteriores = { "Quaternius_Interior", "Quaternius_Moveis", "Kenney" };

    static bool TemInteriores => PacotesInteriores.All(p => AssetDatabase.IsValidFolder(INTERIORES + p));

    // FBX dos móveis: sem animação, câmera ou luz; conversão de eixo "assada" na malha (transform limpo)
    static void ConfigurarImportInteriores()
    {
        int n = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var pacote in PacotesInteriores)
                foreach (var path in ModelosDoPacote(pacote))
                {
                    if (!(AssetImporter.GetAtPath(path) is ModelImporter imp)) continue;
                    if (imp.animationType == ModelImporterAnimationType.None && !imp.importAnimation && !imp.importCameras
                        && !imp.importLights && imp.bakeAxisConversion && !imp.importBlendShapes) continue;
                    imp.animationType = ModelImporterAnimationType.None;
                    imp.importAnimation = false;
                    imp.importCameras = false;
                    imp.importLights = false;
                    imp.importBlendShapes = false;
                    imp.bakeAxisConversion = true;
                    imp.SaveAndReimport();
                    n++;
                }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        if (n > 0) Debug.Log($"[Sinal da Lia] Interiores: importação ajustada em {n} modelos");
    }

    // montagem (chamada pelo Montar Cena)

    internal const string RAIZ_INTERIORES = "Interiores_v14";

    // fachadas na rua (antes do NavMesh da cidade: o degrau e a moldura entram nele)
    static void MontarFachadas(GeradorCidade g, Vector3 posEscola, Vector3 posClinica, out InfoFachada escola, out InfoFachada clinica)
    {
        var velhas = g.transform.Find(FACHADAS);
        if (velhas != null) Object.DestroyImmediate(velhas.gameObject);
        escola = MontarFachada(g.transform, posEscola, "Escola", "ESCOLA MUNICIPAL ESPERANÇA", new Color(0.86f, 0.78f, 0.52f),
            new Color(0.95f, 0.93f, 0.86f), new Color(0.1f, 0.22f, 0.45f), false);
        clinica = MontarFachada(g.transform, posClinica, "Clinica", "CLÍNICA CENTRAL", new Color(0.85f, 0.86f, 0.84f),
            new Color(0.95f, 0.96f, 0.95f), new Color(0.7f, 0.06f, 0.05f), true);
        Debug.Log($"[Sinal da Lia] Fachadas: escola {(escola.achouParede ? "na parede de um prédio" : "solta")}, clínica {(clinica.achouParede ? "na parede de um prédio" : "solta")}");
    }

    // escola e clínica por dentro, longe da cidade (300 m pro lado e 500 m pra baixo); devolve o desenho da Lia
    static DesenhoLia MontarInteriores(Transform raiz, Vector3 origemCidade, InfoFachada fEscola, InfoFachada fClinica, GameObject lia)
    {
        ConfigurarImportInteriores();
        var ri = new GameObject(RAIZ_INTERIORES).transform;
        ri.SetParent(raiz, false);
        ri.position = origemCidade + new Vector3(-300f, -500f, 0f);
        var paraZumbis = new List<GameObject>();
        var esc = MontarEscolaInterior(ri, ri.position, fEscola, paraZumbis);
        var cli = MontarClinicaInterior(ri, ri.position + new Vector3(0f, 0f, 45f), fClinica);
        AssetDatabase.SaveAssets();
        BakeNavMeshInteriores(ri.gameObject);
        ZumbisEscola(esc);
        ZumbisClinica(cli);
        // a Lia fica agachada no nicho atrás do armário do depósito (olhando pra "porta")
        if (lia != null && cli.esconderijo != null)
        {
            Vector3 p = cli.esconderijo.position;
            p.y = cli.transform.position.y;
            lia.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, -90f, 0f));
        }
        int luzes = ri.GetComponentsInChildren<Light>(true).Length;
        Debug.Log($"[Sinal da Lia] Interiores: escola + clínica, {luzes} luzes, {ri.GetComponentsInChildren<ZombieAI>(true).Length} zumbis fixos, {nMalhas} malhas");
        return esc.desenho;
    }

    static string[] ModelosDoPacote(string pacote) =>
        AssetDatabase.FindAssets("t:Model", new[] { INTERIORES + pacote })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p).ToArray();

    // inventário (medidas + fotos)

    [MenuItem("Sinal da Lia/Interiores: inventário dos móveis")]
    public static void InventarioInteriores()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ConfigurarImportInteriores();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var sb = new StringBuilder();
        sb.AppendLine("INVENTÁRIO DOS MÓVEIS (v1.4) - " + System.DateTime.Now);
        sb.AppendLine("tamanho = largura X × altura Y × profundidade Z (m), como o FBX vem; base = y mais baixo; centro = deslocamento do meio em X/Z");
        sb.AppendLine("Fotos: câmera do lado +Z olhando pra -Z (girada 30°): o lado que aparece de frente é o +Z do modelo.\n");

        var luz = new GameObject("Luz").AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.transform.rotation = Quaternion.Euler(35f, 200f, 0f);
        luz.intensity = 1.3f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
        RenderSettings.fog = false;
        var fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        foreach (var pacote in PacotesInteriores)
        {
            var modelos = ModelosDoPacote(pacote);
            sb.AppendLine($"== {pacote} ({modelos.Length} modelos)");
            const int colunas = 7, linhas = 5, porFolha = colunas * linhas;
            for (int folha = 0; folha * porFolha < modelos.Length; folha++)
            {
                var raiz = new GameObject("Folha").transform;
                const float S = 2.6f;
                for (int k = 0; k < porFolha && folha * porFolha + k < modelos.Length; k++)
                {
                    string path = modelos[folha * porFolha + k];
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset == null) continue;
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                    var b = Limites(inst);
                    var mats = inst.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct()
                        .Select(m => $"{m.name}({CorHex(m)}{(m.mainTexture != null ? ",tex" : "")}{(m.shader != null && m.shader.name.Contains("Universal") ? "" : "," + (m.shader != null ? m.shader.name : "?"))})");
                    int verts = inst.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null).Sum(f => f.sharedMesh.vertexCount);
                    string nome = Path.GetFileNameWithoutExtension(path);
                    string tam = $"{b.size.x:0.00}x{b.size.y:0.00}x{b.size.z:0.00}";
                    sb.AppendLine($"{nome,-28} {b.size.x,5:0.00} × {b.size.y,5:0.00} × {b.size.z,5:0.00}  base {b.min.y:+0.00;-0.00}  centro {b.center.x:+0.00;-0.00}/{b.center.z:+0.00;-0.00}  {verts} vért  {string.Join(" ", mats)}");

                    // foto: cabe numa célula de 2 m, pé na prateleira da linha
                    int c = k % colunas, l = k / colunas;
                    var cel = new GameObject(nome).transform;
                    cel.SetParent(raiz, false);
                    cel.position = new Vector3(c * S, (linhas - 1 - l) * S, 0f);
                    inst.transform.SetParent(cel, true);
                    float maior = Mathf.Max(b.size.x, b.size.y, b.size.z);
                    if (maior > 0.001f) inst.transform.localScale *= Mathf.Min(1f, 1.9f / maior) * (maior < 0.4f ? 0.4f / maior : 1f);
                    b = Limites(inst);
                    inst.transform.position += cel.position - new Vector3(b.center.x, b.min.y, b.center.z);
                    var rot = new GameObject("Rotulo").AddComponent<TextMesh>();
                    rot.transform.SetParent(cel, false);
                    rot.transform.localPosition = new Vector3(0f, -0.08f, 0.9f);
                    rot.transform.rotation = Quaternion.Euler(12f, 210f, 0f);
                    rot.text = $"{nome}\n{tam}";
                    rot.font = fonte;
                    rot.fontSize = 40;
                    rot.characterSize = 0.045f;
                    rot.anchor = TextAnchor.UpperCenter;
                    rot.alignment = TextAlignment.Center;
                    rot.color = new Color(0.05f, 0.05f, 0.1f);
                    rot.GetComponent<MeshRenderer>().sharedMaterial = fonte.material;
                }
                var cam = new GameObject("Cam").AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = linhas * S / 2f + 0.6f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.66f, 0.7f, 0.74f);
                cam.transform.rotation = Quaternion.Euler(12f, 210f, 0f);
                Vector3 meio = new Vector3((colunas - 1) * S / 2f, (linhas - 1) * S / 2f + 0.4f, 0f);
                cam.transform.position = meio - cam.transform.forward * 30f;
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 80f;
                FotoEditor(cam, $"inv_{pacote}_{folha + 1}", 1750, 1250);
                Object.DestroyImmediate(cam.gameObject);
                Object.DestroyImmediate(raiz.gameObject);
            }
            sb.AppendLine();
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/Interiores_Inventario.txt", sb.ToString());
        Debug.Log("[Sinal da Lia] Inventário dos móveis: Logs/Interiores_Inventario.txt + Logs/Fotos/inv_*.png");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // fotos de conferência (planta + NavMesh + vistas)

    [MenuItem("Sinal da Lia/Interiores: fotos de conferência")]
    public static void FotosInteriores()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        var ri = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == RAIZ_INTERIORES);
        if (ri == null) { Debug.LogWarning("[Sinal da Lia] Sem interiores na cena"); if (Application.isBatchMode) EditorApplication.Exit(1); return; }
        RenderSettings.fog = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
        foreach (var t in ri.GetComponentsInChildren<Transform>(true))
            if (t.name == "Teto" || t.name.StartsWith("Light_Ceiling")) t.gameObject.SetActive(false);
        // zumbis aparecem (pra ver onde estão)
        foreach (var z in ri.GetComponentsInChildren<ZombieAI>(true)) { z.gameObject.SetActive(true); z.transform.parent.gameObject.SetActive(true); }
        var tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
        var malha = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        malha.vertices = tri.vertices.Select(v => v + Vector3.up * 0.06f).ToArray();
        malha.triangles = tri.indices;
        var matNav = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        matNav.SetColor("_BaseColor", new Color(0.1f, 1f, 0.25f, 0.4f));
        matNav.SetFloat("_Surface", 1f);
        matNav.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        matNav.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        matNav.SetFloat("_ZWrite", 0f);
        matNav.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        matNav.renderQueue = 3000;
        var nav = new GameObject("NavOverlay");
        nav.AddComponent<MeshFilter>().sharedMesh = malha;
        nav.AddComponent<MeshRenderer>().sharedMaterial = matNav;
        var cam = new GameObject("CamFoto").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.15f, 0.15f, 0.18f);
        cam.farClipPlane = 200f;
        var farol = new GameObject("Farol").AddComponent<Light>();
        farol.type = LightType.Point;
        farol.range = 18f;
        farol.intensity = 6f;
        farol.shadows = LightShadows.None;
        foreach (Transform interior in ri)
        {
            // só as malhas (partícula parada fora do Play tem caixa na origem do mundo)
            var rends = interior.GetComponentsInChildren<MeshRenderer>();
            var b = rends[0].bounds;
            foreach (var rd in rends) b.Encapsulate(rd.bounds);
            cam.orthographic = true;
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.transform.position = new Vector3(b.center.x, b.max.y + 20f, b.center.z);
            cam.orthographicSize = Mathf.Max(b.size.z / 2f, b.size.x / 2f / (1600f / 1000f)) + 0.8f;
            farol.enabled = false;
            nav.SetActive(false);
            FotoEditor(cam, $"int_{interior.name}_planta", 1600, 1000);
            nav.SetActive(true);
            FotoEditor(cam, $"int_{interior.name}_navmesh", 1600, 1000);
            nav.SetActive(false);
            // vistas na altura dos olhos (com uma luz junto da câmera)
            cam.orthographic = false;
            cam.fieldOfView = 72f;
            farol.enabled = true;
            var vistas = interior.name.StartsWith("Escola")
                ? new[] { (new Vector3(17f, 1.7f, 1.2f), 0f), (new Vector3(1.2f, 1.7f, 10f), 90f), (new Vector3(24f, 1.7f, 12.6f), -25f), (new Vector3(7.4f, 1.7f, 7.4f), -135f), (new Vector3(33f, 1.7f, 7.4f), -120f), (new Vector3(9f, 1.7f, 19.4f), 150f) }
                : new[] { (new Vector3(13f, 1.7f, 1.2f), 0f), (new Vector3(1.2f, 1.7f, 8.8f), 90f), (new Vector3(12.4f, 1.7f, 11.2f), -60f), (new Vector3(21f, 1.7f, 11.2f), 40f), (new Vector3(17.6f, 1.7f, 6.5f), 130f) };
            for (int k = 0; k < vistas.Length; k++)
            {
                cam.transform.position = interior.position + vistas[k].Item1;
                cam.transform.rotation = Quaternion.Euler(12f, vistas[k].Item2, 0f);
                farol.transform.position = cam.transform.position + Vector3.up * 0.3f;
                FotoEditor(cam, $"int_{interior.name}_vista{k + 1}", 1280, 720);
            }
        }
        Debug.Log("[Sinal da Lia] Fotos dos interiores em Logs/Fotos/int_*.png");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static string CorHex(Material m)
    {
        Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.color : Color.magenta);
        return "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    // renderiza uma câmera (fora do Play) e salva em Logs/Fotos/<nome>.png
    internal static void FotoEditor(Camera cam, string nome, int L = 1280, int A = 720)
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
        Directory.CreateDirectory("Logs/Fotos");
        File.WriteAllBytes($"Logs/Fotos/{nome}.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
}
