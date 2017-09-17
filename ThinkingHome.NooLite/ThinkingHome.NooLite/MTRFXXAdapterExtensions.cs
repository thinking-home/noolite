using System;
using ThinkingHome.NooLite.Internal;

namespace ThinkingHome.NooLite
{
    public static class MTRFXXAdapterExtensions
    {
        #region private

        private static Tuple<MTRFXXMode, MTRFXXAction> GetModeAndAction(bool useFMode, UInt32? deviceId)
        {
            var mode = useFMode ? MTRFXXMode.TXF : MTRFXXMode.TX;
            var action = useFMode 
                ? deviceId.HasValue
                    ? deviceId == 0 ? MTRFXXAction.SendBroadcastCommand : MTRFXXAction.SendTargetedCommand
                    : MTRFXXAction.SendCommand
                : MTRFXXAction.SendCommand;
            
            return new Tuple<MTRFXXMode, MTRFXXAction>(mode, action);
        }

        private static void Send(MTRFXXAdapter adapter, MTRFXXCommand command, bool useFMode, byte channel = 0, UInt32? deviceId = null)
        {
            var ma = GetModeAndAction(useFMode, deviceId);

            adapter.SendCommand(ma.Item1, ma.Item2, channel, command, target: deviceId ?? 0);
        }

        private static void SendData(MTRFXXAdapter adapter, MTRFXXCommand command, bool useFMode, byte channel, UInt32? deviceId, MTRFXXDataFormat format, params byte[] data)
        {
            var ma = GetModeAndAction(useFMode, deviceId);
            
            adapter.SendCommand(ma.Item1, ma.Item2, channel, command, MTRFXXRepeatCount.NoRepeat, format, data, deviceId ?? 0);
        }
        
        
        #endregion

        #region brightness

        #region cmd: on

        public static void On(this MTRFXXAdapter adapter, bool useFMode, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.On, useFMode, channel, deviceId);
        }

        #endregion
        
        #region cmd: off

        public static void Off(this MTRFXXAdapter adapter, bool useFMode, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.Off, useFMode, channel, deviceId);
        }

        #endregion

        #region cmd: switch

        public static void Switch(this MTRFXXAdapter adapter, bool useFMode, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.Switch, useFMode, channel, deviceId);
        }

        #endregion

        #region cmd: temporary switch on

        public static void TemporarySwitchOn(this MTRFXXAdapter adapter, bool useFMode, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.TemporarySwitchOn, useFMode, channel, deviceId);
        }

        #endregion
        
        #region cmd: set brightness

        public static void SetBrightness(this MTRFXXAdapter adapter, bool useFMode, byte channel, byte brightness, UInt32? deviceId = null)
        {
            SendData(adapter, MTRFXXCommand.SetBrightness, useFMode, channel, deviceId, MTRFXXDataFormat.OneByteData, brightness);
        }

        #endregion
        
        #endregion

        #region presets

        #region cmd: save preset 

        public static void SavePreset(this MTRFXXAdapter adapter, bool useFMode, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.SavePreset, useFMode, channel, deviceId);
        }

        #endregion

        #region cmd: load preset 

        public static void LoadPreset(this MTRFXXAdapter adapter, bool useFMode, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.LoadPreset, useFMode, channel, deviceId);
        }

        #endregion

        #endregion

        #region led

        #region cmd: set led color

        public static void SetLedColor(this MTRFXXAdapter adapter, bool useFMode, byte channel, byte valueR, byte valueG, byte valueB, UInt32? deviceId = null)
        {
            SendData(adapter, MTRFXXCommand.SetBrightness, useFMode, channel, deviceId, MTRFXXDataFormat.LED, valueR, valueG, valueB);
        }

        #endregion
        
        #region cmd: change color

        public static void ChangeLedColor(this MTRFXXAdapter adapter, bool useFMode, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.ChangeColor, useFMode, channel, deviceId);
        }

        #endregion
        
        #region cmd: change color mode

        public static void ChangeLedColorMode(this MTRFXXAdapter adapter, bool useFMode, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.ChangeColorMode, useFMode, channel, deviceId);
        }

        #endregion

        #region cmd: change color speed

        public static void ChangeLedColorSpeed(this MTRFXXAdapter adapter, bool useFMode, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.ChangeColorSpeed, useFMode, channel, deviceId);
        }

        #endregion
        
        #endregion
        
        #region binding

        #region cmd: bind

        public static void Bind(this MTRFXXAdapter adapter, bool useFMode, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.Bind, useFMode, channel, deviceId);
        }

        #endregion

        #region cmd: unbind

        public static void Unbind(this MTRFXXAdapter adapter, bool useFMode, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.Unbind, useFMode, channel, deviceId);
        }

        #endregion
        
        #endregion

        #region RX mode
        
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
        
        #endregion
    }
}