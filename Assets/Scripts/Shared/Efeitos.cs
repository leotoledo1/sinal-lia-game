using UnityEngine;

// Partículas de impacto (sangue / poeira). Um ParticleSystem só pra cada, reposicionado a cada emissão.
public class Efeitos : MonoBehaviour
{
    public static Efeitos Instance { get; private set; }

    public Material materialParticula;
    public Color corSangue = new Color(0.45f, 0.02f, 0.02f);
    public Color corPoeira = new Color(0.62f, 0.6f, 0.55f);

    ParticleSystem sangue, poeira;

    void Awake()
    {
        Instance = this;
        sangue = Criar("Sangue", corSangue, 3.5f, 1.6f, 0.05f, 0.12f);
        poeira = Criar("Poeira", corPoeira, 2.2f, 0.6f, 0.04f, 0.1f);
    }

    ParticleSystem Criar(string nome, Color cor, float velocidade, float gravidade, float tamMin, float tamMax)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(velocidade * 0.4f, velocidade);
        main.startSize = new ParticleSystem.MinMaxCurve(tamMin, tamMax);
        main.startColor = cor;
        main.gravityModifier = gravidade;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        var emissao = ps.emission;
        emissao.rateOverTime = 0f;
        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 35f;
        forma.radius = 0.02f;
        var tamanho = ps.sizeOverLifetime;
        tamanho.enabled = true;
        tamanho.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (materialParticula != null) r.sharedMaterial = materialParticula;
        return ps;
    }

    static void Emitir(ParticleSystem ps, Vector3 pos, Vector3 normal, int qtd)
    {
        if (ps == null) return;
        ps.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(normal.sqrMagnitude > 0.01f ? normal : Vector3.up));
        ps.Emit(qtd);
    }

    public static void Sangue(Vector3 pos, Vector3 normal, int qtd = 8)
    {
        if (Instance != null) Emitir(Instance.sangue, pos, normal, qtd);
    }

    public static void Poeira(Vector3 pos, Vector3 normal, int qtd = 6)
    {
        if (Instance != null) Emitir(Instance.poeira, pos, normal, qtd);
    }

    public static int ParticulasVivas => Instance == null ? 0 : Instance.sangue.particleCount + Instance.poeira.particleCount;
}
