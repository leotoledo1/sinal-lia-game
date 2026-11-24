using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Menu "Sinal da Lia > Montar Cena v0.1": cria a cena Game.unity pronta pra jogar.
// Pode rodar de novo quantas vezes quiser (recria o que ele mesmo criou).
public static partial class SinalDaLiaSetup
{
    const string CENA_ORIGEM = "Assets/Scenes/SampleScene.unity";
    const string CENA_JOGO = "Assets/Scenes/Game.unity";
    const string NAVMESH_ASSET = "Assets/Scenes/Game_NavMesh.asset";
    const string PASTA_MARCOS = "Assets/Characters/Marcos";
    const string FBX_ZUMBI = "Assets/zombies_1/source/zombie_ani_player.fbx";
    const string TEX_ZUMBI = "Assets/zombies_1/textures/world_people_colors.png";
    const string TEX_ARMAS = "Assets/Weapons/Textures/atlass_1_diffuse.png";
    const string GUNS = "Assets/Weapons/Models/Guns/";
    const string RAIZ = "[SinalDaLia]";

    [MenuItem("Sinal da Lia/Montar Cena")]
    public static void MontarCena()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try
        {
            Progresso("Abrindo cena", 0.05f);
            var cena = AbrirCenaJogo();
            var antigo = GameObject.Find(RAIZ);
            if (antigo != null) Object.DestroyImmediate(antigo);
            var raiz = new GameObject(RAIZ).transform;

            Pasta("Assets/Materials");
            Pasta("Assets/Prefabs");
            Pasta("Assets/Data/Armas");

            Progresso("Gerando sons", 0.08f);
            GeradorSons.GerarTodos(false);

            Progresso("Gerando cidade", 0.15f);
            var gerador = Object.FindAnyObjectByType<GeradorCidade>();
            if (gerador == null)
            {
                EditorUtility.DisplayDialog("Sinal da Lia", "Não achei o GeradorDeCidade na cena.", "OK");
                return;
            }
            Vector3 origem = gerador.transform.position;
            float passo = (gerador.tamanhoQuarteirao + 1) * gerador.tamanhoTile;
            Vector2 tamanho = new Vector2(gerador.blocosX * passo, gerador.blocosZ * passo);
            GerarCidade(gerador, ref passo, ref tamanho);
            Vector3 PosCruz(int i, int j) => origem + new Vector3(i * passo, 0f, j * passo);

            Progresso("Montando cenário (carros, barricadas, fogo, postes)", 0.25f);
            MontarCenario(gerador, passo, PosCruz(0, 0),
                PosCruz(Mathf.Min(2, gerador.blocosX), Mathf.Min(2, gerador.blocosZ)), PosCruz(gerador.blocosX, gerador.blocosZ));

            // porta da escola e da clínica na rua (antes do NavMesh: o degrau entra nele)
            bool comInteriores = TemInteriores;
            InfoFachada fachadaEscola = default, fachadaClinica = default;
            if (comInteriores)
            {
                Progresso("Fachadas da escola e da clínica", 0.3f);
                ConfigurarImportInteriores();
                MontarFachadas(gerador, PosCruz(Mathf.Min(2, gerador.blocosX), Mathf.Min(2, gerador.blocosZ)), PosCruz(gerador.blocosX, gerador.blocosZ),
                    out fachadaEscola, out fachadaClinica);
            }
            else
            {
                var velhas = gerador.transform.Find(FACHADAS);
                if (velhas != null) Object.DestroyImmediate(velhas.gameObject);
            }

            Progresso("Bake do NavMesh", 0.35f);
            BakeNavMesh(gerador);

            Progresso("Criando materiais e armas", 0.5f);
            var armas = CriarArmas();
            if (TemQuaternius) ArmasQuaternius(armas);

            Progresso("Criando zumbi", 0.6f);
            var zumbis = TemQuaternius ? CriarZumbisQuaternius() : CriarPrefabsZumbi();

            Progresso("Criando jogador", 0.75f);
            Vector3 posInicio = NoChao(PosCruz(0, 0), origem.y);
            Vector3 posEscola = NoChao(PosCruz(Mathf.Min(2, gerador.blocosX), Mathf.Min(2, gerador.blocosZ)), origem.y);
            Vector3 posClinica = NoChao(PosCruz(gerador.blocosX, gerador.blocosZ), origem.y);

            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.farClipPlane = 200f;

            var jogador = CriarJogador(raiz, posInicio, cam, armas);
            // o Marcos começa olhando pra cidade (pro lado da escola), não pro muro
            Vector3 praEscola = posEscola - posInicio;
            praEscola.y = 0f;
            if (praEscola.sqrMagnitude > 0.01f) jogador.transform.rotation = Quaternion.LookRotation(praEscola);
            // anti-serrilhado (SMAA) + dithering (céu escuro sem "degraus")
            var dadosCam = cam.GetUniversalAdditionalCameraData();
            dadosCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            dadosCam.antialiasingQuality = AntialiasingQuality.High;
            dadosCam.dithering = true;
            dadosCam.renderPostProcessing = true;
            EditorUtility.SetDirty(dadosCam);

            var tpc = GetOrAdd<ThirdPersonCamera>(cam.gameObject);
            tpc.alvo = jogador.transform;
            tpc.jogador = jogador.GetComponent<PlayerController>();
            // por cima do ombro direito: o Marcos fica à esquerda e não tampa a mira.
            // Mais longe e com FOV maior: o Marcos ocupa ~1/3 da tela (antes ocupava metade e parecia gigante)
            tpc.alturaOmbro = 1.5f;
            tpc.ladoOmbro = 0.7f;
            tpc.distancia = 4.3f;
            tpc.distanciaMira = 2.4f;
            tpc.alturaMira = 1.8f;
            tpc.ladoMira = 0.95f;
            tpc.fovNormal = 62f;
            tpc.fovMira = 46f;
            tpc.pitchMin = -40f;
            tpc.pitchMax = 65f;
            tpc.sensibilidade = 0.1f;
            tpc.maxMovimentoPorQuadro = 1000f; // só corta "pulos" absurdos (80 travava giros rápidos)
            cam.transform.position = posInicio + new Vector3(0f, 2f, -4f);

            Progresso("Objetivos e gerenciadores", 0.9f);
            var escola = CriarMarcador(raiz, "Escola", posEscola, new Color(2.4f, 1.8f, 0.35f), out var farolEscola);
            var clinica = CriarMarcador(raiz, "Clinica", posClinica, new Color(0.45f, 2.4f, 0.8f), out var farolClinica);
            var lia = TemQuaternius ? CriarLia(raiz, posClinica) : CriarLiaPlaceholder(raiz, posClinica);
            // silhueta de cidade além do muro
            MontarHorizonte(gerador, origem + new Vector3(tamanho.x / 2f, 0f, tamanho.y / 2f), tamanho);
            // escola e clínica por dentro (o desenho fica na sala do 4ºB; a Lia, escondida no depósito da clínica)
            DesenhoLia desenho;
            if (comInteriores)
            {
                Progresso("Interiores da escola e da clínica", 0.82f);
                desenho = MontarInteriores(raiz, origem, fachadaEscola, fachadaClinica, lia);
                if (lia != null) LampiaoLia(raiz, lia.transform.position, new Vector3(0.38f, 0f, -0.45f));
            }
            else
            {
                desenho = CriarDesenho(raiz, posEscola);
                if (lia != null) LampiaoLia(raiz, lia.transform.position);
            }

            var jogo = new GameObject("[Jogo]");
            jogo.transform.SetParent(raiz);
            var gm = jogo.AddComponent<GameManager>();
            gm.jogador = jogador.transform;
            gm.escola = escola;
            gm.clinica = clinica;
            gm.farolEscola = farolEscola;
            gm.farolClinica = farolClinica;
            gm.lia = lia;
            gm.desenho = desenho;
            gm.origemCidade = origem;
            gm.limitesCidade = tamanho;
            var spawner = jogo.AddComponent<WaveSpawner>();
            spawner.variantes = zumbis;
            spawner.prefabZumbi = zumbis[0];
            var texZumbi = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_ZUMBI);
            spawner.materialRapido = TemQuaternius
                ? Mat("Atlas_Rapido", new Color(1f, 0.5f, 0.45f), false, AssetDatabase.LoadAssetAtPath<Texture2D>(Q_ATLAS))
                : Mat("Zumbi_Rapido", new Color(1f, 0.55f, 0.5f), false, texZumbi);
            var radioS = jogo.AddComponent<RadioSystem>();
            radioS.somChiado = GeradorSons.Carregar("radio_chiado");
            radioS.vozes = CarregarVozes(); // Assets/Audio/Falas/<id>.wav (Ferramentas/GerarVozes.ps1 ou gravação do grupo)
            CriarMaterialBase();
            gm.prefabHelicoptero = CriarPrefabHelicoptero();
            ConfigurarSom(jogo.AddComponent<SomManager>());
            jogo.AddComponent<Efeitos>().materialParticula = MatParticula("Particula", Color.white);
            jogo.AddComponent<ObjectiveManager>();
            var dn = jogo.AddComponent<DayNightController>();
            dn.cam = cam;
            dn.sol = Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            jogo.AddComponent<HUD>();
            ConfigurarVolume();
            ConfigurarCeu();

            // cena na Build (necessário pro "Recomeçar")
            var lista = EditorBuildSettings.scenes.Where(s => s.path != CENA_JOGO).ToList();
            lista.Insert(0, new EditorBuildSettingsScene(CENA_JOGO, true));
            EditorBuildSettings.scenes = lista.ToArray();

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = null; // Inspector aberto no jogador pesa durante o Play
            SinalDaLiaBuild.ConfigurarPlayer();
            EditorUtility.DisplayDialog("Sinal da Lia", "Cena Game.unity montada!\n\nAperte Play para testar (o jogo abre no menu principal).\nWASD andar · Shift correr · Mouse mirar/atirar · R recarregar · 1-4 armas · E interagir · Q kit médico · Esc pausa", "Bora");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void Progresso(string msg, float p) => EditorUtility.DisplayProgressBar("Sinal da Lia", msg + "...", p);

    static UnityEngine.SceneManagement.Scene AbrirCenaJogo()
    {
        if (File.Exists(CENA_JOGO)) return EditorSceneManager.OpenScene(CENA_JOGO);
        var cena = EditorSceneManager.OpenScene(CENA_ORIGEM);
        EditorSceneManager.SaveScene(cena, CENA_JOGO); // vira a cena Game.unity
        return cena;
    }

    static void Pasta(string caminho)
    {
        if (AssetDatabase.IsValidFolder(caminho)) return;
        string pai = Path.GetDirectoryName(caminho).Replace('\\', '/');
        Pasta(pai);
        AssetDatabase.CreateFolder(pai, Path.GetFileName(caminho));
    }

    // cidade

    static void GerarCidade(GeradorCidade g, ref float passo, ref Vector2 tamanho)
    {
        // tamanho de cidade jogável pra v0.1 (os valores antigos eram gigantes)
        if (g.blocosX > 8 || g.blocosZ > 8 || g.tamanhoQuarteirao > 4)
        {
            g.blocosX = 5;
            g.blocosZ = 5;
            g.tamanhoQuarteirao = 2;
        }
        if (g.seed == 0) g.seed = 1234;
        g.gerarAoIniciar = false;
        g.GerarCidade();
        EditorUtility.SetDirty(g);

        passo = (g.tamanhoQuarteirao + 1) * g.tamanhoTile;
        tamanho = new Vector2(g.blocosX * passo, g.blocosZ * passo);
        Vector3 centro = g.transform.position + new Vector3(tamanho.x / 2f, 0f, tamanho.y / 2f);

        // chão por baixo de tudo + paredes invisíveis nas bordas
        foreach (var nome in new[] { "Chao_v01", "Limites_v01" })
        {
            var velho = g.transform.Find(nome);
            if (velho != null) Object.DestroyImmediate(velho.gameObject);
        }
        // faixa de terra mais larga entre a cidade e o muro (árvores mortas, pedras, acampamento):
        // antes o muro ficava colado na rua e ocupava metade da tela
        float margem = MARGEM_MURO;
        var chao = GameObject.CreatePrimitive(PrimitiveType.Plane);
        chao.name = "Chao_v01";
        chao.transform.SetParent(g.transform);
        chao.transform.position = centro + Vector3.down * 0.03f;
        chao.transform.localScale = new Vector3((tamanho.x + margem * 2f) / 10f, 1f, (tamanho.y + margem * 2f) / 10f);
        var matChao = Mat("Chao", Color.white, false, TexturaTerra());
        matChao.SetTextureScale("_BaseMap", new Vector2((tamanho.x + margem * 2f) / 4f, (tamanho.y + margem * 2f) / 4f)); // repete a cada 4 m
        if (matChao.HasProperty("_Smoothness")) matChao.SetFloat("_Smoothness", 0.05f);
        chao.GetComponent<Renderer>().sharedMaterial = matChao;
        chao.isStatic = true;

        var limites = new GameObject("Limites_v01").transform;
        limites.SetParent(g.transform);
        float lx = tamanho.x / 2f + margem - 2f, lz = tamanho.y / 2f + margem - 2f;
        Parede(limites, centro + new Vector3(lx, 2f, 0f), new Vector3(1f, 6f, lz * 2f));
        Parede(limites, centro + new Vector3(-lx, 2f, 0f), new Vector3(1f, 6f, lz * 2f));
        Parede(limites, centro + new Vector3(0f, 2f, lz), new Vector3(lx * 2f, 6f, 1f));
        Parede(limites, centro + new Vector3(0f, 2f, -lz), new Vector3(lx * 2f, 6f, 1f));
        // pilares do lado de dentro do muro (quebram a parede lisa)
        var matPilar = Mat("MuroPilar", new Color(0.27f, 0.27f, 0.26f));
        for (float s = -lz + 4f; s <= lz - 4f; s += 12f)
            foreach (float lado in new[] { -1f, 1f })
                Pilar(limites, centro + new Vector3(lado * (lx - 0.65f), 0f, s), matPilar);
        for (float s = -lx + 4f; s <= lx - 4f; s += 12f)
            foreach (float lado in new[] { -1f, 1f })
                Pilar(limites, centro + new Vector3(s, 0f, lado * (lz - 0.65f)), matPilar);

        // cidade parada = estática: o Unity junta as malhas e desenha muito mais rápido
        var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
        foreach (var t in g.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
    }

    // distância (m) entre a borda da cidade e o muro da quarentena
    internal const float MARGEM_MURO = 22f;

    static void Pilar(Transform pai, Vector3 basePos, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Pilar";
        go.transform.SetParent(pai);
        go.transform.position = basePos + Vector3.up * 3.6f;
        go.transform.localScale = new Vector3(0.7f, 7.2f, 0.7f);
        go.GetComponent<Renderer>().sharedMaterial = m;
        go.isStatic = true;
    }

    // muro da quarentena em volta da cidade (antes era parede invisível e dava pra ver o "nada" lá fora)
    static void Parede(Transform pai, Vector3 pos, Vector3 tam)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "MuroQuarentena";
        go.transform.SetParent(pai);
        go.transform.position = pos + Vector3.up * 0.5f;
        go.transform.localScale = new Vector3(tam.x, tam.y + 1f, tam.z);
        go.GetComponent<Renderer>().sharedMaterial = Mat("Muro", new Color(0.36f, 0.36f, 0.34f));
        go.isStatic = true;
        // faixa de alerta no topo
        var faixa = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(faixa.GetComponent<Collider>());
        faixa.name = "Faixa";
        faixa.transform.SetParent(go.transform, false);
        faixa.transform.localPosition = new Vector3(0f, 0.42f, 0f);
        faixa.transform.localScale = new Vector3(1.01f, 0.06f, 1.02f);
        faixa.GetComponent<Renderer>().sharedMaterial = Mat("FaixaAlerta", new Color(0.85f, 0.65f, 0.1f));
    }

    static void BakeNavMesh(GeradorCidade g)
    {
        var surface = GetOrAdd<NavMeshSurface>(g.gameObject);
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        // altura exata do chão pros zumbis (sem isso eles flutuam/afundam alguns cm)
        surface.buildHeightMesh = true;
        if (surface.navMeshData != null) surface.RemoveData();
        surface.navMeshData = null;
        AssetDatabase.DeleteAsset(NAVMESH_ASSET);
        surface.BuildNavMesh();
        if (surface.navMeshData != null) AssetDatabase.CreateAsset(surface.navMeshData, NAVMESH_ASSET);
        EditorUtility.SetDirty(surface);
    }

    // ponto do NavMesh mais próximo, evitando telhados
    static Vector3 NoChao(Vector3 p, float alturaChao)
    {
        for (float r = 3f; r <= 30f; r += 3f)
        {
            for (int k = 0; k < 12; k++)
            {
                Vector3 tentativa = p + Quaternion.Euler(0f, k * 30f, 0f) * Vector3.forward * (r - 3f);
                if (NavMesh.SamplePosition(tentativa, out var hit, 3f, NavMesh.AllAreas) && hit.position.y < alturaChao + 1.5f)
                    return hit.position;
            }
        }
        Debug.LogWarning("[Sinal da Lia] Não achei NavMesh perto de " + p);
        return p;
    }

    // materiais

    static Material Mat(string nome, Color cor, bool unlit = false, Texture tex = null)
    {
        string path = $"Assets/Materials/{nome}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", cor);
        if (tex != null) m.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(m);
        return m;
    }

    // armas

    static WeaponData[] CriarArmas()
    {
        var pistola = Arma("Pistola", GUNS + "Pistols/Pistol1/pistol_1.prefab", a =>
        {
            a.nome = "Pistola"; a.dano = 25; a.tirosPorSegundo = 4; a.automatica = false; a.projeteis = 1;
            a.spread = 1.2f; a.alcance = 60; a.recuo = 1.2f; a.tamanhoPente = 12; a.municaoInicial = 0;
            a.municaoMaxima = 0; a.municaoInfinita = true; a.tempoRecarga = 1.2f;
            a.corMunicao = new Color(1f, 0.85f, 0.2f); a.comprimentoModelo = 0.22f;
        });
        var escopeta = Arma("Escopeta", GUNS + "Shotguns/Shotgun1/shotgun_1.prefab", a =>
        {
            a.nome = "Escopeta"; a.dano = 14; a.tirosPorSegundo = 1.2f; a.automatica = false; a.projeteis = 8;
            a.spread = 6f; a.alcance = 22; a.recuo = 4f; a.tamanhoPente = 6; a.municaoInicial = 12;
            a.municaoMaxima = 36; a.tempoRecarga = 2.2f;
            a.corMunicao = new Color(1f, 0.3f, 0.2f); a.comprimentoModelo = 0.95f;
        });
        var rifle = Arma("Rifle", GUNS + "Rifles/Rifle1/rifle_1.prefab", a =>
        {
            a.nome = "Rifle"; a.dano = 18; a.tirosPorSegundo = 9; a.automatica = true; a.projeteis = 1;
            a.spread = 2.2f; a.alcance = 80; a.recuo = 0.6f; a.tamanhoPente = 30; a.municaoInicial = 30;
            a.municaoMaxima = 180; a.tempoRecarga = 2f;
            a.corMunicao = new Color(0.3f, 0.6f, 1f); a.comprimentoModelo = 0.9f;
        });
        var sniper = Arma("Sniper", GUNS + "Rifles/Rifle3/rifle_3.prefab", a =>
        {
            a.nome = "Sniper"; a.dano = 150; a.tirosPorSegundo = 0.8f; a.automatica = false; a.projeteis = 1;
            a.spread = 3.5f; a.spreadMirando = 0f; a.fovMira = 18f; a.perfura = 2; a.impacto = 2f;
            a.alcance = 160; a.recuo = 6f; a.tamanhoPente = 5; a.municaoInicial = 10;
            a.municaoMaxima = 30; a.tempoRecarga = 2.6f;
            a.corMunicao = new Color(0.7f, 0.3f, 1f); a.comprimentoModelo = 1.25f;
        });
        Sons(pistola, "tiro_pistola");
        Sons(escopeta, "tiro_escopeta");
        Sons(rifle, "tiro_rifle");
        Sons(sniper, "tiro_sniper");
        return new[] { pistola, escopeta, rifle, sniper };
    }

    static void Sons(WeaponData a, string tiro)
    {
        if (a.somTiro == null) a.somTiro = GeradorSons.Carregar(tiro);
        if (a.somRecarga == null) a.somRecarga = GeradorSons.Carregar("recarga");
        EditorUtility.SetDirty(a);
    }

    // só configura na criação, pra não apagar ajustes feitos depois no Inspector
    static WeaponData Arma(string arquivo, string modelo, System.Action<WeaponData> cfg)
    {
        string path = $"Assets/Data/Armas/{arquivo}.asset";
        var a = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
        if (a == null)
        {
            a = ScriptableObject.CreateInstance<WeaponData>();
            cfg(a);
            AssetDatabase.CreateAsset(a, path);
        }
        if (a.modelo == null) a.modelo = AssetDatabase.LoadAssetAtPath<GameObject>(modelo);
        EditorUtility.SetDirty(a);
        return a;
    }

    // som

    static void ConfigurarSom(SomManager s)
    {
        s.passos = Enumerable.Range(1, 4).Select(i => GeradorSons.Carregar("passo_" + i)).ToArray();
        s.gemidos = Enumerable.Range(1, 4).Select(i => GeradorSons.Carregar("zumbi_gemido_" + i)).ToArray();
        s.danoJogador = GeradorSons.Carregar("dano_jogador");
        s.vazio = GeradorSons.Carregar("vazio");
        s.acerto = GeradorSons.Carregar("acerto");
        s.pegarItem = GeradorSons.Carregar("pegar_item");
        s.cura = GeradorSons.Carregar("cura");
        s.zumbiAtaque = GeradorSons.Carregar("zumbi_ataque");
        s.zumbiMorte = GeradorSons.Carregar("zumbi_morte");
        s.impactoCarne = GeradorSons.Carregar("impacto_carne");
        s.impactoParede = GeradorSons.Carregar("impacto_parede");
        s.vento = GeradorSons.Carregar("vento_loop");
        s.noite = GeradorSons.Carregar("noite_loop");
        s.fogo = GeradorSons.Carregar("fogo_loop");
        // trilha de verdade (Kevin MacLeod, CC BY 4.0 - creditada no jogo) em Assets/Audio/Musica;
        // se não tiver o arquivo, usa a música sintetizada
        s.musicaMenu = Musica("musica_menu_real") ?? GeradorSons.Carregar("musica_menu");
        s.musicaDia = Musica("musica_dia_real") ?? GeradorSons.Carregar("musica_dia");
        s.musicaDia2 = Musica("musica_dia2_real") ?? s.musicaDia;
        s.musicaNoite = Musica("musica_noite_real") ?? GeradorSons.Carregar("musica_noite");
        s.musicaFinal = Musica("musica_final_real") ?? GeradorSons.Carregar("musica_final");
        s.musicaVitoria = Musica("musica_vitoria_real");
        s.musicaGameOver = Musica("musica_gameover_real");
        s.volumeMusica = 0.4f;
        s.somVitoria = GeradorSons.Carregar("vitoria");
        s.somGameOver = GeradorSons.Carregar("game_over");
        s.helicoptero = GeradorSons.Carregar("helicoptero_loop");
    }

    // MP3 da pasta Musica: toca em streaming (não carrega a música inteira na memória) e comprime em Vorbis
    static AudioClip Musica(string nome)
    {
        string path = $"Assets/Audio/Musica/{nome}.mp3";
        var imp = AssetImporter.GetAtPath(path) as AudioImporter;
        if (imp == null) return null;
        var cfg = imp.defaultSampleSettings;
        if (cfg.loadType != AudioClipLoadType.Streaming || cfg.compressionFormat != AudioCompressionFormat.Vorbis)
        {
            cfg.loadType = AudioClipLoadType.Streaming;
            cfg.compressionFormat = AudioCompressionFormat.Vorbis;
            cfg.quality = 0.6f;
            imp.defaultSampleSettings = cfg;
            imp.loadInBackground = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    static Material MatParticula(string nome, Color cor)
    {
        string path = $"Assets/Materials/{nome}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", cor);
        EditorUtility.SetDirty(m);
        return m;
    }

    // jogador

    static GameObject CriarJogador(Transform raiz, Vector3 pos, Camera cam, WeaponData[] armas)
    {
        var go = new GameObject("Marcos");
        go.tag = "Player";
        go.transform.SetParent(raiz);
        go.transform.position = pos;
        var cc = go.AddComponent<CharacterController>();
        cc.center = new Vector3(0f, 0.85f, 0f);
        cc.height = 1.7f;
        cc.radius = 0.33f;
        cc.stepOffset = 0.4f;
        cc.skinWidth = 0.04f;
        go.AddComponent<PlayerHealth>();
        go.AddComponent<PlayerHunger>();
        go.AddComponent<PlayerController>().cameraTransform = cam.transform;
        go.AddComponent<InteracaoJogador>(); // v1.4: tecla E (portas, mochila, chave, armários)
        var pw = go.AddComponent<PlayerWeapons>();
        pw.armas = armas;
        pw.cam = cam;
        pw.cameraJogador = GetOrAdd<ThirdPersonCamera>(cam.gameObject);
        pw.materialTracer = Mat("Tracer", new Color(1f, 0.9f, 0.5f), true);
        var texArmas = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_ARMAS);
        if (texArmas != null) pw.materialArmas = Mat("Armas", Color.white, false, texArmas);
        if (TemQuaternius) pw.materialArmas = MatAtlas();
        pw.materialFeixe = MatFeixe("Feixe_Arma", new Color(2.4f, 0.45f, 0.25f));
        go.AddComponent<PlayerAnimator>();

        var visual = new GameObject("Visual").transform;
        visual.SetParent(go.transform, false);
        // o CharacterController fica "skinWidth" acima do chão: desce o visual pra o pé encostar
        visual.localPosition = new Vector3(0f, -cc.skinWidth, 0f);
        if (!(TemQuaternius && CriarVisualQuaternius(visual, pw)) && !CriarVisualMarcos(visual))
        {
            var capsula = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsula.name = "Capsula (placeholder)";
            Object.DestroyImmediate(capsula.GetComponent<Collider>());
            capsula.transform.SetParent(visual, false);
            capsula.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            capsula.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            capsula.GetComponent<Renderer>().sharedMaterial = Mat("Marcos_Placeholder", new Color(0.25f, 0.4f, 0.7f));
            // ponto da arma na frente da cápsula
            var mao = new GameObject("PontoArma").transform;
            mao.SetParent(visual, false);
            mao.localPosition = new Vector3(0.3f, 1.25f, 0.4f);
            pw.pontoArma = mao;
        }
        return go;
    }

    static bool CriarVisualMarcos(Transform visual)
    {
        if (!AssetDatabase.IsValidFolder(PASTA_MARCOS)) return false;
        var fbxs = AssetDatabase.FindAssets("t:Model", new[] { PASTA_MARCOS })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)).ToArray();
        if (fbxs.Length == 0) return false;

        foreach (var p in fbxs) ConfigurarMixamo(p, p.Contains("Idle") || p.Contains("Run"));

        string idle = fbxs.FirstOrDefault(p => p.Contains("Idle"));
        string run = fbxs.FirstOrDefault(p => p.Contains("Run") && !p.Contains("Backward"));
        string morte = fbxs.FirstOrDefault(p => p.Contains("Dying") || p.Contains("Death"));
        string recarga = fbxs.FirstOrDefault(p => p.Contains("Reload"));
        var ctrl = CriarControllerMarcos(Clip(idle), Clip(run), Clip(morte), Clip(recarga));

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(idle ?? fbxs[0]);
        var modelo = (GameObject)PrefabUtility.InstantiatePrefab(asset, visual);
        modelo.name = "Modelo";
        var anim = GetOrAdd<Animator>(modelo);
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        return true;
    }

    static void ConfigurarMixamo(string path, bool loop)
    {
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) return;
        if (imp.animationType == ModelImporterAnimationType.Human && imp.clipAnimations.Length > 0) return;
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        var clips = imp.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.loopTime = loop;
            c.lockRootRotation = true;
            c.lockRootHeightY = true;
            c.lockRootPositionXZ = true;
            c.keepOriginalOrientation = true;
            c.keepOriginalPositionY = false;
            c.heightFromFeet = true;
            c.keepOriginalPositionXZ = true;
        }
        imp.clipAnimations = clips;
        imp.SaveAndReimport();
    }

    static AnimationClip Clip(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    static AnimatorController CriarControllerMarcos(AnimationClip idle, AnimationClip run, AnimationClip morte, AnimationClip recarga)
    {
        string path = PASTA_MARCOS + "/Marcos.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Morrer", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter(new AnimatorControllerParameter { name = "VelAnim", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        ctrl.AddParameter("Recarregando", AnimatorControllerParameterType.Bool);
        var sm = ctrl.layers[0].stateMachine;
        var loco = ctrl.CreateBlendTreeInController("Locomocao", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        if (idle != null) tree.AddChild(idle, 0f);
        if (run != null) tree.AddChild(run, 1f);
        // VelAnim = -1 toca a corrida ao contrário (andar de costas)
        loco.speedParameterActive = true;
        loco.speedParameter = "VelAnim";
        sm.defaultState = loco;

        // camada "Mira": só do quadril pra cima (pose de mira / recarga enquanto as pernas correm)
        string pathMascara = PASTA_MARCOS + "/MascaraTronco.mask";
        AssetDatabase.DeleteAsset(pathMascara);
        var mascara = new AvatarMask();
        for (var parte = AvatarMaskBodyPart.Root; parte < AvatarMaskBodyPart.LastBodyPart; parte++)
            mascara.SetHumanoidBodyPartActive(parte, parte == AvatarMaskBodyPart.Body || parte == AvatarMaskBodyPart.Head
                || parte == AvatarMaskBodyPart.LeftArm || parte == AvatarMaskBodyPart.RightArm
                || parte == AvatarMaskBodyPart.LeftFingers || parte == AvatarMaskBodyPart.RightFingers);
        AssetDatabase.CreateAsset(mascara, pathMascara);
        ctrl.AddLayer("Mira");
        var camadas = ctrl.layers;
        camadas[1].avatarMask = mascara;
        camadas[1].defaultWeight = 0f;
        camadas[1].blendingMode = AnimatorLayerBlendingMode.Override;
        ctrl.layers = camadas;
        var smMira = camadas[1].stateMachine;
        var mira = smMira.AddState("Mira");
        mira.motion = idle;
        smMira.defaultState = mira;
        if (recarga != null)
        {
            var rec = smMira.AddState("Recarga");
            rec.motion = recarga;
            var ida = mira.AddTransition(rec);
            ida.AddCondition(AnimatorConditionMode.If, 0f, "Recarregando");
            ida.duration = 0.1f;
            ida.hasExitTime = false;
            var volta = rec.AddTransition(mira);
            volta.AddCondition(AnimatorConditionMode.IfNot, 0f, "Recarregando");
            volta.duration = 0.15f;
            volta.hasExitTime = false;
        }
        if (morte != null)
        {
            var st = sm.AddState("Morte");
            st.motion = morte;
            var t = sm.AddAnyStateTransition(st);
            t.AddCondition(AnimatorConditionMode.If, 0f, "Morrer");
            t.duration = 0.15f;
            t.canTransitionToSelf = false;
        }
        return ctrl;
    }

    // zumbi

    // O FBX traz 10 zumbis lado a lado (cada um com seu esqueleto rig_CharRootN), cabelos e uma malha de chão.
    // Cria 1 prefab por personagem; o spawner sorteia entre eles.
    static GameObject[] CriarPrefabsZumbi()
    {
        Pasta("Assets/Prefabs/Zumbis");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(FBX_ZUMBI);
        if (asset == null) return new[] { SalvarZumbi(NovoZumbiCapsula(), null, null, "Assets/Prefabs/Zumbis/Zumbi_Capsula.prefab") };

        var clip = Clip(FBX_ZUMBI);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_ZUMBI);
        var mat = tex != null ? Mat("Zumbi", Color.white, false, tex) : null;
        var corpos = asset.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Where(r => r.name.ToLower().Contains("zombie")).Select(r => r.name).ToArray();

        var lista = new List<GameObject>();
        foreach (var nomeCorpo in corpos)
        {
            var root = new GameObject("Zumbi_" + nomeCorpo);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var m = (GameObject)PrefabUtility.InstantiatePrefab(asset, visual);
            PrefabUtility.UnpackPrefabInstance(m, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            m.name = "Modelo";
            foreach (var a in m.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(a);
            if (clip != null) clip.SampleAnimation(m, 0f);

            var corpo = m.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == nomeCorpo);
            Transform rig = FilhoDireto(corpo.rootBone, m.transform);

            // fica só este personagem (+ cabelo/chapéu presos no esqueleto dele); some o resto e a malha do chão
            var manter = new HashSet<Transform> { rig, corpo.transform };
            foreach (var smr in m.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.bones.Any(b => b != null && rig != null && b.IsChildOf(rig))) manter.Add(FilhoDireto(smr.transform, m.transform));
            foreach (var filho in m.transform.Cast<Transform>().ToArray())
                if (!manter.Contains(filho)) Object.DestroyImmediate(filho.gameObject);

            if (mat != null)
                foreach (var r in m.GetComponentsInChildren<Renderer>())
                    r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();

            // no FBX eles estão espalhados: centraliza a bacia no meio do prefab
            Vector3 bacia = corpo.rootBone != null ? corpo.rootBone.position : rig.position;
            m.transform.localPosition = new Vector3(-bacia.x, 0f, -bacia.z);

            // vira o personagem pra frente (+Z) usando a direção calcanhar -> dedão
            var ossos = rig.GetComponentsInChildren<Transform>();
            var pe = ossos.FirstOrDefault(t => t.name.Contains("L Foot"));
            var dedo = ossos.FirstOrDefault(t => t.name.Contains("L Toe0"));
            if (pe != null && dedo != null)
            {
                Vector3 frente = dedo.position - pe.position;
                frente.y = 0f;
                if (frente.sqrMagnitude > 0.0001f) visual.localRotation = Quaternion.Euler(0f, Vector3.SignedAngle(frente, Vector3.forward, Vector3.up), 0f);
            }
            else Debug.LogWarning("[Sinal da Lia] Não achei os ossos do pé de " + nomeCorpo + " (orientação não ajustada)");

            // altura: só corrige se estiver fora do normal; pés no chão
            var bnd = Limites(m);
            if (bnd.size.y < 1.3f || bnd.size.y > 2.2f) visual.localScale = Vector3.one * (1.75f / Mathf.Max(0.01f, bnd.size.y));
            bnd = Limites(m);
            visual.localPosition = new Vector3(0f, -bnd.min.y, 0f);

            lista.Add(SalvarZumbi(root, clip, m.transform, $"Assets/Prefabs/Zumbis/{nomeCorpo}.prefab"));
        }
        // prefab antigo (10 zumbis juntos) não é mais usado
        AssetDatabase.DeleteAsset("Assets/Prefabs/Zumbi.prefab");
        return lista.ToArray();
    }

    static Transform FilhoDireto(Transform t, Transform raiz)
    {
        while (t != null && t.parent != raiz) t = t.parent;
        return t;
    }

    static GameObject NovoZumbiCapsula()
    {
        var root = new GameObject("Zumbi");
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Object.DestroyImmediate(cap.GetComponent<Collider>());
        cap.transform.SetParent(visual, false);
        cap.transform.localPosition = Vector3.up * 0.9f;
        cap.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        cap.GetComponent<Renderer>().sharedMaterial = Mat("Zumbi_Placeholder", new Color(0.35f, 0.55f, 0.3f));
        return root;
    }

    static GameObject SalvarZumbi(GameObject root, AnimationClip clip, Transform modelo, string path)
    {
        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.9f, 0f);
        col.height = 1.8f;
        col.radius = 0.28f;
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.32f;
        agent.height = 1.8f;
        agent.speed = 1.5f;
        agent.angularSpeed = 360f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 1.2f;
        var ai = root.AddComponent<ZombieAI>();
        ai.clip = clip;
        ai.modelo = modelo;
        root.AddComponent<ZombieHealth>();

        // mantém os ajustes de animação que o grupo já tenha feito no prefab
        var velho = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var aiVelho = velho != null ? velho.GetComponent<ZombieAI>() : null;
        if (aiVelho != null)
        {
            ai.comprimentoCiclo = aiVelho.comprimentoCiclo;
            ai.anguloPerna = aiVelho.anguloPerna;
            ai.anguloJoelho = aiVelho.anguloJoelho;
            ai.anguloBracos = aiVelho.anguloBracos;
            ai.inclinacaoCorpo = aiVelho.inclinacaoCorpo;
            ai.balanco = aiVelho.balanco;
        }
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static Bounds Limites(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        return b;
    }

    // cenário

    const string PANDA = "Assets/Pandazole_Ultimate_Pack/Pandazole City Town Pack/Prefabs/";
    const string LOWPOLY = "Assets/LowpolyStreetPack/Prefabs/StreetProps/";
    const string CENARIO = "Cenario_v02";

    // carros abandonados, barricadas, barris pegando fogo, postes, lixo. Filho do gerador (entra no NavMesh).
    static void MontarCenario(GeradorCidade g, float passo, Vector3 posInicio, Vector3 posEscola, Vector3 posClinica)
    {
        var velho = g.transform.Find(CENARIO);
        if (velho != null) Object.DestroyImmediate(velho.gameObject);
        var raiz = new GameObject(CENARIO).transform;
        raiz.SetParent(g.transform, false);
        Physics.SyncTransforms();

        var r = new System.Random(g.seed + 77);
        float Rnd(float a, float b) => a + (float)r.NextDouble() * (b - a);
        float Lado() => r.Next(2) == 0 ? -1f : 1f;

        var corCarros = new[]
        {
            new Color(0.45f, 0.25f, 0.15f), new Color(0.3f, 0.35f, 0.4f), new Color(0.55f, 0.52f, 0.47f),
            new Color(0.22f, 0.27f, 0.22f), new Color(0.5f, 0.12f, 0.1f), new Color(0.15f, 0.15f, 0.17f)
        }.Select((c, i) => Mat("Carro_" + i, c)).ToArray();
        var vidro = Mat("Vidro", new Color(0.07f, 0.09f, 0.11f));
        var pneu = Mat("Pneu", new Color(0.06f, 0.06f, 0.06f));
        var concreto = Mat("Concreto", new Color(0.55f, 0.55f, 0.52f));
        var areia = Mat("SacoAreia", new Color(0.55f, 0.5f, 0.35f));
        var texFumaca = TexturaGerada("fumaca", AlfaFumaca);
        var matFogo = MatParticulaTransparente("Fogo", new Color(3f, 1.2f, 0.3f, 1f), texFumaca, true);
        var matFumaca = MatParticulaTransparente("Fumaca", new Color(0.2f, 0.19f, 0.18f, 0.6f), texFumaca, false);
        var matSangue = MatRecorte("SangueChao", new Color(0.32f, 0.02f, 0.02f), TexturaGerada("sangue", AlfaSangue));
        var tijolo = Mat("Tijolo", new Color(0.45f, 0.3f, 0.24f));
        var somFogo = GeradorSons.Carregar("fogo_loop");

        // acampamento do Marcos primeiro (os outros objetos desviam dele)
        MontarAbrigo(raiz, posInicio, posEscola, matFogo, matFumaca, somFogo);

        // trechos retos de rua (entre cruzamentos): posição + direção
        var trechos = new List<(Vector3 pos, Vector3 dir)>();
        Vector3 o = g.transform.position;
        int q = g.tamanhoQuarteirao;
        float tile = g.tamanhoTile;
        for (int linha = 0; linha <= g.blocosZ; linha++)
            for (int i = 0; i < g.blocosX; i++)
                for (int k = 1; k <= q; k++) trechos.Add((o + new Vector3(i * passo + k * tile, 0f, linha * passo), Vector3.right));
        for (int coluna = 0; coluna <= g.blocosX; coluna++)
            for (int j = 0; j < g.blocosZ; j++)
                for (int k = 1; k <= q; k++) trechos.Add((o + new Vector3(coluna * passo, 0f, j * passo + k * tile), Vector3.forward));
        var importantes = new[] { posInicio, posEscola, posClinica };
        bool Longe(Vector3 p, float raio) => importantes.All(x => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(x.x, x.z)) > raio);
        Vector3 Lateral(Vector3 d) => new Vector3(d.z, 0f, -d.x);
        float Yaw(Vector3 d) => Quaternion.LookRotation(d).eulerAngles.y;

        int carros = 0, postes = 0, barris = 0, objetos = 0;

        // carros abandonados (comprimento no eixo X local)
        for (int t = 0; t < 120 && carros < 16; t++)
        {
            var tr = trechos[r.Next(trechos.Count)];
            Vector3 p = tr.pos + Lateral(tr.dir) * 2.3f * Lado() + tr.dir * Rnd(-3f, 3f);
            if (!Longe(p, 12f)) continue;
            float rot = Yaw(tr.dir) - 90f + Rnd(-25f, 25f) + (r.NextDouble() < 0.2 ? 90f : 0f) + r.Next(2) * 180f;
            var carro = Carro(raiz, corCarros[r.Next(corCarros.Length)], vidro, pneu, 4.2f, 1.8f, 0.75f, true);
            if (!Colocar(carro, p, rot)) continue;
            carro.transform.rotation *= Quaternion.Euler(0f, 0f, Rnd(-3f, 3f)); // pneu murcho
            carros++;
        }

        // escola: ônibus escolar + barreiras de concreto nas 4 entradas
        var onibus = Carro(raiz, Mat("Onibus", new Color(0.95f, 0.72f, 0.1f)), vidro, pneu, 9f, 2.4f, 1.9f, false, 0.5f);
        Peca(onibus, vidro, new Vector3(0f, 0.45f + 1.4f, 0f), new Vector3(8.4f, 0.55f, 2.42f));
        onibus.name = "Onibus_Escolar";
        if (Colocar(onibus, posEscola + Vector3.right * 13f + Vector3.forward * 2.6f, Rnd(-6f, 6f))) carros++;
        for (int k = 0; k < 4; k++)
            foreach (float lado in new[] { -1f, 1f })
            {
                Vector3 radial = Quaternion.Euler(0f, 90f * k, 0f) * Vector3.forward;
                var b = (TemQuaternius ? InstQ("Traffic Barrier/TrafficBarrier_" + (1 + r.Next(2)) + ".fbx", 2.2f, raiz) : null)
                        ?? Bloco(raiz, "Barreira", concreto, new Vector3(2f, 0.85f, 0.45f));
                if (Colocar(b, posEscola + radial * 8.5f + Lateral(radial) * 2.6f * lado, Yaw(radial) + Rnd(-10f, 10f))) objetos++;
            }

        // clínica: ambulância + sacos de areia (posto do exército) + placa com cruz vermelha
        var ambulancia = Carro(raiz, Mat("Ambulancia", new Color(0.92f, 0.92f, 0.9f)), vidro, pneu, 5f, 2.1f, 1.7f, false, 0.38f);
        Peca(ambulancia, Mat("Vermelho", new Color(0.75f, 0.08f, 0.06f)), new Vector3(0f, 0.38f * 0.9f + 0.85f, 0f), new Vector3(5.02f, 0.22f, 2.12f));
        Peca(ambulancia, vidro, new Vector3(2.0f, 0.38f * 0.9f + 1.35f, 0f), new Vector3(1.02f, 0.5f, 1.9f));
        ambulancia.name = "Ambulancia";
        if (Colocar(ambulancia, posClinica - Vector3.right * 12f - Vector3.forward * 2.4f, Rnd(-8f, 8f))) carros++;
        for (int k = 0; k < 4; k++)
            foreach (float lado in new[] { -1f, 1f })
            {
                Vector3 radial = Quaternion.Euler(0f, 90f * k, 0f) * Vector3.forward;
                var s = SacosDeAreia(raiz, areia);
                if (Colocar(s, posClinica + radial * 7f + Lateral(radial) * 2.8f * lado, Yaw(radial) + Rnd(-8f, 8f))) objetos++;
            }
        var placa = new GameObject("Placa_Clinica");
        placa.transform.SetParent(raiz, false);
        Peca(placa, concreto, new Vector3(0f, 1.6f, 0f), new Vector3(0.15f, 3.2f, 0.15f), PrimitiveType.Cube);
        Peca(placa, Mat("Branco", new Color(0.9f, 0.9f, 0.88f)), new Vector3(0f, 3.8f, 0f), new Vector3(2f, 2f, 0.12f));
        var vermelho = Mat("Vermelho", new Color(0.75f, 0.08f, 0.06f));
        Peca(placa, vermelho, new Vector3(0f, 3.8f, -0.08f), new Vector3(1.3f, 0.4f, 0.04f));
        Peca(placa, vermelho, new Vector3(0f, 3.8f, -0.08f), new Vector3(0.4f, 1.3f, 0.04f));
        placa.AddComponent<BoxCollider>().size = new Vector3(0.3f, 4.8f, 0.3f);
        placa.GetComponent<BoxCollider>().center = new Vector3(0f, 2.4f, 0f);
        Colocar(placa, posClinica + new Vector3(4.5f, 0f, 4.5f), 45f); // cruz (lado -Z) virada pra cidade

        // barris pegando fogo: cantos da escola e da clínica + alguns pela cidade
        var barril = AssetDatabase.LoadAssetAtPath<GameObject>(PANDA + "Prop_OilBerrel_01.prefab");
        if (barril != null)
        {
            void Barril(Vector3 p)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(barril, raiz);
                // o barril do pacote tem 1,34 m (maior que a Lia): deixa com ~1 m
                float hb = Limites(go).size.y;
                if (hb > 0.1f) go.transform.localScale *= 1.0f / hb;
                if (!Colocar(go, p, Rnd(0f, 360f))) return;
                CriarFogo(go, matFogo, matFumaca, somFogo);
                barris++;
            }
            foreach (var centro in new[] { posEscola, posClinica })
                for (int k = 0; k < 4; k++) Barril(centro + Quaternion.Euler(0f, 45f + 90f * k, 0f) * Vector3.forward * 4.6f);
            for (int t = 0; t < 30 && barris < 12; t++)
            {
                var tr = trechos[r.Next(trechos.Count)];
                Vector3 p = tr.pos + Lateral(tr.dir) * 3.8f * Lado();
                if (Longe(p, 10f)) Barril(p);
            }
        }

        // postes de luz (acendem à noite; alguns piscam ou estão quebrados)
        var poste = AssetDatabase.LoadAssetAtPath<GameObject>(LOWPOLY + "ParkLamp/ParkLamp.prefab");
        if (poste != null)
        {
            for (int idx = 0; idx < trechos.Count && postes < 18; idx += 3)
            {
                var tr = trechos[idx];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(poste, raiz);
                if (!Colocar(go, tr.pos + Lateral(tr.dir) * ((idx / 3) % 2 == 0 ? 4.4f : -4.4f), Rnd(0f, 360f))) continue;
                double sorte = r.NextDouble();
                // o poste do pacote tem várias lâmpadas: fica só 1 luz (desempenho)
                var luzes = go.GetComponentsInChildren<Light>();
                for (int k = 1; k < luzes.Length; k++) Object.DestroyImmediate(luzes[k]);
                if (luzes.Length > 0) ConfigLuz(luzes[0], sorte < 0.12 ? 0f : 7f, 0f, 0f, sorte >= 0.12 && sorte < 0.32, new Color(1f, 0.8f, 0.55f), 13f);
                postes++;
            }
        }

        // lixo, bancos e hidrantes nas calçadas
        var miudos = new[] { "Prop_TrashBag_01", "Prop_TrashBag_02", "Prop_TrashBag_03", "Prop_CTPTrashCan_01", "Prop_CTPTrashCan_02" }
            .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(PANDA + n + ".prefab"))
            .Concat(new[] { "Bench", "Hidrant" }.SelectMany(pasta =>
                AssetDatabase.FindAssets("t:Prefab", new[] { LOWPOLY + pasta }).Select(gid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(gid)))))
            .Where(x => x != null).ToArray();
        for (int t = 0; t < 70 && miudos.Length > 0 && objetos < 45; t++)
        {
            var tr = trechos[r.Next(trechos.Count)];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(miudos[r.Next(miudos.Length)], raiz);
            if (Colocar(go, tr.pos + Lateral(tr.dir) * Rnd(3.6f, 4.4f) * Lado() + tr.dir * Rnd(-4f, 4f), Rnd(0f, 360f))) objetos++;
        }

        // objetos do pacote Quaternius: pallets, sofá, pneus, blocos, canos, lixo, barreiras
        if (TemQuaternius)
        {
            var propsQ = new (string arq, float tam)[]
            {
                ("Pallet/Pallet.fbx", 1.3f), ("Pallet Broken/Pallet_Broken.fbx", 1.3f), ("Damaged Couch/Couch.fbx", 2f),
                ("Wheels Stack/Wheels_Stack.fbx", 1.1f), ("Wheel/Wheel.fbx", 0.75f), ("Cinder Block/CinderBlock.fbx", 0.45f),
                ("Pipes/Pipes.fbx", 2.2f), ("Trash Bag/TrashBag_1.fbx", 0.7f), ("Trash Bags/TrashBag_2.fbx", 1.1f),
                ("Barrel/Barrel.fbx", 1f), ("Traffic Cone/TrafficCone_1.fbx", 0.7f), ("Plastic Barrier/PlasticBarrier.fbx", 1.4f)
            };
            int colocados = 0;
            for (int t = 0; t < 100 && colocados < 40; t++)
            {
                var tr = trechos[r.Next(trechos.Count)];
                var prop = propsQ[r.Next(propsQ.Length)];
                var go = InstQ(prop.arq, prop.tam, raiz);
                if (go == null) continue;
                if (Colocar(go, tr.pos + Lateral(tr.dir) * Rnd(2.8f, 4.3f) * Lado() + tr.dir * Rnd(-4f, 4f), Rnd(0f, 360f))) colocados++;
            }
            objetos += colocados;
            // posto militar na clínica: containers
            var cont1 = InstQ("Container Red/Container_Red.fbx", 6f, raiz);
            if (cont1 != null && Colocar(cont1, posClinica + new Vector3(9f, 0f, 3f), 90f)) objetos++;
            var cont2 = InstQ("Container Green/Container_Green.fbx", 6f, raiz);
            if (cont2 != null && Colocar(cont2, posClinica + new Vector3(3f, 0f, 9f), 0f)) objetos++;
            // placa da cidade na saída do abrigo
            var placaCidade = InstQ("Town Sign/TownSign.fbx", 3f, raiz);
            if (placaCidade != null) Colocar(placaCidade, posInicio + new Vector3(4.5f, 0f, 6f), 200f);
        }

        // colunas de fumaça saindo dos telhados (dá pra ver de longe)
        int fumacas = 0;
        var quarteiroes = new HashSet<int>();
        for (int t = 0; t < 40 && fumacas < 5; t++)
        {
            int bi = r.Next(g.blocosX), bj = r.Next(g.blocosZ);
            if (!quarteiroes.Add(bi * 100 + bj)) continue;
            Vector3 lote = o + new Vector3(bi * passo + tile * (1 + r.Next(q)), 0f, bj * passo + tile * (1 + r.Next(q)));
            if (!Physics.Raycast(lote + Vector3.up * 80f, Vector3.down, out var teto, 100f, ~0, QueryTriggerInteraction.Ignore)) continue;
            CriarFumaca(raiz, teto.point, matFumaca, matFogo);
            fumacas++;
        }

        // manchas de sangue no chão (mais perto da escola e da clínica)
        int manchas = 0;
        for (int t = 0; t < 80 && manchas < 24; t++)
        {
            Vector3 p;
            if (t % 3 == 0) p = importantes[1 + r.Next(2)] + new Vector3(Rnd(-9f, 9f), 0f, Rnd(-9f, 9f));
            else
            {
                var tr = trechos[r.Next(trechos.Count)];
                p = tr.pos + Lateral(tr.dir) * Rnd(-3.5f, 3.5f) + tr.dir * Rnd(-4f, 4f);
            }
            float chao = float.MinValue;
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
                if (h.point.y > chao) chao = h.point.y;
            if (chao == float.MinValue || chao > p.y + 0.6f) continue; // em cima de carro/objeto: pula
            if (TemQuaternius && manchas % 3 == 0)
            {
                // poça de sangue 3D do pacote
                var poca = InstQ(r.Next(3) == 0 ? "Blood/Blood_3.fbx" : "Blood Splat/Blood_" + (1 + r.Next(2)) + ".fbx", Rnd(1f, 2f), raiz, false);
                if (poca != null)
                {
                    poca.name = "Mancha";
                    poca.transform.SetPositionAndRotation(new Vector3(p.x, chao + 0.01f, p.z), Quaternion.Euler(0f, Rnd(0f, 360f), 0f));
                    manchas++;
                    continue;
                }
            }
            var mancha = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(mancha.GetComponent<Collider>());
            mancha.name = "Mancha";
            mancha.transform.SetParent(raiz, false);
            mancha.transform.SetPositionAndRotation(new Vector3(p.x, chao + 0.015f, p.z), Quaternion.Euler(90f, Rnd(0f, 360f), 0f));
            mancha.transform.localScale = Vector3.one * Rnd(0.8f, 2.4f);
            var mr = mancha.GetComponent<Renderer>();
            mr.sharedMaterial = matSangue;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            manchas++;
        }

        // entulho (pedaços de concreto e tijolo) pelas ruas
        int entulhos = 0;
        for (int t = 0; t < 50 && entulhos < 14; t++)
        {
            var tr = trechos[r.Next(trechos.Count)];
            Vector3 p = tr.pos + Lateral(tr.dir) * Rnd(2.5f, 4.2f) * Lado() + tr.dir * Rnd(-4f, 4f);
            if (!Longe(p, 9f)) continue;
            var pilha = new GameObject("Entulho");
            pilha.transform.SetParent(raiz, false);
            int n = 3 + r.Next(4);
            for (int k = 0; k < n; k++)
                Peca(pilha, r.Next(2) == 0 ? concreto : tijolo,
                    new Vector3(Rnd(-0.7f, 0.7f), Rnd(0.1f, 0.35f), Rnd(-0.7f, 0.7f)),
                    new Vector3(Rnd(0.3f, 0.9f), Rnd(0.2f, 0.5f), Rnd(0.3f, 0.8f))).transform.localRotation = Quaternion.Euler(Rnd(-25f, 25f), Rnd(0f, 360f), Rnd(-25f, 25f));
            var colE = pilha.AddComponent<BoxCollider>();
            colE.center = new Vector3(0f, 0.25f, 0f);
            colE.size = new Vector3(1.6f, 0.5f, 1.6f);
            if (Colocar(pilha, p, Rnd(0f, 360f))) entulhos++;
        }

        // árvores mortas e pedras na faixa de terra entre a cidade e o muro
        MontarNatureza(raiz, o, new Vector2(g.blocosX * passo, g.blocosZ * passo), tile / 2f, r);

        var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.GetComponent<ParticleSystem>() == null) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        Debug.Log($"[Sinal da Lia] Cenário: {carros} veículos, {barris} barris com fogo, {postes} postes, {objetos} objetos, {fumacas} fumaças, {manchas} manchas, {entulhos} entulhos");
    }

    // posiciona no chão de verdade e desiste (apaga) se encostar em prédio/outro objeto
    static bool Colocar(GameObject go, Vector3 p, float rotY)
    {
        float y = p.y, melhor = float.MinValue;
        foreach (var h in Physics.RaycastAll(p + Vector3.up * 6f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
            if (!h.transform.IsChildOf(go.transform) && h.point.y > melhor && h.point.y < p.y + 1.5f) melhor = h.point.y;
        if (melhor > float.MinValue) y = melhor;
        // mantém a rotação original do modelo (vários FBX vêm "deitados" -90° em X e o prefab corrige):
        // trocar pela rotação só em Y deixava poste deitado no chão e banco em pé
        go.transform.SetPositionAndRotation(new Vector3(p.x, y, p.z), Quaternion.Euler(0f, rotY, 0f) * go.transform.rotation);
        Physics.SyncTransforms();
        var cols = go.GetComponentsInChildren<Collider>();
        if (cols.Length == 0) return true;
        Bounds b = cols[0].bounds;
        foreach (var c in cols) b.Encapsulate(c.bounds);
        float fundo = y + 0.3f, topo = Mathf.Max(b.max.y, fundo + 0.1f); // ignora o chão
        var centro = new Vector3(b.center.x, (fundo + topo) / 2f, b.center.z);
        var ext = new Vector3(b.extents.x * 0.9f, (topo - fundo) / 2f, b.extents.z * 0.9f);
        foreach (var c in Physics.OverlapBox(centro, ext, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            if (!c.transform.IsChildOf(go.transform))
            {
                Object.DestroyImmediate(go);
                return false;
            }
        return true;
    }

    static GameObject Peca(GameObject pai, Material m, Vector3 pos, Vector3 escala, PrimitiveType tipo = PrimitiveType.Cube)
    {
        var p = GameObject.CreatePrimitive(tipo);
        Object.DestroyImmediate(p.GetComponent<Collider>());
        p.transform.SetParent(pai.transform, false);
        p.transform.localPosition = pos;
        p.transform.localScale = escala;
        p.GetComponent<Renderer>().sharedMaterial = m;
        return p;
    }

    // carro low-poly: lataria + cabine de vidro + 4 rodas; comprimento no X local
    static GameObject Carro(Transform pai, Material corpo, Material vidro, Material pneu, float comp, float larg, float alt, bool cabine, float roda = 0.34f)
    {
        var go = new GameObject("Carro");
        go.transform.SetParent(pai, false);
        float chao = roda * 0.9f, altura = chao + alt;
        Peca(go, corpo, new Vector3(0f, chao + alt / 2f, 0f), new Vector3(comp, alt, larg));
        if (cabine)
        {
            Peca(go, vidro, new Vector3(-comp * 0.05f, altura + 0.31f, 0f), new Vector3(comp * 0.52f, 0.62f, larg * 0.9f));
            altura += 0.62f;
        }
        float rx = comp / 2f - roda * 1.6f, rz = larg / 2f - 0.08f;
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                Peca(go, pneu, new Vector3(sx * rx, roda, sz * rz), new Vector3(roda * 2f, 0.12f, roda * 2f), PrimitiveType.Cylinder).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, altura / 2f, 0f);
        col.size = new Vector3(comp, altura, larg);
        return go;
    }

    static GameObject Bloco(Transform pai, string nome, Material m, Vector3 tam)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nome;
        go.transform.SetParent(pai, false);
        go.transform.localScale = tam;
        go.GetComponent<Renderer>().sharedMaterial = m;
        // cubo tem o centro no meio: sobe o visual pro pé ficar no chão
        var raiz = new GameObject(nome);
        raiz.transform.SetParent(pai, false);
        go.transform.SetParent(raiz.transform, false);
        go.transform.localPosition = Vector3.up * tam.y / 2f;
        return raiz;
    }

    static GameObject SacosDeAreia(Transform pai, Material m)
    {
        var go = new GameObject("SacosDeAreia");
        go.transform.SetParent(pai, false);
        for (int i = 0; i < 3; i++) Peca(go, m, new Vector3(-0.85f + i * 0.85f, 0.2f, 0f), new Vector3(0.82f, 0.4f, 0.55f));
        for (int i = 0; i < 2; i++) Peca(go, m, new Vector3(-0.42f + i * 0.85f, 0.6f, 0f), new Vector3(0.82f, 0.4f, 0.55f));
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.4f, 0f);
        col.size = new Vector3(2.55f, 0.8f, 0.55f);
        return go;
    }

    static void ConfigLuz(Light l, float noite, float dia, float piscar, bool defeito, Color cor, float alcance)
    {
        l.color = cor;
        l.range = alcance;
        l.shadows = LightShadows.None;
        l.lightmapBakeType = LightmapBakeType.Realtime;
        l.intensity = noite;
        var ln = GetOrAdd<LuzNoturna>(l.gameObject);
        ln.intensidadeNoite = noite;
        ln.intensidadeDia = dia;
        ln.piscar = piscar;
        ln.defeito = defeito;
    }

    static void CriarFogo(GameObject barril, Material mat, Material matFumaca, AudioClip som)
    {
        var b = Limites(barril);
        var fogo = new GameObject("Fogo");
        fogo.transform.SetParent(barril.transform, false);
        fogo.transform.SetPositionAndRotation(new Vector3(b.center.x, b.max.y + 0.05f, b.center.z), Quaternion.Euler(-90f, 0f, 0f));
        ParticulasFogo(fogo, mat, 1f);
        Brasas(fogo, mat, 1f);
        if (matFumaca != null) FumacaDoFogo(fogo, matFumaca, 1f);

        var luzGo = new GameObject("Luz");
        luzGo.transform.SetParent(fogo.transform, false);
        luzGo.transform.position = fogo.transform.position + Vector3.up * 0.6f;
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Point;
        ConfigLuz(luz, 4f, 1.2f, 0.45f, false, new Color(1f, 0.55f, 0.2f), 9f);

        var a = fogo.AddComponent<AudioSource>();
        a.clip = som;
        a.loop = true;
        a.playOnAwake = true;
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = 1.5f;
        a.maxDistance = 14f;
        a.volume = 0.5f;
        a.dopplerLevel = 0f;
    }

    // chamas: partículas aditivas que sobem, encolhem e somem (escala 1 = barril)
    static void ParticulasFogo(GameObject go, Material mat, float escala)
    {
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        float raiz = Mathf.Sqrt(escala);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f * raiz, 0.85f * raiz);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * raiz, 1.2f * raiz);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f * escala, 0.6f * escala);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.55f), new Color(1f, 0.48f, 0.12f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = Mathf.RoundToInt(80 * escala);
        main.gravityModifier = -0.15f;
        var em = ps.emission;
        em.rateOverTime = 42f * escala;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 9f;
        forma.radius = 0.2f * escala;
        // labareda: nasce pequena, cresce e afina no topo
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.12f)));
        var cor = ps.colorOverLifetime;
        cor.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.55f, 0.16f), 0.4f), new GradientColorKey(new Color(0.45f, 0.08f, 0.02f), 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        cor.color = grad;
        var giro = ps.rotationOverLifetime;
        giro.enabled = true;
        giro.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        // tremulando (o fogo não sobe reto)
        var ruido = ps.noise;
        ruido.enabled = true;
        ruido.strength = 0.35f * raiz;
        ruido.frequency = 1.3f;
        ruido.scrollSpeed = 0.8f;
        ruido.quality = ParticleSystemNoiseQuality.Low;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = mat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
    }

    // coluna de fumaça escura subindo (com fogo na base) - dá pra ver de longe
    static void CriarFumaca(Transform pai, Vector3 pos, Material matFumaca, Material matFogo)
    {
        var go = new GameObject("Fumaca");
        go.transform.SetParent(pai, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(-90f, 0f, 0f));
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 18f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.15f, 0.14f), new Color(0.3f, 0.29f, 0.27f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 45;
        main.gravityModifier = -0.01f;
        var em = ps.emission;
        em.rateOverTime = 2.6f;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 10f;
        forma.radius = 2f;
        var vento = ps.velocityOverLifetime;
        vento.enabled = true;
        vento.space = ParticleSystemSimulationSpace.World;
        vento.x = new ParticleSystem.MinMaxCurve(0.8f);
        vento.y = new ParticleSystem.MinMaxCurve(0f);
        vento.z = new ParticleSystem.MinMaxCurve(0.3f);
        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 2.2f)));
        var cor = ps.colorOverLifetime;
        cor.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.75f, 0.1f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
        cor.color = grad;
        var giro = ps.rotationOverLifetime;
        giro.enabled = true;
        giro.z = new ParticleSystem.MinMaxCurve(0.1f);
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = matFumaca;
        pr.sortMode = ParticleSystemSortMode.Distance;
        pr.maxParticleSize = 3f;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;

        var fogo = new GameObject("FogoBase");
        fogo.transform.SetParent(go.transform, false);
        ParticulasFogo(fogo, matFogo, 2.5f);
    }

    // textura gerada por código (branca, só o alfa muda) - fumaça macia, mancha de sangue
    static Texture2D TexturaGerada(string nome, System.Func<float, float, float> alfa, int tam = 128)
    {
        string path = $"Assets/Textures/Gerado/{nome}.png";
        var existente = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existente != null) return existente;
        Pasta("Assets/Textures/Gerado");
        var t = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        for (int y = 0; y < tam; y++)
            for (int x = 0; x < tam; x++)
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alfa((x + 0.5f) / tam * 2f - 1f, (y + 0.5f) / tam * 2f - 1f))));
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float AlfaFumaca(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float borda = Mathf.Clamp01(1f - r);
        return Mathf.Pow(borda, 1.5f) * (0.7f + 0.3f * Mathf.PerlinNoise(u * 3f + 5f, v * 3f + 5f));
    }

    static float AlfaSangue(float u, float v)
    {
        // poça principal + respingos, com borda irregular
        var r = new System.Random(5);
        float valor = 0f;
        for (int i = 0; i < 9; i++)
        {
            float cx = i == 0 ? 0f : (float)(r.NextDouble() * 1.6 - 0.8);
            float cy = i == 0 ? 0f : (float)(r.NextDouble() * 1.6 - 0.8);
            float raio = i == 0 ? 0.5f : (float)(r.NextDouble() * 0.12 + 0.05);
            float d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));
            valor = Mathf.Max(valor, 1f - d / raio);
        }
        valor += (Mathf.PerlinNoise(u * 6f + 2f, v * 6f + 9f) - 0.5f) * 0.5f;
        return Mathf.Clamp01((valor - 0.05f) * 4f);
    }

    static Material MatParticulaTransparente(string nome, Color cor, Texture tex, bool aditivo)
    {
        var m = MatParticula(nome, cor);
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", aditivo ? 2f : 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(aditivo ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)(aditivo ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    // material com recorte pelo alfa (mancha no chão)
    static Material MatRecorte(string nome, Color cor, Texture tex)
    {
        var m = Mat(nome, cor, false, tex);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.65f); // parece molhado
        EditorUtility.SetDirty(m);
        return m;
    }

    // céu nublado de apocalipse (o DayNightController muda as cores por fase)
    static void ConfigurarCeu()
    {
        // céu próprio (Assets/Shaders/SinalDaLiaCeu.shader): gradiente + nuvens + sol/lua/estrelas,
        // com o horizonte da mesma cor da neblina. O antigo (Skybox/Procedural) deixava o céu verde-oliva.
        const string path = "Assets/Materials/CeuApocalipse.mat";
        var shader = Shader.Find("SinalDaLia/Ceu") ?? Shader.Find("Skybox/Procedural");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader) m.shader = shader;
        var dia = DayNightController.DiaPadrao(); // mesmas cores do dia do jogo (pra cena no Editor ficar igual)
        if (m.HasProperty("_CorTopo"))
        {
            m.SetColor("_CorTopo", dia.topo);
            m.SetColor("_CorHorizonte", dia.horizonte);
            m.SetColor("_CorChao", dia.chao);
            m.SetColor("_NuvemCor", dia.nuvem);
            m.SetColor("_NuvemSombra", dia.nuvemSombra);
            m.SetFloat("_Nuvens", dia.nuvens);
            m.SetFloat("_SolTamanho", dia.solTamanho);
            m.SetFloat("_SolBrilho", dia.solBrilho);
            m.SetFloat("_Estrelas", 0f);
            m.SetFloat("_LuaTamanho", 0f);
        }
        EditorUtility.SetDirty(m);
        RenderSettings.skybox = m;
        // luz ambiente em 3 tons e neblina da cor do horizonte (o DayNightController troca por fase)
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = dia.ambCeu;
        RenderSettings.ambientEquatorColor = dia.ambMeio;
        RenderSettings.ambientGroundColor = dia.ambChao;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = dia.horizonte;
        RenderSettings.fogDensity = dia.neblina;
    }

    // visual (pós-processamento)

    // clima de apocalipse: cores lavadas, contraste, vinheta e granulado de filme
    static void ConfigurarVolume()
    {
        const string path = "Assets/Settings/SinalDaLia_Volume.asset";
        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (perfil == null)
        {
            perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(perfil, path);
        }
        // menos lavado (o -30 de saturação + filtro verde deixavam tudo "doente"); o DayNightController muda por fase
        var dia = DayNightController.DiaPadrao();
        Componente<Tonemapping>(perfil).mode.Override(TonemappingMode.ACES);
        var cor = Componente<ColorAdjustments>(perfil);
        cor.postExposure.Override(dia.exposicao);
        cor.contrast.Override(dia.contraste);
        cor.saturation.Override(dia.saturacao);
        cor.colorFilter.Override(Color.white);
        Componente<WhiteBalance>(perfil).temperature.Override(dia.temperatura);
        var tons = Componente<SplitToning>(perfil);
        tons.shadows.Override(new Color(0.44f, 0.5f, 0.56f)); // sombras levemente azuladas
        tons.highlights.Override(new Color(0.57f, 0.53f, 0.47f)); // luz levemente quente
        tons.balance.Override(0f);
        var vin = Componente<Vignette>(perfil);
        vin.intensity.Override(dia.vinheta);
        vin.smoothness.Override(0.45f);
        var grao = Componente<FilmGrain>(perfil);
        grao.type.Override(FilmGrainLookup.Thin1);
        grao.intensity.Override(0.18f);
        var bloom = Componente<Bloom>(perfil);
        bloom.intensity.Override(dia.bloom);
        bloom.threshold.Override(0.95f);
        bloom.scatter.Override(0.65f);
        EditorUtility.SetDirty(perfil);
        var vol = Object.FindAnyObjectByType<Volume>();
        if (vol == null)
        {
            vol = new GameObject("Global Volume").AddComponent<Volume>();
            vol.isGlobal = true;
        }
        vol.sharedProfile = perfil;
        EditorUtility.SetDirty(vol);
    }

    // componente do perfil de pós-processamento (cria e salva dentro do asset se ainda não existir)
    static T Componente<T>(VolumeProfile perfil) where T : VolumeComponent
    {
        if (perfil.TryGet<T>(out var c)) { EditorUtility.SetDirty(c); return c; }
        c = perfil.Add<T>(true);
        c.name = typeof(T).Name;
        c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(c, perfil);
        return c;
    }

    // marcadores

    // desenho da Lia no chão, ao lado do farol da escola (a escopeta fica bem no farol).
    // Precisa de: caminho andável até o farol, espaço pro Marcos em pé e nada por cima.
    static DesenhoLia CriarDesenho(Transform raiz, Vector3 posEscola)
    {
        Physics.SyncTransforms();
        var caminho = new NavMeshPath();
        Vector3 p = posEscola;
        bool achou = false;
        for (float r = 2.5f; r <= 5.5f && !achou; r += 0.75f)
            for (int k = 0; k < 16 && !achou; k++)
            {
                Vector3 c = posEscola + Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward * r;
                if (!NavMesh.SamplePosition(c, out var hit, 0.5f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - posEscola.y) > 0.3f) continue;
                if (!NavMesh.CalculatePath(posEscola, hit.position, NavMesh.AllAreas, caminho) || caminho.status != NavMeshPathStatus.PathComplete) continue;
                if (Physics.CheckCapsule(hit.position + Vector3.up * 0.5f, hit.position + Vector3.up * 1.6f, 0.45f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Raycast(hit.position + Vector3.up * 0.2f, Vector3.up, 6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                p = hit.position;
                achou = true;
            }
        if (!achou) Debug.LogWarning("[Sinal da Lia] Sem lugar livre pro desenho perto da escola; ficou no farol");
        var go = new GameObject("DesenhoLia");
        go.transform.SetParent(raiz);
        go.transform.position = p;
        return go.AddComponent<DesenhoLia>();
    }

    // farol do objetivo: feixe de luz macio subindo pro céu (antes era um cilindro sólido amarelo) + brilho no chão
    static Transform CriarMarcador(Transform raiz, string nome, Vector3 pos, Color corFeixe, out GameObject farol)
    {
        var go = new GameObject(nome).transform;
        go.SetParent(raiz);
        go.position = pos;
        var f = new GameObject("Farol");
        f.transform.SetParent(go, false);
        LinhaFeixe(f, MatFeixe("Feixe_" + nome, corFeixe), 75f, 2.2f, 1.1f, 0.9f);
        var luz = new GameObject("Brilho").AddComponent<Light>();
        luz.transform.SetParent(f.transform, false);
        luz.transform.localPosition = Vector3.up * 1.5f;
        luz.type = LightType.Point;
        luz.range = 9f;
        luz.intensity = 3f;
        luz.color = new Color(Mathf.Clamp01(corFeixe.r), Mathf.Clamp01(corFeixe.g), Mathf.Clamp01(corFeixe.b));
        luz.shadows = LightShadows.None;
        farol = f;
        return go;
    }
}
