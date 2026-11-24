using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Menu "Sinal da Lia > Gerar Build Windows (.exe)": gera o jogo pronto pra entregar em Build/SinalDaLia/.
// Linha de comando: Unity -batchmode -quit -projectPath . -executeMethod SinalDaLiaBuild.Windows
public static class SinalDaLiaBuild
{
    const string CENA = "Assets/Scenes/Game.unity";
    const string PASTA = "Build/SinalDaLia";

    public static void ConfigurarPlayer()
    {
        PlayerSettings.productName = "Sinal da Lia";
        PlayerSettings.companyName = "Grupo Sinal da Lia";
        PlayerSettings.bundleVersion = "1.4.1";
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false;
    }

    [MenuItem("Sinal da Lia/Gerar Build Windows (.exe)")]
    public static void Windows()
    {
        ConfigurarPlayer();
        var opcoes = new BuildPlayerOptions
        {
            scenes = new[] { CENA },
            locationPathName = PASTA + "/SinalDaLia.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        var rel = BuildPipeline.BuildPlayer(opcoes);
        bool ok = rel.summary.result == BuildResult.Succeeded;
        if (ok && File.Exists("COMO_JOGAR.txt")) File.Copy("COMO_JOGAR.txt", PASTA + "/COMO_JOGAR.txt", true);
        string msg = ok
            ? $"Build pronta: {Path.GetFullPath(PASTA)}\\SinalDaLia.exe ({rel.summary.totalSize / (1024 * 1024)} MB, {rel.summary.totalTime.TotalSeconds:0} s)"
            : $"Build FALHOU: {rel.summary.result} - {rel.summary.totalErrors} erro(s). Veja o Console.";
        Debug.Log("[Sinal da Lia] " + msg);
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        else EditorUtility.DisplayDialog("Sinal da Lia", msg + (ok ? "\n\nPra entregar: zipe a pasta Build/SinalDaLia inteira." : ""), "OK");
    }
}
