using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Armas do jogador: tiro por raycast do centro da câmera, recarga, troca (1-4 / scroll).
// Progressão: só as armas "liberadas" podem ser usadas; as outras são achadas no mapa (Liberar).
public class PlayerWeapons : MonoBehaviour
{
    public static PlayerWeapons Instance { get; private set; }

    public WeaponData[] armas;
    public Camera cam;
    public ThirdPersonCamera cameraJogador;
    public Material materialTracer;
    public Material materialArmas;
    [Tooltip("Onde o modelo da arma fica (mão direita). Se vazio, usa o osso da mão do Animator humanoide")]
    public Transform pontoArma;
    [Tooltip("Multiplicador de dano no tiro na cabeça")]
    public float multCabeca = 2.2f;
    [Tooltip("Feixe de luz em cima das armas novas no mapa")]
    public Material materialFeixe;

    public int IndiceAtual { get; private set; }
    public WeaponData ArmaAtual => armas != null && armas.Length > 0 ? armas[IndiceAtual] : null;
    public int PenteAtual => pente[IndiceAtual];
    public int ReservaAtual => reserva[IndiceAtual];
    public bool Recarregando { get; private set; }

    int[] pente, reserva;
    bool[] liberada;
    float proximoTiro;
    GameObject[] modelos;
    Light flash;
    AudioSource audioSrc;
    PlayerHealth vida;
    Coroutine recarga;
    readonly RaycastHit[] hits = new RaycastHit[128];
    LineRenderer[] tracers;
    float[] fimTracer;
    int proximoTracer;
    float fimFlash;
    PlayerAnimator anim;

    void Awake()
    {
        Instance = this;
        AmmoPickup.MaterialArmas = materialArmas;
        AmmoPickup.MaterialFeixe = materialFeixe;
        vida = GetComponent<PlayerHealth>();
        if (cam == null) cam = Camera.main;
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.spatialBlend = 0f;

        pente = new int[armas.Length];
        reserva = new int[armas.Length];
        liberada = new bool[armas.Length];
        for (int i = 0; i < armas.Length; i++)
        {
            liberada[i] = armas[i].liberadaNoInicio;
            pente[i] = liberada[i] ? armas[i].tamanhoPente : 0;
            reserva[i] = liberada[i] ? armas[i].municaoInicial : 0;
        }

        var luz = new GameObject("FlashTiro");
        luz.transform.SetParent(transform, false);
        luz.transform.localPosition = new Vector3(0.3f, 1.4f, 0.8f);
        flash = luz.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.range = 8f;
        flash.intensity = 4f;
        flash.color = new Color(1f, 0.8f, 0.4f);
        flash.enabled = false;
    }

    void Start()
    {
        CriarModelos();
        Equipar(0);
        // cria os rastros já no início (e aquece o material) em vez de no 1º tiro
        if (cam != null) CriarTracer(cam.transform.position + cam.transform.forward * 3f, cam.transform.position + cam.transform.forward * 8f);
    }

    void CriarModelos()
    {
        modelos = new GameObject[armas.Length];
        Transform mao = pontoArma;
        if (mao == null)
        {
            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman) mao = anim.GetBoneTransform(HumanBodyBones.RightHand);
        }
        if (mao == null) return;
        for (int i = 0; i < armas.Length; i++) modelos[i] = CriarModeloNaMao(armas[i], mao, materialArmas, ajusteNaMao);
    }

    [Tooltip("Desloca todas as armas em relação ao ponto da mão (Z = pra frente do cano). Mão grande do estilo: a arma fica na frente dos dedos")]
    public Vector3 ajusteNaMao = new Vector3(0f, 0.06f, 0.03f);

    // modelo da arma preso na mão
    public static GameObject CriarModeloNaMao(WeaponData arma, Transform mao, Material material, Vector3 ajuste = default)
    {
        if (arma == null || arma.modelo == null || mao == null) return null;
        var m = Instantiate(arma.modelo, mao);
        m.transform.localPosition = arma.posicaoNaMao + ajuste;
        m.transform.localRotation = Quaternion.Euler(arma.rotacaoNaMao);
        foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
        // ajusta a escala pelo tamanho real do modelo
        var rends = m.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            float maior = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (maior > 0.0001f) m.transform.localScale *= arma.comprimentoModelo / maior;
        }
        if (material != null)
            foreach (var r in rends)
            {
                var mats = r.sharedMaterials;
                for (int k = 0; k < mats.Length; k++) mats[k] = material;
                r.sharedMaterials = mats;
            }
        return m;
    }

    public bool Liberada(int i) => i >= 0 && i < liberada.Length && liberada[i];

    // achou a arma no mapa: libera, enche o pente, soma a munição e já equipa
    public void Liberar(WeaponData arma)
    {
        int i = System.Array.IndexOf(armas, arma);
        if (i < 0) return;
        if (!liberada[i])
        {
            liberada[i] = true;
            pente[i] = arma.tamanhoPente;
            reserva[i] = Mathf.Min(arma.municaoMaxima, reserva[i] + arma.municaoInicial);
            HUD.Aviso($"NOVA ARMA: {arma.nome.ToUpper()}  (tecla {i + 1})");
            Som2D(arma.somRecarga, 0.9f, 0f);
        }
        else AdicionarMunicao(arma, arma.tamanhoPente);
        Equipar(i);
    }

    // checkpoint por fase: guarda quais armas foram achadas e a munição de reserva
    public bool[] Liberadas() => (bool[])liberada.Clone();
    public int[] Reservas() => (int[])reserva.Clone();

    public void Restaurar(bool[] lib, int[] res)
    {
        if (lib == null || res == null || lib.Length != armas.Length || res.Length != armas.Length) return;
        for (int i = 0; i < armas.Length; i++)
        {
            liberada[i] = lib[i];
            reserva[i] = res[i];
            pente[i] = lib[i] ? armas[i].tamanhoPente : 0;
        }
        Equipar(0);
    }

    public void LiberarTodas()
    {
        foreach (var a in armas) if (!liberada[System.Array.IndexOf(armas, a)]) Liberar(a);
    }

    void Equipar(int i)
    {
        if (armas == null || i < 0 || i >= armas.Length || !liberada[i]) return;
        if (recarga != null) { StopCoroutine(recarga); recarga = null; }
        Recarregando = false;
        IndiceAtual = i;
        if (modelos != null)
            for (int k = 0; k < modelos.Length; k++)
                if (modelos[k] != null) modelos[k].SetActive(k == i);
    }

    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.JogoAtivo) return;
        if (vida != null && !vida.EstaVivo) return;
        if (!MouseJogo.Preso) return;
        if (Time.frameCount - GameManager.QuadroTravou <= 1) return; // o clique que travou o mouse não atira
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null || ArmaAtual == null) return;

        // Teclas 1 a 4: selecionam diretamente cada arma liberada
        if (kb.digit1Key.wasPressedThisFrame) Equipar(0);
        if (kb.digit2Key.wasPressedThisFrame) Equipar(1);
        if (kb.digit3Key.wasPressedThisFrame) Equipar(2);
        if (kb.digit4Key.wasPressedThisFrame) Equipar(3);

        // Rodinha do mouse: alterna entre as armas ja liberadas
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.1f)
        {
            int dir = scroll > 0f ? 1 : -1, i = IndiceAtual;
            for (int k = 0; k < armas.Length; k++)
            {
                i = (i + dir + armas.Length) % armas.Length;
                if (liberada[i]) { Equipar(i); break; }
            }
        }

        // Tecla R: recarrega a arma atual
        if (kb.rKey.wasPressedThisFrame) TentarRecarregar();

        // Botao esquerdo do mouse: dispara (ou recarrega se o pente estiver vazio)
        var a = ArmaAtual;
        bool gatilho = a.automatica ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
        if (gatilho && !Recarregando && Time.time >= proximoTiro)
        {
            if (pente[IndiceAtual] > 0) Atirar(mouse.rightButton.isPressed);
            else TentarRecarregar();
        }
    }

    void TentarRecarregar()
    {
        var a = ArmaAtual;
        int i = IndiceAtual;
        if (Recarregando || pente[i] >= a.tamanhoPente) return;
        if (!a.municaoInfinita && reserva[i] <= 0)
        {
            HUD.Aviso("Sem munição de " + a.nome);
            if (Time.time >= proximoTiro && SomManager.Instance != null) SomManager.Instance.Tocar2D(SomManager.Instance.vazio, 0.6f);
            proximoTiro = Time.time + 0.3f;
            return;
        }
        recarga = StartCoroutine(Recarregar(i));
    }

    IEnumerator Recarregar(int i)
    {
        Recarregando = true;
        Som2D(armas[i].somRecarga, 0.7f, 0f);
        yield return new WaitForSeconds(armas[i].tempoRecarga);
        int falta = armas[i].tamanhoPente - pente[i];
        int qtd = armas[i].municaoInfinita ? falta : Mathf.Min(falta, reserva[i]);
        pente[i] += qtd;
        if (!armas[i].municaoInfinita) reserva[i] -= qtd;
        Recarregando = false;
        recarga = null;
    }

    void Atirar(bool mirando)
    {
        var a = ArmaAtual;
        proximoTiro = Time.time + 1f / a.tirosPorSegundo;
        pente[IndiceAtual]--;
        Som2D(a.somTiro, 0.9f, 0.05f);
        // tiro faz barulho: zumbis parados por perto (escola, clínica) vêm atrás
        ZombieAI.Barulho(transform.position, 14f);
        if (cameraJogador != null)
        {
            cameraJogador.AdicionarRecuo(a.recuo);
            cameraJogador.Tremer(0.05f + a.recuo * 0.03f);
        }
        if (anim == null) anim = GetComponent<PlayerAnimator>();
        if (anim != null) anim.Coice(2f + a.recuo * 1.2f);
        HUD.Disparou(a.recuo);
        flash.enabled = true;
        fimFlash = Time.time + 0.04f;

        Vector3 origemVisual = flash.transform.position;
        float spread = mirando ? (a.spreadMirando >= 0f ? a.spreadMirando : a.spread * 0.5f) : a.spread;
        bool acertou = false, matou = false, somParede = false, naCabeca = false;
        Vector3 posAcerto = Vector3.zero;
        for (int p = 0; p < a.projeteis; p++)
        {
            Vector3 dir = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * Vector3.forward;
            dir = cam.transform.rotation * dir;
            Vector3 fim = cam.transform.position + dir * a.alcance;

            // acertos em ordem de distância: atravessa zumbis (sniper) até bater em parede
            // o NonAlloc devolve os acertos em ordem qualquer: se o buffer encher, o zumbi da frente pode ficar de fora
            int n = Physics.RaycastNonAlloc(cam.transform.position, dir, hits, a.alcance, ~0, QueryTriggerInteraction.Ignore);
            var lista = hits;
            if (n == hits.Length)
            {
                lista = Physics.RaycastAll(cam.transform.position, dir, a.alcance, ~0, QueryTriggerInteraction.Ignore);
                n = lista.Length;
            }
            System.Array.Sort(lista, 0, n, PorDistancia);
            int restantes = a.perfura + 1;
            for (int i = 0; i < n && restantes > 0; i++)
            {
                var h = lista[i];
                if (h.transform.IsChildOf(transform)) continue;
                fim = h.point;
                var alvo = h.collider.GetComponentInParent<IDamageable>();
                if (alvo == null)
                {
                    Efeitos.Poeira(h.point, h.normal);
                    if (!somParede && SomManager.Instance != null) SomManager.Instance.Tocar(SomManager.Instance.impactoParede, h.point, 0.5f, 1f, 0.15f, 25f);
                    somParede = true;
                    break;
                }
                if (alvo.EstaVivo)
                {
                    var zumbi = h.collider.GetComponentInParent<ZombieAI>();
                    // tiro na cabeça: dano bem maior
                    bool cabeca = zumbi != null && h.point.y > zumbi.transform.position.y + zumbi.alturaCabeca;
                    alvo.ReceberDano(a.dano * (cabeca ? multCabeca : 1f));
                    acertou = true;
                    posAcerto = h.point;
                    if (cabeca) naCabeca = true;
                    if (!alvo.EstaVivo)
                    {
                        matou = true;
                        if (cabeca && GameManager.Instance != null) GameManager.Instance.Headshots++;
                    }
                    Efeitos.Sangue(h.point, h.normal, cabeca ? 14 : (a.projeteis > 1 ? 4 : 9));
                    if (zumbi != null) zumbi.Atordoar(dir, a.impacto);
                }
                restantes--;
            }
            CriarTracer(origemVisual, fim);
        }

        if (acertou)
        {
            HUD.Acerto(matou, naCabeca);
            if (SomManager.Instance != null)
            {
                SomManager.Instance.Tocar2D(SomManager.Instance.acerto, matou ? 0.6f : 0.35f, 0.05f);
                SomManager.Instance.Tocar(SomManager.Instance.impactoCarne, posAcerto, 0.9f, 1f, 0.15f, 30f);
            }
        }
    }

    static readonly IComparer<RaycastHit> PorDistancia = Comparer<RaycastHit>.Create((x, y) => x.distance.CompareTo(y.distance));

    void Som2D(AudioClip clip, float volume, float variacao)
    {
        if (clip == null) return;
        if (SomManager.Instance != null) SomManager.Instance.Tocar2D(clip, volume, variacao);
        else audioSrc.PlayOneShot(clip, volume);
    }

    // tracers reaproveitados (sem criar/destruir objeto a cada tiro)
    void CriarTracer(Vector3 de, Vector3 ate)
    {
        if (tracers == null)
        {
            tracers = new LineRenderer[16];
            fimTracer = new float[16];
            for (int i = 0; i < tracers.Length; i++)
            {
                var go = new GameObject("Tracer");
                var l = go.AddComponent<LineRenderer>();
                l.positionCount = 2;
                l.startWidth = 0.03f;
                l.endWidth = 0.01f;
                l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (materialTracer != null) l.sharedMaterial = materialTracer;
                l.enabled = false;
                tracers[i] = l;
            }
        }
        var lr = tracers[proximoTracer];
        fimTracer[proximoTracer] = Time.time + 0.05f;
        proximoTracer = (proximoTracer + 1) % tracers.Length;
        lr.SetPosition(0, de);
        lr.SetPosition(1, ate);
        lr.enabled = true;
    }

    void LateUpdate()
    {
        // flash e rastro saem do cano da arma (o PlayerAnimator já apontou a arma nesse quadro)
        if (pontoArma != null && ArmaAtual != null)
            flash.transform.position = pontoArma.position + pontoArma.forward * (ArmaAtual.comprimentoModelo * 0.6f);
        if (flash.enabled && Time.time > fimFlash) flash.enabled = false;
        if (tracers == null) return;
        for (int i = 0; i < tracers.Length; i++)
            if (tracers[i].enabled && Time.time > fimTracer[i]) tracers[i].enabled = false;
    }

    // retorna false se já está no máximo
    public bool AdicionarMunicao(WeaponData arma, int qtd)
    {
        for (int i = 0; i < armas.Length; i++)
        {
            if (armas[i] != arma) continue;
            if (arma.municaoInfinita || reserva[i] >= arma.municaoMaxima) return false;
            reserva[i] = Mathf.Min(arma.municaoMaxima, reserva[i] + qtd);
            return true;
        }
        return false;
    }
}
