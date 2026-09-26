// 01. Adapta somente áudio à autoridade existente; entrar na partida não reaplica gráficos.
public sealed class WDMatchSettingsRuntime : IWDSettingsRuntime
{
    private readonly WDUnitySettingsRuntime runtime = new();

    public void ApplyAudio(float master, float music, float sfx) =>
        runtime.ApplyAudio(master, music, sfx);

    public void ApplyGraphics(WDFrameRateLimit frameRate, WDGraphicsQuality quality, bool fullscreen)
    {
        // 02. Configurações gráficas continuam pertencendo à Home.
    }
}
