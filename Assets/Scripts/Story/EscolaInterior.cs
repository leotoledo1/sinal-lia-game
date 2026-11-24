using System.Collections;
using UnityEngine;

// Roteiro da escola por dentro (Dia 1):
// porta da frente -> saguão escuro -> mochila da Lia no corredor ("4º ano B") -> sala do 4ºB trancada ->
// chave na sala dos professores -> desenho na carteira dela -> barulho de horda -> FUGA pela porta da frente -> pôr do sol.
public class EscolaInterior : MonoBehaviour
{
    public static EscolaInterior Instance { get; private set; }

    public Interior interior;
    public Interativo mochila, porta4B, chave;
    public PortaInterior folha4B;
    [Tooltip("Chave pendurada no quadro (some quando o Marcos pega)")]
    public GameObject chaveVisual;
    public DesenhoLia desenho;
    [Tooltip("Zumbis que entram na escola quando o Marcos pega o desenho (desligados até lá)")]
    public GameObject zumbisFuga;
    [Tooltip("De onde vem o barulho da horda (portas, janelas)")]
    public Transform[] pontosBarulho;
    [Tooltip("Luzes de emergência: ficam vermelhas na fuga")]
    public Light[] luzesEmergencia;

    public bool ViuMochila { get; private set; }
    public bool TemChave { get; private set; }
    public bool SabeDaChave { get; private set; }
    public bool PortaAberta => folha4B != null && folha4B.aberta;
    public bool Fugindo { get; private set; }
    public bool Fugiu { get; private set; }

    bool chegou;

    void Awake() => Instance = this;

    void OnEnable()
    {
        Instance = this;
        Interativo.Usado += AoUsar;
        if (interior != null)
        {
            interior.AoEntrar += Entrou;
            interior.AoSair += Saiu;
        }
    }

    void OnDisable()
    {
        Interativo.Usado -= AoUsar;
        if (interior != null)
        {
            interior.AoEntrar -= Entrou;
            interior.AoSair -= Saiu;
        }
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || interior == null) return;
        // a porta da rua só abre no Dia 1, até a fuga
        if (interior.portaFora != null) interior.portaFora.ativo = gm.FaseAtual == Fase.Dia1 && !Fugiu && !gm.Acabou;
        // na fuga: chegou na porta da frente, sai sem precisar apertar E
        if (Fugindo && interior.Dentro && gm.JogoAtivo && interior.portaDentro != null && PlayerHealth.Instance != null)
        {
            Vector3 d = PlayerHealth.Instance.transform.position - interior.portaDentro.Posicao;
            d.y = 0f;
            if (d.magnitude < 1.5f) gm.SairInterior();
        }
    }

    static RadioSystem Radio => RadioSystem.Instance;
    static void Objetivo(string texto, Transform alvo = null) => ObjectiveManager.Instance?.Definir(texto, alvo, false);

    // GameManager: o Marcos chegou perto da escola (lá fora)
    public void Chegou()
    {
        if (chegou) return;
        chegou = true;
        Radio?.Enfileirar("Marcos", "Lia?! ...Ninguém aqui fora. A porta da escola tá aberta... vou entrar.", "marcos_02", true);
        Objetivo("Entre na escola (porta da frente)", interior != null && interior.portaFora != null ? interior.portaFora.transform : null);
        HUD.Tutorial("Chegue na PORTA da escola e aperte E pra entrar");
    }

    // checkpoint "Dia 1 - escola": volta pra frente da porta com o objetivo de entrar
    public void RetomarNaPorta()
    {
        chegou = true;
        Objetivo("Entre na escola (porta da frente)", interior != null && interior.portaFora != null ? interior.portaFora.transform : null);
    }

    void Entrou(bool primeira)
    {
        if (primeira)
        {
            Radio?.LimparFila(); // fala de lá de fora não toca atrasada aqui dentro
            Radio?.Enfileirar("Marcos", "Lia? LIA! ...Tá um breu aqui dentro. Calma, Marcos. Uma sala de cada vez.", "marcos_13", true);
            HUD.LimparTutoriais();
            HUD.Tutorial("Aqui dentro é escuro: a LANTERNA acende sozinha e aponta pra onde você mira");
            HUD.Tutorial("Aperte E perto das coisas pra examinar, pegar e abrir portas");
            GameManager.Instance?.CheckpointEscola();
        }
        AtualizarObjetivo();
    }

    void Saiu()
    {
        if (Fugindo)
        {
            Fugindo = false;
            Fugiu = true;
            GameManager.Instance?.FugiuDaEscola();
            return;
        }
        // saiu antes de achar o desenho: pode voltar
        if (!Fugiu) Objetivo("Volte pra escola: a Lia pode ter deixado pistas", interior != null && interior.portaFora != null ? interior.portaFora.transform : null);
    }

    void AtualizarObjetivo()
    {
        if (Fugindo) Objetivo("FUJA DA ESCOLA! Volte pra porta da frente", interior.portaDentro != null ? interior.portaDentro.transform : null);
        else if (PortaAberta) Objetivo("Procure o desenho da Lia na sala do 4º ano B");
        else if (TemChave) Objetivo("Abra a sala do 4º ano B", porta4B != null ? porta4B.transform : null);
        else if (SabeDaChave) Objetivo("Pegue a chave do 4º B na sala dos professores");
        else if (ViuMochila) Objetivo("Ache a sala do 4º ano B");
        else Objetivo("Procure pistas da Lia na escola");
    }

    void AoUsar(Interativo i)
    {
        var gm = GameManager.Instance;
        if (gm == null || i == null || gm.FaseAtual != Fase.Dia1) return;
        if (i == mochila) PegouMochila();
        else if (i == porta4B) UsouPorta();
        else if (i == chave) PegouChave();
    }

    void PegouMochila()
    {
        if (ViuMochila) return;
        ViuMochila = true;
        mochila.ativo = false;
        SomManager.Instance?.Tocar2D(SomManager.Instance.pegarItem, 0.5f);
        Radio?.Enfileirar("Marcos", "Essa mochila... é da Lia! O chaveirinho de coelho que eu dei. 'Lia, quarto ano B'... A sala dela!", "marcos_14", true);
        AtualizarObjetivo();
    }

    void UsouPorta()
    {
        if (PortaAberta) return;
        if (!TemChave)
        {
            if (!SabeDaChave)
            {
                SabeDaChave = true;
                Radio?.Enfileirar("Marcos", "Trancada! ...A chave deve estar na sala dos professores.", "marcos_15", true);
                if (porta4B != null) porta4B.texto = "[E] Trancada";
            }
            else HUD.Aviso("Trancada. Pegue a chave na sala dos professores");
            AtualizarObjetivo();
            return;
        }
        folha4B?.Abrir();
        if (porta4B != null) porta4B.ativo = false;
        AtualizarObjetivo();
    }

    void PegouChave()
    {
        if (TemChave) return;
        TemChave = true;
        SabeDaChave = true;
        chave.ativo = false;
        if (chaveVisual != null) chaveVisual.SetActive(false);
        SomManager.Instance?.Tocar2D(SomManager.Instance.pegarItem, 0.7f);
        Radio?.Enfileirar("Marcos", "Quarto ano B... achei a chave.", "marcos_16", true);
        // o barulho do molho de chaves acorda quem estiver perto
        ZombieAI.Barulho(chave.Posicao, 12f);
        if (porta4B != null) porta4B.texto = "[E] Abrir a sala do 4º B";
        AtualizarObjetivo();
    }

    // GameManager: o desenho foi lido aqui dentro -> a horda entra na escola
    public void ComecarFuga()
    {
        if (Fugindo || Fugiu) return;
        StartCoroutine(Fuga());
    }

    IEnumerator Fuga()
    {
        Fugindo = true;
        Radio?.Enfileirar("Marcos", "\"Fomos pro posto do exército\"... Ela tá viva. Ela tá viva!", "marcos_03", true);
        AtualizarObjetivo();
        // um instante de alívio... e o estrondo
        for (float t = 0f; t < 2.2f; t += Time.deltaTime) yield return null;
        if (Fugiu || !interior.Dentro) yield break;
        var som = SomManager.Instance;
        if (som != null)
        {
            som.Tocar2D(som.impactoParede, 1f);
            if (pontosBarulho != null)
                foreach (var p in pontosBarulho)
                    if (p != null) som.Tocar(SomManager.Sortear(som.gemidos), p.position, 1f, Random.Range(0.85f, 1.1f), 0.1f, 45f);
            som.TocarMusica(som.musicaNoite);
        }
        foreach (var l in luzesEmergencia)
        {
            if (l == null) continue;
            l.color = new Color(1f, 0.12f, 0.08f);
            var ln = l.GetComponent<LuzNoturna>();
            if (ln != null) { ln.piscar = 0.5f; ln.defeito = false; }
        }
        Camera.main?.GetComponent<ThirdPersonCamera>()?.Tremer(0.5f);
        Radio?.LimparFila();
        Radio?.Enfileirar("Marcos", "Que barulho foi esse? ...Eles tão entrando! Tenho que sair daqui, agora!", "marcos_17", true);
        HUD.Aviso("FUJA! A PORTA DA FRENTE É A SAÍDA");
        Interior.LigarZumbis(zumbisFuga, true);
        ZombieAI.Barulho(PlayerHealth.Instance != null ? PlayerHealth.Instance.transform.position : transform.position, 60f);
        AtualizarObjetivo();
    }
}
