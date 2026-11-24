using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Importa o helicóptero "Helicopter" de Zsky (poly.pizza, CC BY 3.0) do arquivo .glb
// (o Unity não lê .glb sem pacote extra) e monta o prefab Assets/Prefabs/Helicoptero_Modelo.prefab:
//   Corpo  = malha com um material por cor (materiais salvos como asset: não ficam rosa na build)
//   Helice = pás da hélice principal separadas, com o pivô no eixo (o script Helicoptero gira)
//   Helice/Borrao = disco transparente que dá a impressão de hélice girando rápido
public static partial class SinalDaLiaSetup
{
    const string HELI_GLB = "Assets/Models/Helicoptero/Helicoptero_Zsky.glb";
    const string HELI_PASTA = "Assets/Models/Helicoptero";
    const string HELI_PREFAB = "Assets/Prefabs/Helicoptero_Modelo.prefab";
    const float HELI_COMPRIMENTO = 13f; // metros, do nariz à cauda (sem a hélice)

    [Serializable] class GltfJson { public GNode[] nodes; public GMesh[] meshes; public GAccessor[] accessors; public GView[] bufferViews; public GMat[] materials; }
    [Serializable] class GNode { public string name; public int mesh = -1; public int[] children; public float[] translation; public float[] rotation; public float[] scale; }
    [Serializable] class GMesh { public string name; public GPrim[] primitives; }
    [Serializable] class GPrim { public GAttr attributes; public int indices = -1; public int material = -1; }
    [Serializable] class GAttr { public int POSITION = -1; public int NORMAL = -1; }
    [Serializable] class GAccessor { public int bufferView; public int byteOffset; public int componentType; public int count; public string type; }
    [Serializable] class GView { public int byteOffset; public int byteLength; public int byteStride; }
    [Serializable] class GMat { public string name; public GPbr pbrMetallicRoughness; }
    [Serializable] class GPbr { public float[] baseColorFactor; }

    static GameObject CriarPrefabHelicoptero()
    {
        if (!File.Exists(HELI_GLB)) return null;
        byte[] b = File.ReadAllBytes(HELI_GLB);
        int lenJson = BitConverter.ToInt32(b, 12);
        var g = JsonUtility.FromJson<GltfJson>(Encoding.UTF8.GetString(b, 20, lenJson));
        int inicioBin = 20 + lenJson + 8;

        // matriz de cada nó com malha (pai -> filho), no espaço do glTF
        var matrizes = new Dictionary<int, Matrix4x4>();
        void Visitar(int i, Matrix4x4 pai)
        {
            var n = g.nodes[i];
            var t = n.translation != null && n.translation.Length == 3 ? new Vector3(n.translation[0], n.translation[1], n.translation[2]) : Vector3.zero;
            var r = n.rotation != null && n.rotation.Length == 4 ? new Quaternion(n.rotation[0], n.rotation[1], n.rotation[2], n.rotation[3]) : Quaternion.identity;
            var s = n.scale != null && n.scale.Length == 3 ? new Vector3(n.scale[0], n.scale[1], n.scale[2]) : Vector3.one;
            var m = pai * Matrix4x4.TRS(t, r, s);
            if (n.mesh >= 0) matrizes[i] = m;
            if (n.children != null) foreach (var c in n.children) Visitar(c, m);
        }
        var filhos = new HashSet<int>(g.nodes.Where(n => n.children != null).SelectMany(n => n.children));
        for (int i = 0; i < g.nodes.Length; i++) if (!filhos.Contains(i)) Visitar(i, Matrix4x4.identity);

        Vector3[] LerVec3(int acc)
        {
            var a = g.accessors[acc];
            var v = g.bufferViews[a.bufferView];
            int passo = v.byteStride > 0 ? v.byteStride : 12;
            int o = inicioBin + v.byteOffset + a.byteOffset;
            var res = new Vector3[a.count];
            for (int k = 0; k < a.count; k++)
                res[k] = new Vector3(BitConverter.ToSingle(b, o + k * passo), BitConverter.ToSingle(b, o + k * passo + 4), BitConverter.ToSingle(b, o + k * passo + 8));
            return res;
        }
        int[] LerIndices(int acc)
        {
            var a = g.accessors[acc];
            var v = g.bufferViews[a.bufferView];
            int o = inicioBin + v.byteOffset + a.byteOffset;
            var res = new int[a.count];
            for (int k = 0; k < a.count; k++)
                res[k] = a.componentType == 5125 ? (int)BitConverter.ToUInt32(b, o + k * 4)
                       : a.componentType == 5123 ? BitConverter.ToUInt16(b, o + k * 2) : b[o + k];
            return res;
        }

        // triângulos no espaço do Unity (glTF é destro: inverte o X e a ordem dos vértices)
        var tris = new List<(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, int mat)>();
        foreach (var kv in matrizes)
        {
            var mesh = g.meshes[g.nodes[kv.Key].mesh];
            var m = kv.Value;
            foreach (var p in mesh.primitives)
            {
                if (p.attributes.POSITION < 0) continue;
                var pos = LerVec3(p.attributes.POSITION);
                var nor = p.attributes.NORMAL >= 0 ? LerVec3(p.attributes.NORMAL) : null;
                var idx = p.indices >= 0 ? LerIndices(p.indices) : Enumerable.Range(0, pos.Length).ToArray();
                Vector3 P(int i) { var w = m.MultiplyPoint3x4(pos[i]); return new Vector3(-w.x, w.y, w.z); }
                Vector3 N(int i) { if (nor == null) return Vector3.up; var w = m.MultiplyVector(nor[i]).normalized; return new Vector3(-w.x, w.y, w.z); }
                for (int k = 0; k + 2 < idx.Length; k += 3)
                    tris.Add((P(idx[k]), P(idx[k + 2]), P(idx[k + 1]), N(idx[k]), N(idx[k + 2]), N(idx[k + 1]), Mathf.Max(0, p.material)));
            }
        }
        if (tris.Count == 0) return null;

        string NomeMat(int i) => g.materials != null && i < g.materials.Length ? g.materials[i].name ?? "" : "";
        int matCinza = Enumerable.Range(0, g.materials.Length).FirstOrDefault(i => NomeMat(i).StartsWith("Gray"));
        int matJanela = Enumerable.Range(0, g.materials.Length).FirstOrDefault(i => NomeMat(i).StartsWith("Window"));

        // topo do corpo = mais alto entre as peças que não são cinza (pás) nem preto (eixo do rotor)
        float topoCorpo = tris.Where(t => t.mat != matCinza && !NomeMat(t.mat).StartsWith("Black"))
                              .SelectMany(t => new[] { t.a.y, t.b.y, t.c.y }).Max();
        bool EhPa(int i) => tris[i].mat == matCinza && Mathf.Min(tris[i].a.y, Mathf.Min(tris[i].b.y, tris[i].c.y)) > topoCorpo - 0.001f;
        var pas = Enumerable.Range(0, tris.Count).Where(EhPa).ToList();

        // eixo do rotor = centro das pás; tamanho e frente (nariz pro +Z: lado das janelas)
        var todos = tris.SelectMany(t => new[] { t.a, t.b, t.c }).ToList();
        var corpoPts = Enumerable.Range(0, tris.Count).Where(i => !pas.Contains(i)).SelectMany(i => new[] { tris[i].a, tris[i].b, tris[i].c }).ToList();
        var bCorpo = new Bounds(corpoPts[0], Vector3.zero);
        foreach (var p in corpoPts) bCorpo.Encapsulate(p);
        bool giraX = bCorpo.size.x > bCorpo.size.z; // comprimento tem que ficar no Z
        var janela = tris.Where(t => t.mat == matJanela).SelectMany(t => new[] { t.a, t.b, t.c }).ToList();
        Vector3 cJanela = janela.Count > 0 ? janela.Aggregate(Vector3.zero, (s, p) => s + p) / janela.Count : bCorpo.center + Vector3.forward;
        Quaternion virar = Quaternion.identity;
        if (giraX) virar = Quaternion.Euler(0f, cJanela.x > bCorpo.center.x ? -90f : 90f, 0f);
        else if (cJanela.z < bCorpo.center.z) virar = Quaternion.Euler(0f, 180f, 0f);
        float escala = HELI_COMPRIMENTO / Mathf.Max(bCorpo.size.x, bCorpo.size.z);
        Vector3 centro = bCorpo.center;
        Vector3 Ajustar(Vector3 p) => virar * ((p - centro) * escala);

        Vector3 eixo = Vector3.zero;
        if (pas.Count > 0)
        {
            var pp = pas.SelectMany(i => new[] { tris[i].a, tris[i].b, tris[i].c }).Select(Ajustar).ToList();
            var bp = new Bounds(pp[0], Vector3.zero);
            foreach (var p in pp) bp.Encapsulate(p);
            eixo = new Vector3(bp.center.x, bp.min.y, bp.center.z);
        }

        Mesh Montar(IEnumerable<int> ids, Vector3 pivo, string nome, out int[] matsUsados)
        {
            var lista = ids.ToList();
            matsUsados = lista.Select(i => tris[i].mat).Distinct().OrderBy(x => x).ToArray();
            var vs = new List<Vector3>();
            var ns = new List<Vector3>();
            var subs = new List<List<int>>();
            foreach (int mi in matsUsados)
            {
                var sub = new List<int>();
                foreach (int i in lista.Where(i => tris[i].mat == mi))
                {
                    var t = tris[i];
                    foreach (var (v, n) in new[] { (t.a, t.na), (t.b, t.nb), (t.c, t.nc) })
                    {
                        sub.Add(vs.Count);
                        vs.Add(Ajustar(v) - pivo);
                        ns.Add(virar * n);
                    }
                }
                subs.Add(sub);
            }
            var mesh = new Mesh { name = nome };
            mesh.SetVertices(vs);
            mesh.SetNormals(ns);
            mesh.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s);
            mesh.RecalculateBounds();
            return mesh;
        }

        Pasta(HELI_PASTA);
        var meshCorpo = Montar(Enumerable.Range(0, tris.Count).Where(i => !pas.Contains(i)), Vector3.zero, "Helicoptero_Corpo", out var matsCorpo);
        var meshHelice = Montar(pas, eixo, "Helicoptero_Helice", out var matsHelice);
        string pathMalhas = HELI_PASTA + "/Helicoptero_Malhas.asset";
        AssetDatabase.DeleteAsset(pathMalhas);
        AssetDatabase.CreateAsset(meshCorpo, pathMalhas);
        AssetDatabase.AddObjectToAsset(meshHelice, pathMalhas);

        // materiais: cor do glTF é linear; o material do Unity espera a cor "de tela" (gamma)
        Material MatHeli(int i)
        {
            var f = g.materials[i].pbrMetallicRoughness?.baseColorFactor;
            var cor = f != null && f.Length >= 3 ? new Color(f[0], f[1], f[2]).gamma : Color.gray;
            var m = Mat("Heli_" + (NomeMat(i) == "" ? i.ToString() : NomeMat(i)), cor);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", i == matJanela ? 0.75f : 0.2f);
            return m;
        }

        var raiz = new GameObject("Helicoptero_Modelo");
        var corpo = new GameObject("Corpo");
        corpo.transform.SetParent(raiz.transform, false);
        corpo.AddComponent<MeshFilter>().sharedMesh = meshCorpo;
        corpo.AddComponent<MeshRenderer>().sharedMaterials = matsCorpo.Select(MatHeli).ToArray();
        var helice = new GameObject("Helice");
        helice.transform.SetParent(raiz.transform, false);
        helice.transform.localPosition = eixo;
        if (pas.Count > 0)
        {
            var pa = new GameObject("Pas");
            pa.transform.SetParent(helice.transform, false);
            pa.AddComponent<MeshFilter>().sharedMesh = meshHelice;
            var rp = pa.AddComponent<MeshRenderer>();
            rp.sharedMaterials = matsHelice.Select(MatHeli).ToArray();
            rp.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // disco transparente do tamanho das pás (efeito de hélice rápida)
            float raio = Mathf.Max(meshHelice.bounds.extents.x, meshHelice.bounds.extents.z);
            var borrao = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            borrao.name = "Borrao";
            UnityEngine.Object.DestroyImmediate(borrao.GetComponent<Collider>());
            borrao.transform.SetParent(helice.transform, false);
            borrao.transform.localPosition = Vector3.up * meshHelice.bounds.center.y;
            borrao.transform.localScale = new Vector3(raio * 2f, 0.01f, raio * 2f);
            var rb = borrao.GetComponent<Renderer>();
            rb.sharedMaterial = MatParticulaTransparente("Heli_Borrao", new Color(0.2f, 0.2f, 0.2f, 0.22f), null, false);
            rb.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        Pasta("Assets/Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(raiz, HELI_PREFAB);
        UnityEngine.Object.DestroyImmediate(raiz);
        Debug.Log($"[Sinal da Lia] Helicóptero importado: {tris.Count} triângulos ({pas.Count} na hélice), {matsCorpo.Length} cores, {HELI_COMPRIMENTO} m");
        return prefab;
    }

    // material base pros objetos criados durante o jogo (Resources sempre entra na build)
    static void CriarMaterialBase()
    {
        Pasta("Assets/Resources");
        const string path = "Assets/Resources/MaterialBase.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
        EditorUtility.SetDirty(m);
    }

    // vozes das falas: todo .wav de Assets/Audio/Falas (nome = id da fala)
    static AudioClip[] CarregarVozes()
    {
        const string pasta = "Assets/Audio/Falas";
        if (!AssetDatabase.IsValidFolder(pasta)) return new AudioClip[0];
        return AssetDatabase.FindAssets("t:AudioClip", new[] { pasta })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .Where(c => c != null).OrderBy(c => c.name).ToArray();
    }
}
