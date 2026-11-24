using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// Câmera por cima do ombro: segue o Marcos sem atraso, desvia de paredes sem dar "pulo" e dá zoom ao mirar.
// O Marcos fica à esquerda da tela (a mira no centro fica livre).
public class ThirdPersonCamera : MonoBehaviour
{
    public Transform alvo;
    public PlayerController jogador;
    [Tooltip("Altura (m, a partir do pé) do ponto que a câmera orbita")]
    public float alturaOmbro = 1.5f;
    [Tooltip("Quanto a câmera fica pra direita do Marcos")]
    public float ladoOmbro = 0.7f;
    public float distancia = 4.3f;
    public float distanciaMira = 2.4f;
    [Tooltip("Mirando: câmera mais alta e mais pro lado (a cabeça do Marcos não tampa a mira)")]
    public float alturaMira = 1.8f;
    public float ladoMira = 0.95f;
    public float sensibilidade = 0.1f;
    [Tooltip("Ignora 'pulos' absurdos do mouse maiores que isso (pixels por quadro). Valor baixo trava giros rápidos")]
    public float maxMovimentoPorQuadro = 1000f;
    public float pitchMin = -40f;
    public float pitchMax = 65f;
    public float fovNormal = 62f;
    public float fovMira = 46f;
    [Tooltip("Câmera mais perto que isso do Marcos: ele some (só a sombra fica) pra não tampar a tela")]
    public float distanciaEsconder = 0.9f;

    float yaw, pitch = 10f, distAtual, ladoAtual, recuo, trauma, pesoMiraCam;
    bool estavaTravado, escondido;
    int quadrosIgnorar = 2;
    Camera cam;
    Renderer[] rendsJogador;
    readonly RaycastHit[] hits = new RaycastHit[16];

    void Awake()
    {
        cam = GetComponent<Camera>();
        distAtual = distancia;
        ladoAtual = ladoOmbro;
        if (alvo != null) yaw = alvo.eulerAngles.y;
        if (cam != null) cam.nearClipPlane = 0.08f;
    }

    public void AdicionarRecuo(float graus) => recuo += graus;
    // positivo = olhando pra baixo (usado pra inclinar o tronco do Marcos junto com a mira)
    public float Pitch => pitch;
    public float Yaw => yaw;

    // tremida (tiro, dano): soma "trauma" que some sozinho
    public void Tremer(float quanto) => trauma = Mathf.Min(1f, trauma + quanto);

    // a câmera olha pra essa direção (usado ao voltar de checkpoint/teleporte)
    public void OlharPara(float novoYaw, float novoPitch = 8f)
    {
        yaw = novoYaw;
        pitch = novoPitch;
    }

    // cenas de cinema (reencontro, final)
    bool emCena, cenaFixa;
    Vector3 cenaFoco, cenaPos;
    float cenaYaw, cenaPitch, cenaDist, cenaGiro, cenaDistAtual, cenaAproximar, cenaTempo;
    public bool EmCena => emCena;

    // plano fixo: câmera em "pos" olhando pro "alvo", chegando um pouquinho mais perto com o tempo (m/s)
    public void IniciarCenaFixa(Vector3 pos, Vector3 alvoCena, float aproximar, float fov = 50f)
    {
        IniciarCena(alvoCena, 0f, 0f, Vector3.Distance(pos, alvoCena), 0f);
        cenaFixa = true;
        cenaPos = pos;
        cenaAproximar = aproximar;
        cenaTempo = 0f;
        cenaFov = fov;
    }

    float cenaFov = 50f;

    // corta pra um enquadramento fixo olhando pro "foco", girando devagar (graus/s)
    public void IniciarCena(Vector3 foco, float yawCena, float pitchCena, float distancia, float giro)
    {
        emCena = true;
        cenaFixa = false;
        cenaFoco = foco;
        cenaYaw = yawCena;
        cenaPitch = pitchCena;
        cenaDist = distancia;
        cenaGiro = giro;
        cenaDistAtual = distancia;
        if (escondido) MostrarJogador(true);
    }

    // volta pra trás do Marcos, olhando pra onde ele olha
    public void FimCena()
    {
        emCena = false;
        if (alvo != null) yaw = alvo.eulerAngles.y;
        pitch = 8f;
        distAtual = distancia;
        recuo = trauma = 0f;
    }

    void MostrarJogador(bool mostrar)
    {
        escondido = !mostrar;
        rendsJogador = alvo.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rendsJogador)
            if (r != null && !(r is ParticleSystemRenderer) && !(r is LineRenderer))
                r.shadowCastingMode = mostrar ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
    }

    void AtualizarCena(float dt)
    {
        if (cenaFixa)
        {
            cenaTempo += dt;
            Vector3 dir = cenaPos - cenaFoco;
            float quer = Mathf.Max(2f, dir.magnitude - cenaAproximar * cenaTempo);
            dir.Normalize();
            float livreFixa = Mathf.Max(1.2f, Livre(cenaFoco, dir, quer, 0.3f) - 0.15f); // sem atravessar parede
            Vector3 p = cenaFoco + dir * Mathf.Min(quer, livreFixa);
            transform.SetPositionAndRotation(p, Quaternion.LookRotation(cenaFoco - p));
            if (cam != null) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, cenaFov, 1f - Mathf.Exp(-4f * dt));
            return;
        }
        cenaYaw += cenaGiro * dt;
        Quaternion rot = Quaternion.Euler(cenaPitch, cenaYaw, 0f);
        Vector3 tras = -(rot * Vector3.forward);
        // parede atrás: a câmera chega mais perto (sem atravessar)
        float livre = Mathf.Max(0.8f, Livre(cenaFoco, tras, cenaDist, 0.25f) - 0.1f);
        cenaDistAtual = livre < cenaDistAtual ? livre : Mathf.Lerp(cenaDistAtual, livre, 1f - Mathf.Exp(-3f * dt));
        transform.SetPositionAndRotation(cenaFoco + tras * cenaDistAtual, rot);
        if (cam != null) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 50f, 1f - Mathf.Exp(-4f * dt));
    }

    // acertos que não bloqueiam a câmera: o próprio Marcos, zumbis, Lia e gatilhos
    bool Ignorar(RaycastHit h)
    {
        if (h.collider == null || h.collider.isTrigger) return true;
        if (h.transform.IsChildOf(alvo)) return true;
        if (h.collider.GetComponentInParent<ZombieAI>() != null) return true;
        if (h.collider.GetComponentInParent<Rigidbody>() != null) return true; // lixo solto, latas
        return false;
    }

    // distância livre de um ponto numa direção (com raio)
    float Livre(Vector3 de, Vector3 dir, float max, float raio)
    {
        float livre = max;
        int n = Physics.SphereCastNonAlloc(de, raio, dir, hits, max, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            if (Ignorar(hits[i])) continue;
            // começou dentro do objeto (distância 0): considera bloqueado já no início
            livre = Mathf.Min(livre, hits[i].distance <= 0f ? 0f : hits[i].distance);
        }
        return livre;
    }

    void LateUpdate()
    {
        if (alvo == null) return;
        float dt = Time.unscaledDeltaTime;
        if (emCena)
        {
            AtualizarCena(dt);
            return;
        }
        var gm = GameManager.Instance;
        bool ativo = gm == null || (gm.JogoAtivo && !gm.Carregando);
        var mouse = Mouse.current;

        // menu principal: a câmera gira devagar em volta do Marcos (fundo do menu)
        if (gm != null && gm.NoMenu)
        {
            yaw += 6f * dt;
            pitch = Mathf.Lerp(pitch, 12f, 2f * dt);
        }

        // com zoom (sniper) o mouse fica proporcionalmente mais lento
        float sens = sensibilidade * Opcoes.Sensibilidade * (cam != null ? cam.fieldOfView / fovNormal : 1f);
        // só lê o mouse com a janela do jogo em foco e o cursor preso (MouseJogo: normal ou Área de Trabalho Remota)
        bool travado = MouseJogo.Preso && Application.isFocused;
        if (travado && !estavaTravado) { quadrosIgnorar = 2; MouseJogo.Reiniciar(); } // acabou de travar: o 1º movimento pode vir com um "pulo"
        estavaTravado = travado;
        if (ativo && mouse != null && travado)
        {
            Vector2 d = MouseJogo.Movimento(mouse);
            if (quadrosIgnorar > 0) { quadrosIgnorar--; d = Vector2.zero; }
            d = Vector2.ClampMagnitude(d, maxMovimentoPorQuadro);
            yaw += d.x * sens;
            pitch -= d.y * sens;
        }
        yaw = Mathf.Repeat(yaw, 360f);

        // recuo da arma sobe a mira e volta aos poucos (pausado não mexe)
        if (Time.timeScale > 0f)
        {
            float r = recuo * (1f - Mathf.Exp(-12f * Time.deltaTime));
            pitch -= r;
            recuo -= r;
        }
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        bool mirando = jogador != null && jogador.Mirando;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 direita = rot * Vector3.right;
        Vector3 tras = -(rot * Vector3.forward);

        // ponto central (dentro do Marcos) -> ombro: se tiver parede do lado, o ombro encolhe
        pesoMiraCam = Mathf.MoveTowards(pesoMiraCam, mirando ? 1f : 0f, 6f * dt);
        float k = Mathf.SmoothStep(0f, 1f, pesoMiraCam);
        Vector3 centro = alvo.position + Vector3.up * Mathf.Lerp(alturaOmbro, alturaMira, k);
        float ladoQuer = Mathf.Lerp(ladoOmbro, ladoMira, k);
        float ladoAlvo = Mathf.Max(0f, Livre(centro, direita, ladoQuer + 0.2f, 0.18f) - 0.2f);
        ladoAtual = ladoAlvo < ladoAtual ? ladoAlvo : Mathf.Lerp(ladoAtual, ladoAlvo, 1f - Mathf.Exp(-6f * dt));
        Vector3 pivo = centro + direita * ladoAtual;

        // do ombro pra trás: parede no caminho puxa a câmera pra frente na hora; afastar é suave
        float distDesejada = Mathf.Lerp(distancia, distanciaMira, k);
        float distAlvo = Mathf.Max(0.25f, Livre(pivo, tras, distDesejada, 0.22f) - 0.08f);
        distAtual = distAlvo < distAtual ? distAlvo : Mathf.Lerp(distAtual, distAlvo, 1f - Mathf.Exp((mirando ? -14f : -5f) * dt));

        // tremida só na rotação (não atravessa parede); forte = trauma²
        trauma = Mathf.MoveTowards(trauma, 0f, 1.6f * Time.deltaTime);
        float forca = trauma * trauma;
        float tt = Time.time * 25f;
        Quaternion tremida = Quaternion.Euler((Mathf.PerlinNoise(tt, 0f) - 0.5f) * 5f * forca, (Mathf.PerlinNoise(0f, tt) - 0.5f) * 5f * forca, (Mathf.PerlinNoise(tt, tt) - 0.5f) * 3f * forca);

        transform.SetPositionAndRotation(pivo + tras * distAtual, rot * tremida);

        var arma = PlayerWeapons.Instance != null ? PlayerWeapons.Instance.ArmaAtual : null;
        float fovAlvo = mirando ? (arma != null && arma.fovMira > 0f ? arma.fovMira : fovMira) : fovNormal;
        if (cam != null) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fovAlvo, 1f - Mathf.Exp(-12f * dt));

        // câmera colada no Marcos (encostado na parede): esconde ele pra não tampar a tela
        bool esconder = distAtual < distanciaEsconder && (gm == null || !gm.NoMenu);
        if (esconder != escondido) MostrarJogador(!esconder); // inclui a arma da mão
    }
}
