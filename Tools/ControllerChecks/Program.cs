using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using IronNebula;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

int checks=0;
void Check(bool value,string label) { checks++;if(!value) throw new Exception(label); }
byte[] Usb()
{
    byte[] bytes=new byte[64];bytes[0]=1;Array.Fill(bytes,(byte)128,1,4);bytes[8]=8;return bytes;
}
GamePadState Pad(params Buttons[] buttons)=>new(Vector2.Zero,Vector2.Zero,0,0,buttons);

// Independent reference computed with Python zlib, including the Sony seed byte.
Check(DualSenseProtocol.Crc(0xa2,Encoding.ASCII.GetBytes("123456789"))==0x63da9f12,"CRC reference");
var usb=Usb();
Check(DualSenseProtocol.TryInput(usb,out var state,out int battery),"USB input");
Check(state.IsConnected && state.ThumbSticks.Left==Vector2.Zero && state.ThumbSticks.Right==Vector2.Zero,"Centered sticks");
foreach(var button in new[]{Buttons.A,Buttons.B,Buttons.X,Buttons.Y,Buttons.Start,Buttons.Back})
    Check(!state.IsButtonDown(button),"Neutral button "+button);
Check(battery==5,"Battery midpoint");
usb[5]=255;usb[6]=255;usb[8]=0xf1;usb[9]=0xf3;usb[10]=2;
Check(DualSenseProtocol.TryInput(usb,out state,out _),"Combined input parse");
foreach(var button in new[]{Buttons.A,Buttons.B,Buttons.X,Buttons.Y,Buttons.LeftShoulder,Buttons.RightShoulder,
    Buttons.Back,Buttons.Start,Buttons.LeftStick,Buttons.RightStick,Buttons.DPadUp,Buttons.DPadRight})
    Check(state.IsButtonDown(button),"Sony button mapping "+button);
Check(state.Triggers.Left==1 && state.Triggers.Right==1,"Simultaneous L2/R2");
usb=Usb();usb[1]=255;usb[2]=0;
DualSenseProtocol.TryInput(usb,out state,out _);
Check(state.ThumbSticks.Left.X>0 && state.ThumbSticks.Left.Y>0 && state.ThumbSticks.Left.Length()<=1.001f,"Circular diagonal clamp");
for(int hat=0;hat<9;hat++)
{
    usb=Usb();usb[8]=(byte)hat;DualSenseProtocol.TryInput(usb,out state,out _);
    Check(state.IsButtonDown(Buttons.DPadUp)==(hat is 7 or 0 or 1),"Hat up "+hat);
    Check(state.IsButtonDown(Buttons.DPadRight)==(hat is 1 or 2 or 3),"Hat right "+hat);
    Check(state.IsButtonDown(Buttons.DPadDown)==(hat is 3 or 4 or 5),"Hat down "+hat);
    Check(state.IsButtonDown(Buttons.DPadLeft)==(hat is 5 or 6 or 7),"Hat left "+hat);
}
byte[] bt=new byte[78];bt[0]=0x31;Array.Fill(bt,(byte)128,2,4);bt[9]=8;
BinaryPrimitives.WriteUInt32LittleEndian(bt.AsSpan(74),0x34e3d165);
Check(DualSenseProtocol.TryInput(bt,out state,out _),"Bluetooth reference packet");
bt[9]^=1;Check(!DualSenseProtocol.TryInput(bt,out _,out _),"Reject corrupt BT input");
foreach(int length in new[]{0,1,10,63,77})
    Check(!DualSenseProtocol.TryInput(new byte[length],out _,out _),"Reject malformed input "+length);

for(int weapon=0;weapon<5;weapon++)
foreach(bool bluetooth in new[]{false,true})
{
    int p=bluetooth?3:1;
    var report=DualSenseProtocol.Effects(bluetooth,19,true,true,true,1,1,weapon,true,true,Color.Cyan);
    Check(report.Length==(bluetooth?78:63),"Output length");
    Check((report[p]&3)==0,"Audio haptics never enables emulated rumble");
    Check(report[p+10]!=5 && report[p+21]!=5,"Both adaptive triggers enabled");
    Check(report[p+44]==0 && report[p+45]==255 && report[p+46]==255,"Lightbar offsets");
    if(bluetooth)
    {
        Check(report[1]==0x30 && report[2]==0x10,"BT sequence wrap and tag");
        Check(BinaryPrimitives.ReadUInt32LittleEndian(report.AsSpan(74))==DualSenseProtocol.Crc(0xa2,report.AsSpan(0,74)),"BT output CRC");
    }
    report=DualSenseProtocol.Effects(bluetooth,0,false,true,false,1,1,weapon,true,true,Color.Black);
    Check(report[p+2]==0 && report[p+3]==0 && report[p+10]==5 && report[p+21]==5,"Stop clears actuators AND triggers");
    report=DualSenseProtocol.Effects(bluetooth,0,true,false,false,.5f,.25f,weapon,false,false,Color.Black);
    Check((report[p]&3)==3 && report[p+2]==63 && report[p+3]==127,"Rumble fallback channels");
    Check(report[p+10]==5 && report[p+21]==5,"Adaptive disabled independently");
}
var pcm=new sbyte[64];pcm[0]=-127;pcm[63]=127;
var audio=DualSenseProtocol.BluetoothAudio(31,42,pcm);
Check(audio.Length==142 && audio[0]==0x32 && audio[1]==0xf0,"BT haptic report");
Check(audio[10]==42 && audio[13]==129 && audio[76]==127,"Signed interleaved BT PCM");
Check(BinaryPrimitives.ReadUInt32LittleEndian(audio.AsSpan(138))==DualSenseProtocol.Crc(0xa2,audio.AsSpan(0,138)),"BT audio CRC");

var navigation=new ControllerNavigation();
navigation.Reset(Pad(Buttons.DPadDown));
for(int i=0;i<100;i++) { navigation.Update(Pad(Buttons.DPadDown),true,.02f);Check(!navigation[1],"Held reconnect navigation suppressed"); }
navigation.Update(Pad(),true,.02f);
navigation.Update(Pad(Buttons.DPadDown),true,.02f);Check(navigation[1],"Fresh menu press");
navigation.Update(Pad(Buttons.DPadDown),true,.2f);Check(!navigation[1],"Initial repeat delay");
navigation.Update(Pad(Buttons.DPadDown),true,.21f);Check(navigation[1],"First repeat");
navigation.Update(Pad(Buttons.DPadDown),true,.13f);Check(navigation[1],"Subsequent repeat");
navigation.Update(Pad(Buttons.DPadDown),false,.02f);Check(!navigation[1],"Inactive window suppressed");
navigation.Update(Pad(Buttons.DPadDown),true,.5f);Check(!navigation[1],"Focus regain needs release");
navigation.Update(default,true,.02f);Check(!navigation[1],"Disconnect navigation cleared");
navigation.Update(new GamePadState(new Vector2(.9f,0),Vector2.Zero,0,0,Array.Empty<Buttons>()),true,.02f);
Check(navigation[3],"Analog menu navigation");

var signal=new HapticSignal();signal.Set(1,1,3,true);
using var haptics=new DualSenseHaptics(signal);
var bytes=new byte[16*128];haptics.Read(bytes,0,bytes.Length);
float peak=0;
for(int i=0;i<bytes.Length;i+=16)
{
    Check(BitConverter.ToSingle(bytes,i)==0 && BitConverter.ToSingle(bytes,i+4)==0,"Speaker channels silent");
    peak=Math.Max(peak,Math.Abs(BitConverter.ToSingle(bytes,i+8)));
    Check(Math.Abs(BitConverter.ToSingle(bytes,i+8))<=.45f && Math.Abs(BitConverter.ToSingle(bytes,i+12))<=.45f,"Actuator amplitude bounded");
}
Check(peak>.1f,"Synthesizer produces actuator audio");
signal.Set(0,0,0,false);haptics.Read(bytes,0,bytes.Length);
Check(bytes.All(b=>b==0 || b==128),"Stopped samples are signed zero");
for(int i=0;i<bytes.Length;i+=4) Check(BitConverter.ToSingle(bytes,i)==0,"Stopped stream silent");
signal.Set(1,1,0,true);Thread.Sleep(270);
signal.Next(48000,out float left,out float right);Check(left==0 && right==0,"Stalled feedback watchdog");
var preferences=JsonSerializer.Deserialize<GuiPreferences.Settings>("{\"Vsync\":false}")!;
Check(preferences.ControllerHaptics && preferences.AdaptiveTriggers && preferences.ControllerStrength==65 && !preferences.BluetoothHaptics,"Existing settings get safe new defaults");
Console.WriteLine($"PASS: {checks} controller checks (synthetic packets/audio; no physical hardware).");
