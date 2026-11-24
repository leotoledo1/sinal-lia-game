using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Monta a fachada e o interior da Escola (Dia 1): saguao, secretaria, banheiro, sala dos professores, corredor e 4 salas de aula.
public static partial class SinalDaLiaSetup
{
    internal const string FACHADAS = "Fachadas_v14";

    struct InfoFachada
    {
        public Interativo porta;
        public Transform saida;
        public bool achouParede;
    }

    // fachada (porta da rua)

    // procura uma parede de prédio lisa perto do marcador (com calçada livre na frente) e monta a fachada nela
    static InfoFachada MontarFachada(Transform cidade, Vector3 alvo, string nome, string letreiro, Color corPainel, Color fundoLetreiro, Color corLetreiro, bool clinica)
    {
        var raizF = cidade.Find(FACHADAS);
        if (raizF == null)
        {
            raizF = new GameObject(FACHADAS).transform;
            raizF.SetParent(cidade, false);
        }
        Physics.SyncTransforms();
        var cenario = cidade.Find(CENARIO);
        var limites = cidade.Find("Limites_v01");
        bool Predio(Collider c) => c != null && c.transform.IsChildOf(cidade) && c.bounds.size.y > 3f
                                   && (cenario == null || !c.transform.IsChildOf(cenario)) && (limites == null || !c.transform.IsChildOf(limites))
                                   && !c.transform.IsChildOf(raizF);
        float melhor = float.MaxValue;
        Vector3 pos = alvo + Vector3.forward * 7f, normal = Vector3.back;
        bool achou = false;
        for (int k = 0; k < 36; k++)
        {
            Vector3 d = Quaternion.Euler(0f, k * 10f, 0f) * Vector3.forward;
            if (!Physics.Raycast(alvo + Vector3.up * 1.4f, d, out var h, 24f, ~0, QueryTriggerInteraction.Ignore) || !Predio(h.collider)) continue;
            Vector3 n = h.normal;
            n.y = 0f;
            if (n.sqrMagnitude < 0.8f) continue;
            n.Normalize();
            // parede lisa na largura da fachada (sem quina nem coisa saindo)
            Vector3 tan = Vector3.Cross(Vector3.up, n);
            bool lisa = true;
            foreach (float s in new[] { -2.3f, 0f, 2.3f })
                foreach (float alt in new[] { 0.4f, 2.6f })
                {
                    Vector3 o = new Vector3(h.point.x, alvo.y + alt, h.point.z) + n * 0.8f + tan * s;
                    if (!Physics.Raycast(o, -n, out var h2, 1.6f, ~0, QueryTriggerInteraction.Ignore) || h2.collider != h.collider
                        || Mathf.Abs(Vector3.Dot(h2.point - h.point, n)) > 0.12f) lisa = false;
                }
            if (!lisa) continue;
            // calçada livre na frente da porta (nada do cenário no caminho)
            Vector3 frente = new Vector3(h.point.x, alvo.y, h.point.z) + n * 1.7f;
            if (Physics.CheckBox(frente + Vector3.up * 1.3f, new Vector3(2.5f, 1.05f, 1.35f), Quaternion.LookRotation(n), ~0, QueryTriggerInteraction.Ignore)) continue;
            float dist = Vector3.Distance(new Vector3(h.point.x, 0f, h.point.z), new Vector3(alvo.x, 0f, alvo.z));
            if (dist < melhor)
            {
                melhor = dist;
                pos = h.point;
                normal = n;
                achou = true;
            }
        }
        if (!achou) Debug.LogWarning($"[Sinal da Lia] Fachada {nome}: não achei parede livre perto do marcador; ficou solta");
        // chão de verdade na frente da parede
        float chaoY = alvo.y;
        if (Physics.Raycast(pos + normal * 1.2f + Vector3.up * 3f, Vector3.down, out var hc, 8f, ~0, QueryTriggerInteraction.Ignore)) chaoY = hc.point.y;

        var raiz = new GameObject("Fachada_" + nome).transform;
        raiz.SetParent(raizF, false);
        raiz.position = new Vector3(pos.x, chaoY, pos.z) + normal * 0.02f;
        raiz.rotation = Quaternion.LookRotation(normal);
        var painel = MatI(nome + "_Painel", corPainel, 0.1f, TexTinta());
        var concreto = MatI("Fachada_Concreto", new Color(0.42f, 0.42f, 0.4f), 0.1f, TexTinta());
        var escuro = MatI("Fachada_Escuro", new Color(0.03f, 0.03f, 0.035f), 0f);
        GameObject Bloco(Material m, Vector3 p, Vector3 t, bool col = true)
        {
            var b = Bloquinho(raiz, m, p, t, "Fachada");
            if (col) b.AddComponent<BoxCollider>();
            return b;
        }
        Bloco(painel, new Vector3(0f, 1.9f, 0.1f), new Vector3(4.8f, 3.8f, 0.2f));
        Bloco(escuro, new Vector3(0f, 1.22f, 0.205f), new Vector3(1.95f, 2.42f, 0.02f), false);
        var porta = MovelI(raiz, "Quaternius_Interior/Door_Double", new Vector3(0f, 0.02f, 0.25f), 0f, 0f, 1, 0f, true, new Vector3(1.85f, 2.35f, 0.1f));
        foreach (float x in new[] { -1.0f, 1.0f }) Bloco(concreto, new Vector3(x, 1.25f, 0.28f), new Vector3(0.16f, 2.5f, 0.18f));
        Bloco(concreto, new Vector3(0f, 2.52f, 0.28f), new Vector3(2.16f, 0.16f, 0.18f));
        Bloco(concreto, new Vector3(0f, 3.5f, 0.75f), new Vector3(5.0f, 0.14f, 1.3f)); // marquise
        Bloco(concreto, new Vector3(0f, 0.06f, 0.7f), new Vector3(3.2f, 0.12f, 1.0f));  // degrau
        Letreiro(raiz, "fachada_" + nome.ToLower(), letreiro, new Vector3(0f, 3.0f, 0.215f), Vector3.forward, 4.0f, 0.62f, fundoLetreiro, corLetreiro, 1024);
        if (clinica)
        {
            var vermelho = MatI("CruzVermelha", new Color(0.75f, 0.06f, 0.05f), 0.2f);
            foreach (float x in new[] { -1.75f, 1.75f })
            {
                Bloco(vermelho, new Vector3(x, 1.75f, 0.215f), new Vector3(0.7f, 0.22f, 0.02f), false);
                Bloco(vermelho, new Vector3(x, 1.75f, 0.215f), new Vector3(0.22f, 0.7f, 0.02f), false);
            }
        }
        else
        {
            // pintura de escola: faixa colorida na base do painel
            Bloco(MatI("Fachada_Faixa_Escola", new Color(0.2f, 0.45f, 0.62f), 0.2f), new Vector3(0f, 0.45f, 0.205f), new Vector3(4.8f, 0.9f, 0.02f), false);
        }
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, FlagsEstatico);
        var i = Interacao(raiz, clinica ? "entrar_clinica" : "entrar_escola", clinica ? "[E] Entrar na clínica" : "[E] Entrar na escola", new Vector3(0f, 0f, 1.25f), 2.1f);
        var saida = Ponto(raiz, "Saida", new Vector3(0f, 0f, 2.5f));
        return new InfoFachada { porta = i, saida = saida, achouParede = achou };
    }

    // prefabs simples da escola

    // carteira escolar (tampo de madeira + estrutura azul) com a cadeira atrás; frente = +Z
    static GameObject PrefabCarteira()
    {
        var t = new GameObject("tmp").transform;
        var tampo = MatI("Carteira_Tampo", new Color(0.7f, 0.58f, 0.4f), 0.3f);
        var metal = MatI("Carteira_Metal", new Color(0.13f, 0.22f, 0.4f), 0.4f, null, false, 0.4f);
        var assento = MatI("Carteira_Assento", new Color(0.15f, 0.3f, 0.58f), 0.3f);
        Bloquinho(t, tampo, new Vector3(0f, 0.72f, 0f), new Vector3(0.6f, 0.03f, 0.45f));
        Bloquinho(t, metal, new Vector3(0f, 0.58f, 0.02f), new Vector3(0.54f, 0.02f, 0.36f)); // porta-livros
        foreach (float x in new[] { -0.27f, 0.27f })
            foreach (float z in new[] { -0.19f, 0.19f })
                Bloquinho(t, metal, new Vector3(x, 0.35f, z), new Vector3(0.03f, 0.7f, 0.03f));
        foreach (float x in new[] { -0.27f, 0.27f }) Bloquinho(t, metal, new Vector3(x, 0.03f, 0f), new Vector3(0.03f, 0.03f, 0.41f));
        // cadeira
        Bloquinho(t, assento, new Vector3(0f, 0.44f, -0.42f), new Vector3(0.4f, 0.03f, 0.38f));
        Bloquinho(t, assento, new Vector3(0f, 0.75f, -0.6f), new Vector3(0.4f, 0.26f, 0.03f));
        foreach (float x in new[] { -0.18f, 0.18f })
        {
            foreach (float z in new[] { -0.26f, -0.58f }) Bloquinho(t, metal, new Vector3(x, 0.22f, z), new Vector3(0.025f, 0.44f, 0.025f));
            Bloquinho(t, metal, new Vector3(x, 0.6f, -0.6f), new Vector3(0.025f, 0.32f, 0.025f));
        }
        return CombinarPrefab(t.gameObject, "Carteira");
    }

    // 4 armários de aço lado a lado (2,4 m); frente = +Z
    static GameObject PrefabArmarios()
    {
        var t = new GameObject("tmp").transform;
        var corpo = MatI("Armario_Corpo", new Color(0.25f, 0.37f, 0.4f), 0.35f, null, false, 0.3f);
        var linha = MatI("Armario_Linha", new Color(0.06f, 0.08f, 0.09f), 0.2f);
        var puxador = MatI("Armario_Puxador", new Color(0.62f, 0.62f, 0.6f), 0.6f, null, false, 0.8f);
        Bloquinho(t, corpo, new Vector3(0f, 0.95f, 0f), new Vector3(2.4f, 1.9f, 0.45f));
        for (int k = 0; k < 4; k++)
        {
            float x = -0.9f + k * 0.6f;
            if (k > 0) Bloquinho(t, linha, new Vector3(x - 0.3f, 0.95f, 0.226f), new Vector3(0.012f, 1.86f, 0.01f));
            for (int v = 0; v < 4; v++) Bloquinho(t, linha, new Vector3(x, 1.72f - v * 0.045f, 0.226f), new Vector3(0.32f, 0.014f, 0.01f)); // ventilação
            Bloquinho(t, puxador, new Vector3(x + 0.2f, 1.05f, 0.24f), new Vector3(0.03f, 0.14f, 0.03f));
            Bloquinho(t, linha, new Vector3(x, 1.4f, 0.226f), new Vector3(0.12f, 0.06f, 0.01f)); // plaquinha do número
        }
        Bloquinho(t, linha, new Vector3(0f, 0.05f, 0.226f), new Vector3(2.4f, 0.1f, 0.01f));
        return CombinarPrefab(t.gameObject, "Armarios");
    }

    // mochila da Lia (rosa, com o chaveirinho de coelho), deitada no chão
    static GameObject PrefabMochila()
    {
        var t = new GameObject("tmp").transform;
        var rosa = MatI("Mochila_Rosa", new Color(0.86f, 0.26f, 0.5f), 0.25f);
        var escuro = MatI("Mochila_RosaEscuro", new Color(0.5f, 0.1f, 0.3f), 0.2f);
        var branco = MatI("Mochila_Coelho", new Color(0.95f, 0.94f, 0.92f), 0.2f);
        var r = new GameObject("Deitada").transform;
        r.SetParent(t, false);
        r.localRotation = Quaternion.Euler(-90f, 0f, 0f); // costas no chão
        r.localPosition = new Vector3(0f, 0.1f, 0f);
        Bloquinho(r, rosa, new Vector3(0f, 0f, 0f), new Vector3(0.33f, 0.42f, 0.18f));
        Bloquinho(r, escuro, new Vector3(0f, -0.07f, 0.11f), new Vector3(0.27f, 0.2f, 0.07f));
        Bloquinho(r, escuro, new Vector3(0f, 0.22f, 0f), new Vector3(0.12f, 0.03f, 0.04f));
        foreach (float x in new[] { -0.1f, 0.1f }) Bloquinho(r, escuro, new Vector3(x, 0f, -0.1f), new Vector3(0.05f, 0.36f, 0.02f));
        // coelhinho pendurado no zíper
        Bloquinho(r, branco, new Vector3(0.12f, 0.12f, 0.16f), new Vector3(0.07f, 0.07f, 0.06f), "Coelho", PrimitiveType.Sphere);
        foreach (float x in new[] { 0.1f, 0.14f }) Bloquinho(r, branco, new Vector3(x, 0.18f, 0.16f), new Vector3(0.018f, 0.06f, 0.018f), "Orelha", PrimitiveType.Capsule);
        // estojo e caderno que caíram
        Bloquinho(t, MatI("Caderno_Azul", new Color(0.2f, 0.35f, 0.7f), 0.2f), new Vector3(0.34f, 0.01f, 0.12f), new Vector3(0.21f, 0.02f, 0.28f)).transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
        Bloquinho(t, MatI("Estojo", new Color(0.95f, 0.75f, 0.15f), 0.2f), new Vector3(-0.28f, 0.03f, 0.2f), new Vector3(0.2f, 0.05f, 0.07f)).transform.localRotation = Quaternion.Euler(0f, -40f, 0f);
        return CombinarPrefab(t.gameObject, "MochilaLia");
    }

    static GameObject PrefabBanco()
    {
        var t = new GameObject("tmp").transform;
        var madeira = MatI("Banco_Madeira", new Color(0.42f, 0.3f, 0.19f), 0.2f);
        var metal = MatI("Carteira_Metal", new Color(0.13f, 0.22f, 0.4f), 0.4f, null, false, 0.4f);
        Bloquinho(t, madeira, new Vector3(0f, 0.45f, 0f), new Vector3(1.7f, 0.05f, 0.36f));
        foreach (float x in new[] { -0.7f, 0.7f }) Bloquinho(t, metal, new Vector3(x, 0.22f, 0f), new Vector3(0.05f, 0.44f, 0.3f));
        return CombinarPrefab(t.gameObject, "BancoEscola");
    }

    // vira o objeto de lado (caído) e apoia no chão
    static void Tombar(GameObject go, float giroX, float giroZ)
    {
        go.transform.localRotation = go.transform.localRotation * Quaternion.Euler(giroX, 0f, giroZ);
        var b = Limites(go);
        go.transform.position += Vector3.up * (go.transform.parent.position.y - b.min.y);
    }

    // papéis espalhados pelo chão (sem colisor)
    static void Papeis(Transform pai, System.Random r, int n, float x0, float z0, float x1, float z1)
    {
        var mats = new[] { MatI("Papel", new Color(0.86f, 0.85f, 0.8f), 0.05f), MatI("Papel_Amarelo", new Color(0.9f, 0.82f, 0.45f), 0.05f), MatI("Papel_Azul", new Color(0.55f, 0.7f, 0.85f), 0.05f) };
        for (int i = 0; i < n; i++)
        {
            var p = Bloquinho(pai, mats[r.Next(10) < 7 ? 0 : 1 + r.Next(2)],
                new Vector3(Mathf.Lerp(x0, x1, (float)r.NextDouble()), 0.004f + i * 0.0003f, Mathf.Lerp(z0, z1, (float)r.NextDouble())), new Vector3(0.21f, 0.002f, 0.29f), "Papel");
            p.transform.localRotation = Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f);
            p.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(p, FlagsEstatico);
        }
    }

    // mancha de sangue do pacote Post Apocalypse no chão (ou na parede)
    static void Sangue(Transform pai, Vector3 pos, float tam, float yaw, System.Random r)
    {
        if (!TemQuaternius) return;
        var s = InstQ(r.Next(2) == 0 ? "Blood/Blood_3.fbx" : "Blood Splat/Blood_" + (1 + r.Next(2)) + ".fbx", tam, pai, false);
        if (s == null) return;
        s.name = "Sangue";
        s.transform.localPosition = pos + Vector3.up * 0.006f;
        s.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        foreach (var rd in s.GetComponentsInChildren<Renderer>()) rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // a escola

    static EscolaInterior MontarEscolaInterior(Transform raizInteriores, Vector3 origem, InfoFachada fachada, System.Collections.Generic.List<GameObject> paraZumbis)
    {
        var raiz = new GameObject("Escola_Interior").transform;
        raiz.SetParent(raizInteriores, false);
        raiz.position = origem;
        var r = new System.Random(2024);
        IniciarMalhas("Malhas_Escola");

        var c = new Construtor
        {
            raiz = raiz,
            pe = 3.2f,
            barra = 1.25f,
            piso = MatI("Escola_Piso", new Color(0.95f, 0.95f, 0.93f), 0.3f, TexGranilite()),
            teto = MatI("Escola_Teto", new Color(0.86f, 0.86f, 0.84f), 0.05f, TexForro()),
            paredeBaixo = MatI("Escola_ParedeBaixo", new Color(0.22f, 0.4f, 0.4f), 0.35f, TexTinta()),
            paredeCima = MatI("Escola_ParedeCima", new Color(0.8f, 0.76f, 0.66f), 0.08f, TexTinta()),
            faixa = MatI("Escola_Faixa", new Color(0.1f, 0.17f, 0.18f), 0.3f),
            batente = MatI("Escola_Batente", new Color(0.33f, 0.24f, 0.16f), 0.25f),
            uvPiso = 1f, uvParede = 2f, uvTeto = 1.25f,
        };
        const float E = 0.25f, I = 0.15f;

        // pisos e tetos (um por cômodo: cada piso recebe as luzes do seu cômodo)
        PisoTeto(c, 0f, 0f, 8f, 8f);       // professores
        PisoTeto(c, 8f, 0f, 12.5f, 8f);    // banheiro
        PisoTeto(c, 12.5f, 0f, 21.5f, 8f); // saguão
        PisoTeto(c, 21.5f, 0f, 34f, 8f);   // secretaria
        PisoTeto(c, 0f, 8f, 17f, 12f);     // corredor (2 pedaços)
        PisoTeto(c, 17f, 8f, 34f, 12f);
        float[] salasX = { 0f, 8.5f, 17f, 25.5f, 34f };
        for (int k = 0; k < 4; k++) PisoTeto(c, salasX[k], 12f, salasX[k + 1], 20f);

        // paredes externas
        ParedeInt(c, new Vector3(0f, 0f, 0f), new Vector3(34f, 0f, 0f), E,
            Vao.Janela(4f), Vao.Janela(14.6f, 1.4f), new Vao { centro = 17f, largura = 1.9f, baseY = 0f, topoY = 2.4f }, Vao.Janela(19.4f, 1.4f),
            Vao.Janela(25.5f), Vao.Janela(30.5f));
        float[] janelasNorte = { 2.25f, 6.25f, 10.75f, 14.75f, 19.25f, 23.25f, 27.75f, 31.75f };
        ParedeInt(c, new Vector3(0f, 0f, 20f), new Vector3(34f, 0f, 20f), E, janelasNorte.Select(x => Vao.Janela(x)).ToArray());
        ParedeInt(c, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 20f), E, Vao.Janela(10f, 1.4f));
        ParedeInt(c, new Vector3(34f, 0f, 0f), new Vector3(34f, 0f, 20f), E, Vao.Janela(10f, 1.4f));
        // paredes internas
        ParedeInt(c, new Vector3(0f, 0f, 8f), new Vector3(12.5f, 0f, 8f), I, Vao.Porta(5.5f), Vao.Porta(10.2f));
        ParedeInt(c, new Vector3(21.5f, 0f, 8f), new Vector3(34f, 0f, 8f), I, Vao.Porta(24.5f - 21.5f));
        ParedeInt(c, new Vector3(0f, 0f, 12f), new Vector3(34f, 0f, 12f), I, Vao.Porta(6.5f), Vao.Porta(15f), Vao.Porta(23.5f), Vao.Porta(32f));
        foreach (float x in new[] { 8f, 12.5f, 21.5f }) ParedeInt(c, new Vector3(x, 0f, 0f), new Vector3(x, 0f, 8f), I);
        foreach (float x in new[] { 8.5f, 17f, 25.5f }) ParedeInt(c, new Vector3(x, 0f, 12f), new Vector3(x, 0f, 20f), I);
        // pilares na boca do saguão
        foreach (float x in new[] { 12.5f, 21.5f }) CaixaInt(c, "Pilar", new Vector3(x, c.pe / 2f, 8f), new Vector3(0.45f, c.pe, 0.45f), c.paredeCima, c.uvParede);

        // portas
        Vector3 x8 = new Vector3(0f, 0f, 8f), x12 = new Vector3(0f, 0f, 12f);
        PortaNoVao(c, "Quaternius_Interior/Door_2", x8, Vector3.right, 5.5f, 1.05f, 2.15f, Abre(Vector3.right, Vector3.back, 165f), true);
        PortaNoVao(c, "Quaternius_Interior/Door_7", x8, Vector3.right, 10.2f, 1.05f, 2.15f, 0f, false); // banheiro: fechado
        PortaNoVao(c, "Quaternius_Interior/Door_2", x8, Vector3.right, 24.5f, 1.05f, 2.15f, Abre(Vector3.right, Vector3.back, 165f), true);
        PortaNoVao(c, "Quaternius_Interior/Door_6", x12, Vector3.right, 6.5f, 1.05f, 2.15f, Abre(Vector3.right, Vector3.forward, 165f), true);
        PortaNoVao(c, "Quaternius_Interior/Door_6", x12, Vector3.right, 15f, 1.05f, 2.15f, Abre(Vector3.right, Vector3.forward, 160f), true);
        var porta4B = PortaNoVao(c, "Quaternius_Interior/Door_6", x12, Vector3.right, 23.5f, 1.05f, 2.15f, Abre(Vector3.right, Vector3.forward, 168f), false, true);
        PortaNoVao(c, "Quaternius_Interior/Door_6", x12, Vector3.right, 32f, 1.05f, 2.15f, Abre(Vector3.right, Vector3.forward, 170f), true);
        porta4B.som = GeradorSons.Carregar("recarga");
        // porta da frente (dupla, fechada: é a saída, com E)
        MovelI(raiz, "Quaternius_Interior/Door_Double", new Vector3(17f, 0.01f, 0f), 0f, 0f, 1, 0f, true, new Vector3(1.75f, 2.36f, 0.1f));

        // letreiros das portas
        Color placaFundo = new Color(0.86f, 0.84f, 0.76f), placaTexto = new Color(0.08f, 0.12f, 0.22f);
        void PlacaPorta(string id, string texto, float x, float z, Vector3 n) =>
            Letreiro(raiz, id, texto, new Vector3(x, 2.48f, z), n, 1.0f, 0.26f, placaFundo, placaTexto, 512);
        PlacaPorta("sala_3a", "3º ANO A", 6.5f, 12f - I / 2f - 0.012f, Vector3.back);
        PlacaPorta("sala_4a", "4º ANO A", 15f, 12f - I / 2f - 0.012f, Vector3.back);
        PlacaPorta("sala_4b", "4º ANO B", 23.5f, 12f - I / 2f - 0.012f, Vector3.back);
        PlacaPorta("sala_5a", "5º ANO A", 32f, 12f - I / 2f - 0.012f, Vector3.back);
        PlacaPorta("professores", "PROFESSORES", 5.5f, 8f + I / 2f + 0.012f, Vector3.forward);
        PlacaPorta("banheiro", "BANHEIRO", 10.2f, 8f + I / 2f + 0.012f, Vector3.forward);
        PlacaPorta("secretaria", "SECRETARIA", 24.5f, 8f + I / 2f + 0.012f, Vector3.forward);
        Letreiro(raiz, "interditado", "INTERDITADO", new Vector3(10.2f, 1.45f, 8f + I / 2f + 0.07f), Vector3.forward, 0.6f, 0.3f, new Color(0.95f, 0.93f, 0.85f), new Color(0.7f, 0.05f, 0.05f), 384);
        Letreiro(raiz, "bemvindos", "BEM-VINDOS DE VOLTA!", new Vector3(17.1f, 2.8f, E / 2f + 0.012f), Vector3.forward, 2.1f, 0.34f, new Color(0.95f, 0.85f, 0.35f), new Color(0.12f, 0.2f, 0.5f), 1024);

        // janelas com luz fria entrando
        Color ceuFrio = new Color(0.62f, 0.68f, 0.76f) * 1.35f, luzFria = new Color(0.72f, 0.8f, 0.95f);
        Color ceuQuente = new Color(0.78f, 0.74f, 0.66f) * 1.35f, luzQuente = new Color(0.95f, 0.86f, 0.72f);
        foreach (float x in janelasNorte) JanelaLuz(raiz, new Vector3(x, 1.65f, 20f), Vector3.back, 1.7f, 1.3f, E, ceuFrio, luzFria, 4f);
        JanelaLuz(raiz, new Vector3(4f, 1.65f, 0f), Vector3.forward, 1.7f, 1.3f, E, ceuQuente, luzQuente, 3.5f);
        JanelaLuz(raiz, new Vector3(14.6f, 1.65f, 0f), Vector3.forward, 1.4f, 1.3f, E, ceuQuente, luzQuente, 3.2f);
        JanelaLuz(raiz, new Vector3(19.4f, 1.65f, 0f), Vector3.forward, 1.4f, 1.3f, E, ceuQuente, luzQuente, 3.2f);
        JanelaLuz(raiz, new Vector3(25.5f, 1.65f, 0f), Vector3.forward, 1.7f, 1.3f, E, ceuQuente, luzQuente, 3.5f);
        JanelaLuz(raiz, new Vector3(30.5f, 1.65f, 0f), Vector3.forward, 1.7f, 1.3f, E, ceuQuente, luzQuente, 3.5f);
        JanelaLuz(raiz, new Vector3(0f, 1.65f, 10f), Vector3.right, 1.4f, 1.3f, E, ceuFrio, luzFria, 3.5f);
        JanelaLuz(raiz, new Vector3(34f, 1.65f, 10f), Vector3.left, 1.4f, 1.3f, E, ceuFrio, luzFria, 3.5f);

        // luzes de emergência (algumas falhando)
        var emergencia = new List<Light>
        {
            LuzEmergencia(raiz, new Vector3(15.6f, 2.62f, E / 2f + 0.01f), Vector3.forward, false),
            LuzEmergencia(raiz, new Vector3(21.5f - I / 2f - 0.01f, 2.6f, 3f), Vector3.left, false),
            LuzEmergencia(raiz, new Vector3(11f, 2.78f, 12f - I / 2f - 0.01f), Vector3.back, false),
            LuzEmergencia(raiz, new Vector3(28f, 2.78f, 12f - I / 2f - 0.01f), Vector3.back, true),
            LuzEmergencia(raiz, new Vector3(3f, 2.78f, 8f + I / 2f + 0.01f), Vector3.forward, false),
            LuzEmergencia(raiz, new Vector3(2f, 2.6f, 8f - I / 2f - 0.01f), Vector3.back, true),
            LuzEmergencia(raiz, new Vector3(28f, 2.6f, 8f - I / 2f - 0.01f), Vector3.back, false),
        };
        // a luz de cada sala fica ao lado da porta, por dentro
        for (int k = 0; k < 4; k++)
            emergencia.Add(LuzEmergencia(raiz, new Vector3(salasX[k] + 5.1f, 2.6f, 12f + I / 2f + 0.01f), Vector3.forward, k == 1));

        // saguão
        var cortica = MatI("Mural_Cortica", Color.white, 0.05f, TexCortica());
        var mural = Placa(raiz, new Vector3(12.5f + I / 2f + 0.02f, 1.6f, 4.5f), Vector3.right, 2.6f, 1.2f, cortica, "Mural", 0.03f);
        Placa(raiz, new Vector3(12.5f + I / 2f + 0.01f, 1.6f, 4.5f), Vector3.right, 2.72f, 1.32f, c.batente, "MolduraMural", 0.02f);
        var coresPapel = new[] { new Color(0.95f, 0.9f, 0.5f), new Color(0.6f, 0.82f, 0.95f), new Color(0.95f, 0.65f, 0.7f), new Color(0.75f, 0.92f, 0.65f) };
        for (int k = 0; k < 7; k++)
        {
            var p = Placa(raiz, new Vector3(12.5f + I / 2f + 0.04f, 1.25f + (k % 2) * 0.6f + (float)r.NextDouble() * 0.1f, 3.5f + k * 0.32f), Vector3.right, 0.24f, 0.3f,
                MatI("PapelMural_" + (k % 4), coresPapel[k % 4], 0.05f), "PapelMural", 0.004f);
            p.transform.localRotation *= Quaternion.Euler(0f, 0f, ((float)r.NextDouble() - 0.5f) * 14f);
        }
        Letreiro(raiz, "aviso_evacuacao", "AVISO\nEVACUAÇÃO DA ESCOLA\nOs alunos serão levados\npelo exército.\nNÃO VOLTEM.", new Vector3(12.5f + I / 2f + 0.05f, 1.7f, 5.35f), Vector3.right, 0.6f, 0.7f, new Color(0.97f, 0.96f, 0.92f), new Color(0.1f, 0.1f, 0.12f), 512);
        var banco = PrefabBanco();
        Por(banco, raiz, new Vector3(21.5f - I / 2f - 0.3f, 0f, 2.6f), 90f);
        Por(banco, raiz, new Vector3(21.5f - I / 2f - 0.3f, 0f, 5.4f), 90f);
        MovelI(raiz, "Quaternius_Interior/Houseplant_8", new Vector3(13.2f, 0f, 0.7f), 0f, 1.2f);
        var lixo = MovelI(raiz, "Quaternius_Interior/Trashcan_Cylindric", new Vector3(20.6f, 0f, 7.2f), 30f, 0.6f);
        if (lixo != null) Tombar(lixo, 90f, 0f);
        var cadeiraCaida = MovelI(raiz, "Quaternius_Moveis/Chair", new Vector3(15.2f, 0f, 5.6f), 120f, 0.92f, 1, 180f);
        if (cadeiraCaida != null) Tombar(cadeiraCaida, 0f, 85f);
        Papeis(raiz, r, 14, 13f, 0.5f, 21f, 7.5f);
        Sangue(raiz, new Vector3(18.6f, 0f, 5.6f), 1.6f, 40f, r);
        foreach (float x in new[] { 15.6f, 18.4f })
            MovelI(raiz, "Quaternius_Interior/Light_Ceiling5", new Vector3(x, c.pe - 0.16f, 4f), 0f, 0.6f, 0, 0f, false);

        // corredor
        var armarios = PrefabArmarios();
        foreach (float x in new[] { 1.5f, 3.9f, 8.8f, 11.2f, 17.9f, 20.3f, 25.8f, 28.2f })
            Por(armarios, raiz, new Vector3(x, 0f, 12f - I / 2f - 0.235f), 180f);
        foreach (float x in new[] { 2.4f, 7.85f, 26.7f, 29.1f, 31.5f })
            Por(armarios, raiz, new Vector3(x, 0f, 8f + I / 2f + 0.235f), 0f);
        var mochila = Por(PrefabMochila(), raiz, new Vector3(16.1f, 0f, 10.3f), 30f);
        LuzInt(raiz, new Vector3(16.1f, 0.6f, 10.3f), new Color(1f, 0.85f, 0.9f), 0.7f, 1.6f);
        var iMochila = Interacao(raiz, "mochila", "[E] Examinar a mochila", new Vector3(16.1f, 0f, 10.3f), 1.6f);
        Papeis(raiz, r, 18, 0.5f, 8.6f, 33.5f, 11.4f);
        Sangue(raiz, new Vector3(6.2f, 0f, 9.8f), 1.3f, 10f, r);
        Sangue(raiz, new Vector3(29.5f, 0f, 10.4f), 1.8f, 200f, r);
        var lixo2 = MovelI(raiz, "Quaternius_Interior/Trashcan_Small1", new Vector3(13.4f, 0f, 9.0f), 0f, 0.55f);
        if (lixo2 != null) Tombar(lixo2, 0f, 90f);
        foreach (float x in new[] { 4f, 11f, 23f, 30f })
            MovelI(raiz, "Quaternius_Interior/Light_Ceiling5", new Vector3(x, c.pe - 0.16f, 10f), 0f, 0.6f, 0, 0f, false);

        // salas
        var carteira = PrefabCarteira();
        string[] lousas =
        {
            "3º ANO A\n\nAULAS SUSPENSAS\nATÉ SEGUNDA ORDEM",
            "4º ANO A\n\nEVACUAÇÃO - 27/10\nSIGAM OS SOLDADOS\nNÃO VOLTEM PRA PEGAR\nO MATERIAL",
            "4º ANO B   Profª Ana\n\nSAÍDA DE EMERGÊNCIA:\nTODOS PARA O PÁTIO!\nFIQUEM JUNTOS",
            "5º ANO A\n\n12 x 4 = 48\n36 : 6 = 6\n\n(PROVA ADIADA)",
        };
        DesenhoLia desenho = null;
        for (int k = 0; k < 4; k++)
        {
            float x0 = salasX[k], x1 = salasX[k + 1];
            bool sala4B = k == 2, baguncada = k <= 1;
            // meia espessura da parede oeste/leste da sala (as das pontas são externas, mais grossas)
            float wO = k == 0 ? E / 2f : I / 2f, wL = k == 3 ? E / 2f : I / 2f;
            // lousa na parede oeste
            var lousaTex = TexturaTexto("lousa_" + k, lousas[k], 1024, 360, new Color(0.11f, 0.22f, 0.16f), new Color(0.88f, 0.9f, 0.86f), false, 0.1f);
            Cartaz(raiz, new Vector3(x0 + wO + 0.035f, 1.55f, 16f), Vector3.right, 3.6f, 1.26f, MatI("Lousa_" + k, Color.white, 0.25f, lousaTex), "Lousa");
            Placa(raiz, new Vector3(x0 + wO + 0.015f, 1.55f, 16f), Vector3.right, 3.72f, 1.38f, MatI("Lousa_Moldura", new Color(0.55f, 0.56f, 0.56f), 0.5f, null, false, 0.6f), "MolduraLousa", 0.03f);
            Bloquinho(raiz, MatI("Lousa_Moldura", new Color(0.55f, 0.56f, 0.56f), 0.5f, null, false, 0.6f), new Vector3(x0 + wO + 0.06f, 0.88f, 16f), new Vector3(0.08f, 0.03f, 3.6f), "Bandeja");
            // mesa da professora
            MovelI(raiz, "Quaternius_Moveis/Desk", new Vector3(x0 + 1.75f, 0f, 16.2f), 90f, 1.4f, 0);
            MovelI(raiz, "Quaternius_Moveis/Chair", new Vector3(x0 + 1.05f, 0f, 16.2f), 90f, 0.92f, 1, 180f);
            // carteiras: 4 fileiras × 4 colunas, viradas pra lousa (oeste)
            for (int fil = 0; fil < 4; fil++)
                for (int col = 0; col < 4; col++)
                {
                    Vector3 p = new Vector3(x0 + 3.2f + fil * 1.4f, 0f, 13.7f + col * 1.6f);
                    float yaw = -90f;
                    if (baguncada && r.NextDouble() < 0.55)
                    {
                        // sala revirada: carteiras arrastadas, tortas, algumas caídas
                        p += new Vector3(((float)r.NextDouble() - 0.5f) * 0.6f, 0f, ((float)r.NextDouble() - 0.5f) * 0.5f);
                        yaw += ((float)r.NextDouble() - 0.5f) * 120f;
                        var cc = Por(carteira, raiz, p, yaw);
                        if (r.NextDouble() < 0.4) Tombar(cc, 0f, r.Next(2) == 0 ? 90f : -90f);
                        continue;
                    }
                    if (!baguncada) yaw += ((float)r.NextDouble() - 0.5f) * 8f;
                    var cart = Por(carteira, raiz, p, yaw);
                    if (sala4B && fil == 1 && col == 2)
                    {
                        // a carteira da Lia: o desenho em cima, com uma luzinha
                        var dg = new GameObject("DesenhoLia");
                        dg.transform.SetParent(raiz, false);
                        dg.transform.localPosition = p + new Vector3(0f, 0.74f, 0f);
                        dg.transform.localRotation = Quaternion.Euler(0f, yaw + 90f, 0f);
                        desenho = dg.AddComponent<DesenhoLia>();
                        desenho.raio = 1.7f;
                    }
                }
            // armário baixo no fundo + lixeira
            MovelI(raiz, "Quaternius_Interior/Drawer_5", new Vector3(x1 - wL - 0.35f, 0f, 14.8f), -90f, 0.75f);
            MovelI(raiz, "Kenney/books", new Vector3(x1 - wL - 0.35f, 0.76f, 14.6f), -90f, 0.25f, 0, 0f, false);
            MovelI(raiz, "Quaternius_Interior/Trashcan_Small2", new Vector3(x0 + 7.9f, 0f, 12.6f), 0f, 0.45f);
            // desenhos das crianças na parede do fundo (a sala da Lia tem a parede cheia)
            int nDes = sala4B ? 10 : 3;
            for (int d = 0; d < nDes; d++)
            {
                float zz = 13.4f + (d % 5) * 1.25f, yy = d < 5 ? 2.1f : 1.55f;
                if (!sala4B) { zz = 14.5f + d * 1.6f; yy = 2.0f; }
                var des = Cartaz(raiz, new Vector3(x1 - wL - 0.012f, yy, zz), Vector3.left, 0.46f, 0.36f,
                    MatI("DesenhoParede_" + ((k * 10 + d) % 8), Color.white, 0.05f, DesenhoCrianca(100 + (k * 10 + d) % 8)), "Desenho");
                des.transform.localRotation *= Quaternion.Euler(0f, 0f, ((float)r.NextDouble() - 0.5f) * 10f);
            }
            if (baguncada) Papeis(raiz, r, 10, x0 + 0.5f, 12.5f, x1 - 0.5f, 19.5f);
            foreach (float zz in new[] { 14.5f, 17.5f })
                MovelI(raiz, "Quaternius_Interior/Light_Ceiling5", new Vector3((x0 + x1) / 2f, c.pe - 0.16f, zz), 0f, 0.6f, 0, 0f, false);
        }
        Sangue(raiz, new Vector3(12f, 0f, 17.5f), 1.5f, 70f, r);
        Letreiro(raiz, "desenhos_4b", "NOSSA FAMÍLIA - 4º B", new Vector3(25.5f - I / 2f - 0.012f, 2.62f, 15.9f), Vector3.left, 1.8f, 0.24f, new Color(0.95f, 0.9f, 0.6f), new Color(0.6f, 0.15f, 0.35f), 768);

        // sala dos professores (chave do 4ºB)
        MovelI(raiz, "Quaternius_Moveis/Table", new Vector3(3.6f, 0f, 3.8f), 0f, 0.78f);
        foreach (float z in new[] { 2.9f, 3.8f, 4.7f })
        {
            MovelI(raiz, "Quaternius_Moveis/Chair", new Vector3(2.55f, 0f, z), 90f, 0.92f, 1, 180f);
            if (z != 3.8f) MovelI(raiz, "Quaternius_Moveis/Chair", new Vector3(4.65f, 0f, z), -90f, 0.92f, 1, 180f);
        }
        MovelI(raiz, "Quaternius_Moveis/Closet", new Vector3(5.6f, 0f, E / 2f + 0.35f), 0f, 2.0f, 1, 180f);
        MovelI(raiz, "Quaternius_Moveis/Bookcase_Books", new Vector3(8f - I / 2f - 0.3f, 0f, 2.6f), -90f, 2.0f, 1, 180f);
        MovelI(raiz, "Quaternius_Interior/Kitchen_Cabinet1", new Vector3(1.2f, 0f, 8f - I / 2f - 0.37f), 180f, 0.9f);
        MovelI(raiz, "Quaternius_Interior/Kitchen_Sink", new Vector3(1.88f, 0f, 8f - I / 2f - 0.37f), 180f, 1.07f);
        MovelI(raiz, "Kenney/kitchenCoffeeMachine", new Vector3(1.2f, 0.9f, 8f - I / 2f - 0.3f), 180f, 0.35f, 1, 0f, false);
        MovelI(raiz, "Quaternius_Moveis/Sofa_individual", new Vector3(7.1f, 0f, 6.8f), -135f, 0.85f, 1, 180f);
        // quadro de chaves na parede oeste
        var madeira = MatI("QuadroChaves", new Color(0.4f, 0.27f, 0.15f), 0.25f);
        var metalChave = MatI("Chave_Metal", new Color(0.72f, 0.7f, 0.6f), 0.6f, null, false, 0.9f);
        Vector3 quadro = new Vector3(E / 2f + 0.02f, 1.55f, 5.3f);
        Placa(raiz, quadro, Vector3.right, 0.75f, 0.55f, madeira, "QuadroChaves", 0.03f);
        Letreiro(raiz, "chaves", "CHAVES", quadro + new Vector3(0.022f, 0.19f, 0f), Vector3.right, 0.42f, 0.1f, new Color(0.9f, 0.86f, 0.7f), new Color(0.15f, 0.1f, 0.05f), 256);
        var coresEtiqueta = new[] { new Color(0.8f, 0.15f, 0.1f), new Color(0.15f, 0.4f, 0.8f), new Color(0.2f, 0.6f, 0.25f), new Color(0.95f, 0.95f, 0.9f) };
        GameObject chave4B = null;
        for (int lin = 0; lin < 2; lin++)
            for (int col = 0; col < 5; col++)
            {
                Vector3 p = quadro + new Vector3(0.035f, 0.03f - lin * 0.2f, -0.26f + col * 0.13f);
                Bloquinho(raiz, metalChave, p + Vector3.up * 0.03f, new Vector3(0.012f, 0.012f, 0.012f), "Gancho");
                bool aLia = lin == 0 && col == 3;
                if (!aLia && r.NextDouble() < 0.3) continue; // gancho vazio
                var chaveGo = new GameObject(aLia ? "Chave4B" : "Chave");
                chaveGo.transform.SetParent(raiz, false);
                chaveGo.transform.localPosition = p;
                Bloquinho(chaveGo.transform, metalChave, new Vector3(0.005f, -0.03f, 0f), new Vector3(0.006f, 0.06f, 0.02f), "Chave");
                Bloquinho(chaveGo.transform, MatI("Etiqueta_" + (aLia ? "4B" : (lin * 5 + col) % 4 + ""), aLia ? new Color(1f, 0.85f, 0.1f) : coresEtiqueta[(lin * 5 + col) % 4], 0.2f),
                    new Vector3(0.008f, -0.085f, 0f), new Vector3(0.008f, 0.045f, 0.035f), "Etiqueta");
                if (aLia) chave4B = chaveGo;
            }
        LuzInt(raiz, quadro + new Vector3(0.35f, 0.1f, 0.05f), new Color(1f, 0.9f, 0.65f), 0.9f, 1.8f);
        var iChave = Interacao(raiz, "chave", "[E] Pegar a chave do 4º B", new Vector3(0.75f, 0f, 5.3f), 1.5f);
        Papeis(raiz, r, 8, 0.6f, 0.6f, 7.4f, 7.4f);

        // secretaria
        for (int k = 0; k < 3; k++)
            MovelI(raiz, "Quaternius_Interior/Kitchen_Cabinet" + (1 + k % 2), new Vector3(26.2f + k * 0.69f, 0f, 5.4f), 0f, 1.05f);
        MovelI(raiz, "Kenney/computerScreen", new Vector3(26.9f, 1.05f, 5.4f), 180f, 0.42f, 1, 0f, false);
        foreach (float x in new[] { 29.3f, 32.2f })
        {
            MovelI(raiz, "Quaternius_Moveis/Desk", new Vector3(x, 0f, 2.3f), 0f, 1.4f, 0);
            MovelI(raiz, "Quaternius_Moveis/OfficeChair", new Vector3(x, 0f, 1.45f), 0f, 1.0f, 1, 180f);
            MovelI(raiz, "Kenney/computerScreen", new Vector3(x, 0.73f, 2.45f), 180f, 0.42f, 1, 0f, false);
        }
        MovelI(raiz, "Quaternius_Moveis/ShortCloset", new Vector3(34f - E / 2f - 0.32f, 0f, 5.2f), -90f, 1.6f, 1, 180f);
        MovelI(raiz, "Quaternius_Interior/Drawer_4", new Vector3(21.5f + I / 2f + 0.3f, 0f, 2.2f), 90f, 1.0f);
        Papeis(raiz, r, 12, 22f, 0.5f, 33.5f, 7.5f);

        // pontos do roteiro
        var interior = raiz.gameObject.AddComponent<Interior>();
        interior.nome = "Escola";
        interior.entrada = Ponto(raiz, "Entrada", new Vector3(17f, 0f, 3.2f), 0f);
        interior.saida = fachada.saida;
        interior.portaFora = fachada.porta;
        interior.portaDentro = Interacao(raiz, "sair_escola", "[E] Sair da escola", new Vector3(17f, 0f, 0.75f), 1.5f);
        interior.pontosItens = new[]
        {
            Ponto(raiz, "Municao_Professores", new Vector3(5.6f, 0.05f, 2.3f)),
            Ponto(raiz, "Comida_Professores", new Vector3(2.0f, 0.05f, 1.6f)),
            Ponto(raiz, "KitMedico_Secretaria", new Vector3(30.8f, 0.05f, 4.4f)),
            Ponto(raiz, "Comida_Secretaria", new Vector3(23.2f, 0.05f, 2.0f)),
            Ponto(raiz, "Municao_5A", new Vector3(30f, 0.05f, 13f)),
        };
        interior.ambienteCeu = new Color(0.06f, 0.065f, 0.08f);
        interior.ambienteMeio = new Color(0.045f, 0.047f, 0.055f);
        interior.ambienteChao = new Color(0.022f, 0.022f, 0.026f);
        interior.corNeblina = new Color(0.012f, 0.014f, 0.018f);
        interior.neblina = 0.028f;

        var esc = raiz.gameObject.AddComponent<EscolaInterior>();
        esc.interior = interior;
        esc.mochila = iMochila;
        esc.chave = iChave;
        esc.chaveVisual = chave4B;
        esc.folha4B = porta4B;
        esc.porta4B = Interacao(raiz, "porta4B", "[E] Abrir a porta do 4º B", new Vector3(23.5f, 0f, 11.25f), 1.4f);
        esc.desenho = desenho;
        esc.luzesEmergencia = emergencia.ToArray();
        esc.pontosBarulho = new[] { Ponto(raiz, "Barulho_Porta", new Vector3(17f, 1.5f, 0.5f)), Ponto(raiz, "Barulho_Oeste", new Vector3(0.6f, 1.5f, 10f)), Ponto(raiz, "Barulho_Leste", new Vector3(33.4f, 1.5f, 10f)) };
        // zumbis entram depois do bake do NavMesh (não podem virar "buraco" no chão)
        paraZumbis.Add(raiz.gameObject);
        return esc;
    }

    // zumbis da escola (chamado depois do bake do NavMesh)
    static void ZumbisEscola(EscolaInterior esc)
    {
        var raiz = esc.transform;
        var grupo = new GameObject("Zumbis").transform;
        grupo.SetParent(raiz, false);
        ZumbiFixo(grupo, "Comum", new Vector3(19.2f, 0f, 6.6f), 30f, 3.8f);       // saguão: o 1º, de costas
        ZumbiFixo(grupo, "Rastejante", new Vector3(3.0f, 0f, 10.0f), 90f, 5f);  // ponta do corredor
        ZumbiFixo(grupo, "Rastejante", new Vector3(5.3f, 0f, 15.3f), 0f, 4.5f); // 3º A, entre as carteiras
        ZumbiFixo(grupo, "Rastejante", new Vector3(12.5f, 0f, 17.0f), 180f, 4.5f); // 4º A
        ZumbiFixo(grupo, "Comum", new Vector3(14.6f, 0f, 19.0f), -90f, 5f);
        ZumbiFixo(grupo, "Comum", new Vector3(6.7f, 0f, 1.5f), -90f, 5f);       // professores
        ZumbiFixo(grupo, "Rastejante", new Vector3(2.4f, 0f, 6.2f), 180f, 4.5f);
        ZumbiFixo(grupo, "Gordao", new Vector3(31.6f, 0f, 5.9f), -90f, 5.5f);   // secretaria
        ZumbiFixo(grupo, "Comum", new Vector3(30.5f, 0f, 17.5f), 180f, 5f);     // 5º A
        grupo.gameObject.SetActive(false);
        esc.interior.zumbis = grupo.gameObject;
        // a horda que entra na fuga (desligada até lá)
        var fuga = new GameObject("ZumbisFuga").transform;
        fuga.SetParent(raiz, false);
        ZumbiFixo(fuga, "Comum", new Vector3(16.0f, 0f, 1.7f), 0f, 30f);
        ZumbiFixo(fuga, "Comum", new Vector3(18.2f, 0f, 2.7f), 0f, 30f);
        ZumbiFixo(fuga, "Comum", new Vector3(1.4f, 0f, 10.2f), 90f, 30f);
        ZumbiFixo(fuga, "Comum", new Vector3(32.9f, 0f, 9.6f), -90f, 30f);
        ZumbiFixo(fuga, "Gordao", new Vector3(26.8f, 0f, 3.8f), 0f, 30f);
        ZumbiFixo(fuga, "Rastejante", new Vector3(11.2f, 0f, 15.4f), 180f, 30f);
        fuga.gameObject.SetActive(false);
        esc.zumbisFuga = fuga.gameObject;
        EditorUtility.SetDirty(esc);
        EditorUtility.SetDirty(esc.interior);
    }
}
