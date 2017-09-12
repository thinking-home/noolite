namespace ThinkingHome.NooLite
{
    public enum MTRFXXCommand : byte
    {
        None = 0,
        
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
        BrightnessStop = 10,
        BrightnessStepDown = 11,
        BrightnessStepUp = 12,
        BrightnessStart = 13,

        Bind = 15,
        Unbind = 9,
        
        Roll_Colour = 16,
        Switch_Colour = 17,
        Switch_Mode = 18,
        Speed_Mode_Back = 19
    }
}
