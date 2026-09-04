# RO Axis Dimension Remover

Samodzielny `.exe` dla Tekla Structures 2025. Ma kasować nadmiarowe wymiary
"do osi" na profilach RO (rura okrągła) w widokach przekroju/detalu miejsc
łączenia.

## ⚠️ Stan: brama bezpieczeństwa ZNOWU ZAMKNIĘTA, `dryRun: true` (wieczór 2026-09-04)

**Przycisk w `MainForm.cs` NIE kasuje naprawdę - `dryRun: true`.** Ten sam
dzień: operator potwierdził wizualnie na `[35270]` i `[3.5013]`, `dryRun`
przełączono na `false`, a potem operator zgłosił, że realne kasowanie na
`[35270]` "usuwa całą szerokość albo całą długość" - więcej niż zamierzony
duplikat. `dryRun` wrócił na `true` od razu. Jeden nadzorowany test po tym
zgłoszeniu usunął TYLKO 12 mm (poprawnie) i nie odtworzył problemu - sprawa
jest NIEZDIAGNOZOWANA, patrz `CLAUDE.md`, sekcja "brama bezpieczeństwa" i
"PUŁAPKA 5". Nie przełączać na `false` bez pełnego, nowego przejścia bramy.

Tryb konsolowy (`--diag-active`/`--diag-mark`, `DiagRunner.cs`) **zostaje na
sztywno `dryRun: true` na zawsze** - to ścieżka wywoływana bez człowieka przy
przycisku (automatyzacja/agent AI) i nigdy nie powinna dostać możliwości
realnego kasowania, niezależnie od tego, jak dobrze zweryfikowana jest
reguła.

## Fakt wyjściowy

Tekla czasem auto-generuje wymiar prosty (`StraightDimension`), którego jeden
koniec siedzi na teoretycznej **osi** profilu RO (współrzędna promienia = 0)
zamiast na jego widocznej powierzchni (±promień). Dzieje się to typowo przy
skosie/ucięciu pod kątem - powierzchnia nie ma tam jednego punktu
odniesienia, więc Tekla łapie oś. Czasem taki wymiar jest **jedyny i
potrzebny** (opisuje długość profilu przy skosie), a czasem jest **duplikatem**
innego wymiaru opisującego to samo miejsce - i tylko duplikat ma zniknąć.

## Historia błędów (NIE powtarzać)

### v1: fałszywe trafienie na współrzędnej Z

Kryterium "koniec wymiaru ma współrzędną ≈0" sprawdzało **wszystkie**
współrzędne (Y i Z), łącznie z tą, która dla wymiaru leżącego płasko w
widoku jest **zawsze 0 dla obu końców** (bo widok jest dwuwymiarowy, nie
dlatego że to oś). To złapało prawidłowy wymiar "z boku rury" jako fałszywy
duplikat i skasowało go razem z właściwym celem, na `[35270]`.

**Poprawka:** sprawdzać tylko współrzędną, która **różni się** między
`StartPoint` i `EndPoint` tego samego wymiaru (patrz `TouchesAxis` w
`RoAxisDimensionService.cs`).

### v2: grupowanie po całym widoku

Po poprawce v1 program grupował wszystkie wymiary "dotykające osi" w
**całym widoku** i zostawiał tylko jeden (o największej wyświetlanej
wartości), kasując resztę. Zadziałało poprawnie na `[35270]` (jeden widok =
jedno złącze). Na `[3.5013]` ("Einzelteil Geländer", więcej złączy RO w
jednym rysunku ogólnym) **skasowało wszystkie wymiary do osi w widoku**,
łącznie z takimi należącymi do zupełnie innych, niepowiązanych złączy.

**Poprawka:** grupować po **bliskości geometrycznej** (`GroupByProximity`,
single-linkage, próg `SameJointDistanceMm = 300` mm), nie po przynależności
do widoku.

### v3: grupowanie po bliskości NADAL kasuje za dużo na [3.5013]

Po wdrożeniu grupowania po bliskości, `[3.5013]` **nadal traciło wszystkie**
wymiary do osi w teście operatora. Zdiagnozowane w v4 (niżej) przez odczyt
logu `[diag]`, nie przez zgadywanie.

### v4 (BIEŻĄCY STAN): zdiagnozowane i naprawione - do potwierdzenia w dry-run na obu rysunkach

Diagnoza z realnych współrzędnych na `[3.5013]` (headless `--diag-mark`,
patrz niżej): w jednym z widoków klaster bliskości miał 3 kandydatów, nie 2 -
oprócz dwóch prawdziwych duplikatów po 21 mm trafił tam też wymiar
**całkowitej długości profilu** (5796 mm, od `(0,0,0)` do wierzchołka skosu).
Ten wymiar też "dotyka osi" wg `TouchesAxis`, bo dla rury okrągłej lokalny
początek układu współrzędnych (punkt referencyjny X=0) leży dokładnie na
teoretycznej osi (Y=0, Z=0) - to definicja układu, nie ślepy skos bez
odniesienia powierzchni. `GroupByProximity` złączył go z dwoma prawdziwymi
duplikatami, bo patrzy tylko na najbliższy punkt (a wszystkie trzy dzielą
wierzchołek skosu), nie na cały wymiar. Reguła "zostaw największą wartość w
klastrze" zostawiała wtedy 5796 mm (przypadkiem poprawnie), ale kasowała
**oba** wymiary po 21 mm - stąd "traci wszystkie wymiary do osi".

**Ślepa uliczka po drodze:** pierwsza poprawka kasowała w klastrze tylko
wymiary o identycznej wyświetlanej wartości. Działała na `[3.5013]`, ale
**zepsuła `[35270]`** - tam prawdziwa para duplikat/oryginał to 24 mm i
12 mm (RÓŻNE wartości - jeden koniec dotyka powierzchni, drugi osi, więc
rzut wychodzi inny), więc wymóg równości wartości nie kasował niczego.
Wniosek: "różne wartości" samo w sobie NIC nie mówi o tym, czy dwa wymiary
są duplikatem - trzeba było innego kryterium.

**Poprawka, która działa na obu rysunkach:** dwuznaczność oś/powierzchnia
dotyczy z definicji tylko **krótkiego, lokalnego** wymiaru przy złączu -
wymiar całkowitej długości profilu nie ma tego problemu (jego punkty
referencyjne to płaskie przekroje, nie promień). Odfiltrowuje się więc z
kandydatów każdy wymiar "dotykający osi", którego WŁASNA długość
(`StartPoint`-`EndPoint`) przekracza `SameJointDistanceMm` (300 mm - ten sam
próg co przy grupowaniu, sensownie: "lokalne złącze" ma skalę promienia
profilu, nie metrów). Reguła "zostaw największą wartość w klastrze" wraca
bez zmian - **ona była poprawna**, brakowało tylko wykluczenia wymiaru,
który w ogóle nie powinien być kandydatem.

Zweryfikowane w dry-run (headless, patrz niżej) po zmianie:
- `[35270]`: kasuje dokładnie `12 mm`, zostawia `24 mm` **i** `2811 mm`
  (`2811 mm` teraz jawnie odfiltrowany jako "nie lokalny artefakt złącza") -
  zgodnie z oczekiwanym zachowaniem.
- `[3.5013]`: 2 widoki, każdy z klastrem 2 prawdziwych duplikatów po 21 mm -
  kasuje po jednym w każdym, `5796 mm` (całkowita długość) jawnie
  odfiltrowany i nietknięty w obu widokach.

**Potwierdzone wizualnie przez operatora w Tekli 2026-09-04** - na obu
rysunkach, patrząc na rzeczywisty rysunek (nie tylko dry-run + log): `[35270]`
poprawnie kasuje `12 mm` i zostawia `24 mm`/`2811 mm`, `[3.5013]` poprawnie
kasuje jeden `21 mm` w każdym z 2 widoków i zostawia `5796 mm`. `dryRun` w
`MainForm.cs` przełączony na `false` tego samego dnia - patrz "Stan" na
początku tego README.

**Headless diagnostyka bez GUI:** Claude Code (i każda automatyzacja) nie
klika w przycisk `MainForm`. `Program.cs` ma więc tryb konsolowy:
`RoAxisDimensionRemover.exe --diag-active` (aktywny rysunek) albo
`--diag-mark "[Mark]"` (otwiera rysunek po Mark, patrz `DiagRunner.cs`).
`dryRun` jest tam na sztywno `true` - nie da się tego przełączyć z linii
komend.

## Rysunki testowe

| Rysunek | Profil / opis | Status |
|---|---|---|
| `[35270]` | Einzelteil Geländer, RO Ø48,3 (promień 24,15) | ⚠️ Potwierdzone wizualnie 2026-09-04, ale POTEM zgłoszenie realnego kasowania "za dużo" na tym rysunku (niezdiagnozowane, jeden nadzorowany powtórz-test wyszedł poprawnie) - patrz `CLAUDE.md`, PUŁAPKA 5 |
| `[3.5013]` | Einzelteil Geländer, więcej złączy RO w jednym widoku | ✅ v4 potwierdzone wizualnie 2026-09-04: 2 widoki, po 1 duplikacie 21 mm skasowanym w każdym, `5796 mm` nietknięty - problem z `[35270]` nie zgłoszony tutaj |

## Znajdowanie kolejnych kandydatów bez klikania (historia - plik usunięty)

`Inspector.cs`, **usunięty po v0.2.0** (był rusztowaniem diagnostycznym,
zaplanowanym do usunięcia przed pierwszym pełnym wydaniem - patrz git
historia, jeśli trzeba go przywrócić): skanował **wszystkie** rysunki
pojedynczych części (`SinglePartDrawing`), filtrował po `Profile.ProfileString`
zaczynającym się od `"RO"`, i dla każdego robił "na sucho" sprawdzenie
(`SetActiveDrawing(d, false)` → `RemoveRedundantAxisDimensions(dryRun: true)`
→ `CloseActiveDrawing(false)`) bez otwierania na ekranie. Znaleziony kandydat
był otwierany od razu z tej samej referencji `Drawing` (bez ponownego,
wolnego szukania po `Mark`).

Na 1023 rysunkach pojedynczych części: 7 miało profil RO, jeden odrzucony
(`CannotPerformOperationDrawingNotUpToDateException` - rysunek nieaktualny,
wymaga `UpdateDrawing()` przed otwarciem, tak samo jak w Radius Dimension
Mover), `[3.5013]` był pierwszym trafieniem z realnym duplikatem (3
kandydatów). Ta rola jest teraz spełniona - obie znalezione wtedy drogi
([35270], [3.5013]) są potwierdzonymi rysunkami testowymi.

## Architektura

Kopiuje wzorzec z `Radius Dimention Mover` (ten sam katalog nadrzędny,
`..\CLAUDE.md` obowiązuje tu też):

| Plik | Zawartość |
|---|---|
| `RoAxisDimensionService.cs` | cała logika wykrywania i kasowania, zero UI |
| `MainForm.cs` | UI: jeden przycisk, log do okna i do pliku |
| `Program.cs` | punkt wejścia, w tym tryb konsolowy `--diag-active`/`--diag-mark` |
| `UpdateCheck.cs` | sprawdza w tle przy starcie, czy na GitHubie jest nowsza wersja |
| `TeklaWindowFocus.cs` | po realnym usunięciu przełącza fokus Windows na okno Tekli (Ctrl+Z od razu trafia tam, gdzie ma) |
| `DiagRunner.cs` | headless runner dry-run na jednym rysunku (patrz "Headless diagnostyka" wyżej) - **świadomie trwały element projektu**, `dryRun` na sztywno `true` na zawsze, patrz komentarz w tym pliku |

## Następne kroki

1. ~~Operator ma spojrzeć na `[35270]` i `[3.5013]` w Tekli po realnym
   uruchomieniu i potwierdzić, że zostają dokładnie te wymiary co w
   dry-run v4.~~ **Zrobione 2026-09-04** - patrz "Stan" na początku README.
2. ~~Przełączyć `dryRun: false` w `MainForm.cs`.~~ **Zrobione 2026-09-04.**
   `DiagRunner.cs` zostaje na sztywno `dryRun: true` na zawsze (patrz
   komentarz w tym pliku).
3. ~~Wydać nową wersję instalatora z realnym kasowaniem (nie pre-release).~~
   **Zrobione 2026-09-04** - [v0.2.0](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/releases/tag/v0.2.0).
4. ~~Usunąć `Inspector.cs` (albo zostawić jako świadomą część projektu).~~
   **Zrobione** - usunięty, był nieużywany od chwili znalezienia obu
   rysunków testowych. `DiagRunner.cs` zostaje świadomie (patrz wyżej).
5. **PUŁAPKA 5 (bieżący priorytet, `dryRun` znowu `true`)** - zdiagnozować
   zgłoszone "usuwa całą szerokość/długość" na `[35270]`, patrz `CLAUDE.md`.
   Brama musi przejść od nowa, zanim `dryRun` znowu wróci na `false`.
6. Rozważyć wiki (jak w Radius Dimention Mover) zamiast tego README, jeśli
   projekt urośnie.
