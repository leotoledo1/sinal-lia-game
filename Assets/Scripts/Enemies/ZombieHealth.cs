using UnityEngine;

// Controla a vida do zumbi, registra abates e sorteia drop de municao ao morrer.
public class ZombieHealth : MonoBehaviour, IDamageable
{
    public float vidaBase = 100f;
    [Range(0f, 1f)] public float chanceDropMunicao = 0.35f;

    public float Vida { get; private set; }
    public bool EstaVivo => Vida > 0f;

    ZombieAI ai;

    void Awake()
    {
        ai = GetComponent<ZombieAI>();
        Vida = vidaBase;
    }

    // também usado pra "reviver" o zumbi quando ele sai do pool
    public void DefinirMultiplicador(float mult) => Vida = vidaBase * mult;

    public void ReceberDano(float dano)
    {
        if (!EstaVivo) return;
        Vida -= dano;
        if (ai != null) ai.Alertar();
        if (EstaVivo) return;

        if (GameManager.Instance != null) GameManager.Instance.Abates++;
        var pw = PlayerWeapons.Instance;
        if (pw != null && pw.armas.Length > 0 && Random.value < chanceDropMunicao)
        {
            var arma = SortearMunicao(pw);
            if (arma != null) AmmoPickup.Criar(arma, Mathf.Max(2, arma.tamanhoPente / 2), transform.position, 45f);
        }
        if (ai != null) ai.Morrer();
        else Destroy(gameObject);
    }

    // munição de uma arma que gasta bala (pistola é infinita: caixa dela não servia pra nada).
    // 75% das vezes é de uma arma que o Marcos já achou
    static WeaponData SortearMunicao(PlayerWeapons pw)
    {
        var achadas = new System.Collections.Generic.List<WeaponData>();
        var todas = new System.Collections.Generic.List<WeaponData>();
        for (int i = 0; i < pw.armas.Length; i++)
        {
            var a = pw.armas[i];
            if (a == null || a.municaoInfinita) continue;
            todas.Add(a);
            if (pw.Liberada(i)) achadas.Add(a);
        }
        var lista = achadas.Count > 0 && Random.value < 0.75f ? achadas : todas;
        return lista.Count > 0 ? lista[Random.Range(0, lista.Count)] : null;
    }
}
