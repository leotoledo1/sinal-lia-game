using UnityEngine;

// Toca os sons do jogo com fontes reaproveitadas (sem criar AudioSource a cada som)
public class SomManager : MonoBehaviour
{
    public static SomManager Instance { get; private set; }

    [Header("Jogador")]
    public AudioClip[] passos;
    public AudioClip danoJogador;
    public AudioClip vazio;
    public AudioClip acerto;
    public AudioClip pegarItem;
    public AudioClip cura;

    [Header("Zumbi")]
    public AudioClip[] gemidos;
    public AudioClip zumbiAtaque;
    public AudioClip zumbiMorte;
    public AudioClip impactoCarne;
    public AudioClip impactoParede;

    [Header("Ambiente (loops)")]
    public AudioClip vento;
    public AudioClip noite;
    public AudioClip fogo;
    public float volumeVento = 0.3f;
    public float volumeNoite = 0.45f;

    [Header("Música e efeitos da história")]
    public AudioClip musicaMenu;
    public AudioClip musicaDia;
    public AudioClip musicaNoite;
    public AudioClip musicaFinal;
    [Tooltip("Dia 2 (seguindo o sinal). Vazio = usa a música do dia")]
    public AudioClip musicaDia2;
    [Tooltip("Tela de vitória (vazio = música do menu)")]
    public AudioClip musicaVitoria;
    [Tooltip("Tela de game over (vazio = silêncio)")]
    public AudioClip musicaGameOver;
    public AudioClip somVitoria;
    public AudioClip somGameOver;
    public AudioClip helicoptero;
    public float volumeMusica = 0.32f;

    [Range(0f, 1f)] public float volumeGeral = 1f;

    public AudioClip MusicaAtual => musicaAtiva != null ? musicaAtiva.clip : null;
    // alguém falando (rádio): a música abaixa pra voz aparecer
    public bool Abafado { get; set; }
    [Range(0f, 1f)] public float volumeMusicaAbafada = 0.4f;
    float fatorAbafar = 1f;
    // tiros, zumbis, ambiente e helicóptero também abaixam enquanto alguém fala (no combate a voz sumia)
    [Range(0f, 1f)] public float volumeEfeitos = 0.7f;
    [Range(0f, 1f)] public float efeitosAbafados = 0.22f;
    public float FatorEfeitos { get; private set; } = 1f;
    float[] volume3D;

    AudioSource[] fontes3D;
    int proxima;
    AudioSource fonte2D, ambDia, ambNoite, musicaA, musicaB, musicaAtiva;
    bool ehNoite;

    void Awake()
    {
        Instance = this;
        fontes3D = new AudioSource[24];
        volume3D = new float[fontes3D.Length];
        FatorEfeitos = volumeEfeitos;
        for (int i = 0; i < fontes3D.Length; i++)
        {
            var go = new GameObject("Som3D_" + i);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 2f;
            s.maxDistance = 40f;
            s.dopplerLevel = 0f;
            fontes3D[i] = s;
        }
        fonte2D = gameObject.AddComponent<AudioSource>();
        fonte2D.playOnAwake = false;
        ambDia = CriarLoop(vento);
        ambNoite = CriarLoop(noite);
        musicaA = CriarLoop(null);
        musicaB = CriarLoop(null);
        Opcoes.Aplicar();
    }

    // troca de música com transição suave (null = silêncio)
    public void TocarMusica(AudioClip clip)
    {
        if (musicaAtiva != null && musicaAtiva.clip == clip && musicaAtiva.isPlaying) return;
        var nova = musicaAtiva == musicaA ? musicaB : musicaA;
        nova.Stop();
        nova.clip = clip;
        nova.volume = 0f;
        if (clip != null) nova.Play();
        musicaAtiva = clip != null ? nova : null;
    }

    // efeito curto "de trilha" (vitória, game over)
    public void Vinheta(AudioClip clip, float volume = 0.8f) => Tocar2D(clip, volume);

    AudioSource CriarLoop(AudioClip c)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = c;
        s.loop = true;
        s.volume = 0f;
        s.playOnAwake = false;
        if (c != null) s.Play();
        return s;
    }

    void Update()
    {
        // alguém falando: efeitos descem rápido (0,2 s) e voltam devagar
        float alvoEf = volumeEfeitos * (Abafado ? efeitosAbafados : 1f);
        FatorEfeitos = Mathf.MoveTowards(FatorEfeitos, alvoEf, Time.unscaledDeltaTime * (alvoEf < FatorEfeitos ? 3.5f : 1.2f));
        fonte2D.volume = FatorEfeitos;
        for (int i = 0; i < fontes3D.Length; i++)
            if (fontes3D[i].isPlaying) fontes3D[i].volume = volume3D[i] * FatorEfeitos;
        // troca suave entre o vento do dia e o clima da noite
        float v = Time.unscaledDeltaTime * 0.5f;
        float amb = volumeGeral * (Abafado ? 0.5f : 1f);
        ambDia.volume = Mathf.MoveTowards(ambDia.volume, ehNoite ? 0f : volumeVento * amb, v);
        ambNoite.volume = Mathf.MoveTowards(ambNoite.volume, ehNoite ? volumeNoite * amb : 0f, v);
        // música: a ativa sobe, a outra desce (2 s de transição)
        float m = Time.unscaledDeltaTime * volumeMusica / 2f;
        fatorAbafar = Mathf.MoveTowards(fatorAbafar, Abafado ? volumeMusicaAbafada : 1f, Time.unscaledDeltaTime * 2.5f);
        AjustarMusica(musicaA, m);
        AjustarMusica(musicaB, m);
    }

    // dentro da escola/clínica a música fica mais baixa (o silêncio assusta mais)
    public float MultMusica { get; set; } = 1f;

    void AjustarMusica(AudioSource s, float passo)
    {
        float alvo = s == musicaAtiva ? volumeMusica * volumeGeral * fatorAbafar * MultMusica : 0f;
        // abafar/desabafar é rápido; troca de música continua suave
        if (s == musicaAtiva && s.volume > alvo) passo = Mathf.Max(passo, Time.unscaledDeltaTime * volumeMusica * 2f);
        s.volume = Mathf.MoveTowards(s.volume, alvo, passo);
        if (s != musicaAtiva && s.volume <= 0f && s.isPlaying) s.Stop();
    }

    public void Ambiente(bool noite) => ehNoite = noite;

    public static AudioClip Sortear(AudioClip[] lista) =>
        lista == null || lista.Length == 0 ? null : lista[Random.Range(0, lista.Length)];

    // som no mundo (zumbis, impactos)
    public void Tocar(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f, float variacao = 0.08f, float alcance = 40f)
    {
        if (clip == null) return;
        int idx = proxima;
        var s = fontes3D[idx];
        proxima = (proxima + 1) % fontes3D.Length;
        s.transform.position = pos;
        s.maxDistance = alcance;
        s.pitch = pitch * Random.Range(1f - variacao, 1f + variacao);
        volume3D[idx] = volume * volumeGeral;
        s.volume = volume3D[idx] * FatorEfeitos;
        s.clip = clip;
        s.Play();
    }

    // som "na cabeça" do jogador (tiros, passos, interface)
    public void Tocar2D(AudioClip clip, float volume = 1f, float variacao = 0f)
    {
        if (clip == null) return;
        fonte2D.pitch = Random.Range(1f - variacao, 1f + variacao);
        fonte2D.PlayOneShot(clip, volume * volumeGeral);
    }
}
