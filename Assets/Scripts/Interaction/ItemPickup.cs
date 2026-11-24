using System.Collections.Generic;
using UnityEngine;

public enum TipoItem { Comida, KitMedico }

// Comida (latas) ou kit médico no chão: flutua, gira e é pego ao encostar.
// Comida enche a fome na hora; o kit vai pro inventário (usar com Q).
public class ItemPickup : MonoBehaviour
{
    public TipoItem tipo;
    public float raioColeta = 1.6f;
    public float valorComida = 45f;

    Vector3 basePos;
    Transform giro;
    static readonly Dictionary<Color, Material> materiais = new Dictionary<Color, Material>();

    public static ItemPickup Criar(TipoItem tipo, Vector3 pos)
    {
        var go = new GameObject(tipo == TipoItem.Comida ? "Comida" : "KitMedico");
        go.transform.position = pos + Vector3.up * 0.45f;
        var p = go.AddComponent<ItemPickup>();
        p.tipo = tipo;
        p.MontarVisual();
        return p;
    }

    void MontarVisual()
    {
        giro = new GameObject("Giro").transform;
        giro.SetParent(transform, false);
        if (tipo == TipoItem.Comida)
        {
            // duas latas: corpo de metal + rótulo colorido
            Peca(PrimitiveType.Cylinder, new Vector3(-0.1f, 0f, 0f), new Vector3(0.17f, 0.11f, 0.17f), new Color(0.72f, 0.72f, 0.74f));
            Peca(PrimitiveType.Cylinder, new Vector3(-0.1f, 0f, 0f), new Vector3(0.175f, 0.07f, 0.175f), new Color(0.85f, 0.3f, 0.1f));
            Peca(PrimitiveType.Cylinder, new Vector3(0.1f, -0.03f, 0.02f), new Vector3(0.17f, 0.08f, 0.17f), new Color(0.72f, 0.72f, 0.74f));
            Peca(PrimitiveType.Cylinder, new Vector3(0.1f, -0.03f, 0.02f), new Vector3(0.175f, 0.05f, 0.175f), new Color(0.25f, 0.55f, 0.25f));
        }
        else
        {
            // maleta branca com cruz vermelha na frente, atrás e em cima
            var vermelho = new Color(0.85f, 0.08f, 0.08f);
            Peca(PrimitiveType.Cube, Vector3.zero, new Vector3(0.44f, 0.28f, 0.3f), new Color(0.93f, 0.93f, 0.9f));
            foreach (float z in new[] { -0.152f, 0.152f })
            {
                Peca(PrimitiveType.Cube, new Vector3(0f, 0f, z), new Vector3(0.2f, 0.06f, 0.006f), vermelho);
                Peca(PrimitiveType.Cube, new Vector3(0f, 0f, z), new Vector3(0.06f, 0.2f, 0.006f), vermelho);
            }
            Peca(PrimitiveType.Cube, new Vector3(0f, 0.142f, 0f), new Vector3(0.2f, 0.006f, 0.06f), vermelho);
            Peca(PrimitiveType.Cube, new Vector3(0f, 0.142f, 0f), new Vector3(0.06f, 0.006f, 0.2f), vermelho);
        }

        // base colorida pra ver de longe (laranja = comida, vermelho = kit)
        var baseCor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(baseCor.GetComponent<Collider>());
        baseCor.transform.SetParent(transform, false);
        baseCor.transform.localPosition = Vector3.down * 0.42f;
        baseCor.transform.localScale = new Vector3(0.6f, 0.02f, 0.6f);
        var r = baseCor.GetComponent<Renderer>();
        r.sharedMaterial = Mat(r, tipo == TipoItem.Comida ? new Color(1f, 0.6f, 0.15f) : new Color(0.9f, 0.15f, 0.15f));
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Peca(PrimitiveType forma, Vector3 pos, Vector3 escala, Color cor)
    {
        var go = GameObject.CreatePrimitive(forma);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(giro, false);
        go.transform.localPosition = pos;
        go.transform.localScale = escala;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = Mat(r, cor);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // um material por cor, compartilhado entre todos os itens
    static Material Mat(Renderer r, Color cor)
    {
        return MaterialBase.Cor(cor, r); // (copiar o material padrão deixava rosa na build)
    }

    void Start() => basePos = transform.position;

    void Update()
    {
        giro.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 2f + basePos.x) * 0.08f;

        var ph = PlayerHealth.Instance;
        if (ph == null || !ph.EstaVivo || (GameManager.Instance != null && !GameManager.Instance.JogoAtivo)) return;
        Vector3 d = ph.transform.position - basePos;
        d.y = 0f;
        if (d.magnitude > raioColeta) return;

        bool pegou;
        if (tipo == TipoItem.Comida)
        {
            pegou = PlayerHunger.Instance != null && PlayerHunger.Instance.Comer(valorComida);
            if (pegou) HUD.Aviso($"Comeu uma lata (fome +{valorComida:0})");
        }
        else
        {
            pegou = ph.PegarKit();
            if (pegou) HUD.Aviso($"+1 kit médico ({ph.Kits}/{ph.kitsMaximo}) - aperte Q pra usar");
        }
        if (!pegou) return;
        if (SomManager.Instance != null) SomManager.Instance.Tocar2D(SomManager.Instance.pegarItem, 0.7f);
        Destroy(gameObject);
    }
}
