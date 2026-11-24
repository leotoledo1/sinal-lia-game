using UnityEngine;
using UnityEngine.InputSystem;

// Controla a vida do jogador, regeneracao automatica e uso de kit medico (tecla Q).
public class PlayerHealth : MonoBehaviour, IDamageable
{
    public static PlayerHealth Instance { get; private set; }

    public float vidaMaxima = 100f;
    [Tooltip("Vida recuperada por segundo depois de um tempo sem levar dano (só sem fome)")]
    public float regeneracao = 1.5f;
    public float atrasoRegeneracao = 8f;

    [Header("Kit médico (tecla Q)")]
    public int kitsMaximo = 3;
    public float curaKit = 50f;

    public float Vida { get; private set; }
    public bool EstaVivo => Vida > 0f;
    public float UltimoDano { get; private set; } = -99f;
    public int Kits { get; private set; }

    void Awake()
    {
        Instance = this;
        Vida = vidaMaxima;
    }

    void Update()
    {
        if (!EstaVivo) return;
        bool semFome = PlayerHunger.Instance == null || !PlayerHunger.Instance.ComFome;
        if (semFome && Time.time - UltimoDano > atrasoRegeneracao)
            Vida = Mathf.Min(vidaMaxima, Vida + regeneracao * Time.deltaTime);

        // Tecla Q: usa um kit medico se tiver no inventario
        var kb = Keyboard.current;
        if (kb != null && kb.qKey.wasPressedThisFrame && (GameManager.Instance == null || GameManager.Instance.JogoAtivo))
            UsarKit();
    }

    public void ReceberDano(float dano)
    {
        if (!EstaVivo) return;
        if (GameManager.Instance != null && !GameManager.Instance.JogoAtivo) return;
        Vida = Mathf.Max(0f, Vida - dano);
        UltimoDano = Time.time;
        if (SomManager.Instance != null) SomManager.Instance.Tocar2D(SomManager.Instance.danoJogador, 0.8f, 0.1f);
        var cam = Camera.main != null ? Camera.main.GetComponent<ThirdPersonCamera>() : null;
        if (cam != null) cam.Tremer(0.35f);
        if (!EstaVivo && GameManager.Instance != null)
            GameManager.Instance.GameOver("Marcos foi pego pelos zumbis.");
    }

    // perda de vida sem "pancada" (fome): sem som nem tremida
    public void PerderVida(float qtd, string motivo)
    {
        if (!EstaVivo) return;
        if (GameManager.Instance != null && !GameManager.Instance.JogoAtivo) return;
        Vida = Mathf.Max(0f, Vida - qtd);
        if (!EstaVivo && GameManager.Instance != null) GameManager.Instance.GameOver(motivo);
    }

    public void Curar(float qtd) => Vida = Mathf.Min(vidaMaxima, Vida + qtd);

    public void DefinirKits(int n) => Kits = Mathf.Clamp(n, 0, kitsMaximo);

    // retorna false se já está com o máximo de kits (o kit fica no chão)
    public bool PegarKit()
    {
        if (Kits >= kitsMaximo) return false;
        Kits++;
        return true;
    }

    public bool UsarKit()
    {
        if (Kits <= 0) { HUD.Aviso("Sem kit médico"); return false; }
        if (Vida >= vidaMaxima - 1f) { HUD.Aviso("A vida já está cheia"); return false; }
        Kits--;
        Curar(curaKit);
        HUD.Aviso($"Kit médico usado (+{curaKit:0} de vida)");
        if (SomManager.Instance != null) SomManager.Instance.Tocar2D(SomManager.Instance.cura, 0.8f);
        return true;
    }
}
