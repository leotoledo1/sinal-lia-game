using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public enum Fase { Dia1, Noite1, Dia2, NoiteFinal, Vitoria, GameOver }

// Controla o fluxo: Menu -> abertura -> Dia 1 -> Noite 1 -> Dia 2 -> reencontro -> Noite final -> cena final -> Vitória.
// Entre as fases a tela escurece e clareia (sem "pulo" de luz). Cenas de cinema têm barras pretas e dá pra pular (ENTER/ESC).
// Morreu: dá pra tentar de novo do começo da fase (checkpoint) sem voltar pro Dia 1.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Referências (preenchidas pelo menu Sinal da Lia)")]
    public Transform jogador;
    public Transform escola;
    public Transform clinica;
    public GameObject farolEscola;
    public GameObject farolClinica;
    public GameObject lia;
    public DesenhoLia desenho;
    [Tooltip("Modelo 3D do helicóptero de resgate (vazio = formas simples)")]
    public GameObject prefabHelicoptero;

    [Header("Itens espalhados")]
    public int comidasIniciais = 10;
    public int kitsIniciais = 4;

    [Header("Regras")]
    public float raioChegada = 8f;
    public float duracaoNoite1 = 90f;
    public float duracaoNoiteFinal = 120f;
    [Tooltip("Segundos antes do fim da noite final em que o helicóptero aparece no céu")]
    public float avisoHelicoptero = 20f;
    [Tooltip("Distância em que o farol da clínica aparece no Dia 2")]
    public float distanciaRevelarClinica = 45f;
    public int caixasMunicaoIniciais = 18;
    public Vector3 origemCidade;
    public Vector2 limitesCidade = new Vector2(150f, 150f);

    [Header("Cinema")]
    [Tooltip("Segundos pra tela escurecer entre as fases")]
    public float duracaoFade = 0.9f;
    [Tooltip("Game Over: segundos de tela vermelha/escura antes do painel")]
    public float atrasoPainelFim = 1.8f;

    [Header("Ondas por fase")]
    public ConfigOnda ondaDia1 = new ConfigOnda { maxVivos = 6, intervalo = 6f, multVida = 1f, multVelocidade = 1f, multDano = 1f, sempreCacar = false, chanceRapido = 0.05f };
    public ConfigOnda ondaNoite1 = new ConfigOnda { maxVivos = 12, intervalo = 2.4f, multVida = 1.2f, multVelocidade = 1.15f, multDano = 1.1f, sempreCacar = true, chanceRapido = 0.15f };
    public ConfigOnda ondaDia2 = new ConfigOnda { maxVivos = 9, intervalo = 4.5f, multVida = 1.1f, multVelocidade = 1.05f, multDano = 1.1f, sempreCacar = false, chanceRapido = 0.12f };
    public ConfigOnda ondaNoiteFinal = new ConfigOnda { maxVivos = 12, intervalo = 2.2f, multVida = 1.25f, multVelocidade = 1.1f, multDano = 1.1f, sempreCacar = true, chanceRapido = 0.12f };

    public Fase FaseAtual { get; private set; }
    public float TempoRestante { get; private set; }
    public bool NoMenu { get; private set; }
    public bool Pausado { get; private set; }
    // desenho da Lia aberto na tela (jogo congelado)
    public bool LendoDesenho { get; private set; }
    // cena de cinema (abertura, reencontro, final): barras pretas, sem controle do jogador
    public bool EmCena { get; private set; }
    // tela escurecendo entre uma fase e outra
    public bool EmTransicao { get; private set; }
    public bool Acabou => FaseAtual == Fase.Vitoria || FaseAtual == Fase.GameOver;
    public bool JogoAtivo => !NoMenu && !Pausado && !LendoDesenho && !EmCena && !EmTransicao && !Acabou;
    public int Abates { get; set; }
    public int Headshots { get; set; }
    public float TempoDeJogo => Acabou ? tempoFinal : (NoMenu ? 0f : Time.time - inicioJogo);
    public string MotivoFim { get; private set; }

    // tela preta por cima do jogo (0..1) e texto no meio dela (abertura)
    public float Escuro { get; private set; }
    public string Cartela { get; private set; } = "";
    public float CartelaAlfa { get; private set; }
    // quando o jogo acabou (o painel de Game Over espera a tela escurecer)
    public float InicioFim { get; private set; } = -99f;
    public bool MostrarPainelFim => Acabou && Time.unscaledTime - InicioFim >= (FaseAtual == Fase.GameOver ? atrasoPainelFim : 0f);

    // título grande no meio da tela quando a fase começa
    public string TituloFase { get; private set; } = "";
    public string SubtituloFase { get; private set; } = "";
    public float InicioTitulo { get; private set; } = -99f;

    // checkpoint (sobrevive ao recarregar a cena)
    class Checkpoint
    {
        public Fase fase;
        public bool[] liberadas;
        public int[] reservas;
        public int kits, abates, headshots;
        public float tempo;
    }
    static Checkpoint checkpoint;
    static bool usarCheckpoint, pularMenu;
    public bool TemCheckpoint => checkpoint != null;
    public string NomeCheckpoint => checkpoint == null ? "" : (checkpoint.fase == Fase.Dia1 ? "Dia 1 (escola)" : NomeFase(checkpoint.fase));

    bool chegouEscola, anoitecendo, achouLia, avisouHelicoptero, chegouClinica;
    bool falouMeioNoite, falouMeioFinal, falouSinalMeio, falouSinalPerto;
    bool pular;
    float inicioCena;
    int quadroDesenho;
    float tempoFinal, inicioJogo;
    Helicoptero heli;
    ThirdPersonCamera camJogo;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        // o caminho no NavMesh é calculado aos poucos (padrão 100 passos por quadro, dividido entre Lia e zumbis).
        // Com FPS baixo (60, ou 16-32 na Área de Trabalho Remota) caminhos longos não terminavam e a Lia ficava parada
        UnityEngine.AI.NavMesh.pathfindingIterationsPerFrame = 1000;
        if (Camera.main != null) camJogo = Camera.main.GetComponent<ThirdPersonCamera>();
        // tecla E nas portas/objetos (cenas antigas sem o componente ganham ele aqui)
        if (jogador != null && jogador.GetComponent<InteracaoJogador>() == null) jogador.gameObject.AddComponent<InteracaoJogador>();
        if (qualidadeOriginal < 0) qualidadeOriginal = QualitySettings.GetQualityLevel();
        AplicarGraficos(PlayerPrefs.GetInt(PREF_GRAFICOS, 0) == 1);
        Opcoes.Aplicar();
        TelaCheiaNaPrimeiraVez();
    }

        // abre em tela cheia na resolucao do monitor na primeira execucao
    const string PREF_TELA = "SinalDaLia_TelaV2";
    static void TelaCheiaNaPrimeiraVez()
    {
        if (Application.isEditor || PlayerPrefs.GetInt(PREF_TELA, 0) == 1) return;
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testeRapido") >= 0) return;
        PlayerPrefs.SetInt(PREF_TELA, 1);
        var r = Screen.currentResolution;
        Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
    }

    // F11: tela cheia <-> janela
    public static void AlternarTelaCheia()
    {
        if (Screen.fullScreenMode == FullScreenMode.Windowed)
        {
            var r = Screen.currentResolution;
            Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
        }
        else Screen.SetResolution(Mathf.RoundToInt(Screen.currentResolution.width * 0.75f), Mathf.RoundToInt(Screen.currentResolution.height * 0.75f), FullScreenMode.Windowed);
    }

    void Start()
    {
        EspalharMunicao();
        EspalharItens();
        if (lia != null) lia.SetActive(false);
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-testeRapido") >= 0) StartCoroutine(TesteRapido());
        else if (usarCheckpoint && checkpoint != null) ComecarDoCheckpoint();
        else if (pularMenu) ComecarJogo(false);
        else AbrirMenu();
        usarCheckpoint = pularMenu = false;
        StartCoroutine(Aquecer());
    }

    public static string NomeFase(Fase f)
    {
        switch (f)
        {
            case Fase.Dia1: return "Dia 1";
            case Fase.Noite1: return "Noite 1";
            case Fase.Dia2: return "Dia 2";
            case Fase.NoiteFinal: return "Noite final";
            default: return "";
        }
    }

    // menu / início

    void AbrirMenu()
    {
        NoMenu = true;
        checkpoint = null;
        TravarCursor(false);
        SomManager.Instance?.TocarMusica(SomManager.Instance.musicaMenu);
    }

    // botão JOGAR do menu: começa com a abertura (texto da história)
    public void Jogar() => ComecarJogo(true);

    void ComecarJogo(bool abertura)
    {
        if ((!NoMenu && FaseAtual != Fase.Dia1) || EmCena || EmTransicao) return;
        NoMenu = false;
        inicioJogo = Time.time;
        TravarCursor(true);
        if (abertura) StartCoroutine(Abertura());
        else
        {
            Escuro = 1f;
            ComecarDia1();
            StartCoroutine(Clarear(1.2f));
        }
    }

    void ComecarDia1()
    {
        if (jogador != null) camJogo?.OlharPara(jogador.eulerAngles.y);
        IniciarFase(Fase.Dia1);
    }

    // tela preta com a história, uma frase de cada vez (ENTER/ESC pula)
    IEnumerator Abertura()
    {
        ComecarCena();
        yield return Escurecer(0.8f);
        string[] linhas =
        {
            "Duas semanas depois do surto.",
            "Marcos foi mordido... e não virou.",
            "O exército quer o sangue dele: pode ser a vacina.",
            "Mas antes ele precisa achar a filha.\n<color=#ffd34d>Lia, 9 anos.</color> Sumiu quando a escola foi evacuada.",
        };
        CartelaAlfa = 1f;
        for (int i = 0; i < linhas.Length && !pular; i++)
        {
            string antes = string.Join("\n\n", linhas, 0, i);
            for (float t = 0f; t < 0.7f && !pular; t += Time.unscaledDeltaTime)
            {
                // a frase nova aparece devagar (alfa no rich text)
                string alfa = Mathf.RoundToInt(Mathf.Clamp01(t / 0.7f) * 255f).ToString("X2");
                Cartela = (i > 0 ? antes + "\n\n" : "") + $"<color=#FFFFFF{alfa}>{linhas[i]}</color>";
                yield return null;
            }
            Cartela = string.Join("\n\n", linhas, 0, i + 1);
            yield return EsperarSeg(i == linhas.Length - 1 ? 2.6f : 1.7f);
        }
        for (float t = 0f; t < 0.6f && !pular; t += Time.unscaledDeltaTime)
        {
            CartelaAlfa = 1f - t / 0.6f;
            yield return null;
        }
        CartelaAlfa = 0f;
        Cartela = "";
        pular = false;
        EmCena = false;
        ComecarDia1();
        yield return Clarear(1.4f);
    }

    // build: "SinalDaLia.exe -testeRapido" joga sozinho por uns segundos e fecha (confere se a build roda)
    IEnumerator TesteRapido()
    {
        ComecarJogo(false);
        yield return new WaitForSecondsRealtime(15f);
        Debug.Log($"[TesteRapido] OK - fase {FaseAtual}, {Time.frameCount} quadros, zumbis no pool: {(WaveSpawner.Instance != null)}");
        Application.Quit();
    }

    void ComecarDoCheckpoint()
    {
        var c = checkpoint;
        NoMenu = false;
        inicioJogo = Time.time - c.tempo;
        Abates = c.abates;
        Headshots = c.headshots;
        PlayerWeapons.Instance?.Restaurar(c.liberadas, c.reservas);
        PlayerHealth.Instance?.DefinirKits(c.kits);
        chegouEscola = true;
        // checkpoint "Dia 1 (escola)": volta pra frente da porta da escola (o desenho ainda não foi lido)
        var esc = EscolaInterior.Instance;
        bool naEscola = c.fase == Fase.Dia1 && esc != null && esc.interior != null && esc.interior.saida != null;
        if (!naEscola && desenho != null) desenho.gameObject.SetActive(false); // já foi lido
        if (naEscola)
        {
            TeleportarJogador(esc.interior.saida.position);
            jogador.rotation = Quaternion.Euler(0f, esc.interior.saida.eulerAngles.y + 180f, 0f); // de frente pra porta
        }
        else if (c.fase == Fase.NoiteFinal && clinica != null) TeleportarJogador(clinica.position);
        else if (escola != null) TeleportarJogador(escola.position);
        if (jogador != null) camJogo?.OlharPara(jogador.eulerAngles.y);
        // armas que ainda não tinham sido pegas continuam no mapa
        if (escola != null) ColocarArma(0, escola.position);
        if (c.fase == Fase.NoiteFinal && escola != null && clinica != null)
        {
            ColocarArma(1, MeioDoCaminho(escola.position, clinica.position));
            ColocarArma(2, clinica.position);
        }
        TravarCursor(true);
        Escuro = 1f;
        StartCoroutine(Clarear(1.2f));
        RadioSystem.Instance?.Enfileirar("Marcos", "De novo. Dessa vez eu consigo.", "marcos_12", true);
        if (c.fase == Fase.NoiteFinal) AchouLia(false);
        else if (naEscola) RetomarNaEscola();
        else IniciarFase(c.fase);
    }

    // Dia 1 de novo, já na porta da escola (sem as falas do começo do dia)
    void RetomarNaEscola()
    {
        FaseAtual = Fase.Dia1;
        DayNightController.Instance?.Aplicar(false);
        HUD.LimparTutoriais();
        if (farolEscola != null) farolEscola.SetActive(true);
        if (farolClinica != null) farolClinica.SetActive(false);
        WaveSpawner.Instance?.Configurar(ondaDia1);
        var som = SomManager.Instance;
        som?.TocarMusica(som.musicaDia);
        Titulo("DIA 1", "A escola da Lia");
        EscolaInterior.Instance?.RetomarNaPorta();
    }

    // 1ª vez dentro da escola = checkpoint (morreu lá dentro: volta pra porta, não pro acampamento)
    public void CheckpointEscola()
    {
        if (FaseAtual == Fase.Dia1 && !Acabou) SalvarCheckpoint(Fase.Dia1);
    }

    void TeleportarJogador(Vector3 p)
    {
        if (jogador == null) return;
        var cc = jogador.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        jogador.position = p;
        if (cc != null) cc.enabled = true;
    }

    void SalvarCheckpoint(Fase fase)
    {
        var pw = PlayerWeapons.Instance;
        checkpoint = new Checkpoint
        {
            fase = fase,
            liberadas = pw != null ? pw.Liberadas() : null,
            reservas = pw != null ? pw.Reservas() : null,
            kits = PlayerHealth.Instance != null ? PlayerHealth.Instance.Kits : 0,
            abates = Abates,
            headshots = Headshots,
            tempo = TempoDeJogo,
        };
    }

    // desempenho

    const string PREF_GRAFICOS = "SinalDaLia_GraficosLeves";
    static int qualidadeOriginal = -1;
    public bool GraficosLeves { get; private set; }
    public bool Carregando { get; private set; }

    public void AlternarGraficos() => AplicarGraficos(!GraficosLeves);

    // Alto = perfil "PC" do projeto (anti-serrilhado SMAA); Leve = perfil "Mobile" (menos sombra, resolução 80%, FXAA)
    void AplicarGraficos(bool leve)
    {
        GraficosLeves = leve;
        int nivel = System.Array.IndexOf(QualitySettings.names, leve ? "Mobile" : "PC");
        if (nivel >= 0 && nivel != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(nivel, true);
        // VSync deixa o ritmo dos quadros estável (sem "travadinhas"). Na Área de Trabalho Remota a tela virtual
        // roda a ~32 Hz e o VSync prendia o jogo em 16 fps: lá fica sem VSync, limitado a 60
        bool remoto = MouseJogo.SessaoRemota;
        QualitySettings.vSyncCount = remoto ? 0 : 1;
        Application.targetFrameRate = remoto ? 60 : -1;
        PlayerPrefs.SetInt(PREF_GRAFICOS, leve ? 1 : 0);
        var cam = Camera.main;
        if (cam != null && cam.TryGetComponent<UniversalAdditionalCameraData>(out var dados))
        {
            dados.antialiasing = leve ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            dados.antialiasingQuality = AntialiasingQuality.High;
            dados.dithering = true; // sem "degraus" no degradê do céu escuro
        }
    }

    void OnApplicationQuit()
    {
        // no Editor, não deixa o projeto preso no perfil "Leve" depois do Play
        if (qualidadeOriginal >= 0) QualitySettings.SetQualityLevel(qualidadeOriginal, false);
    }

    // Os primeiros usos de shader/luz compilam na hora e engasgam.
    // Aqui tudo aparece por 2 quadros atrás da tela "Carregando...".
    IEnumerator Aquecer()
    {
        Carregando = true;
        var ligadas = new System.Collections.Generic.List<Light>();
        foreach (var l in FindObjectsByType<Light>())
            if (!l.enabled && l.type != LightType.Directional) { l.enabled = true; ligadas.Add(l); }
        if (WaveSpawner.Instance != null) yield return WaveSpawner.Instance.Aquecer(Camera.main);
        else { yield return null; yield return null; }
        foreach (var l in ligadas) if (l != null) l.enabled = false;
        if (DayNightController.Instance != null) DayNightController.Instance.Aplicar(!NoMenu && (FaseAtual == Fase.Noite1 || FaseAtual == Fase.NoiteFinal));
        yield return null;
        Carregando = false;
    }

    // cinema (tela preta, esperar falas, pular)

    IEnumerator Escurecer(float duracao)
    {
        while (Escuro < 1f)
        {
            Escuro = Mathf.MoveTowards(Escuro, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, duracao));
            yield return null;
        }
    }

    IEnumerator Clarear(float duracao)
    {
        while (Escuro > 0f)
        {
            Escuro = Mathf.MoveTowards(Escuro, 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, duracao));
            yield return null;
        }
    }

    // escurece, troca a fase com a tela preta, e clareia (o jogador já pode se mexer enquanto clareia)
    IEnumerator Transicao(System.Action noEscuro, float segurar = 0.8f)
    {
        EmTransicao = true;
        yield return Escurecer(duracaoFade);
        noEscuro?.Invoke();
        for (float t = 0f; t < segurar; t += Time.unscaledDeltaTime) yield return null;
        EmTransicao = false;
        yield return Clarear(duracaoFade * 1.3f);
    }

    IEnumerator EsperarSeg(float segundos)
    {
        for (float t = 0f; t < segundos && !pular; t += Time.deltaTime) yield return null;
    }

    // espera a fila de falas acabar (ou o jogador pular a cena)
    IEnumerator EsperarRadio(float maximo)
    {
        for (float t = 0f; t < maximo && !pular && RadioSystem.Instance != null && RadioSystem.Instance.Ocupado; t += Time.deltaTime)
            yield return null;
    }

    // começo de uma cena (abertura, reencontro, final)
    void ComecarCena()
    {
        EmCena = true;
        pular = false;
        inicioCena = Time.unscaledTime;
    }

    // ENTER/ESC durante uma cena: corta as falas e segue.
    // Ignora os primeiros 0,4 s: o ENTER que começou o jogo no menu não pode pular a abertura no mesmo quadro
    public void PularCena(bool forcar = false)
    {
        if (!EmCena || (!forcar && Time.unscaledTime - inicioCena < 0.4f)) return;
        pular = true;
        RadioSystem.Instance?.Silenciar();
    }

    // loop

    void Update()
    {
        var kb = Keyboard.current;
        // F11 alterna entre tela cheia e janela
        if (kb != null && kb.f11Key.wasPressedThisFrame) AlternarTelaCheia();
        if (LendoDesenho)
        {
            // E, ESC, Espaco ou clique fecham o desenho aberto
            var m = Mouse.current;
            bool fechar = (kb != null && (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                          || (m != null && m.leftButton.wasPressedThisFrame);
            if (fechar && Time.frameCount > quadroDesenho) FecharDesenho();
            return;
        }
        // ENTER ou ESC pulam as cenas de historia
        if (EmCena && kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
        {
            PularCena();
            return;
        }
        // ESC abre ou fecha o menu de pausa durante o jogo
        if (kb != null && kb.escapeKey.wasPressedThisFrame && !NoMenu && !Acabou && !EmCena && !EmTransicao)
            AlternarPausa();

        if (!JogoAtivo) return;

        // jogando (sem pausa/menu/cena): o mouse fica sempre preso no centro da tela, senão a câmera não gira.
        // (no Editor, o ESC que pula a abertura solta o mouse; antes só voltava clicando na tela)
        // (preso do jeito errado = mudou o modo do mouse, ex.: Área de Trabalho Remota: prende do jeito novo)
        if (!MouseJogo.Preso && (Application.isFocused || Cursor.lockState != CursorLockMode.None || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)))
            TravarCursor(true);

        var radio = RadioSystem.Instance;
        switch (FaseAtual)
        {
            case Fase.Dia1:
                if (!chegouEscola && Perto(escola)) ChegouNaEscola();
                break;
            case Fase.Noite1:
                TempoRestante -= Time.deltaTime;
                // no meio da noite a Lia chama no rádio
                if (!falouMeioNoite && TempoRestante <= duracaoNoite1 * 0.5f)
                {
                    falouMeioNoite = true;
                    radio?.Enfileirar("Lia", "Pai... cê ainda tá aí? Eu contei até mil e você ainda não chegou...", "lia_02");
                    radio?.Enfileirar("Marcos", "Tô aqui, filha. Aguenta só essa noite. Amanhã eu te busco. Prometo.", "marcos_05", true);
                }
                if (TempoRestante <= 0f) Amanheceu();
                break;
            case Fase.Dia2:
                if (farolClinica != null && clinica != null && jogador != null)
                    farolClinica.SetActive(!chegouClinica && Vector3.Distance(jogador.position, clinica.position) < distanciaRevelarClinica);
                if (ObjectiveManager.Instance != null && jogador != null)
                {
                    float forca = ObjectiveManager.Instance.ForcaSinal(jogador.position);
                    // quanto mais perto da Lia, mais o sol se põe
                    var dn = DayNightController.Instance;
                    if (dn != null) dn.AplicarEntardecer(Mathf.MoveTowards(dn.Entardecer, Mathf.Max(dn.Entardecer, forca), 0.15f * Time.deltaTime));
                    // a Lia percebe o pai chegando (o sinal melhora) - lá fora; dentro da clínica é o "quente/frio"
                    if (!falouSinalMeio && forca >= 0.5f && Interior.Atual == null)
                    {
                        falouSinalMeio = true;
                        radio?.Enfileirar("Lia", "Pai? Tá chiando menos! Cê tá chegando?", "lia_04");
                    }
                }
                if (!falouSinalPerto && farolClinica != null && farolClinica.activeSelf && Interior.Atual == null)
                {
                    falouSinalPerto = true;
                    radio?.Enfileirar("Lia", "Eu ouvi tiro! É você? Eu tô perto da cruz vermelha!", "lia_05");
                }
                // na porta da clínica a Lia chama; achar ela é lá dentro (ClinicaInterior)
                if (ClinicaInterior.Instance != null)
                {
                    if (!chegouClinica && Perto(clinica))
                    {
                        chegouClinica = true;
                        ClinicaInterior.Instance.Chegou();
                    }
                }
                else if (!achouLia && Perto(clinica)) AchouLia(true);
                break;
            case Fase.NoiteFinal:
                TempoRestante -= Time.deltaTime;
                if (!falouMeioFinal && TempoRestante <= 70f)
                {
                    falouMeioFinal = true;
                    radio?.Enfileirar("Lia", "Pai, eles tão vindo de todo lado!", "lia_08", true);
                    radio?.Enfileirar("Marcos", "Fica atrás de mim, Lia! Ninguém encosta em você!", "marcos_10", true);
                }
                if (!avisouHelicoptero && TempoRestante <= avisoHelicoptero) ChamarHelicoptero();
                if (TempoRestante <= 0f)
                {
                    TempoRestante = 0f;
                    StartCoroutine(CenaFinal());
                }
                break;
        }
    }

    void ChamarHelicoptero()
    {
        avisouHelicoptero = true;
        if (clinica != null && heli == null) heli = Helicoptero.Criar(clinica.position, SomManager.Instance != null ? SomManager.Instance.helicoptero : null, prefabHelicoptero);
        int seg = Mathf.CeilToInt(avisoHelicoptero);
        RadioSystem.Instance?.Enfileirar("Base Ômega", seg == 20 ? "Helicóptero chegando! Aguentem mais vinte segundos!" : $"Helicóptero chegando! Aguentem mais {seg} segundos!", seg == 20 ? "omega_05" : null);
        HUD.Tutorial("O HELICÓPTERO ESTÁ CHEGANDO! Proteja a Lia só mais um pouco!");
    }

    public Helicoptero Heli => heli;

    bool Perto(Transform alvo)
    {
        if (alvo == null || jogador == null) return false;
        Vector3 d = jogador.position - alvo.position;
        d.y = 0f;
        return d.magnitude < raioChegada;
    }

    void Titulo(string titulo, string sub)
    {
        TituloFase = titulo;
        SubtituloFase = sub;
        InicioTitulo = Time.unscaledTime;
    }

    void IniciarFase(Fase fase)
    {
        FaseAtual = fase;
        var radio = RadioSystem.Instance;
        var obj = ObjectiveManager.Instance;
        var spawner = WaveSpawner.Instance;
        var som = SomManager.Instance;
        bool noite = fase == Fase.Noite1 || fase == Fase.NoiteFinal;
        if (DayNightController.Instance != null) DayNightController.Instance.Aplicar(noite);
        // dicas da fase anterior que ainda não apareceram não fazem mais sentido
        HUD.LimparTutoriais();

        if (farolEscola != null) farolEscola.SetActive(fase == Fase.Dia1);
        if (farolClinica != null) farolClinica.SetActive(false); // no Dia 2 aparece quando chega perto; na noite final você já está lá
        if (fase != Fase.Dia1) SalvarCheckpoint(fase);

        switch (fase)
        {
            case Fase.Dia1:
                Titulo("DIA 1", "Encontre a escola da Lia");
                radio?.Enfileirar("Base Ômega", "Sobrevivente assintomático, aqui é a Base Ômega. Seu sangue pode ser a chave da vacina. Extração na Clínica Central em dois dias. Sobreviva.", "omega_01");
                radio?.Enfileirar("Lia", "...pai? Pai, você tá aí? Eu tô com medo...", "lia_01");
                radio?.Enfileirar("Marcos", "Lia?! Filha, é o papai! Fala comigo! ...Nada. A escola... ela tava na escola.", "marcos_01", true);
                obj?.Definir("Vá até a escola da Lia", escola, false);
                spawner?.Configurar(ondaDia1);
                som?.TocarMusica(som.musicaDia);
                ColocarArma(0, escola != null ? escola.position : Vector3.zero);
                HUD.Tutorial("WASD anda   ·   SHIFT corre   ·   ESPAÇO pula");
                HUD.Tutorial("MOUSE gira a câmera   ·   BOTÃO DIREITO mira   ·   BOTÃO ESQUERDO atira");
                HUD.Tutorial("R recarrega   ·   1 2 3 4 trocam de arma (as outras armas você acha no mapa)");
                HUD.Tutorial("Siga a SETA e o FAROL AMARELO até a escola da Lia");
                break;
            case Fase.Noite1:
                Titulo("NOITE 1", "Sobreviva até amanhecer");
                TempoRestante = duracaoNoite1;
                falouMeioNoite = false;
                radio?.Enfileirar("Marcos", "Escureceu... e eles tão acordando. Aguenta, Marcos. É só uma noite.", "marcos_04", true);
                obj?.Definir("Sobreviva até amanhecer", null, false);
                spawner?.Configurar(ondaNoite1);
                som?.TocarMusica(som.musicaNoite);
                HUD.Tutorial("Eles vêm de todos os lados! Atire, recarregue (R) e não pare de se mexer.");
                HUD.Tutorial("O tempo até amanhecer aparece no topo da tela.");
                break;
            case Fase.Dia2:
                Titulo("DIA 2", "Siga o sinal do rádio até a Lia");
                falouSinalMeio = falouSinalPerto = false;
                radio?.Enfileirar("Lia", "Pai... os soldados foram embora. Tem muitos deles aqui fora. Eu fiquei quietinha e escondida.", "lia_03");
                radio?.Enfileirar("Marcos", "O rádio... quanto menos chiado, mais perto ela tá. Fica escondida, filha. Tô indo.", "marcos_06", true);
                radio?.Enfileirar("Base Ômega", "Sobrevivente, deixamos armamento no caminho da clínica. Procure a luz vermelha.", "omega_03");
                obj?.Definir("Siga o sinal do rádio até a Lia", clinica, true);
                spawner?.Configurar(ondaDia2);
                som?.TocarMusica(som.musicaDia2 != null ? som.musicaDia2 : som.musicaDia);
                if (escola != null && clinica != null) ColocarArma(1, MeioDoCaminho(escola.position, clinica.position));
                if (clinica != null) ColocarArma(2, clinica.position);
                if (lia != null) lia.SetActive(true); // escondida, agachada na clínica
                HUD.Tutorial("Não tem farol agora: siga o SINAL DO RÁDIO no topo da tela. Mais barras verdes = mais perto da Lia.");
                HUD.Tutorial("Feixes de luz vermelha no chão são ARMAS NOVAS. Pegue no caminho!");
                break;
            case Fase.NoiteFinal:
                Titulo("NOITE FINAL", "Proteja a Lia até o helicóptero chegar");
                TempoRestante = duracaoNoiteFinal;
                avisouHelicoptero = falouMeioFinal = false;
                radio?.Enfileirar("Marcos", "Base Ômega, aqui é o Marcos. Achei minha filha. Ela também é imune. Nós dois vamos nesse helicóptero.", "marcos_09", true);
                radio?.Enfileirar("Base Ômega", "...Entendido. Dois imunes. Helicóptero a caminho: dois minutos. Segurem a posição!", "omega_04");
                obj?.Definir("Proteja a Lia até o helicóptero chegar", null, false);
                spawner?.Configurar(ondaNoiteFinal);
                som?.TocarMusica(som.musicaFinal);
                HUD.Tutorial("A Lia vai te seguir. A barra ROSA no topo é a vida dela: se ela morrer, acabou.");
                HUD.Tutorial("Fique perto dela e mate os zumbis antes que cheguem nela.");
                break;
        }
    }

    // N-ésima arma que ainda está trancada vira um pickup com feixe de luz naquele ponto
    void ColocarArma(int ordem, Vector3 pos)
    {
        var pw = PlayerWeapons.Instance;
        if (pw == null) return;
        var trancadas = new System.Collections.Generic.List<WeaponData>();
        for (int i = 0; i < pw.armas.Length; i++) if (!pw.armas[i].liberadaNoInicio) trancadas.Add(pw.armas[i]);
        if (ordem >= trancadas.Count) return;
        int idx = System.Array.IndexOf(pw.armas, trancadas[ordem]);
        if (pw.Liberada(idx)) return;
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 6f, UnityEngine.AI.NavMesh.AllAreas)) pos = hit.position;
        AmmoPickup.Criar(trancadas[ordem], 0, pos, 0f, true);
    }

    // ponto na metade do caminho andável (pra arma ficar no trajeto do jogador)
    static Vector3 MeioDoCaminho(Vector3 a, Vector3 b)
    {
        var caminho = new UnityEngine.AI.NavMeshPath();
        if (!UnityEngine.AI.NavMesh.SamplePosition(a, out var ha, 5f, UnityEngine.AI.NavMesh.AllAreas) ||
            !UnityEngine.AI.NavMesh.SamplePosition(b, out var hb, 5f, UnityEngine.AI.NavMesh.AllAreas) ||
            !UnityEngine.AI.NavMesh.CalculatePath(ha.position, hb.position, UnityEngine.AI.NavMesh.AllAreas, caminho) || caminho.corners.Length < 2)
            return (a + b) / 2f;
        float total = 0f;
        for (int i = 1; i < caminho.corners.Length; i++) total += Vector3.Distance(caminho.corners[i - 1], caminho.corners[i]);
        float falta = total / 2f;
        for (int i = 1; i < caminho.corners.Length; i++)
        {
            float d = Vector3.Distance(caminho.corners[i - 1], caminho.corners[i]);
            if (falta <= d) return Vector3.Lerp(caminho.corners[i - 1], caminho.corners[i], falta / d);
            falta -= d;
        }
        return caminho.corners[caminho.corners.Length - 1];
    }

    // história

    void ChegouNaEscola()
    {
        chegouEscola = true;
        // a escola tem interior: o desenho está lá dentro, na sala do 4ºB
        if (EscolaInterior.Instance != null) { EscolaInterior.Instance.Chegou(); return; }
        if (desenho == null || desenho.Pego) { LeuDesenho(); return; } // cena sem desenho: segue direto
        RadioSystem.Instance?.Enfileirar("Marcos", "Lia?! ...Ninguém. Só cadeira virada... Espera. Aquele papel no chão.", "marcos_02", true);
        ObjectiveManager.Instance?.Definir("Pegue o desenho da Lia (chegue perto e aperte E)", desenho.transform, false);
        HUD.Tutorial("Chegue perto do PAPEL BRILHANDO no chão e aperte E");
    }

    // chamado pelo DesenhoLia: mostra o desenho na tela e congela o jogo
    public void AbrirDesenho()
    {
        if (LendoDesenho || FaseAtual != Fase.Dia1 || !JogoAtivo) return;
        chegouEscola = true;
        LendoDesenho = true;
        quadroDesenho = Time.frameCount;
        Time.timeScale = 0f;
    }

    public void FecharDesenho()
    {
        if (!LendoDesenho) return;
        LendoDesenho = false;
        Time.timeScale = 1f;
        QuadroTravou = Time.frameCount; // o clique que fechou não vira tiro
        LeuDesenho();
    }

    // desenho lido: a Base Ômega avisa da clínica, o sol se põe e começa a Noite 1.
    // lido dentro da escola -> primeiro a fuga (EscolaInterior); o pôr do sol vem quando ele sai
    void LeuDesenho()
    {
        if (anoitecendo) return;
        var esc = EscolaInterior.Instance;
        if (esc != null && Interior.Atual != null && Interior.Atual == esc.interior)
        {
            esc.ComecarFuga();
            return;
        }
        ComecarAnoitecer(true);
    }

    // saiu da escola fugindo da horda, com o desenho
    public void FugiuDaEscola() => ComecarAnoitecer(false);

    void ComecarAnoitecer(bool falaDoDesenho)
    {
        if (anoitecendo) return;
        anoitecendo = true;
        var radio = RadioSystem.Instance;
        if (falaDoDesenho) radio?.Enfileirar("Marcos", "\"Fomos pro posto do exército\"... Ela tá viva. Ela tá viva!", "marcos_03", true);
        radio?.Enfileirar("Base Ômega", "Sobrevivente, as crianças da escola foram levadas pra Clínica Central. Está escurecendo. Aguente até o amanhecer.", "omega_02");
        ObjectiveManager.Instance?.Definir("Está escurecendo... prepare-se", null, false);
        HUD.LimparTutoriais();
        StartCoroutine(Anoitecer());
    }

    // o céu vai ficando laranja enquanto a Base Ômega fala; depois escurece de vez
    IEnumerator Anoitecer()
    {
        var dn = DayNightController.Instance;
        float t = 0f;
        while (!Acabou && (t < 8f || (RadioSystem.Instance != null && RadioSystem.Instance.Ocupado && t < 18f)))
        {
            t += Time.deltaTime;
            if (dn != null) dn.AplicarEntardecer(Mathf.Clamp01(t / 8f));
            yield return null;
        }
        if (Acabou) yield break;
        yield return Transicao(() =>
        {
            RadioSystem.Instance?.LimparFila(); // fala que ficou pra trás não toca fora de hora
            IniciarFase(Fase.Noite1);
        });
    }

    void Amanheceu()
    {
        StartCoroutine(Transicao(() =>
        {
            WaveSpawner.Instance?.LimparTodos();
            RadioSystem.Instance?.LimparFila();
            IniciarFase(Fase.Dia2);
        }));
    }

    // achou a Lia: cena do reencontro (com falas) e depois a noite final.
    // comCena = false (checkpoint): já começa na noite final
    void AchouLia(bool comCena)
    {
        achouLia = true;
        if (comCena)
        {
            StartCoroutine(CenaReencontro());
            return;
        }
        if (lia != null)
        {
            lia.SetActive(true);
            var npc = lia.GetComponent<LiaNPC>();
            // checkpoint: a Lia aparece do lado do pai (ela "mora" escondida lá dentro da clínica)
            if (npc != null && jogador != null) npc.Posicionar(jogador.position - jogador.forward * 1.4f + jogador.right * 0.7f, jogador.rotation);
            if (npc != null) npc.Encontrar();
        }
        IniciarFase(Fase.NoiteFinal);
    }

    // o Marcos empurrou o armário do depósito e achou a Lia
    public void AchouLiaNaClinica()
    {
        if (achouLia || FaseAtual != Fase.Dia2) return;
        AchouLia(true);
    }

    // interiores (escola, clínica)

    // porta da rua: escurece, teleporta pra dentro e troca a luz
    public void EntrarInterior(Interior i)
    {
        if (i == null || !JogoAtivo || Interior.Atual != null) return;
        StartCoroutine(Transicao(() => EntrarAgora(i), 0.45f));
    }

    // porta de dentro: volta pra calçada
    public void SairInterior()
    {
        if (Interior.Atual == null || !JogoAtivo) return;
        StartCoroutine(Transicao(() => SairAgora(false), 0.45f));
    }

    void EntrarAgora(Interior i)
    {
        if (Acabou) return;
        if (i.entrada != null && jogador != null)
        {
            TeleportarJogador(i.entrada.position);
            jogador.rotation = Quaternion.Euler(0f, i.entrada.eulerAngles.y, 0f);
            camJogo?.OlharPara(i.entrada.eulerAngles.y);
        }
        WaveSpawner.Instance?.LimparTodos(); // os de fora ficam pra trás
        DayNightController.Instance?.EntrarInterior(i);
        var som = SomManager.Instance;
        if (som != null) { som.MultMusica = i.volumeMusica; som.Ambiente(true); }
        if (EscolaInterior.Instance != null && i == EscolaInterior.Instance.interior) chegouEscola = true;
        if (ClinicaInterior.Instance != null && i == ClinicaInterior.Instance.interior) chegouClinica = true;
        i.Entrou();
    }

    // comLia: saindo da clínica depois do reencontro (ela vem junto)
    void SairAgora(bool comLia)
    {
        var i = Interior.Atual;
        if (i == null) return;
        if (i.saida != null && jogador != null)
        {
            TeleportarJogador(i.saida.position);
            jogador.rotation = Quaternion.Euler(0f, i.saida.eulerAngles.y, 0f);
            camJogo?.OlharPara(i.saida.eulerAngles.y);
        }
        if (comLia && lia != null && jogador != null)
        {
            var npc = lia.GetComponent<LiaNPC>();
            Vector3 p = jogador.position - jogador.forward * 1.4f + jogador.right * 0.7f;
            if (npc != null) npc.Posicionar(p, jogador.rotation);
            else lia.transform.position = p;
        }
        var dn = DayNightController.Instance;
        dn?.SairInterior();
        var som = SomManager.Instance;
        if (som != null) { som.MultMusica = 1f; som.Ambiente(dn != null && dn.Noite); }
        WaveSpawner.Instance?.Respiro(5f);
        i.Saiu();
    }

    IEnumerator CenaReencontro()
    {
        ComecarCena();
        RadioSystem.Instance?.Silenciar(); // nenhuma fala atrasada atrapalha o reencontro
        var spawner = WaveSpawner.Instance;
        spawner?.Configurar(null);
        spawner?.LimparTodos(); // a câmera corta pros dois: ninguém vê os zumbis sumindo
        ObjectiveManager.Instance?.Definir("", null, false);
        if (farolClinica != null) farolClinica.SetActive(false);
        SomManager.Instance?.TocarMusica(null); // momento quieto: só o vento e as vozes

        Vector3 pLia = jogador != null ? jogador.position + jogador.forward * 2f : Vector3.zero;
        if (lia != null)
        {
            lia.SetActive(true);
            var npc = lia.GetComponent<LiaNPC>();
            if (npc != null) npc.Encontrar();
            pLia = lia.transform.position;
        }
        if (jogador != null)
        {
            // o Marcos fica de frente pra ela, a uns 2 m
            Vector3 entre = pLia - jogador.position;
            entre.y = 0f;
            // dentro da clínica a Lia sai do nicho e os dois ficam no meio do depósito (a câmera cabe)
            if (Interior.Atual != null && lia != null && entre.sqrMagnitude > 0.01f)
            {
                // zumbis do interior não aparecem no meio da cena
                foreach (var z in Interior.Atual.GetComponentsInChildren<ZombieAI>()) z.gameObject.SetActive(false);
                Vector3 praPai = -entre.normalized;
                var npcL = lia.GetComponent<LiaNPC>();
                if (npcL != null) npcL.Posicionar(pLia + praPai * 1.5f, Quaternion.LookRotation(praPai));
                pLia = lia.transform.position;
                if (UnityEngine.AI.NavMesh.SamplePosition(pLia + praPai * 1.7f, out var hm, 1f, UnityEngine.AI.NavMesh.AllAreas)) TeleportarJogador(hm.position);
                Physics.SyncTransforms();
                entre = pLia - jogador.position;
                entre.y = 0f;
            }
            if (entre.magnitude > 3f)
            {
                Vector3 p = pLia - entre.normalized * 2.2f;
                if (UnityEngine.AI.NavMesh.SamplePosition(p, out var h, 2f, UnityEngine.AI.NavMesh.AllAreas)) TeleportarJogador(h.position);
                entre = pLia - jogador.position;
                entre.y = 0f;
            }
            if (entre.sqrMagnitude > 0.01f)
            {
                jogador.rotation = Quaternion.LookRotation(entre);
                Vector3 lado = Vector3.Cross(Vector3.up, entre.normalized);
                Vector3 meio = (jogador.position + pLia) * 0.5f + Vector3.up * 1.05f;
                float yawCena = Quaternion.LookRotation(lado).eulerAngles.y - 20f;
                if (Interior.Atual != null) yawCena = MelhorYawCena(meio, yawCena, jogador.position, pLia, 3.4f);
                camJogo?.IniciarCena(meio, yawCena, 9f, 3.4f, Interior.Atual != null ? 1.2f : 3f);
            }
        }

        var radio = RadioSystem.Instance;
        radio?.Enfileirar("Lia", "Pai! Eu sabia que você vinha!", "lia_06", true);
        radio?.Enfileirar("Marcos", "Lia... graças a Deus. Deixa eu te ver. Você tá machucada?", "marcos_07", true);
        radio?.Enfileirar("Lia", "Me morderam, pai... mas parou de doer. Eu não virei monstro.", "lia_07", true);
        radio?.Enfileirar("Marcos", "Igual a mim... Você é igual a mim, filha. Fica atrás de mim e não solta.", "marcos_08", true);
        yield return EsperarRadio(30f);
        if (pular) radio?.Silenciar();
        pular = false;
        yield return Transicao(() =>
        {
            EmCena = false;
            // o reencontro foi dentro da clínica -> os dois saem pra rua (a noite final é lá fora)
            if (Interior.Atual != null) SairAgora(true);
            camJogo?.FimCena();
            IniciarFase(Fase.NoiteFinal);
        }, 1f);
    }

    // o tempo acabou: o helicóptero desce, pai e filha se olham, a tela escurece e o rádio confirma o resgate
    IEnumerator CenaFinal()
    {
        ComecarCena();
        tempoFinal = TempoDeJogo;
        WaveSpawner.Instance?.Configurar(null);
        WaveSpawner.Instance?.LimparTodos();
        ObjectiveManager.Instance?.Definir("", null, false);
        HUD.LimparTutoriais();
        var som = SomManager.Instance;
        if (som != null) som.TocarMusica(som.musicaVitoria != null ? som.musicaVitoria : som.musicaMenu);
        if (heli == null && clinica != null)
        {
            heli = Helicoptero.Criar(clinica.position, som != null ? som.helicoptero : null, prefabHelicoptero);
            heli.PularVoo();
        }

        if (jogador != null)
        {
            // o helicóptero desce no espaço aberto mais perto da clínica; pai e filha olham pra ele
            Vector3 baseCena = clinica != null ? clinica.position : jogador.position;
            Vector3 pairar = PontoAbertoNoAr(baseCena);
            Vector3 praHeli = pairar - baseCena;
            praHeli.y = 0f;
            praHeli = praHeli.sqrMagnitude > 1f ? praHeli.normalized : jogador.forward;
            if (UnityEngine.AI.NavMesh.SamplePosition(baseCena + praHeli * 1.5f, out var hp, 4f, UnityEngine.AI.NavMesh.AllAreas)) TeleportarJogador(hp.position);
            jogador.rotation = Quaternion.LookRotation(praHeli);
            if (lia != null) lia.GetComponent<LiaNPC>()?.FicarAoLado(jogador);
            if (heli != null) heli.Descer(pairar, jogador);
            // câmera baixa atrás ou ao lado dos dois, num lugar livre e com visão deles
            Vector3 posCam = PosicaoCameraFinal(jogador.position, praHeli, Vector3.Lerp(jogador.position + Vector3.up * 1.6f, pairar, 0.45f));
            // enquadramento (lente de 70°): os dois logo acima da legenda e o helicóptero inteiro abaixo da barra preta de cima.
            // Parte visível = de 17,5° abaixo a 27° acima do centro (barras pretas + legenda tapam o resto);
            // o helicóptero ocupa uns 9° pra cada lado do centro dele
            Vector3 aoPeito = jogador.position + Vector3.up * 1.1f - posCam;
            Vector3 aoHeli = pairar - posCam;
            float Elev(Vector3 v) => Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg;
            float centro = ((Elev(aoPeito) + 15.5f) + (Elev(aoHeli) - 18f)) * 0.5f;
            Vector3 horiz = new Vector3(aoPeito.x, 0f, aoPeito.z).normalized + new Vector3(aoHeli.x, 0f, aoHeli.z).normalized * 0.8f;
            horiz = horiz.sqrMagnitude > 0.001f ? horiz.normalized : jogador.forward;
            Vector3 eixo = Vector3.Cross(Vector3.up, horiz);
            Vector3 olhar = posCam + Quaternion.AngleAxis(-centro, eixo) * horiz * 6f;
            camJogo?.IniciarCenaFixa(posCam, olhar, 0.1f, 70f);
        }

        var radio = RadioSystem.Instance;
        radio?.Silenciar();
        // o dia começa a nascer enquanto eles conversam (fim da última noite)
        StartCoroutine(Aurora(8f));
        yield return EsperarSeg(1.2f);
        radio?.Enfileirar("Lia", "Pai... a gente vai ficar bem?", "lia_09", true);
        radio?.Enfileirar("Marcos", "Agora vai, filha. Agora vai.", "marcos_11", true);
        yield return EsperarRadio(15f);
        if (!pular)
        {
            yield return Escurecer(1.8f);
            radio?.Enfileirar("Resgate Um", "Base Ômega, aqui é o Resgate Um. Dois imunes a bordo. Repito: dois imunes a bordo. Ainda tem esperança.", "omega_06");
            yield return EsperarRadio(14f);
        }
        radio?.Silenciar();
        Escuro = 1f;
        EmCena = false;
        Vencer();
    }

    // céu da noite clareando (horizonte laranja, lua sumindo) durante a cena final
    IEnumerator Aurora(float duracao)
    {
        var dn = DayNightController.Instance;
        if (dn == null) yield break;
        for (float t = 0f; t < duracao && FaseAtual != Fase.GameOver; t += Time.deltaTime)
        {
            dn.AplicarAurora(0.8f * Mathf.Clamp01(t / duracao));
            yield return null;
        }
    }

    // ponto no ar (8 m) mais perto da clínica onde o helicóptero cabe: esfera de 5,5 m sem bater em prédio/poste.
    // (o marcador da clínica fica na quina de um prédio: "pra frente" o helicóptero entrava no prédio)
    static Vector3 PontoAbertoNoAr(Vector3 p)
    {
        for (float r = 5f; r <= 26f; r += 3f)
            for (int k = 0; k < 16; k++)
            {
                Vector3 ar = p + Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward * r + Vector3.up * 6.5f;
                if (!Physics.CheckSphere(ar, 4.8f, ~0, QueryTriggerInteraction.Ignore)) return ar;
            }
        return p + Vector3.up * 13f; // apertado demais: fica bem alto em cima da clínica
    }

    // câmera ~7 m atrás do Marcos (do lado oposto ao helicóptero), a 1,3 m do chão; testa ângulos até achar
    // um lugar com folga (nada de beco) e com visão livre até o ponto que ela olha e até os dois
    Vector3 PosicaoCameraFinal(Vector3 pai, Vector3 praHeli, Vector3 olhar)
    {
        Vector3 cabeca = pai + Vector3.up * 1.5f;
        Vector3 melhor = pai - praHeli * 4f + Vector3.up * 2f; // reserva
        float melhorNota = float.MinValue;
        for (int k = 0; k < 24; k++)
        {
            Vector3 dir = Quaternion.Euler(0f, k * 15f, 0f) * Vector3.forward;
            float atras = Vector3.Dot(dir, -praHeli); // 1 = atrás dos dois, 0 = de lado, -1 = entre eles e o helicóptero
            if (atras < -0.3f) continue;
            foreach (float dist in new[] { 8.5f, 7f, 5.5f })
            {
                Vector3 pos = pai + dir * dist + Vector3.up * 1.4f;
                if (Physics.CheckSphere(pos, 0.9f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Bloqueado(cabeca, pos, 0.25f) || Bloqueado(olhar, pos, 0.32f)) continue;
                float nota = atras * 2f + dist * 0.35f;
                if (nota > melhorNota) { melhorNota = nota; melhor = pos; }
                break; // a maior distância livre nessa direção basta
            }
        }
        return melhor;
    }

    // cena em lugar apertado: testa 24 ângulos e fica com o de mais espaço em que dá pra ver o pai e a filha
    float MelhorYawCena(Vector3 meio, float preferido, Vector3 a, Vector3 b, float dist)
    {
        float melhor = preferido, melhorNota = float.MinValue;
        Vector3 eixo = b - a;
        eixo.y = 0f;
        for (int k = 0; k < 24; k++)
        {
            float yaw = preferido + k * 15f;
            Vector3 tras = -(Quaternion.Euler(9f, yaw, 0f) * Vector3.forward);
            float livre = dist;
            foreach (var h in Physics.SphereCastAll(meio, 0.25f, tras, dist, ~0, QueryTriggerInteraction.Ignore))
                if (jogador == null || !h.transform.IsChildOf(jogador)) livre = Mathf.Min(livre, h.distance);
            Vector3 pos = meio + tras * Mathf.Max(0.8f, livre - 0.1f);
            // de lado (90°) mostra os dois; na linha deles um tampa o outro
            float deLado = Vector3.Angle(new Vector3(tras.x, 0f, tras.z), eixo);
            deLado = 1f - Mathf.Abs(deLado - 90f) / 90f;
            float nota = livre * 1.2f + deLado * 2.2f - Mathf.Abs(Mathf.DeltaAngle(yaw, preferido)) / 180f * 0.3f;
            if (Bloqueado(a + Vector3.up * 1.4f, pos, 0.1f)) nota -= 3f;
            if (Bloqueado(b + Vector3.up * 0.9f, pos, 0.1f)) nota -= 3f;
            if (nota > melhorNota) { melhorNota = nota; melhor = yaw; }
        }
        return melhor;
    }

    // tem algo (fora o Marcos) entre os dois pontos? (esfera do raio dado)
    bool Bloqueado(Vector3 de, Vector3 ate, float raio)
    {
        Vector3 d = ate - de;
        float dist = d.magnitude;
        if (dist < 0.01f) return false;
        foreach (var h in Physics.SphereCastAll(de, raio, d / dist, dist, ~0, QueryTriggerInteraction.Ignore))
            if (jogador == null || !h.transform.IsChildOf(jogador)) return true;
        return false;
    }

    void Vencer()
    {
        WaveSpawner.Instance?.Configurar(null);
        WaveSpawner.Instance?.LimparTodos();
        MotivoFim = "Marcos e Lia embarcaram no helicóptero da Base Ômega.\nOs dois são imunes - e podem ser a chave da vacina.";
        if (tempoFinal <= 0f) tempoFinal = TempoDeJogo;
        FaseAtual = Fase.Vitoria;
        InicioFim = Time.unscaledTime;
        checkpoint = null;
        TravarCursor(false);
        var som = SomManager.Instance;
        if (som != null) { som.Vinheta(som.somVitoria); som.TocarMusica(som.musicaVitoria != null ? som.musicaVitoria : som.musicaMenu); }
    }

    public void GameOver(string motivo)
    {
        if (Acabou) return;
        // para o que estiver acontecendo (anoitecer, transição)
        StopAllCoroutines();
        EmCena = EmTransicao = false;
        Escuro = 0f;
        Cartela = "";
        CartelaAlfa = 0f;
        MotivoFim = motivo;
        tempoFinal = TempoDeJogo;
        FaseAtual = Fase.GameOver;
        InicioFim = Time.unscaledTime;
        RadioSystem.Instance?.Silenciar(); // a legenda não fica por baixo do painel de Game Over
        WaveSpawner.Instance?.Configurar(null);
        TravarCursor(false);
        var som = SomManager.Instance;
        if (som != null) { som.TocarMusica(som.musicaGameOver); som.Vinheta(som.somGameOver); }
    }

    public void AlternarPausa()
    {
        if (NoMenu || EmCena || EmTransicao || Acabou) return;
        Pausado = !Pausado;
        Time.timeScale = Pausado ? 0f : 1f;
        TravarCursor(!Pausado);
    }

    // morreu: volta pro começo da fase em que estava
    public void TentarDeNovo()
    {
        usarCheckpoint = checkpoint != null;
        pularMenu = checkpoint == null;
        Recarregar();
    }

    // do zero (Dia 1), sem passar pelo menu
    public void Recomecar()
    {
        checkpoint = null;
        pularMenu = true;
        Recarregar();
    }

    public void VoltarAoMenu()
    {
        checkpoint = null;
        pularMenu = false;
        Recarregar();
    }

    void Recarregar()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Sair()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // quadro em que o mouse foi travado (o clique que trava não pode virar tiro)
    public static int QuadroTravou { get; private set; } = -10;

    void TravarCursor(bool travar)
    {
        // normal: travado no centro; Área de Trabalho Remota: preso na janela e o MouseJogo devolve pro meio
        if (travar && !MouseJogo.Preso) { QuadroTravou = Time.frameCount; MouseJogo.Reiniciar(); }
        MouseJogo.QuerPreso = travar;
        Cursor.lockState = travar ? MouseJogo.ModoPreso : CursorLockMode.None;
        Cursor.visible = !travar;
    }

    // voltou pra janela do jogo (Alt+Tab, clicou no Game do Editor): trava o mouse de novo
    void OnApplicationFocus(bool focado)
    {
        if (focado && (JogoAtivo || EmCena || EmTransicao)) TravarCursor(true);
        else if (!focado) MouseJogo.QuerPreso = false; // Área de Trabalho Remota: ao voltar prende de novo (e o clique da volta não atira)
    }

    // itens no mapa

    // dá pra ir andando do Marcos até esse ponto? (sem isso 1 em cada 4 itens nascia preso entre prédio e muro)
    bool Alcancavel(Vector3 p)
    {
        if (jogador == null) return true;
        var c = new UnityEngine.AI.NavMeshPath();
        return UnityEngine.AI.NavMesh.SamplePosition(jogador.position, out var h, 3f, UnityEngine.AI.NavMesh.AllAreas)
               && UnityEngine.AI.NavMesh.CalculatePath(h.position, p, UnityEngine.AI.NavMesh.AllAreas, c)
               && c.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
    }

    // ponto sorteado na cidade, no chão e alcançável
    bool PontoNaCidade(out Vector3 ponto)
    {
        ponto = Vector3.zero;
        for (int t = 0; t < 12; t++)
        {
            Vector3 p = origemCidade + new Vector3(Random.Range(0f, limitesCidade.x), 0f, Random.Range(0f, limitesCidade.y));
            if (!UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 6f, UnityEngine.AI.NavMesh.AllAreas)) continue;
            if (hit.position.y > origemCidade.y + 1.5f || !Alcancavel(hit.position)) continue;
            ponto = hit.position;
            return true;
        }
        return false;
    }

    // caixas de munição: só de armas que gastam munição (caixa de pistola não dava pra pegar)
    void EspalharMunicao()
    {
        var pw = PlayerWeapons.Instance;
        if (pw == null || pw.armas == null) return;
        var comMunicao = System.Array.FindAll(pw.armas, a => a != null && !a.municaoInfinita);
        if (comMunicao.Length == 0) return;
        for (int i = 0; i < caixasMunicaoIniciais; i++)
        {
            if (!PontoNaCidade(out var p)) continue;
            WeaponData arma = comMunicao[Random.Range(0, comMunicao.Length)];
            AmmoPickup.Criar(arma, arma.tamanhoPente, p, 0f);
        }
    }

    // comida e kits médicos: espalhados pela cidade + garantidos na escola (Noite 1) e na clínica (noite final)
    void EspalharItens()
    {
        for (int i = 0; i < comidasIniciais + kitsIniciais; i++)
        {
            if (!PontoNaCidade(out var p)) continue;
            ItemPickup.Criar(i < comidasIniciais ? TipoItem.Comida : TipoItem.KitMedico, p);
        }
        ColocarItem(TipoItem.Comida, escola, 5f);
        ColocarItem(TipoItem.KitMedico, escola, 6f);
        ColocarItem(TipoItem.Comida, clinica, 5f);
        ColocarItem(TipoItem.Comida, clinica, 7f);
        ColocarItem(TipoItem.KitMedico, clinica, 6f);
    }

    void ColocarItem(TipoItem tipo, Transform perto, float raio)
    {
        if (perto == null) return;
        for (int t = 0; t < 16; t++)
        {
            Vector3 p = perto.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * raio;
            if (!UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)
                || hit.position.y >= origemCidade.y + 1.5f || !Alcancavel(hit.position)) continue;
            ItemPickup.Criar(tipo, hit.position);
            return;
        }
    }
}
