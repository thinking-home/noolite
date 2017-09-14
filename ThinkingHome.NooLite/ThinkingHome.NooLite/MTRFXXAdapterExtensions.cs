using ThinkingHome.NooLite.Params;

namespace ThinkingHome.NooLite
{
    public static class MTRFXXAdapterExtensions
    {
        private static void Send(MTRFXXAdapter adapter, MTRFXXCommand command, byte channel = 0)
        {
            adapter.SendCommand(MTRFXXMode.TX, MTRFXXAction.SendCommand, channel, command);
        }

        private static void SendData(MTRFXXAdapter adapter, MTRFXXCommand command, byte channel, MTRFXXDataFormat format, params byte[] data)
        {
            adapter.SendCommand(MTRFXXMode.TX, MTRFXXAction.SendCommand, channel, command, MTRFXXRepeatCount.NoRepeat, format, data);
        }
        
        // brightness
        public static void On(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.On, channel);
        }

        public static void Off(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.Off, channel);
        }

        public static void Toggle(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.Switch, channel);
        }

        public static void SetBrightness(this MTRFXXAdapter adapter, byte channel, byte brightness)
        {
            SendData(adapter, MTRFXXCommand.SetBrightness, channel, MTRFXXDataFormat.OneByteData, brightness);
        }

        // presets
        public static void SavePreset(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.SavePreset, channel);    
        }

        public static void LoadPreset(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.LoadPreset, channel);
        }

        // led
        public static void SetLedColor(this MTRFXXAdapter adapter, byte channel, byte valueR, byte valueG, byte valueB)
        {
            SendData(adapter, MTRFXXCommand.SetBrightness, channel, MTRFXXDataFormat.LED, valueR, valueG, valueB);
        }

        public static void ChangeLedColor(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.ChangeColor, channel);
        }

        public static void ChangeLedColorMode(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.ChangeColorMode, channel);
        }

        public static void ChangeLedColorSpeed(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.ChangeColorSpeed, channel);
        }

        // binding tx
        public static void Bind(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.Bind, channel);
        }

        public static void UnBind(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.Unbind, channel);
        }
        
        // binding rx
        public static void BindStart(this MTRFXXAdapter adapter, byte channel)
        {
            adapter.SendCommand(MTRFXXMode.RX, MTRFXXAction.StartBinding, channel, MTRFXXCommand.None);
        }

        public static void BindStop(this MTRFXXAdapter adapter)
        {
            adapter.SendCommand(MTRFXXMode.RX, MTRFXXAction.StopBinding, 0, MTRFXXCommand.None);
        }

        public static void ClearChannel(this MTRFXXAdapter adapter, byte channel)
        {
            adapter.SendCommand(MTRFXXMode.RX, MTRFXXAction.ClearChannel, channel, MTRFXXCommand.None);
        }

        public static void ClearAllChannels(this MTRFXXAdapter adapter)
        {
            adapter.SendCommand(MTRFXXMode.RX, MTRFXXAction.ClearAllChannels, 0, MTRFXXCommand.None, 
                MTRFXXRepeatCount.NoRepeat, MTRFXXDataFormat.NoData, new byte[] { 170, 85, 170, 85 });
        } 

        public static void ExitServiceMode(this MTRFXXAdapter adapter)
        {
            adapter.SendCommand(MTRFXXMode.Service, MTRFXXAction.SendCommand, 0, MTRFXXCommand.None);
        }   
    }
}