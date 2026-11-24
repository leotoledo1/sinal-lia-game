using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Menu "Sinal da Lia > Validar v0.1": checa a cena/assets e depois roda o teste automático em Play.
// Relatório em Logs/Validacao_v01.txt
public static class SinalDaLiaValidacao
{
    const string CENA_JOGO = "Assets/Scenes/Game.unity";
    static StringBuilder sb;
    static int ok, falhas, avisos;

    [MenuItem("Sinal da Lia/Validar Jogo")]
    public static void Validar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        RodarChecagens();
        AutoTesteEmPlay();
    }

    // linha de comando: Unity -batchmode -executeMethod SinalDaLiaValidacao.Batch
    public static void Batch()
    {
        RodarChecagens();
        AutoTesteEmPlay();
    }

    static void AutoTesteEmPlay()
    {
        SessionState.SetBool(AutoTesteV01.FLAG, true);
        EditorApplication.EnterPlaymode();
    }

    static void Checar(bool cond, string nome, string detalhe = "")
    {
        if (cond) { ok++; sb.AppendLine("  [OK] " + nome + (detalhe != "" ? "  (" + detalhe + ")" : "")); }
        else { falhas++; sb.AppendLine("  [FALHA] " + nome + (detalhe != "" ? "  - " + detalhe : "")); }
    }

    static void Aviso(string msg) { avisos++; sb.AppendLine("  [AVISO] " + msg); }
    static void Secao(string nome) => sb.AppendLine("\n== " + nome);

    static void RodarChecagens()
    {
        sb = new StringBuilder();
        ok = falhas = avisos = 0;
        sb.AppendLine("VALIDAÇÃO SINAL DA LIA v1.0 - " + System.DateTime.Now);
        sb.AppendLine("######## CHECAGEM DA CENA E ASSETS ########");

        Secao("Build");
        var cenas = EditorBuildSettings.scenes;
        Checar(cenas.Length > 0 && cenas[0].path == CENA_JOGO && cenas[0].enabled, "Game.unity é a 1ª cena da Build");

        var cena = EditorSceneManager.OpenScene(CENA_JOGO);
        var todos = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Select(t => t.gameObject).ToArray();

        Secao("Referências da cena");
        var gm = Object.FindAnyObjectByType<GameManager>();
        Checar(gm != null, "GameManager na cena");
        if (gm != null)
        {
            Checar(gm.jogador != null && gm.escola != null && gm.clinica != null, "GameManager: jogador, escola e clínica ligados");
            Checar(gm.farolEscola != null && gm.farolClinica != null && gm.lia != null, "GameManager: faróis e Lia ligados");
        }
        var ws = Object.FindAnyObjectByType<WaveSpawner>();
        Checar(ws != null && ws.prefabZumbi != null, "WaveSpawner com prefab do zumbi");
        var dn = Object.FindAnyObjectByType<DayNightController>();
        Checar(dn != null && dn.sol != null && dn.cam != null, "DayNightController com sol e câmera");
        Checar(Object.FindAnyObjectByType<RadioSystem>() != null && Object.FindAnyObjectByType<ObjectiveManager>() != null && Object.FindAnyObjectByType<HUD>() != null, "Rádio, objetivos e HUD na cena");
        var tpc = Object.FindAnyObjectByType<ThirdPersonCamera>();
        Checar(tpc != null && tpc.alvo != null && tpc.jogador != null, "Câmera 3ª pessoa seguindo o Marcos");
        Checar(Object.FindObjectsByType<GameManager>().Length == 1, "Só 1 GameManager (sem duplicata)");

        int semScript = todos.Sum(g => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g));
        Checar(semScript == 0, "Nenhum script faltando (Missing Script)", semScript + " encontrados");

        Secao("Jogador");
        var pw = Object.FindAnyObjectByType<PlayerWeapons>();
        Checar(pw != null && pw.armas != null && pw.armas.Length == 4 && pw.armas.All(a => a != null), "4 armas no jogador", pw != null && pw.armas != null ? string.Join(", ", pw.armas.Where(a => a).Select(a => a.nome)) : "");
        if (pw != null && pw.armas != null)
        {
            foreach (var a in pw.armas.Where(a => a != null))
            {
                Checar(a.dano > 0 && a.tirosPorSegundo > 0 && a.tamanhoPente > 0 && a.projeteis > 0, $"{a.nome}: valores válidos", $"dano {a.dano} x{a.projeteis}, {a.tirosPorSegundo}/s, pente {a.tamanhoPente}");
                Checar(a.somTiro != null && a.somRecarga != null, $"{a.nome}: som de tiro e recarga", a.somTiro != null ? a.somTiro.name : "");
                if (a.modelo == null) Aviso($"{a.nome} sem modelo 3D (vai atirar sem arma na mão)");
            }
            var sniper = pw.armas.FirstOrDefault(a => a != null && a.nome == "Sniper");
            Checar(sniper != null && sniper.fovMira > 0f && sniper.perfura > 0, "Sniper com zoom e bala que atravessa", sniper != null ? $"FOV {sniper.fovMira}, atravessa {sniper.perfura}" : "");
            Checar(pw.materialTracer != null, "Material do rastro de tiro");
        }
        var anim = pw != null ? pw.GetComponentInChildren<Animator>() : null;
        if (anim == null) Aviso("Marcos sem Animator (cápsula placeholder)");
        else
        {
            Checar(anim.runtimeAnimatorController != null, "Animator do Marcos com controller", anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "");
            if (anim.avatar != null && anim.avatar.isHuman) Checar(anim.avatar.isValid, "Avatar humanoide válido (Mixamo)");
            Checar(!anim.applyRootMotion, "Root motion desligado");
            Checar(pw.pontoArma != null, "Ponto da arma na mão do Marcos", pw.pontoArma != null ? pw.pontoArma.parent.name : "");
            SinalDaLiaSetup.MedirOssos(anim.gameObject, out float pe, out _, out _, out _, out _);
            SinalDaLiaSetup.MedirMalha(anim.gameObject, out float peM, out float topoM);
            Checar(topoM - peM > 1.6f && topoM - peM < 1.85f && Mathf.Abs(pe - (pw.transform.position.y - 0.04f)) < 0.12f, "Marcos com altura de adulto (malha, com a cabeça) e pé no chão", $"{topoM - peM:0.00} m, pé {pe - pw.transform.position.y:+0.00;-0.00}");
        }
        var player = pw != null ? pw.gameObject : null;
        Checar(player != null && player.CompareTag("Player"), "Marcos com tag Player");
        if (pw != null && pw.armas != null)
            Checar(pw.armas.Count(a => a != null && a.liberadaNoInicio) == 1 && pw.armas[0].liberadaNoInicio, "Progressão: começa só com a pistola",
                string.Join(", ", pw.armas.Where(a => a != null && !a.liberadaNoInicio).Select(a => a.nome)) + " pra achar");
        var liaNpc = Object.FindAnyObjectByType<LiaNPC>(FindObjectsInactive.Include);
        Checar(liaNpc != null, "Lia na clínica");
        if (liaNpc != null && liaNpc.animator != null)
        {
            Checar(liaNpc.animator.runtimeAnimatorController != null, "Lia com animações (escondida / acenando)");
            SinalDaLiaSetup.MedirMalha(liaNpc.animator.gameObject, out float peL, out float topoL);
            Checar(topoL - peL > 0.95f && topoL - peL < 1.35f, "Lia com tamanho de criança (malha)", $"{topoL - peL:0.00} m");
        }

        Secao("História (v0.3)");
        if (liaNpc != null)
        {
            var agL = liaNpc.GetComponent<NavMeshAgent>();
            Checar(agL != null && !agL.enabled, "Lia com NavMeshAgent (desligado até seguir o pai)");
            var ctrlL = liaNpc.animator != null ? liaNpc.animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController : null;
            Checar(ctrlL != null && new[] { "Achou", "Speed", "Morrer", "Dano" }.All(n => ctrlL.parameters.Any(p => p.name == n)), "Animator da Lia: esconder, acenar, andar, morrer, dano");
        }
        Checar(player != null && player.GetComponent<PlayerHunger>() != null, "Marcos com fome (PlayerHunger)");
        if (gm != null)
        {
            Checar(gm.desenho != null, "Desenho da Lia ligado no GameManager");
            var escI = Object.FindAnyObjectByType<EscolaInterior>();
            if (gm.desenho != null && escI != null)
                Checar(gm.desenho.transform.IsChildOf(escI.transform) && NavMesh.SamplePosition(gm.desenho.transform.position, out _, 1.5f, NavMesh.AllAreas),
                    "Desenho na carteira da Lia (sala do 4º B, dentro da escola), alcançável");
            else if (gm.desenho != null && gm.escola != null)
            {
                float dd = Vector3.Distance(gm.desenho.transform.position, gm.escola.position);
                Checar(dd < gm.raioChegada - 1f && NavMesh.SamplePosition(gm.desenho.transform.position, out _, 1f, NavMesh.AllAreas), "Desenho no chão da escola, alcançável", $"{dd:0.0} m do farol");
            }
        }
        Checar(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).All(t => !t.name.StartsWith("Soldado_")), "Sem soldados (o Marcos joga sozinho)");

        Secao("Menus, música e entrega (v0.4 / v0.5 / v1.0)");
        var somV = Object.FindAnyObjectByType<SomManager>();
        if (somV != null)
        {
            var musicas = new[] { somV.musicaMenu, somV.musicaDia, somV.musicaNoite, somV.musicaFinal, somV.somVitoria, somV.somGameOver, somV.helicoptero };
            Checar(musicas.All(c => c != null), "Músicas (menu, dia, noite, final), vinhetas e helicóptero ligados", $"{musicas.Count(c => c != null)}/{musicas.Length}");
            var musicaNoite = somV.musicaNoite;
            if (musicaNoite != null) Checar(musicaNoite.length > 10f, "Música da noite com loop longo", $"{musicaNoite.length:0.0} s");
        }
        Checar(PlayerSettings.productName == "Sinal da Lia", "Nome do jogo na build: \"Sinal da Lia\"", PlayerSettings.productName);
        var tpcV = Object.FindAnyObjectByType<ThirdPersonCamera>();
        Checar(tpcV != null && tpcV.maxMovimentoPorQuadro >= 500f, "Câmera não corta giros rápidos do mouse", tpcV != null ? $"limite {tpcV.maxMovimentoPorQuadro:0} px/quadro" : "");
        var pcV = Object.FindAnyObjectByType<PlayerController>();
        Checar(pcV != null && pcV.corpoSegueCamera, "Marcos sempre virado pra onde a câmera olha");
        Checar(File.Exists("COMO_JOGAR.txt"), "COMO_JOGAR.txt na raiz do projeto (vai junto com a build)");

        Secao("Som, efeitos e cenário (v0.2)");
        var som = Object.FindAnyObjectByType<SomManager>();
        Checar(som != null, "SomManager na cena");
        if (som != null)
        {
            var clips = new[] { som.danoJogador, som.vazio, som.acerto, som.pegarItem, som.cura, som.zumbiAtaque, som.zumbiMorte, som.impactoCarne, som.impactoParede, som.vento, som.noite, som.fogo }
                .Concat(som.passos ?? new AudioClip[0]).Concat(som.gemidos ?? new AudioClip[0]).ToArray();
            int faltando = clips.Count(c => c == null);
            Checar(faltando == 0 && som.passos.Length == 4 && som.gemidos.Length == 4, "Todos os sons ligados", $"{clips.Length - faltando}/{clips.Length}");
        }
        var radio = Object.FindAnyObjectByType<RadioSystem>();
        Checar(radio != null && radio.somChiado != null, "Rádio com som de chiado");
        var ef = Object.FindAnyObjectByType<Efeitos>();
        Checar(ef != null && ef.materialParticula != null, "Efeitos de sangue/poeira com material");
        Checar(ws != null && ws.materialRapido != null, "Material do zumbi rápido");
        var cenario = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == "Cenario_v02");
        Checar(cenario != null, "Cenário v0.2 na cena");
        if (cenario != null)
        {
            int nCarros = cenario.GetComponentsInChildren<Transform>().Count(t => t.parent == cenario && (t.name == "Carro" || t.name == "Onibus_Escolar" || t.name == "Ambulancia"));
            int nFogo = cenario.GetComponentsInChildren<ParticleSystem>().Length;
            int nLuzes = cenario.GetComponentsInChildren<LuzNoturna>().Length;
            Checar(nCarros >= 8, "Carros abandonados", nCarros + " veículos");
            Checar(nFogo >= 6, "Barris pegando fogo", nFogo + " fogueiras");
            Checar(nLuzes >= 10, "Luzes de poste/fogo controladas por dia/noite", nLuzes + " luzes");
            int nFumaca = cenario.GetComponentsInChildren<Transform>().Count(t => t.name == "Fumaca");
            int nManchas = cenario.GetComponentsInChildren<Transform>().Count(t => t.name == "Mancha");
            int nEntulho = cenario.GetComponentsInChildren<Transform>().Count(t => t.name == "Entulho");
            Checar(nFumaca >= 3, "Colunas de fumaça nos telhados", nFumaca + "");
            Checar(nManchas >= 10, "Manchas de sangue no chão", nManchas + "");
            Checar(nEntulho >= 6, "Entulho nas ruas", nEntulho + "");
            var matFogo = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Fogo.mat");
            Checar(matFogo != null && matFogo.renderQueue >= 3000, "Fogo transparente com brilho");
            Checar(RenderSettings.skybox != null && RenderSettings.skybox.name == "CeuApocalipse", "Céu nublado de apocalipse", RenderSettings.skybox != null ? RenderSettings.skybox.name : "nenhum");
            var luzesCenario = cenario.GetComponentsInChildren<Light>();
            Checar(luzesCenario.All(l => l.shadows == LightShadows.None), "Luzes do cenário sem sombra (desempenho)");
            Checar(luzesCenario.Length <= 40, "Quantidade de luzes do cenário sob controle", luzesCenario.Length + " luzes");
            // objetos de rua em pé (antes o poste ficava deitado e o banco em pé)
            float AlturaDe(string prefixo)
            {
                var t = cenario.Cast<Transform>().FirstOrDefault(x => x.name.StartsWith(prefixo));
                if (t == null) return -1f;
                var rs = t.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
                if (rs.Length == 0) return -1f;
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                return b.size.y;
            }
            float hPoste = AlturaDe("ParkLamp"), hBanco = AlturaDe("Bench"), hBarril = AlturaDe("Prop_OilBerrel");
            Checar(hPoste < 0f || hPoste > 2f, "Postes de luz em pé", $"{hPoste:0.00} m");
            Checar(hBanco < 0f || hBanco < 1.6f, "Bancos apoiados no chão (não em pé)", $"{hBanco:0.00} m");
            Checar(hBarril < 0f || hBarril < 1.15f, "Barris do tamanho certo", $"{hBarril:0.00} m");
        }

        Secao("Zumbi (prefab)");
        if (ws != null && ws.prefabZumbi != null)
        {
            var z = ws.prefabZumbi;
            var ai = z.GetComponent<ZombieAI>();
            Checar(ai != null && z.GetComponent<ZombieHealth>() != null && z.GetComponent<NavMeshAgent>() != null && z.GetComponent<Collider>() != null, "Componentes: AI, vida, NavMeshAgent, colisor");
            if (ai != null)
            {
                bool temAnimator = ai.animator != null && ai.animator.runtimeAnimatorController != null;
                Checar((temAnimator || ai.clip != null) && ai.modelo != null, "Animação e modelo ligados",
                    temAnimator ? "Animator " + ai.animator.runtimeAnimatorController.name : (ai.clip != null ? ai.clip.name : ""));
            }
            var variantes = ws.variantes != null && ws.variantes.Length > 0 ? ws.variantes : new[] { z };
            Checar(variantes.All(v => v != null), $"{variantes.Length} zumbis no sorteio ({variantes.Where(v => v).Distinct().Count()} tipos)");
            foreach (var v in variantes.Where(v => v != null).Distinct())
            {
                var temp = (GameObject)PrefabUtility.InstantiatePrefab(v);
                var aiV = temp.GetComponent<ZombieAI>();
                var rends = temp.GetComponentsInChildren<Renderer>();
                // medido pelos ossos: 1 personagem, pé no chão, centrado, altura de zumbi (rastejante é baixo)
                SinalDaLiaSetup.MedirOssos(temp, out float pe, out _, out Vector3 quadril, out _, out _);
                SinalDaLiaSetup.MedirMalha(temp, out float peM, out float topoM);
                bool ok1 = topoM - peM > 0.8f && topoM - peM < 2.4f && Mathf.Abs(pe) < 0.15f
                           && new Vector2(quadril.x, quadril.z).magnitude < 0.45f && rends.Length <= 4;
                Checar(ok1, $"{v.name}: 1 personagem, pé no chão, centrado e com altura de gente (malha)",
                    $"altura {topoM - peM:0.00} m, pé {pe:+0.00;-0.00}, cabeça a {(aiV != null ? aiV.alturaCabeca : 0f):0.00} m, {rends.Length} malha(s)");
                Object.DestroyImmediate(temp);
            }
        }

        Secao("NavMesh e mapa");
        var surf = Object.FindAnyObjectByType<NavMeshSurface>();
        Checar(surf != null && surf.navMeshData != null, "NavMesh salvo na cena");
        var tri = NavMesh.CalculateTriangulation();
        Checar(tri.vertices.Length > 100, "NavMesh tem área andável", tri.vertices.Length + " vértices");
        if (gm != null && gm.jogador != null && gm.escola != null && gm.clinica != null)
        {
            Caminho(gm.jogador.position, gm.escola.position, "Caminho andável: início -> escola");
            Caminho(gm.escola.position, gm.clinica.position, "Caminho andável: escola -> clínica");
            float y0 = gm.origemCidade.y;
            Checar(Mathf.Abs(gm.escola.position.y - y0) < 1.5f && Mathf.Abs(gm.clinica.position.y - y0) < 1.5f, "Escola e clínica no chão (não em telhado)");
            float d1 = Vector3.Distance(gm.jogador.position, gm.escola.position), d2 = Vector3.Distance(gm.escola.position, gm.clinica.position);
            Checar(d1 > 30f && d2 > 50f, "Distâncias razoáveis entre objetivos", $"início->escola {d1:0} m, escola->clínica {d2:0} m");
        }

        ChecarInteriores(gm);

        Secao("Visual");
        var rosa = new HashSet<string>();
        int semMaterial = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
        {
            if (r is LineRenderer) continue;
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) { semMaterial++; continue; }
                string sh = m.shader != null ? m.shader.name : "";
                if (sh == "Hidden/InternalErrorShader" || sh == "Standard" || sh.StartsWith("Legacy Shaders") || !m.shader.isSupported)
                    rosa.Add(m.name + " [" + sh + "]");
            }
        }
        Checar(rosa.Count == 0, "Nenhum material rosa (shader incompatível com URP)", rosa.Count + ": " + string.Join(", ", rosa.Take(8)));
        if (semMaterial > 0) Aviso(semMaterial + " slots de material vazios (aparecem rosa)");
        int estaticos = todos.Count(g => GameObjectUtility.AreStaticEditorFlagsSet(g, StaticEditorFlags.BatchingStatic));
        Checar(estaticos > 100, "Cidade marcada como estática (desempenho)", estaticos + " objetos");

        sb.AppendLine($"\nRESULTADO DA CHECAGEM: {ok} OK, {falhas} FALHA(S), {avisos} aviso(s)");
        Directory.CreateDirectory(Path.GetDirectoryName(AutoTesteV01.Relatorio));
        File.WriteAllText(AutoTesteV01.Relatorio, sb.ToString());
        Debug.Log("[Validação v0.1] Checagem: " + ok + " OK, " + falhas + " falhas, " + avisos + " avisos");
    }

    // escola e clínica por dentro
    static void ChecarInteriores(GameManager gm)
    {
        var esc = Object.FindAnyObjectByType<EscolaInterior>();
        var cli = Object.FindAnyObjectByType<ClinicaInterior>();
        if (esc == null && cli == null) { Aviso("Cena sem interiores (pacotes de móveis não encontrados em Assets/Models/Interiores)"); return; }
        Secao("Interiores (v1.4)");
        Checar(esc != null && cli != null, "Escola e clínica por dentro na cena");
        Checar(Object.FindObjectsByType<NavMeshSurface>().Count(s => s.navMeshData != null) >= 2, "NavMesh da cidade + NavMesh dos interiores salvos");
        foreach (var it in new Interior[] { esc != null ? esc.interior : null, cli != null ? cli.interior : null })
        {
            if (it == null) { Checar(false, "Interior ligado ao roteiro"); continue; }
            Checar(it.entrada != null && it.saida != null && it.portaFora != null && it.portaDentro != null, $"{it.nome}: entrada, saída e as duas portas ligadas");
            if (it.entrada == null || it.saida == null || gm == null) continue;
            Checar(it.entrada.position.y < gm.origemCidade.y - 400f, $"{it.nome}: fica longe da cidade (sem sol/céu atrapalhando)", $"y = {it.entrada.position.y:0}");
            Transform marcador = it == esc?.interior ? gm.escola : gm.clinica;
            if (marcador != null)
            {
                Checar(Vector3.Distance(it.portaFora.Posicao, marcador.position) < 26f, $"{it.nome}: porta da rua perto do marcador", $"{Vector3.Distance(it.portaFora.Posicao, marcador.position):0.0} m");
                Caminho(marcador.position, it.saida.position, $"{it.nome}: caminho andável até a porta da rua");
            }
            int luzes = it.GetComponentsInChildren<Light>(true).Length;
            Checar(luzes >= 8 && luzes <= 45 && it.GetComponentsInChildren<Light>(true).All(l => l.shadows == LightShadows.None), $"{it.nome}: luzes próprias, sem sombra (desempenho)", luzes + " luzes");
            var zs = it.GetComponentsInChildren<ZombieAI>(true);
            int fora = zs.Count(z => !NavMesh.SamplePosition(z.transform.position, out _, 0.6f, NavMesh.AllAreas));
            Checar(zs.Length > 0 && fora == 0 && zs.All(z => !z.voltarAoPool && z.ficarParado), $"{it.nome}: zumbis fixos em cima do NavMesh", $"{zs.Length} zumbis, {fora} fora");
            Checar(it.zumbis != null && !it.zumbis.activeSelf, $"{it.nome}: zumbis dormem até o Marcos entrar");
        }
        if (esc != null)
        {
            var e = esc.interior;
            Checar(esc.mochila != null && esc.chave != null && esc.porta4B != null && esc.folha4B != null && esc.desenho != null && esc.zumbisFuga != null,
                "Escola: mochila, chave, porta do 4º B, desenho e horda da fuga ligados");
            if (e != null && e.entrada != null)
            {
                Caminho(e.entrada.position, esc.mochila.Posicao, "Escola: entrada -> mochila (corredor)");
                Caminho(e.entrada.position, esc.chave.Posicao, "Escola: entrada -> quadro de chaves (professores)");
                Caminho(e.entrada.position, esc.porta4B.Posicao, "Escola: entrada -> porta do 4º B");
                // a porta trancada corta o NavMesh também no Editor: o caminho de verdade (porta aberta) é testado em Play
                Caminho(esc.porta4B.transform.parent.TransformPoint(new Vector3(23.5f, 0f, 13f)), esc.desenho.transform.position, "Escola: porta do 4º B -> carteira da Lia");
            }
            Checar(esc.folha4B != null && !esc.folha4B.aberta && esc.folha4B.GetComponentInChildren<NavMeshObstacle>() != null, "Escola: porta do 4º B começa trancada");
        }
        if (cli != null)
        {
            var c = cli.interior;
            Checar(cli.esconderijo != null && cli.armario != null && cli.armarioMovel != null && cli.falsos != null && cli.falsos.Length == 2
                   && cli.sustos != null && cli.sustos.Length == 2 && cli.sustos.All(s => s != null && !s.activeSelf), "Clínica: esconderijo, armário de empurrar e 2 esconderijos falsos com susto");
            if (c != null && c.entrada != null)
            {
                Caminho(c.entrada.position, cli.armario.Posicao, "Clínica: entrada -> armário do depósito");
                foreach (var f in cli.falsos) if (f != null) Caminho(c.entrada.position, f.Posicao, "Clínica: entrada -> " + f.id);
            }
            var liaN = Object.FindAnyObjectByType<LiaNPC>(FindObjectsInactive.Include);
            if (liaN != null && cli.esconderijo != null)
            {
                Vector3 d = liaN.transform.position - cli.esconderijo.position;
                d.y = 0f;
                Checar(d.magnitude < 0.8f, "Lia escondida no nicho atrás do armário (dentro da clínica)", $"{d.magnitude:0.00} m do esconderijo");
            }
        }
        var fachadas = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Count(t => t.name.StartsWith("Fachada_"));
        Checar(fachadas >= 2, "Fachadas com porta na rua (escola e clínica)", fachadas + "");
    }

    static void Caminho(Vector3 a, Vector3 b, string nome)
    {
        var path = new NavMeshPath();
        bool achou = NavMesh.SamplePosition(a, out var ha, 3f, NavMesh.AllAreas) & NavMesh.SamplePosition(b, out var hb, 3f, NavMesh.AllAreas);
        if (achou) NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path);
        float comp = 0f;
        for (int i = 1; i < path.corners.Length; i++) comp += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        Checar(achou && path.status == NavMeshPathStatus.PathComplete, nome, achou ? $"{path.status}, {comp:0} m andando" : "ponto fora do NavMesh");
    }
}
