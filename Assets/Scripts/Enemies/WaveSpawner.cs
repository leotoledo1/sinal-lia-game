using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class ConfigOnda
{
    public int maxVivos = 6;
    public float intervalo = 5f;
    public float multVida = 1f;
    public float multVelocidade = 1f;
    public float multDano = 1f;
    public bool sempreCacar;
    [Range(0f, 1f)] public float chanceRapido;
}

// Nasce zumbis num anel em volta do jogador, sobre o NavMesh.
// Usa um pool: os zumbis são criados no início e reaproveitados (criar/destruir no meio do jogo dá engasgo).
public class WaveSpawner : MonoBehaviour
{
    public static WaveSpawner Instance { get; private set; }

    public GameObject prefabZumbi;
    [Tooltip("Visuais diferentes; sorteia um a cada zumbi (vazio = usa o prefabZumbi)")]
    public GameObject[] variantes;
    public float raioMin = 18f;
    public float raioMax = 35f;
    public float distanciaDespawn = 70f;
    [Tooltip("Zumbis criados no início (maior horda + os que estão morrendo)")]
    public int tamanhoPool = 34;
    [Tooltip("Material avermelhado do zumbi rápido")]
    public Material materialRapido;
    [Tooltip("Vida do zumbi rápido em relação ao normal")]
    public float vidaRapido = 0.6f;

    readonly List<ZombieAI> vivos = new List<ZombieAI>();
    readonly List<ZombieAI> livres = new List<ZombieAI>();
    Transform pool;
    ConfigOnda config;
    float proximoSpawn;
    NavMeshPath caminho;
    static readonly System.Predicate<ZombieAI> MortoOuDestruido = z => z == null || z.EstaMorto;
    static readonly System.Predicate<ZombieAI> Destruido = z => z == null;

    void Awake()
    {
        Instance = this;
        caminho = new NavMeshPath();
        // filhos de um objeto desligado ficam "guardados" sem rodar nem desenhar
        var go = new GameObject("PoolZumbis");
        go.SetActive(false);
        pool = go.transform;
        for (int i = 0; i < tamanhoPool; i++) livres.Add(CriarNoPool(i));
    }

    GameObject Prefab(int i)
    {
        if (variantes != null && variantes.Length > 0) return variantes[i % variantes.Length];
        return prefabZumbi;
    }

    ZombieAI CriarNoPool(int i)
    {
        var p = Prefab(i);
        if (p == null) return null;
        return Instantiate(p, pool).GetComponent<ZombieAI>();
    }

    public void Configurar(ConfigOnda c)
    {
        config = c;
        // respiro no começo de cada fase (a tela ainda está clareando)
        proximoSpawn = Time.time + 3f;
    }

    // alguns segundos sem nascer zumbi (ex.: acabou de sair de um interior)
    public void Respiro(float segundos) => proximoSpawn = Mathf.Max(proximoSpawn, Time.time + segundos);

    public void LimparTodos()
    {
        foreach (var z in vivos) if (z != null) Devolver(z);
        vivos.Clear();
    }

    public void Devolver(ZombieAI z)
    {
        if (z == null || z.transform.parent == pool) return;
        z.transform.SetParent(pool, false);
        livres.Add(z);
    }

    // mostra 1 de cada visual na frente da câmera por 2 quadros (compila shaders antes do jogo começar)
    public IEnumerator Aquecer(Camera cam)
    {
        if (cam == null) yield break;
        var usados = new List<ZombieAI>();
        int n = variantes != null && variantes.Length > 0 ? variantes.Length : 1;
        for (int i = 0; i < n && i < livres.Count; i++)
        {
            var z = livres[i];
            if (z == null) continue;
            z.GetComponent<NavMeshAgent>().enabled = false; // fora do NavMesh, sem IA
            z.transform.position = cam.transform.position + cam.transform.forward * 4f + cam.transform.right * (i - n / 2f) * 0.8f - Vector3.up * 1f;
            z.transform.SetParent(null, true);
            usados.Add(z);
        }
        yield return null;
        yield return null;
        foreach (var z in usados) if (z != null) z.transform.SetParent(pool, false);
    }

    void Update()
    {
        vivos.RemoveAll(MortoOuDestruido);
        livres.RemoveAll(Destruido);
        var jogador = PlayerHealth.Instance;
        if (jogador == null) return;

        // recicla zumbis que ficaram muito longe
        for (int i = vivos.Count - 1; i >= 0; i--)
        {
            if (Vector3.Distance(vivos[i].transform.position, jogador.transform.position) > distanciaDespawn)
            {
                Devolver(vivos[i]);
                vivos.RemoveAt(i);
            }
        }

        if (config == null) return;
        if (GameManager.Instance != null && !GameManager.Instance.JogoAtivo) return;
        // dentro da escola/clínica não nasce zumbi de onda (lá os zumbis são fixos)
        if (Interior.Atual != null) return;
        if (vivos.Count >= config.maxVivos || Time.time < proximoSpawn) return;
        proximoSpawn = Time.time + config.intervalo;
        TentarSpawn(jogador.transform.position);
    }

    void TentarSpawn(Vector3 centro)
    {
        if (!NavMesh.SamplePosition(centro, out var hitJogador, 3f, NavMesh.AllAreas)) return;
        for (int tentativa = 0; tentativa < 8; tentativa++)
        {
            Vector2 c = Random.insideUnitCircle.normalized * Random.Range(raioMin, raioMax);
            Vector3 p = centro + new Vector3(c.x, 0f, c.y);
            if (!NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) continue;
            if (hit.position.y > centro.y + 2f) continue; // telhado
            if (!NavMesh.CalculatePath(hit.position, hitJogador.position, NavMesh.AllAreas, caminho)) continue;
            if (caminho.status != NavMeshPathStatus.PathComplete) continue;

            ZombieAI ai;
            if (livres.Count > 0)
            {
                int k = Random.Range(0, livres.Count);
                ai = livres[k];
                livres.RemoveAt(k);
            }
            else ai = CriarNoPool(Random.Range(0, 100)); // pool vazio: cria mais um
            if (ai == null) return;

            var rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            ai.transform.SetPositionAndRotation(hit.position, rot);
            ai.transform.SetParent(null, true);
            ai.Reativar(hit.position, rot);
            ai.Configurar(config.multVelocidade, config.multDano, config.sempreCacar);
            bool rapido = Random.value < config.chanceRapido;
            if (rapido) ai.DefinirRapido(materialRapido);
            var vida = ai.GetComponent<ZombieHealth>();
            if (vida != null) vida.DefinirMultiplicador(config.multVida * (rapido ? vidaRapido : 1f));
            vivos.Add(ai);
            return;
        }
    }
}
