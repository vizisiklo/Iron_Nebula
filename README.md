# IRON NEBULA

Xbox and native PS5 DualSense support: see [Controller support](CONTROLLER_SUPPORT.md)
for controls, adaptive triggers, USB haptics, and validation status. Bluetooth HD
haptics is an experimental option and is **disabled by default**.

**New: playable 2–6-player Online/LAN co-op.** Open **ONLINE / LAN CO-OP** in the
main menu, host a party, then share the displayed `address:port/roomcode` with guests.
See [Co-op setup and validation](COOP_SUPPORT.md) for controls, Internet connectivity
requirements and the first-alpha limitations. Smooth mouse flight and controller
icons remain supported. Co-op does not overwrite solo saves/high scores.

**Alpha 0.9.12 — Online Co-op** · Windows desktop · C# / .NET 8 / MonoGame WindowsDX

A saját fejlesztésű, vízszintesen scrollozó sci-fi shoot ’em up játék. Három szektor, öt váltható és fejleszthető primary weapon, minibossok és főbossok, szektorvégi shop, mentés/Continue, billentyűzet-, egér- és Xbox-kompatibilis kontrollerkezelés.

> **Validation:** Windows Release build and controller/loopback checks pass. A real
> game host plus five client processes exercised six ships, all weapons, all three
> sectors and a rendered guest. Physical multi-PC Internet/LAN testing and a full
> human campaign playthrough remain pending. See [details](COOP_SUPPORT.md).

## Indítás Windowson

1. Telepítsd a **.NET 8 SDK-t**.
2. A teljes forrásprojekt gyökeréből futtasd a `BUILD_WINDOWS.bat` fájlt.
3. A kiadáshoz használd a `PUBLISH_WINDOWS.bat` parancsfájlt.
4. A szükséges képi és audioasseteknek az `Assets/` mappában kell lenniük.

A projekt Windowsra készült; a fő projektfájl: `IronNebula.csproj`. A MonoGame-csomag a projektben rögzített verzióval áll vissza.

## Játékmenet

- **Sector 01 – Outer Frontier:** HD űrháttér, normál ellenségek, Warden miniboss, Sentinel főboss.
- **Sector 02 – The Rift:** lila–zöld űrvilág, homályos mélységi roncsok, Guardian miniboss és Colossus főboss.
- **Sector 03 – Inferno Belt:** három vörös–narancs pályaszakasz, új flotta, Inferno Guardian és Inferno Titan.
- **Primary weapons:** Pulse (alapfegyver), Lance, Arc, Gatling, Beam; fegyverfejlesztés 1–7 szintig. Az új fegyvereket kreditekért kell feloldani.
- **Gazdaság:** kreditpickupok, szektorvégi eredményösszesítő, három shopkategória, hajó- és fegyverfejlesztések; Sector 01 kreditbevételének plafonja 900 CR.
- **Nehézség:** Easy / Normal / Hard; New Game indításakor választható.
- **Kijelző:** nagy betűk, F1 Help, F10 diagnosztika, testreszabható SFX/music hangerő.
- **Megjelenítés:** 16:9 / 16:10, 720p–4K választható módok, MSAA, lövedékglow, találat utáni rövid blur/rázkódás, ablakméret monitorhoz illesztése.
- **Irányítás:** billentyűzet, egér (hajó közvetlenül a kurzornál), kontroller; a menük egérrel is használhatók.

## Projektstruktúra

```text
Source/             C# játéklogika, UI, mentés, audio, grafika
Assets/HD/          űrhajók, hátterek, sprite-ok
Assets/Rift/        Rift-szektor, DOF drifters
Assets/Inferno/     Inferno-szektor, hajók és bossok
Assets/FX/          sérülés, robbanás, fény- és harci effektek
Assets/UI/          HUD és menüelemek
Assets/Audio/       zene és hanghatások
Tools/              ellenőrző segédprogramok
BUILD_WINDOWS.bat   Windows forrásfordítás
PUBLISH_WINDOWS.bat Windows publikálás
CHANGELOG.md        változástörténet
ROADMAP.md          további terv
DEVELOPMENT_HISTORY.md teljes fejlesztési idővonal
PATCH_HISTORY.md    rekonstruált patchek sorrendje
```

## Fontos megjegyzés a zenei jogokról

A *Propulsive Peak* felhasználó által biztosított hangsáv. **A nyilvános GitHub-repositoryba csak megfelelő terjesztési engedéllyel tölthető fel.** A helyi teljes archívum tartalmazza a felhasznált MP3-at és a streamelt WAV-ot; a nyilvános repóhoz mellékelt feltöltőútmutató ezeket alapértelmezetten kizárja a Git-commitból. Az egyéb grafikai assetek terjesztési joga is ellenőrizendő nyilvános kiadás előtt.

## Dokumentáció

- [Teljes változástörténet](CHANGELOG.md)
- [Mérföldkövek és történet](DEVELOPMENT_HISTORY.md)
- [Roadmap](ROADMAP.md)
- [0.9.11.2 release notes](RELEASE_NOTES_0.9.11.2.md)
- [GitHub feltöltési útmutató](GITHUB_UPLOAD_HU.md)

A későbbi fejlesztői kiadásokat továbbra is **csak változott/új fájlokat tartalmazó PATCH ZIP** formában készítjük el, megőrzött könyvtárszerkezettel.
