using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Menus do HUD: principal, como jogar, opções, créditos, pausa, fim de jogo e o desenho da Lia
public partial class HUD
{
    GameObject menuPrincipal, painelPausa, painelFim, painelComo, painelOpcoes, painelCreditos, painelDesenho;
    GameObject botoesDerrota, botoesVitoria, botaoTentar;
    Text txtFimTitulo, txtFimSub, txtTentar, txtVolume, txtSens, txtGraficos, txtPausaSub;
    // painel aberto por cima do menu/pausa/fim: null, "como", "opcoes" ou "creditos"
    string subTela;

    const string TEXTO_COMO_JOGAR =
        "<b><color=#ffd966>A HISTÓRIA</color></b>\n" +
        "Você é o Marcos: foi mordido e não virou zumbi. O exército quer o seu sangue pra vacina e vem te buscar em 2 dias.\n" +
        "Antes disso, você precisa achar sua filha Lia, que sumiu quando a escola foi evacuada.\n\n" +
        "<b><color=#ffd966>O QUE FAZER</color></b>  (o objetivo atual fica sempre no canto de cima da tela)\n" +
        "1. <b>DIA 1</b> - Vá até a escola (siga a seta ▼ e o farol amarelo) e pegue o desenho da Lia com <b>E</b>.\n" +
        "2. <b>NOITE 1</b> - Sobreviva aos zumbis até amanhecer (o relógio fica no topo da tela).\n" +
        "3. <b>DIA 2</b> - Siga o SINAL DO RÁDIO: mais barras = mais perto. Perto da clínica aparece um farol verde.\n" +
        "4. <b>NOITE FINAL</b> - A Lia te segue. Proteja ela até o helicóptero chegar.\n" +
        "Se você ou a Lia morrer, dá pra tentar de novo do começo da fase.\n\n" +
        "<b><color=#ffd966>CONTROLES</color></b>\n" +
        "WASD andar  ·  SHIFT correr  ·  ESPAÇO pular  ·  MOUSE girar a câmera\n" +
        "BOTÃO DIREITO mirar  ·  BOTÃO ESQUERDO atirar  ·  R recarregar  ·  1 2 3 4 ou rodinha trocar de arma\n" +
        "E pegar / interagir  ·  Q usar kit médico  ·  ESC pausa  ·  ENTER pula as cenas\n\n" +
        "<b><color=#ffd966>DICAS</color></b>\n" +
        "Tiro na cabeça dá dano dobrado. Feixe de luz vermelha no chão = arma nova.\n" +
        "Base laranja = comida (mata a fome). Base vermelha = kit médico. Zumbis às vezes deixam munição.\n" +
        "Com fome você não recupera vida; com a fome zerada você perde vida.";

    const string TEXTO_CREDITOS =
        "<b>SINAL DA LIA</b>\nJogo feito para a faculdade por\n<b>Fernando  ·  Danillo  ·  Henrique  ·  Leonardo</b>\n\n" +
        "<b><color=#ffd966>Modelos 3D</color></b>\n" +
        "Personagens, zumbis, armas e objetos: \"Post Apocalypse Pack\" - Quaternius (quaternius.com) - CC-BY\n" +
        "Natureza: \"Stylized Nature MegaKit\" - Quaternius (quaternius.com)\n" +
        "Escola e clínica por dentro: móveis \"Ultimate House Interior\" e \"Furniture\" - Quaternius (CC0) · \"Furniture Kit\" - Kenney (kenney.nl, CC0)\n" +
        "Cidade: \"LowpolyStreetPack\" e \"Pandazole City Town Pack\"\n" +
        "Sketchfab (CC-BY): \"Polyart Zombies with Animations\", \"Polygonal Modern Weapons Collection 1\" e prédios low poly\n" +
        "Helicóptero: \"Helicopter\" - Zsky (poly.pizza) - CC-BY 3.0\n\n" +
        "<b><color=#ffd966>Música</color></b>\n" +
        "\"Despair and Triumph\", \"Aftermath\", \"Lost Frontier\", \"Darkling\", \"Black Vortex\", \"Bittersweet\", \"Heartbreaking\"\n" +
        "Kevin MacLeod (incompetech.com) - Licensed under Creative Commons: By Attribution 4.0\n" +
        "Efeitos sonoros e céu: gerados por código no próprio projeto\n" +
        "Vozes: gravadas pelo grupo\n\n" +
        "Feito com Unity 6 (URP)";

    void MontarMenus()
    {
        // menu principal (o jogo aparece atrás, com a câmera girando devagar)
        var fundoMenu = CriarImagem(canvasT, "MenuPrincipal", new Color(0f, 0f, 0f, 0.3f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fundoMenu.raycastTarget = true;
        menuPrincipal = fundoMenu.gameObject;
        var m = menuPrincipal.transform;
        CriarImagem(m, "Faixa", new Color(0f, 0f, 0f, 0.62f), Vector2.zero, new Vector2(0.42f, 1f), Vector2.zero, Vector2.zero);
        var titulo = CriarTexto(m, "Titulo", 120, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(100, 300), new Vector2(1100, 150));
        titulo.text = "SINAL DA LIA";
        titulo.color = new Color(1f, 0.93f, 0.82f);
        var sub = CriarTexto(m, "Subtitulo", 30, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(106, 195), new Vector2(800, 90));
        sub.text = "Um pai imune. Uma filha perdida.\nDois dias até o helicóptero do resgate.";
        sub.color = new Color(1f, 0.8f, 0.45f);
        var botoes = Grupo(m, "Botoes", new Vector2(0f, 0.5f), new Vector2(330, -110), new Vector2(440, 420));
        CriarBotao(botoes, "JOGAR", new Vector2(0, 160), () => GameManager.Instance.Jogar());
        CriarBotao(botoes, "Como jogar", new Vector2(0, 80), () => subTela = "como");
        CriarBotao(botoes, "Opções", new Vector2(0, 0), () => subTela = "opcoes");
        CriarBotao(botoes, "Créditos", new Vector2(0, -80), () => subTela = "creditos");
        CriarBotao(botoes, "Sair", new Vector2(0, -160), () => GameManager.Instance.Sair());
        var rodape = CriarTexto(m, "Rodape", 20, TextAnchor.LowerLeft, Vector2.zero, new Vector2(100, 40), new Vector2(1200, 30));
        rodape.text = "Grupo: Fernando · Danillo · Henrique · Leonardo      ENTER = jogar";
        rodape.color = new Color(1f, 1f, 1f, 0.6f);

        // como jogar / opções / créditos (abrem por cima do menu, da pausa ou do fim)
        painelComo = CriarPainel(canvasT, "ComoJogar", out var tComo, out var sComo);
        tComo.text = "COMO JOGAR";
        TextoLongo(sComo, TEXTO_COMO_JOGAR, 23, TextAnchor.UpperLeft);
        CriarBotao(painelComo.transform, "Voltar", new Vector2(0, -440), () => subTela = null);

        painelOpcoes = CriarPainel(canvasT, "Opcoes", out var tOp, out _);
        tOp.text = "OPÇÕES";
        txtVolume = CriarTexto(painelOpcoes.transform, "Volume", 32, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(-140, 130), new Vector2(560, 60));
        CriarBotao(painelOpcoes.transform, "−", new Vector2(220, 130), () => Opcoes.Volume = Mathf.Round(Opcoes.Volume * 10f - 1f) / 10f, 80f);
        CriarBotao(painelOpcoes.transform, "+", new Vector2(320, 130), () => Opcoes.Volume = Mathf.Round(Opcoes.Volume * 10f + 1f) / 10f, 80f);
        txtSens = CriarTexto(painelOpcoes.transform, "Sensibilidade", 32, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(-140, 40), new Vector2(560, 60));
        CriarBotao(painelOpcoes.transform, "−", new Vector2(220, 40), () => Opcoes.Sensibilidade = Mathf.Round(Opcoes.Sensibilidade * 10f - 1f) / 10f, 80f);
        CriarBotao(painelOpcoes.transform, "+", new Vector2(320, 40), () => Opcoes.Sensibilidade = Mathf.Round(Opcoes.Sensibilidade * 10f + 1f) / 10f, 80f);
        txtGraficos = CriarBotao(painelOpcoes.transform, "Gráficos", new Vector2(0, -70), () => GameManager.Instance.AlternarGraficos(), 560f);
        CriarBotao(painelOpcoes.transform, "Voltar", new Vector2(0, -190), () => subTela = null);

        painelCreditos = CriarPainel(canvasT, "Creditos", out var tCred, out var sCred);
        tCred.text = "CRÉDITOS";
        TextoLongo(sCred, TEXTO_CREDITOS, 27, TextAnchor.UpperCenter);
        CriarBotao(painelCreditos.transform, "Voltar", new Vector2(0, -440), () => subTela = null);

        // pausa
        painelPausa = CriarPainel(canvasT, "Pausa", out var tPausa, out txtPausaSub);
        tPausa.text = "PAUSADO";
        txtPausaSub.color = new Color(1f, 0.85f, 0.45f);
        CriarBotao(painelPausa.transform, "Continuar", new Vector2(0, 60), () => GameManager.Instance.AlternarPausa());
        CriarBotao(painelPausa.transform, "Como jogar", new Vector2(0, -20), () => subTela = "como");
        CriarBotao(painelPausa.transform, "Opções", new Vector2(0, -100), () => subTela = "opcoes");
        CriarBotao(painelPausa.transform, "Menu principal", new Vector2(0, -180), () => GameManager.Instance.VoltarAoMenu());
        CriarBotao(painelPausa.transform, "Sair do jogo", new Vector2(0, -260), () => GameManager.Instance.Sair());

        // fim de jogo (vitória ou derrota)
        painelFim = CriarPainel(canvasT, "Fim", out txtFimTitulo, out txtFimSub);
        botoesDerrota = Grupo(painelFim.transform, "BotoesDerrota", new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(600, 300)).gameObject;
        txtTentar = CriarBotao(botoesDerrota.transform, "Tentar de novo", new Vector2(0, 80), () => GameManager.Instance.TentarDeNovo(), 560f);
        botaoTentar = txtTentar.transform.parent.gameObject;
        CriarBotao(botoesDerrota.transform, "Recomeçar do Dia 1", new Vector2(0, 0), () => GameManager.Instance.Recomecar(), 560f);
        CriarBotao(botoesDerrota.transform, "Menu principal", new Vector2(0, -80), () => GameManager.Instance.VoltarAoMenu(), 560f);
        botoesVitoria = Grupo(painelFim.transform, "BotoesVitoria", new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(600, 300)).gameObject;
        CriarBotao(botoesVitoria.transform, "Jogar de novo", new Vector2(0, 80), () => GameManager.Instance.Recomecar(), 560f);
        CriarBotao(botoesVitoria.transform, "Créditos", new Vector2(0, 0), () => subTela = "creditos", 560f);
        CriarBotao(botoesVitoria.transform, "Menu principal", new Vector2(0, -80), () => GameManager.Instance.VoltarAoMenu(), 560f);

        painelDesenho = CriarDesenho(canvasT);

        foreach (var p in new[] { menuPrincipal, painelComo, painelOpcoes, painelCreditos, painelPausa, painelFim, painelDesenho }) p.SetActive(false);
    }

    // área vazia (pra agrupar botões); "ancora" = ponto da tela, "centro" = deslocamento do meio do grupo
    Transform Grupo(Transform pai, string nome, Vector2 ancora, Vector2 centro, Vector2 tam)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = ancora;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = centro;
        rt.sizeDelta = tam;
        return go.transform;
    }

    static void TextoLongo(Text t, string texto, int tamanho, TextAnchor alinhamento)
    {
        t.text = texto;
        t.fontSize = tamanho;
        t.alignment = alinhamento;
        t.rectTransform.anchoredPosition = new Vector2(0, -40);
        t.rectTransform.sizeDelta = new Vector2(1500, 640);
    }

    void AtualizarMenus(GameManager gm)
    {
        if (gm == null) return;
        // Game Over: o painel espera a tela escurecer (dá pra ver o Marcos caindo)
        bool fim = gm.MostrarPainelFim;
        if (!gm.NoMenu && !gm.Pausado && !fim) subTela = null;
        menuPrincipal.SetActive(gm.NoMenu && subTela == null);
        painelPausa.SetActive(gm.Pausado && subTela == null);
        painelFim.SetActive(fim && subTela == null);
        painelComo.SetActive(subTela == "como");
        painelOpcoes.SetActive(subTela == "opcoes");
        painelCreditos.SetActive(subTela == "creditos");
        painelDesenho.SetActive(gm.LendoDesenho);

        var kb = Keyboard.current;
        if (kb != null && gm.NoMenu)
        {
            if (subTela != null && kb.escapeKey.wasPressedThisFrame) subTela = null;
            else if (subTela == null && !gm.Carregando && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) gm.Jogar();
        }

        if (painelOpcoes.activeSelf)
        {
            txtVolume.text = $"Volume: {Mathf.RoundToInt(Opcoes.Volume * 100f)}%";
            txtSens.text = $"Sensibilidade do mouse: {Opcoes.Sensibilidade:0.0}x";
            txtGraficos.text = "Gráficos: " + (gm.GraficosLeves ? "LEVE (mais rápido)" : "ALTO");
        }
        if (painelPausa.activeSelf)
            txtPausaSub.text = ObjectiveManager.Instance != null ? "Objetivo: " + ObjectiveManager.Instance.Texto : "";
        if (painelFim.activeSelf)
        {
            bool venceu = gm.FaseAtual == Fase.Vitoria;
            txtFimTitulo.text = venceu ? "VOCÊS ESCAPARAM!" : "GAME OVER";
            txtFimTitulo.color = venceu ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.3f, 0.3f);
            string stats = $"Zumbis abatidos: {gm.Abates}   ·   Tiros na cabeça: {gm.Headshots}   ·   Tempo: {Tempo(gm.TempoDeJogo)}";
            txtFimSub.text = venceu ? $"{gm.MotivoFim}\n\n{stats}\n<color=#ffd966>Obrigado por jogar!</color>"
                                    : $"{gm.MotivoFim}\n\n{stats}";
            botoesVitoria.SetActive(venceu);
            botoesDerrota.SetActive(!venceu);
            botaoTentar.SetActive(gm.TemCheckpoint);
            txtTentar.text = $"Tentar de novo ({gm.NomeCheckpoint})";
        }
    }

    // desenho da Lia (aparece ao pegar o papel na escola)

    GameObject CriarDesenho(Transform raiz)
    {
        var fundo = CriarImagem(raiz, "Desenho", new Color(0, 0, 0, 0.8f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var papelGo = new GameObject("Papel", typeof(RectTransform), typeof(Image));
        papelGo.transform.SetParent(fundo.transform, false);
        var rt = papelGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1000, 680);
        rt.localRotation = Quaternion.Euler(0f, 0f, -2f);
        papelGo.GetComponent<Image>().color = new Color(0.97f, 0.95f, 0.86f);
        var p = papelGo.transform;

        Color azul = new Color(0.15f, 0.25f, 0.75f), rosa = new Color(0.9f, 0.3f, 0.6f), amarelo = new Color(1f, 0.72f, 0.1f);
        Color verde = new Color(0.2f, 0.6f, 0.25f), vermelho = new Color(0.85f, 0.15f, 0.15f), marrom = new Color(0.45f, 0.3f, 0.15f);

        Rotulo(p, "PAPAI, A MOÇA DO EXÉRCITO FALOU QUE A GENTE VAI PRO <b>POSTO DO EXÉRCITO</b>", new Vector2(0, 268), new Vector2(920, 90), 34, azul);
        // sol
        Vector2 sol = new Vector2(-380, 130);
        Circulo(p, sol, 42, 7, amarelo);
        for (int i = 0; i < 8; i++)
        {
            var d = new Vector2(Mathf.Cos(i * Mathf.PI / 4f), Mathf.Sin(i * Mathf.PI / 4f));
            Traco(p, sol + d * 56, sol + d * 82, 6, amarelo);
        }
        // chão
        Traco(p, new Vector2(-470, -200), new Vector2(470, -195), 9, verde);
        // pai (alto, azul)
        Circulo(p, new Vector2(-130, 95), 38, 7, azul);
        Traco(p, new Vector2(-146, 82), new Vector2(-130, 74), 5, azul);
        Traco(p, new Vector2(-130, 74), new Vector2(-114, 82), 5, azul);
        Traco(p, new Vector2(-130, 57), new Vector2(-130, -80), 8, azul);
        Traco(p, new Vector2(-130, -80), new Vector2(-170, -195), 8, azul);
        Traco(p, new Vector2(-130, -80), new Vector2(-90, -195), 8, azul);
        Traco(p, new Vector2(-130, 20), new Vector2(-200, -35), 8, azul);
        Traco(p, new Vector2(-130, 20), new Vector2(-30, -30), 8, azul);
        // Lia (baixinha, rosa, de vestido), segurando a mão do pai
        Circulo(p, new Vector2(40, 5), 30, 7, rosa);
        Traco(p, new Vector2(14, 22), new Vector2(0, -30), 6, marrom);
        Traco(p, new Vector2(66, 22), new Vector2(80, -30), 6, marrom);
        Traco(p, new Vector2(28, -4), new Vector2(40, -10), 5, rosa);
        Traco(p, new Vector2(40, -10), new Vector2(52, -4), 5, rosa);
        Traco(p, new Vector2(40, -25), new Vector2(5, -120), 7, rosa);
        Traco(p, new Vector2(40, -25), new Vector2(75, -120), 7, rosa);
        Traco(p, new Vector2(5, -120), new Vector2(75, -120), 7, rosa);
        Traco(p, new Vector2(28, -120), new Vector2(22, -195), 7, rosa);
        Traco(p, new Vector2(52, -120), new Vector2(58, -195), 7, rosa);
        Traco(p, new Vector2(40, -50), new Vector2(-30, -30), 7, rosa);
        Traco(p, new Vector2(40, -50), new Vector2(105, -85), 7, rosa);
        Rotulo(p, "♥", new Vector2(-45, 120), new Vector2(120, 100), 80, vermelho);
        // posto do exército: barraca com cruz vermelha e bandeira
        Traco(p, new Vector2(240, -197), new Vector2(340, -60), 8, verde);
        Traco(p, new Vector2(340, -60), new Vector2(440, -197), 8, verde);
        Traco(p, new Vector2(305, -197), new Vector2(340, -140), 6, verde);
        Traco(p, new Vector2(340, -140), new Vector2(375, -197), 6, verde);
        Traco(p, new Vector2(322, -100), new Vector2(358, -100), 7, vermelho);
        Traco(p, new Vector2(340, -82), new Vector2(340, -118), 7, vermelho);
        Traco(p, new Vector2(340, -60), new Vector2(340, 40), 6, marrom);
        Traco(p, new Vector2(340, 40), new Vector2(400, 22), 6, verde);
        Traco(p, new Vector2(400, 22), new Vector2(340, 4), 6, verde);
        Rotulo(p, "PAPAI", new Vector2(-130, -240), new Vector2(240, 44), 30, azul);
        Rotulo(p, "EU", new Vector2(40, -240), new Vector2(160, 44), 30, rosa);
        Rotulo(p, "POSTO", new Vector2(340, -240), new Vector2(240, 44), 30, verde);
        Rotulo(p, "VEM ME BUSCAR! TE AMO.  - LIA", new Vector2(0, -295), new Vector2(900, 50), 34, vermelho);

        var dica = CriarTexto(fundo.transform, "DicaDesenho", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -400), new Vector2(900, 40));
        dica.text = "[E] guardar o desenho";
        return fundo.gameObject;
    }

    void Rotulo(Transform pai, string texto, Vector2 pos, Vector2 tam, int tamanho, Color cor)
    {
        var t = CriarTexto(pai, "Rotulo", tamanho, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), pos, tam);
        t.text = texto;
        t.color = cor;
        t.GetComponent<Shadow>().enabled = false;
    }

    // risco de giz de cera entre dois pontos (coordenadas a partir do centro do pai)
    static void Traco(Transform pai, Vector2 a, Vector2 b, float esp, Color cor)
    {
        var go = new GameObject("Traco", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(pai, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 d = b - a;
        rt.anchoredPosition = (a + b) / 2f;
        rt.sizeDelta = new Vector2(d.magnitude + esp * 0.6f, esp);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        var img = go.GetComponent<Image>();
        img.color = cor;
        img.raycastTarget = false;
    }

    static void Circulo(Transform pai, Vector2 c, float r, float esp, Color cor, int lados = 12)
    {
        for (int i = 0; i < lados; i++)
        {
            float a0 = i * 2f * Mathf.PI / lados, a1 = (i + 1) * 2f * Mathf.PI / lados;
            Traco(pai, c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, esp, cor);
        }
    }
}
