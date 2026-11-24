using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// HUD do jogo, tudo criado por código. Menus (principal, pausa, opções, créditos, fim) ficam em HUDMenus.cs.
// Camadas (de baixo pra cima): HUD de jogo -> cinema (barras pretas, tela preta, legenda, título, texto da abertura) -> menus.
public partial class HUD : MonoBehaviour
{
    static HUD instance;

    Font fonte;
    Transform canvasT;
    GameObject grupoJogo, grupoCinema;
    Text txtObjetivo, txtAlvo, txtFase, txtTimer, txtVida, txtArma, txtMunicao, txtRadio, txtAviso, txtMira, txtAbates;
    RectTransform barraVida, fundoObjetivo;
    Image flashDano, fundoRadio, fundoSinal;
    Text txtRadioQuem, txtSinalTitulo, txtSinalDica;
    Image[] barrasSinal;
    int barrasAntes = -1;
    float flashSinal, flashGolpe;
    string legendaAntes, quemAntes;
    int estadoQuem = -1;
    GameObject telaCarregando;
    float fimAviso, vidaAnterior, proximoTexto;

    Text txtAcerto, txtCabeca, txtClique;
    float fimAcerto, fimCabeca;
    public static float UltimoAcerto { get; private set; } = -99f;

    // fome, kits, Lia, interação
    RectTransform barraFome, barraLia;
    Text txtFome, txtKits, txtLia, txtDica;
    Image fundoLia, fundoBarraLia;
    float fimDica;

    // título da fase, dicas de tutorial e seta do objetivo
    Text txtTitulo, txtSubtitulo, txtTutorial, txtMarcador;
    Image fundoTutorial;
    static readonly Queue<string> filaTutorial = new Queue<string>();
    float fimTutorial = -1f;
    bool dicaFome, dicaVida, dicaMunicao;

    // cinema: barras pretas, tela preta (fade), texto da abertura, "ENTER: pular"
    RectTransform barraCima, barraBaixo;
    Image escuro;
    Text txtCartela, txtPular;
    float barras;

    // mira dinâmica: abre correndo/atirando, fica vermelha em cima de zumbi
    Image[] miraBarras;
    Image miraPonto;
    float aberturaMira = 12f, tiroRecente;
    PlayerController controle;
    readonly RaycastHit[] hitsMira = new RaycastHit[12];

    // marcador de acerto na mira (vermelho e maior quando mata)
    public static void Acerto(bool matou, bool naCabeca = false)
    {
        UltimoAcerto = Time.time;
        if (instance == null) return;
        instance.fimAcerto = Time.unscaledTime + (matou ? 0.3f : 0.12f);
        instance.txtAcerto.color = matou ? new Color(1f, 0.2f, 0.15f) : Color.white;
        instance.txtAcerto.fontSize = matou ? 46 : 34;
        if (naCabeca)
        {
            instance.txtCabeca.text = matou ? "NA CABEÇA!" : "na cabeça";
            instance.txtCabeca.fontSize = matou ? 34 : 24;
            instance.fimCabeca = Time.unscaledTime + 0.7f;
        }
    }

    // tiro: a mira abre um pouco (mais com arma de coice forte)
    public static void Disparou(float recuo)
    {
        if (instance != null) instance.tiroRecente = Mathf.Min(1.6f, instance.tiroRecente + 0.35f + recuo * 0.12f);
    }

    public static void Aviso(string msg)
    {
        if (instance == null) return;
        instance.txtAviso.text = msg;
        instance.fimAviso = Time.unscaledTime + 2.5f;
    }

    // texto de interação no centro da tela (quem chama precisa chamar todo quadro)
    public static void Dica(string msg)
    {
        if (instance == null) return;
        instance.txtDica.text = msg;
        instance.fimDica = Time.unscaledTime + 0.15f;
    }

    // dica de tutorial na caixa amarela do topo (uma de cada vez, ~5 s cada)
    public static void Tutorial(string msg) => filaTutorial.Enqueue(msg);

    // fase nova: as dicas antigas que ainda não apareceram (ou estão na tela) saem
    public static void LimparTutoriais()
    {
        filaTutorial.Clear();
        if (instance != null) instance.fimTutorial = -1f;
    }

    void Awake()
    {
        instance = this;
        filaTutorial.Clear();
        fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Montar();
    }

    void Montar()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        var canvasGo = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasT = canvasGo.transform;

        // tudo do jogo fica num grupo (escondido no menu principal e nas cenas)
        grupoJogo = CriarImagem(canvasT, "Jogo", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var raiz = grupoJogo.transform;

        flashDano = CriarImagem(raiz, "FlashDano", new Color(0.8f, 0f, 0f, 0f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // canto superior esquerdo: objetivo (com fundo escuro pra ler em cima do céu claro)
        var fundoObj = CriarImagem(raiz, "ObjetivoFundo", new Color(0f, 0f, 0f, 0.42f), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero);
        fundoObjetivo = fundoObj.rectTransform;
        fundoObjetivo.pivot = new Vector2(0, 1);
        fundoObjetivo.anchoredPosition = new Vector2(24, -18);
        fundoObjetivo.sizeDelta = new Vector2(600, 96);
        CriarImagem(fundoObj.transform, "Faixa", new Color(1f, 0.78f, 0.25f, 0.9f), Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(5, 0));
        txtObjetivo = CriarTexto(raiz, "Objetivo", 29, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(42, -28), new Vector2(860, 45));
        txtObjetivo.horizontalOverflow = HorizontalWrapMode.Overflow;
        txtAlvo = CriarTexto(raiz, "Alvo", 23, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(42, -70), new Vector2(860, 40));
        txtAlvo.horizontalOverflow = HorizontalWrapMode.Overflow;
        txtAlvo.color = new Color(0.8f, 0.9f, 1f);
        var lembrete = CriarTexto(raiz, "Lembrete", 18, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(30, -122), new Vector2(700, 30));
        lembrete.text = "ESC: pausa e controles";
        lembrete.color = new Color(1f, 1f, 1f, 0.5f);

        // topo: fase e timer
        txtFase = CriarTexto(raiz, "Fase", 28, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-40, -30), new Vector2(500, 40));
        txtAbates = CriarTexto(raiz, "Abates", 22, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-40, -70), new Vector2(500, 35));
        txtTimer = CriarTexto(raiz, "Timer", 40, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(800, 55));
        txtTimer.color = new Color(1f, 0.85f, 0.4f);

        // vida da Lia (noite final), embaixo do timer
        fundoBarraLia = CriarImagem(raiz, "LiaFundo", new Color(0, 0, 0, 0.6f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-200, -122), new Vector2(200, -98));
        fundoLia = CriarImagem(raiz, "LiaBarra", new Color(1f, 0.45f, 0.7f, 0.95f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-196, -118), new Vector2(196, -102));
        barraLia = fundoLia.rectTransform;
        txtLia = CriarTexto(raiz, "Lia", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -97), new Vector2(400, 26));

        // dicas de tutorial (caixa amarela no topo, abaixo do painel do sinal do rádio)
        fundoTutorial = CriarImagem(raiz, "TutorialFundo", new Color(0, 0, 0, 0.7f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-600, -226), new Vector2(600, -148));
        txtTutorial = CriarTexto(raiz, "Tutorial", 27, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1160, 74));
        txtTutorial.color = new Color(1f, 0.88f, 0.35f);

        // seta do objetivo (fica em cima do lugar; na borda da tela quando está fora da visão)
        txtMarcador = CriarTexto(raiz, "Marcador", 30, TextAnchor.MiddleCenter, new Vector2(0, 0), Vector2.zero, new Vector2(160, 90));
        txtMarcador.color = new Color(1f, 0.85f, 0.25f);

        // mira: 4 tracinhos + ponto (a sniper mirando usa só o ponto "•")
        miraBarras = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            miraBarras[i] = CriarImagem(raiz, "MiraBarra" + i, new Color(1f, 1f, 1f, 0.9f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var rt = miraBarras[i].rectTransform;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = i < 2 ? new Vector2(3, 12) : new Vector2(12, 3);
            miraBarras[i].gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.5f);
        }
        miraPonto = CriarImagem(raiz, "MiraPonto", new Color(1f, 1f, 1f, 0.9f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        miraPonto.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        miraPonto.rectTransform.sizeDelta = new Vector2(3, 3);
        txtMira = CriarTexto(raiz, "Mira", 36, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
        txtMira.text = "•";
        txtAcerto = CriarTexto(raiz, "Acerto", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 80));
        txtAcerto.text = "X";
        txtAcerto.enabled = false;
        txtCabeca = CriarTexto(raiz, "Cabeca", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(500, 50));
        txtCabeca.color = new Color(1f, 0.75f, 0.2f);
        txtCabeca.enabled = false;
        txtClique = CriarTexto(raiz, "Clique", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(900, 60));
        txtClique.text = "Clique na tela para jogar";
        txtClique.color = new Color(1f, 0.9f, 0.5f);
        txtClique.enabled = false;
        txtDica = CriarTexto(raiz, "Dica", 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(900, 50));
        txtDica.color = new Color(1f, 0.95f, 0.6f);
        txtDica.enabled = false;

        // vida, fome e kits (canto inferior esquerdo)
        CriarImagem(raiz, "VidaFundo", new Color(0, 0, 0, 0.6f), Vector2.zero, Vector2.zero, new Vector2(40, 40), new Vector2(440, 70));
        barraVida = CriarImagem(raiz, "VidaBarra", new Color(0.8f, 0.15f, 0.15f, 0.9f), Vector2.zero, Vector2.zero, new Vector2(45, 45), new Vector2(435, 65)).rectTransform;
        txtVida = CriarTexto(raiz, "Vida", 22, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(60, 55), new Vector2(380, 30));
        CriarImagem(raiz, "FomeFundo", new Color(0, 0, 0, 0.6f), Vector2.zero, Vector2.zero, new Vector2(40, 78), new Vector2(440, 104));
        barraFome = CriarImagem(raiz, "FomeBarra", new Color(0.95f, 0.6f, 0.15f, 0.9f), Vector2.zero, Vector2.zero, new Vector2(45, 82), new Vector2(435, 100)).rectTransform;
        txtFome = CriarTexto(raiz, "Fome", 18, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(60, 79), new Vector2(380, 24));
        txtKits = CriarTexto(raiz, "Kits", 22, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(42, 112), new Vector2(500, 30));

        // arma / munição
        txtArma = CriarTexto(raiz, "Arma", 30, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-40, 95), new Vector2(500, 40));
        txtMunicao = CriarTexto(raiz, "Municao", 44, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-40, 40), new Vector2(500, 55));
        txtAviso = CriarTexto(raiz, "Aviso", 24, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-40, 145), new Vector2(800, 35));
        txtAviso.color = new Color(0.6f, 1f, 0.6f);

        // sinal do rádio da Lia (Dia 2): painel grande no topo com 5 barras
        var fundoSinalImg = CriarImagem(raiz, "SinalFundo", new Color(0, 0, 0, 0.62f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-340, -232), new Vector2(340, -12));
        fundoSinal = fundoSinalImg;
        var ps = fundoSinalImg.transform;
        txtSinalTitulo = CriarTexto(ps, "SinalTitulo", 34, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(660, 46));
        txtSinalTitulo.color = new Color(1f, 0.85f, 0.35f);
        barrasSinal = new Image[5];
        for (int i = 0; i < barrasSinal.Length; i++)
        {
            float x = -182f + i * 76f, h = 24f + i * 21f;
            barrasSinal[i] = CriarImagem(ps, "Barra" + i, new Color(1, 1, 1, 0.12f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(x, 50), new Vector2(x + 60, 50 + h));
        }
        txtSinalDica = CriarTexto(ps, "SinalDica", 26, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(660, 36));
        fundoSinal.gameObject.SetActive(false);

        MontarCinema();
        MontarMenus();

        // tela preta do aquecimento (fica por cima de tudo)
        telaCarregando = CriarPainel(canvasT, "Carregando", out var tCarr, out _);
        telaCarregando.GetComponent<Image>().color = Color.black;
        tCarr.text = "Carregando...";
    }

    // camada de cinema: fica por cima do HUD de jogo (a legenda aparece até com a tela preta)
    void MontarCinema()
    {
        grupoCinema = CriarImagem(canvasT, "Cinema", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        var c = grupoCinema.transform;
        barraCima = CriarImagem(c, "BarraCima", Color.black, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero).rectTransform;
        barraCima.pivot = new Vector2(0.5f, 1f);
        barraBaixo = CriarImagem(c, "BarraBaixo", Color.black, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, Vector2.zero).rectTransform;
        barraBaixo.pivot = new Vector2(0.5f, 0f);
        escuro = CriarImagem(c, "Escuro", new Color(0f, 0f, 0f, 0f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // legenda das falas (embaixo, no centro): quem fala em cima, texto grande embaixo
        fundoRadio = CriarImagem(c, "RadioFundo", new Color(0, 0, 0, 0.62f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-640, 128), new Vector2(640, 268));
        txtRadioQuem = CriarTexto(c, "RadioQuem", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 228), new Vector2(1240, 34));
        txtRadio = CriarTexto(c, "Radio", 31, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 134), new Vector2(1240, 94));
        txtRadio.horizontalOverflow = HorizontalWrapMode.Wrap;
        txtRadio.lineSpacing = 1.05f;

        // título grande da fase (aparece também em cima da tela preta)
        txtTitulo = CriarTexto(c, "TituloFase", 96, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1600, 120));
        txtTitulo.GetComponent<Outline>().effectDistance = new Vector2(2.5f, -2.5f);
        txtSubtitulo = CriarTexto(c, "SubtituloFase", 40, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(1600, 60));

        // texto da abertura (história)
        txtCartela = CriarTexto(c, "Cartela", 40, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1500, 600));
        txtCartela.lineSpacing = 1.1f;
        txtCartela.color = new Color(0.95f, 0.93f, 0.88f);
        txtPular = CriarTexto(c, "Pular", 22, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-40, 30), new Vector2(600, 34));
        txtPular.text = "ENTER: pular cena";
        txtPular.color = new Color(1f, 1f, 1f, 0.55f);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        var vida = PlayerHealth.Instance;
        var armas = PlayerWeapons.Instance;
        var obj = ObjectiveManager.Instance;
        var radio = RadioSystem.Instance;

        AtualizarMenus(gm);
        if (gm != null)
        {
            telaCarregando.SetActive(gm.Carregando);
            grupoJogo.SetActive(!gm.NoMenu && !gm.EmCena && !gm.Acabou);
            grupoCinema.SetActive(!gm.NoMenu);
            if (gm.NoMenu) return;
        }
        AtualizarCinema(gm);
        AtualizarLegenda(gm != null && gm.MostrarPainelFim ? null : radio); // sem legenda por baixo do painel de fim
        AtualizarTitulo(gm);
        if (gm != null && (gm.EmCena || gm.Acabou)) return;

        if (vida != null)
        {
            // flash vermelho ao levar dano + pulsação com a vida baixa (abaixo de 35%)
            if (vida.Vida < vidaAnterior) flashGolpe = 0.35f;
            vidaAnterior = vida.Vida;
            flashGolpe = Mathf.MoveTowards(flashGolpe, 0f, Time.deltaTime * 0.8f);
            float baixa = vida.EstaVivo ? Mathf.Clamp01(1f - vida.Vida / (vida.vidaMaxima * 0.35f)) : 0f;
            float pulso = baixa * (0.14f + 0.1f * Mathf.Sin(Time.time * 5.5f));
            flashDano.color = new Color(0.75f, 0f, 0f, Mathf.Max(flashGolpe, pulso));
        }

        txtAcerto.enabled = Time.unscaledTime < fimAcerto;
        txtCabeca.enabled = Time.unscaledTime < fimCabeca;
        // mouse solto durante o jogo (ex.: no Editor antes de clicar no Game): avisa
        txtClique.enabled = gm != null && gm.JogoAtivo && !gm.Carregando && !MouseJogo.Preso;
        txtDica.enabled = Time.unscaledTime < fimDica && gm != null && gm.JogoAtivo;
        AtualizarMira(gm, armas);
        AtualizarTutorial(gm, vida, armas);
        AtualizarMarcador(gm, obj, vida);
        AtualizarSinal(gm, obj, vida);

        // textos 20x por segundo (montar string todo frame gera lixo de memória)
        if (Time.unscaledTime < proximoTexto) return;
        proximoTexto = Time.unscaledTime + 0.05f;

        if (obj != null && vida != null)
        {
            txtObjetivo.text = obj.Texto == "" ? "" : "<color=#ffd34d><b>OBJETIVO</b></color>   " + obj.Texto;
            if (obj.Alvo == null) txtAlvo.text = "";
            else if (obj.ModoSinal) txtAlvo.text = "Siga o SINAL DO RÁDIO (painel no topo da tela)";
            else txtAlvo.text = $"Distância: {obj.Distancia(vida.transform.position):0} m";
            // fundo do tamanho do texto
            float largura = Mathf.Max(txtObjetivo.preferredWidth, txtAlvo.text == "" ? 0f : txtAlvo.preferredWidth);
            fundoObjetivo.gameObject.SetActive(obj.Texto != "");
            fundoObjetivo.sizeDelta = new Vector2(Mathf.Min(880f, largura + 40f), txtAlvo.text == "" ? 56f : 94f);
        }

        if (gm != null)
        {
            switch (gm.FaseAtual)
            {
                case Fase.Dia1: txtFase.text = "DIA 1"; txtTimer.text = ""; break;
                case Fase.Noite1: txtFase.text = "NOITE 1"; txtTimer.text = "Amanhecer em " + Tempo(gm.TempoRestante); break;
                case Fase.Dia2: txtFase.text = "DIA 2"; txtTimer.text = ""; break;
                case Fase.NoiteFinal: txtFase.text = "NOITE FINAL"; txtTimer.text = "Helicóptero em " + Tempo(gm.TempoRestante); break;
                default: txtTimer.text = ""; break;
            }
            txtAbates.text = "Zumbis abatidos: " + gm.Abates;
        }

        if (vida != null)
        {
            barraVida.sizeDelta = new Vector2(390f * vida.Vida / vida.vidaMaxima, barraVida.sizeDelta.y);
            txtVida.text = $"VIDA {Mathf.CeilToInt(vida.Vida)}";
            txtKits.text = vida.Kits > 0 ? $"<color=#ff6b6b>+</color> Kit médico x{vida.Kits}   [Q] usar" : "";
        }

        var fome = PlayerHunger.Instance;
        if (fome != null)
        {
            barraFome.sizeDelta = new Vector2(390f * fome.Fome / fome.fomeMaxima, barraFome.sizeDelta.y);
            // pisca quando está com fome
            bool piscar = fome.ComFome && Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;
            txtFome.text = fome.Fome <= 0f ? "FOME!  perdendo vida" : (fome.ComFome ? "COM FOME" : $"FOME {Mathf.CeilToInt(fome.Fome)}");
            txtFome.color = piscar ? new Color(1f, 0.4f, 0.3f) : Color.white;
        }

        var lia = LiaNPC.Instance;
        bool mostraLia = lia != null && lia.Seguindo && gm != null && gm.FaseAtual == Fase.NoiteFinal;
        fundoBarraLia.enabled = fundoLia.enabled = txtLia.enabled = mostraLia;
        if (mostraLia)
        {
            barraLia.sizeDelta = new Vector2(392f * lia.Vida / lia.vidaMaxima, barraLia.sizeDelta.y);
            txtLia.text = $"LIA  {Mathf.CeilToInt(lia.Vida)}";
            txtLia.color = Time.time - lia.UltimoDano < 0.3f ? new Color(1f, 0.3f, 0.3f) : Color.white;
        }

        if (armas != null && armas.ArmaAtual != null)
        {
            var a = armas.ArmaAtual;
            txtArma.text = $"[{armas.IndiceAtual + 1}] {a.nome}";
            string reserva = a.municaoInfinita ? "∞" : armas.ReservaAtual.ToString();
            txtMunicao.text = armas.Recarregando ? "RECARREGANDO..." : $"{armas.PenteAtual} / {reserva}";
        }

        if (txtAviso.text != "" && Time.unscaledTime > fimAviso) txtAviso.text = "";
    }

    // barras pretas nas cenas, tela preta das transições (vermelha escurecendo no Game Over), texto da abertura
    void AtualizarCinema(GameManager gm)
    {
        if (gm == null) return;
        float dt = Time.unscaledDeltaTime;
        barras = Mathf.MoveTowards(barras, gm.EmCena ? 1f : 0f, dt * 1.6f);
        float alturaBarra = 120f * Mathf.SmoothStep(0f, 1f, barras);
        barraCima.sizeDelta = new Vector2(0f, alturaBarra);
        barraBaixo.sizeDelta = new Vector2(0f, alturaBarra);
        barraCima.gameObject.SetActive(alturaBarra > 0.5f);
        barraBaixo.gameObject.SetActive(alturaBarra > 0.5f);

        float a = gm.Escuro;
        Color cor = Color.black;
        if (gm.FaseAtual == Fase.GameOver)
        {
            float k = Mathf.Clamp01((Time.unscaledTime - gm.InicioFim) / 1.4f);
            a = Mathf.Max(a, 0.85f * k);
            cor = Color.Lerp(new Color(0.45f, 0.02f, 0.02f), Color.black, k * k);
        }
        escuro.color = new Color(cor.r, cor.g, cor.b, a);
        escuro.enabled = a > 0.001f;

        bool cartela = gm.CartelaAlfa > 0.001f && !string.IsNullOrEmpty(gm.Cartela);
        txtCartela.enabled = cartela;
        if (cartela)
        {
            txtCartela.text = gm.Cartela;
            txtCartela.color = new Color(0.95f, 0.93f, 0.88f, gm.CartelaAlfa);
        }
        txtPular.enabled = gm.EmCena && !gm.Acabou;
    }

    // mira: abre andando/correndo/atirando, fecha mirando; vermelha em cima de zumbi
    void AtualizarMira(GameManager gm, PlayerWeapons armas)
    {
        var arma = armas != null ? armas.ArmaAtual : null;
        bool ativo = gm == null || gm.JogoAtivo;
        if (controle == null && PlayerHealth.Instance != null) controle = PlayerHealth.Instance.GetComponent<PlayerController>();
        bool mirando = controle != null && controle.Mirando;
        bool zoom = arma != null && arma.fovMira > 0f && mirando;
        txtMira.enabled = ativo && zoom;
        bool barrasVisiveis = ativo && !zoom;
        foreach (var b in miraBarras) b.enabled = barrasVisiveis;
        miraPonto.enabled = barrasVisiveis;
        if (!barrasVisiveis) return;

        float dt = Time.unscaledDeltaTime;
        tiroRecente = Mathf.MoveTowards(tiroRecente, 0f, dt * 3f);
        float mov = controle != null ? Mathf.Clamp01(controle.VelocidadeNormalizada) : 0f;
        float spread = arma != null ? (mirando ? (arma.spreadMirando >= 0f ? arma.spreadMirando : arma.spread * 0.5f) : arma.spread) : 1.5f;
        float alvo = 6f + spread * 4f + mov * 16f + tiroRecente * 16f;
        aberturaMira = Mathf.Lerp(aberturaMira, alvo, 1f - Mathf.Exp(-16f * dt));
        miraBarras[0].rectTransform.anchoredPosition = new Vector2(0f, aberturaMira + 6f);
        miraBarras[1].rectTransform.anchoredPosition = new Vector2(0f, -aberturaMira - 6f);
        miraBarras[2].rectTransform.anchoredPosition = new Vector2(-aberturaMira - 6f, 0f);
        miraBarras[3].rectTransform.anchoredPosition = new Vector2(aberturaMira + 6f, 0f);

        Color cor = ZumbiNaMira(arma) ? new Color(1f, 0.35f, 0.28f, 0.95f) : new Color(1f, 1f, 1f, 0.9f);
        foreach (var b in miraBarras) b.color = cor;
        miraPonto.color = cor;
    }

    bool ZumbiNaMira(WeaponData arma)
    {
        var cam = Camera.main;
        var ph = PlayerHealth.Instance;
        if (cam == null || ph == null) return false;
        float alcance = arma != null ? arma.alcance : 60f;
        int n = Physics.RaycastNonAlloc(cam.transform.position, cam.transform.forward, hitsMira, alcance, ~0, QueryTriggerInteraction.Ignore);
        float melhor = float.MaxValue;
        Collider perto = null;
        for (int i = 0; i < n; i++)
        {
            if (hitsMira[i].transform.IsChildOf(ph.transform) || hitsMira[i].distance >= melhor) continue;
            melhor = hitsMira[i].distance;
            perto = hitsMira[i].collider;
        }
        if (perto == null) return false;
        var z = perto.GetComponentInParent<ZombieAI>();
        return z != null && !z.EstaMorto;
    }

    // cor do nome de cada personagem na legenda
    static string CorDe(string quem)
    {
        if (quem == "Lia") return "#ff9ccf";
        if (quem == "Marcos") return "#9fd0ff";
        if (quem != null && (quem.StartsWith("Base") || quem.StartsWith("Resgate"))) return "#9dff8a";
        return "#ffffff";
    }

    // legenda: aparece suave, nome colorido em cima; pelo rádio tem um ponto vermelho piscando enquanto fala
    void AtualizarLegenda(RadioSystem radio)
    {
        bool tem = radio != null && radio.Mostrando;
        fundoRadio.enabled = txtRadio.enabled = txtRadioQuem.enabled = tem;
        if (!tem) { legendaAntes = null; return; }
        if (!ReferenceEquals(legendaAntes, radio.Texto))
        {
            legendaAntes = radio.Texto;
            txtRadio.text = radio.Texto;
        }
        bool pisca = radio.PeloRadio && radio.Falando && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.45f;
        int estado = (pisca ? 1 : 0) + (radio.PeloRadio ? 2 : 0);
        if (!ReferenceEquals(quemAntes, radio.Quem) || estado != estadoQuem)
        {
            quemAntes = radio.Quem;
            estadoQuem = estado;
            string ponto = radio.PeloRadio ? (pisca ? "<color=#ff4040>●</color> " : "<color=#663333>●</color> ") : "";
            string quem = (radio.PeloRadio ? "RÁDIO  ·  " : "") + radio.Quem.ToUpper();
            txtRadioQuem.text = $"{ponto}<color={CorDe(radio.Quem)}><b>{quem}</b></color>";
        }
        // entra em 0,2 s e sai nos últimos 0,25 s
        float a = Mathf.Clamp01(radio.TempoNaTela / 0.2f);
        if (!radio.Falando) a = Mathf.Min(a, Mathf.Clamp01(radio.TempoRestante / 0.25f));
        txtRadio.color = new Color(1f, 1f, 1f, a);
        txtRadioQuem.color = new Color(1f, 1f, 1f, a);
        fundoRadio.color = new Color(0f, 0f, 0f, 0.62f * a);
    }

    // painel grande do sinal do rádio da Lia (Dia 2, até o farol da clínica aparecer)
    void AtualizarSinal(GameManager gm, ObjectiveManager obj, PlayerHealth vida)
    {
        bool mostra = gm != null && gm.JogoAtivo && obj != null && obj.ModoSinal && obj.Alvo != null && vida != null;
        if (fundoSinal.gameObject.activeSelf != mostra)
        {
            fundoSinal.gameObject.SetActive(mostra);
            // com o painel do sinal na tela, a caixa de dicas desce pra baixo dele
            float desce = mostra ? 100f : 0f;
            var rtF = fundoTutorial.rectTransform;
            rtF.offsetMin = new Vector2(-600, -226 - desce);
            rtF.offsetMax = new Vector2(600, -148 - desce);
            txtTutorial.rectTransform.anchoredPosition = new Vector2(0, -150 - desce);
        }
        if (!mostra) { barrasAntes = -1; return; }
        float forca = obj.ForcaSinal(vida.transform.position);
        int barras5 = Mathf.Clamp(Mathf.RoundToInt(forca * 5f), 0, 5);
        if (barrasAntes >= 0 && barras5 > barrasAntes) flashSinal = 1f; // melhorou: painel pisca verde
        if (barras5 != barrasAntes)
        {
            barrasAntes = barras5;
            txtSinalTitulo.text = "● SINAL DO RÁDIO DA LIA";
            txtSinalDica.text = barras5 <= 1 ? "Só chiado... ande e veja se as barras aumentam"
                              : barras5 <= 3 ? "Está esquentando! Continue nessa direção"
                              : Interior.Atual != null ? "MUITO QUENTE! Procure aqui perto"
                              : "MUITO PERTO! Procure o farol verde da clínica";
            txtSinalDica.color = barras5 >= 4 ? new Color(0.6f, 1f, 0.5f) : Color.white;
        }
        for (int i = 0; i < barrasSinal.Length; i++)
        {
            bool acesa = i < barras5;
            // a barra seguinte "chia" (pisca fraco) pra mostrar que dá pra melhorar
            float chiado = !acesa && i == barras5 ? 0.12f + 0.15f * Mathf.PerlinNoise(Time.unscaledTime * 6f, i) : 0.12f;
            barrasSinal[i].color = acesa ? Color.Lerp(new Color(1f, 0.3f, 0.2f), new Color(0.35f, 1f, 0.35f), i / 4f) : new Color(1f, 1f, 1f, chiado);
        }
        flashSinal = Mathf.MoveTowards(flashSinal, 0f, Time.unscaledDeltaTime * 1.5f);
        fundoSinal.color = Color.Lerp(new Color(0f, 0f, 0f, 0.62f), new Color(0.1f, 0.45f, 0.1f, 0.85f), flashSinal);
        // ponto do título pisca como luz de rádio
        txtSinalTitulo.color = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.85f, 0.35f, 0.75f);
    }

    // título da fase: aparece (até em cima da tela preta da transição), fica uns segundos e some
    void AtualizarTitulo(GameManager gm)
    {
        float t = gm != null ? Time.unscaledTime - gm.InicioTitulo : 99f;
        float a = t < 0.6f ? t / 0.6f : Mathf.Clamp01((4.5f - t) / 1.2f);
        bool mostra = gm != null && a > 0f && !gm.NoMenu && !gm.Pausado && !gm.Acabou && !gm.EmCena;
        txtTitulo.enabled = txtSubtitulo.enabled = mostra;
        if (!mostra) return;
        txtTitulo.text = gm.TituloFase;
        txtSubtitulo.text = gm.SubtituloFase;
        txtTitulo.color = new Color(1f, 0.95f, 0.85f, a);
        txtSubtitulo.color = new Color(1f, 0.85f, 0.4f, a);
    }

    void AtualizarTutorial(GameManager gm, PlayerHealth vida, PlayerWeapons armas)
    {
        bool ativo = gm != null && gm.JogoAtivo;
        // dicas que aparecem na hora certa (uma vez por partida)
        if (ativo)
        {
            var fome = PlayerHunger.Instance;
            if (!dicaFome && fome != null && fome.Fome < 55f)
            {
                dicaFome = true;
                Tutorial("Sua FOME está caindo (barra laranja). Pegue LATAS DE COMIDA: base LARANJA no chão.");
            }
            if (!dicaVida && vida != null && vida.Vida < vida.vidaMaxima * 0.5f)
            {
                dicaVida = true;
                Tutorial(vida.Kits > 0 ? "Vida baixa! Aperte Q para usar um KIT MÉDICO."
                                       : "Vida baixa! Procure KITS MÉDICOS (base VERMELHA no chão). Sem fome, a vida volta devagar.");
            }
            if (!dicaMunicao && armas != null && armas.ArmaAtual != null && !armas.ArmaAtual.municaoInfinita && armas.PenteAtual == 0 && armas.ReservaAtual == 0)
            {
                dicaMunicao = true;
                Tutorial("Sem munição! Troque de arma (1-4) ou pegue CAIXAS DE MUNIÇÃO. A pistola (1) nunca acaba.");
            }
        }
        // só conta o tempo com o jogo rodando (pausa não "gasta" a dica)
        if (ativo && Time.time > fimTutorial && filaTutorial.Count > 0)
        {
            txtTutorial.text = filaTutorial.Dequeue();
            fimTutorial = Time.time + 5.5f;
        }
        bool mostra = ativo && Time.time < fimTutorial;
        txtTutorial.enabled = fundoTutorial.enabled = mostra;
    }

    // seta em cima do objetivo (no Dia 2 só aparece quando o farol da clínica já está visível)
    void AtualizarMarcador(GameManager gm, ObjectiveManager obj, PlayerHealth vida)
    {
        var cam = Camera.main;
        bool mostra = gm != null && gm.JogoAtivo && obj != null && obj.Alvo != null && cam != null && vida != null
                      && (!obj.ModoSinal || (gm.farolClinica != null && gm.farolClinica.activeSelf));
        txtMarcador.enabled = mostra;
        if (!mostra) return;
        Vector3 sp = cam.WorldToScreenPoint(obj.Alvo.position + Vector3.up * 2.5f);
        bool atras = sp.z < 0f;
        if (atras) { sp.x = Screen.width - sp.x; sp.y = 0f; }
        float margem = 70f * Screen.height / 1080f;
        bool naBorda = atras || sp.x < margem || sp.x > Screen.width - margem || sp.y < margem || sp.y > Screen.height - margem;
        sp.x = Mathf.Clamp(sp.x, margem, Screen.width - margem);
        sp.y = Mathf.Clamp(sp.y, margem * 1.6f, Screen.height - margem * 2.2f);
        txtMarcador.rectTransform.position = new Vector3(sp.x, sp.y, 0f);
        float dist = Vector3.Distance(vida.transform.position, obj.Alvo.position);
        txtMarcador.text = (naBorda ? "◆" : "▼") + $"\n{dist:0} m";
    }

    static string Tempo(float s)
    {
        s = Mathf.Max(0f, s);
        return $"{(int)(s / 60):00}:{(int)(s % 60):00}";
    }

    // helpers de UI

    Text CriarTexto(Transform pai, string nome, int tamanho, TextAnchor alinhamento, Vector2 ancora, Vector2 pos, Vector2 tam)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(Text), typeof(Outline));
        go.transform.SetParent(pai, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = ancora;
        rt.pivot = ancora;
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        var t = go.GetComponent<Text>();
        t.font = fonte;
        t.fontSize = tamanho;
        t.alignment = alinhamento;
        t.color = Color.white;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        // contorno escuro: dá pra ler em cima do céu claro e do fogo
        var contorno = go.GetComponent<Outline>();
        contorno.effectColor = new Color(0f, 0f, 0f, 0.55f);
        contorno.effectDistance = new Vector2(1.3f, -1.3f);
        return t;
    }

    Image CriarImagem(Transform pai, string nome, Color cor, Vector2 ancMin, Vector2 ancMax, Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(pai, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = ancMin;
        rt.anchorMax = ancMax;
        rt.pivot = Vector2.zero;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
        var img = go.GetComponent<Image>();
        img.color = cor;
        img.raycastTarget = false;
        return img;
    }

    GameObject CriarPainel(Transform pai, string nome, out Text titulo, out Text sub)
    {
        var fundo = CriarImagem(pai, nome, new Color(0, 0, 0, 0.8f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fundo.raycastTarget = true;
        titulo = CriarTexto(fundo.transform, "Titulo", 72, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(1400, 100));
        sub = CriarTexto(fundo.transform, "Sub", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1400, 160));
        return fundo.gameObject;
    }

    Text CriarBotao(Transform pai, string texto, Vector2 pos, UnityEngine.Events.UnityAction acao, float largura = 420f)
    {
        var go = new GameObject("Botao_" + texto, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(pai, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(largura, 64);
        go.GetComponent<Image>().color = Color.white; // a cor vem do "tint" do botão
        var botao = go.GetComponent<Button>();
        var cores = botao.colors;
        cores.normalColor = new Color(0.2f, 0.22f, 0.25f, 0.95f);
        cores.highlightedColor = new Color(0.75f, 0.58f, 0.22f, 1f);
        cores.selectedColor = cores.normalColor;
        cores.pressedColor = new Color(0.55f, 0.42f, 0.16f, 1f);
        botao.colors = cores;
        botao.onClick.AddListener(acao);
        var t = CriarTexto(go.transform, "Texto", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(largura, 64));
        t.text = texto;
        return t;
    }
}
