using HidSharp;
using System.Buffers.Binary;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace IronNebula;

/// <summary>Nonblocking game-facing native DualSense transport. All HID I/O belongs to one worker.</summary>
internal sealed class DualSenseDevice : IDisposable
{
    private readonly object _gate=new();
    private readonly CancellationTokenSource _cancel=new();
    private Task? _worker;
    private GamePadState _state;
    private long _lastInput,_lastFeedback;
    private int _generation,_battery=-1;
    private bool _bluetooth;
    private string _status="SEARCHING FOR DUALSENSE";
    private Feedback _feedback=new();
    private readonly HapticSignal _signal=new();
    private sealed record Feedback(bool Live=false,bool Adaptive=true,bool Haptics=true,bool BluetoothAudio=false,
        float Strength=.65f,float Low=0,float High=0,int Weapon=0,bool Firing=false,bool Precision=false,
        Color Color=default);

    internal bool TryState(out GamePadState state,out int generation)
    {
        _worker??=Task.Run(Work);
        lock(_gate)
        {
            state=Environment.TickCount64-_lastInput<250?_state:default;
            generation=_generation;
            return state.IsConnected;
        }
    }

    internal string Status { get { lock(_gate) return _status; } }
    internal void SetFeedback(bool live,bool adaptive,bool haptics,bool bluetoothAudio,float strength,
        float low,float high,int weapon,bool firing,bool precision,Color color)
    {
        lock(_gate)
        {
            _feedback=new(live,adaptive,haptics,bluetoothAudio,strength,low,high,weapon,firing,precision,color);
            _lastFeedback=Environment.TickCount64;
            if(!live) _signal.Set(0,0,weapon,false);
        }
    }

    private void Work()
    {
        while(!_cancel.IsCancellationRequested)
        {
            try
            {
                // Genuine Sony DualSense and DualSense Edge only. Never open arbitrary HID devices.
                var devices=DeviceList.Local.GetHidDevices(0x054c)
                    .Where(d=>d.ProductID is 0x0ce6 or 0x0df2)
                    .OrderBy(d=>d.GetMaxInputReportLength()==64?0:1).ToArray();
                bool opened=false;
                foreach(var device in devices)
                {
                    if(_cancel.IsCancellationRequested) break;
                    try
                    {
                        if(!device.TryOpen(out HidStream stream)) continue;
                        opened=true;
                        using(stream) RunConnected(device,stream);
                        break;
                    }
                    catch(Exception ex) when(ex is not OutOfMemoryException)
                    { SetStatus("DUALSENSE DISCONNECTED / DEVICE BUSY"); }
                }
                if(!opened) SetStatus("NO NATIVE DUALSENSE / XINPUT AVAILABLE");
            }
            catch(Exception ex) when(ex is not OutOfMemoryException)
            { SetStatus("DUALSENSE HID UNAVAILABLE / XINPUT AVAILABLE"); }
            lock(_gate) { _state=default;_lastInput=0; }
            _signal.Set(0,0,0,false);
            _cancel.Token.WaitHandle.WaitOne(1500);
        }
    }

    private void RunConnected(HidDevice device,HidStream stream)
    {
        stream.ReadTimeout=4;stream.WriteTimeout=100;
        _bluetooth=device.GetMaxInputReportLength()>64;
        // Feature read enables full reports on Bluetooth. No firmware or pairing writes.
        if(_bluetooth)
        {
            try { byte[] feature=new byte[41];feature[0]=0x05;stream.GetFeature(feature); }
            catch(IOException) { /* Some firmware enables full reports after the first output. */ }
        }
        using var audio=new DualSenseHaptics(_signal);
        byte[] input=new byte[device.GetMaxInputReportLength()];
        byte sequence=0,counter=0;
        long lastInput=Environment.TickCount64,nextEffects=0,nextUsbProbe=0;
        double nextAudio=0;
        bool wasStreaming=false,hadInput=false;
        bool lightbarReady=!_bluetooth,lightbarReset=false;
        try
        {
            while(!_cancel.IsCancellationRequested)
            {
                try
                {
                    int count=stream.Read(input);
                    if(DualSenseProtocol.TryInput(input.AsSpan(0,count),out var state,out int battery))
                    {
                        lastInput=Environment.TickCount64;
                        if(_bluetooth && BinaryPrimitives.ReadUInt32LittleEndian(input.AsSpan(29,4))>=10200000)
                            lightbarReady=true;
                        lock(_gate)
                        {
                            if(!hadInput) { _generation++;hadInput=true; }
                            _state=state;_battery=battery;_lastInput=lastInput;
                        }
                    }
                }
                catch(TimeoutException) { }
                long now=Environment.TickCount64;
                if(now-lastInput>2000) break;
                Feedback feedback;
                lock(_gate) feedback=now-_lastFeedback<250?_feedback:_feedback with { Live=false };
                if(!_bluetooth && feedback.Haptics && now>=nextUsbProbe && !audio.Ready)
                { nextUsbProbe=now+5000;audio.TryStart(); }
                bool streaming=feedback.Live && feedback.Haptics &&
                    (_bluetooth?feedback.BluetoothAudio:audio.Ready);
                if(streaming && !wasStreaming) nextAudio=now;
                _signal.Set(streaming?feedback.Low*feedback.Strength:0,
                    streaming?feedback.High*feedback.Strength:0,feedback.Weapon,feedback.Firing);
                if(now>=nextEffects || streaming!=wasStreaming)
                {
                    Write(stream,device,DualSenseProtocol.Effects(_bluetooth,sequence++,feedback.Live,
                        feedback.Adaptive,streaming,feedback.Haptics?feedback.Low*feedback.Strength:0,
                        feedback.Haptics?feedback.High*feedback.Strength:0,
                        feedback.Weapon,feedback.Firing,feedback.Precision,feedback.Color,lightbarReady && !lightbarReset));
                    if(lightbarReady) lightbarReset=true;
                    nextEffects=now+32;
                }
                if(_bluetooth && (streaming || wasStreaming) && now>=nextAudio)
                {
                    var samples=new sbyte[64];
                    for(int i=0;i<32;i++)
                    {
                        _signal.Next(3000,out float left,out float right);
                        samples[i*2]=(sbyte)(left*127);samples[i*2+1]=(sbyte)(right*127);
                    }
                    Write(stream,device,DualSenseProtocol.BluetoothAudio(sequence++,counter++,samples));
                    nextAudio=Math.Max(nextAudio+32000d/3000,now-20);
                }
                wasStreaming=streaming;
                SetStatus($"DUALSENSE {(_bluetooth?"BT":"USB")} / {_battery}% / "+
                    (streaming?(_bluetooth?"HD HAPTICS EXPERIMENTAL":"HD HAPTICS"):
                    feedback.Haptics && !_bluetooth && !audio.Ready?"RUMBLE / AUDIO UNAVAILABLE":"READY"));
            }
        }
        finally
        {
            _signal.Set(0,0,0,false);
            try
            {
                // Release BOTH adaptive triggers and all actuators on exit/disconnect.
                Write(stream,device,DualSenseProtocol.Effects(_bluetooth,sequence++,false,false,false,
                    0,0,0,false,false,Color.Black));
                if(_bluetooth && wasStreaming)
                    Write(stream,device,DualSenseProtocol.BluetoothAudio(sequence++,counter,new sbyte[64]));
            }
            catch(IOException) { }
            catch(TimeoutException) { }
        }
    }

    private static void Write(HidStream stream,HidDevice device,byte[] report)
    {
        // Windows HID interrupt writes use the descriptor's maximum output length.
        // CRC stays at the protocol packet boundary, before any transport padding.
        int length=device.GetMaxOutputReportLength();
        if(length>report.Length) Array.Resize(ref report,length);
        stream.Write(report);
    }

    private void SetStatus(string status) { lock(_gate) _status=status; }

    public void Dispose()
    {
        if(_cancel.IsCancellationRequested) return;
        _signal.Set(0,0,0,false);
        _cancel.Cancel();
        // Bounded HID read/write timeouts let the worker release hardware without blocking the UI.
        try { _worker?.Wait(1000); } catch(AggregateException) { }
    }
}
