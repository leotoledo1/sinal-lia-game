using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

// Parte do montador que usa o "Post Apocalypse Pack" (Quaternius, assets/Quaternius/PostApocalypse):
// Marcos (Matt), 4 tipos de zumbi, Lia (Lis), armas e objetos de cenário. Todos usam a paleta Zombie_Atlas.png.
public static partial class SinalDaLiaSetup
{
    const string QPA = "Assets/Quaternius/PostApocalypse/";
    const string Q_ATLAS = QPA + "Zombie_Atlas.png";

    static bool TemQuaternius => File.Exists(QPA + "Characters Matt/Characters_Matt.fbx") && File.Exists(Q_ATLAS);

    // alturas da MALHA (do pé ao topo da cabeça). A cidade tem casas de ~5,5 m e prédios de ~10 m.
    internal const float ALTURA_MARCOS = 1.70f;
    internal const float ALTURA_LIA = 1.15f;
    internal const float ALTURA_ZUMBI = 1.72f;

    // paleta / importação

    static Material MatAtlas()
    {
        var imp = AssetImporter.GetAtPath(Q_ATLAS) as TextureImporter;
        if (imp != null && (imp.filterMode != FilterMode.Point || imp.mipmapEnabled))
        {
            imp.filterMode = FilterMode.Point; // paleta: cores chapadas, sem borrar
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        var m = Mat("Quaternius_Atlas", Color.white, false, AssetDatabase.LoadAssetAtPath<Texture2D>(Q_ATLAS));
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        return m;
    }

    // os sobreviventes vêm com faca/arma branca grudada na mão: esconde (a arma é a do jogo)
    static void EsconderFaca(GameObject modelo)
    {
        // (a Lis/Lia vinha com uma pistola na mão; a arma do Marcos é criada pelo jogo, separada do modelo)
        string[] armasBrancas = { "knife", "axe", "bat", "spear", "machete", "pistol", "gun", "rifle", "smg" };
        foreach (var r in modelo.GetComponentsInChildren<Renderer>(true))
            if (armasBrancas.Any(n => r.name.ToLower().Contains(n))) r.gameObject.SetActive(false);
    }

    static void AplicarAtlas(GameObject go, Material mat = null)
    {
        if (mat == null) mat = MatAtlas();
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
    }

    static readonly string[] ClipsEmLoop = { "Idle", "Idle_Gun", "Walk", "Walk_Gun", "Run", "Run_Gun", "Run_Arms", "Crawl", "Idle_Attack", "Duck", "Jump_Idle" };

    // animações de andar/correr/parado precisam de loop (o FBX vem sem)
    static bool ConfigurarImportQ(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) return false;
        if (imp.clipAnimations.Length > 0) return true;
        imp.animationType = ModelImporterAnimationType.Generic;
        var clips = imp.defaultClipAnimations;
        foreach (var c in clips) c.loopTime = ClipsEmLoop.Contains(c.name.Split('|').Last());
        imp.clipAnimations = clips;
        imp.SaveAndReimport();
        return true;
    }

    static AnimationClip ClipQ(string path, string nome)
    {
        if (string.IsNullOrEmpty(nome)) return null;
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        return clips.FirstOrDefault(c => c.name == "CharacterArmature|" + nome)
            ?? clips.FirstOrDefault(c => c.name.EndsWith("|" + nome))
            ?? clips.FirstOrDefault(c => c.name == nome);
    }

    // mede pelos ossos (as caixas de limite do FBX não são confiáveis com a escala 100 do esqueleto)
    internal static void MedirOssos(GameObject inst, out float pe, out float topo, out Vector3 quadril, out float pescoco, out Vector3 frente)
    {
        var ossos = new HashSet<Transform>();
        foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            foreach (var b in smr.bones)
                if (b != null)
                {
                    ossos.Add(b);
                    foreach (Transform f in b) if (f.name.EndsWith("_end")) ossos.Add(f);
                }
        pe = ossos.Count > 0 ? ossos.Min(t => t.position.y) : inst.transform.position.y;
        topo = ossos.Count > 0 ? ossos.Max(t => t.position.y) : pe + 1.8f;
        var todos = inst.GetComponentsInChildren<Transform>();
        var hips = todos.FirstOrDefault(t => t.name == "Hips");
        quadril = hips != null ? hips.position : inst.transform.position;
        var neck = todos.FirstOrDefault(t => t.name == "Neck");
        pescoco = neck != null ? neck.position.y : pe + (topo - pe) * 0.82f;
        var pes = todos.FirstOrDefault(t => t.name == "Foot.L");
        var ponta = todos.FirstOrDefault(t => t.name == "Foot.L_end");
        frente = pes != null && ponta != null ? ponta.position - pes.position : Vector3.forward;
        frente.y = 0f;
        if (frente.sqrMagnitude < 1e-6f) frente = Vector3.forward;
    }

    // escala, põe o pé no chão, centraliza o quadril e vira pra frente (+Z do pai)
    static void Encaixar(GameObject modelo, Transform pai, float escala)
    {
        modelo.transform.localScale *= escala;
        MedirOssos(modelo, out float pe, out _, out Vector3 quadril, out _, out Vector3 frente);
        modelo.transform.position += new Vector3(pai.position.x - quadril.x, pai.position.y - pe, pai.position.z - quadril.z);
        float ang = Vector3.SignedAngle(frente, pai.forward, Vector3.up);
        modelo.transform.RotateAround(pai.position, Vector3.up, ang);
    }

    static float Altura(GameObject inst)
    {
        MedirOssos(inst, out float pe, out float topo, out _, out _, out _);
        return topo - pe;
    }

    // altura REAL da malha na pose atual (os ossos não chegam no topo da cabeçona do estilo Quaternius:
    // pelos ossos o Marcos tinha 1,78 m, mas a malha tinha 2,09 m - por isso todo mundo parecia gigante)
    internal static void MedirMalha(GameObject inst, out float pe, out float topo)
    {
        pe = float.MaxValue;
        topo = float.MinValue;
        foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!smr.gameObject.activeInHierarchy || smr.sharedMesh == null) continue;
            foreach (var p in VerticesNoMundo(smr))
            {
                if (p.y < pe) pe = p.y;
                if (p.y > topo) topo = p.y;
            }
        }
        if (pe == float.MaxValue)
        {
            MedirOssos(inst, out pe, out topo, out _, out _, out _);
        }
    }

    // posição de cada vértice com a pose atual dos ossos (skinning feito na mão: exato, sem depender de escala do BakeMesh)
    internal static Vector3[] VerticesNoMundo(SkinnedMeshRenderer smr)
    {
        var mesh = smr.sharedMesh;
        var verts = mesh.vertices;
        var pesos = mesh.boneWeights;
        var bind = mesh.bindposes;
        var ossos = smr.bones;
        var res = new Vector3[verts.Length];
        if (pesos.Length != verts.Length || ossos.Length == 0 || bind.Length != ossos.Length)
        {
            var m = smr.transform.localToWorldMatrix;
            for (int i = 0; i < verts.Length; i++) res[i] = m.MultiplyPoint3x4(verts[i]);
            return res;
        }
        var mats = new Matrix4x4[ossos.Length];
        for (int b = 0; b < ossos.Length; b++)
            mats[b] = ossos[b] != null ? ossos[b].localToWorldMatrix * bind[b] : smr.transform.localToWorldMatrix;
        for (int i = 0; i < verts.Length; i++)
        {
            var w = pesos[i];
            var v = verts[i];
            res[i] = mats[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0
                   + mats[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                   + mats[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2
                   + mats[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
        }
        return res;
    }

    internal static float AlturaMalha(GameObject inst)
    {
        MedirMalha(inst, out float pe, out float topo);
        return topo - pe;
    }

    // ajusta a escala pra malha ficar com a altura pedida (mantém pé no chão e centrado)
    static void AjustarAlturaMalha(GameObject modelo, Transform pai, float altura)
    {
        float h = AlturaMalha(modelo);
        if (h > 0.2f) Encaixar(modelo, pai, altura / h);
    }

    // Animator

    static AnimatorController CriarControllerQ(string path, string fbx, string idle, string walk, string run, string morte, string ataque, string dano)
    {
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter(new AnimatorControllerParameter { name = "VelAnim", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        ctrl.AddParameter("Morrer", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Atacar", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Dano", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;
        var loco = ctrl.CreateBlendTreeInController("Locomocao", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        var cIdle = ClipQ(fbx, idle);
        var cWalk = ClipQ(fbx, walk);
        var cRun = ClipQ(fbx, run);
        if (cIdle != null) tree.AddChild(cIdle, 0f);
        if (cWalk != null) tree.AddChild(cWalk, 0.45f);
        if (cRun != null) tree.AddChild(cRun, 1f);
        loco.speedParameterActive = true;  // VelAnim = -1: anda de costas
        loco.speedParameter = "VelAnim";
        sm.defaultState = loco;

        var cMorte = ClipQ(fbx, morte);
        if (cMorte != null)
        {
            var st = sm.AddState("Morte");
            st.motion = cMorte;
            var t = sm.AddAnyStateTransition(st);
            t.AddCondition(AnimatorConditionMode.If, 0f, "Morrer");
            t.duration = 0.1f;
            t.canTransitionToSelf = false;
        }
        void Golpe(string estado, string clip, string gatilho)
        {
            var c = ClipQ(fbx, clip);
            if (c == null) return;
            var s = sm.AddState(estado);
            s.motion = c;
            var ida = sm.AddAnyStateTransition(s);
            ida.AddCondition(AnimatorConditionMode.If, 0f, gatilho);
            ida.duration = 0.08f;
            ida.canTransitionToSelf = false;
            var volta = s.AddTransition(loco);
            volta.hasExitTime = true;
            volta.exitTime = 0.85f;
            volta.duration = 0.15f;
        }
        Golpe("Ataque", ataque, "Atacar");
        Golpe("Dano", dano, "Dano");
        return ctrl;
    }

    // Marcos

    static bool CriarVisualQuaternius(Transform visual, PlayerWeapons pw)
    {
        string fbx = QPA + "Characters Matt/Characters_Matt.fbx";
        if (!ConfigurarImportQ(fbx)) return false;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var modelo = (GameObject)PrefabUtility.InstantiatePrefab(asset, visual);
        modelo.name = "Modelo";
        AplicarAtlas(modelo);
        EsconderFaca(modelo);
        var idle = ClipQ(fbx, "Idle_Gun") ?? ClipQ(fbx, "Idle");
        if (idle != null) idle.SampleAnimation(modelo, 0.2f);
        float h = Altura(modelo);
        Encaixar(modelo, visual, h > 0.3f ? 1.78f / h : 1f);
        AjustarAlturaMalha(modelo, visual, ALTURA_MARCOS);

        var anim = GetOrAdd<Animator>(modelo);
        anim.runtimeAnimatorController = CriarControllerQ("Assets/Characters/Marcos_Quaternius.controller", fbx, "Idle_Gun", "Walk_Gun", "Run_Gun", "Death", null, null);
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var ponto = CriarPontoArma(modelo, visual);
        if (ponto != null) pw.pontoArma = ponto;
        return true;
    }

    // ponto da arma: palma da mão direita, apontando pra frente do personagem
    static Transform CriarPontoArma(GameObject modelo, Transform visual)
    {
        var todos = modelo.GetComponentsInChildren<Transform>();
        var antebraco = todos.FirstOrDefault(t => t.name == "LowerArm.R");
        var dedos = todos.Where(t => t.name == "Index1.R" || t.name == "Middle1.R" || t.name == "Pinky1.R").ToArray();
        if (antebraco == null || dedos.Length == 0) return null;
        var ponto = new GameObject("PontoArma").transform;
        ponto.position = dedos.Aggregate(Vector3.zero, (s, t) => s + t.position) / dedos.Length;
        ponto.rotation = Quaternion.LookRotation(visual.forward, Vector3.up);
        ponto.SetParent(antebraco, true); // mantém escala 1 no mundo
        return ponto;
    }

    // zumbis

    struct TipoZumbi
    {
        public string nome, fbx, ataque;
        public float vida, vel, dano, raio, altura, alcance, maxAltura;
        public int peso;
    }

    static GameObject[] CriarZumbisQuaternius()
    {
        Pasta("Assets/Prefabs/Zumbis");
        var tipos = new[]
        {
            // maxAltura = altura máxima da MALHA (m). Marcos tem 1,70 m.
            new TipoZumbi { nome = "Comum", fbx = "Zombie/Zombie_Basic.fbx", ataque = "Punch", vida = 100, vel = 2.8f, dano = 10, raio = 0.28f, altura = 1.72f, alcance = 1.5f, peso = 6, maxAltura = 1.75f },
            new TipoZumbi { nome = "Gordao", fbx = "Zombie/Zombie_Chubby.fbx", ataque = "Punch", vida = 190, vel = 2.1f, dano = 15, raio = 0.38f, altura = 1.7f, alcance = 1.6f, peso = 2, maxAltura = 1.78f },
            new TipoZumbi { nome = "Rastejante", fbx = "Zombie half/Zombie_Ribcage.fbx", ataque = "Jump", vida = 55, vel = 2.0f, dano = 8, raio = 0.3f, altura = 1.1f, alcance = 1.35f, peso = 2, maxAltura = 1.15f },
            new TipoZumbi { nome = "Brutamontes", fbx = "Big arm/Zombie_Arm.fbx", ataque = "Punch", vida = 260, vel = 2.4f, dano = 24, raio = 0.42f, altura = 2.25f, alcance = 1.9f, peso = 1, maxAltura = 2.3f },
        };
        var mat = MatAtlas();

        // um fator de escala só pro pacote todo (medido no zumbi comum), mantendo a proporção entre eles
        float fator = 1f;
        string basePath = QPA + tipos[0].fbx;
        if (ConfigurarImportQ(basePath))
        {
            var tmp = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(basePath));
            var idle = ClipQ(basePath, "Idle");
            if (idle != null) idle.SampleAnimation(tmp, 0.2f);
            float h = AlturaMalha(tmp);
            if (h > 0.2f) fator = ALTURA_ZUMBI / h;
            Object.DestroyImmediate(tmp);
        }

        var lista = new List<GameObject>();
        foreach (var t in tipos)
        {
            string fbx = QPA + t.fbx;
            if (!ConfigurarImportQ(fbx)) continue;
            var root = new GameObject("Zumbi_" + t.nome);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var modelo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), visual);
            modelo.name = "Modelo";
            AplicarAtlas(modelo, mat);
            var idle = ClipQ(fbx, "Idle");
            if (idle != null) idle.SampleAnimation(modelo, 0.2f);
            Encaixar(modelo, visual, fator);
            // alguns modelos do pacote são bem maiores: limita a altura (medida na malha, com a cabeça)
            float hz = AlturaMalha(modelo);
            if (hz > t.maxAltura) Encaixar(modelo, visual, t.maxAltura / hz);
            MedirOssos(modelo, out float pe, out _, out _, out float pescoco, out _);
            float alturaReal = Mathf.Min(AlturaMalha(modelo), t.maxAltura + 0.05f);

            var anim = GetOrAdd<Animator>(modelo);
            anim.runtimeAnimatorController = CriarControllerQ($"Assets/Prefabs/Zumbis/Anim_{t.nome}.controller", fbx, "Idle", "Walk", "Run", "Death", t.ataque, "HitReact");
            anim.applyRootMotion = false;

            // colisor do tamanho do corpo de verdade (tiro na cabeça pega a cabeçona inteira)
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, alturaReal / 2f, 0f);
            col.height = alturaReal;
            col.radius = t.raio;
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = t.raio + 0.04f;
            agent.height = Mathf.Max(1f, alturaReal);
            agent.speed = t.vel * 0.45f;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = t.alcance * 0.75f;
            var ai = root.AddComponent<ZombieAI>();
            ai.tipo = t.nome;
            ai.modelo = modelo.transform;
            ai.animator = anim;
            ai.velocidadePerseguir = t.vel;
            ai.velocidadeVagar = t.vel * 0.4f;
            ai.velocidadeAnimacaoRef = t.vel;
            ai.danoAtaque = t.dano;
            ai.alcanceAtaque = t.alcance;
            ai.alturaCabeca = Mathf.Max(0.3f, pescoco - pe);
            root.AddComponent<ZombieHealth>().vidaBase = t.vida;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"Assets/Prefabs/Zumbis/Q_{t.nome}.prefab");
            Object.DestroyImmediate(root);
            for (int i = 0; i < t.peso; i++) lista.Add(prefab);
        }
        return lista.ToArray();
    }

    // Lia

    static GameObject CriarLia(Transform raiz, Vector3 posClinica)
    {
        string fbx = QPA + "Lis/Characters_Lis.fbx";
        if (!ConfigurarImportQ(fbx)) return CriarLiaPlaceholder(raiz, posClinica);
        var go = new GameObject("Lia");
        go.transform.SetParent(raiz);
        go.transform.SetPositionAndRotation(posClinica + new Vector3(2.2f, 0f, 2.2f), Quaternion.Euler(0f, 225f, 0f));
        var visual = new GameObject("Visual").transform;
        visual.SetParent(go.transform, false);
        var modelo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), visual);
        modelo.name = "Modelo";
        AplicarAtlas(modelo);
        EsconderFaca(modelo);
        var idle = ClipQ(fbx, "Idle");
        if (idle != null) idle.SampleAnimation(modelo, 0.2f);
        float h = Altura(modelo);
        Encaixar(modelo, visual, h > 0.3f ? 1.25f / h : 0.75f);
        AjustarAlturaMalha(modelo, visual, ALTURA_LIA); // tamanho de criança de 9 anos (com a cabeçona)

        // escondida (agachada) -> acena quando o pai chega -> anda/corre atrás dele (Speed); + morte e reação ao dano
        string path = "Assets/Characters/Lia_Quaternius.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Achou", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Morrer", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Dano", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;
        var escondida = sm.AddState("Escondida");
        escondida.motion = ClipQ(fbx, "Duck") ?? idle;
        var acenar = sm.AddState("Acenar");
        acenar.motion = ClipQ(fbx, "Wave") ?? idle;
        var loco = ctrl.CreateBlendTreeInController("Locomocao", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        if (idle != null) tree.AddChild(idle, 0f);
        var walk = ClipQ(fbx, "Walk");
        var run = ClipQ(fbx, "Run");
        if (walk != null) tree.AddChild(walk, 0.45f);
        if (run != null) tree.AddChild(run, 1f);
        sm.defaultState = escondida;
        var t1 = escondida.AddTransition(acenar);
        t1.AddCondition(AnimatorConditionMode.If, 0f, "Achou");
        t1.hasExitTime = false;
        t1.duration = 0.2f;
        var t2 = acenar.AddTransition(loco);
        t2.hasExitTime = true;
        t2.exitTime = 0.95f;
        t2.duration = 0.25f;
        var morte = sm.AddState("Morte");
        morte.motion = ClipQ(fbx, "Death");
        var tm = sm.AddAnyStateTransition(morte);
        tm.AddCondition(AnimatorConditionMode.If, 0f, "Morrer");
        tm.duration = 0.1f;
        tm.canTransitionToSelf = false;
        var dano = sm.AddState("Dano");
        dano.motion = ClipQ(fbx, "HitReact");
        var td = loco.AddTransition(dano);
        td.AddCondition(AnimatorConditionMode.If, 0f, "Dano");
        td.hasExitTime = false;
        td.duration = 0.08f;
        var tv = dano.AddTransition(loco);
        tv.hasExitTime = true;
        tv.exitTime = 0.85f;
        tv.duration = 0.15f;

        var anim = GetOrAdd<Animator>(modelo);
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        go.AddComponent<LiaNPC>().animator = anim;

        // anda pelo NavMesh atrás do pai na noite final (o LiaNPC liga na hora)
        var agent = go.AddComponent<NavMeshAgent>();
        agent.radius = 0.22f;
        agent.height = 1.2f;
        agent.speed = 3.2f;
        agent.angularSpeed = 540f;
        agent.acceleration = 14f;
        agent.stoppingDistance = 0.3f;
        agent.enabled = false;
        return go;
    }

    static GameObject CriarLiaPlaceholder(Transform raiz, Vector3 posClinica)
    {
        var lia = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        lia.name = "Lia (placeholder)";
        Object.DestroyImmediate(lia.GetComponent<Collider>());
        lia.transform.SetParent(raiz);
        lia.transform.position = posClinica + new Vector3(1.5f, 0.6f, 0f);
        lia.transform.localScale = new Vector3(0.45f, 0.6f, 0.45f);
        lia.GetComponent<Renderer>().sharedMaterial = Mat("Lia", new Color(1f, 0.45f, 0.7f));
        lia.AddComponent<LiaNPC>();
        return lia;
    }

    // armas

    static void ArmasQuaternius(WeaponData[] armas)
    {
        var arquivos = new Dictionary<string, (string fbx, float comp)>
        {
            // personagens do pacote têm mãos grandes: arma "de verdade" (0,24 m) sumia dentro da mão
            { "Pistola", ("Pistol/Pistol.fbx", 0.36f) },
            { "Escopeta", ("Shotgun/Shotgun.fbx", 1.0f) },
            { "Rifle", ("Smg/SMG.fbx", 0.72f) },
            { "Sniper", ("Rifle/Rifle.fbx", 1.2f) },
        };
        foreach (var a in armas)
        {
            // progressão: começa só com a pistola; as outras são achadas no mapa
            a.liberadaNoInicio = a.nome == "Pistola";
            if (arquivos.TryGetValue(a.nome, out var info))
            {
                var m = AssetDatabase.LoadAssetAtPath<GameObject>(QPA + info.fbx);
                if (m != null)
                {
                    a.modelo = m;
                    a.comprimentoModelo = info.comp;
                    AlinharArma(a);
                }
            }
            EditorUtility.SetDirty(a);
        }
    }

    // descobre pela malha onde é o cano (frente) e o cabo (baixo/atrás) e calcula a pose na mão
    static void AlinharArma(WeaponData a)
    {
        var inst = (GameObject)Object.Instantiate(a.modelo);
        var vs = new List<Vector3>();
        foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null)
                foreach (var v in mf.sharedMesh.vertices) vs.Add(inst.transform.InverseTransformPoint(mf.transform.TransformPoint(v)));
        Object.DestroyImmediate(inst);
        if (vs.Count < 8) return;

        var min = new Vector3(vs.Min(v => v.x), vs.Min(v => v.y), vs.Min(v => v.z));
        var max = new Vector3(vs.Max(v => v.x), vs.Max(v => v.y), vs.Max(v => v.z));
        var tam = max - min;
        var centro = (min + max) / 2f;
        var media = vs.Aggregate(Vector3.zero, (s, v) => s + v) / vs.Count;
        // eixo mais comprido = cano; segundo = altura (cabo)
        var eixos = new[] { 0, 1, 2 }.OrderByDescending(i => tam[i]).ToArray();
        int iCano = eixos[0], iAlto = eixos[1];
        // a parte de cima (corpo da arma) tem mais vértices que o cabo
        float sinalCima = Mathf.Sign(media[iAlto] - centro[iAlto]);
        if (sinalCima == 0f) sinalCima = 1f;
        // cabo = parte de baixo; ele fica atrás do meio da arma
        float corteBaixo = sinalCima > 0f ? min[iAlto] + tam[iAlto] * 0.45f : max[iAlto] - tam[iAlto] * 0.45f;
        var cabo = vs.Where(v => sinalCima > 0f ? v[iAlto] < corteBaixo : v[iAlto] > corteBaixo).ToList();
        if (cabo.Count == 0) cabo = vs;
        var mCabo = cabo.Aggregate(Vector3.zero, (s, v) => s + v) / cabo.Count;
        float sinalFrente = Mathf.Sign(centro[iCano] - mCabo[iCano]);
        if (sinalFrente == 0f) sinalFrente = 1f;

        Vector3 frente = Vector3.zero, cima = Vector3.zero;
        frente[iCano] = sinalFrente;
        cima[iAlto] = sinalCima;
        var rot = Quaternion.Inverse(Quaternion.LookRotation(frente, cima));
        float escala = a.comprimentoModelo / Mathf.Max(0.0001f, tam[iCano]);
        // a mão segura no alto do cabo
        var mao = mCabo;
        mao[iAlto] = centro[iAlto] - sinalCima * tam[iAlto] * 0.05f;
        a.rotacaoNaMao = rot.eulerAngles;
        a.posicaoNaMao = -(rot * (mao * escala));
    }

    // objetos de cenário

    // instancia um objeto do pacote: paleta, tamanho (maior medida), pé no chão e colisor de caixa
    static GameObject InstQ(string arquivo, float tamanhoMax, Transform pai, bool colisor = true)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(QPA + arquivo);
        if (asset == null) return null;
        var raiz = new GameObject(Path.GetFileNameWithoutExtension(arquivo));
        raiz.transform.SetParent(pai, false);
        var m = (GameObject)PrefabUtility.InstantiatePrefab(asset, raiz.transform);
        AplicarAtlas(m);
        var b = Limites(m);
        float maior = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (maior > 0.001f) m.transform.localScale *= tamanhoMax / maior;
        b = Limites(m);
        // comprimento sempre no X local (barreiras, containers, bancos ficam "de lado" pra rua)
        if (b.size.z > b.size.x * 1.2f)
        {
            m.transform.RotateAround(b.center, Vector3.up, 90f);
            b = Limites(m);
        }
        var p = raiz.transform.position;
        m.transform.position += new Vector3(p.x - b.center.x, p.y - b.min.y, p.z - b.center.z);
        if (colisor)
        {
            b = Limites(m);
            var c = raiz.AddComponent<BoxCollider>();
            c.center = raiz.transform.InverseTransformPoint(b.center);
            c.size = b.size;
        }
        return raiz;
    }
}
