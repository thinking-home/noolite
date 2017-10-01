namespace ThinkingHome.NooLite.Internal
{
    public enum MTRFXXDataFormat : byte
    {
        NoData = 0x00,

        OneByteData = 0x01,

        FourByteData = 0x03,

        LED = 0x04,

        TemporarySwitchOnOneByte = 0x05,

        TemporarySwitchOnTwoBytes = 0x06,

        MicroclimateData = 0x07
    }
}
