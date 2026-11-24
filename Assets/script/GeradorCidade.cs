using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GeradorCidade : MonoBehaviour
{
    [Header("Configurações da Matriz (Grid)")]
    public int blocosX = 3;
    public int blocosZ = 3;
    public float tamanhoTile = 10f;
    public int tamanhoQuarteirao = 2;

    [Header("Prefabs de Rua (Variações)")]
    public GameObject[] prefabsCruzamentos;
    public GameObject[] prefabsRuasRetas;
    public GameObject[] prefabsCurvas;
    public GameObject[] prefabsJuncoesT;

    [Header("Prefabs de Casas/Prédios")]
    public GameObject[] prefabsPredios;

    [Header("Prefabs de Detalhes (Props com Física)")]
    [Tooltip("Coloque aqui cones, lixeiras, caixas, barricadas, etc.")]
    public GameObject[] prefabsProps;
    [Tooltip("Chance de uma rua ter entulho espalhado (0 a 1)")]
    [Range(0f, 1f)] public float chanceGerarProp = 0.5f;
    [Tooltip("Quantidade máxima de props bagunçados em um único pedaço de rua")]
    [Range(1, 10)] public int maxPropsPorRua = 4;
    [Tooltip("Altura de onde o lixo vai cair (Deixe alto para a física atuar)")]
    public float alturaQueda = 3f;

    [Header("Configurações de Geração")]
    [Range(0f, 1f)] public float densidadePredios = 0.9f;
    public float correcaoRotacaoX = 0f;
    [Tooltip("Mesma seed = mesma cidade. 0 = aleatória")]
    public int seed = 1234;
    [Tooltip("Desligado = usa a cidade já salva na cena (necessário para o NavMesh)")]
    public bool gerarAoIniciar = true;

    private const string NOME_CONTAINER = "CidadeGerada";
    private Transform containerCidade;

    void Start()
    {
        if (gerarAoIniciar) GerarCidade();
    }

    [ContextMenu("Regerar Cidade")]
    public void GerarCidade()
    {
        if (seed != 0) Random.InitState(seed);

        if (containerCidade == null)
        {
            Transform existente = transform.Find(NOME_CONTAINER);
            if (existente != null) containerCidade = existente;
        }

        if (containerCidade != null)
        {
            if (Application.isPlaying) Destroy(containerCidade.gameObject);
            else DestroyImmediate(containerCidade.gameObject);
        }

        containerCidade = new GameObject(NOME_CONTAINER).transform;
        containerCidade.parent = transform;

        GerarMalhaMatricial();
    }

    void GerarMalhaMatricial()
    {
        int passoGrid = tamanhoQuarteirao + 1;
        int tamanhoTotalX = blocosX * passoGrid;
        int tamanhoTotalZ = blocosZ * passoGrid;

        for (int x = 0; x <= tamanhoTotalX; x++)
        {
            for (int z = 0; z <= tamanhoTotalZ; z++)
            {
                Vector3 posicaoBase = new Vector3(x * tamanhoTile, 0, z * tamanhoTile);

                bool linhaRuaX = (z % passoGrid == 0);
                bool linhaRuaZ = (x % passoGrid == 0);

                bool bordaEsq = (x == 0);
                bool bordaDir = (x == tamanhoTotalX);
                bool bordaInf = (z == 0);
                bool bordaSup = (z == tamanhoTotalZ);

                bool ehRua = false;

                if (linhaRuaX && linhaRuaZ)
                {
                    ehRua = true;
                    if ((bordaEsq || bordaDir) && (bordaInf || bordaSup))
                    {
                        GameObject curva = ObterPrefabAleatorio(prefabsCurvas) ?? ObterPrefabAleatorio(prefabsCruzamentos);
                        float rot = 0f;
                        if (bordaEsq && bordaInf) rot = 0f;
                        else if (bordaEsq && bordaSup) rot = 90f;
                        else if (bordaDir && bordaSup) rot = 180f;
                        else if (bordaDir && bordaInf) rot = -90f;
                        InstanciarObjeto(curva, posicaoBase, Quaternion.Euler(correcaoRotacaoX, rot, 0), $"Curva_{x}_{z}");
                    }
                    else if (bordaEsq || bordaDir || bordaInf || bordaSup)
                    {
                        GameObject juncaoT = ObterPrefabAleatorio(prefabsJuncoesT) ?? ObterPrefabAleatorio(prefabsCruzamentos);
                        float rot = 0f;
                        if (bordaInf) rot = 0f;
                        else if (bordaEsq) rot = 90f;
                        else if (bordaSup) rot = 180f;
                        else if (bordaDir) rot = -90f;
                        InstanciarObjeto(juncaoT, posicaoBase, Quaternion.Euler(correcaoRotacaoX, rot, 0), $"JuncaoT_{x}_{z}");
                    }
                    else
                    {
                        GameObject cruzamento = ObterPrefabAleatorio(prefabsCruzamentos);
                        float rotAleatoria = Random.Range(0, 4) * 90f;
                        InstanciarObjeto(cruzamento, posicaoBase, Quaternion.Euler(correcaoRotacaoX, rotAleatoria, 0), $"Cruzamento_{x}_{z}");
                    }
                }
                else if (linhaRuaX)
                {
                    ehRua = true;
                    float rotH = Random.value > 0.5f ? 0f : 180f;
                    GameObject ruaH = ObterPrefabAleatorio(prefabsRuasRetas);
                    InstanciarObjeto(ruaH, posicaoBase, Quaternion.Euler(correcaoRotacaoX, rotH, 0), $"RuaH_{x}_{z}");
                }
                else if (linhaRuaZ)
                {
                    ehRua = true;
                    float rotV = Random.value > 0.5f ? 90f : 270f;
                    GameObject ruaV = ObterPrefabAleatorio(prefabsRuasRetas);
                    InstanciarObjeto(ruaV, posicaoBase, Quaternion.Euler(correcaoRotacaoX, rotV, 0), $"RuaV_{x}_{z}");
                }
                else
                {
                    GerarPredioNoLote(posicaoBase, x, z, passoGrid);
                }

                if (ehRua)
                {
                    EspalharPropsComFisica(posicaoBase);
                }
            }
        }
    }

    void EspalharPropsComFisica(Vector3 centroRua)
    {
        if (prefabsProps == null || prefabsProps.Length == 0) return;
        if (Random.value > chanceGerarProp) return;

        int quantidade = Random.Range(1, maxPropsPorRua + 1);

        for (int i = 0; i < quantidade; i++)
        {
            GameObject prop = ObterPrefabAleatorio(prefabsProps);
            if (prop == null) continue;

            float offsetX = Random.Range(-4f, 4f);
            float offsetZ = Random.Range(-4f, 4f);

            // Nasce no ar para cair
            Vector3 posicaoProp = centroRua + new Vector3(offsetX, alturaQueda, offsetZ);

            // Rotação 100% caótica em todos os eixos
            float rotX = Random.Range(0f, 360f);
            float rotY = Random.Range(0f, 360f);
            float rotZ = Random.Range(0f, 360f);

            // No editor não há física: deixa o prop parado no chão, só girado em Y
            Quaternion rotacaoProp = Quaternion.Euler(rotX, rotY, rotZ);
            if (!Application.isPlaying)
            {
                posicaoProp.y = centroRua.y;
                rotacaoProp = Quaternion.Euler(0f, rotY, 0f);
            }

            GameObject propInstanciado = InstanciarObjeto(prop, posicaoProp, rotacaoProp, "Prop_Fisica");

            // ADICIONA A FÍSICA (Apenas se o jogo estiver a rodar)
            if (Application.isPlaying && propInstanciado != null)
            {
                Rigidbody rb = propInstanciado.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = propInstanciado.AddComponent<Rigidbody>();
                }
                // Dá um peso ao lixo para não voar para longe facilmente se o jogador lhe bater
                rb.mass = 5f;
            }
        }
    }

    void GerarPredioNoLote(Vector3 posicao, int x, int z, int passoGrid)
    {
        if (Random.value > densidadePredios) return;

        int posXnoBloco = x % passoGrid;
        int posZnoBloco = z % passoGrid;

        bool bordaEsqLote = (posXnoBloco == 1);
        bool bordaDirLote = (posXnoBloco == tamanhoQuarteirao);
        bool bordaInfLote = (posZnoBloco == 1);
        bool bordaSupLote = (posZnoBloco == tamanhoQuarteirao);

        float rotY = 0f;

        if (bordaEsqLote && bordaInfLote) rotY = Random.value > 0.5f ? 180f : -90f;
        else if (bordaDirLote && bordaInfLote) rotY = Random.value > 0.5f ? 180f : 90f;
        else if (bordaEsqLote && bordaSupLote) rotY = Random.value > 0.5f ? 0f : -90f;
        else if (bordaDirLote && bordaSupLote) rotY = Random.value > 0.5f ? 0f : 90f;
        else if (bordaEsqLote) rotY = -90f;
        else if (bordaDirLote) rotY = 90f;
        else if (bordaInfLote) rotY = 180f;
        else if (bordaSupLote) rotY = 0f;

        Quaternion rotacao = Quaternion.Euler(correcaoRotacaoX, rotY, 0);

        GameObject prefab = ObterPrefabAleatorio(prefabsPredios);
        InstanciarObjeto(prefab, posicao, rotacao, $"Predio_{x}_{z}");
    }

    GameObject ObterPrefabAleatorio(GameObject[] listaPrefabs)
    {
        if (listaPrefabs == null || listaPrefabs.Length == 0) return null;
        return listaPrefabs[Random.Range(0, listaPrefabs.Length)];
    }

    GameObject InstanciarObjeto(GameObject prefab, Vector3 posicao, Quaternion rotacao, string nomeObjeto)
    {
        if (prefab == null) return null;

        GameObject instancia;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefab, containerCidade);
            instancia.transform.position = posicao;
            instancia.transform.rotation = rotacao;
        }
        else
#endif
        {
            instancia = Instantiate(prefab, posicao, rotacao, containerCidade);
        }
        instancia.name = nomeObjeto;
        return instancia;
    }
}