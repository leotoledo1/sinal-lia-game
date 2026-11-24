using UnityEngine;

// Opções do jogador (volume e sensibilidade do mouse), salvas entre partidas
public static class Opcoes
{
    const string VOLUME = "SinalDaLia_Volume";
    const string SENS = "SinalDaLia_Sensibilidade";

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VOLUME, 0.8f);
        set
        {
            PlayerPrefs.SetFloat(VOLUME, Mathf.Clamp01(value));
            AudioListener.volume = Volume;
        }
    }

    // multiplica a sensibilidade base da câmera (1 = padrão)
    public static float Sensibilidade
    {
        get => PlayerPrefs.GetFloat(SENS, 1f);
        set => PlayerPrefs.SetFloat(SENS, Mathf.Clamp(value, 0.3f, 3f));
    }

    public static void Aplicar() => AudioListener.volume = Volume;
}
