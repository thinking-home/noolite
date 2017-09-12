namespace ThinkingHome.NooLite
{
    public static class MTRFXXAdapterExtensions
    {
        // brightness
        public static void On(this MTRFXXAdapter adapter, int channel) { }
        public static void Off(this MTRFXXAdapter adapter, int channel) { }
        public static void Toggle(this MTRFXXAdapter adapter, int channel) { }
        public static void SetBrightness(this MTRFXXAdapter adapter, int channel, int brightness) { }

        // presets
        public static void SavePreset(this MTRFXXAdapter adapter, int channel) { }
        public static void LoadPreset(this MTRFXXAdapter adapter, int channel) { }

        // led
        public static void SetLedColor(this MTRFXXAdapter adapter, int channel, byte valueR, byte valueG, byte valueB) { }
        public static void ChangeLedColor(this MTRFXXAdapter adapter, int channel) { }
        public static void ChangeLedColorMode(this MTRFXXAdapter adapter, int channel) { }
        public static void ChangeLedColorSpeed(this MTRFXXAdapter adapter, int channel) { }

        // binding tx
        public static void Bind(this MTRFXXAdapter adapter, int channel) { }
        public static void UnBind(this MTRFXXAdapter adapter, int channel) { }
        
        // binding rx
        public static void BindStart(this MTRFXXAdapter adapter) { }
        public static void BindStop(this MTRFXXAdapter adapter) { }
        public static void UnBindAll(this MTRFXXAdapter adapter) { }                
    }
}