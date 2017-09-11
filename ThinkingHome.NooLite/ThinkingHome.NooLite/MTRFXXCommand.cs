namespace ThinkingHome.NooLite
{
    public enum MTRFXXCommand : byte
    {
        Off = 0,
        BrightnessDown = 1,
        On = 2,
        BrightnessUp = 3,

        Toggle = 4,
        Switch = 4,

        BrightnessBack = 5,
        SetBrightness = 6,
        LoadPreset = 7,
        SavePreset = 8,
        Unbind = 9,
        BrightnessStop = 10,
        BrightnessStepDown = 11,
        BrightnessStepUp = 12,
        BrightnessStart = 13,

        Bind = 15
    }
}
