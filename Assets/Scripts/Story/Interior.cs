using System.Collections.Generic;
using UnityEngine;

// Interior da escola / da clínica: área separada da cidade (bem embaixo do mapa, y = -500),
// montada pelo menu "Sinal da Lia > Montar Cena". Entrar/sair pela porta: a tela escurece, o Marcos é
// teleportado, a luz vira a do interior (sem sol, lanterna ligada) e as ondas de zumbi param (aqui dentro
// os zumbis são fixos, posicionados pela ferramenta).
public class Interior : MonoBehaviour
{
    // onde o Marcos está agora (null = na rua)
    public static Interior Atual { get; private set; }
    static readonly List<Interior> todos = new List<Interior>();
    public static IReadOnlyList<Interior> Todos => todos;

    public string nome = "Escola";
    [Tooltip("Onde o Marcos aparece ao entrar (olhando pra dentro)")]
    public Transform entrada;
    [Tooltip("Onde o Marcos aparece ao sair (na calçada, de costas pra porta)")]
    public Transform saida;
    [Tooltip("Porta da rua (fora) e a porta de sair (dentro)")]
    public Interativo portaFora, portaDentro;
    [Tooltip("Zumbis que já estão lá dentro (ligados na 1ª vez que o Marcos entra)")]
    public GameObject zumbis;
    [Tooltip("Comida, kit e munição criados na 1ª entrada (o nome do ponto diz o quê: Comida, KitMedico, Municao)")]
    public Transform[] pontosItens;

    [Header("Luz do interior (o DayNightController usa enquanto o Marcos está aqui)")]
    public Color ambienteCeu = new Color(0.05f, 0.055f, 0.07f);
    public Color ambienteMeio = new Color(0.04f, 0.042f, 0.05f);
    public Color ambienteChao = new Color(0.02f, 0.02f, 0.025f);
    public Color corNeblina = new Color(0.01f, 0.012f, 0.016f);
    public float neblina = 0.035f;
    public float distanciaVisao = 70f;
    [Header("Cor do filme")]
    public float exposicao = 0.45f;
    public float contraste = 22f;
    public float saturacao = -14f;
    public float temperatura = -8f;
    public float vinheta = 0.46f;
    public float bloom = 0.9f;
    [Tooltip("Volume da música aqui dentro (1 = normal)")]
    public float volumeMusica = 0.45f;

    public bool Visitado { get; private set; }
    public bool Dentro => Atual == this;
    // zumbis vivos daqui de dentro "dormem" enquanto o Marcos está na rua (e acordam quando ele volta)
    readonly List<ZombieAI> adormecidos = new List<ZombieAI>();
    // roteiro (EscolaInterior / ClinicaInterior) escuta
    public event System.Action<bool> AoEntrar; // true = 1ª vez
    public event System.Action AoSair;

    void OnEnable()
    {
        if (!todos.Contains(this)) todos.Add(this);
        Interativo.Usado += AoUsar;
    }

    void OnDisable()
    {
        todos.Remove(this);
        Interativo.Usado -= AoUsar;
        if (Atual == this) Atual = null;
    }

    void AoUsar(Interativo i)
    {
        var gm = GameManager.Instance;
        if (gm == null || i == null) return;
        if (i == portaFora) gm.EntrarInterior(this);
        else if (i == portaDentro && Dentro) gm.SairInterior();
    }

    // chamado pelo GameManager com a tela preta
    public void Entrou()
    {
        Atual = this;
        bool primeira = !Visitado;
        if (primeira)
        {
            Visitado = true;
            if (zumbis != null) LigarZumbis(zumbis, false);
            CriarItens();
        }
        foreach (var z in adormecidos)
            if (z != null && !z.EstaMorto) Acordar(z);
        adormecidos.Clear();
        AoEntrar?.Invoke(primeira);
    }

    public void Saiu()
    {
        if (Atual == this) Atual = null;
        foreach (var z in GetComponentsInChildren<ZombieAI>())
        {
            if (!z.EstaMorto) adormecidos.Add(z);
            z.gameObject.SetActive(false);
        }
        AoSair?.Invoke();
    }

    static void Acordar(ZombieAI z)
    {
        z.gameObject.SetActive(true);
        var ag = z.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (ag != null && UnityEngine.AI.NavMesh.SamplePosition(z.transform.position, out var h, 2f, UnityEngine.AI.NavMesh.AllAreas))
        {
            ag.enabled = true;
            ag.Warp(h.position);
        }
    }

    void CriarItens()
    {
        if (pontosItens == null) return;
        var pw = PlayerWeapons.Instance;
        foreach (var p in pontosItens)
        {
            if (p == null) continue;
            if (p.name.StartsWith("Comida")) ItemPickup.Criar(TipoItem.Comida, p.position);
            else if (p.name.StartsWith("KitMedico")) ItemPickup.Criar(TipoItem.KitMedico, p.position);
            else if (p.name.StartsWith("Municao") && pw != null && pw.armas != null)
            {
                // munição da escopeta (a 1ª arma achada); se não tiver, de qualquer arma que gaste bala
                WeaponData arma = null;
                foreach (var a in pw.armas) if (a != null && !a.municaoInfinita) { arma = a; break; }
                if (arma != null) AmmoPickup.Criar(arma, arma.tamanhoPente, p.position, 0f);
            }
        }
    }

    // liga um grupo de zumbis fixos (põe cada um em cima do NavMesh do interior).
    // cacar = já sabem onde o Marcos está (fuga da escola, susto da clínica)
    public static void LigarZumbis(GameObject grupo, bool cacar)
    {
        if (grupo == null) return;
        grupo.SetActive(true);
        foreach (var z in grupo.GetComponentsInChildren<ZombieAI>(true))
        {
            if (z.EstaMorto) continue;
            Acordar(z);
            if (cacar)
            {
                z.Configurar(1f, 1f, true);
                z.Alertar();
            }
        }
    }
}
