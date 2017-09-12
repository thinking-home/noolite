namespace ThinkingHome.NooLite
{
    public static class MTRFXXAdapterExtensions
    {
        public static void On(this MTRFXXAdapter adapter, int channel) { }
        public static void Off(this MTRFXXAdapter adapter, int channel) { }
        public static void SetBrightness(this MTRFXXAdapter adapter, int channel, int brightness) { }
        public static void Bind(this MTRFXXAdapter adapter, int channel) { }
        public static void UnBind(this MTRFXXAdapter adapter, int channel) { }
        public static void SetLedColor(this MTRFXXAdapter adapter, int channel, byte valueR, byte valueG, byte valueB) { }
        
    }
}