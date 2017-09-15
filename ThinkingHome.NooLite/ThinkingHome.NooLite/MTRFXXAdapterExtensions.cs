using System;
using ThinkingHome.NooLite.Internal;

namespace ThinkingHome.NooLite
{
    public static class MTRFXXAdapterExtensions
    {
        #region private
        
        private static void Send(MTRFXXAdapter adapter, MTRFXXCommand command, Mode mode, byte channel = 0)
        {
            var _mode = mode == Mode.Default ? MTRFXXMode.TX : MTRFXXMode.TXF;
            var _action = mode == Mode.Broadcast ? MTRFXXAction.SendBroadcastCommand : MTRFXXAction.SendCommand;

            adapter.SendCommand(_mode, _action, channel, command);
        }

        private static void Send(MTRFXXAdapter adapter, MTRFXXCommand command, UInt32 deviceId)
        {
            var _mode = MTRFXXMode.TXF;
            var _action = MTRFXXAction.SendTargetedCommand;

            adapter.SendCommand(_mode, _action, 0, command, target: deviceId);
        }

        private static void SendData(MTRFXXAdapter adapter, MTRFXXCommand command, Mode mode, byte channel, MTRFXXDataFormat format, params byte[] data)
        {
            var _mode = mode == Mode.Default ? MTRFXXMode.TX : MTRFXXMode.TXF;
            var _action = mode == Mode.Broadcast ? MTRFXXAction.SendBroadcastCommand : MTRFXXAction.SendCommand;
            
            adapter.SendCommand(_mode, _action, channel, command, MTRFXXRepeatCount.NoRepeat, format, data);
        }
        
        private static void SendData(MTRFXXAdapter adapter, MTRFXXCommand command, UInt32 deviceId, MTRFXXDataFormat format, params byte[] data)
        {
            var _mode = MTRFXXMode.TXF;
            var _action = MTRFXXAction.SendTargetedCommand;
            
            adapter.SendCommand(_mode, _action, 0, command, MTRFXXRepeatCount.NoRepeat, format, data, target: deviceId);
        }
        
        #endregion

        #region brightness

        #region cmd: on

        public static void On(this MTRFXXAdapter adapter, Mode mode, byte channel)
        {
            Send(adapter, MTRFXXCommand.On, mode, channel);
        }

        public static void On(this MTRFXXAdapter adapter, UInt32 deviceId)
        {
            Send(adapter, MTRFXXCommand.On, deviceId);
        }

        #endregion
        
        #region cmd: off

        public static void Off(this MTRFXXAdapter adapter, Mode mode, byte channel)
        {
            Send(adapter, MTRFXXCommand.Off, mode, channel);
        }

        public static void Off(this MTRFXXAdapter adapter, UInt32 deviceId)
        {
            Send(adapter, MTRFXXCommand.Off, deviceId);
        }

        #endregion

        #region cmd: switch

        public static void Switch(this MTRFXXAdapter adapter, Mode mode, byte channel)
        {
            Send(adapter, MTRFXXCommand.Switch, mode, channel);
        }

        public static void Switch(this MTRFXXAdapter adapter, UInt32 deviceId)
        {
            Send(adapter, MTRFXXCommand.Switch, deviceId);
        }

        #endregion
        
        #region cmd: set brightness

        public static void SetBrightness(this MTRFXXAdapter adapter, Mode mode, byte channel, byte brightness)
        {
            SendData(adapter, MTRFXXCommand.SetBrightness, mode, channel, MTRFXXDataFormat.OneByteData, brightness);
        }

        public static void SetBrightness(this MTRFXXAdapter adapter, UInt32 deviceId, byte brightness)
        {
            SendData(adapter, MTRFXXCommand.SetBrightness, deviceId, MTRFXXDataFormat.OneByteData, brightness);
        }

        #endregion
        
        #endregion

        #region presets

        #region cmd: save preset 

        public static void SavePreset(this MTRFXXAdapter adapter, Mode mode, byte channel)
        {
            Send(adapter, MTRFXXCommand.SavePreset, mode, channel);
        }

        public static void SavePreset(this MTRFXXAdapter adapter, UInt32 deviceId)
        {
            Send(adapter, MTRFXXCommand.SavePreset, deviceId);
        }

        #endregion

        #region cmd: load preset 

        public static void LoadPreset(this MTRFXXAdapter adapter, Mode mode, byte channel)
        {
            Send(adapter, MTRFXXCommand.LoadPreset, mode, channel);
        }

        public static void LoadPreset(this MTRFXXAdapter adapter, UInt32 deviceId)
        {
            Send(adapter, MTRFXXCommand.LoadPreset, deviceId);
        }

        #endregion

        #endregion

        #region led

        #region cmd: set led color

        public static void SetLedColor(this MTRFXXAdapter adapter, Mode mode, byte channel, byte valueR, byte valueG, byte valueB)
        {
            SendData(adapter, MTRFXXCommand.SetBrightness, mode, channel, MTRFXXDataFormat.LED, valueR, valueG, valueB);
        }

        public static void SetLedColor(this MTRFXXAdapter adapter, UInt32 deviceId, byte valueR, byte valueG, byte valueB)
        {
            SendData(adapter, MTRFXXCommand.SetBrightness, deviceId, MTRFXXDataFormat.LED, valueR, valueG, valueB);
        }

        #endregion
        
        #region cmd: change color

        public static void ChangeLedColor(this MTRFXXAdapter adapter, Mode mode, byte channel)
        {
            Send(adapter, MTRFXXCommand.ChangeColor, mode, channel);
        }

        public static void ChangeLedColor(this MTRFXXAdapter adapter, UInt32 deviceId)
        {
            Send(adapter, MTRFXXCommand.ChangeColor, deviceId);
        }

        #endregion
        
        #region cmd: change color mode

        public static void ChangeLedColorMode(this MTRFXXAdapter adapter, Mode mode, byte channel)
        {
            Send(adapter, MTRFXXCommand.ChangeColorMode, mode, channel);
        }

        public static void ChangeLedColorMode(this MTRFXXAdapter adapter, UInt32 deviceId)
        {
            Send(adapter, MTRFXXCommand.ChangeColorMode, deviceId);
        }

        #endregion

        #region cmd: change color speed

        public static void ChangeLedColorSpeed(this MTRFXXAdapter adapter, Mode mode, byte channel)
        {
            Send(adapter, MTRFXXCommand.ChangeColorSpeed, mode, channel);
        }

        public static void ChangeLedColorSpeed(this MTRFXXAdapter adapter, UInt32 deviceId)
        {
            Send(adapter, MTRFXXCommand.ChangeColorSpeed, deviceId);
        }

        #endregion
        
        #endregion
        
        #region binding

        #region cmd: bind

        public static void Bind(this MTRFXXAdapter adapter, Mode mode, byte channel)
        {
            Send(adapter, MTRFXXCommand.Bind, mode, channel);
        }

        public static void Bind(this MTRFXXAdapter adapter, UInt32 deviceId)
        {
            Send(adapter, MTRFXXCommand.Bind, deviceId);
        }

        #endregion

        #region cmd: unbind

        public static void Unbind(this MTRFXXAdapter adapter, Mode mode, byte channel)
        {
            Send(adapter, MTRFXXCommand.Unbind, mode, channel);
        }

        public static void Unbind(this MTRFXXAdapter adapter, UInt32 deviceId)
        {
            Send(adapter, MTRFXXCommand.Unbind, deviceId);
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