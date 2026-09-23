# RO Axis Dimension Remover

Samodzielny `.exe` dla Tekla Structures 2025. Ma kasować nadmiarowe wymiary
"do osi" na profilach RO (rura okrągła) w widokach przekroju/detalu miejsc
łączenia.

## Stan: `dryRun: true` — przycisk nic nie kasuje

Reguła wykrywania została zmieniona: zamiast oceniać, które wymiary do osi
są "duplikatami" (dawna reguła v1–v4, miała znany błąd na parach
prostopadłych wymiarów o tej samej wartości — pełna diagnoza w
[issue #18](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/issues/18)),
program kasuje teraz **wszystkie** wymiary do osi w wybranym widoku, bez
wyjątku — docelowo mają zostać zastąpione osobnym wymiarem opisującym
wcięcie profilu (jeszcze niezaimplementowane). Ta nowa reguła jeszcze nie
przeszła własnej "bramy bezpieczeństwa", więc `dryRun` zostaje `true`.

Pełna historia wersji i "brama bezpieczeństwa", przez którą musi przejść
każda zmiana reguły przed włączeniem realnego kasowania, są w `CLAUDE.md`
— to on jest bazą wiedzy tego projektu, README to tylko skrót.

Tryb konsolowy (`--diag-active`/`--diag-mark`, `DiagRunner.cs`) ma
`dryRun: true` na sztywno, na zawsze — to ścieżka bez człowieka przy
przycisku i nigdy nie powinna dostać możliwości realnego kasowania,
niezależnie od stanu reguły.

## Fakt wyjściowy

Tekla czasem auto-generuje wymiar prosty (`StraightDimension`), którego jeden
koniec siedzi na teoretycznej **osi** profilu RO (współrzędna promienia = 0)
zamiast na jego widocznej powierzchni (±promień). Dzieje się to typowo przy
skosie/ucięciu pod kątem - powierzchnia nie ma tam jednego punktu
odniesienia, więc Tekla łapie oś. Czasem taki wymiar jest **jedyny i
potrzebny** (opisuje długość profilu przy skosie), a czasem jest **duplikatem**
innego wymiaru opisującego to samo miejsce - i tylko duplikat ma zniknąć.

## Headless diagnostyka bez GUI

Claude Code (i każda automatyzacja) nie klika w przycisk `MainForm`.
`Program.cs` ma więc tryb konsolowy:

```
RoAxisDimensionRemover.exe --diag-active        # aktywny rysunek w Tekli
RoAxisDimensionRemover.exe --diag-mark "[Mark]" # otwiera rysunek po Mark
RoAxisDimensionRemover.exe --diag-notch         # tylko odczyt: geometria bryły partów (grunt pod wymiar wcięcia)
```

`dryRun` jest tam na sztywno `true` - nie da się tego przełączyć z linii
komend. Log leci na `stdout`.

## Architektura

Kopiuje wzorzec z siostrzanego projektu w tym samym katalogu nadrzędnym
(`..\CLAUDE.md` obowiązuje tu też):

| Plik | Zawartość |
|---|---|
| `RoAxisDimensionService.cs` | cała logika wykrywania i kasowania, zero UI |
| `MainForm.cs` | UI: jeden przycisk, log do okna i do pliku |
| `Program.cs` | punkt wejścia, w tym tryb konsolowy `--diag-active`/`--diag-mark` |
| `DiagRunner.cs` | headless runner dry-run na jednym rysunku (patrz wyżej) - `dryRun` na sztywno `true` na zawsze |
| `UpdateCheck.cs` | sprawdza w tle przy starcie, czy na GitHubie jest nowsza wersja |
| `TeklaWindowFocus.cs` | po realnym usunięciu przełącza fokus Windows na okno Tekli |
| `installer/setup.iss`, `fetch-dependencies.ps1`, `TeklaEULA.txt` | instalator Inno Setup - nie dołącza bibliotek Tekla, dociąga je z NuGet po instalacji |

## Następne kroki

Dwa aktualne priorytety: (1) przeprowadzić nową regułę wykrywania przez
bramę bezpieczeństwa, zanim realne kasowanie zostanie włączone; (2) dodać
automatyczne tworzenie wymiaru wcięcia po skasowaniu wymiarów do osi —
wymaga rzeczywistej geometrii bryły cięcia z modelu, nie zgadywania.
Szczegóły i wymagana procedura zatwierdzenia: `CLAUDE.md`.

Osobno, bez pośpiechu: rozważyć wiki (jak w siostrzanym projekcie) zamiast
tego README, jeśli projekt urośnie.
