# IRON NEBULA – Teljes CHANGELOG

A projekthez tartozó kiadástörténet 2026. október 28-i állapot szerint. A felsorolt Alpha változatok a fejlesztés történeti mérföldkövei; a csomag a **0.9.11.2** állapotot reprezentálja. A fejlesztések funkcionális összefoglalói alapján készült, nem külön-külön Git-commitból rekonstruált történet.

## Alpha 0.9.12 - Online Co-op

- Add a playable 2–6-player host-authoritative Online/LAN mode with lobby, IPv4/DNS
  connection string, temporary room code and a six-player admission limit.
- Replicate ships, weapons, beams, drones, enemies, projectiles, hazards, pickups,
  team credits, shop state, boss phases and sector transitions from the host.
- Add independent player lives/damage, party colors/HUD, party-scaled enemy hull,
  local pause/settings, all-ready shop progression and clean disconnect behavior.
- Preserve solo saves/high scores, smooth mouse flight, Xbox/DualSense support,
  adaptive triggers, default-off experimental Bluetooth haptics and button icons.
- Add real-loopback transport checks and a six-process game/render smoke utility.
  Direct Internet reachability, multi-PC performance and a full human campaign
  remain unverified. Mid-run reconnect, host migration and co-op save/resume are
  not part of this initial playable alpha. See [setup](COOP_SUPPORT.md).

## Alpha 0.9.11.2 – Direct Mouse Flight
- Az egérmutató pozíciója közvetlenül meghatározza a hajó helyét, követési késleltetés nélkül.
- Egérkurzor elrejtése a játékban; menükben megmarad. Bal gomb: tüzelés. Billentyűzet és kontroller megmaradt.

## Alpha 0.9.11.1 – Build Fix
- `pos` lokális változók hatókörütközésének javítása a shop kezelőjében.

## Alpha 0.9.11 – Mouse & Resolution
- Egérnavigáció főmenüben, shopban, helpben és Settingsben; egérgörgős termékválasztás.
- Külön Aspect Ratio (16:9/16:10) és legördülő Resolution; Enter/kattintás kiválasztás, Esc megszakítás.
- Ablakos módban a felbontás a monitor méretéhez illeszkedik; az elmentett logikai felbontás megmarad.
- MSAA módosítása nem írja felül a felbontást.

## Alpha 0.9.10 – Resolution, Glow & Hit FX
- Felbontásválasztó 720p–4K-ig, 16:9 és 16:10 módokkal.
- Ellenséges lövedékek glow erőssége 0–100%; találatkor képrázkódás és 0,5 másodperces blur, külön erősséggel.
- MSAA Off/2×/4×/8× beállítás; kép aránytartó megjelenítése.

## Alpha 0.9.9 – Combat & Audio Overhaul (0.9.8 + 0.9.9)
- New Game után külön Easy/Normal/Hard választó; Settingsben `Difficulty` felirat.
- Hat boss új találati, páncélsérülési és fázisváltási grafikai visszajelzéssel.
- Öt új boss-/szektorhang; háttérzene átvezetése menü és harc között.

## Alpha 0.9.7 – HD Fleet, Stability & Difficulty (0.9.6 + 0.9.7)
- A korábbi egyszerű hajó- és bossgrafikák HD PNG-kre cserélése mindhárom szektorban.
- Warden, Sentinel, Rift Guardian, Rift Colossus, Inferno Guardian és Inferno Titan új sprite-okkal és modulokkal.
- F10 fejlesztői HUD: FPS, lövedék-, enemy- és effektobjektum-számlálók.
- Lövedék-/ellenséglisták védelme és maximum 1100 aktív lövedék; crash.log fájl.
- Easy/Normal/Hard: HP, lövedéksebesség, támadási ütem, score multiplikátorok.

## Alpha 0.9.5.1 – Build Fix
- `PickupKind.Repair` összehasonlítás hiányából eredő `||` C# fordítási hiba javítása.
- Elavult `Enemy.Entered` mező eltávolítása.

## Alpha 0.9.5 – Economy & Progression 3.0
- Weapon Power és Engine Tuning kétszintű, menthető shopfejlesztések.
- Második/harmadik szektor ritkább, nagyobb értékű kreditlootja.
- Szektoronkénti teljesítményrang (C/B/A/S) és jutalmazás; kreditkorlátok: 900/3000/5300 CR.

## Alpha 0.9.4 – Inferno Bosses
- Inferno Guardian miniboss két fegyvermodullal.
- Inferno Titan főboss négy modullal, három harci fázissal; szektorvégi eredményösszesítés.

## Alpha 0.9.3 – Sector 03: Inferno Belt
- Három vörös–narancs szakasz: Outer Belt, Broken Fleet, Inferno Core.
- Hat új normál ellenségtípus, nyolc hullám-/támadási forma, DOF roncsok, meteoritok.
- Rift shop utáni belépés a harmadik szektorba; checkpoint/Continue és F5 teszt.

## Alpha 0.9.2.6 – Rift DOF Rebuild
- Elmosott, foltos/halós DOF sprite-ok helyett az eredeti részletes űrroncsok újrafeldolgozása.
- Méret/objektumszám visszafogása; Rift DOF az F6-tal kapcsolható.

## Alpha 0.9.2.5 – Rift DOF Edge Fix (felülírva)
- Erősebb blur próbálkozás, amely a visszajelzés szerint rosszabb megjelenést adott; a 0.9.2.6 kiváltotta.

## Alpha 0.9.2.4 – Enemy HP & Rift DOF
- Kisméretű enemy HP-barok láthatóságának javítása; HP-arányos színezés.
- Új mozgó Rift-roncs/aszteroida DOF sprite-ok.

## Alpha 0.9.2.3 – Real Font UI
- Valódi, szabadon használható betűtípusból készített atlasz, arányos karakterléptetéssel; javított szövegigazítás.
- Az elfogadott stabil tipográfiai alap; a későbbi verziók ezt megtartják.

## Alpha 0.9.2.2 / 0.9.2.2a – Font, Number & XML Fix
- Ezreselválasztó `?` megjelenítési hiba javítása, betűatlasz korrigálása.
- XML `&` karakter hibájának javítása a `.csproj` fájlban.

## Alpha 0.9.2.1 – Readable GUI Rebuild
- Nagyobb címek, új fontatlasz, három nagy shopkártya, négyoldalas F1 help, jobban elhelyezett Settings.

## Alpha 0.9.2 – Save, Controls & Cinematic GUI (0.9.1 + 0.9.2)
- Tartós mentés, Continue, checkpoint/shopállapot megőrzése.
- Billentyűk átállítása és mentése, Xbox-kompatibilis kontroller, GUI áttervezése.

## Alpha 0.9.0 – Shop & Main Menu Overhaul (0.8.4 + 0.9.0)
- Animáltabb főmenü, pause, Settings, Controls/Help.
- Weapons / Ship Upgrades / Support shopkategóriák, erősebb kijelölés, nagyobb feliratok.

## Alpha 0.8.2.1 – Audio Build Fix
- Hiányzó NAudio-függőség eltávolítása, MonoGame-alapú PCM/WAV streaming.
- Az eredeti zene és hangeffektfájlok megőrzése.

## Alpha 0.8.2 – Audio, Help & Settings
- Felhasználó által adott *Propulsive Peak* zene, első komplett hangeffektrendszer.
- F1 billentyűsúgó, F12/O zene- és SFX-hangerő (0–100%) és mentés.

## Alpha 0.8.1.2 – Rift Boss Projectile Fix
- Boss megsemmisülés közbeni lövedéklistatörlésből adódó `RemoveAt` indexhiba javítása.

## Alpha 0.8.1.1 – Shop Crash Fix
- A Beam shopikon hiányzó textúrája miatt fellépő MonoGame `ArgumentNullException` javítása.

## Alpha 0.8.1 – Rift Bosses & Shop 2.0 (0.7.9 + 0.8.0 + 0.8.1)
- Rift Guardian, Rift Colossus és lelőhető fegyvermodulok, többfázisú boss-harcok.
- Pajzs-, hull-, drón-, pickupmágnes-fejlesztések és pajzsfeltöltés shopban.

## Alpha 0.7.8 – Shop Economy & Clean UI
- Első szektor kreditplafonja 900 CR, első fizetős fegyver elérhető, többiek nem.
- Sárga figyelmeztető szövegek és pickupok zavaró kereteinek eltávolítása.

## Alpha 0.7.7 – Credits & Sector Shop
- Kreditpickupok, szektorvégi pont/lootösszesítő és shop.
- Pulse mint egyetlen kezdőfegyver, Lance/Arc/Gatling/Beam vásárolható feloldás, plusz élet.

## Alpha 0.7.6 – Enemy Readability
- HD enemy körvonalak, lövedékek és HP-kijelzők kontrasztjának javítása.

## Alpha 0.7.5 – Rift Background Rework
- Élesebb, letisztult lila–zöld háttér, egy domináns távoli állomás, karcsúbb felső HUD.

## Alpha 0.7.4 – Clean Rift & Compact HUD
- Jóváhagyott egyszerűbb háttér beépítése; csak felső cockpit HUD.

## Alpha 0.7.3 – Rift Readability
- A zsúfolt háttér és az alsó/oldalsó HUD csökkentése, kevesebb DOF takarás.

## Alpha 0.7.0 / 0.7.1 – Sector 02: The Rift
- Második szektor, idegen flotta, új formációk, környezeti akadályok, parallax hátterek és DOF.

## Alpha 0.6.x – Primary Weapon Framework
- Elsődleges fegyverek és 1–7 fejlesztési szint, egy pickup egy fejlesztési lépés.
- Pulse, Lance, Arc; gyors Gatling hullámzó szórással és folyamatos Beam szélesedő nyalábbal.
- Pickupgrafikák, kisebb látványosabb shield.

## Alpha 0.5.x – Visual & FX
- A hajó helyes tengely körüli döntése; stabil középpont, ugrálás megszüntetése.
- Külön PNG hajósprite-ok, HD effektek, füst sérülésfüggő intenzitással.
- Átlátszó robbanásképek javítása, több mélységi háttérréteg.

## Alpha 0.4.x – HD Sprite Overhaul
- Renderelt HD hajók, animált hajtómű, asteroid sprite-ok és parallax háttér.
- Sprite-középpont és mozgási/jelenetillesztési javítások.

## Alpha 0.3 – Sector One
- Outer Frontier szektor, hullámok, Warden miniboss és Sentinel főboss, checkpoint és high score.

## Alpha 0.2 – Combat Upgrade
- Mozgás, pajzs, drón, pickup, enemy formációk, többfázisú boss, találatkezelés.

## Alpha 0.1 – First Windows Prototype
- C#/.NET 8 + MonoGame WindowsDX alap, Katakis-szerű horizontális shooter.
- Korai MonoGame projekt- és framework-referenciahibák javítása; működő első prototípus.
