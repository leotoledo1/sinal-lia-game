using UnityEditor;
using UnityEditor.SceneManagement;

// Menu "Sinal da Lia > Diagnóstico (fotos e medidas)": abre o jogo em Play, mede tamanhos e tira fotos de conferência.
// Linha de comando: Unity -batchmode -executeMethod SinalDaLiaDiagnostico.Batch (sem -quit)
public static class SinalDaLiaDiagnostico
{
    [MenuItem("Sinal da Lia/Diagnóstico (fotos e medidas)")]
    public static void Rodar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Batch();
    }

    // quanto de cada osso da mão direita mexe na malha do Marcos (dedos ligados ou mão rígida?)
    public static void PesosMao()
    {
        var go = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Quaternius/PostApocalypse/Characters Matt/Characters_Matt.fbx");
        var sb = new System.Text.StringBuilder();
        foreach (var smr in go.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true))
        {
            var m = smr.sharedMesh;
            var soma = new float[smr.bones.Length];
            foreach (var w in m.boneWeights) { soma[w.boneIndex0] += w.weight0; soma[w.boneIndex1] += w.weight1; soma[w.boneIndex2] += w.weight2; soma[w.boneIndex3] += w.weight3; }
            sb.AppendLine($"{smr.name}: {m.vertexCount} vértices");
            for (int i = 0; i < smr.bones.Length; i++)
                if (smr.bones[i] != null && smr.bones[i].name.EndsWith(".R") && (smr.bones[i].name.Contains("Arm") || smr.bones[i].name.Contains("1") || smr.bones[i].name.Contains("2") || smr.bones[i].name.Contains("3")))
                    sb.AppendLine($"  {smr.bones[i].name}: {soma[i]:0.0}");
        }
        System.IO.File.WriteAllText("Logs/PesosMao.txt", sb.ToString());
        EditorApplication.Exit(0);
    }

    public static void Batch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        SessionState.SetBool(Diagnostico.FLAG, true);
        EditorApplication.EnterPlaymode();
    }
}
