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

        public static void On(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.On, false, channel);
        }

        public static void OnF(this MTRFXXAdapter adapter, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.On, true, channel, deviceId);
        }

        #endregion

        #region cmd: off

        public static void Off(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.Off, false, channel);
        }

        public static void OffF(this MTRFXXAdapter adapter, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.Off, true, channel, deviceId);
        }

        #endregion

        #region cmd: switch

        public static void Switch(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.Switch, false, channel);
        }

        public static void SwitchF(this MTRFXXAdapter adapter, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.Switch, true, channel, deviceId);
        }

        #endregion

        #region cmd: temporary switch on

        public static void TemporarySwitchOn(this MTRFXXAdapter adapter, byte channel, UInt16 interval)
        {
            byte b1 = (byte)interval;
            byte b2 = (byte)(interval >> 8);
            var format = b2 == 0
                ? MTRFXXDataFormat.TemporarySwitchOnOneByte
                : MTRFXXDataFormat.TemporarySwitchOnTwoBytes;

            SendData(adapter, MTRFXXCommand.TemporarySwitchOn, false, channel, null, format, b1, b2);
        }

        public static void TemporarySwitchOnF(this MTRFXXAdapter adapter, byte channel, UInt16 interval, UInt32? deviceId = null)
        {
            byte b1 = (byte)interval;
            byte b2 = (byte)(interval >> 8);
            var format = b2 == 0
                ? MTRFXXDataFormat.TemporarySwitchOnOneByte
                : MTRFXXDataFormat.TemporarySwitchOnTwoBytes;

            SendData(adapter, MTRFXXCommand.TemporarySwitchOn, true, channel, deviceId, format, b1, b2);
        }

        #endregion

        #region cmd: set brightness

        public static void SetBrightness(this MTRFXXAdapter adapter, byte channel, byte brightness)
        {
            SendData(adapter, MTRFXXCommand.SetBrightness, false, channel, null, MTRFXXDataFormat.OneByteData, brightness);
        }

        public static void SetBrightnessF(this MTRFXXAdapter adapter, byte channel, byte brightness, UInt32? deviceId = null)
        {
            SendData(adapter, MTRFXXCommand.SetBrightness, true, channel, deviceId, MTRFXXDataFormat.OneByteData, brightness);
        }

        #endregion

        #endregion

        #region presets

        #region cmd: save preset

        public static void SavePreset(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.SavePreset, false, channel);
        }

        public static void SavePresetF(this MTRFXXAdapter adapter, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.SavePreset, true, channel, deviceId);
        }

        #endregion

        #region cmd: load preset

        public static void LoadPreset(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.LoadPreset, false, channel);
        }

        public static void LoadPresetF(this MTRFXXAdapter adapter, byte channel, UInt32? deviceId = null)
        {
            Send(adapter, MTRFXXCommand.LoadPreset, true, channel, deviceId);
        }

        #endregion

        #endregion

        #region led

        #region cmd: set led color

        public static void SetLedColor(this MTRFXXAdapter adapter, byte channel, byte valueR, byte valueG, byte valueB)
        {
            SendData(adapter, MTRFXXCommand.SetBrightness, false, channel, null, MTRFXXDataFormat.FourByteData, valueR, valueG, valueB);
        }

        public static void SetLedColorF(this MTRFXXAdapter adapter, byte channel, byte valueR, byte valueG, byte valueB, UInt32? deviceId = null)
        {
            SendData(adapter, MTRFXXCommand.SetBrightness, true, channel, deviceId, MTRFXXDataFormat.FourByteData, valueR, valueG, valueB);
        }

        #endregion

        #region cmd: start color changing

        public static void SwitchColorChanging(this MTRFXXAdapter adapter, byte channel)
        {
            SendData(adapter, MTRFXXCommand.SwitchColorChanging, false, channel, null, MTRFXXDataFormat.LED);
        }

        public static void SwitchColorChangingF(this MTRFXXAdapter adapter, byte channel, UInt32? deviceId = null)
        {
            SendData(adapter, MTRFXXCommand.SwitchColorChanging, true, channel, deviceId, MTRFXXDataFormat.LED);
        }

        #endregion

        #region cmd: change color

        public static void ChangeLedColor(this MTRFXXAdapter adapter, byte channel)
        {
            SendData(adapter, MTRFXXCommand.ChangeColor, false, channel, null, MTRFXXDataFormat.LED);
        }

        public static void ChangeLedColorF(this MTRFXXAdapter adapter, byte channel, UInt32? deviceId = null)
        {
            SendData(adapter, MTRFXXCommand.ChangeColor, true, channel, deviceId, MTRFXXDataFormat.LED);
        }

        #endregion

        #region cmd: change color mode

        public static void ChangeLedColorMode(this MTRFXXAdapter adapter, byte channel)
        {
            SendData(adapter, MTRFXXCommand.ChangeColorMode, false, channel, null, MTRFXXDataFormat.LED);
        }

        public static void ChangeLedColorModeF(this MTRFXXAdapter adapter, byte channel, UInt32? deviceId = null)
        {
            SendData(adapter, MTRFXXCommand.ChangeColorMode, true, channel, deviceId, MTRFXXDataFormat.LED);
        }

        #endregion

        #region cmd: change color speed

        public static void ChangeLedColorSpeed(this MTRFXXAdapter adapter, byte channel)
        {
            SendData(adapter, MTRFXXCommand.ChangeColorSpeed, false, channel, null, MTRFXXDataFormat.LED);
        }

        public static void ChangeLedColorSpeedF(this MTRFXXAdapter adapter, byte channel, UInt32? deviceId = null)
        {
            SendData(adapter, MTRFXXCommand.ChangeColorSpeed, true, channel, deviceId, MTRFXXDataFormat.LED);
        }

        #endregion

        #endregion

        #region binding

        #region cmd: bind

        public static void Bind(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.Bind, false, channel);
        }

        public static void BindF(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.Bind, true, channel);
        }

        #endregion

        #region cmd: unbind

        public static void Unbind(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.Unbind, false, channel);
        }

        public static void UnbindF(this MTRFXXAdapter adapter, byte channel)
        {
            Send(adapter, MTRFXXCommand.Unbind, true, channel);
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


        public static void ReadState(this MTRFXXAdapter adapter, byte channel)
        {
            adapter.SendCommand(MTRFXXMode.TXF, MTRFXXAction.SendCommand, channel, MTRFXXCommand.ReadState);
        }

        #endregion
    }
}