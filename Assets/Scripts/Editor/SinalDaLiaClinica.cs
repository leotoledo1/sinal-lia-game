using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Monta o interior da Clinica Central (Dia 2): recepcao, farmacia, consultorio, corredor, enfermaria, isolamento e deposito.
public static partial class SinalDaLiaSetup
{
    // painel de luz fria/esverdeada no teto (algumas piscando)
    static Light LuzFluorescente(Transform pai, Construtor c, Vector3 pos, bool defeito)
    {
        MovelI(pai, "Quaternius_Interior/Light_Ceiling6", new Vector3(pos.x, c.pe - 0.15f, pos.z), 0f, 0.7f, 0, 0f, false);
        return LuzInt(pai, new Vector3(pos.x, c.pe - 0.45f, pos.z), new Color(0.74f, 1f, 0.8f), 2.1f, 7f, defeito ? 0.3f : 0.05f, defeito);
    }

    static ClinicaInterior MontarClinicaInterior(Transform raizInteriores, Vector3 origem, InfoFachada fachada)
    {
        var raiz = new GameObject("Clinica_Interior").transform;
        raiz.SetParent(raizInteriores, false);
        raiz.position = origem;
        var r = new System.Random(77);
        IniciarMalhas("Malhas_Clinica");

        var c = new Construtor
        {
            raiz = raiz,
            pe = 3.0f,
            barra = 1.5f,
            piso = MatI("Clinica_Piso", new Color(0.9f, 0.91f, 0.9f), 0.38f, TexPisoClinica()),
            teto = MatI("Clinica_Teto", new Color(0.86f, 0.88f, 0.86f), 0.05f, TexForro()),
            paredeBaixo = MatI("Clinica_Azulejo", new Color(0.64f, 0.79f, 0.72f), 0.6f, TexAzulejo()),
            paredeCima = MatI("Clinica_ParedeCima", new Color(0.78f, 0.8f, 0.76f), 0.1f, TexTinta()),
            faixa = MatI("Clinica_Faixa", new Color(0.18f, 0.3f, 0.28f), 0.3f),
            batente = MatI("Clinica_Batente", new Color(0.72f, 0.73f, 0.71f), 0.3f),
            uvPiso = 1f, uvParede = 2f, uvTeto = 1.25f, uvParedeBaixo = 0.6f,
        };
        const float E = 0.25f, I = 0.15f;

        // pisos e tetos
        PisoTeto(c, 0f, 0f, 9f, 7f);       // farmácia
        PisoTeto(c, 9f, 0f, 17f, 7f);      // recepção
        PisoTeto(c, 17f, 0f, 26f, 7f);     // consultório
        PisoTeto(c, 0f, 7f, 13f, 10.5f);   // corredor
        PisoTeto(c, 13f, 7f, 26f, 10.5f);
        PisoTeto(c, 0f, 10.5f, 13f, 18f);  // enfermaria
        PisoTeto(c, 13f, 10.5f, 20f, 18f); // isolamento
        PisoTeto(c, 20f, 10.5f, 26f, 18f); // depósito
        PisoTeto(c, 26f, 14.7f, 27.6f, 16.3f); // nicho (esconderijo)

        // paredes externas
        ParedeInt(c, new Vector3(0f, 0f, 0f), new Vector3(26f, 0f, 0f), E,
            Vao.Janela(4.5f), Vao.Janela(10.6f, 1.3f), new Vao { centro = 13f, largura = 1.9f, baseY = 0f, topoY = 2.4f }, Vao.Janela(15.4f, 1.3f), Vao.Janela(22f));
        ParedeInt(c, new Vector3(0f, 0f, 18f), new Vector3(26f, 0f, 18f), E, Vao.Janela(2.5f), Vao.Janela(6.5f), Vao.Janela(10.5f), Vao.Janela(16.5f));
        ParedeInt(c, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 18f), E, Vao.Janela(8.75f, 1.3f));
        // leste: janela no corredor + a abertura do nicho (o armário tampa)
        ParedeInt(c, new Vector3(26f, 0f, 0f), new Vector3(26f, 0f, 18f), E, Vao.Janela(8.75f, 1.3f), new Vao { centro = 15.5f, largura = 1.2f, baseY = 0f, topoY = 2.1f });
        // nicho atrás do armário
        ParedeInt(c, new Vector3(26f, 0f, 14.7f), new Vector3(27.6f, 0f, 14.7f), I);
        ParedeInt(c, new Vector3(26f, 0f, 16.3f), new Vector3(27.6f, 0f, 16.3f), I);
        ParedeInt(c, new Vector3(27.6f, 0f, 14.7f), new Vector3(27.6f, 0f, 16.3f), I);
        // paredes internas
        ParedeInt(c, new Vector3(0f, 0f, 7f), new Vector3(9f, 0f, 7f), I, Vao.Porta(6.8f));
        ParedeInt(c, new Vector3(17f, 0f, 7f), new Vector3(26f, 0f, 7f), I, Vao.Porta(19f - 17f));
        ParedeInt(c, new Vector3(0f, 0f, 10.5f), new Vector3(26f, 0f, 10.5f), I, Vao.Porta(10.8f, 1.25f), Vao.Porta(16f), Vao.Porta(21.4f));
        foreach (float x in new[] { 9f, 17f }) ParedeInt(c, new Vector3(x, 0f, 0f), new Vector3(x, 0f, 7f), I);
        foreach (float x in new[] { 13f, 20f }) ParedeInt(c, new Vector3(x, 0f, 10.5f), new Vector3(x, 0f, 18f), I);
        foreach (float x in new[] { 9f, 17f }) CaixaInt(c, "Pilar", new Vector3(x, c.pe / 2f, 7f), new Vector3(0.4f, c.pe, 0.4f), c.paredeCima, c.uvParede);

        // portas (brancas)
        Vector3 z7 = new Vector3(0f, 0f, 7f), z105 = new Vector3(0f, 0f, 10.5f);
        PortaNoVao(c, "Quaternius_Interior/Door_7", z7, Vector3.right, 6.8f, 1.05f, 2.15f, Abre(Vector3.right, Vector3.back), true);
        PortaNoVao(c, "Quaternius_Interior/Door_7", z7, Vector3.right, 19f, 1.05f, 2.15f, Abre(Vector3.right, Vector3.back, 85f), true);
        PortaNoVao(c, "Quaternius_Interior/Door_7", z105, Vector3.right, 10.8f, 1.25f, 2.15f, Abre(Vector3.right, Vector3.forward), true);
        PortaNoVao(c, "Quaternius_Interior/Door_7", z105, Vector3.right, 16f, 1.05f, 2.15f, Abre(Vector3.right, Vector3.forward, 70f), true);
        PortaNoVao(c, "Quaternius_Interior/Door_7", z105, Vector3.right, 21.4f, 1.05f, 2.15f, Abre(Vector3.right, Vector3.forward, 100f), true);
        MovelI(raiz, "Quaternius_Interior/Door_Double", new Vector3(13f, 0.01f, 0f), 0f, 0f, 1, 0f, true, new Vector3(1.75f, 2.36f, 0.1f));

        // letreiros
        Color fundo = new Color(0.9f, 0.92f, 0.9f), texto = new Color(0.05f, 0.3f, 0.35f);
        void PlacaPorta(string id, string t, float x, float z, Vector3 n) => Letreiro(raiz, id, t, new Vector3(x, 2.42f, z), n, 1.1f, 0.26f, fundo, texto, 512);
        PlacaPorta("farmacia", "FARMÁCIA", 6.8f, 7f + I / 2f + 0.012f, Vector3.forward);
        PlacaPorta("consultorio", "CONSULTÓRIO", 19f, 7f + I / 2f + 0.012f, Vector3.forward);
        PlacaPorta("enfermaria", "ENFERMARIA", 10.8f, 10.5f - I / 2f - 0.012f, Vector3.back);
        PlacaPorta("isolamento", "ISOLAMENTO", 16f, 10.5f - I / 2f - 0.012f, Vector3.back);
        PlacaPorta("deposito", "DEPÓSITO", 21.4f, 10.5f - I / 2f - 0.012f, Vector3.back);
        Letreiro(raiz, "recepcao", "RECEPÇÃO", new Vector3(9f + I / 2f + 0.012f, 2.45f, 4.8f), Vector3.right, 1.4f, 0.3f, fundo, texto, 512);
        Letreiro(raiz, "risco", "RISCO BIOLÓGICO\nNÃO ENTRE", new Vector3(17.15f, 1.55f, 10.5f - I / 2f - 0.02f), Vector3.back, 0.55f, 0.4f, new Color(0.98f, 0.82f, 0.1f), new Color(0.1f, 0.08f, 0.05f), 384);

        // janelas
        Color ceu = new Color(0.66f, 0.72f, 0.72f) * 1.35f, luz = new Color(0.78f, 0.88f, 0.86f);
        JanelaLuz(raiz, new Vector3(4.5f, 1.65f, 0f), Vector3.forward, 1.7f, 1.3f, E, ceu, luz, 3.5f);
        JanelaLuz(raiz, new Vector3(10.6f, 1.65f, 0f), Vector3.forward, 1.3f, 1.3f, E, ceu, luz, 3.2f);
        JanelaLuz(raiz, new Vector3(15.4f, 1.65f, 0f), Vector3.forward, 1.3f, 1.3f, E, ceu, luz, 3.2f);
        JanelaLuz(raiz, new Vector3(22f, 1.65f, 0f), Vector3.forward, 1.7f, 1.3f, E, ceu, luz, 3.5f);
        foreach (float x in new[] { 2.5f, 6.5f, 10.5f, 16.5f }) JanelaLuz(raiz, new Vector3(x, 1.65f, 18f), Vector3.back, 1.7f, 1.3f, E, ceu, luz, 3.8f);
        JanelaLuz(raiz, new Vector3(0f, 1.65f, 8.75f), Vector3.right, 1.3f, 1.3f, E, ceu, luz, 3f);
        JanelaLuz(raiz, new Vector3(26f, 1.65f, 8.75f), Vector3.left, 1.3f, 1.3f, E, ceu, luz, 3f);

        // luzes (fluorescentes esverdeadas + emergência vermelha)
        LuzFluorescente(raiz, c, new Vector3(12f, 0f, 3.5f), false);
        LuzFluorescente(raiz, c, new Vector3(4f, 0f, 8.75f), true);
        LuzFluorescente(raiz, c, new Vector3(13f, 0f, 8.75f), false);
        LuzFluorescente(raiz, c, new Vector3(22f, 0f, 8.75f), true);
        LuzFluorescente(raiz, c, new Vector3(4f, 0f, 14.5f), false);
        LuzFluorescente(raiz, c, new Vector3(9.5f, 0f, 14.5f), true);
        LuzFluorescente(raiz, c, new Vector3(21.5f, 0f, 3.5f), false);
        LuzFluorescente(raiz, c, new Vector3(4.5f, 0f, 3.5f), true);
        LuzFluorescente(raiz, c, new Vector3(16.5f, 0f, 14f), true);
        Color vermelha = new Color(1f, 0.16f, 0.1f);
        LuzEmergencia(raiz, new Vector3(0f + E / 2f + 0.01f, 2.55f, 9.6f), Vector3.right, true, vermelha);
        LuzEmergencia(raiz, new Vector3(23.2f, 2.55f, 10.5f + I / 2f + 0.01f), Vector3.forward, false, vermelha); // dentro do depósito
        LuzEmergencia(raiz, new Vector3(13f, 2.6f, E / 2f + 0.01f), Vector3.forward, false);

        // recepção
        for (int k = 0; k < 3; k++)
            MovelI(raiz, "Quaternius_Interior/Kitchen_Cabinet" + (1 + k % 2), new Vector3(9.9f, 0f, 3.0f + k * 0.7f), 90f, 1.05f);
        MovelI(raiz, "Kenney/computerScreen", new Vector3(9.95f, 1.05f, 3.7f), -90f, 0.42f, 1, 0f, false);
        MovelI(raiz, "Quaternius_Moveis/OfficeChair", new Vector3(9.55f, 0f, 2.15f), 0f, 1.0f, 1, 180f);
        for (int fila = 0; fila < 2; fila++)
            for (int k = 0; k < 4; k++)
            {
                var cad = MovelI(raiz, "Quaternius_Interior/Chair_2", new Vector3(14.0f + k * 0.85f, 0f, 2.4f + fila * 1.5f), 180f, 0.9f);
                if (cad != null && r.NextDouble() < 0.2) Tombar(cad, 0f, 85f);
            }
        MovelI(raiz, "Kenney/televisionModern", new Vector3(17f - I / 2f - 0.08f, 1.9f, 3.0f), -90f, 1.0f, 0, 0f, false);
        MovelI(raiz, "Quaternius_Interior/Houseplant_8", new Vector3(16.4f, 0f, 0.6f), 0f, 1.2f);
        Papeis(raiz, r, 10, 9.5f, 0.5f, 16.5f, 6.5f);
        Sangue(raiz, new Vector3(12.5f, 0f, 5.8f), 1.7f, 20f, r);
        Sangue(raiz, new Vector3(14.0f, 0f, 8.5f), 1.4f, 100f, r);

        // farmácia
        foreach (float z in new[] { 1.4f, 3.2f, 5.0f })
            MovelI(raiz, "Quaternius_Interior/Shelf_Large", new Vector3(E / 2f + 0.2f, 0f, z), 90f, 2.0f);
        for (int k = 0; k < 2; k++)
            MovelI(raiz, "Quaternius_Interior/Kitchen_2Drawers", new Vector3(5.2f + k * 0.7f, 0f, 3.6f), 0f, 1.0f);
        Caixinhas(raiz, r, new Vector3(0.26f, 0f, 0.75f), 30);
        Papeis(raiz, r, 6, 1f, 0.5f, 8.5f, 6.5f);

        // consultório (armário falso: vazio... e um susto)
        MovelI(raiz, "Quaternius_Interior/Bed_Single", new Vector3(17.65f, 0f, 3.2f), 0f, 0.75f);
        MovelI(raiz, "Quaternius_Moveis/Desk", new Vector3(21.2f, 0f, 1.2f), 0f, 1.4f, 0);
        MovelI(raiz, "Quaternius_Moveis/OfficeChair", new Vector3(21.2f, 0f, 2.0f), 180f, 1.0f, 1, 180f);
        MovelI(raiz, "Kenney/laptop", new Vector3(21.2f, 0.73f, 1.15f), 180f, 0.35f, 0, 0f, false);
        MovelI(raiz, "Quaternius_Interior/Bathroom_Sink", new Vector3(23.6f, 0f, 7f - I / 2f - 0.25f), 180f, 0.9f);
        var armFalso = ArmarioMetal(raiz, new Vector3(26f - E / 2f - 0.3f, 0f, 3.6f), -90f, out var portaFalsa);
        var iArmFalso = Interacao(raiz, "armario_consultorio", "[E] Abrir o armário", new Vector3(24.6f, 0f, 3.6f), 1.5f);

        // enfermaria: 4 macas com cortinas; a última toda fechada (o 1º susto)
        var cortinaMat = MatI("Cortina_Hospital", new Color(0.5f, 0.68f, 0.66f), 0.05f);
        var trilho = MatI("Trilho", new Color(0.7f, 0.7f, 0.7f), 0.5f, null, false, 0.8f);
        foreach (float x in new[] { 2.0f, 5.0f, 8.0f, 11.0f })
            MovelI(raiz, "Quaternius_Interior/Bed_Single", new Vector3(x, 0f, 18f - E / 2f - 1.15f), 0f, 0.75f);
        foreach (float x in new[] { 3.5f, 6.5f, 9.5f })
        {
            Bloquinho(raiz, trilho, new Vector3(x, 2.25f, 16.1f), new Vector3(0.03f, 0.03f, 3.6f), "Trilho");
            var cort = Bloquinho(raiz, cortinaMat, new Vector3(x, 1.2f, 16.9f + (float)r.NextDouble() * 0.4f), new Vector3(0.025f, 1.9f, 1.4f), "Cortina");
            GameObjectUtility.SetStaticEditorFlags(cort, FlagsEstatico);
        }
        // cabine fechada da maca 4 (frente = a cortina que abre)
        var cabine = new GameObject("CabineFechada").transform;
        cabine.SetParent(raiz, false);
        Bloquinho(cabine, trilho, new Vector3(11f, 2.25f, 14.5f), new Vector3(1.9f, 0.03f, 0.03f), "Trilho");
        foreach (float x in new[] { 10.05f, 11.95f })
        {
            Bloquinho(cabine, trilho, new Vector3(x, 2.25f, 16.1f), new Vector3(0.03f, 0.03f, 3.2f), "Trilho");
            var lado = Bloquinho(cabine, cortinaMat, new Vector3(x, 1.2f, 16.1f), new Vector3(0.025f, 1.9f, 3.1f), "Cortina");
            lado.AddComponent<BoxCollider>();
        }
        var frente = Bloquinho(raiz, cortinaMat, new Vector3(11f, 1.2f, 14.5f), new Vector3(1.9f, 1.9f, 0.025f), "CortinaFrente");
        frente.AddComponent<BoxCollider>();
        frente.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        var iCortina = Interacao(raiz, "cortina", "[E] Abrir a cortina", new Vector3(11f, 0f, 13.9f), 1.5f);
        foreach (float x in new[] { 3.4f, 9.4f })
            MovelI(raiz, "Kenney/coatRackStanding", new Vector3(x, 0f, 17.3f), 0f, 1.8f, 1, 0f, false); // suporte de soro
        Papeis(raiz, r, 8, 1f, 11f, 12.5f, 14f);
        Sangue(raiz, new Vector3(5.2f, 0f, 13.6f), 1.5f, 160f, r);

        // isolamento
        MovelI(raiz, "Quaternius_Interior/Bed_Single", new Vector3(17.6f, 0f, 18f - E / 2f - 1.15f), 0f, 0.75f);
        var lona = MatI("Lona_Isolamento", new Color(0.62f, 0.72f, 0.78f), 0.5f);
        Bloquinho(raiz, lona, new Vector3(16.35f, 1.15f, 16.5f), new Vector3(0.02f, 2.1f, 2.6f), "Lona").AddComponent<BoxCollider>();
        Bloquinho(raiz, lona, new Vector3(17.6f, 1.15f, 15.15f), new Vector3(2.5f, 2.1f, 0.02f), "Lona").AddComponent<BoxCollider>();
        Papeis(raiz, r, 5, 13.5f, 11f, 19.5f, 14.5f);

        // depósito: prateleiras, caixas e o armário na frente do nicho
        foreach (float z in new[] { 12.0f, 13.8f, 15.6f })
            MovelI(raiz, "Quaternius_Interior/Shelf_Large", new Vector3(20f + I / 2f + 0.2f, 0f, z), 90f, 2.0f);
        Caixinhas(raiz, r, new Vector3(20.21f, 0f, 11.3f), 26);
        for (int k = 0; k < 6; k++)
        {
            var cx = MovelI(raiz, "Kenney/cardboardBoxClosed", new Vector3(22.2f + (k % 3) * 0.55f, k < 3 ? 0f : 0.47f, 17.3f), (float)r.NextDouble() * 20f, 0.46f);
            if (cx != null && k >= 3) Object.DestroyImmediate(cx.GetComponent<BoxCollider>());
        }
        var armario = MovelI(raiz, "Quaternius_Moveis/Closet", new Vector3(26f - E / 2f - 0.31f, 0f, 15.5f), -90f, 0f, 1, 180f, true, new Vector3(1.6f, 2.2f, 0.6f), false);
        armario.name = "ArmarioEsconderijo";
        armario.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        var obsA = armario.AddComponent<NavMeshObstacle>();
        obsA.shape = NavMeshObstacleShape.Box;
        obsA.center = armario.GetComponent<BoxCollider>().center;
        obsA.size = armario.GetComponent<BoxCollider>().size;
        obsA.carving = true;
        var desl = armario.AddComponent<Deslizar>();
        desl.deslocamento = new Vector3(0f, 0f, -1.75f);
        desl.som = GeradorSons.Carregar("impacto_parede");
        var iArmario = Interacao(raiz, "empurrar_armario", "[E] Empurrar o armário", new Vector3(24.9f, 0f, 15.5f), 1.4f);
        // o esconderijo da Lia: cobertor, ursinho e o lampião (a luz vaza pelas frestas do armário)
        var cobertor = MatI("Cobertor_Lia", new Color(0.45f, 0.3f, 0.55f), 0.05f);
        Bloquinho(raiz, cobertor, new Vector3(26.85f, 0.02f, 15.5f), new Vector3(1.1f, 0.04f, 1.2f), "Cobertor");
        MovelI(raiz, "Kenney/bear", new Vector3(27.2f, 0f, 16.0f), -120f, 0.3f, 1, 0f, false);
        var esconderijo = Ponto(raiz, "Esconderijo", new Vector3(26.8f, 0.6f, 15.5f), -90f);

        // pontos do roteiro
        var interior = raiz.gameObject.AddComponent<Interior>();
        interior.nome = "Clínica";
        interior.entrada = Ponto(raiz, "Entrada", new Vector3(13f, 0f, 3.2f), 0f);
        interior.saida = fachada.saida;
        interior.portaFora = fachada.porta;
        interior.portaDentro = Interacao(raiz, "sair_clinica", "[E] Sair da clínica", new Vector3(13f, 0f, 0.75f), 1.5f);
        interior.pontosItens = new[]
        {
            Ponto(raiz, "KitMedico_Farmacia", new Vector3(5.5f, 0.05f, 2.4f)),
            Ponto(raiz, "Comida_Farmacia", new Vector3(3.2f, 0.05f, 5.8f)),
            Ponto(raiz, "Municao_Recepcao", new Vector3(15.8f, 0.05f, 5.5f)),
            Ponto(raiz, "Comida_Consultorio", new Vector3(19.6f, 0.05f, 5.0f)),
        };
        interior.ambienteCeu = new Color(0.05f, 0.068f, 0.06f);
        interior.ambienteMeio = new Color(0.04f, 0.05f, 0.045f);
        interior.ambienteChao = new Color(0.02f, 0.024f, 0.022f);
        interior.corNeblina = new Color(0.012f, 0.018f, 0.016f);
        interior.neblina = 0.03f;
        interior.temperatura = -4f;
        interior.saturacao = -10f;

        var cli = raiz.gameObject.AddComponent<ClinicaInterior>();
        cli.interior = interior;
        cli.esconderijo = esconderijo;
        cli.armario = iArmario;
        cli.armarioMovel = desl;
        cli.falsos = new[] { iCortina, iArmFalso };
        cli.abrirFalso = new[] { frente, portaFalsa.gameObject };
        return cli;
    }

    // zumbis da clínica (depois do bake do NavMesh)
    static void ZumbisClinica(ClinicaInterior cli)
    {
        var raiz = cli.transform;
        var grupo = new GameObject("Zumbis").transform;
        grupo.SetParent(raiz, false);
        ZumbiFixo(grupo, "Comum", new Vector3(2.2f, 0f, 8.7f), 90f, 6f);      // ponta do corredor
        ZumbiFixo(grupo, "Comum", new Vector3(7.4f, 0f, 2.0f), -90f, 5f);     // atrás do balcão da farmácia
        ZumbiFixo(grupo, "Rastejante", new Vector3(5.0f, 0f, 14.6f), 0f, 4.5f); // enfermaria, entre as macas
        ZumbiFixo(grupo, "Gordao", new Vector3(18.6f, 0f, 13.2f), 180f, 5f); // isolamento
        grupo.gameObject.SetActive(false);
        cli.interior.zumbis = grupo.gameObject;
        // sustos dos esconderijos falsos
        var s1 = new GameObject("Susto_Cortina").transform;
        s1.SetParent(raiz, false);
        ZumbiFixo(s1, "Comum", new Vector3(11f, 0f, 15.1f), 180f, 20f);
        s1.gameObject.SetActive(false);
        var s2 = new GameObject("Susto_Armario").transform;
        s2.SetParent(raiz, false);
        ZumbiFixo(s2, "Comum", new Vector3(20.2f, 0f, 5.6f), 120f, 20f); // entre o Marcos e a porta do consultório
        s2.gameObject.SetActive(false);
        cli.sustos = new[] { s1.gameObject, s2.gameObject };
        EditorUtility.SetDirty(cli);
        EditorUtility.SetDirty(cli.interior);
    }

    // armário de metal de consultório (corpo + porta que abre); frente = "yaw"
    static GameObject ArmarioMetal(Transform pai, Vector3 pos, float yaw, out PortaInterior porta)
    {
        var go = new GameObject("ArmarioConsultorio");
        go.transform.SetParent(pai, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var metal = MatI("ArmarioConsultorio", new Color(0.78f, 0.8f, 0.8f), 0.45f, null, false, 0.3f);
        var escuro = MatI("Armario_Linha", new Color(0.06f, 0.08f, 0.09f), 0.2f);
        // caixa aberta na frente (+Z local)
        Bloquinho(go.transform, metal, new Vector3(0f, 1f, -0.27f), new Vector3(0.9f, 2f, 0.04f), "Fundo");
        foreach (float x in new[] { -0.43f, 0.43f }) Bloquinho(go.transform, metal, new Vector3(x, 1f, 0f), new Vector3(0.04f, 2f, 0.56f), "Lado");
        Bloquinho(go.transform, metal, new Vector3(0f, 1.98f, 0f), new Vector3(0.9f, 0.04f, 0.56f), "Topo");
        Bloquinho(go.transform, metal, new Vector3(0f, 0.04f, 0f), new Vector3(0.9f, 0.08f, 0.56f), "Base");
        Bloquinho(go.transform, escuro, new Vector3(0f, 1f, -0.24f), new Vector3(0.82f, 1.9f, 0.01f), "Escuro");
        foreach (float y in new[] { 0.7f, 1.3f }) Bloquinho(go.transform, metal, new Vector3(0f, y, 0f), new Vector3(0.82f, 0.02f, 0.5f), "Prateleira");
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 1f, 0f);
        col.size = new Vector3(0.9f, 2f, 0.56f);
        // porta (dobradiça na esquerda)
        var dob = new GameObject("Porta");
        dob.transform.SetParent(go.transform, false);
        dob.transform.localPosition = new Vector3(-0.44f, 0f, 0.29f);
        Bloquinho(dob.transform, metal, new Vector3(0.44f, 1f, 0f), new Vector3(0.88f, 1.92f, 0.03f), "Folha");
        Bloquinho(dob.transform, escuro, new Vector3(0.8f, 1.05f, 0.03f), new Vector3(0.03f, 0.18f, 0.03f), "Puxador");
        porta = dob.AddComponent<PortaInterior>();
        porta.anguloAberta = -110f;
        porta.duracao = 0.35f;
        porta.som = GeradorSons.Carregar("impacto_parede");
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            if (!t.IsChildOf(dob.transform)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, FlagsEstatico);
        return go;
    }

    // caixinhas de remédio nas prateleiras (a partir de "ini": fileiras subindo)
    static void Caixinhas(Transform pai, System.Random r, Vector3 ini, int n)
    {
        var cores = new[] { MatI("Remedio_Branco", new Color(0.88f, 0.88f, 0.86f), 0.2f), MatI("Remedio_Azul", new Color(0.3f, 0.48f, 0.75f), 0.2f),
                            MatI("Remedio_Vermelho", new Color(0.75f, 0.2f, 0.18f), 0.2f), MatI("Remedio_Verde", new Color(0.3f, 0.6f, 0.4f), 0.2f) };
        var grupo = new GameObject("Remedios").transform;
        grupo.SetParent(pai, false);
        for (int i = 0; i < n; i++)
        {
            Vector3 p = ini + new Vector3((float)r.NextDouble() * 0.06f, 0.45f + (i % 4) * 0.48f, (i / 4) * 0.62f + (float)r.NextDouble() * 0.2f);
            var b = Bloquinho(grupo, cores[r.Next(cores.Length)], p, new Vector3(0.12f, 0.08f + (float)r.NextDouble() * 0.08f, 0.16f), "Remedio");
            b.transform.localRotation = Quaternion.Euler(0f, ((float)r.NextDouble() - 0.5f) * 30f, 0f);
            GameObjectUtility.SetStaticEditorFlags(b, FlagsEstatico);
        }
    }
}
