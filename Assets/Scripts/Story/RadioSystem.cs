using System.Collections.Generic;
using UnityEngine;

// Falas da história, uma de cada vez: legenda na tela + voz gravada.
// Pelo rádio: chiado no começo e som "de walkie-talkie" (filtros). Ao vivo: voz limpa.
// A legenda fica até a voz acabar. A música abaixa enquanto alguém fala.
// Voz = AudioClip com o nome do id da fala (Assets/Audio/Falas/<id>.wav). Sem arquivo: só a legenda.
public class RadioSystem : MonoBehaviour
{
    public static RadioSystem Instance { get; private set; }

    public float segundosPorMensagem = 5f;
    public AudioClip somChiado;
    [Tooltip("Vozes das falas (o nome do arquivo é o id: lia_01, omega_02, marcos_03...)")]
    public AudioClip[] vozes;
    [Range(0f, 1f)] public float volumeVoz = 1f;
    [Tooltip("Tom extra da voz da Lia (1 = como foi gravada)")]
    public float tomLia = 1f;

    public string Quem { get; private set; }
    public string Texto { get; private set; }
    public bool PeloRadio { get; private set; }
    public bool Mostrando => Texto != null;
    // tem fala na tela ou esperando na fila (as cenas esperam isso acabar)
    public bool Ocupado => Texto != null || fila.Count > 0;
    public bool Falando => vozPendente != null || (vozSrc != null && vozSrc.isPlaying) || Time.time < fimVoz;
    // 0..1 de quanto falta a legenda (pra fade)
    public float TempoNaTela => Time.unscaledTime - inicioLegenda;
    public float TempoRestante => Mathf.Max(0f, fim - Time.time);

    struct Fala { public string quem, texto, id; public bool aoVivo; }
    readonly Queue<Fala> fila = new Queue<Fala>();
    readonly Dictionary<string, AudioClip> porId = new Dictionary<string, AudioClip>();
    float fim, inicioVoz = -1f, fimVoz = -1f, inicioLegenda;
    AudioClip vozPendente;
    AudioSource audioSrc, vozSrc;
    AudioHighPassFilter passaAlta;
    AudioLowPassFilter passaBaixa;
    AudioDistortionFilter distorcao;
    bool pausado;

    void Awake()
    {
        Instance = this;
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        var go = new GameObject("Voz");
        go.transform.SetParent(transform, false);
        vozSrc = go.AddComponent<AudioSource>();
        vozSrc.playOnAwake = false;
        vozSrc.spatialBlend = 0f;
        vozSrc.priority = 0;
        passaAlta = go.AddComponent<AudioHighPassFilter>();
        passaBaixa = go.AddComponent<AudioLowPassFilter>();
        distorcao = go.AddComponent<AudioDistortionFilter>();
        if (vozes != null)
            foreach (var c in vozes)
                if (c != null) porId[c.name] = c;
    }

    // idVoz: nome do arquivo da voz (null = só legenda). aoVivo: falado pessoalmente (sem efeito de rádio)
    public void Enfileirar(string quem, string texto, string idVoz = null, bool aoVivo = false) =>
        fila.Enqueue(new Fala { quem = quem, texto = texto, id = idVoz, aoVivo = aoVivo });

    // tira da fila o que ainda não começou (a fala atual termina normalmente)
    public void LimparFila() => fila.Clear();

    // corta a fala atual e esvazia a fila (pular cena)
    public void Silenciar()
    {
        fila.Clear();
        vozPendente = null;
        if (vozSrc != null) vozSrc.Stop();
        Texto = null;
        fim = 0f;
        fimVoz = -1f;
        if (SomManager.Instance != null) SomManager.Instance.Abafado = false;
    }

    // grito curto sem legenda (ex.: a Lia pedindo ajuda). Não atrapalha as falas da história.
    AudioSource gritoSrc;
    float fimGrito = -1f;
    readonly Dictionary<string, float> proximoGrito = new Dictionary<string, float>();
    public void Grito(string id, float intervalo = 9f)
    {
        if (string.IsNullOrEmpty(id) || Falando || !porId.TryGetValue(id, out var clip) || clip == null) return;
        if (proximoGrito.TryGetValue(id, out var t) && Time.time < t) return;
        proximoGrito[id] = Time.time + intervalo;
        if (gritoSrc == null)
        {
            gritoSrc = gameObject.AddComponent<AudioSource>();
            gritoSrc.playOnAwake = false;
            gritoSrc.spatialBlend = 0f;
            gritoSrc.priority = 1;
        }
        gritoSrc.pitch = id.StartsWith("lia") ? tomLia : 1f;
        gritoSrc.PlayOneShot(clip, volumeVoz);
        fimGrito = Time.time + clip.length;
    }

    void Update()
    {
        // pausa (Time.timeScale 0): a voz pausa junto
        bool pausaAgora = Time.timeScale == 0f;
        if (pausaAgora != pausado)
        {
            pausado = pausaAgora;
            if (pausado) vozSrc.Pause(); else vozSrc.UnPause();
        }
        if (pausado) return;

        // a voz começa um instante depois do chiado do rádio
        if (vozPendente != null && Time.time >= inicioVoz)
        {
            vozSrc.clip = vozPendente;
            vozSrc.Play();
            vozPendente = null;
        }
        if (SomManager.Instance != null) SomManager.Instance.Abafado = Falando || Time.time < fimGrito;

        if (Texto != null && (Time.time < fim || Falando)) return;
        if (fila.Count == 0) { Texto = null; return; }
        Mostrar(fila.Dequeue());
    }

    void Mostrar(Fala f)
    {
        Quem = f.quem;
        Texto = f.texto;
        PeloRadio = !f.aoVivo;
        inicioLegenda = Time.unscaledTime;
        AudioClip voz = null;
        if (!string.IsNullOrEmpty(f.id)) porId.TryGetValue(f.id, out voz);

        float atraso = 0f;
        if (PeloRadio && somChiado != null)
        {
            audioSrc.PlayOneShot(somChiado, voz != null ? 0.55f : 1f);
            atraso = 0.25f;
        }
        ConfigurarVoz(f.quem, PeloRadio);
        if (voz != null)
        {
            vozPendente = voz;
            inicioVoz = Time.time + atraso;
            fimVoz = inicioVoz + voz.length / Mathf.Max(0.5f, vozSrc.pitch);
            // legenda fica a fala inteira + um respiro pra terminar de ler
            fim = Time.time + atraso + voz.length / Mathf.Max(0.5f, vozSrc.pitch) + 0.7f;
        }
        else
        {
            vozPendente = null;
            inicioVoz = fimVoz = -1f;
            fim = Time.time + Mathf.Max(segundosPorMensagem, f.texto.Length * 0.06f);
        }
    }

    // som de cada um: Base Ômega = rádio militar chiado; Lia = walkie-talkie de brinquedo; ao vivo = limpo
    void ConfigurarVoz(string quem, bool radio)
    {
        bool lia = quem == "Lia";
        bool omega = quem.StartsWith("Base") || quem.StartsWith("Resgate"); // rádio militar
        vozSrc.pitch = lia ? tomLia : 1f;
        vozSrc.volume = volumeVoz * (radio ? 1f : 0.95f);
        passaAlta.enabled = passaBaixa.enabled = distorcao.enabled = radio;
        if (!radio) return;
        passaAlta.cutoffFrequency = omega ? 450f : 380f;
        passaBaixa.cutoffFrequency = omega ? 3000f : 3600f;
        distorcao.distortionLevel = omega ? 0.5f : 0.3f;
    }
}
