using UnityEngine;
using UnityEngine.AI;

// Lia: agachada escondida na clínica -> acena quando o pai chega -> segue o Marcos na noite final.
// Tem vida e os zumbis também atacam ela. Se a Lia morrer, é Game Over.
public class LiaNPC : MonoBehaviour, IAliado
{
    public static LiaNPC Instance { get; private set; }

    public Animator animator;
    public float vidaMaxima = 200f;
    [Tooltip("Dano dos zumbis é multiplicado por isto (ela se encolhe atrás do pai)")]
    public float multDanoRecebido = 0.25f;
    public float regeneracao = 5f;
    public float atrasoRegeneracao = 5f;
    [Tooltip("Distância que ela fica atrás do pai")]
    public float distanciaSeguir = 2.2f;
    public float velocidadeAndar = 3.2f;
    public float velocidadeCorrer = 6.5f;
    [Tooltip("Velocidade (m/s) em que a animação de correr fica no máximo")]
    public float velocidadeAnimacaoRef = 5f;
    [Tooltip("Segundos acenando antes de começar a seguir")]
    public float tempoAceno = 2f;

    public bool Encontrada { get; private set; }
    public bool Seguindo { get; private set; }
    public float Vida { get; private set; }
    public bool EstaVivo => Vida > 0f;
    public float UltimoDano { get; private set; } = -99f;

    NavMeshAgent agent;
    float inicioSeguir, proximoDestino, proximoAvisoLonge, proximoAvisoDano;
    Vector3 destinoPedido = new Vector3(1e6f, 0f, 0f);

    void Awake()
    {
        Instance = this;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false; // liga quando começar a seguir
        Vida = vidaMaxima;
    }

    void OnDisable() => Aliados.Sair(this);

    public void Encontrar()
    {
        if (Encontrada) return;
        Encontrada = true;
        if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger("Achou");
        inicioSeguir = Time.time + tempoAceno;
    }

    void ComecarASeguir()
    {
        Seguindo = true;
        Aliados.Entrar(this);
        if (agent != null && NavMesh.SamplePosition(transform.position, out var h, 4f, NavMesh.AllAreas))
        {
            transform.position = h.position;
            agent.enabled = true;
            agent.Warp(h.position);
        }
    }

    public void ReceberDano(float dano)
    {
        if (!EstaVivo || !Seguindo) return;
        if (GameManager.Instance != null && !GameManager.Instance.JogoAtivo) return;
        Vida = Mathf.Max(0f, Vida - dano * multDanoRecebido);
        UltimoDano = Time.time;
        if (animator != null && Random.value < 0.35f) animator.SetTrigger("Dano");
        if (Time.time > proximoAvisoDano)
        {
            proximoAvisoDano = Time.time + 2f;
            HUD.Aviso("A LIA ESTÁ SENDO ATACADA!");
        }
        // "Pai! Me ajuda!" (no máximo a cada 9 s, e só se ninguém estiver falando)
        if (EstaVivo) RadioSystem.Instance?.Grito("lia_grito");
        if (!EstaVivo) Morrer();
    }

    // leva a Lia pra outro lugar (saindo da clínica com o pai / checkpoint da noite final)
    public void Posicionar(Vector3 pos, Quaternion rot)
    {
        if (NavMesh.SamplePosition(pos, out var h, 3f, NavMesh.AllAreas)) pos = h.position;
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(pos);
        else transform.position = pos;
        transform.rotation = rot;
        destinoPedido = new Vector3(1e6f, 0f, 0f);
    }

    // cena final: fica do lado do pai (se ficou pra trás, aparece perto dele)
    public void FicarAoLado(Transform pai)
    {
        if (pai == null || !EstaVivo) return;
        Vector3 lado = pai.position + pai.right * 1.1f - pai.forward * 0.4f;
        if (!NavMesh.SamplePosition(lado, out var h, 2f, NavMesh.AllAreas)) return;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.Warp(h.position);
            agent.isStopped = true;
        }
        else transform.position = h.position;
        transform.rotation = pai.rotation;
        if (animator != null) animator.SetFloat("Speed", 0f);
    }

    void Morrer()
    {
        Aliados.Sair(this);
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
        }
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.ResetTrigger("Dano");
            animator.SetTrigger("Morrer");
        }
        GameManager.Instance?.GameOver("A Lia foi pega pelos zumbis.");
    }

    void Update()
    {
        if (!Encontrada || !EstaVivo || PlayerHealth.Instance == null) return;
        Vector3 pai = PlayerHealth.Instance.transform.position;
        if (!Seguindo)
        {
            OlharPara(pai);
            // durante a cena do reencontro ela fica parada olhando o pai
            if (Time.time >= inicioSeguir && (GameManager.Instance == null || GameManager.Instance.JogoAtivo)) ComecarASeguir();
            return;
        }

        bool andando = agent != null && agent.enabled;
        if (animator != null) animator.SetFloat("Speed", andando ? agent.velocity.magnitude / velocidadeAnimacaoRef : 0f, 0.1f, Time.deltaTime);
        if (!andando || !agent.isOnNavMesh) return;
        if (GameManager.Instance != null && !GameManager.Instance.JogoAtivo)
        {
            agent.isStopped = true;
            return;
        }

        if (Time.time - UltimoDano > atrasoRegeneracao) Vida = Mathf.Min(vidaMaxima, Vida + regeneracao * Time.deltaTime);

        float dist = Vector3.Distance(transform.position, pai);
        agent.speed = dist > 6f ? velocidadeCorrer : velocidadeAndar;
        if (Time.time >= proximoDestino)
        {
            proximoDestino = Time.time + 0.25f;
            if (dist > distanciaSeguir + 0.6f)
            {
                // fica atrás do pai (do lado oposto de onde ele olha)
                Vector3 frente = PlayerHealth.Instance.transform.forward;
                frente.y = 0f;
                Vector3 alvo = pai - frente.normalized * distanciaSeguir;
                // só pede caminho novo quando o destino muda de verdade (> 1 m) e o anterior já foi calculado:
                // recalcular a cada 0,25 s fazia a Lia "patinar" no lugar a 60 fps (teste de 04/11/2026)
                if (NavMesh.SamplePosition(alvo, out var h, 2.5f, NavMesh.AllAreas))
                {
                    agent.isStopped = false;
                    if (!agent.pathPending && (!agent.hasPath || (h.position - destinoPedido).sqrMagnitude > 1f))
                    {
                        agent.SetDestination(h.position);
                        destinoPedido = h.position;
                    }
                }
            }
            else agent.isStopped = true;
        }
        if (agent.velocity.sqrMagnitude < 0.2f) OlharPara(pai);
        if (dist > 20f && Time.time > proximoAvisoLonge)
        {
            proximoAvisoLonge = Time.time + 6f;
            HUD.Aviso("Não deixe a Lia pra trás!");
        }
    }

    void OlharPara(Vector3 p)
    {
        Vector3 olhar = p - transform.position;
        olhar.y = 0f;
        if (olhar.sqrMagnitude > 0.1f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(olhar), 4f * Time.deltaTime);
    }
}
