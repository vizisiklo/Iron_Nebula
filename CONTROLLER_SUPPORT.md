# Xbox and PlayStation 5 controller support

Windows controllers exposed through XInput are supported in all four player slots.
The game retains the active controller until it disconnects. Buttons held while
connecting do not immediately confirm a menu or toggle pause.

| Control | Action |
| --- | --- |
| Left or right stick | Analog flight (the stick with greater deflection wins) |
| D-pad | Flight / menu navigation |
| Left stick in menus | Navigate; hold to repeat after 0.4 seconds, every 0.12 seconds |
| RT / X | Fire |
| LT | Precision movement |
| LB / RB | Previous / next weapon |
| A / B | Confirm / back |
| Menu / Start | Pause / resume; continue from shop |
| View / Back | Help |
| Y | Settings |
| RB in settings | Switch settings tab |
| X on game over | Resume unlocked checkpoint |

Keyboard and mouse remain supported. Losing window focus pauses flight and stops
weapon audio and vibration. Resume explicitly after returning to the window.

Menu hints automatically follow the selected controller: colored Xbox face-button
icons or DualSense Cross/Circle/Square/Triangle and shoulder/system-button icons.
This covers title, pause, shop, difficulty, help, settings, resolution selection,
and game-over prompts. Disconnecting all controllers restores keyboard labels.
The icons are drawn by the native renderer and require no extra image/font assets.

For visual verification with the game's real renderer, run
`dotnet run --project Tools/GlyphPreview/GlyphPreview.csproj -c Release -- Assets/ui_atlas.png bin/controller-glyphs.png`.
This exports all 24 icons and Xbox/DualSense menu examples; it does not emulate a
physical controller or validate device recognition.
Mouse flight follows the visible cursor through a short damped spring rather than
teleporting. Precision mode softens the follow response. Even small stick movement
can take over flight, and a stationary mouse cannot override a controller menu selection.

## Native DualSense

DualSense (Sony USB vendor `054c`, product `0ce6`) and the shared controls on
DualSense Edge (`0df2`) use native HID input over USB or Bluetooth. No virtual
Xbox driver is required. A native DualSense takes priority over XInput while
connected; XInput is scanned again when it disconnects. Edge paddles, gyro aiming,
microphone, and speaker playback do not have gameplay bindings in this change.

| DualSense control | Action |
| --- | --- |
| Left / right stick, D-pad | Flight; left stick / D-pad navigate menus |
| R2 / Square | Fire; Square on game over resumes an unlocked checkpoint |
| L2 | Precision movement |
| L1 / R1 | Previous / next weapon; R1 changes settings page |
| Cross / Circle | Confirm / back |
| Options | Pause / resume; continue from shop |
| Create / touchpad click | Help |
| Triangle | Settings |

Both triggers release in menus, on pause, on focus loss, on shutdown, and on a
feedback timeout. L2 has light resistance for precision control. R2 has five
weapon profiles: Pulse resistance, Lance break point, Arc texture, Gatling rapid
vibration, and Beam continuous texture. Effects are moderate, with no full lock.
The lightbar shows the weapon color, blue with shields, or red at critical hull.
The connection indicator reports native transport, battery, and haptics status.

### Haptics and settings

Open **Settings > Controllers** using mouse, keyboard, or controller. Settings
persist alongside existing display preferences and preserve older settings files.

| Option | Default | Behavior |
| --- | --- | --- |
| Haptics / Xbox vibration | On | Enables DualSense feedback or Xbox vibration |
| Haptic strength | 65% | Scales actuator audio / rumble; separate from trigger resistance |
| DualSense adaptive L2 / R2 | On | Independent trigger resistance toggle |
| Bluetooth HD haptics (experimental) | **Off** | Opt-in 3 kHz signed stereo PCM HID stream |

USB HD haptics uses a uniquely identified, active, four-channel controller audio
endpoint through WASAPI. Channels 1/2 stay silent; channels 3/4 carry synthesized
weapon/impact textures. It does not capture system audio or change the default
Windows sound device, microphone, or master volume. If the endpoint is missing,
disabled, ambiguous, or cannot start, feedback falls back to compatible rumble.
If audio playback stops, the connection status and fallback follow that state.

Bluetooth defaults to compatible rumble plus adaptive triggers. Experimental HD
haptics uses report `0x32`; it depends on controller firmware and Windows HID
behavior, has lower fidelity than USB, and may have scheduling jitter. Disable it
to return to rumble. Compatible rumble and audio haptics are never enabled at the
same time. Feedback has a 250 ms watchdog if the game stops updating.

Steam Input, DS4Windows, DSX, or device-hiding tools may intercept or overwrite
native controller reports. For native DualSense testing, avoid having multiple
applications drive the physical controller at the same time. Their presence is
not automatically diagnosed as a conflict by this game.

### Protocol references

This implementation uses the published report layouts documented in
[SDL's PS5 driver](https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_ps5.c)
and [DualSense native protocol notes](https://github.com/Lelisvaldo/godot-dualsense-native/blob/main/docs/PROTOCOL.md).
It uses HidSharp 2.1.0 for HID I/O and NAudio.Wasapi 2.2.1 for USB actuator audio.
No external controller utility, driver installer, or local AI service is launched.

## Hardware verification

Build validation does not verify a physical controller. Check USB and wireless
connections, all four XInput slots, proportional movement, simultaneous RT/LT,
weapon cycling, menu repeat, checkpoint resume, vibration, focus loss, and
disconnect/reconnect with A or Start held. Verify that keyboard/mouse still work.

Run `dotnet run --project Tools/ControllerChecks/ControllerChecks.csproj -c Release`
for synthetic packet, CRC, menu-repeat, settings compatibility, trigger-release,
watchdog, and audio-channel checks. These do not open physical controllers.

Also verify DualSense USB and Bluetooth separately: Cross/Circle/Square/Triangle,
simultaneous L2/R2, all five weapon profiles, adaptive-off and haptics-off,
experimental Bluetooth HD on/off, USB endpoint unavailable, controller switching,
focus loss, battery display, and effect shutdown. Inspect the three settings tabs
with keyboard, mouse, Xbox, and DualSense. Physical hardware validation is pending.
