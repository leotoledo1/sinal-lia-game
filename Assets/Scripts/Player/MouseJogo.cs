using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

// Controla a leitura do mouse para girar a camera.
// No modo normal usa o cursor travado no centro; em sessao remota (RDP) le a posicao pela API do Windows para evitar travar na borda.
public static class MouseJogo
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int indice);
    const int SM_REMOTESESSION = 0x1000;
#endif
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [StructLayout(LayoutKind.Sequential)] struct PONTO { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] struct RET { public int esq, topo, dir, baixo; }
    [DllImport("user32.dll")] static extern bool GetCursorPos(out PONTO p);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr janela, out RET r);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr janela, ref PONTO p);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr janela, out uint processo);
    [DllImport("kernel32.dll")] static extern uint GetCurrentProcessId();
#endif

    // qual modo usar
    static bool sessaoRemota, jaChecou;
    static int ultimaChecagem;

    // jogo rodando dentro de uma Área de Trabalho Remota? (confere de novo a cada 2 s: dá pra conectar com o jogo aberto)
    public static bool SessaoRemota
    {
        get
        {
            int agora = Environment.TickCount;
            if (!jaChecou || unchecked(agora - ultimaChecagem) > 2000)
            {
                jaChecou = true;
                ultimaChecagem = agora;
                sessaoRemota = DetectarSessaoRemota();
            }
            return sessaoRemota;
        }
    }

    static bool DetectarSessaoRemota()
    {
        try
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (GetSystemMetrics(SM_REMOTESESSION) != 0) return true;
#endif
            // reserva: o Windows chama a sessão remota de "RDP-Tcp#0"
            string sessao = Environment.GetEnvironmentVariable("SESSIONNAME");
            return sessao != null && sessao.StartsWith("RDP", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) { return false; }
    }

    // teste automático: força um modo (null = detecta sozinho)
    public static bool? ForcarRemoto;

    // modo em uso agora: normal em qualquer PC; o remoto só entra sozinho dentro da Área de Trabalho Remota
    public static bool Remoto => ForcarRemoto ?? SessaoRemota;

    // o jogo quer o cursor preso (jogando). No modo remoto não dá pra confiar no Cursor.lockState
    // (o Unity só guarda o "Confined" na build; no Editor e no teste automático ele volta como None)
    public static bool QuerPreso;

    // jogando: normal = travado no centro; remoto = escondido e preso na janela (o próprio jogo devolve pro meio)
    public static CursorLockMode ModoPreso => Remoto ? CursorLockMode.Confined : CursorLockMode.Locked;
    public static bool Preso => Remoto ? QuerPreso && Cursor.lockState != CursorLockMode.Locked
                                       : Cursor.lockState == CursorLockMode.Locked;

    // de onde vem o cursor (o teste automático troca por um cursor simulado)
    // posições em pixels da tela com y pra BAIXO (igual ao Windows)
    public static Func<Vector2> LerCursor = LerCursorPadrao;
    public static Action<Vector2> MoverCursor = MoverCursorPadrao;
    public static Func<Rect> AreaJanela = AreaJanelaPadrao;

    public static void RestaurarFonte()
    {
        LerCursor = LerCursorPadrao;
        MoverCursor = MoverCursorPadrao;
        AreaJanela = AreaJanelaPadrao;
    }

    static Vector2 LerCursorPadrao()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        return GetCursorPos(out PONTO p) ? new Vector2(p.x, p.y) : new Vector2(float.NaN, float.NaN);
#else
        var m = Mouse.current;
        if (m == null) return new Vector2(float.NaN, float.NaN);
        Vector2 u = m.position.ReadValue();
        return new Vector2(u.x, Screen.height - u.y);
#endif
    }

    static void MoverCursorPadrao(Vector2 alvo)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        SetCursorPos(Mathf.RoundToInt(alvo.x), Mathf.RoundToInt(alvo.y));
#else
        Mouse.current?.WarpCursorPosition(new Vector2(alvo.x, Screen.height - alvo.y));
#endif
    }

    // área de desenho da janela do jogo na tela (pixels, y pra baixo)
    static Rect AreaJanelaPadrao()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            IntPtr j = GetForegroundWindow();
            if (j != IntPtr.Zero && GetWindowThreadProcessId(j, out uint dono) != 0 && dono == GetCurrentProcessId()
                && GetClientRect(j, out RET r) && r.dir - r.esq > 8 && r.baixo - r.topo > 8)
            {
                var canto = new PONTO();
                ClientToScreen(j, ref canto);
                return new Rect(canto.x, canto.y, r.dir - r.esq, r.baixo - r.topo);
            }
        }
        catch (Exception) { }
#endif
        return new Rect(0, 0, Screen.width, Screen.height);
    }

    // leitura
    const float LIMITE_VOLTA = 0.25f;   // cursor mais longe que isso do meio (fração da janela) -> volta pro centro
    const float ESPERA_VOLTA = 0.5f;    // segundos esperando o PC de fora aplicar a volta
    const float FAIXA_BORDA = 0.06f;    // cursor encostado na borda (fração da janela) -> câmera gira sozinha
    const float GIRO_BORDA = 1200f;     // "pixels por segundo" na borda (≈120°/s com a sensibilidade padrão)

    static int quadroLido = -10;
    static Vector2 movQuadro;
    static bool temRef, pendente, semVolta;
    static Vector2 refAgora, refAntes, alvoVolta, vetorVolta;
    static float tVolta = -10f;
    static int velhas, falhas, balanco;

    // usados pelo teste automático e pelo diagnóstico
    public static int Recentralizacoes { get; private set; }
    public static Vector2 UltimoCentro => alvoVolta;
    public static bool VoltaNaoFunciona => semVolta;
    public static Vector2 GiroBorda { get; private set; }

    // começa do zero a cada Play (o Editor pode manter as variáveis estáticas entre um Play e outro)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ZerarAoIniciar()
    {
        ForcarRemoto = null;
        QuerPreso = false;
        RestaurarFonte();
        quadroLido = -10;
        temRef = pendente = semVolta = false;
        falhas = velhas = 0;
        Recentralizacoes = 0;
        tVolta = -10f;
        FecharDiagnostico();
    }

    // cursor acabou de prender / voltou o foco: a próxima leitura só guarda a posição
    public static void Reiniciar()
    {
        temRef = false;
        pendente = false;
    }

    // quanto o mouse andou neste quadro (pixels, y pra cima). Pode chamar mais de uma vez no mesmo quadro.
    public static Vector2 Movimento(Mouse m)
    {
        int q = Time.frameCount;
        if (q == quadroLido) return movQuadro;
        if (q - quadroLido > 1) temRef = false; // ficou um tempo sem ler (cena, pausa): a posição antiga não vale
        quadroLido = q;
        if (Remoto) movQuadro = PelaPosicao(Time.unscaledDeltaTime);
        else
        {
            movQuadro = m != null ? m.delta.ReadValue() : Vector2.zero;
            GiroBorda = Vector2.zero;
        }
        return movQuadro;
    }

    static Vector2 PelaPosicao(float dt)
    {
        Rect jan = AreaJanela();
        Vector2 p = LerCursor();
        if (jan.width < 8f || jan.height < 8f || float.IsNaN(p.x) || float.IsNaN(p.y))
        {
            temRef = false;
            GiroBorda = Vector2.zero;
            return Vector2.zero;
        }
        float agora = Time.unscaledTime;
        Vector2 d = Vector2.zero;
        string evento = null;
        if (!temRef)
        {
            temRef = true;
            pendente = false;
            refAgora = p;
        }
        else
        {
            d = p - refAgora;
            if (pendente)
            {
                // logo depois da volta pro meio, o PC de fora ainda pode mandar posições "de antes":
                // vale a referência que der o menor movimento (a outra seria um pulo falso do tamanho da volta)
                Vector2 dAntes = p - refAntes;
                if (dAntes.sqrMagnitude < d.sqrMagnitude)
                {
                    d = dAntes;
                    refAntes = p;
                    if (dAntes.sqrMagnitude > 0.25f) velhas++;
                }
                else
                {
                    refAgora = p;
                    // o cursor já anda a partir do centro novo: a volta pro meio chegou no PC de fora
                    if ((p - alvoVolta).sqrMagnitude > 2.25f)
                    {
                        pendente = false;
                        falhas = 0;
                        evento = $"volta OK em {(agora - tVolta) * 1000f:0} ms";
                    }
                }
                if (pendente && agora - tVolta > ESPERA_VOLTA)
                {
                    pendente = false;
                    refAgora = p;
                    if (velhas >= 2)
                    {
                        falhas++;
                        evento = $"volta NAO pegou ({velhas} posicoes antigas)";
                        if (falhas >= 3)
                        {
                            semVolta = true;
                            evento += " -> parei de devolver pro meio; borda gira a camera";
                        }
                    }
                }
            }
            else
            {
                refAgora = p;
                // volta pro meio que chegou atrasada no PC de fora: o "pulo" tem o tamanho da volta -> ignora
                if (agora - tVolta < 1.5f && vetorVolta.sqrMagnitude > 1f && (d - vetorVolta).sqrMagnitude < 0.25f * vetorVolta.sqrMagnitude)
                {
                    evento = "volta atrasada ignorada";
                    d = Vector2.zero;
                }
            }
            float maxPulo = jan.height * 0.4f;
            if (d.sqrMagnitude > maxPulo * maxPulo)
            {
                evento = $"pulo ignorado ({d.x:0},{d.y:0})";
                d = Vector2.zero;
            }
        }

        // encostado na borda: a câmera continua girando pra aquele lado (funciona mesmo se a volta pro meio não pegar)
        float fx = (p.x - jan.x) / jan.width, fy = (p.y - jan.y) / jan.height;
        GiroBorda = new Vector2(Borda(fx), Borda(fy) * 0.6f) * GIRO_BORDA;
        d += GiroBorda * dt;

        // longe do meio: devolve o cursor pro centro (varia 1 px: o RDP ignora "ir pro mesmo lugar" repetido)
        Vector2 centro = jan.center;
        if (!semVolta && !pendente && (Mathf.Abs(p.x - centro.x) > jan.width * LIMITE_VOLTA || Mathf.Abs(p.y - centro.y) > jan.height * LIMITE_VOLTA))
        {
            balanco = balanco >= 1 ? -1 : balanco + 1;
            alvoVolta = new Vector2(Mathf.Round(centro.x) + balanco, Mathf.Round(centro.y));
            MoverCursor(alvoVolta);
            refAntes = p;
            refAgora = alvoVolta;
            vetorVolta = alvoVolta - p;
            pendente = true;
            tVolta = agora;
            velhas = 0;
            Recentralizacoes++;
            evento = (evento != null ? evento + " | " : "") + $"volta pro meio ({p.x:0},{p.y:0}) -> ({alvoVolta.x:0},{alvoVolta.y:0})";
        }
        Anotar(jan, p, d, evento);
        return new Vector2(d.x, -d.y);
    }

    // 0 longe da borda; de 0 a 1 entrando na faixa da borda (negativo = esquerda/em cima)
    static float Borda(float f)
    {
        if (f < FAIXA_BORDA) return -Mathf.Clamp01((FAIXA_BORDA - f) / FAIXA_BORDA);
        if (f > 1f - FAIXA_BORDA) return Mathf.Clamp01((f - (1f - FAIXA_BORDA)) / FAIXA_BORDA);
        return 0f;
    }

    // diagnóstico (só na build): mouse_rdp.txt na pasta do Player.log
    static StreamWriter diag;
    static int linhasDiag;
    const int MAX_LINHAS_DIAG = 8000;

    static void FecharDiagnostico()
    {
        try { diag?.Flush(); diag?.Dispose(); } catch (Exception) { }
        diag = null;
        linhasDiag = 0;
    }

    static void Anotar(Rect jan, Vector2 p, Vector2 d, string evento)
    {
#if !UNITY_EDITOR
        if (linhasDiag >= MAX_LINHAS_DIAG || LerCursor != (Func<Vector2>)LerCursorPadrao) return;
        try
        {
            if (diag == null)
            {
                diag = new StreamWriter(Path.Combine(Application.persistentDataPath, "mouse_rdp.txt"), false);
                diag.WriteLine($"Sinal da Lia - diagnostico do mouse (modo Area Remota) {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                diag.WriteLine($"Unity {Application.unityVersion} | sessao remota: {SessaoRemota} | SESSIONNAME={Environment.GetEnvironmentVariable("SESSIONNAME")} | " +
                               $"tela {Screen.width}x{Screen.height} {Screen.fullScreenMode} {Screen.currentResolution.refreshRateRatio.value:0.#} Hz | janela ({jan.x:0},{jan.y:0}) {jan.width:0}x{jan.height:0} | vsync {QualitySettings.vSyncCount}");
                diag.WriteLine("    t(s)  fps  cursor(x,y)   unityPos(x,y)  unityDelta     mov(x,y)   borda    estado");
                Application.quitting += FecharDiagnostico;
            }
            var m = Mouse.current;
            Vector2 up = m != null ? m.position.ReadValue() : Vector2.zero;
            Vector2 ud = m != null ? m.delta.ReadValue() : Vector2.zero;
            diag.WriteLine($"{Time.unscaledTime,8:0.000} {1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime),4:0}  {p.x,5:0},{p.y,5:0}  {up.x,6:0},{up.y,5:0}  {ud.x,6:0},{ud.y,5:0}  {d.x,6:0},{d.y,5:0}  {GiroBorda.x,5:0},{GiroBorda.y,4:0}  " +
                           $"{(pendente ? "P" : "-")}{(semVolta ? "S" : "-")} v{Recentralizacoes} f{falhas}{(evento != null ? "  << " + evento : "")}");
            linhasDiag++;
            if (linhasDiag % 30 == 0 || evento != null) diag.Flush();
            if (linhasDiag >= MAX_LINHAS_DIAG) { diag.WriteLine("(fim: limite de linhas)"); diag.Flush(); }
        }
        catch (Exception) { linhasDiag = MAX_LINHAS_DIAG; }
#endif
    }
}
