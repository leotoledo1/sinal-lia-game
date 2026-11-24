using UnityEngine;

// Liga o PlayerController ao Animator e ajusta o corpo por código:
// - o corpo olha pra câmera; as PERNAS giram pra direção do movimento (andar de lado sem animação de lado)
// - andando de costas, a corrida toca ao contrário
// - mirando: braços apontam a arma pra mira e o tronco inclina junto com a câmera; tiro dá coice
// Todas as torções partem da pose da animação NAQUELE quadro (guardada antes de mexer):
// se o Animator não atualizar um quadro (pausa, câmera fora), nada acumula e o corpo não "gira sozinho".
public class PlayerAnimator : MonoBehaviour
{
    public Animator animator;
    [Tooltip("Quanto as pernas podem girar em relação ao tronco")]
    public float giroMaximoPernas = 45f;
    [Tooltip("Quanto o tronco acompanha a câmera pra cima/baixo ao mirar")]
    public float inclinacaoMira = 0.6f;
    [Tooltip("Sem mirar: quanto o braço direito vai pra posição de 'pronto' (0 = braço caído da animação)")]
    [Range(0f, 1f)] public float pesoPronto = 0.6f;
    [Header("Esqueleto não-humanoide (Quaternius)")]
    public string nomeQuadril = "Hips";
    public string nomeColuna = "Abdomen";
    public string nomePeito = "Torso";

    public float GiroPernas => giroPernas;
    public float PesoMira => pesoMira;

    PlayerController controller;
    PlayerHealth vida;
    PlayerWeapons armas;
    ThirdPersonCamera cam;
    Transform quadril, coluna, peito;
    Transform bracoD, antebracoD, maoD, bracoE, antebracoE, maoE, pontoArma;
    float giroPernas, pesoMira, coice, pesoRecarga, pesoCena, sentido = 1f;
    int camadaMira = -1;
    bool morto, temVelAnim, temRecarga, deCostas;

    // pose da animação x pose depois das torções (pra detectar quadro sem animação)
    Transform[] ossos;
    Transform[,] dedos;
    Transform pontaDedo;
    [Tooltip("Quanto os dedos da mão direita fecham em volta da arma (graus por junta: base, meio, ponta)")]
    public Vector3 fecharDedos = new Vector3(60f, 75f, 55f);
    Quaternion[] poseAnim, poseAplicada;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        vida = GetComponent<PlayerHealth>();
        armas = GetComponent<PlayerWeapons>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            quadril = animator.GetBoneTransform(HumanBodyBones.Hips);
            coluna = animator.GetBoneTransform(HumanBodyBones.Spine);
            peito = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (peito == null) peito = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            bracoD = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            antebracoD = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            maoD = animator.GetBoneTransform(HumanBodyBones.RightHand);
            bracoE = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            antebracoE = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            maoE = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        }
        else if (animator != null)
        {
            foreach (var t in animator.GetComponentsInChildren<Transform>())
            {
                if (t.name == nomeQuadril) quadril = t;
                else if (t.name == nomeColuna) coluna = t;
                else if (t.name == nomePeito) peito = t;
                else if (t.name == "UpperArm.R") bracoD = t;
                else if (t.name == "LowerArm.R") antebracoD = t;
                else if (t.name == "Middle1.R") maoD = t;
                else if (t.name == "UpperArm.L") bracoE = t;
                else if (t.name == "LowerArm.L") antebracoE = t;
                else if (t.name == "Middle1.L") maoE = t;
            }
        }
        if (animator != null)
        {
            // sempre anima, mesmo com a câmera colada/fora (senão as torções somam quadro a quadro)
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (animator.runtimeAnimatorController != null)
            {
                camadaMira = animator.GetLayerIndex("Mira");
                foreach (var p in animator.parameters)
                {
                    if (p.name == "VelAnim") temVelAnim = true;
                    if (p.name == "Recarregando") temRecarga = true;
                }
            }
        }
        // dedos da mão direita (Quaternius): fecham em volta do cabo da arma (a animação deixa a mão aberta)
        if (animator != null)
        {
            var todos = animator.GetComponentsInChildren<Transform>();
            Transform Achar(string n) => System.Array.Find(todos, t => t.name == n);
            string[] nomes = { "Index", "Middle", "Pinky" };
            dedos = new Transform[nomes.Length, 3];
            for (int f = 0; f < nomes.Length; f++)
                for (int j = 0; j < 3; j++) dedos[f, j] = Achar(nomes[f] + (j + 1) + ".R");
            pontaDedo = Achar("Middle3.R_end") ?? Achar("Middle3.R");
        }
        var lista = new System.Collections.Generic.List<Transform> { quadril, coluna, peito, bracoD, antebracoD, bracoE, antebracoE };
        if (dedos != null) foreach (var d in dedos) lista.Add(d);
        ossos = lista.ToArray();
        poseAnim = new Quaternion[ossos.Length];
        poseAplicada = new Quaternion[ossos.Length];
        for (int i = 0; i < ossos.Length; i++)
            if (ossos[i] != null) poseAnim[i] = poseAplicada[i] = ossos[i].localRotation;
    }

    void Start()
    {
        if (Camera.main != null) cam = Camera.main.GetComponent<ThirdPersonCamera>();
        if (armas != null) pontoArma = armas.pontoArma;
    }

    public void Coice(float graus) => coice = Mathf.Min(coice + graus, 12f);

    void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null || controller == null) return;
        float dt = Time.deltaTime;

        // direção do movimento em relação ao corpo
        Vector3 local = transform.InverseTransformDirection(controller.VelocidadeMundo);
        local.y = 0f;
        float alvo = 0f;
        if (local.magnitude > 0.3f)
        {
            float ang = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg; // 0 = frente, ±180 = costas
            // de costas: a corrida toca ao contrário. Folga grande (entra >125°, sai <55°):
            // andar de lado ou na diagonal nunca fica perto do ponto de troca (era o que dava "giro" no quadril)
            if (deCostas) { if (Mathf.Abs(ang) < 55f) deCostas = false; }
            else if (Mathf.Abs(ang) > 125f) deCostas = true;
            sentido = deCostas ? -1f : 1f;
            float relativo = deCostas ? ang - Mathf.Sign(ang) * 180f : ang;
            alvo = Mathf.Clamp(relativo, -giroMaximoPernas, giroMaximoPernas);
        }
        // segue o alvo de forma suave e com velocidade máxima (sem chicotada)
        float suave = Mathf.LerpAngle(giroPernas, alvo, 1f - Mathf.Exp(-12f * dt));
        giroPernas = Mathf.MoveTowardsAngle(giroPernas, suave, 360f * dt);

        animator.SetFloat("Speed", controller.VelocidadeNormalizada, 0.1f, dt);
        if (temVelAnim) animator.SetFloat("VelAnim", sentido);

        bool recarregando = armas != null && armas.Recarregando;
        bool mirando = controller.Mirando || controller.Atirando || recarregando;
        pesoMira = Mathf.MoveTowards(pesoMira, mirando ? 1f : 0f, 8f * dt);
        // cenas (reencontro, final): arma abaixada, braço solto - ele não aponta a arma pra filha
        var gm = GameManager.Instance;
        pesoCena = Mathf.MoveTowards(pesoCena, gm != null && gm.EmCena ? 1f : 0f, 3f * Time.unscaledDeltaTime);
        if (camadaMira >= 0) animator.SetLayerWeight(camadaMira, pesoMira);
        if (temRecarga) animator.SetBool("Recarregando", recarregando);
        // sem animação de recarga: abaixa o tronco/arma enquanto recarrega
        pesoRecarga = Mathf.MoveTowards(pesoRecarga, recarregando && !temRecarga ? 1f : 0f, 5f * dt);
        coice = Mathf.MoveTowards(coice, 0f, 60f * dt);

        if (!morto && vida != null && !vida.EstaVivo)
        {
            morto = true;
            animator.SetTrigger("Morrer");
        }
    }

    // depois da animação: torce o corpo (pernas pro movimento, tronco pra mira)
    void LateUpdate()
    {
        if (morto || quadril == null || coluna == null) return;

        // o Animator não escreveu a pose neste quadro? volta pra última pose da animação antes de torcer
        for (int i = 0; i < ossos.Length; i++)
        {
            var o = ossos[i];
            if (o == null) continue;
            if (Quaternion.Angle(o.localRotation, poseAplicada[i]) < 0.001f) o.localRotation = poseAnim[i];
            poseAnim[i] = o.localRotation;
        }

        Vector3 cima = Vector3.up;
        if (Mathf.Abs(giroPernas) > 0.1f)
        {
            quadril.rotation = Quaternion.AngleAxis(giroPernas, cima) * quadril.rotation;
            if (peito != null && peito != coluna)
            {
                coluna.rotation = Quaternion.AngleAxis(-giroPernas * 0.5f, cima) * coluna.rotation;
                peito.rotation = Quaternion.AngleAxis(-giroPernas * 0.5f, cima) * peito.rotation;
            }
            else coluna.rotation = Quaternion.AngleAxis(-giroPernas, cima) * coluna.rotation;
        }
        var alvoInclina = peito != null ? peito : coluna;
        float pitch = cam != null ? Mathf.Clamp(cam.Pitch, -45f, 45f) : 0f;
        float inclina = Mathf.Clamp(pitch * inclinacaoMira * pesoMira, -28f, 28f) - coice + 25f * pesoRecarga;
        if (Mathf.Abs(inclina) > 0.05f) alvoInclina.rotation = Quaternion.AngleAxis(inclina, transform.right) * alvoInclina.rotation;
        MirarBracos();

        for (int i = 0; i < ossos.Length; i++)
            if (ossos[i] != null) poseAplicada[i] = ossos[i].localRotation;
    }

    // mirando/atirando: braço direito estica na direção da mira e o esquerdo apoia embaixo do cano.
    // A arma sempre aponta pra mira (ou pra frente, um pouco pra baixo, "pronta", quando não está mirando).
    void MirarBracos()
    {
        if (pontoArma == null) return;
        Vector3 mira = Camera.main != null ? Camera.main.transform.forward : transform.forward;
        // "pronto" (sem mirar): braço direito um pouco à frente e pro lado, cano pra frente e pra baixo.
        // Assim a arma aparece de lado pra câmera (com o braço caído ela ficava "de ponta" e sumia na mão)
        Vector3 pronta = Quaternion.AngleAxis(28f, Vector3.up) * Vector3.Slerp(transform.forward, Vector3.down, 0.42f);
        Vector3 dir = Vector3.Slerp(pronta, mira, pesoMira).normalized;
        float pBraco = Mathf.Lerp(pesoPronto, 1f, pesoMira) * (1f - pesoRecarga) * (1f - pesoCena);
        float pApoio = pesoMira * (1f - pesoRecarga) * (1f - pesoCena);
        if (pBraco > 0.01f && bracoD != null && antebracoD != null && maoD != null)
        {
            Apontar(bracoD, antebracoD, dir, pBraco);
            Apontar(antebracoD, maoD, dir, pBraco);
            if (pApoio > 0.01f && bracoE != null && antebracoE != null && maoE != null)
            {
                Vector3 apoio = maoD.position + mira * 0.2f - Vector3.up * 0.06f;
                Apontar(bracoE, antebracoE, (apoio - bracoE.position).normalized, pApoio);
                Apontar(antebracoE, maoE, (apoio - antebracoE.position).normalized, pApoio);
            }
        }
        float pArma = pesoMira * (1f - pesoRecarga);
        var rotArma = Quaternion.Slerp(Quaternion.LookRotation(pronta, transform.forward + Vector3.up), Quaternion.LookRotation(mira, Vector3.up), pArma);
        if (pesoCena > 0.001f) rotArma = Quaternion.Slerp(rotArma, Quaternion.LookRotation(Vector3.down * 0.9f + transform.forward * 0.35f, transform.forward), pesoCena);
        pontoArma.rotation = rotArma;
        FecharMao();
    }

    // fecha os dedos (indicador, médio, mínimo) girando em volta da linha dos nós dos dedos,
    // pro lado que leva a ponta do dedo pra baixo do cano (onde fica o cabo)
    void FecharMao()
    {
        if (dedos == null || dedos[1, 0] == null || pontaDedo == null) return;
        // eixo de dobrar = perpendicular ao dedo e ao "cima" da arma
        // (os 3 dedos deste modelo nascem quase no mesmo ponto: não dá pra usar a linha dos nós)
        Vector3 pivo = dedos[1, 0].position;
        Vector3 ponta = pontaDedo.position - pivo;
        Vector3 cimaArma = pontoArma.up;
        Vector3 eixo = Vector3.Cross(ponta, cimaArma);
        if (eixo.sqrMagnitude < 1e-10f) eixo = pontoArma.right;
        eixo.Normalize();
        float sinal = Vector3.Dot(Quaternion.AngleAxis(90f, eixo) * ponta, cimaArma) < Vector3.Dot(Quaternion.AngleAxis(-90f, eixo) * ponta, cimaArma) ? 1f : -1f;
        for (int f = 0; f < dedos.GetLength(0); f++)
            for (int j = 0; j < 3; j++)
            {
                var d = dedos[f, j];
                if (d == null) continue;
                d.rotation = Quaternion.AngleAxis(sinal * fecharDedos[j], eixo) * d.rotation;
            }
    }

    // gira o osso pra que o filho fique na direção pedida (peso 0..1)
    static void Apontar(Transform osso, Transform filho, Vector3 dir, float peso)
    {
        Vector3 atual = filho.position - osso.position;
        if (atual.sqrMagnitude < 1e-8f || dir.sqrMagnitude < 1e-8f) return;
        osso.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(atual, dir), peso) * osso.rotation;
    }
}
