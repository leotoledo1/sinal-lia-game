using UnityEngine;
using UnityEngine.InputSystem;

// Movimento 3ª pessoa: WASD relativo à câmera, Shift corre, Espaço pula, botão direito mira
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Transform cameraTransform;
    public float velocidadeAndar = 3.5f;
    public float velocidadeCorrer = 6.5f;
    public float velocidadeMirando = 2.5f;
    public float velocidadeGiro = 18f;
    public float gravidade = -20f;
    public float alturaPulo = 1f;
    [Tooltip("Força com que o Marcos empurra zumbis ao encostar (pra não ficar preso na horda)")]
    public float forcaEmpurrao = 7f;

    [Tooltip("Ligado: o corpo sempre olha pra onde a câmera olha, até parado (estilo jogo de tiro)")]
    public bool corpoSegueCamera = true;

    public bool Mirando { get; private set; }
    public bool Atirando { get; private set; }
    // 0 = parado, 1 = correndo (usado pelo Animator)
    public float VelocidadeNormalizada { get; private set; }
    public Vector3 VelocidadeMundo => velHorizontal;

    [Tooltip("m/s² - quanto maior, mais 'seco' o movimento")]
    public float aceleracao = 28f;

    CharacterController cc;
    PlayerHealth vida;
    float velY;
    Vector3 velHorizontal;
    float distPasso;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        vida = GetComponent<PlayerHealth>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        bool ativo = (GameManager.Instance == null || GameManager.Instance.JogoAtivo) && (vida == null || vida.EstaVivo);
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        Vector2 input = Vector2.zero;
        bool correndo = false;
        bool atirando = false;
        Mirando = false;

        if (ativo && kb != null)
        {
            // Movimento horizontal (WASD)
            if (kb.wKey.isPressed) input.y += 1;
            if (kb.sKey.isPressed) input.y -= 1;
            if (kb.dKey.isPressed) input.x += 1;
            if (kb.aKey.isPressed) input.x -= 1;
            input = Vector2.ClampMagnitude(input, 1f);

            // Mouse direito mira, mouse esquerdo atira, Shift esquerdo corre
            Mirando = mouse != null && mouse.rightButton.isPressed;
            atirando = mouse != null && mouse.leftButton.isPressed;
            correndo = kb.leftShiftKey.isPressed && !Mirando && input.y > 0.1f;

            // Espaco pula quando esta no chao
            if (kb.spaceKey.wasPressedThisFrame && cc.isGrounded)
                velY = Mathf.Sqrt(2f * alturaPulo * -gravidade);
        }

        Vector3 frente = cameraTransform != null ? cameraTransform.forward : transform.forward;
        frente.y = 0f;
        frente.Normalize();
        Vector3 direita = new Vector3(frente.z, 0f, -frente.x);
        Vector3 dir = frente * input.y + direita * input.x;

        float vel = Mirando ? velocidadeMirando : (correndo ? velocidadeCorrer : velocidadeAndar);
        if (input.y < -0.1f) vel *= 0.75f; // de costas é mais devagar
        Atirando = atirando;

        // o corpo sempre olha pra frente da câmera (a câmera fica sempre atrás do Marcos);
        // andando de lado/de costas as pernas viram pro movimento no PlayerAnimator
        bool movendo = input.sqrMagnitude > 0.01f;
        Vector3 olhar = corpoSegueCamera || Mirando || atirando ? frente : (movendo ? dir : Vector3.zero);
        // suavização exponencial: mesmo giro com 30 ou 144 FPS (sem tranco em queda de quadros)
        if (ativo && olhar.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(olhar), 1f - Mathf.Exp(-velocidadeGiro * Time.deltaTime));

        if (cc.isGrounded && velY < 0f) velY = -2f;
        velY += gravidade * Time.deltaTime;

        // acelera/freia rápido mas não instantâneo (movimento mais fluido, sem "robô")
        velHorizontal = Vector3.MoveTowards(velHorizontal, dir * vel, aceleracao * Time.deltaTime);
        cc.Move((velHorizontal + Vector3.up * velY) * Time.deltaTime);
        VelocidadeNormalizada = velHorizontal.magnitude / velocidadeCorrer;

        // passos: um som a cada "passada" no chão
        if (cc.isGrounded && SomManager.Instance != null)
        {
            distPasso += velHorizontal.magnitude * Time.deltaTime;
            float passada = correndo ? 1.5f : 1.1f;
            if (distPasso >= passada)
            {
                distPasso = 0f;
                SomManager.Instance.Tocar2D(SomManager.Sortear(SomManager.Instance.passos), correndo ? 0.35f : 0.25f, 0.1f);
            }
        }
    }

    // encostou num zumbi: empurra ele pro lado em vez de parar
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var zumbi = hit.collider.GetComponentInParent<ZombieAI>();
        if (zumbi == null) return;
        Vector3 dir = hit.collider.transform.position - transform.position;
        dir.y = 0f;
        zumbi.Empurrar(dir.normalized * forcaEmpurrao * Time.deltaTime);
    }
}
