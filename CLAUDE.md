# RO Axis Dimension Remover — baza wiedzy dla agentów AI

Ten plik ładuje się automatycznie każdej sesji Claude Code otwartej w tym
katalogu. Nadrzędny `..\CLAUDE.md` (środowisko Tekla Structures 2025, stos,
konwencje, pułapki API wspólne dla wszystkich projektów w `Projekty`)
obowiązuje też tutaj — to jest uzupełnienie specyficzne dla TEGO projektu.
`README.md` to krótki opis dla człowieka; ten plik to szczegółowa baza
wiedzy — historia, powody decyzji, ślepe uliczki.

## Zanim cokolwiek uruchomisz — brama bezpieczeństwa

- **Brama PRZESZŁA 2026-09-04.** Operator potwierdził wizualnie w Tekli —
  na żywo, patrząc na rysunek, nie tylko na log dry-run — że na `[35270]`
  i `[3.5013]` reguła v4 usuwa dokładnie te wymiary, które przewidywał
  dry-run. `dryRun` w [MainForm.cs](MainForm.cs) (przycisk GUI) jest od tego
  dnia `false` — program **kasuje naprawdę**.
- **[DiagRunner.cs](DiagRunner.cs) (tryb headless) ma `dryRun` na sztywno
  `true` NA ZAWSZE**, mimo że brama wyżej przeszła. To ścieżka wywoływana
  bez człowieka przy przycisku (automatyzacja/agent AI, `--diag-active` /
  `--diag-mark`) — nigdy nie powinna dostać możliwości realnego kasowania,
  niezależnie od tego, jak dobrze zweryfikowana jest reguła. Jeśli
  potrzebujesz sprawdzić regułę bez ryzyka, to jest do tego droga; jeśli
  ktoś prosi o headless tryb, który NAPRAWDĘ kasuje — to jest dokładnie
  ten rodzaj prośby, przy którym trzeba się zatrzymać i zapytać, nie
  zgadywać.
- **Powód tej bramy jest realny, nie proceduralny**: dwie kolejne wersje
  reguły wykrywania (v1, v2) NAPRAWDĘ skasowały dobre wymiary na żywym
  modelu, zanim ktoś to zauważył. v3 znowu gubiła wszystkie wymiary do osi
  na `[3.5013]`. Historia niżej istnieje po to, żeby nie powtórzyć tych
  samych błędów pod inną postacią — a każda KOLEJNA zmiana reguły
  wykrywania w tym pliku powinna przejść przez tę samą bramę od nowa
  (dry-run → operator patrzy na żywy rysunek → dopiero wtedy realne
  kasowanie), nie dziedziczyć zaufania z tego potwierdzenia.
- **Nigdy nie zgaduj progu ani reguły "na wyczucie".** Każda stała w
  [RoAxisDimensionService.cs](RoAxisDimensionService.cs) ma komentarz
  wyjaśniający skąd się wzięła. Jeśli trzeba dodać nową regułę — najpierw
  zdobądź realne współrzędne z dry-run (`--diag-active`/`--diag-mark`,
  patrz niżej), dopiero potem pisz kod.

## Co robi program

Kasuje nadmiarowy wymiar prosty (`StraightDimension`) "do osi" na profilach
RO (rura okrągła) w widokach przekroju/detalu miejsc łączenia. Tekla czasem
auto-generuje taki wymiar zaczepiony o teoretyczną OŚ profilu (współrzędna
promienia ≈ 0) zamiast o jego widoczną powierzchnię — typowo przy
skosie/ucięciu pod kątem, gdzie powierzchnia nie ma jednego jednoznacznego
punktu odniesienia. Czasem taki wymiar jest jedyny i potrzebny (opisuje
długość profilu przy skosie), a czasem jest duplikatem innego wymiaru
opisującego to samo miejsce — i tylko duplikat ma zniknąć.

## Aktualna reguła (v4) — cała logika w RoAxisDimensionService.cs

1. **`TouchesAxis`**: wymiar "dotyka osi", jeśli współrzędna, która MIĘDZY
   jego końcami się różni (Y albo Z — inaczej płaski wymiar 2D fałszywie
   łapie się jako "na osi", patrz v1 niżej), jest bliska zeru na
   którymkolwiek końcu.
2. **Filtr długości własnej** (v4, patrz niżej): wymiar dotykający osi, ale
   dłuższy niż `SameJointDistanceMm` (300 mm) między własnym `StartPoint` a
   `EndPoint`, jest wykluczany z kandydatów — dwuznaczność oś/powierzchnia
   dotyczy z definicji tylko krótkiego, lokalnego wymiaru przy złączu, nie
   całkowitej długości profilu.
3. **`GroupByProximity`**: pozostali kandydaci w widoku grupują się po
   bliskości geometrycznej (single-linkage, próg `SameJointDistanceMm`), nie
   po przynależności do widoku — jeden widok może zawierać kilka
   niepowiązanych złączy RO.
4. W każdym klastrze z 2+ kandydatami zostaje ten o **największej
   wyświetlanej wartości** (`Dimension.Value`, nie geometryczny dystans —
   patrz PUŁAPKA 2 niżej), reszta jest kasowana (albo tylko logowana w
   dry-run).
5. Klaster z JEDNYM kandydatem nie jest ruszany — to nie duplikat, tylko
   jedyny sposób opisania danego miejsca.

## Historia nieudanych podejść (NIE powtarzać)

Pełne przykłady liczbowe są w komentarzach klasy
[RoAxisDimensionService.cs](RoAxisDimensionService.cs) i w README. Skrót:

| Wersja | Błąd | Poprawka |
|---|---|---|
| v1 | `TouchesAxis` sprawdzał WSZYSTKIE współrzędne, nie tylko tę różniącą się między końcami — złapał prawidłowy wymiar "z boku rury" (dla którego jedna współrzędna jest 0 dla obu końców, bo widok jest 2D) jako fałszywy duplikat. Skasował dobry wymiar na żywym modelu, `[35270]`. | Sprawdzać tylko współrzędną, która MIĘDZY końcami się różni. |
| v2 | Grupowanie po CAŁYM widoku zamiast po złączu — na `[3.5013]` (więcej złączy RO w jednym widoku ogólnym) zostawiał tylko jeden wymiar z całego widoku, kasując wymiary należące do zupełnie innych złączy. Skasował dobre wymiary na żywym modelu. | `GroupByProximity` — grupowanie po bliskości geometrycznej (próg 300 mm), nie po widoku. |
| v3 | Po poprawce v2 `[3.5013]` nadal traciło WSZYSTKIE wymiary do osi w teście operatora. Przyczyna nieznana na koniec tamtej sesji. | Zdiagnozowane w v4 przez odczyt logu `[diag]`, nie przez zgadywanie. |
| v4, próba 1 (ODRZUCONA) | Diagnoza v3: klaster bliskości na `[3.5013]` mieszał 2 prawdziwe duplikaty (21 mm) z wymiarem całkowitej długości profilu (5796 mm), który też "dotyka osi" (bo lokalny początek układu współrzędnych rury leży na jej osi z definicji). Reguła "zostaw największy" zostawiała 5796 mm, ale kasowała OBA wymiary 21 mm. Pierwsza próba poprawki: kasować w klastrze tylko wymiary o IDENTYCZNEJ wartości. Działało na `[3.5013]`, ale **zepsuło `[35270]`** — tam prawdziwa para duplikat/oryginał to 24 mm i 12 mm (RÓŻNE wartości, bo jeden koniec dotyka powierzchni a drugi osi, więc rzut wychodzi inny) — wymóg równości wartości nie kasował niczego. | Odrzucone: "różne wartości" samo w sobie nic nie mówi o tym, czy dwa wymiary są duplikatem. |
| v4 (BIEŻĄCY STAN) | — | Filtr długości własnej (patrz "Aktualna reguła" pkt 2) — wyklucza wymiar całkowitej długości z kandydatów PRZED grupowaniem, więc reguła "zostaw największy w klastrze" (poprawna od początku) znowu działa poprawnie. Zweryfikowane w dry-run na obu rysunkach — patrz README, "Rysunki testowe". **Nie potwierdzone jeszcze wizualnie przez operatora.** |

PUŁAPKA 2 (osobna, nie wersja): "krótszy" nie znaczy mniejszy surowy dystans
3D między `StartPoint`/`EndPoint` — to osobna liczba od wyświetlanej
wartości (`Dimension.Value` to rzut na kierunek wymiaru, nie odległość
euklidesowa). Trzeba porównywać wyświetlaną wartość, nie geometrię.

## Pułapki API specyficzne dla tego projektu

(Uzupełnienie do `..\CLAUDE.md` — te są specyficzne dla wymiarów rysunkowych
i profilu RO, nie ogólne dla Tekla Open API).

- **`Dimension.Value` to kolekcja, nie liczba.** Trzeba iterować i szukać
  pierwszego elementu z właściwością `Value` dającą się sparsować jako
  liczba (obsługa mieszanego formatowania tekstu wymiaru) — zmierzone na
  `[35270]` przez zrzut refleksją. Patrz `GetDisplayedValue`.
- **Dla rury okrągłej lokalny punkt referencyjny (X=0) leży DOKŁADNIE na
  teoretycznej osi.** To nie jest awaria ani niejednoznaczność — to
  definicja układu współrzędnych profilu. Każdy wymiar odnoszący się do
  tego punktu (typowo: całkowita długość) będzie "dotykał osi" wg prostego
  testu współrzędnych, mimo że jest całkowicie poprawny. Patrz v4 wyżej.
- **`StraightDimension.StartPoint`/`EndPoint` są w jednostkach modelu**, nie
  mm na papierze — zgodne z ogólną pułapką jednostek z `..\CLAUDE.md`, ale
  warto pamiętać przy debugowaniu tego konkretnego serwisu, bo `Value` (mm
  na papierze) i współrzędne (mm modelu) żyją w tym samym wywołaniu obok
  siebie.

## Jak testować bez klikania w GUI

Automatyzacja (w tym Claude Code) nie klika w przycisk `MainForm`.
[Program.cs](Program.cs) ma więc tryb konsolowy:

```
RoAxisDimensionRemover.exe --diag-active          # aktywny rysunek w Tekli
RoAxisDimensionRemover.exe --diag-mark "[3.5013]" # otwiera rysunek po Mark, potem diagnostyka
```

`dryRun` jest w obu wywołaniach na sztywno `true` w
[DiagRunner.cs](DiagRunner.cs) — nie da się tego przełączyć z linii poleceń.
Log leci na `stdout` (przechwyć np. `> plik.txt 2>&1` albo uruchom w tle i
przeczytaj output). `--diag-mark` woła `SetActiveDrawing(d, true)` — otwiera
rysunek na ekranie, żeby operator mógł od razu spojrzeć na wynik.

Przed przebudowaniem: `taskkill /F /IM RoAxisDimensionRemover.exe`, jeśli
proces działa (patrz `..\CLAUDE.md`, zasada 4).

## Struktura plików

| Plik | Zawartość |
|---|---|
| `RoAxisDimensionService.cs` | cała logika wykrywania i kasowania, zero UI |
| `MainForm.cs` | UI: jeden przycisk, log do okna i do pliku (`dryRun: false` od 2026-09-04 — kasuje naprawdę) |
| `Program.cs` | punkt wejścia; GUI domyślnie, `--diag-active`/`--diag-mark` dla trybu konsolowego |
| `DiagRunner.cs` | headless runner dry-run (patrz wyżej) — **świadomie trwały element projektu**, `dryRun` na sztywno `true` na zawsze, nie do usunięcia |
| `UpdateCheck.cs` | sprawdza w tle przy starcie, czy na GitHubie jest nowsza wersja (cisza przy braku internetu/błędzie) — wzorzec 1:1 z `Radius Dimention Mover` |

`Inspector.cs` (tymczasowy skaner kandydatów RO po całym modelu, użyty
jednorazowo do znalezienia `[3.5013]`) **usunięty po v0.2.0** — nieużywany
od chwili, gdy oba rysunki testowe były już znane. Patrz git historia, jeśli
trzeba znaleźć kolejnego kandydata na innym modelu.
| `installer/setup.iss`, `installer/fetch-dependencies.ps1`, `installer/TeklaEULA.txt` | instalator Inno Setup — nie dołącza bibliotek Tekla, dociąga je z NuGet po instalacji, patrz komentarze w plikach |

## Wydania (GitHub Releases)

- Wersja w `RoAxisDimensionRemover.csproj` (`<Version>`), `installer/setup.iss`
  (`MyAppVersion`) i tag na GitHubie muszą się zgadzać — `UpdateCheck.cs`
  porównuje assembly version z tagiem najnowszego release.
- Instalator budowany lokalnie: `dotnet build -c Debug -p:Platform=x64`
  (WAŻNE: `-p:Platform=x64` explicit, bez tego `dotnet build` ląduje w
  `bin\Debug\net48` bez segmentu `x64`, co nie zgadza się ze ścieżką w
  `setup.iss`), potem `ISCC.exe installer\setup.iss` (Inno Setup 6,
  `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`).
- `v0.1.0` opublikowane jako **pre-release** na GitHubie — dry-run-only build
  do testów, nie potwierdzone bezpieczne narzędzie. Nie usuwać flagi
  pre-release z kolejnych wydań, dopóki operator nie przejdzie przez bramę
  bezpieczeństwa z sekcji wyżej.

## Branche

- `dev` — domyślny branch repo, rozwojowy/WIP (dawniej `main`, zmieniony na
  `dev` i ustawiony jako domyślny przez GitHub API — `gh` nie jest
  zainstalowany na tej maszynie, patrz `..\CLAUDE.md`). `dryRun: true` jest
  tu dozwolony i oczekiwany dopóki reguła nie przejdzie bramy bezpieczeństwa
  wyżej.
- `release` — ma trzymać tylko kod potwierdzony jako bezpieczny
  (`dryRun: false`, po wizualnym potwierdzeniu operatora na obu rysunkach
  testowych).
  **UWAGA:** poprawka v4 (filtr długości własnej) została scalona do
  `release` przez [PR #1](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/pull/1)
  — to porządkowanie repo (branch `dev`→`release` jako miejsce docelowe),
  **NIE jest to potwierdzenie bezpieczeństwa**. `dryRun` w kodzie na obu
  branchach jest nadal na sztywno `true`. Merge do `release` sam w sobie
  nie zastępuje bramy z sekcji wyżej — dopóki operator nie spojrzy na
  `[35270]` i `[3.5013]` w Tekli po realnym uruchomieniu, traktuj `release`
  jako "kod gotowy do przetestowania na żywo", nie "przetestowany".

## Następne kroki

1. ~~Operator ma spojrzeć na `[35270]` i `[3.5013]` w Tekli po realnym
   uruchomieniu i potwierdzić, że zostają dokładnie te wymiary co w
   dry-run v4.~~ **Zrobione 2026-09-04.**
2. ~~Przełączyć `dryRun: false` w `MainForm.cs`.~~ **Zrobione 2026-09-04.**
   `DiagRunner.cs` NIE dostał tej zmiany — zostaje na sztywno `dryRun: true`
   na zawsze, patrz brama bezpieczeństwa wyżej.
3. ~~Wydać wersję instalatora z realnym kasowaniem jako pełne wydanie (nie
   pre-release).~~ **Zrobione 2026-09-04** — v0.2.0.
4. ~~Usunąć `Inspector.cs` i `DiagRunner.cs` (albo świadomie zostawić).~~
   **Zdecydowane i zrobione 2026-09-04**: `Inspector.cs` usunięty (był
   nieużywany od chwili znalezienia obu rysunków testowych).
   `DiagRunner.cs` zostaje świadomie, na zawsze — patrz brama
   bezpieczeństwa wyżej i komentarz w tym pliku.
