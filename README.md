# RO Axis Dimension Remover

Samodzielny `.exe` dla Tekla Structures 2025. Ma kasować nadmiarowe wymiary
"do osi" na profilach RO (rura okrągła) w widokach przekroju/detalu miejsc
łączenia.

## ⚠️ STAN: WIP, NIEBEZPIECZNE DO UŻYCIA NA ŻYWO

**Przycisk w `MainForm.cs` jest celowo przełączony na `dryRun: true`.**
Program **NIE kasuje** wymiarów, tylko loguje, co by skasował. Reguła
wykrywania nadal jest w trakcie ustalania - dwie kolejne wersje algorytmu
realnie skasowały złe wymiary na żywym modelu (patrz niżej), zanim ktoś to
zauważył. **Nie przełączać z powrotem na `dryRun: false`, dopóki reguła nie
przejdzie testu na `[3.5013]` bez utraty żadnego potrzebnego wymiaru.**

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

**Nadal NIE potwierdzone wizualnie przez operatora w Tekli** (tylko dry-run
+ odczyt współrzędnych z logu). Nie przełączaj `dryRun` na `false`, dopóki
ktoś nie spojrzy na oba rysunki po realnym uruchomieniu i nie potwierdzi, że
nic wartościowego nie zniknęło.

**Headless diagnostyka bez GUI:** Claude Code (i każda automatyzacja) nie
klika w przycisk `MainForm`. `Program.cs` ma więc tryb konsolowy:
`RoAxisDimensionRemover.exe --diag-active` (aktywny rysunek) albo
`--diag-mark "[Mark]"` (otwiera rysunek po Mark, patrz `DiagRunner.cs`).
`dryRun` jest tam na sztywno `true` - nie da się tego przełączyć z linii
komend.

## Rysunki testowe

| Rysunek | Profil / opis | Status |
|---|---|---|
| `[35270]` | Einzelteil Geländer, RO Ø48,3 (promień 24,15) | ⚠️ v4 w dry-run: kasuje tylko `12`, zostawia `24` i `2811` (zgodnie z oczekiwaniem) - operator jeszcze nie potwierdził wizualnie po v4 |
| `[3.5013]` | Einzelteil Geländer, więcej złączy RO w jednym widoku | ⚠️ v4 w dry-run: 2 widoki, po 1 realnym duplikacie 21 mm w każdym, `5796 mm` nietknięty - operator jeszcze nie potwierdził wizualnie |

## Znajdowanie kolejnych kandydatów bez klikania

`Inspector.cs` (rusztowanie diagnostyczne, **do usunięcia przed pierwszym
"prawdziwym" wydaniem**, na razie zostawione bo działa i jest przydatne):
skanuje **wszystkie** rysunki pojedynczych części (`SinglePartDrawing`),
filtruje po `Profile.ProfileString` zaczynającym się od `"RO"`, i dla
każdego robi "na sucho" sprawdzenie (`SetActiveDrawing(d, false)` →
`RemoveRedundantAxisDimensions(dryRun: true)` → `CloseActiveDrawing(false)`)
bez otwierania na ekranie. Znaleziony kandydat jest otwierany od razu z tej
samej referencji `Drawing` (bez ponownego, wolnego szukania po `Mark`).

Na 1023 rysunkach pojedynczych części: 7 miało profil RO, jeden odrzucony
(`CannotPerformOperationDrawingNotUpToDateException` - rysunek nieaktualny,
wymaga `UpdateDrawing()` przed otwarciem, tak samo jak w Radius Dimension
Mover), `[3.5013]` był pierwszym trafieniem z realnym duplikatem (3
kandydatów).

Wywołanie tymczasowo wyłączone w `Program.cs` (`Inspector.FindAnotherCandidate`
zakomentowane/usunięte z `Main()`) - dopisz z powrotem, jeśli potrzebny kolejny
kandydat.

## Architektura

Kopiuje wzorzec z `Radius Dimention Mover` (ten sam katalog nadrzędny,
`..\CLAUDE.md` obowiązuje tu też):

| Plik | Zawartość |
|---|---|
| `RoAxisDimensionService.cs` | cała logika wykrywania i kasowania, zero UI |
| `MainForm.cs` | UI: jeden przycisk, log do okna i do pliku |
| `Program.cs` | punkt wejścia, w tym tryb konsolowy `--diag-active`/`--diag-mark` |
| `Inspector.cs` | tymczasowy skaner kandydatów po całym modelu (patrz wyżej) |
| `DiagRunner.cs` | tymczasowy headless runner dry-run na jednym rysunku (patrz "Headless diagnostyka" wyżej), **do usunięcia przed pierwszym wydaniem** jak `Inspector.cs` |

## Następne kroki

1. **Operator ma spojrzeć na `[35270]` i `[3.5013]` w Tekli** po realnym
   uruchomieniu (`dryRun: false`, ale NIE przełączać jeszcze - najpierw ten
   punkt) i potwierdzić, że zostają dokładnie te wymiary, co w dry-run v4
   (patrz "Rysunki testowe"). Dry-run i odczyt współrzędnych to nie to samo
   co spojrzenie na gotowy rysunek.
2. Dopiero po potwierdzeniu punktu 1: przełączyć `dryRun: false` w
   `MainForm.cs` (jedno miejsce, `RunButton_Click`).
3. Usunąć `Inspector.cs` i `DiagRunner.cs` (albo zostawić jako świadomą
   część projektu - zdecydować przy porządkach przed pierwszym wydaniem).
4. Rozważyć wiki (jak w Radius Dimention Mover) zamiast tego README, jeśli
   projekt urośnie.
