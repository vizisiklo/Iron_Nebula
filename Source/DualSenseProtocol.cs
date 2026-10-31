using System.Buffers.Binary;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace IronNebula;

/// <summary>DualSense USB/BT wire format. No device I/O; also used by packet regression checks.</summary>
internal static class DualSenseProtocol
{
    internal static uint Crc(byte seed, ReadOnlySpan<byte> data)
    {
        uint crc=0xffffffff;
        crc=Step(crc,seed);
        foreach(byte value in data) crc=Step(crc,value);
        return ~crc;
    }

    private static uint Step(uint crc,byte value)
    {
        crc^=value;
        for(int i=0;i<8;i++) crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);
        return crc;
    }

    internal static bool TryInput(ReadOnlySpan<byte> report,out GamePadState state,out int battery)
    {
        state=default; battery=-1;
        bool bluetooth=report.Length>=78 && report[0]==0x31;
        bool usb=report.Length==64 && report[0]==0x01;
        if(!bluetooth && !usb) return false;
        if(bluetooth && BinaryPrimitives.ReadUInt32LittleEndian(report.Slice(74,4))!=Crc(0xa1,report[..74])) return false;
        int p=bluetooth?2:1;
        byte face=report[p+7],buttons=report[p+8],extra=report[p+9];
        var pressed=new List<Buttons>();
        void Add(bool down,Buttons button) { if(down) pressed.Add(button); }
        Add((face&0x10)!=0,Buttons.X); Add((face&0x20)!=0,Buttons.A);
        Add((face&0x40)!=0,Buttons.B); Add((face&0x80)!=0,Buttons.Y);
        int hat=face&15;
        Add(hat is 7 or 0 or 1,Buttons.DPadUp); Add(hat is 1 or 2 or 3,Buttons.DPadRight);
        Add(hat is 3 or 4 or 5,Buttons.DPadDown); Add(hat is 5 or 6 or 7,Buttons.DPadLeft);
        Add((buttons&1)!=0,Buttons.LeftShoulder); Add((buttons&2)!=0,Buttons.RightShoulder);
        Add((buttons&0x10)!=0 || (extra&2)!=0,Buttons.Back); // Create or touchpad click opens help.
        Add((buttons&0x20)!=0,Buttons.Start);
        Add((buttons&0x40)!=0,Buttons.LeftStick); Add((buttons&0x80)!=0,Buttons.RightStick);
        state=new GamePadState(Stick(report[p],report[p+1]),Stick(report[p+2],report[p+3]),
            report[p+4]/255f,report[p+5]/255f,pressed.ToArray());
        battery=Math.Min(100,(report[p+52]&15)*10+5);
        return true;
    }

    private static Vector2 Stick(byte x,byte y)
    {
        float Axis(byte b) => b>=128?(b-128)/127f:(b-128)/128f;
        Vector2 value=new(Axis(x),-Axis(y));
        float length=value.Length();
        return length<=.12f?Vector2.Zero:value/length*Math.Clamp((length-.12f)/.88f,0f,1f);
    }

    internal static byte[] Effects(bool bluetooth,byte sequence,bool live,bool adaptive,bool audioHaptics,
        float low,float high,int weapon,bool firing,bool precision,Color color,bool resetLightbar=false)
    {
        byte[] report=new byte[bluetooth?78:63];
        report[0]=bluetooth?(byte)0x31:(byte)0x02;
        int p=bluetooth?3:1;
        if(bluetooth) { report[1]=(byte)((sequence&15)<<4);report[2]=0x10; }
        report[p]=0x0c; // Independently update both triggers, including explicit release.
        report[p+1]=0x14; // Lightbar and player indicator only; leave audio routing/mic alone.
        if(resetLightbar) report[p+1]|=8;
        if(live && !audioHaptics)
        {
            report[p]|=3; // Compatible vibration, never enable alongside audio haptics.
            report[p+2]=(byte)(Math.Clamp(high,0,1)*255);
            report[p+3]=(byte)(Math.Clamp(low,0,1)*255);
        }
        // Selecting haptics without compatible vibration also clears an earlier rumble.
        if(!live) report[p]|=2;
        Trigger(report.AsSpan(p+10,11),live && adaptive,weapon,firing,false);
        Trigger(report.AsSpan(p+21,11),live && adaptive,weapon,precision,true);
        report[p+43]=0x04;
        report[p+44]=color.R;report[p+45]=color.G;report[p+46]=color.B;
        if(bluetooth) Seal(report,0xa2);
        return report;
    }

    private static void Trigger(Span<byte> effect,bool enabled,int weapon,bool active,bool left)
    {
        effect.Clear(); effect[0]=0x05;
        if(!enabled) return;
        if(left) { Resistance(effect,2,active?3:1);return; }
        // Five weapons, five distinct breakpoints/textures; no full-strength lock.
        if(weapon==1) // Lance: pronounced resistance followed by a release point.
        {
            effect[0]=0x25;BinaryPrimitives.WriteUInt16LittleEndian(effect[1..],(1<<3)|(1<<7));effect[3]=4;
        }
        else if(active && weapon is 2 or 3 or 4)
        {
            effect[0]=0x26;
            Zones(effect,2,weapon==3?3:2);
            effect[9]=(byte)(weapon==3?28:weapon==4?55:16);
        }
        else Resistance(effect,weapon==0?2:3,weapon==0?2:3);
    }

    private static void Resistance(Span<byte> effect,int start,int force)
    { effect[0]=0x21;Zones(effect,start,force); }

    private static void Zones(Span<byte> effect,int start,int force)
    {
        ushort mask=0;uint forces=0;
        for(int zone=start;zone<10;zone++) { mask|=(ushort)(1<<zone);forces|=(uint)(force-1)<<(zone*3); }
        BinaryPrimitives.WriteUInt16LittleEndian(effect[1..],mask);
        BinaryPrimitives.WriteUInt32LittleEndian(effect[3..],forces);
    }

    internal static byte[] BluetoothAudio(byte sequence,byte counter,ReadOnlySpan<sbyte> samples)
    {
        if(samples.Length!=64) throw new ArgumentException("Expected 32 stereo frames.",nameof(samples));
        byte[] report=new byte[142];
        report[0]=0x32;report[1]=(byte)((sequence&15)<<4);
        report[2]=0x91;report[3]=7;report[4]=0xfe;report[9]=0xff;report[10]=counter;
        report[11]=0x92;report[12]=64;
        for(int i=0;i<64;i++) report[13+i]=unchecked((byte)samples[i]);
        Seal(report,0xa2);
        return report;
    }

    private static void Seal(Span<byte> report,byte seed)
        =>BinaryPrimitives.WriteUInt32LittleEndian(report[^4..],Crc(seed,report[..^4]));
}
