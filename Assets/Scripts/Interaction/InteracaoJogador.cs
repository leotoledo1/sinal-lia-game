using UnityEngine;
using UnityEngine.InputSystem;

// Detecta o objeto interativo mais proximo do jogador, mostra a dica na tela e ativa ao apertar E.
public class InteracaoJogador : MonoBehaviour
{
    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.JogoAtivo)
        {
            Interativo.DefinirPerto(null);
            return;
        }
        var perto = Interativo.MaisPerto(transform.position);
        Interativo.DefinirPerto(perto);
        if (perto == null) return;
        HUD.Dica(perto.texto);
        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame) perto.Usar();
    }
}
