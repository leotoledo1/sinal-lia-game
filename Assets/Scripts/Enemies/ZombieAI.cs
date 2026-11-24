using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AI;

// Zumbi: vaga -> persegue o alvo mais perto (Marcos ou Lia) -> ataca.
// Com Animator (zumbis Quaternius): parâmetros Speed + gatilhos Atacar / Dano / Morrer.
// Sem Animator (modelo antigo): animação procedural girando os ossos "bip ..." por código.
[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAI : MonoBehaviour
{
    [Header("Tipo")]
    public string tipo = "Comum";
    [Tooltip("Acima desta altura (m, a partir do pé) o tiro conta como na cabeça")]
    public float alturaCabeca = 1.45f;
    public Animator animator;
    [Tooltip("Velocidade (m/s) em que a animação de correr fica no máximo")]
    public float velocidadeAnimacaoRef = 2.8f;

    public float velocidadeVagar = 1.2f;
    public float velocidadePerseguir = 2.8f;
    public float raioDeteccao = 25f;
    public float alcanceAtaque = 1.6f;
    public float danoAtaque = 10f;
    public float intervaloAtaque = 1.2f;
    [Tooltip("Tempo entre chegar no jogador e dar o 1º golpe")]
    public float preparoAtaque = 0.5f;

    [Header("Animação procedural")]
    public Transform modelo;
    [Tooltip("Só usado pelo montador da cena pra pose base")]
    public AnimationClip clip;
    [Tooltip("Metros andados por ciclo completo (2 passos)")]
    public float comprimentoCiclo = 1.3f;
    public float anguloPerna = 28f;
    public float anguloJoelho = 45f;
    public float anguloBracos = 75f;
    public float inclinacaoCorpo = 14f;
    public float balanco = 0.05f;

    [Header("Variante rápida")]
    public float multVelocidadeRapido = 1.8f;

    public bool EstaMorto { get; private set; }
    public bool Rapido { get; private set; }
    public bool Atordoado => Time.time < atordoadoAte;
    // quantos ossos a animação procedural achou (esperado: 7)
    public int OssosAnimados => new[] { coluna, bracoE, bracoD, coxaE, coxaD, canelaE, canelaD }.Count(o => o != null);
    // teste automático roda sem janela (nada fica "visível")
    public static bool AnimarForaDaTela;
    [Tooltip("A Lia conta como mais longe que o Marcos nesta proporção (os zumbis preferem o Marcos)")]
    public float pesoAliado = 2.2f;
    [Header("zumbi fixo (interiores)")]
    [Tooltip("Fica parado (sem vagar) até ver/ouvir o Marcos")]
    public bool ficarParado;
    [Tooltip("Desligado = depois de morrer some (não volta pro pool das ondas)")]
    public bool voltarAoPool = true;

    // todos os zumbis ativos (barulho de tiro / da horda acorda os que estão perto)
    static readonly System.Collections.Generic.List<ZombieAI> ativos = new System.Collections.Generic.List<ZombieAI>();

    void OnEnable() { if (!ativos.Contains(this)) ativos.Add(this); }
    void OnDisable() => ativos.Remove(this);

    // barulho (tiro, molho de chaves, estrondo): quem estiver no raio passa a caçar o Marcos
    public static void Barulho(Vector3 pos, float raio)
    {
        float r2 = raio * raio;
        for (int i = ativos.Count - 1; i >= 0; i--)
        {
            var z = ativos[i];
            if (z == null || z.EstaMorto) continue;
            if ((z.transform.position - pos).sqrMagnitude <= r2) z.alertado = true;
        }
    }

    NavMeshAgent agent;
    Transform alvoT;
    IDamageable alvoD;
    float proximaEscolha;
    float multVel = 1f, multDano = 1f;
    bool sempreCacar, alertado, atacando, golpeAnimado;
    float proximoAtaque, proximoRepath, proximoVagar, tempoMorte;
    Transform visual;
    Renderer rend;
    Quaternion rotacaoVisual = Quaternion.identity;
    Vector3 posicaoVisual;

    class Osso { public Transform t; public Quaternion baseRel; }
    Osso coluna, bracoE, bracoD, coxaE, coxaD, canelaE, canelaD;
    float fase, intensidade, ataqueT, levantarBracos;
    float atordoadoAte, recuoCorpo, proximoGemido;
    Renderer[] rends;
    Material[][] materiaisOriginais;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        visual = modelo != null ? modelo.parent : null;
        if (visual != null)
        {
            rotacaoVisual = visual.localRotation;
            posicaoVisual = visual.localPosition;
        }
        rend = modelo != null ? modelo.GetComponentInChildren<Renderer>() : null;
        rends = modelo != null ? modelo.GetComponentsInChildren<Renderer>() : new Renderer[0];
        materiaisOriginais = rends.Select(r => r.sharedMaterials).ToArray();
        proximoGemido = Time.time + Random.Range(1f, 6f);
        if (animator == null && modelo != null) animator = modelo.GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController == null) animator = null;
        if (animator != null) animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        else AcharOssos();
    }

    // zumbi rápido: corre, tem menos vida (o spawner ajusta) e cor avermelhada
    public void DefinirRapido(Material material)
    {
        Rapido = true;
        if (animator != null) animator.speed = 1.25f;
        if (material == null) return;
        foreach (var r in rends) r.sharedMaterials = Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray();
    }

    // levou tiro: para um instante, é empurrado e o corpo dá um tranco pra trás
    public void Atordoar(Vector3 direcao, float forca)
    {
        if (EstaMorto) return;
        alertado = true;
        if (animator != null && !Atordoado) animator.SetTrigger("Dano");
        atordoadoAte = Mathf.Max(atordoadoAte, Time.time + 0.1f + 0.15f * Mathf.Min(forca, 2f));
        recuoCorpo = Mathf.Min(40f, recuoCorpo + forca * 22f);
        direcao.y = 0f;
        if (agent.enabled && agent.isOnNavMesh) agent.Move(direcao.normalized * 0.12f * Mathf.Min(forca, 3f));
    }

    void Som(AudioClip clip, float volume)
    {
        if (SomManager.Instance != null) SomManager.Instance.Tocar(clip, transform.position + Vector3.up * 1.5f, volume, Rapido ? 1.25f : 1f, 0.12f, 28f);
    }

    void AcharOssos()
    {
        if (modelo == null) return;
        var todos = modelo.GetComponentsInChildren<Transform>();
        Osso Achar(string nome)
        {
            var re = new Regex("^bip " + nome + @"\d*$");
            // se tiver mais de um (Spine, Spine1...), pega o mais perto da raiz
            var t = todos.Where(x => re.IsMatch(x.name)).OrderBy(Profundidade).FirstOrDefault();
            return t == null ? null : new Osso { t = t, baseRel = Quaternion.Inverse(transform.rotation) * t.rotation };
        }
        coluna = Achar("Spine");
        bracoE = Achar("L UpperArm");
        bracoD = Achar("R UpperArm");
        coxaE = Achar("L Thigh");
        coxaD = Achar("R Thigh");
        canelaE = Achar("L Calf");
        canelaD = Achar("R Calf");
    }

    static int Profundidade(Transform t)
    {
        int n = 0;
        for (; t != null; t = t.parent) n++;
        return n;
    }

    public void Configurar(float multVelocidade, float multDanoAtaque, bool cacarSempre)
    {
        multVel = multVelocidade;
        multDano = multDanoAtaque;
        sempreCacar = cacarSempre;
    }

    public void Alertar() => alertado = true;

    // o mais perto entre o Marcos e a Lia (com preferência pelo Marcos)
    void EscolherAlvo()
    {
        proximaEscolha = Time.time + Random.Range(0.4f, 0.6f);
        alvoT = null;
        alvoD = null;
        float melhor = float.MaxValue;
        var ph = PlayerHealth.Instance;
        if (ph != null && ph.EstaVivo)
        {
            alvoT = ph.transform;
            alvoD = ph;
            melhor = Vector3.Distance(transform.position, ph.transform.position);
        }
        foreach (var a in Aliados.Lista)
        {
            if ((a as Object) == null || !a.EstaVivo) continue;
            float d = Vector3.Distance(transform.position, a.transform.position) * pesoAliado;
            if (d >= melhor) continue;
            melhor = d;
            alvoT = a.transform;
            alvoD = a;
        }
    }

    // usado pelo jogador pra abrir caminho na horda (respeita o NavMesh)
    public void Empurrar(Vector3 deslocamento)
    {
        if (!EstaMorto && agent.enabled && agent.isOnNavMesh) agent.Move(deslocamento);
    }

    void Update()
    {
        if (EstaMorto) { AnimarMorte(); return; }
        if (!agent.isOnNavMesh) return;
        if (animator != null) animator.SetFloat("Speed", agent.velocity.magnitude / velocidadeAnimacaoRef, 0.1f, Time.deltaTime);

        if (Time.time >= proximaEscolha || (alvoD as Object) == null || !alvoD.EstaVivo) EscolherAlvo();
        bool podeCacar = alvoD != null && alvoD.EstaVivo && (GameManager.Instance == null || GameManager.Instance.JogoAtivo);
        float dist = podeCacar ? Vector3.Distance(transform.position, alvoT.position) : float.MaxValue;

        if (Time.time >= proximoGemido && dist < 35f)
        {
            proximoGemido = Time.time + Random.Range(4f, 10f);
            if (SomManager.Instance != null) Som(SomManager.Sortear(SomManager.Instance.gemidos), 0.8f);
        }

        // cambaleando depois de levar tiro
        if (Atordoado)
        {
            agent.isStopped = true;
            return;
        }

        if (podeCacar && (sempreCacar || alertado || dist < raioDeteccao))
        {
            alertado = true;
            agent.speed = velocidadePerseguir * multVel * (Rapido ? multVelocidadeRapido : 1f);
            if (dist <= alcanceAtaque)
            {
                // acabou de chegar: prepara o golpe (dá tempo de reagir e espalha os ataques da horda)
                if (!atacando)
                {
                    proximoAtaque = Mathf.Max(proximoAtaque, Time.time + preparoAtaque + Random.Range(0f, 0.3f));
                    // alinha o movimento do braço com o momento do dano
                    ataqueT = intervaloAtaque * 0.25f - (proximoAtaque - Time.time);
                }
                atacando = true;
                agent.isStopped = true;
                Vector3 olhar = alvoT.position - transform.position;
                olhar.y = 0f;
                if (olhar.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(olhar), 10f * Time.deltaTime);
                // começa o soco um pouco antes do dano, pra bater junto
                if (animator != null && !golpeAnimado && proximoAtaque - Time.time <= 0.35f)
                {
                    animator.SetTrigger("Atacar");
                    golpeAnimado = true;
                }
                if (Time.time >= proximoAtaque)
                {
                    proximoAtaque = Time.time + intervaloAtaque;
                    golpeAnimado = false;
                    alvoD.ReceberDano(danoAtaque * multDano);
                    if (SomManager.Instance != null) Som(SomManager.Instance.zumbiAtaque, 0.9f);
                }
            }
            else
            {
                atacando = false;
                golpeAnimado = false;
                agent.isStopped = false;
                if (Time.time >= proximoRepath && !agent.pathPending) // não recomeça um caminho que ainda está sendo calculado
                {
                    proximoRepath = Time.time + 0.25f;
                    agent.SetDestination(alvoT.position);
                }
            }
        }
        else if (ficarParado)
        {
            // zumbi fixo (escola, clínica): espera parado no escuro
            atacando = false;
            agent.isStopped = true;
        }
        else
        {
            atacando = false;
            agent.isStopped = false;
            agent.speed = velocidadeVagar * multVel;
            if (Time.time >= proximoVagar || (!agent.pathPending && agent.remainingDistance < 1f))
            {
                proximoVagar = Time.time + Random.Range(4f, 8f);
                Vector3 p = transform.position + Random.insideUnitSphere * 10f;
                if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
            }
        }
    }

    // animação procedural

    void LateUpdate()
    {
        if (animator != null || EstaMorto || coxaE == null || coxaD == null) return;
        if (rend != null && !rend.isVisible && !AnimarForaDaTela) return; // fora da tela: não gasta

        float dt = Time.deltaTime;
        float vel = agent.enabled ? agent.velocity.magnitude : 0f;
        intensidade = Mathf.MoveTowards(intensidade, Mathf.Clamp01(vel / 1.2f), dt * 4f);
        fase += vel * dt * (2f * Mathf.PI / comprimentoCiclo);
        if (atacando) ataqueT += dt;
        levantarBracos = Mathf.MoveTowards(levantarBracos, alertado ? 1f : 0.45f, dt * 2f);

        float s = Mathf.Sin(fase), c = Mathf.Cos(fase), k = intensidade;
        float golpe = atacando ? Mathf.Max(0f, Mathf.Sin(ataqueT * 2f * Mathf.PI / intervaloAtaque)) : 0f;
        Vector3 lado = transform.right, frente = transform.forward;
        recuoCorpo = Mathf.MoveTowards(recuoCorpo, 0f, 120f * dt);
        float inclinacao = inclinacaoCorpo + (Rapido ? 14f * k : 0f);

        // corpo: inclina pra frente, balança de um lado pro outro a cada passo, avança no golpe, tranco ao levar tiro
        Aplicar(coluna, Quaternion.AngleAxis(inclinacao * (0.6f + 0.4f * k) + golpe * 20f - recuoCorpo, lado) * Quaternion.AngleAxis(s * 6f * k, frente));

        // braços esticados pra frente (zumbi), balançando um pouco; no golpe descem com força
        float braco = anguloBracos * levantarBracos - golpe * 55f;
        Aplicar(bracoE, Quaternion.AngleAxis(-(braco + s * 8f * k), lado));
        Aplicar(bracoD, Quaternion.AngleAxis(-(braco - s * 8f * k), lado));

        // pernas: coxa vai e volta; joelho dobra quando a perna está avançando
        float coxa = anguloPerna * s * k;
        Aplicar(coxaE, Quaternion.AngleAxis(-coxa, lado));
        Aplicar(coxaD, Quaternion.AngleAxis(coxa, lado));
        Aplicar(canelaE, Quaternion.AngleAxis(-coxa + anguloJoelho * k * Mathf.Max(0f, c), lado));
        Aplicar(canelaD, Quaternion.AngleAxis(coxa + anguloJoelho * k * Mathf.Max(0f, -c), lado));

        // sobe e desce: mais baixo com as pernas abertas
        if (visual != null) visual.localPosition = posicaoVisual + Vector3.down * (balanco * k * Mathf.Abs(s));
    }

    // rotação absoluta (relativa ao zumbi), aplicada do pai pro filho
    void Aplicar(Osso o, Quaternion extra)
    {
        if (o == null) return;
        o.t.rotation = extra * (transform.rotation * o.baseRel);
    }

    // morte / pool

    public void Morrer()
    {
        if (EstaMorto) return;
        EstaMorto = true;
        tempoMorte = 0f;
        if (agent.isOnNavMesh) agent.isStopped = true;
        agent.enabled = false;
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        if (SomManager.Instance != null) Som(SomManager.Instance.zumbiMorte, 0.9f);
        if (animator != null)
        {
            animator.speed = 1f;
            animator.SetFloat("Speed", 0f);
            animator.ResetTrigger("Dano");
            animator.ResetTrigger("Atacar");
            animator.SetTrigger("Morrer");
        }
    }

    // volta a ser usado pelo pool do WaveSpawner (sem Instantiate/Destroy = sem engasgo)
    public void Reativar(Vector3 pos, Quaternion rot)
    {
        EstaMorto = false;
        alertado = atacando = golpeAnimado = false;
        alvoT = null;
        alvoD = null;
        proximaEscolha = 0f;
        tempoMorte = fase = intensidade = ataqueT = 0f;
        if (animator != null)
        {
            // volta pro estado inicial (estava na animação de morte)
            animator.Rebind();
            animator.speed = 1f;
        }
        if (visual != null)
        {
            visual.localRotation = rotacaoVisual;
            visual.localPosition = posicaoVisual;
        }
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = true;
        transform.SetPositionAndRotation(pos, rot);
        agent.enabled = true;
        agent.Warp(pos);
        agent.isStopped = false;
        multVel = multDano = 1f;
        sempreCacar = false;
        atordoadoAte = recuoCorpo = 0f;
        proximoGemido = Time.time + Random.Range(1f, 6f);
        if (Rapido)
        {
            Rapido = false;
            for (int i = 0; i < rends.Length; i++) rends[i].sharedMaterials = materiaisOriginais[i];
        }
    }

    void AnimarMorte()
    {
        tempoMorte += Time.deltaTime;
        if (visual != null && animator == null)
        {
            // tomba pra trás
            float k = Mathf.Clamp01(tempoMorte / 0.5f);
            visual.localRotation = Quaternion.Euler(-85f * k * k, 0f, 0f) * rotacaoVisual;
        }
        if (tempoMorte > 3.5f) transform.position += Vector3.down * 0.3f * Time.deltaTime; // afunda no chão e some
        if (tempoMorte > 6f)
        {
            if (!voltarAoPool) gameObject.SetActive(false); // zumbi fixo de interior: só some
            else if (WaveSpawner.Instance != null) WaveSpawner.Instance.Devolver(this);
            else Destroy(gameObject);
        }
    }
}
