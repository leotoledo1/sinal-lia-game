using System.Collections;
using UnityEngine;

// Roteiro da clínica por dentro (Dia 2): achar a Lia vira "quente/frio" pelo walkie-talkie.
// O medidor de sinal do Dia 2 passa a valer em escala pequena (dentro da clínica); a Lia sussurra quando o pai
// chega perto; 2 esconderijos falsos (cortina, armário) com susto de zumbi; ela está no depósito, atrás de um
// armário que o Marcos empurra -> cena do reencontro -> os dois saem pra noite final.
public class ClinicaInterior : MonoBehaviour
{
    public static ClinicaInterior Instance { get; private set; }

    public Interior interior;
    [Tooltip("Onde a Lia está escondida (alvo do sinal)")]
    public Transform esconderijo;
    [Tooltip("Distância (m) em que o sinal zera aqui dentro")]
    public float distanciaSinal = 26f;
    public Interativo armario;
    public Deslizar armarioMovel;
    [Tooltip("Esconderijos falsos (cortina da maca, armário do consultório)")]
    public Interativo[] falsos;
    [Tooltip("O que abre em cada esconderijo falso (cortina/porta: some ou gira)")]
    public GameObject[] abrirFalso;
    [Tooltip("Zumbi que pula de cada esconderijo falso (desligado até abrir)")]
    public GameObject[] sustos;

    public bool Achou { get; private set; }
    public int FalsosAbertos { get; private set; }
    bool chegou, falouMeio, falouPerto, falouVazio;

    void Awake() => Instance = this;

    void OnEnable()
    {
        Instance = this;
        Interativo.Usado += AoUsar;
        if (interior != null)
        {
            interior.AoEntrar += Entrou;
            interior.AoSair += Saiu;
        }
    }

    void OnDisable()
    {
        Interativo.Usado -= AoUsar;
        if (interior != null)
        {
            interior.AoEntrar -= Entrou;
            interior.AoSair -= Saiu;
        }
    }

    static RadioSystem Radio => RadioSystem.Instance;

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || interior == null) return;
        if (interior.portaFora != null) interior.portaFora.ativo = gm.FaseAtual == Fase.Dia2 && !Achou && !gm.Acabou;
        if (!interior.Dentro || Achou || !gm.JogoAtivo || PlayerHealth.Instance == null || ObjectiveManager.Instance == null) return;
        // quente/frio: a Lia percebe o pai chegando
        float forca = ObjectiveManager.Instance.ForcaSinal(PlayerHealth.Instance.transform.position);
        if (!falouMeio && forca >= 0.45f)
        {
            falouMeio = true;
            Radio?.Enfileirar("Lia", "Pai... conta até três e me acha. Igual esconde-esconde...", "lia_11");
        }
        if (!falouPerto && forca >= 0.8f)
        {
            falouPerto = falouMeio = true;
            Radio?.LimparFila();
            Radio?.Enfileirar("Lia", "Tá quente, pai... tá muito quente!", "lia_12");
            HUD.Tutorial("O sinal está no máximo: ela está atrás de alguma coisa aqui perto");
        }
    }

    // GameManager: chegou perto da clínica (lá fora) seguindo o sinal
    public void Chegou()
    {
        if (chegou) return;
        chegou = true;
        Radio?.Enfileirar("Lia", "Pai? Cê tá aí fora? Eu tô aqui dentro... escondida. Entra devagar.", "lia_10");
        ObjectiveManager.Instance?.Definir("Entre na Clínica Central", interior != null && interior.portaFora != null ? interior.portaFora.transform : null, false);
        HUD.Tutorial("Chegue na PORTA da clínica e aperte E pra entrar");
    }

    void Entrou(bool primeira)
    {
        if (primeira)
        {
            Radio?.LimparFila();
            Radio?.Enfileirar("Marcos", "Lia? Sou eu, filha. Fala comigo pelo rádio... que eu te acho.", "marcos_18", true);
            HUD.LimparTutoriais();
            HUD.Tutorial("Quente ou frio: o SINAL DO RÁDIO no topo enche quando você chega perto da Lia");
        }
        if (!Achou) ObjectiveManager.Instance?.Definir("Ache a Lia: siga o SINAL", esconderijo, true, distanciaSinal);
    }

    void Saiu()
    {
        if (Achou) return;
        ObjectiveManager.Instance?.Definir("A Lia está na clínica: entre de novo", interior != null && interior.portaFora != null ? interior.portaFora.transform : null, false);
    }

    void AoUsar(Interativo i)
    {
        var gm = GameManager.Instance;
        if (gm == null || i == null || gm.FaseAtual != Fase.Dia2 || Achou) return;
        if (i == armario) { StartCoroutine(Empurrar()); return; }
        if (falsos == null) return;
        for (int k = 0; k < falsos.Length; k++)
            if (falsos[k] == i) { AbrirFalso(k); return; }
    }

    void AbrirFalso(int k)
    {
        falsos[k].ativo = false;
        FalsosAbertos++;
        if (abrirFalso != null && k < abrirFalso.Length && abrirFalso[k] != null)
        {
            var porta = abrirFalso[k].GetComponent<PortaInterior>();
            if (porta != null) porta.Abrir();
            else abrirFalso[k].SetActive(false);
        }
        // susto: o zumbi escondido ali pula em cima do Marcos
        if (sustos != null && k < sustos.Length && sustos[k] != null)
        {
            Interior.LigarZumbis(sustos[k], true);
            var som = SomManager.Instance;
            if (som != null) som.Tocar(som.zumbiAtaque, falsos[k].Posicao + Vector3.up, 1f, 0.8f, 0.05f, 30f);
            Camera.main?.GetComponent<ThirdPersonCamera>()?.Tremer(0.45f);
        }
        if (!falouVazio)
        {
            falouVazio = true;
            Radio?.Enfileirar("Marcos", "Não é aqui...!", "marcos_19", true);
        }
    }

    IEnumerator Empurrar()
    {
        if (armario != null) armario.ativo = false;
        Radio?.LimparFila();
        Radio?.Enfileirar("Marcos", "Atrás do armário... Lia? Segura, filha!", "marcos_20", true);
        if (armarioMovel != null)
        {
            armarioMovel.Empurrar();
            while (armarioMovel.Movendo) yield return null;
        }
        // deixa o Marcos terminar de falar (a cena do reencontro corta qualquer fala)
        for (float t = 0f; t < 3f && Radio != null && Radio.Ocupado; t += Time.deltaTime) yield return null;
        Achou = true;
        GameManager.Instance?.AchouLiaNaClinica();
    }
}
