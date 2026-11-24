using UnityEngine;

// Dados configuraveis de cada arma (dano, cadencia, recuo, municao e modelo 3D).
[CreateAssetMenu(menuName = "Sinal da Lia/Arma", fileName = "NovaArma")]
public class WeaponData : ScriptableObject
{
    public string nome = "Arma";
    [Header("Tiro")]
    public float dano = 20f;            // por projétil
    public float tirosPorSegundo = 4f;
    public bool automatica = false;
    public int projeteis = 1;           // escopeta > 1
    public float spread = 1.5f;         // graus (metade ao mirar)
    public float alcance = 60f;
    public float recuo = 1f;            // graus que a câmera sobe por tiro
    [Tooltip("Desligado = precisa achar a arma no mapa")]
    public bool liberadaNoInicio = true;
    [Tooltip("Spread ao mirar (-1 = metade do spread normal)")]
    public float spreadMirando = -1f;
    [Tooltip("Campo de visão ao mirar (0 = padrão da câmera). Sniper ~18")]
    public float fovMira = 0f;
    [Tooltip("Quantos zumbis a bala atravessa além do primeiro")]
    public int perfura = 0;
    [Tooltip("Força do empurrão no zumbi por projétil")]
    public float impacto = 0.5f;
    [Header("Munição")]
    public int tamanhoPente = 12;
    public int municaoInicial = 48;     // reserva inicial (fora do pente)
    public int municaoMaxima = 120;
    public bool municaoInfinita = false;
    public float tempoRecarga = 1.5f;
    public Color corMunicao = Color.yellow;
    [Header("Visual / som (opcionais)")]
    public GameObject modelo;
    public Vector3 posicaoNaMao = new Vector3(0.05f, 0.03f, 0.02f);
    public Vector3 rotacaoNaMao = new Vector3(0f, 90f, 90f);
    [Tooltip("Tamanho (m) do modelo na mão; a escala é ajustada sozinha")]
    public float comprimentoModelo = 0.3f;
    public AudioClip somTiro;
    public AudioClip somRecarga;
}
