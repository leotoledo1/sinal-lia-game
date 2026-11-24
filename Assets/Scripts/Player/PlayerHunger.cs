using UnityEngine;

// Fome: cai com o tempo (mais rápido correndo). Com fome o Marcos não regenera; zerada, drena a vida.
// Comida (latas no mapa) enche a barra na hora.
public class PlayerHunger : MonoBehaviour
{
    public static PlayerHunger Instance { get; private set; }

    public float fomeMaxima = 100f;
    [Tooltip("Pontos perdidos por segundo andando (0,4 ≈ 4 min de barriga cheia até zerar)")]
    public float perdaPorSegundo = 0.4f;
    public float multCorrendo = 1.75f;
    [Tooltip("Vida perdida por segundo com a fome zerada")]
    public float danoFome = 1.5f;
    [Tooltip("Abaixo disso conta como 'com fome' (não regenera vida e o HUD avisa)")]
    public float limiteFome = 25f;

    public float Fome { get; private set; }
    public bool ComFome => Fome < limiteFome;

    PlayerHealth vida;
    PlayerController controle;
    float proximoAviso;

    void Awake()
    {
        Instance = this;
        Fome = fomeMaxima;
        vida = GetComponent<PlayerHealth>();
        controle = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.JogoAtivo) return;
        if (vida != null && !vida.EstaVivo) return;
        bool correndo = controle != null && controle.VelocidadeNormalizada > 0.75f;
        Fome = Mathf.Max(0f, Fome - perdaPorSegundo * (correndo ? multCorrendo : 1f) * Time.deltaTime);
        if (Fome <= 0f && vida != null) vida.PerderVida(danoFome * Time.deltaTime, "Marcos morreu de fome.");
        if (ComFome && Time.time > proximoAviso)
        {
            proximoAviso = Time.time + 20f;
            HUD.Aviso(Fome <= 0f ? "MORRENDO DE FOME! Ache comida (latas)." : "Com fome... procure comida (latas).");
        }
    }

    // retorna false se já está de barriga cheia (a comida fica no chão)
    public bool Comer(float qtd)
    {
        if (Fome >= fomeMaxima - 2f) return false;
        Fome = Mathf.Min(fomeMaxima, Fome + qtd);
        return true;
    }

    public void Definir(float valor) => Fome = Mathf.Clamp(valor, 0f, fomeMaxima);
}
