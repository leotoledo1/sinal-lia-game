using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Teste automático da v0.1. Só roda quando chamado pelo menu "Sinal da Lia > Validar v0.1".
// Joga sozinho: anda, atira, troca arma, pega munição, sai da horda, passa por todas as fases.
public class AutoTesteV01 : MonoBehaviour
{
    public const string FLAG = "SinalDaLia_AutoTeste";
    public static string Relatorio => Path.Combine(Application.dataPath, "../Logs/Validacao_v01.txt");

    int ok, falhas, avisos, erros;
    readonly StringBuilder sb = new StringBuilder();
    bool imortal;
    InputSettings inputOriginal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
#if UNITY_EDITOR
        if (!UnityEditor.SessionState.GetBool(FLAG, false)) return;
        UnityEditor.SessionState.SetBool(FLAG, false);
        var go = new GameObject("AutoTeste");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoTesteV01>();
#endif
    }

    void OnEnable() => Application.logMessageReceived += AoLogar;
    void OnDisable() => Application.logMessageReceived -= AoLogar;

    void AoLogar(string msg, string stack, LogType tipo)
    {
        if (tipo != LogType.Error && tipo != LogType.Exception) return;
        erros++;
        if (erros <= 10) sb.AppendLine("  [ERRO NO CONSOLE] " + msg + "\n    " + stack.Replace("\n", "\n    "));
    }

    void Update()
    {
        if (imortal && PlayerHealth.Instance != null) PlayerHealth.Instance.Curar(1000f);
    }

    void Checar(bool cond, string nome, string detalhe = "")
    {
        if (cond) { ok++; sb.AppendLine("  [OK] " + nome + (detalhe != "" ? "  (" + detalhe + ")" : "")); }
        else { falhas++; sb.AppendLine("  [FALHA] " + nome + (detalhe != "" ? "  - " + detalhe : "")); }
    }

    void Aviso(string msg) { avisos++; sb.AppendLine("  [AVISO] " + msg); }
    void Info(string msg) => sb.AppendLine("  [INFO] " + msg);
    void Secao(string nome) => sb.AppendLine("\n== " + nome);

    static object Chamar(object o, string metodo, params object[] args) =>
        o.GetType().GetMethod(metodo, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, args);

    static void DefinirTempo(GameManager gm, float t) => typeof(GameManager).GetProperty("TempoRestante").SetValue(gm, t);

    static float DistH(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    static void Teleportar(Vector3 p)
    {
        var ph = PlayerHealth.Instance;
        var cc = ph.GetComponent<CharacterController>();
        cc.enabled = false;
        ph.transform.position = p;
        cc.enabled = true;
        Physics.SyncTransforms();
    }

    // altura do chão de verdade (colisor) logo abaixo, ignorando o próprio objeto
    static float ChaoAbaixo(Vector3 p, Transform ignorar)
    {
        float melhor = p.y - 10f;
        foreach (var h in Physics.RaycastAll(p + Vector3.up * 0.8f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
            if (!h.transform.IsChildOf(ignorar) && h.collider.GetComponentInParent<ZombieAI>() == null && h.point.y > melhor) melhor = h.point.y;
        return melhor;
    }

    // distância até o primeiro obstáculo no centro da tela (ignorando o Marcos)
    static float DistanciaLivre()
    {
        var c = Camera.main.transform;
        var jogador = PlayerHealth.Instance.transform;
        float menor = 100f;
        foreach (var h in Physics.RaycastAll(c.position, c.forward, 100f, ~0, QueryTriggerInteraction.Ignore))
            if (!h.transform.IsChildOf(jogador)) menor = Mathf.Min(menor, h.distance);
        return menor;
    }

    IEnumerator MirarLivre(float minimo)
    {
        var tpc = Camera.main.GetComponent<ThirdPersonCamera>();
        var f = BindingFlags.Instance | BindingFlags.NonPublic;
        var campoYaw = typeof(ThirdPersonCamera).GetField("yaw", f);
        typeof(ThirdPersonCamera).GetField("pitch", f).SetValue(tpc, 6f);
        yield return null;
        yield return null;
        for (int i = 0; i < 16 && DistanciaLivre() < minimo; i++)
        {
            campoYaw.SetValue(tpc, (float)campoYaw.GetValue(tpc) + 22.5f);
            yield return null;
            yield return null;
        }
        yield return new WaitForSeconds(0.3f); // corpo do Marcos acompanha a câmera
    }

    // teclado/mouse simulados funcionam mesmo sem a janela em foco (modo automático)
    void ConfigurarInput()
    {
        if (inputOriginal != null) return;
        inputOriginal = InputSystem.settings;
        var s = ScriptableObject.CreateInstance<InputSettings>();
        s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = s;
    }

    // captura de tela durante a validacao

    // renderiza a câmera do jogo e salva em Logs/Fotos/<nome>.png
    static void Foto(string nome)
    {
        var cam = Camera.main;
        if (cam == null) return;
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

    // foto com o HUD junto (Canvas "Overlay" não sai no cam.Render: vira "Camera" só durante a foto)
    static void FotoComHUD(string nome)
    {
        var go = GameObject.Find("HUD_Canvas");
        var canvas = go != null ? go.GetComponent<Canvas>() : null;
        var cam = Camera.main;
        if (canvas == null || cam == null) { Foto(nome); return; }
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = cam.nearClipPlane + 0.05f;
        Canvas.ForceUpdateCanvases();
        Foto(nome);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }

    // vira a câmera 3ª pessoa pra um ponto (mexe no yaw/pitch internos)
    static void OlharPara(Vector3 alvo, float pitch = 8f)
    {
        var tpc = Camera.main != null ? Camera.main.GetComponent<ThirdPersonCamera>() : null;
        if (tpc == null || tpc.alvo == null) return;
        Vector3 d = alvo - tpc.alvo.position;
        d.y = 0f;
        var f = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(ThirdPersonCamera).GetField("yaw", f).SetValue(tpc, Quaternion.LookRotation(d).eulerAngles.y);
        typeof(ThirdPersonCamera).GetField("pitch", f).SetValue(tpc, pitch);
    }

    // ponta do pé (osso "Foot..._end" / "Toe") mais baixa - vale pra Quaternius e Mixamo
    static float PeOssos(GameObject go)
    {
        float menor = float.MaxValue;
        foreach (var t in go.GetComponentsInChildren<Transform>())
            if ((t.name.Contains("Foot") && t.name.EndsWith("_end")) || t.name.Contains("Toe")) menor = Mathf.Min(menor, t.position.y);
        return menor < float.MaxValue ? menor : PeMaisBaixo(go);
    }

    // vértice mais baixo da malha já deformada pela animação
    static float PeMaisBaixo(GameObject go)
    {
        float menor = float.MaxValue;
        var malha = new Mesh();
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            smr.BakeMesh(malha, true);
            foreach (var v in malha.vertices) menor = Mathf.Min(menor, (smr.transform.position + smr.transform.rotation * v).y);
        }
        Object.Destroy(malha);
        return menor;
    }

    // aperta E (teclado virtual) - usa o Interativo mais perto
    IEnumerator ApertarE()
    {
        ConfigurarInput();
        var kb = InputSystem.AddDevice<Keyboard>();
        kb.MakeCurrent();
        yield return null;
        InputSystem.QueueStateEvent(kb, new KeyboardState(Key.E));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(kb, new KeyboardState());
        yield return null;
        InputSystem.RemoveDevice(kb);
    }

    // teleporta pro ponto andável mais perto (não cai dentro de móvel)
    static void TeleportarNoChao(Vector3 p)
    {
        if (NavMesh.SamplePosition(p, out var h, 2.5f, NavMesh.AllAreas)) p = h.position;
        Teleportar(p);
    }

    static string TextoDica()
    {
        var t = GameObject.Find("Dica")?.GetComponent<UnityEngine.UI.Text>();
        return t != null && t.enabled ? t.text : "";
    }

    static int ZumbisAtivosEm(Transform raiz) => raiz == null ? 0 : raiz.GetComponentsInChildren<ZombieAI>().Count(z => !z.EstaMorto && z.gameObject.activeInHierarchy);

    static void LimparZumbis()
    {
        WaveSpawner.Instance.LimparTodos();
        foreach (var z in FindObjectsByType<ZombieAI>()) Destroy(z.gameObject);
    }

    static int ZumbisVivos()
    {
        int n = 0;
        foreach (var z in FindObjectsByType<ZombieAI>()) if (!z.EstaMorto) n++;
        return n;
    }

    ZombieAI CriarZumbi(Vector3 pos)
    {
        var prefab = WaveSpawner.Instance.prefabZumbi;
        if (!NavMesh.SamplePosition(pos, out var hit, 3f, NavMesh.AllAreas)) return null;
        var z = Instantiate(prefab, hit.position, Quaternion.identity).GetComponent<ZombieAI>();
        z.Configurar(1f, 1f, true);
        return z;
    }

    IEnumerator Start()
    {
        sb.AppendLine("\n######## TESTE EM PLAY ########");
        ZombieAI.AnimarForaDaTela = true;
        yield return new WaitForSeconds(1f);

        var gm = GameManager.Instance;
        var ph = PlayerHealth.Instance;
        var pw = PlayerWeapons.Instance;

        Secao("Menu principal (v0.4)");
        Checar(gm != null && ph != null && pw != null, "GameManager, jogador e armas existem");
        if (gm == null || ph == null || pw == null) { Finalizar(); yield break; }
        var menu = GameObject.Find("MenuPrincipal");
        Checar(gm.NoMenu && menu != null && menu.activeInHierarchy && !gm.JogoAtivo, "Jogo abre no menu principal (parado)");
        Checar(Cursor.lockState == CursorLockMode.None, "Mouse solto no menu pra clicar nos botões");
        Checar(SomManager.Instance != null && SomManager.Instance.MusicaAtual == SomManager.Instance.musicaMenu && SomManager.Instance.MusicaAtual != null, "Música do menu tocando");
        Checar(WaveSpawner.Instance != null && FindObjectsByType<ZombieAI>().Count(z => !z.EstaMorto) == 0, "Nenhum zumbi no menu");
        FotoComHUD("00_menu");
        Checar(RenderSettings.skybox != null && RenderSettings.skybox.shader != null && RenderSettings.skybox.shader.name == "SinalDaLia/Ceu" && RenderSettings.skybox.shader.isSupported,
            "Céu novo (gradiente + nuvens) funcionando", RenderSettings.skybox != null && RenderSettings.skybox.shader != null ? RenderSettings.skybox.shader.name : "sem céu");
        Checar(RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight, "Luz ambiente em 3 tons (céu / lado / chão)", RenderSettings.ambientMode.ToString());
        var dadosCam = Camera.main != null ? Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
        Checar(dadosCam != null && dadosCam.antialiasing != UnityEngine.Rendering.Universal.AntialiasingMode.None, "Anti-serrilhado ligado", dadosCam != null ? dadosCam.antialiasing.ToString() : "");
        Checar(GameObject.Find("Abrigo_Marcos") != null, "Acampamento do Marcos no começo do mapa");
        gm.Jogar();
        yield return new WaitForSeconds(1.2f);

        Secao("Abertura (v1.3)");
        Checar(gm.EmCena && !gm.JogoAtivo && gm.Escuro > 0.9f && gm.Cartela.Contains("surto"), "JOGAR mostra a abertura com a história (tela preta)", gm.Cartela.Replace("\n", " "));
        FotoComHUD("00b_abertura");
        gm.PularCena();
        float tAb = Time.time;
        while (!gm.JogoAtivo && Time.time - tAb < 10f) yield return null;
        Checar(gm.JogoAtivo, "ENTER pula a abertura e o jogo começa", $"{Time.time - tAb:0.0} s");
        yield return new WaitForSeconds(0.4f);

        Secao("Início");
        Checar(!gm.NoMenu && gm.JogoAtivo && gm.FaseAtual == Fase.Dia1, "JOGAR começa o Dia 1");
        var titulo = GameObject.Find("TituloFase")?.GetComponent<UnityEngine.UI.Text>();
        Checar(titulo != null && titulo.enabled && titulo.text == "DIA 1", "Título grande da fase aparece", titulo != null ? titulo.text : "");
        var tutorial = GameObject.Find("Tutorial")?.GetComponent<UnityEngine.UI.Text>();
        Checar(tutorial != null && tutorial.enabled && tutorial.text.Contains("WASD"), "Dica de tutorial dos controles aparece", tutorial != null ? tutorial.text : "");
        var marcador = GameObject.Find("Marcador")?.GetComponent<UnityEngine.UI.Text>();
        Checar(marcador != null && marcador.enabled && marcador.text.Contains("m"), "Seta do objetivo na tela (com distância)", marcador != null ? marcador.text.Replace("\n", " ") : "");
        Checar(SomManager.Instance.MusicaAtual == SomManager.Instance.musicaDia, "Música do dia tocando");
        FotoComHUD("01a_hud_dia1");
        Checar(RadioSystem.Instance != null && RadioSystem.Instance.Mostrando, "Rádio mostra a 1ª mensagem", RadioSystem.Instance?.Texto);
        Checar(RadioSystem.Instance != null && RadioSystem.Instance.Falando && RadioSystem.Instance.PeloRadio, "1ª fala tem VOZ (Base Ômega pelo rádio)");
        Checar(SomManager.Instance.Abafado, "Música abaixa enquanto alguém fala");
        var legendaQuem = GameObject.Find("RadioQuem")?.GetComponent<UnityEngine.UI.Text>();
        Checar(legendaQuem != null && legendaQuem.enabled && legendaQuem.text.Contains("BASE"), "Legenda mostra quem está falando", legendaQuem != null ? legendaQuem.text : "");
        Checar(ObjectiveManager.Instance != null && ObjectiveManager.Instance.Texto != "", "Objetivo definido", ObjectiveManager.Instance?.Texto);
        Checar(GameObject.Find("HUD_Canvas") != null, "HUD criado");
        int caixas = FindObjectsByType<AmmoPickup>().Length;
        Checar(caixas >= 5, "Munição espalhada pela cidade", caixas + " caixas");
        int inuteis = FindObjectsByType<AmmoPickup>().Count(c => !c.liberaArma && c.arma != null && c.arma.municaoInfinita);
        Checar(inuteis == 0, "Nenhuma caixa de munição de pistola (infinita, não dava pra pegar)", inuteis + " caixas inúteis");
        Checar(NavMesh.SamplePosition(ph.transform.position, out _, 1.5f, NavMesh.AllAreas), "Jogador começa em cima do NavMesh");
        Checar(gm.farolEscola != null && gm.farolEscola.activeInHierarchy, "Farol da escola aceso no Dia 1");
        Checar(gm.lia != null && !gm.lia.activeSelf, "Lia escondida até ser encontrada");
        int nComida = FindObjectsByType<ItemPickup>().Count(i => i.tipo == TipoItem.Comida);
        int nKits = FindObjectsByType<ItemPickup>().Count(i => i.tipo == TipoItem.KitMedico);
        Checar(nComida >= 5 && nKits >= 3, "Comida e kits médicos espalhados", $"{nComida} comidas, {nKits} kits");
        Checar(PlayerHunger.Instance != null && PlayerHunger.Instance.Fome > PlayerHunger.Instance.fomeMaxima - 2f, "Fome começa cheia", PlayerHunger.Instance != null ? $"{PlayerHunger.Instance.Fome:0.0}" : "");
        Foto("01_inicio");
        // Marcos mirando (botão direito simulado): braços levantados apontando a arma
        ConfigurarInput();
        var kbV = InputSystem.AddDevice<Keyboard>();
        kbV.MakeCurrent();
        var mouseV = InputSystem.AddDevice<Mouse>();
        mouseV.MakeCurrent();
        InputSystem.QueueStateEvent(mouseV, new MouseState().WithButton(MouseButton.Right));
        yield return new WaitForSeconds(0.7f);
        var paM = ph.GetComponent<PlayerAnimator>();
        Checar(paM != null && paM.PesoMira > 0.9f, "Botão direito: Marcos levanta a arma pra mirar", paM != null ? $"peso {paM.PesoMira:0.00}" : "");
        Foto("01b_mirando");
        // a cabeça não pode ficar girando sozinha (mirando pra cima, e também com o jogo pausado)
        var cabecaM = ph.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Head");
        if (cabecaM != null)
        {
            var tpcC = Camera.main.GetComponent<ThirdPersonCamera>();
            typeof(ThirdPersonCamera).GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tpcC, 20f);
            yield return null;
            float GiroCabeca(Quaternion r) => Quaternion.Angle(r, Quaternion.Inverse(ph.transform.rotation) * cabecaM.rotation);
            Quaternion rc0 = Quaternion.Inverse(ph.transform.rotation) * cabecaM.rotation;
            float giroJogo = 0f, giroPausa = 0f;
            for (int i = 0; i < 30; i++) { yield return null; giroJogo = Mathf.Max(giroJogo, GiroCabeca(rc0)); }
            gm.AlternarPausa();
            yield return null;
            rc0 = Quaternion.Inverse(ph.transform.rotation) * cabecaM.rotation;
            for (int i = 0; i < 30; i++) { yield return null; giroPausa = Mathf.Max(giroPausa, GiroCabeca(rc0)); }
            gm.AlternarPausa();
            Checar(giroJogo < 25f, "Cabeça do Marcos não fica girando (mirando)", $"variou {giroJogo:0}° em 30 quadros");
            Checar(giroPausa < 3f, "Cabeça do Marcos parada com o jogo pausado", $"variou {giroPausa:0}° em 30 quadros");
        }
        InputSystem.QueueStateEvent(mouseV, new MouseState());
        yield return null;
        InputSystem.RemoveDevice(mouseV);
        InputSystem.RemoveDevice(kbV);
        // galeria: um zumbi de cada tipo na frente da câmera
        if (WaveSpawner.Instance != null && WaveSpawner.Instance.variantes != null)
        {
            var tiposZ = WaveSpawner.Instance.variantes.Where(v => v != null).Distinct().ToArray();
            var galeria = new System.Collections.Generic.List<GameObject>();
            Vector3 fr = Camera.main.transform.forward;
            fr.y = 0f;
            fr.Normalize();
            Vector3 lado = new Vector3(fr.z, 0f, -fr.x);
            for (int i = 0; i < tiposZ.Length; i++)
            {
                Vector3 p = ph.transform.position + fr * 6f + lado * ((i - (tiposZ.Length - 1) / 2f) * 1.9f);
                var g = Instantiate(tiposZ[i], p, Quaternion.LookRotation(-fr));
                g.GetComponent<NavMeshAgent>().enabled = false;
                g.GetComponent<ZombieAI>().enabled = false;
                galeria.Add(g);
            }
            yield return new WaitForSeconds(0.6f);
            Foto("02_zumbis");
            foreach (var g in galeria) Destroy(g);
        }
        Checar(!gm.Carregando && GameObject.Find("Carregando") == null, "Tela \"Carregando...\" some depois do aquecimento");
        Checar(QualitySettings.vSyncCount == (MouseJogo.SessaoRemota ? 0 : 1),
            MouseJogo.SessaoRemota ? "VSync desligado na Área de Trabalho Remota (a tela de 32 Hz prendia o jogo em 16 fps)" : "VSync ligado");
        int noPool = FindObjectsByType<ZombieAI>(FindObjectsInactive.Include).Length - ZumbisVivos();
        Checar(noPool >= 20, "Pool de zumbis criado no início", noPool + " guardados");
        imortal = true;
        ph.vidaMaxima = 1e7f; // aguenta a horda inteira batendo no mesmo quadro
        ph.Curar(1e7f);

        Secao("Movimento (teclado simulado)");
        ConfigurarInput();
        var kb = InputSystem.AddDevice<Keyboard>();
        kb.MakeCurrent();
        yield return null;
        Vector3 p0 = ph.transform.position;
        InputSystem.QueueStateEvent(kb, new KeyboardState(Key.W));
        yield return new WaitForSeconds(1.2f);
        InputSystem.QueueStateEvent(kb, new KeyboardState());
        yield return null;
        float andou = DistH(p0, ph.transform.position);
        if (andou > 1.5f) Checar(true, "Andar com W move o Marcos", andou.ToString("0.0") + " m em 1,2 s");
        else Aviso($"Teclado simulado moveu só {andou:0.0} m (pode ser parede na frente ou limitação do modo automático)");

        // andar de lado (D): corpo continua virado pra câmera, pernas giram pro lado
        var pa = ph.GetComponent<PlayerAnimator>();
        InputSystem.QueueStateEvent(kb, new KeyboardState(Key.D));
        yield return new WaitForSeconds(0.8f);
        Vector3 fCam = Camera.main.transform.forward; fCam.y = 0f;
        float desvio = Vector3.Angle(ph.transform.forward, fCam);
        float giro = pa != null ? pa.GiroPernas : 0f;
        InputSystem.QueueStateEvent(kb, new KeyboardState());
        yield return null;
        Checar(desvio < 15f, "Corpo do Marcos segue a câmera ao andar de lado", $"{desvio:0}° de diferença");
        Checar(Mathf.Abs(giro) > 40f, "Pernas giram pra direção do movimento (andar de lado)", $"{giro:0}°");
        // parado: girar a câmera (até 360°) faz o Marcos virar junto - a câmera fica sempre atrás dele
        {
            var tpcT = Camera.main.GetComponent<ThirdPersonCamera>();
            var campoYaw = typeof(ThirdPersonCamera).GetField("yaw", BindingFlags.Instance | BindingFlags.NonPublic);
            yield return new WaitForSeconds(0.3f);
            float yawCam = (float)campoYaw.GetValue(tpcT);
            float pior = 0f;
            for (int passo = 1; passo <= 4; passo++)
            {
                campoYaw.SetValue(tpcT, yawCam + passo * 90f);
                yield return new WaitForSeconds(0.5f);
                pior = Mathf.Max(pior, Mathf.Abs(Mathf.DeltaAngle(yawCam + passo * 90f, ph.transform.eulerAngles.y)));
            }
            Checar(pior < 8f, "Parado, o Marcos vira junto com a câmera (volta de 360°)", $"pior diferença {pior:0}°");
            // mouse rápido (300 px num quadro) não é cortado - só dá pra simular com a janela em foco (modo normal do mouse)
            MouseJogo.ForcarRemoto = false;
            yield return null;
            yield return null;
            if (Application.isFocused && Cursor.lockState == CursorLockMode.Locked)
            {
                var mouseR = InputSystem.AddDevice<Mouse>();
                mouseR.MakeCurrent();
                yield return new WaitForSeconds(0.1f);
                float antesR = (float)campoYaw.GetValue(tpcT);
                InputSystem.QueueDeltaStateEvent(mouseR.delta, new Vector2(300f, 0f));
                yield return null;
                yield return null;
                float girouR = Mathf.DeltaAngle(antesR, (float)campoYaw.GetValue(tpcT));
                InputSystem.RemoveDevice(mouseR);
                float esperado = 300f * tpcT.sensibilidade * Opcoes.Sensibilidade;
                Checar(girouR > esperado * 0.9f, "Movimento rápido do mouse gira a câmera inteira (sem trava)", $"{girouR:0}° de {esperado:0}° esperados");
            }
            else Info($"Mouse rápido: limite da câmera {tpcT.maxMovimentoPorQuadro:0} px/quadro (teste de mouse real pulado: janela sem foco no modo automático)");

            // Área de Trabalho Remota: o mouse chega como POSIÇÃO. O jogo lê o cursor do Windows, devolve pro meio e,
            // se a volta não pegar no PC de fora, gira a câmera com o cursor encostado na borda. Aqui o cursor é simulado.
            Info($"Sessão de Área de Trabalho Remota detectada: {(MouseJogo.SessaoRemota ? "SIM" : "não")}");
            MouseJogo.ForcarRemoto = true;
            for (int i = 0; i < 3; i++) yield return null; // o GameManager prende o cursor do jeito novo
            Checar(MouseJogo.Preso && Cursor.lockState != CursorLockMode.Locked, "Área remota: cursor não fica travado no centro (o próprio jogo devolve pro meio)", Cursor.lockState.ToString());
            var janSim = new Rect(0f, 0f, 1920f, 1080f);
            Vector2 cursorSim = janSim.center, voltaSim = Vector2.zero;
            bool pcSegue = true;
            int atrasoSim = -1; // quadros até o "PC de fora" aplicar a volta pro meio (-1 = nenhuma)
            MouseJogo.AreaJanela = () => janSim;
            MouseJogo.LerCursor = () => cursorSim;
            MouseJogo.MoverCursor = paraOnde => { if (pcSegue) { voltaSim = paraOnde; atrasoSim = 1; } };
            // 1) o PC de fora acompanha a volta pro meio, mandando 1 posição atrasada antes (como o RDP faz)
            MouseJogo.Reiniciar();
            yield return null;
            MouseJogo.Movimento(Mouse.current);
            int rec0 = MouseJogo.Recentralizacoes;
            float lidoA = 0f, maiorA = 0f;
            for (int i = 0; i < 40; i++)
            {
                if (atrasoSim == 0) { cursorSim = voltaSim; atrasoSim = -1; }
                else if (atrasoSim > 0) atrasoSim--;
                cursorSim.x += 60f;
                yield return null;
                float dx = MouseJogo.Movimento(Mouse.current).x;
                lidoA += dx;
                maiorA = Mathf.Max(maiorA, Mathf.Abs(dx));
            }
            int recA = MouseJogo.Recentralizacoes - rec0;
            Checar(recA >= 3 && Mathf.Abs(lidoA - 2400f) < 90f && maiorA < 90f,
                "Área remota: câmera gira sem parar (lê o cursor e devolve pro meio)", $"{lidoA:0} de 2400 px, {recA} voltas pro meio, maior passo {maiorA:0} px");
            // 2) o PC de fora nunca devolve o cursor: o movimento continua certo, sem pulo falso
            pcSegue = false;
            atrasoSim = -1;
            cursorSim = janSim.center;
            MouseJogo.Reiniciar();
            yield return null;
            MouseJogo.Movimento(Mouse.current);
            float lidoB = 0f, maiorB = 0f;
            for (int i = 0; i < 12; i++)
            {
                cursorSim.x -= 60f;
                yield return null;
                float dx = MouseJogo.Movimento(Mouse.current).x;
                lidoB += dx;
                maiorB = Mathf.Max(maiorB, Mathf.Abs(dx));
            }
            Checar(Mathf.Abs(lidoB + 720f) < 90f && maiorB < 90f, "Área remota: sem pulo falso se o cursor não voltar pro meio", $"{lidoB:0} de -720 px, maior passo {maiorB:0} px");
            // 3) ...e com o cursor encostado na borda esquerda (não passa dali), a câmera continua girando sozinha
            cursorSim.x = 0f;
            yield return null;
            MouseJogo.Movimento(Mouse.current);
            float giroBorda = 0f, esperadoBorda = 0f, t0b = Time.unscaledTime;
            while (Time.unscaledTime - t0b < 0.6f)
            {
                yield return null;
                giroBorda += MouseJogo.Movimento(Mouse.current).x;
                esperadoBorda -= 1200f * Time.unscaledDeltaTime;
            }
            Checar(giroBorda < -100f && Mathf.Abs(giroBorda - esperadoBorda) < Mathf.Abs(esperadoBorda) * 0.1f + 5f,
                "Área remota: cursor encostado na borda continua girando a câmera (plano B)", $"{giroBorda:0} px em {Time.unscaledTime - t0b:0.00} s (esperado {esperadoBorda:0})");
            MouseJogo.RestaurarFonte();
            MouseJogo.ForcarRemoto = null;
            yield return null;
            yield return null;
        }
        var animM = ph.GetComponentInChildren<Animator>();
        Checar(animM != null && animM.runtimeAnimatorController != null, "Animator do Marcos ativo", animM != null && animM.runtimeAnimatorController != null ? animM.runtimeAnimatorController.name : "");
        InputSystem.RemoveDevice(kb);

        Secao("Zumbis");
        float t0 = Time.time;
        while (ZumbisVivos() == 0 && Time.time - t0 < 15f) yield return null;
        Checar(ZumbisVivos() > 0, "Zumbis nascem no Dia 1", ZumbisVivos() + " vivos após " + (Time.time - t0).ToString("0.0") + " s");

        WaveSpawner.Instance.Configurar(null);
        LimparZumbis();
        yield return null;
        var zumbi = CriarZumbi(ph.transform.position + ph.transform.right * 8f);
        Checar(zumbi != null, "Zumbi criado a 8 m do jogador");
        if (zumbi != null)
        {
            yield return new WaitForSeconds(0.8f);
            if (zumbi.animator != null)
            {
                float spd = zumbi.animator.GetFloat("Speed");
                Checar(spd > 0.2f, "Zumbi anda com animação de verdade (Animator)", $"{zumbi.tipo}, Speed {spd:0.00}");
            }
            else
            {
                // animação procedural: a coxa tem que mexer enquanto ele anda
                var coxa = zumbi.GetComponentsInChildren<Transform>().FirstOrDefault(t => System.Text.RegularExpressions.Regex.IsMatch(t.name, @"^bip L Thigh\d*$"));
                float maiorGiro = 0f;
                if (coxa != null)
                {
                    Quaternion rotIni = coxa.localRotation;
                    for (int i = 0; i < 10; i++)
                    {
                        yield return new WaitForSeconds(0.05f);
                        maiorGiro = Mathf.Max(maiorGiro, Quaternion.Angle(rotIni, coxa.localRotation));
                    }
                }
                Checar(coxa != null && maiorGiro > 8f, "Zumbi mexe as pernas ao andar (animação procedural)", $"coxa girou até {maiorGiro:0}°");
            }
            float peZ = PeOssos(zumbi.gameObject);
            float chaoZ = ChaoAbaixo(zumbi.transform.position, zumbi.transform);
            Checar(Mathf.Abs(peZ - chaoZ) < 0.15f, "Zumbi pisa no chão (não flutua)", $"pé {peZ - chaoZ:+0.00;-0.00} m do chão");

            float d0 = Vector3.Distance(zumbi.transform.position, ph.transform.position);
            float danoAntes = ph.UltimoDano;
            t0 = Time.time;
            while (ph.UltimoDano == danoAntes && Time.time - t0 < 8f) yield return null;
            float d1 = zumbi != null ? Vector3.Distance(zumbi.transform.position, ph.transform.position) : 0f;
            Checar(d1 < d0, "Zumbi persegue o jogador", $"{d0:0.0} m -> {d1:0.0} m");
            Checar(ph.UltimoDano != danoAntes, "Zumbi ataca e causa dano", (Time.time - t0).ToString("0.0") + " s pra alcançar");
            Destroy(zumbi.gameObject);
        }
        yield return null;

        Secao("Marcos no chão");
        yield return new WaitForSeconds(0.3f);
        if (ph.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0)
        {
            float peM = PeOssos(ph.gameObject);
            float chaoM = ChaoAbaixo(ph.transform.position, ph.transform);
            Checar(Mathf.Abs(peM - chaoM) < 0.1f, "Pé do Marcos encosta no chão (não flutua)", $"pé {peM - chaoM:+0.00;-0.00} m do chão");
        }
        else Aviso("Marcos sem modelo (cápsula)");

        Secao("Armas");
        var cam = pw.cam;
        // progressão: começa só com a pistola; as outras são achadas no mapa
        Checar(pw.Liberada(0) && !pw.Liberada(1) && !pw.Liberada(2) && !pw.Liberada(3), "Começa só com a pistola");
        Checar(GameObject.Find("Arma_" + pw.armas[1].nome) != null, "Escopeta esperando na escola (com feixe de luz)");
        AmmoPickup.Criar(pw.armas[2], 0, ph.transform.position, 0f, true);
        yield return new WaitForSeconds(0.3f);
        Checar(pw.Liberada(2) && pw.IndiceAtual == 2, "Pegar arma nova libera e já equipa", pw.ArmaAtual.nome);
        pw.LiberarTodas();
        Chamar(pw, "Equipar", 0);
        // vira a câmera pra uma direção sem objetos na frente (o cenário tem muita coisa na rua)
        yield return MirarLivre(16f);
        Info($"Linha de tiro livre: {DistanciaLivre():0} m");
        var alvo = CriarZumbi(ph.transform.position + ph.transform.forward * 5f);
        if (alvo != null)
        {
            alvo.GetComponent<NavMeshAgent>().enabled = false;
            alvo.enabled = false;
            alvo.transform.position = cam.transform.position + cam.transform.forward * 8f - Vector3.up * 0.9f;
            Physics.SyncTransforms();
            var vidaZ = alvo.GetComponent<ZombieHealth>();
            float v0 = vidaZ.Vida;
            float tAcerto = Time.time;
            // anota antes de atirar: se os tiros pegarem na cabeça, o zumbi já morre aqui
            int abates0 = gm.Abates;
            for (int i = 0; i < 3; i++) Chamar(pw, "Atirar", false);
            Checar(vidaZ.Vida < v0, "Pistola acerta zumbi na mira (raycast do centro da câmera)", $"vida {v0:0} -> {vidaZ.Vida:0}");
            Checar(Efeitos.ParticulasVivas > 0, "Sangue sai ao acertar", Efeitos.ParticulasVivas + " partículas");
            Checar(HUD.UltimoAcerto >= tAcerto, "Marcador de acerto na mira");
            Checar(alvo.Atordoado, "Zumbi cambaleia ao levar tiro");
            vidaZ.ReceberDano(9999f);
            Checar(alvo.EstaMorto && gm.Abates == abates0 + 1, "Zumbi morre e conta abate");
        }
        else Aviso("Não consegui posicionar zumbi pra testar o tiro");

        // tiro na cabeça (o zumbi anterior já morreu e não bloqueia mais o tiro)
        var alvoCab = CriarZumbi(ph.transform.position + ph.transform.forward * 5f);
        if (alvoCab != null)
        {
            alvoCab.GetComponent<NavMeshAgent>().enabled = false;
            alvoCab.enabled = false;
            alvoCab.transform.position = cam.transform.position + cam.transform.forward * 9f - Vector3.up * (alvoCab.alturaCabeca + 0.2f);
            Physics.SyncTransforms();
            var vCab = alvoCab.GetComponent<ZombieHealth>();
            float antesCab = vCab.Vida;
            var noRaio = Physics.RaycastAll(cam.transform.position, cam.transform.forward, 12f, ~0, QueryTriggerInteraction.Ignore)
                .OrderBy(h => h.distance).Select(h => $"{h.collider.name}@{h.distance:0.0}");
            int noRaio60 = Physics.RaycastAll(cam.transform.position, cam.transform.forward, 60f, ~0, QueryTriggerInteraction.Ignore).Length;
            Info($"Raio da mira (tiro na cabeça): {string.Join(", ", noRaio)}  ·  {noRaio60} colisores em 60 m");
            Chamar(pw, "Atirar", true);
            float perdeu = antesCab - vCab.Vida;
            {
                var fl = BindingFlags.Instance | BindingFlags.NonPublic;
                var trs = (LineRenderer[])typeof(PlayerWeapons).GetField("tracers", fl).GetValue(pw);
                int prox = (int)typeof(PlayerWeapons).GetField("proximoTracer", fl).GetValue(pw);
                var lr = trs[(prox + trs.Length - 1) % trs.Length];
                Vector3 fimT = lr.GetPosition(1);
                Info($"Tiro: arma {pw.ArmaAtual.nome} [{pw.IndiceAtual}], fim do rastro a {Vector3.Distance(cam.transform.position, fimT):0.0} m, " +
                     $"desvio da mira {Vector3.Angle(cam.transform.forward, fimT - cam.transform.position):0.0}°, zumbi em {alvoCab.transform.position}, fim {fimT}, colisor ativo {alvoCab.GetComponent<Collider>().enabled}");
            }
            Checar(perdeu >= pw.armas[0].dano * 2f, "Tiro na cabeça dá dano dobrado", $"{perdeu:0} de dano (normal {pw.armas[0].dano:0})");
            Destroy(alvoCab.gameObject);
        }

        Chamar(pw, "Equipar", 1);
        Checar(pw.IndiceAtual == 1 && pw.ArmaAtual.nome == "Escopeta", "Troca para escopeta (tecla 2)");
        int pente0 = pw.PenteAtual, res0 = pw.ReservaAtual;
        typeof(PlayerWeapons).GetField("proximoTiro", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pw, 0f);
        Chamar(pw, "Atirar", false);
        Checar(pw.PenteAtual == pente0 - 1, "Escopeta gasta 1 cartucho por tiro (8 projéteis)", $"{pente0} -> {pw.PenteAtual}");
        Chamar(pw, "TentarRecarregar");
        Checar(pw.Recarregando, "Recarga começa (R)");
        yield return new WaitForSeconds(pw.ArmaAtual.tempoRecarga + 0.3f);
        Checar(!pw.Recarregando && pw.PenteAtual == pw.ArmaAtual.tamanhoPente && pw.ReservaAtual == res0 - 1, "Recarga completa e desconta da reserva", $"pente {pw.PenteAtual}, reserva {res0} -> {pw.ReservaAtual}");

        Chamar(pw, "Equipar", 2);
        Checar(pw.IndiceAtual == 2 && pw.ArmaAtual.automatica, "Troca para rifle automático (tecla 3)");
        int r0 = pw.ReservaAtual;
        AmmoPickup.Criar(pw.ArmaAtual, 10, ph.transform.position, 0f);
        yield return new WaitForSeconds(0.3f);
        Checar(pw.ReservaAtual == Mathf.Min(r0 + 10, pw.ArmaAtual.municaoMaxima), "Pegar caixa de munição soma na reserva", $"{r0} -> {pw.ReservaAtual}");
        // sniper: zoom + bala atravessa 2 zumbis em fila
        Chamar(pw, "Equipar", 3);
        Checar(pw.IndiceAtual == 3 && pw.ArmaAtual.fovMira > 0f, "Troca para sniper (tecla 4) com zoom", pw.ArmaAtual.nome);
        var fila = new System.Collections.Generic.List<ZombieAI>();
        foreach (float d in new[] { 8f, 11f })
        {
            var z = CriarZumbi(ph.transform.position + ph.transform.forward * 5f);
            if (z == null) continue;
            z.GetComponent<NavMeshAgent>().enabled = false;
            z.enabled = false;
            z.transform.position = cam.transform.position + cam.transform.forward * d - Vector3.up * 0.9f;
            fila.Add(z);
        }
        Physics.SyncTransforms();
        Chamar(pw, "Atirar", true);
        Checar(fila.Count == 2 && fila.All(z => z.EstaMorto), "Bala da sniper atravessa e mata 2 zumbis em fila", fila.Count(z => z.EstaMorto) + "/2 mortos");

        // zumbi rápido
        var rapido = CriarZumbi(ph.transform.position + ph.transform.right * 10f);
        if (rapido != null)
        {
            rapido.DefinirRapido(WaveSpawner.Instance.materialRapido);
            yield return new WaitForSeconds(0.5f);
            float vel = rapido.GetComponent<NavMeshAgent>().speed;
            Checar(rapido.Rapido && vel > rapido.velocidadePerseguir * 1.5f, "Zumbi rápido corre mais", $"{vel:0.0} m/s (normal {rapido.velocidadePerseguir:0.0})");
            Destroy(rapido.gameObject);
        }

        Chamar(pw, "Equipar", 0);
        Checar(pw.ArmaAtual.municaoInfinita, "Pistola tem munição infinita (nunca fica sem arma)");

        Secao("Horda (não pode prender o jogador)");
        LimparZumbis();
        yield return null;
        Vector3 centro = ph.transform.position;
        int criados = 0;
        for (int i = 0; i < 16; i++)
        {
            Vector3 p = centro + Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward * 1.3f;
            if (CriarZumbi(p) != null) criados++;
        }
        yield return new WaitForSeconds(1.5f);
        var cc = ph.GetComponent<CharacterController>();
        // direção livre de prédios (só os zumbis no caminho)
        Vector3 fuga = Vector3.forward;
        for (int i = 0; i < 8; i++)
        {
            Vector3 d = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
            if (!NavMesh.Raycast(ph.transform.position, ph.transform.position + d * 6f, out _, NavMesh.AllAreas)) { fuga = d; break; }
        }
        Vector3 antes = ph.transform.position;
        float somaDt = 0f, piorDt = 0f;
        int frames = 0;
        t0 = Time.time;
        while (Time.time - t0 < 2.5f)
        {
            cc.Move(fuga * 4f * Time.deltaTime);
            somaDt += Time.unscaledDeltaTime;
            piorDt = Mathf.Max(piorDt, Time.unscaledDeltaTime);
            frames++;
            yield return null;
        }
        float escapou = DistH(antes, ph.transform.position);
        Foto("03_horda"); // depois da medição (salvar a foto demora e estragaria o número)
        Checar(escapou > 2f, $"Jogador consegue sair do meio de {criados} zumbis empurrando", escapou.ToString("0.0") + " m em 2,5 s");
        Info($"Desempenho com horda (modo automático, só referência): média {1000f * somaDt / Mathf.Max(1, frames):0.0} ms/quadro, pior {1000f * piorDt:0.0} ms");
        LimparZumbis();
        yield return null;

        int totalAntes = FindObjectsByType<ZombieAI>(FindObjectsInactive.Include).Length;
        if (EscolaInterior.Instance != null)
        {
            yield return TesteEscolaPorDentro(gm, ph);
            goto Fases;
        }
        Secao("Escola: desenho da Lia (v0.3)");
        Teleportar(gm.escola.position);
        yield return new WaitForSeconds(0.5f);
        Checar(gm.FaseAtual == Fase.Dia1 && ObjectiveManager.Instance.Texto.Contains("desenho"), "Chegar na escola pede pra pegar o desenho", ObjectiveManager.Instance.Texto);
        Teleportar(gm.desenho.transform.position);
        OlharPara(gm.desenho.transform.position + Vector3.up, 35f);
        yield return new WaitForSeconds(0.3f);
        Foto("04_desenho_no_chao");
        Vector3 dPos = ph.transform.position - gm.desenho.transform.position;
        Checar(Mathf.Abs(dPos.y) < 0.3f && DistH(ph.transform.position, gm.desenho.transform.position) < 1f, "Marcos fica em pé no lugar do desenho (sem obstáculo)", $"diferença {dPos.x:0.0}/{dPos.y:0.0}/{dPos.z:0.0} m");
        var txtDica = GameObject.Find("Dica")?.GetComponent<UnityEngine.UI.Text>();
        Checar(txtDica != null && txtDica.enabled && txtDica.text.Contains("[E]"), "Perto do desenho aparece \"[E] Pegar o desenho\"", txtDica != null ? txtDica.text : "");
        var kbE = InputSystem.AddDevice<Keyboard>();
        kbE.MakeCurrent();
        InputSystem.QueueStateEvent(kbE, new KeyboardState(Key.E));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(kbE, new KeyboardState());
        yield return null;
        var painelDesenho = GameObject.Find("Desenho");
        Checar(gm.LendoDesenho && Time.timeScale == 0f && painelDesenho != null && painelDesenho.activeInHierarchy, "Tecla E pega o desenho e mostra na tela (jogo congela)");
        FotoComHUD("04a_desenho_tela");
        InputSystem.QueueStateEvent(kbE, new KeyboardState(Key.E));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(kbE, new KeyboardState());
        yield return null;
        InputSystem.RemoveDevice(kbE);
        Checar(!gm.LendoDesenho && Time.timeScale == 1f, "E de novo guarda o desenho e o jogo volta");

        Fases:
        Secao("Fases");
        // o sol se põe enquanto a Base Ômega fala, a tela escurece e começa a noite
        var dnA = DayNightController.Instance;
        float maxEntardecer = 0f, maxEscuro = 0f;
        bool fotoTarde = false;
        t0 = Time.time;
        while ((gm.FaseAtual != Fase.Noite1 || gm.EmTransicao) && Time.time - t0 < 40f)
        {
            if (dnA != null) maxEntardecer = Mathf.Max(maxEntardecer, dnA.Entardecer);
            maxEscuro = Mathf.Max(maxEscuro, gm.Escuro);
            if (!fotoTarde && dnA != null && dnA.Entardecer > 0.8f && gm.Escuro < 0.05f)
            {
                fotoTarde = true;
                OlharPara(gm.escola.position + (gm.clinica.position - gm.escola.position).normalized * 40f, 4f);
                yield return null;
                Foto("04c_anoitecer");
            }
            yield return null;
        }
        Checar(maxEntardecer > 0.9f, "Depois do desenho o céu fica laranja (pôr do sol) antes da noite", $"entardecer {maxEntardecer:0.00}");
        Checar(maxEscuro > 0.98f, "Troca de fase com a tela escurecendo (sem pulo de luz)", $"escuro máx {maxEscuro:0.00}");
        Checar(gm.FaseAtual == Fase.Noite1, "Ler o desenho na escola -> Noite 1", $"{Time.time - t0:0.0} s");
        var tutN = GameObject.Find("Tutorial")?.GetComponent<UnityEngine.UI.Text>();
        yield return new WaitForSeconds(0.3f);
        Checar(tutN == null || !tutN.enabled || !tutN.text.Contains("PAPEL"), "Dica velha (pegar o papel) não aparece na noite", tutN != null && tutN.enabled ? tutN.text : "");
        Checar(SomManager.Instance.MusicaAtual == SomManager.Instance.musicaNoite, "Música de tensão na noite");
        var dn = DayNightController.Instance;
        Checar(dn != null && dn.sol != null && dn.sol.intensity < 0.5f, "Luz muda pra noite");
        var lanterna = GameObject.Find("Lanterna");
        Checar(lanterna != null && lanterna.GetComponent<Light>().enabled, "Lanterna acende à noite");
        Checar(gm.farolEscola == null || !gm.farolEscola.activeSelf, "Farol da escola apaga");
        int acesas = FindObjectsByType<LuzNoturna>().Count(l => l.GetComponent<Light>().enabled);
        Checar(acesas >= 8, "Postes e fogueiras acesos à noite", acesas + " luzes");
        t0 = Time.time;
        while (ZumbisVivos() == 0 && Time.time - t0 < 8f) yield return null;
        Checar(ZumbisVivos() > 0, "Horda noturna nasce em volta da escola", ZumbisVivos() + " vivos");
        yield return new WaitForSeconds(4f);
        int totalNoite = FindObjectsByType<ZombieAI>(FindObjectsInactive.Include).Length;
        Checar(totalNoite == totalAntes, "Horda reaproveita o pool (sem criar zumbi novo)", $"{totalAntes} -> {totalNoite} objetos, {ZumbisVivos()} vivos");
        Foto("04_noite_escola");

        DefinirTempo(gm, 0.3f);
        t0 = Time.time;
        yield return new WaitForSeconds(0.4f);
        while ((gm.FaseAtual != Fase.Dia2 || gm.EmTransicao) && Time.time - t0 < 8f) yield return null;
        Checar(gm.FaseAtual == Fase.Dia2, "Timer da noite acaba -> Dia 2", $"{Time.time - t0:0.0} s com a transição");
        Checar(ZumbisVivos() == 0, "Zumbis da noite somem ao amanhecer");
        Checar(ObjectiveManager.Instance.ModoSinal, "Dia 2 usa o medidor de sinal do rádio");
        yield return new WaitForSeconds(1.6f); // espera a tela clarear pra foto
        var painelSinal = GameObject.Find("SinalFundo");
        Checar(painelSinal != null && painelSinal.activeInHierarchy, "Painel grande do SINAL DO RÁDIO aparece no topo da tela (Dia 2)");
        FotoComHUD("05a_sinal_radio");
        int postesAcesos = FindObjectsByType<LuzNoturna>().Count(l => l.intensidadeDia <= 0f && l.GetComponent<Light>().enabled);
        Checar(postesAcesos == 0, "Postes apagam de dia", postesAcesos + " acesos");
        Checar(gm.farolClinica != null && !gm.farolClinica.activeSelf, "Farol da clínica escondido quando longe");

        // chegando perto da clínica o sol vai se pondo
        Vector3 perto = gm.clinica.position + (gm.escola.position - gm.clinica.position).normalized * 30f;
        if (NavMesh.SamplePosition(perto, out var hitPerto, 8f, NavMesh.AllAreas)) Teleportar(hitPerto.position);
        Color solAntes = dn.sol.color;
        yield return new WaitForSeconds(2f);
        Checar(dn.Entardecer > 0.1f && dn.sol.color.b < solAntes.b, "Pôr do sol chegando perto da Lia (Dia 2)", $"entardecer {dn.Entardecer:0.00}");
        OlharPara(gm.clinica.position, 4f);
        yield return null;
        yield return null;
        Foto("05_entardecer");

        if (ClinicaInterior.Instance != null) yield return TesteClinicaPorDentro(gm, ph);
        else Teleportar(gm.clinica.position);
        t0 = Time.time;
        while (!gm.EmCena && Time.time - t0 < 3f) yield return null;
        Checar(gm.EmCena && !gm.JogoAtivo, "Achou a Lia -> cena do reencontro (barras pretas, sem controle)");
        yield return new WaitForSeconds(2.6f);
        Checar(RadioSystem.Instance != null && RadioSystem.Instance.Mostrando && !RadioSystem.Instance.PeloRadio, "Reencontro: pai e filha conversam ao vivo", RadioSystem.Instance != null ? RadioSystem.Instance.Quem + ": " + RadioSystem.Instance.Texto : "");
        FotoComHUD("06a_reencontro");
        gm.PularCena();
        t0 = Time.time;
        while ((gm.FaseAtual != Fase.NoiteFinal || !gm.JogoAtivo) && Time.time - t0 < 10f) yield return null;
        Checar(gm.FaseAtual == Fase.NoiteFinal && gm.JogoAtivo, "Depois do reencontro -> Noite final", $"{Time.time - t0:0.0} s");
        Checar(gm.lia != null && gm.lia.activeSelf, "Lia aparece na clínica");
        if (ClinicaInterior.Instance != null)
            Checar(Interior.Atual == null && gm.lia != null && DistH(gm.lia.transform.position, ph.transform.position) < 3f && DistH(ph.transform.position, gm.clinica.position) < 30f,
                "Depois do reencontro os dois saem juntos pra rua (noite final lá fora)", gm.lia != null ? $"Lia a {DistH(gm.lia.transform.position, ph.transform.position):0.0} m" : "");
        if (gm.lia != null)
        {
            OlharPara(gm.lia.transform.position, 12f);
            yield return new WaitForSeconds(1.2f);
            Foto("06_lia_clinica");

            // Lia segue o pai e leva dano (horda desligada pra ela não morrer no meio do teste)
            WaveSpawner.Instance.Configurar(null);
            LimparZumbis();
            var liaN = gm.lia.GetComponent<LiaNPC>();
            t0 = Time.time;
            while (!liaN.Seguindo && Time.time - t0 < 5f) yield return null;
            Checar(liaN.Seguindo, "Depois de acenar, a Lia passa a seguir o pai");
            Vector3 livre = Vector3.forward;
            for (int i = 0; i < 8; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                if (!NavMesh.Raycast(ph.transform.position, ph.transform.position + d * 10f, out _, NavMesh.AllAreas)) { livre = d; break; }
            }
            if (NavMesh.SamplePosition(ph.transform.position + livre * 10f, out var hl, 2f, NavMesh.AllAreas)) Teleportar(hl.position);
            float dl0 = DistH(liaN.transform.position, ph.transform.position);
            var caminhoL = new NavMeshPath();
            float compL = -1f;
            if (NavMesh.CalculatePath(liaN.transform.position, ph.transform.position, NavMesh.AllAreas, caminhoL) && caminhoL.corners.Length > 1)
            {
                compL = 0f;
                for (int ic = 1; ic < caminhoL.corners.Length; ic++) compL += Vector3.Distance(caminhoL.corners[ic - 1], caminhoL.corners[ic]);
            }
            t0 = Time.time;
            var agL = liaN.GetComponent<NavMeshAgent>();
            var sbL = new StringBuilder();
            float proxAmostra = 0f;
            while (DistH(liaN.transform.position, ph.transform.position) > 4.5f && Time.time - t0 < 7f)
            {
                if (agL != null && Time.time - t0 >= proxAmostra)
                {
                    proxAmostra += 0.5f;
                    sbL.Append($" | {Time.time - t0:0.0}s d={DistH(liaN.transform.position, ph.transform.position):0.0} v={agL.velocity.magnitude:0.0} spd={agL.speed:0.0} acel={agL.acceleration:0} pend={agL.pathPending} path={agL.hasPath} stop={agL.isStopped} rem={agL.remainingDistance:0.0} {agL.pathStatus} ativo={gm.JogoAtivo}");
                }
                yield return null;
            }
            Info("Lia seguindo (amostras):" + sbL);
            float dl1 = DistH(liaN.transform.position, ph.transform.position);
            Checar(dl1 < dl0 - 3f && dl1 < 5f, "Lia segue o pai pelo NavMesh", $"{dl0:0.0} m -> {dl1:0.0} m em {Time.time - t0:0.0} s (caminho {compL:0.0} m)");
            var barraLia = GameObject.Find("LiaBarra");
            Checar(barraLia != null && barraLia.GetComponent<UnityEngine.UI.Image>().enabled, "Vida da Lia aparece no HUD");
            OlharPara(liaN.transform.position, 10f);
            yield return null;
            yield return null;
            Foto("06b_lia_seguindo");
            Vector3 ladoL = liaN.transform.position - ph.transform.position;
            ladoL.y = 0f;
            var zl = CriarZumbi(liaN.transform.position + (ladoL.sqrMagnitude > 0.01f ? ladoL.normalized : Vector3.forward) * 1.2f);
            if (zl != null)
            {
                zl.GetComponent<ZombieHealth>().DefinirMultiplicador(30f);
                float dL0 = liaN.UltimoDano;
                t0 = Time.time;
                while (liaN.UltimoDano == dL0 && Time.time - t0 < 6f) yield return null;
                Checar(liaN.UltimoDano != dL0 && liaN.Vida < liaN.vidaMaxima, "Zumbis também atacam a Lia", $"vida {liaN.Vida:0}/{liaN.vidaMaxima:0}");
                Destroy(zl.gameObject);
            }
        }

        Checar(SomManager.Instance.MusicaAtual == SomManager.Instance.musicaFinal, "Música da noite final");
        DefinirTempo(gm, gm.avisoHelicoptero - 0.5f);
        yield return new WaitForSeconds(0.3f);
        var heli = gm.Heli;
        Checar(heli != null, "Helicóptero aparece no fim da noite final");
        if (heli != null)
        {
            heli.PularVoo();
            yield return null;
            yield return null;
            Checar(DistH(heli.transform.position, gm.clinica.position) < 3f && heli.transform.position.y > gm.clinica.position.y + 8f, "Helicóptero chega e fica pairando em cima da clínica");
            var modeloH = heli.transform.Find("Modelo");
            var heliceH = modeloH != null ? modeloH.Find("Helice") : null;
            Checar(modeloH != null && heliceH != null, "Helicóptero usa o modelo 3D (com hélice separada)");
            bool rosa = heli.GetComponentsInChildren<Renderer>().Any(r => r.sharedMaterial == null || r.sharedMaterial.shader == null || !r.sharedMaterial.shader.isSupported || r.sharedMaterial.shader.name.Contains("Error"));
            Checar(!rosa, "Helicóptero sem material faltando (não fica rosa)");
            if (heliceH != null)
            {
                Quaternion h0 = heliceH.localRotation;
                yield return null;
                yield return null;
                Checar(Quaternion.Angle(h0, heliceH.localRotation) > 1f, "Hélice do helicóptero gira");
            }
            OlharPara(heli.transform.position, -30f);
            yield return new WaitForSeconds(0.2f);
            Foto("07_helicoptero");
        }

        DefinirTempo(gm, 0.3f);
        t0 = Time.time;
        while (!gm.EmCena && Time.time - t0 < 3f) yield return null;
        Checar(gm.EmCena, "Timer acaba -> cena final (helicóptero descendo, falas)");
        yield return new WaitForSeconds(3.2f);
        Checar(gm.Heli != null && gm.Heli.transform.position.y < gm.clinica.position.y + 15f, "Helicóptero desce pra buscar os dois", gm.Heli != null ? $"{gm.Heli.transform.position.y - gm.clinica.position.y:0.0} m acima da clínica" : "");
        FotoComHUD("07b_cena_final");
        {
            // diagnóstico da cena final: posições + foto de cima
            var camT = Camera.main.transform;
            Info($"Cena final: clínica {gm.clinica.position}, Marcos {ph.transform.position} olhando {ph.transform.forward}, " +
                 $"Lia {(gm.lia != null ? gm.lia.transform.position.ToString() : "-")}, heli {(gm.Heli != null ? gm.Heli.transform.position.ToString() : "-")}, " +
                 $"câmera {camT.position} olhando {camT.forward}");
            foreach (var h in Physics.RaycastAll(camT.position, camT.forward, 40f, ~0, QueryTriggerInteraction.Ignore).OrderBy(x => x.distance).Take(4))
                Info($"  na frente da câmera: {h.collider.name} a {h.distance:0.0} m");
            var cima = new GameObject("CamCima").AddComponent<Camera>();
            cima.transform.position = gm.clinica.position + Vector3.up * 45f;
            cima.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cima.fieldOfView = 60f;
            cima.farClipPlane = 120f;
            var principal = Camera.main;
            principal.enabled = false;
            cima.tag = "MainCamera";
            yield return null;
            Foto("07c_cena_final_cima");
            cima.tag = "Untagged";
            Destroy(cima.gameObject);
            principal.enabled = true;
        }
        gm.PularCena();
        t0 = Time.time;
        while (gm.FaseAtual != Fase.Vitoria && Time.time - t0 < 8f) yield return null;
        yield return null;
        Checar(gm.FaseAtual == Fase.Vitoria, "Cena final -> Vitória", $"{Time.time - t0:0.0} s");
        Checar(!gm.JogoAtivo, "Jogo para na vitória");
        var fim = GameObject.Find("Fim");
        Checar(fim != null && fim.activeInHierarchy && GameObject.Find("BotoesVitoria") != null, "Tela de vitória aparece (Jogar de novo / Créditos / Menu)");
        FotoComHUD("08_vitoria");

        Secao("Recomeçar / pausa / game over");
        imortal = false;
        gm.Recomecar();
        yield return new WaitForSeconds(1.5f);
        gm = GameManager.Instance;
        ph = PlayerHealth.Instance;
        Checar(gm != null && gm.FaseAtual == Fase.Dia1 && Time.timeScale == 1f, "Recomeçar volta pro Dia 1");
        Checar(ph != null && ph.Vida == ph.vidaMaxima, "Vida cheia ao recomeçar");

        gm.AlternarPausa();
        yield return new WaitForSecondsRealtime(0.2f);
        var pausa = GameObject.Find("Pausa");
        Checar(Time.timeScale == 0f && !gm.JogoAtivo && pausa != null && pausa.activeInHierarchy, "Pausa congela o jogo e mostra o menu");
        string q0 = QualitySettings.names[QualitySettings.GetQualityLevel()];
        string rp0 = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline.name : "-";
        gm.AlternarGraficos();
        yield return null;
        string q1 = QualitySettings.names[QualitySettings.GetQualityLevel()];
        string rp1 = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline.name : "-";
        bool leve = gm.GraficosLeves;
        gm.AlternarGraficos();
        yield return null;
        string q2 = QualitySettings.names[QualitySettings.GetQualityLevel()];
        Checar(q1 != q0 && q2 == q0 && QualitySettings.vSyncCount == (MouseJogo.SessaoRemota ? 0 : 1), "Botão Gráficos alterna Alto/Leve",
            $"{q0} -> {q1} -> {q2}; pipeline {rp0} -> {rp1}; leve={leve}; níveis: {string.Join("/", QualitySettings.names)}");
        gm.AlternarPausa();
        Checar(Time.timeScale == 1f && gm.JogoAtivo, "Despausar volta ao jogo");

        Secao("Fome, comida e kit médico (v0.3)");
        WaveSpawner.Instance.Configurar(null);
        LimparZumbis();
        var fome = PlayerHunger.Instance;
        float f0 = fome.Fome;
        yield return new WaitForSeconds(1f);
        Checar(fome.Fome < f0, "Fome cai com o tempo", $"{f0:0.0} -> {fome.Fome:0.0}");
        fome.Definir(0f);
        float vF = ph.Vida;
        yield return new WaitForSeconds(1f);
        Checar(ph.Vida < vF, "Fome zerada drena a vida", $"{vF:0.0} -> {ph.Vida:0.0}");
        ItemPickup.Criar(TipoItem.Comida, ph.transform.position);
        yield return new WaitForSeconds(0.3f);
        Checar(fome.Fome >= 40f, "Comer lata enche a fome", $"fome {fome.Fome:0}");
        int kits0 = ph.Kits;
        ItemPickup.Criar(TipoItem.KitMedico, ph.transform.position);
        yield return new WaitForSeconds(0.3f);
        Checar(ph.Kits == kits0 + 1, "Pegar kit médico guarda no inventário", ph.Kits + " kit(s)");
        ph.ReceberDano(60f);
        float vK = ph.Vida;
        bool usou = ph.UsarKit();
        Checar(usou && ph.Kits == kits0 && ph.Vida >= vK + ph.curaKit - 0.5f, "Kit médico (Q) cura", $"{vK:0} -> {ph.Vida:0}");

        ph.ReceberDano(1000f);
        yield return new WaitForSeconds(0.3f);
        Checar(gm.FaseAtual == Fase.GameOver, "Vida zerada -> Game Over", gm.MotivoFim);
        Checar(GameObject.Find("Fim") == null, "Game Over: a tela escurece antes do painel (dá pra ver o Marcos cair)");
        yield return new WaitForSeconds(gm.atrasoPainelFim + 0.3f);
        fim = GameObject.Find("Fim");
        Checar(fim != null && fim.activeInHierarchy, "Tela de Game Over aparece");
        Checar(!gm.TemCheckpoint && GameObject.Find("Botao_Tentar de novo") == null, "Morreu no Dia 1: sem checkpoint, só \"Recomeçar\"");

        Secao("Derrota se a Lia morrer + checkpoint (v0.3 / v0.4)");
        gm.Recomecar();
        yield return new WaitForSeconds(1.5f);
        gm = GameManager.Instance;
        Chamar(gm, "IniciarFase", Fase.Dia2);
        Chamar(gm, "AchouLia", false); // sem a cena do reencontro (já testada)
        var liaM = LiaNPC.Instance;
        t0 = Time.time;
        while ((liaM == null || !liaM.Seguindo) && Time.time - t0 < 5f)
        {
            yield return null;
            liaM = LiaNPC.Instance;
        }
        Checar(liaM != null && liaM.Seguindo && gm.FaseAtual == Fase.NoiteFinal, "(preparação) noite final com a Lia seguindo");
        if (liaM != null) liaM.ReceberDano(99999f);
        yield return new WaitForSeconds(gm.atrasoPainelFim + 0.4f);
        Checar(gm.FaseAtual == Fase.GameOver && gm.MotivoFim != null && gm.MotivoFim.Contains("Lia"), "Lia morre -> Game Over", gm.MotivoFim);
        var botaoTentar = GameObject.Find("Botao_Tentar de novo");
        string textoTentar = botaoTentar != null ? botaoTentar.GetComponentInChildren<UnityEngine.UI.Text>().text : "";
        Checar(gm.TemCheckpoint && botaoTentar != null && textoTentar.Contains("Noite final"), "Game Over oferece \"Tentar de novo\" do começo da fase", textoTentar);
        FotoComHUD("09_game_over");
        gm.TentarDeNovo();
        yield return new WaitForSeconds(1.5f);
        gm = GameManager.Instance;
        Checar(gm != null && !gm.NoMenu && gm.FaseAtual == Fase.NoiteFinal, "Tentar de novo volta direto pra noite final (sem refazer o Dia 1)", gm != null ? gm.FaseAtual.ToString() : "");
        Checar(gm != null && DistH(PlayerHealth.Instance.transform.position, gm.clinica.position) < 3f && gm.lia != null && gm.lia.activeSelf, "Checkpoint: Marcos na clínica com a Lia");
        Checar(PlayerHealth.Instance.Vida == PlayerHealth.Instance.vidaMaxima, "Checkpoint: vida cheia");

        if (EscolaInterior.Instance != null)
        {
            Secao("Checkpoint da escola (v1.4)");
            gm.Recomecar();
            yield return new WaitForSeconds(1.5f);
            gm = GameManager.Instance;
            var escC = EscolaInterior.Instance;
            TeleportarNoChao(escC.interior.portaFora.Posicao);
            yield return null;
            gm.EntrarInterior(escC.interior);
            float tc = Time.time;
            while ((Interior.Atual == null || gm.EmTransicao) && Time.time - tc < 6f) yield return null;
            Checar(Interior.Atual == escC.interior && gm.TemCheckpoint, "(preparação) dentro da escola, com checkpoint");
            PlayerHealth.Instance.ReceberDano(99999f);
            yield return new WaitForSeconds(gm.atrasoPainelFim + 0.4f);
            var botaoE = GameObject.Find("Botao_Tentar de novo");
            string txtE = botaoE != null ? botaoE.GetComponentInChildren<UnityEngine.UI.Text>().text : "";
            Checar(gm.FaseAtual == Fase.GameOver && txtE.Contains("escola"), "Morreu dentro da escola: \"Tentar de novo\" é da escola", txtE);
            gm.TentarDeNovo();
            yield return new WaitForSeconds(1.5f);
            gm = GameManager.Instance;
            escC = EscolaInterior.Instance;
            Checar(gm.FaseAtual == Fase.Dia1 && Interior.Atual == null && DistH(PlayerHealth.Instance.transform.position, escC.interior.saida.position) < 2f
                   && ObjectiveManager.Instance.Texto.Contains("Entre na escola"), "Checkpoint da escola: volta na porta, pronto pra entrar de novo", ObjectiveManager.Instance.Texto);
        }
        gm.VoltarAoMenu();
        yield return new WaitForSeconds(1.5f);
        gm = GameManager.Instance;
        Checar(gm != null && gm.NoMenu, "Botão Menu principal volta pro menu");

        Finalizar();
    }

    // escola por dentro
    IEnumerator TesteEscolaPorDentro(GameManager gm, PlayerHealth ph)
    {
        var esc = EscolaInterior.Instance;
        var obj = ObjectiveManager.Instance;
        var dn = DayNightController.Instance;
        Secao("Escola por dentro (v1.4)");
        Teleportar(gm.escola.position);
        yield return new WaitForSeconds(0.5f);
        Checar(obj.Texto.Contains("Entre na escola") && obj.Alvo == esc.interior.portaFora.transform, "Chegar na escola pede pra entrar pela porta da frente", obj.Texto);
        var porta = esc.interior.portaFora;
        Checar(DistH(porta.Posicao, gm.escola.position) < 26f, "Porta da escola perto do farol", $"{DistH(porta.Posicao, gm.escola.position):0.0} m");
        var caminho = new NavMeshPath();
        bool chega = NavMesh.SamplePosition(esc.interior.saida.position, out var hs, 2f, NavMesh.AllAreas)
                     && NavMesh.CalculatePath(ph.transform.position, hs.position, NavMesh.AllAreas, caminho) && caminho.status == NavMeshPathStatus.PathComplete;
        Checar(chega, "Dá pra andar até a porta da escola (NavMesh da rua)");
        TeleportarNoChao(esc.interior.saida.position);
        OlharPara(porta.transform.position, 6f);
        yield return new WaitForSeconds(0.4f);
        Foto("04_escola_fachada");
        TeleportarNoChao(porta.Posicao);
        yield return null;
        yield return null;
        Checar(TextoDica().Contains("Entrar na escola"), "Na porta aparece \"[E] Entrar na escola\"", TextoDica());
        yield return ApertarE();
        float t0 = Time.time;
        while ((Interior.Atual != esc.interior || gm.EmTransicao) && Time.time - t0 < 6f) yield return null;
        Checar(Interior.Atual == esc.interior, "E na porta: a tela escurece e o Marcos entra na escola", $"{Time.time - t0:0.0} s");
        Checar(dn != null && dn.Interno && dn.sol != null && !dn.sol.enabled && GameObject.Find("Lanterna").GetComponent<Light>().enabled,
            "Lá dentro: sem sol, escuro e com a lanterna acesa");
        Checar(ph.transform.position.y < gm.origemCidade.y - 400f, "Interior fica longe da cidade (embaixo do mapa)", $"y = {ph.transform.position.y:0}");
        Checar(gm.TemCheckpoint && gm.NomeCheckpoint.Contains("escola"), "Entrar na escola salva checkpoint", gm.NomeCheckpoint);
        int fixos = ZumbisAtivosEm(esc.interior.zumbis != null ? esc.interior.zumbis.transform : null);
        Checar(fixos >= 5, "Zumbis fixos acordam lá dentro", fixos + " zumbis");
        yield return new WaitForSeconds(2.5f);
        int deOnda = FindObjectsByType<ZombieAI>().Count(z => !z.EstaMorto && !z.transform.IsChildOf(esc.transform));
        Checar(deOnda == 0, "Dentro da escola não nasce zumbi de onda", deOnda + " de fora");
        // fotos do caminho (sem zumbi atrapalhando o teste: imortal)
        OlharPara(ph.transform.position + Vector3.forward * 10f, 6f);
        yield return new WaitForSeconds(0.4f);
        Foto("04b_escola_saguao");
        Vector3 R = esc.transform.position;
        TeleportarNoChao(R + new Vector3(12.5f, 0f, 10f));
        OlharPara(R + new Vector3(30f, 1f, 10f), 4f);
        yield return new WaitForSeconds(0.4f);
        Foto("04c_escola_corredor");

        // mochila
        TeleportarNoChao(esc.mochila.Posicao + Vector3.back * 0.6f);
        yield return null;
        yield return null;
        Checar(TextoDica().Contains("mochila"), "Perto da mochila aparece \"[E] Examinar a mochila\"", TextoDica());
        yield return ApertarE();
        yield return null;
        Checar(esc.ViuMochila && obj.Texto.Contains("4º ano B"), "Mochila da Lia: objetivo vira achar a sala do 4º B", obj.Texto);
        // porta trancada
        TeleportarNoChao(esc.porta4B.Posicao);
        yield return null;
        yield return ApertarE();
        yield return null;
        Checar(!esc.PortaAberta && esc.SabeDaChave && obj.Texto.Contains("professores"), "Sala do 4º B trancada: precisa da chave da sala dos professores", obj.Texto);
        var carvado = esc.folha4B.GetComponentInChildren<NavMeshObstacle>();
        Checar(carvado != null && carvado.carving, "Porta trancada bloqueia o NavMesh (zumbi não atravessa)");
        // chave
        TeleportarNoChao(esc.chave.Posicao);
        OlharPara(esc.chaveVisual != null ? esc.chaveVisual.transform.position : esc.chave.Posicao, 10f);
        yield return new WaitForSeconds(0.3f);
        Foto("04f_professores_chaves");
        yield return ApertarE();
        yield return null;
        Checar(esc.TemChave && (esc.chaveVisual == null || !esc.chaveVisual.activeSelf), "Pegar a chave do 4º B no quadro de chaves");
        // abrir a sala
        TeleportarNoChao(esc.porta4B.Posicao);
        yield return null;
        yield return ApertarE();
        yield return new WaitForSeconds(1.2f);
        Checar(esc.PortaAberta && Quaternion.Angle(esc.folha4B.transform.localRotation, Quaternion.identity) > 1f, "Com a chave a porta do 4º B abre");
        bool entra = NavMesh.SamplePosition(esc.desenho.transform.position, out var hd, 2f, NavMesh.AllAreas)
                     && NavMesh.CalculatePath(ph.transform.position, hd.position, NavMesh.AllAreas, caminho) && caminho.status == NavMeshPathStatus.PathComplete;
        Checar(entra, "Porta aberta: dá pra andar até a carteira da Lia");
        // desenho
        TeleportarNoChao(esc.desenho.transform.position);
        OlharPara(R + new Vector3(25.5f, 1.6f, 15.5f), 8f);
        yield return new WaitForSeconds(0.4f);
        Foto("04e_sala_4b");
        Checar(TextoDica().Contains("desenho"), "Perto da carteira dela aparece \"[E] Pegar o desenho\"", TextoDica());
        yield return ApertarE();
        yield return null;
        var painelDesenho = GameObject.Find("Desenho");
        Checar(gm.LendoDesenho && Time.timeScale == 0f && painelDesenho != null && painelDesenho.activeInHierarchy, "Tecla E pega o desenho e mostra na tela (jogo congela)");
        FotoComHUD("04a_desenho_tela");
        yield return ApertarE();
        yield return null;
        Checar(!gm.LendoDesenho && Time.timeScale == 1f, "E de novo guarda o desenho e o jogo volta");
        // fuga
        t0 = Time.time;
        while (ZumbisAtivosEm(esc.zumbisFuga != null ? esc.zumbisFuga.transform : null) == 0 && Time.time - t0 < 6f) yield return null;
        int horda = ZumbisAtivosEm(esc.zumbisFuga != null ? esc.zumbisFuga.transform : null);
        Checar(esc.Fugindo && horda >= 4 && obj.Texto.Contains("FUJA"), "Depois do desenho: barulho, a horda entra e o objetivo vira FUGIR", $"{horda} zumbis, \"{obj.Texto}\"");
        Checar(SomManager.Instance.MusicaAtual == SomManager.Instance.musicaNoite, "Música de tensão na fuga");
        var zf = esc.zumbisFuga.GetComponentsInChildren<ZombieAI>().FirstOrDefault(z => !z.EstaMorto);
        if (zf != null)
        {
            float d0 = Vector3.Distance(zf.transform.position, ph.transform.position);
            yield return new WaitForSeconds(1.5f);
            float d1 = Vector3.Distance(zf.transform.position, ph.transform.position);
            Checar(d1 < d0 - 0.5f, "Zumbis da horda vêm atrás do Marcos lá dentro", $"{d0:0.0} m -> {d1:0.0} m");
        }
        TeleportarNoChao(R + new Vector3(17f, 0f, 6f));
        OlharPara(R + new Vector3(17f, 1f, 0f), 6f);
        yield return new WaitForSeconds(0.3f);
        Foto("04d_fuga");
        TeleportarNoChao(esc.interior.portaDentro.Posicao);
        t0 = Time.time;
        while ((Interior.Atual != null || gm.EmTransicao) && Time.time - t0 < 6f) yield return null;
        Checar(Interior.Atual == null && esc.Fugiu, "Chegou na porta da frente: sai da escola sem precisar de E", $"{Time.time - t0:0.0} s");
        Checar(dn.sol.enabled && !dn.Interno, "Lá fora a luz do dia volta");
        Checar(ZumbisAtivosEm(esc.transform) == 0, "Zumbis da escola ficam lá dentro (desligados)");
        Checar(DistH(ph.transform.position, esc.interior.saida.position) < 1.5f, "Sai na calçada, na frente da porta da escola");
    }

    // clínica por dentro
    IEnumerator TesteClinicaPorDentro(GameManager gm, PlayerHealth ph)
    {
        var cli = ClinicaInterior.Instance;
        var obj = ObjectiveManager.Instance;
        Secao("Clínica por dentro (v1.4)");
        Teleportar(gm.clinica.position);
        yield return new WaitForSeconds(0.5f);
        Checar(obj.Texto.Contains("Entre na Clínica") && obj.Alvo == cli.interior.portaFora.transform, "Chegar na clínica: a Lia chama e o objetivo é entrar", obj.Texto);
        TeleportarNoChao(cli.interior.saida.position);
        OlharPara(cli.interior.portaFora.transform.position, 6f);
        yield return new WaitForSeconds(0.4f);
        Foto("05b_clinica_fachada");
        TeleportarNoChao(cli.interior.portaFora.Posicao);
        yield return null;
        yield return ApertarE();
        float t0 = Time.time;
        while ((Interior.Atual != cli.interior || gm.EmTransicao) && Time.time - t0 < 6f) yield return null;
        Checar(Interior.Atual == cli.interior, "E na porta: entra na clínica");
        Checar(obj.ModoSinal && obj.Alvo == cli.esconderijo && obj.DistanciaSinal < 40f, "Lá dentro o sinal do rádio vira \"quente ou frio\" (escala da clínica)", $"zera a {obj.DistanciaSinal:0} m");
        float f0 = obj.ForcaSinal(ph.transform.position);
        yield return new WaitForSeconds(1.6f);
        FotoComHUD("06b_clinica_recepcao");
        Vector3 R = cli.transform.position;
        // esconderijo falso 1: cortina da maca (susto)
        TeleportarNoChao(cli.falsos[0].Posicao);
        OlharPara(R + new Vector3(11f, 1f, 16f), 6f);
        yield return new WaitForSeconds(0.3f);
        Foto("06c_clinica_enfermaria");
        yield return ApertarE();
        yield return new WaitForSeconds(0.4f);
        int susto = ZumbisAtivosEm(cli.sustos[0].transform);
        Checar(cli.FalsosAbertos == 1 && susto == 1 && !cli.abrirFalso[0].activeSelf, "Cortina falsa: vazia... e um zumbi pula", $"{susto} zumbi");
        foreach (var z in cli.sustos[0].GetComponentsInChildren<ZombieAI>()) z.GetComponent<ZombieHealth>().ReceberDano(99999f);
        // quente: perto do armário do depósito
        TeleportarNoChao(cli.armario.Posicao);
        OlharPara(cli.esconderijo.position, 6f);
        yield return null;
        float f1 = obj.ForcaSinal(ph.transform.position);
        Checar(f1 > 0.75f && f1 > f0 + 0.3f, "Quente: o sinal enche perto do esconderijo", $"{f0:0.00} na entrada -> {f1:0.00} no depósito");
        t0 = Time.time;
        bool falouQuente = false;
        while (Time.time - t0 < 12f && !falouQuente)
        {
            falouQuente = RadioSystem.Instance.Mostrando && RadioSystem.Instance.Texto.Contains("quente");
            yield return null;
        }
        Checar(falouQuente, "A Lia sussurra \"tá quente\" pelo rádio");
        Foto("06d_clinica_deposito");
        var armario = cli.armarioMovel.transform;
        Vector3 a0 = armario.position;
        yield return ApertarE();
        t0 = Time.time;
        while (!gm.EmCena && Time.time - t0 < 10f) yield return null;
        Checar(cli.Achou && Vector3.Distance(a0, armario.position) > 1.2f, "Empurrar o armário revela o esconderijo da Lia", $"andou {Vector3.Distance(a0, armario.position):0.0} m");
    }

    void Finalizar()
    {
        Secao("Console");
        Checar(erros == 0, "Nenhum erro/exceção no Console durante o teste", erros + " erros");
        if (inputOriginal != null) InputSystem.settings = inputOriginal;
        ZombieAI.AnimarForaDaTela = false;

        sb.AppendLine($"\nRESULTADO DO PLAY: {ok} OK, {falhas} FALHA(S), {avisos} aviso(s)");
        File.AppendAllText(Relatorio, sb.ToString());
        Debug.Log("[Validação v0.1] " + (falhas == 0 ? "PASSOU" : "FALHOU") + $" - {ok} OK, {falhas} falhas, {avisos} avisos. Relatório: Logs/Validacao_v01.txt");
#if UNITY_EDITOR
        if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(falhas > 0 ? 1 : 0);
        else UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
