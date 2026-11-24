using System.Collections.Generic;
using UnityEngine;

// Material pros objetos criados DURANTE o jogo (helicóptero reserva, itens, caixas de munição, desenho).
// Antes eles copiavam o material padrão do Unity, que não vai garantido pra build: ficavam ROSA no .exe.
// Este vem de Assets/Resources/MaterialBase.mat (tudo em Resources sempre entra na build).
public static class MaterialBase
{
    static Material lit;
    static readonly Dictionary<Color, Material> porCor = new Dictionary<Color, Material>();

    public static Material Lit
    {
        get
        {
            if (lit == null) lit = Resources.Load<Material>("MaterialBase");
            return lit;
        }
    }

    // um material por cor, compartilhado
    public static Material Cor(Color cor, Renderer reserva = null)
    {
        if (porCor.TryGetValue(cor, out var m) && m != null) return m;
        var origem = Lit != null ? Lit : (reserva != null ? reserva.sharedMaterial : null);
        m = origem != null ? new Material(origem) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = cor;
        porCor[cor] = m;
        return m;
    }
}
