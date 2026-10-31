using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace IronNebula;

/// <summary>Event-driven actuator audio. Speaker/headphone channels always contain silence.</summary>
internal sealed class DualSenseHaptics : IWaveProvider, IDisposable
{
    private WasapiOut? _output;
    private MMDevice? _device;
    private readonly HapticSignal _signal;
    internal bool Ready => _output?.PlaybackState==PlaybackState.Playing;
    public WaveFormat WaveFormat { get; }=new WaveFormatExtensible(48000,32,4);

    internal DualSenseHaptics(HapticSignal signal) => _signal=signal;

    internal bool TryStart()
    {
        try
        {
            Dispose();
            using var enumerator=new MMDeviceEnumerator();
            var candidates=new List<MMDevice>();
            foreach(var endpoint in enumerator.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active))
            {
                string name=endpoint.FriendlyName;
                if((name.Contains("DualSense",StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Wireless Controller",StringComparison.OrdinalIgnoreCase)) &&
                    endpoint.AudioClient.MixFormat.Channels==4) candidates.Add(endpoint);
                else endpoint.Dispose();
            }
            // Ambiguous devices must never redirect actuator audio to another controller.
            if(candidates.Count!=1) { foreach(var item in candidates) item.Dispose();return false; }
            _device=candidates[0];
            _output=new WasapiOut(_device,AudioClientShareMode.Shared,true,30);
            _output.Init(this);_output.Play();
            return Ready;
        }
        catch(Exception ex) when(ex is not OutOfMemoryException)
        { Dispose();return false; }
    }

    public int Read(byte[] buffer,int offset,int count)
    {
        Array.Clear(buffer,offset,count);
        for(int i=0;i+16<=count;i+=16)
        {
            _signal.Next(48000,out float left,out float right);
            BitConverter.TryWriteBytes(buffer.AsSpan(offset+i+8,4),left);
            BitConverter.TryWriteBytes(buffer.AsSpan(offset+i+12,4),right);
        }
        return count;
    }

    public void Dispose()
    {
        var output=_output;_output=null;
        try { output?.Stop(); } finally { output?.Dispose();_device?.Dispose();_device=null; }
    }
}

/// <summary>Shared synthesis for USB 48 kHz and experimental Bluetooth 3 kHz.</summary>
internal sealed class HapticSignal
{
    private readonly object _gate=new();
    private float _low,_high;
    private int _weapon;
    private bool _firing;
    private long _updated;
    private double _phase;

    internal void Set(float low,float high,int weapon,bool firing)
    {
        lock(_gate)
        {
            _low=Math.Clamp(low,0,1);_high=Math.Clamp(high,0,1);
            _weapon=weapon;_firing=firing;_updated=Environment.TickCount64;
        }
    }

    internal void Next(int rate,out float left,out float right)
    {
        lock(_gate)
        {
            // A stalled frame loop must not leave a continuous effect running.
            if(Environment.TickCount64-_updated>250) { left=right=0;return; }
            double hz=_weapon switch { 1=>75,2=>145,3=>95,4=>185,_=>115 };
            double texture=_firing?.75+.25*Math.Sin(_phase*2*Math.PI*23):1;
            left=(float)(Math.Sin(_phase*2*Math.PI*hz)*_low*.45*texture);
            right=(float)(Math.Sin(_phase*2*Math.PI*(hz+45))*_high*.45*texture);
            _phase+=1d/rate;
            if(_phase>=10) _phase-=10;
        }
    }
}
