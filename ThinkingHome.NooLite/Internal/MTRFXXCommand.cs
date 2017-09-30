namespace ThinkingHome.NooLite.Internal
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

        SwitchColorChanging = 16,
        ChangeColor = 17,
        ChangeColorMode = 18,
        ChangeColorSpeed = 19,

        LowBatteryStatus = 20,
        MicroclimateData = 21,
        TemporarySwitchOn = 25,
        Modes = 26,
        ReadState = 128,
        WriteState = 129,
        SendState = 130,
        Service = 131,
        ClearMemory = 132
    }
}
