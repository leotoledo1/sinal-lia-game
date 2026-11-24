using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

// Diagnóstico visual (menu "Sinal da Lia > Diagnóstico (fotos e medidas)").
// Mede tamanhos (Marcos, zumbis, prédios, portas, carros, postes) e tira fotos de conferência
// em Logs/Fotos/diag_*.png + relatório em Logs/Diagnostico.txt
public class Diagnostico : MonoBehaviour
{
    public const string FLAG = "SinalDaLia_Diagnostico";
    readonly StringBuilder sb = new StringBuilder();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
#if UNITY_EDITOR
        if (!UnityEditor.SessionState.GetBool(FLAG, false)) return;
        UnityEditor.SessionState.SetBool(FLAG, false);
        var go = new GameObject("Diagnostico");
        DontDestroyOnLoad(go);
        go.AddComponent<Diagnostico>();
#endif
    }

    static Bounds Limites(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer) && !(r is LineRenderer)).ToArray();
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        return b;
    }

    static string V(Vector3 v) => $"{v.x:0.00} x {v.y:0.00} x {v.z:0.00}";

    // altura exata da malha deformada (pé ao topo da cabeça), skinning feito na mão
    static float AlturaMalha(GameObject go)
    {
        float pe = float.MaxValue, topo = float.MinValue;
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = smr.sharedMesh;
            if (mesh == null || !smr.gameObject.activeInHierarchy) continue;
            var verts = mesh.vertices;
            var pesos = mesh.boneWeights;
            var bind = mesh.bindposes;
            var ossos = smr.bones;
            bool skin = pesos.Length == verts.Length && ossos.Length > 0 && bind.Length == ossos.Length;
            var mats = new Matrix4x4[Mathf.Max(1, ossos.Length)];
            for (int b = 0; b < ossos.Length; b++) mats[b] = ossos[b] != null ? ossos[b].localToWorldMatrix * bind[b] : smr.transform.localToWorldMatrix;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p;
                if (!skin) p = smr.transform.localToWorldMatrix.MultiplyPoint3x4(verts[i]);
                else
                {
                    var w = pesos[i];
                    p = mats[w.boneIndex0].MultiplyPoint3x4(verts[i]) * w.weight0 + mats[w.boneIndex1].MultiplyPoint3x4(verts[i]) * w.weight1
                      + mats[w.boneIndex2].MultiplyPoint3x4(verts[i]) * w.weight2 + mats[w.boneIndex3].MultiplyPoint3x4(verts[i]) * w.weight3;
                }
                pe = Mathf.Min(pe, p.y);
                topo = Mathf.Max(topo, p.y);
            }
        }
        return pe == float.MaxValue ? 0f : topo - pe;
    }

    // renderiza uma câmera qualquer e salva em Logs/Fotos/<nome>.png
    static void Foto(Camera cam, string nome)
    {
        const int L = 1280, A = 720;
        var rt = RenderTexture.GetTemporary(L, A, 24);
        var antes = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = antes;
        var ativo = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(L, A, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, L, A), 0, 0);
        tex.Apply();
        RenderTexture.active = ativo;
        RenderTexture.ReleaseTemporary(rt);
        string dir = Path.Combine(Application.dataPath, "../Logs/Fotos");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, nome + ".png"), tex.EncodeToPNG());
        Destroy(tex);
    }

    Camera CameraTemp(Vector3 pos, Vector3 olhar, float fov)
    {
        var go = new GameObject("CamDiag");
        var c = go.AddComponent<Camera>();
        c.fieldOfView = fov;
        c.nearClipPlane = 0.02f;
        c.farClipPlane = 300f;
        c.enabled = false;
        var principal = Camera.main;
        if (principal != null)
        {
            c.clearFlags = principal.clearFlags;
            c.backgroundColor = principal.backgroundColor;
        }
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(olhar - pos);
        return c;
    }

    IEnumerator Start()
    {
        sb.AppendLine("DIAGNÓSTICO - " + System.DateTime.Now);
        yield return new WaitForSeconds(1f);
        var gm = GameManager.Instance;
        var ph = PlayerHealth.Instance;
        var pw = PlayerWeapons.Instance;
        if (gm == null || ph == null || pw == null) { sb.AppendLine("Faltou GameManager/jogador"); Fim(); yield break; }
        gm.Jogar();
        yield return null;
        gm.PularCena(true); // pula a abertura (texto da história)
        WaveSpawner.Instance?.Configurar(null);
        yield return new WaitForSeconds(1.5f);
        WaveSpawner.Instance?.LimparTodos();

        // medidas
        sb.AppendLine("\n== Personagens");
        var visual = ph.transform.Find("Visual");
        var bM = Limites(visual != null ? visual.gameObject : ph.gameObject);
        sb.AppendLine($"Marcos: altura {bM.size.y:0.00} m (bounds {V(bM.size)})");
        var ossos = ph.GetComponentsInChildren<Transform>();
        var cabeca = ossos.FirstOrDefault(t => t.name == "Head");
        var pescoco = ossos.FirstOrDefault(t => t.name == "Neck");
        if (pescoco != null) sb.AppendLine($"  pescoço a {pescoco.position.y - ph.transform.position.y:0.00} m; cabeça ocupa {bM.max.y - pescoco.position.y:0.00} m do topo");
        var animM = ph.GetComponentInChildren<Animator>();
        if (animM != null) sb.AppendLine($"  Animator: culling {animM.cullingMode}, update {animM.updateMode}, rootMotion {animM.applyRootMotion}");
        if (gm.lia != null)
        {
            gm.lia.SetActive(true);
            yield return null;
            sb.AppendLine($"Lia: altura {Limites(gm.lia).size.y:0.00} m");
        }
        if (WaveSpawner.Instance != null && WaveSpawner.Instance.variantes != null)
            foreach (var pz in WaveSpawner.Instance.variantes.Where(v => v != null).Distinct())
            {
                var z = Instantiate(pz, new Vector3(0f, -500f, 0f), Quaternion.identity);
                z.GetComponent<NavMeshAgent>().enabled = false;
                z.GetComponent<ZombieAI>().enabled = false;
                yield return null;
                sb.AppendLine($"Zumbi {pz.name}: altura {Limites(z).size.y:0.00} m");
                Destroy(z);
            }

        sb.AppendLine("\n== Cidade (tamanho real de cada tipo)");
        var gerador = FindAnyObjectByType<GeradorCidade>();
        var vistos = new Dictionary<string, Vector3>();
        if (gerador != null)
        {
            var cidade = gerador.transform.Find("CidadeGerada");
            if (cidade != null)
                foreach (Transform t in cidade)
                {
                    var mf = t.GetComponentInChildren<MeshFilter>();
                    string chave = (t.name.Split('_')[0]) + ":" + (mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : t.name);
                    if (vistos.ContainsKey(chave)) continue;
                    vistos[chave] = Limites(t.gameObject).size;
                }
            foreach (var kv in vistos.OrderBy(k => k.Key)) sb.AppendLine($"  {kv.Key}: {V(kv.Value)}");
            // portas e janelas (nome do objeto ou da malha)
            var portas = new Dictionary<string, Vector3>();
            foreach (var mf in gerador.GetComponentsInChildren<MeshFilter>())
            {
                string n = (mf.name + " " + (mf.sharedMesh != null ? mf.sharedMesh.name : "")).ToLower();
                if (!(n.Contains("door") || n.Contains("porta") || n.Contains("window") || n.Contains("stair"))) continue;
                string k = mf.sharedMesh != null ? mf.sharedMesh.name : mf.name;
                if (!portas.ContainsKey(k)) portas[k] = mf.GetComponent<Renderer>().bounds.size;
            }
            sb.AppendLine("  Portas/janelas/escadas:");
            foreach (var kv in portas.Take(25)) sb.AppendLine($"    {kv.Key}: {V(kv.Value)}");

            var cenario = gerador.transform.Find("Cenario_v02");
            if (cenario != null)
            {
                sb.AppendLine("\n== Cenário (1 de cada)");
                var tipos = new Dictionary<string, Vector3>();
                foreach (Transform t in cenario)
                {
                    string k = t.name.Replace("(Clone)", "").Trim();
                    if (!tipos.ContainsKey(k)) tipos[k] = Limites(t.gameObject).size;
                }
                foreach (var kv in tipos.OrderBy(k => k.Key)) sb.AppendLine($"  {kv.Key}: {V(kv.Value)}");
            }
        }

        // arma na mão
        sb.AppendLine("\n== Armas na mão");
        pw.LiberarTodas();
        var equipar = typeof(PlayerWeapons).GetMethod("Equipar", BindingFlags.Instance | BindingFlags.NonPublic);
        var campoModelos = typeof(PlayerWeapons).GetField("modelos", BindingFlags.Instance | BindingFlags.NonPublic);
        var modelos = campoModelos.GetValue(pw) as GameObject[];
        sb.AppendLine($"pontoArma: {(pw.pontoArma != null ? Caminho(pw.pontoArma) : "NULO")}");
        var maoD = ossos.FirstOrDefault(t => t.name == "Middle1.R");
        {
            var ante = ossos.FirstOrDefault(t => t.name == "LowerArm.R");
            var m3 = ossos.FirstOrDefault(t => t.name == "Middle3.R_end");
            var pa = ph.GetComponent<PlayerAnimator>();
            var campoDedos = typeof(PlayerAnimator).GetField("dedos", BindingFlags.Instance | BindingFlags.NonPublic);
            var dd = campoDedos != null ? campoDedos.GetValue(pa) as Transform[,] : null;
            for (int q = 0; q < 3; q++)
            {
                yield return null; // (WaitForEndOfFrame não dispara em -batchmode)
                if (ante != null && maoD != null && m3 != null)
                {
                    var i1 = ossos.FirstOrDefault(t => t.name == "Index1.R");
                    var p1 = ossos.FirstOrDefault(t => t.name == "Pinky1.R");
                    sb.AppendLine($"Dedos (quadro {q}): ângulo dedo x antebraço {Vector3.Angle(maoD.position - ante.position, m3.position - maoD.position):0}°, dedos achados {(dd != null ? dd.Cast<Transform>().Count(t => t != null) : -1)}, distância indicador-mínimo {(i1 != null && p1 != null ? Vector3.Distance(i1.position, p1.position) : -1f):0.000} m");
                }
            }
        }
        for (int i = 0; i < pw.armas.Length; i++)
        {
            equipar.Invoke(pw, new object[] { i });
            yield return null;
            yield return null;
            var m = modelos != null && i < modelos.Length ? modelos[i] : null;
            if (m == null) { sb.AppendLine($"  {pw.armas[i].nome}: SEM MODELO"); continue; }
            var b = Limites(m);
            var rs = m.GetComponentsInChildren<Renderer>();
            float dist = maoD != null ? Vector3.Distance(b.center, maoD.position) : -1f;
            sb.AppendLine($"  {pw.armas[i].nome}: ativo {m.activeInHierarchy}, {rs.Length} renderers (ligados {rs.Count(r => r.enabled)}), tamanho {V(b.size)}, centro a {dist:0.00} m da mão, escala {m.transform.lossyScale.x:0.000}, material {(rs.Length > 0 && rs[0].sharedMaterial != null ? rs[0].sharedMaterial.name : "-")}");
            if (pw.pontoArma != null)
            {
                Vector3 alvo = pw.pontoArma.position;
                var c = CameraTemp(alvo + ph.transform.right * 0.9f + Vector3.up * 0.25f + ph.transform.forward * 0.4f, alvo, 40f);
                Foto(c, "diag_arma_" + i + "_" + pw.armas[i].nome);
                Destroy(c.gameObject);
            }
        }
        equipar.Invoke(pw, new object[] { 0 });

        // régua: todos enfileirados, câmera ortográfica de lado, faixas de altura
        {
            Vector3 baseP = ph.transform.position;
            Vector3 dirCam = Vector3.forward, lado = Vector3.right;
            var fila = new List<GameObject>();
            void Faixa(float h, Color cor)
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(f.GetComponent<Collider>());
                f.transform.position = baseP + lado * 2.5f + Vector3.up * h + dirCam * 1.2f;
                f.transform.localScale = new Vector3(12f, 0.025f, 0.02f);
                var mr = f.GetComponent<Renderer>();
                mr.material.color = cor;
                if (mr.material.HasProperty("_BaseColor")) mr.material.SetColor("_BaseColor", cor);
                fila.Add(f);
            }
            Faixa(1.0f, Color.white);
            Faixa(1.5f, Color.cyan);
            Faixa(1.7f, Color.yellow);
            Faixa(2.0f, Color.red);
            ph.transform.rotation = Quaternion.LookRotation(-dirCam);
            if (gm.lia != null) { gm.lia.SetActive(true); gm.lia.transform.SetPositionAndRotation(baseP - lado * 1.3f, Quaternion.LookRotation(-dirCam)); }
            float x = 1.4f;
            if (WaveSpawner.Instance != null && WaveSpawner.Instance.variantes != null)
                foreach (var pz in WaveSpawner.Instance.variantes.Where(v => v != null).Distinct())
                {
                    var z = Instantiate(pz, baseP + lado * x, Quaternion.LookRotation(-dirCam));
                    z.GetComponent<NavMeshAgent>().enabled = false;
                    z.GetComponent<ZombieAI>().enabled = false;
                    fila.Add(z);
                    x += 1.5f;
                }
            yield return new WaitForSeconds(0.4f);
            var c = CameraTemp(baseP + lado * 2.3f + Vector3.up * 1.3f - dirCam * 8f, baseP + lado * 2.3f + Vector3.up * 1.3f, 50f);
            c.transform.rotation = Quaternion.LookRotation(dirCam);
            c.orthographic = true;
            c.orthographicSize = 1.5f;
            Foto(c, "diag_regua");
            Destroy(c.gameObject);
            foreach (var g in fila) Destroy(g);
            sb.AppendLine("\nRégua (foto diag_regua): faixas branca 1,0 m, ciano 1,5 m, amarela 1,7 m, vermelha 2,0 m. Ordem: Lia, Marcos, zumbis " +
                string.Join(", ", WaveSpawner.Instance != null ? WaveSpawner.Instance.variantes.Where(v => v != null).Distinct().Select(v => v.name) : new string[0]));
        }

        // helicóptero de perto
        if (gm.prefabHelicoptero != null)
        {
            var h = Helicoptero.Criar(ph.transform.position + Vector3.forward * 20f, null, gm.prefabHelicoptero);
            h.PularVoo();
            yield return null;
            yield return null;
            Vector3 hp = h.transform.position;
            var c = CameraTemp(hp + new Vector3(14f, -4f, 12f), hp, 50f);
            Foto(c, "diag_helicoptero");
            c.transform.position = hp + new Vector3(-6f, 9f, -16f);
            c.transform.rotation = Quaternion.LookRotation(hp - c.transform.position);
            Foto(c, "diag_helicoptero_cima");
            Destroy(c.gameObject);
            var rs = h.GetComponentsInChildren<Renderer>();
            sb.AppendLine($"\nHelicóptero: {rs.Length} renderers, materiais: {string.Join(", ", rs.SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct())}");
            Destroy(h.gameObject);
        }
        else sb.AppendLine("\nHelicóptero: SEM PREFAB (usa formas simples)");

        // câmera do jogo
        yield return new WaitForSeconds(0.3f);
        Foto(Camera.main, "diag_camera_jogo");
        var tpc = Camera.main.GetComponent<ThirdPersonCamera>();
        if (tpc != null) sb.AppendLine($"\n== Câmera: altura {tpc.alturaOmbro}, lado {tpc.ladoOmbro}, distância {tpc.distancia}/{tpc.distanciaMira}, FOV {tpc.fovNormal}/{tpc.fovMira}");

        // escala: Marcos + zumbi na frente de um prédio, câmera de lado
        Vector3 p0 = ph.transform.position;
        Vector3 melhorDir = Vector3.zero;
        float melhorDist = 99f;
        Vector3 pontoParede = Vector3.zero;
        for (int k = 0; k < 16; k++)
        {
            Vector3 d = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward;
            foreach (var h in Physics.RaycastAll(p0 + Vector3.up * 1f, d, 30f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (h.transform.IsChildOf(ph.transform)) continue;
                if (Limites(h.collider.gameObject).size.y < 4f) break; // carro/objeto pequeno: não serve
                if (h.distance < melhorDist) { melhorDist = h.distance; melhorDir = d; pontoParede = h.point; }
                break;
            }
        }
        if (melhorDir != Vector3.zero)
        {
            Vector3 pos = pontoParede - melhorDir * 1.6f;
            pos.y = p0.y;
            var cc = ph.GetComponent<CharacterController>();
            cc.enabled = false;
            ph.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-melhorDir));
            cc.enabled = true;
            Vector3 lado = new Vector3(melhorDir.z, 0f, -melhorDir.x);
            var zPrefab = WaveSpawner.Instance != null ? WaveSpawner.Instance.prefabZumbi : null;
            GameObject z = null;
            if (zPrefab != null)
            {
                z = Instantiate(zPrefab, pos + lado * 1.4f, Quaternion.LookRotation(-melhorDir));
                z.GetComponent<NavMeshAgent>().enabled = false;
                z.GetComponent<ZombieAI>().enabled = false;
            }
            if (gm.lia != null) gm.lia.transform.position = pos - lado * 1.2f;
            yield return new WaitForSeconds(0.5f);
            var c = CameraTemp(pos - melhorDir * 9f + Vector3.up * 1.6f, pos + Vector3.up * 2.5f, 55f);
            Foto(c, "diag_escala_frente_predio");
            c.transform.position = pos - melhorDir * 4f + lado * 6f + Vector3.up * 1.2f;
            c.transform.rotation = Quaternion.LookRotation(pos + Vector3.up * 1f - c.transform.position);
            Foto(c, "diag_escala_lado");
            Destroy(c.gameObject);
            sb.AppendLine($"\nFoto de escala: prédio a {melhorDist:0.0} m do início");
        }
        else sb.AppendLine("\nNão achei prédio perto pra foto de escala");

        // carro do lado do Marcos
        var carro = FindObjectsByType<Transform>().FirstOrDefault(t => t.name.StartsWith("Carro"));
        if (carro != null)
        {
            var bc = Limites(carro.gameObject);
            Vector3 pos = bc.center + Vector3.right * (bc.extents.x + 0.8f);
            pos.y = bc.min.y;
            var cc = ph.GetComponent<CharacterController>();
            cc.enabled = false;
            ph.transform.position = pos;
            cc.enabled = true;
            yield return new WaitForSeconds(0.3f);
            var c = CameraTemp(pos + new Vector3(2f, 1.5f, -7f), pos + Vector3.up * 0.9f, 50f);
            Foto(c, "diag_escala_carro");
            Destroy(c.gameObject);
            sb.AppendLine($"Carro {carro.name}: {V(bc.size)}");
        }
        Fim();
    }

    static string Caminho(Transform t)
    {
        var s = t.name;
        for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
        return s;
    }

    void Fim()
    {
        string path = Path.Combine(Application.dataPath, "../Logs/Diagnostico.txt");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Debug.Log("[Diagnostico] pronto: " + path);
#if UNITY_EDITOR
        if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
        else UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
