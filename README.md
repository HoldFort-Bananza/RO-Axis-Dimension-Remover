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

### v3 (BIEŻĄCY STAN): grupowanie po bliskości NADAL kasuje za dużo na [3.5013]

Po wdrożeniu grupowania po bliskości, `[3.5013]` **nadal traci wszystkie**
wymiary do osi w teście operatora. Nie zdiagnozowane. Hipotezy do
sprawdzenia w następnej sesji:

1. **Próg 300 mm jest zgadywany, nie zmierzony** - możliwe że na `[3.5013]`
   wszystkie kandydaci naprawdę leżą bliżej niż 300 mm, bo to jedno złącze o
   innej geometrii niż `[35270]`, a nie kilka złączy. W takim razie problem
   nie jest w grupowaniu, tylko w założeniu "keep tylko 1 na klaster" - może
   przy 3+ kandydatach reguła musi być inna (np. zostają wszyscy oprócz
   dokładnie jednego zduplikowanego pod względem wartości).
2. **Jednostki współrzędnych mogą się różnić między widokami** - `CLAUDE.md`
   nadrzędny (`..\CLAUDE.md`) ostrzega, że w tej samej klasie potrafią
   mieszać się jednostki modelu i mm na papierze zależnie od kontekstu.
   Możliwe że różne widoki na `[3.5013]` mają różną skalę, więc stały próg
   w mm nie ma sensu bez podzielenia przez skalę widoku (`view.Attributes.Scale`?).
3. Trzeba **przeczytać log diagnostyczny** (`dryRun: true` już wgrane,
   loguje pełne współrzędne i podział na klastry - `RoAxisDimensionService.cs`,
   bloki `[diag]`) zamiast zgadywać dalej. Log leci do `logs/session_*.log`
   obok `.exe` **i** do okna programu.

**Nie przełączaj `dryRun` z powrotem na `false`, dopóki log z `[3.5013]`
nie pokaże sensownego podziału na klastry.**

## Rysunki testowe

| Rysunek | Profil / opis | Status |
|---|---|---|
| `[35270]` | Einzelteil Geländer, RO Ø48,3 (promień 24,15) | ✅ v2 działa poprawnie: kasuje tylko `12`, zostawia oba `24` i `2811` |
| `[3.5013]` | Einzelteil Geländer, więcej złączy RO w jednym widoku | ❌ v2 i v3 kasują za dużo - patrz wyżej |

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
| `Program.cs` | punkt wejścia |
| `Inspector.cs` | tymczasowy skaner kandydatów po całym modelu (patrz wyżej) |

## Następne kroki

1. Przeczytać `logs/session_*.log` z próby na `[3.5013]` (dryRun) -
   zobaczyć realne współrzędne i podział na klastry.
2. Ustalić poprawną regułę (patrz hipotezy wyżej) i **zmierzyć**, nie zgadywać
   progu kolejny raz.
3. Przetestować na obu rysunkach (`[35270]` i `[3.5013]`) - żadna zmiana nie
   może psuć tego, co już działa.
4. Dopiero wtedy przełączyć `dryRun: false` w `MainForm.cs`.
5. Usunąć `Inspector.cs` (albo zostawić jako świadomą część projektu -
   zdecydować przy porządkach przed pierwszym wydaniem).
6. Rozważyć wiki (jak w Radius Dimention Mover) zamiast tego README, jeśli
   projekt urośnie.
