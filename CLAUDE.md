# RO Axis Dimension Remover — baza wiedzy dla agentów AI

Ten plik ładuje się automatycznie każdej sesji Claude Code otwartej w tym
katalogu. Nadrzędny `..\CLAUDE.md` (środowisko Tekla Structures 2025, stos,
konwencje, pułapki API wspólne dla wszystkich projektów w `Projekty`)
obowiązuje też tutaj — to jest uzupełnienie specyficzne dla TEGO projektu.
`README.md` to krótki opis dla człowieka; ten plik to szczegółowa baza
wiedzy — historia, powody decyzji, ślepe uliczki.

## Zanim cokolwiek uruchomisz — brama bezpieczeństwa

- **BRAMA ZAMKNIĘTA. `dryRun: true` w [MainForm.cs](MainForm.cs).
  PUŁAPKA 5 OTWARTA, ale ZDIAGNOZOWANA (2026-09-04, późny wieczór).**
  Operator miał rację od pierwszego zgłoszenia - to NIE był pojedynczy
  incydent ani błędna interpretacja.

  **Na czym polega błąd (zdiagnozowane z logu + zgłoszenia operatora
  "usuwa jeden poziomy, jeden pionowy"):** para `21`/`21` na `[3.5013]` to
  NIE duplikat. To DWA PROSTOPADŁE wymiary tego samego skosu 45° - jeden
  mierzy offset POZIOMO (wzdłuż rury), drugi PIONOWO (w poprzek). Oba
  pokazują `21`, bo kąt to dokładnie 45° (adnotacja `45°` jest na rysunku).
  Równa wartość, zupełnie inne znaczenie. Reguła v4 widzi: oba dotykają
  osi ✓, oba krótkie ✓, oba blisko siebie ✓, oba `21,0` ✓ → "duplikat,
  kasuj jeden" → ginie CAŁA jedna informacja (albo poziom, albo pion).
  To dokładnie to, co operator opisał na początku jako "usuwa całą
  szerokość albo całą długość".

  ```
  21,0  Start=(5775,0; 0,0;  0,0)   End=(5796,2; 21,2; 0,0)
  21,0  Start=(5775,0; 0,0; 21,2)   End=(5796,2; 21,2; 0,0)
  ```

  **DLACZEGO TRZY "CZYSTE" TESTY TEGO NIE ZŁAPAŁY - najważniejsza lekcja
  z tej sesji:** testy sprawdzały, czy realne kasowanie zgadza się z
  PRZEWIDYWANIEM DRY-RUN, a nie czy wynik jest POPRAWNY INŻYNIERSKO. To
  tautologia - dry-run i realny przebieg używają tego samego kodu, więc
  zawsze się zgodzą, także gdy oba są błędne. Do tego pytanie do operatora
  było zadane z już wbudowanym założeniem ("usuwa jeden z pary duplikatów -
  poprawnie?"), więc potwierdzenie dotyczyło ramki, nie geometrii.
  **Nigdy nie waliduj reguły przez porównanie z jej własnym dry-runem.
  Waliduj przez pytanie "czy po tej operacji rysunek nadal opisuje
  wszystko, co musi opisywać?" - i formułuj pytanie do operatora BEZ
  podpowiadania odpowiedzi.**

  **Kierunek naprawy (nie zaimplementowany):** trzeba porównywać KIERUNEK
  POMIARU wymiaru, nie tylko wartość - dwa prostopadłe wymiary nigdy nie
  są duplikatem, nawet przy identycznej wartości. Uwaga: wyświetlana
  wartość to RZUT rozpiętości `StartPoint`→`EndPoint` na kierunek pomiaru,
  więc sama rozpiętość nie wystarcza do rozróżnienia (dla obu wymiarów
  wyżej rzut na `(1,0,0)` i na `(0,1,0)` daje to samo 21,2). Trop z
  refleksji nad `Tekla.Structures.Drawing.dll`: **istnieje `UpDirection`**
  (na typie atrybutów wymiaru / w hierarchii `DimensionBase` - nie
  dokończono sprawdzania, dokończyć zanim pisać kod). Najpierw dopisać
  `UpDirection` (i cokolwiek jeszcze opisuje orientację) do logu `[diag]`
  dla OBU rysunków, potem projektować regułę na zmierzonych danych.
  **`[35270]` jest inny i musi dalej działać:** tam para to `24` i `12`
  (RÓŻNE wartości), są DWA wymiary `24`, a `12` jest faktycznie zbędny -
  operator potwierdził to wizualnie i osobno w nadzorowanym teście. Fix
  nie może zepsuć tego przypadku.
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
| v4 | Filtr długości własnej naprawił [3.5013]/v3, ale reguła bazowa ("ta sama wartość + bliskość = duplikat, kasuj mniejszy/inny") ma osobny, poważniejszy błąd - patrz PUŁAPKA 5 wyżej: dwa PROSTOPADŁE wymiary tego samego skosu 45° mają identyczną wartość, ale nie są duplikatem. v4 przeszedł "bramę bezpieczeństwa" trzy razy (wizualnie + trzy nadzorowane testy) i mimo to okazał się błędny, bo wszystkie te potwierdzenia sprawdzały wynik względem przewidywania dry-runa tej samej reguły, nie względem poprawności inżynierskiej rysunku. | **NIE ZAIMPLEMENTOWANE.** Trzeba dodać porównanie KIERUNKU POMIARU (nie tylko wartości) - patrz brama bezpieczeństwa wyżej, sekcja "Kierunek naprawy" i "Następne kroki" pkt 5. Do tego momentu `dryRun: true` na sztywno. |

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
| `MainForm.cs` | UI: jeden przycisk, log do okna i do pliku (`dryRun: true` — NIE kasuje, PUŁAPKA 5 otwarta, patrz brama bezpieczeństwa wyżej) |
| `Program.cs` | punkt wejścia; GUI domyślnie, `--diag-active`/`--diag-mark` dla trybu konsolowego |
| `DiagRunner.cs` | headless runner dry-run (patrz wyżej) — **świadomie trwały element projektu**, `dryRun` na sztywno `true` na zawsze, nie do usunięcia |
| `UpdateCheck.cs` | sprawdza w tle przy starcie, czy na GitHubie jest nowsza wersja (cisza przy braku internetu/błędzie) — wzorzec 1:1 z `Radius Dimention Mover` |
| `TeklaWindowFocus.cs` | po realnym usunięciu przełącza fokus Windows na główne okno Tekla Structures (Win32 `SetForegroundWindow`, nie API Tekli - Open API nie ma metody do tego, zweryfikowane), żeby Ctrl+Z od razu trafił tam gdzie ma |
| `installer/setup.iss`, `installer/fetch-dependencies.ps1`, `installer/TeklaEULA.txt` | instalator Inno Setup — nie dołącza bibliotek Tekla, dociąga je z NuGet po instalacji, patrz komentarze w plikach |

`Inspector.cs` (tymczasowy skaner kandydatów RO po całym modelu, użyty
jednorazowo do znalezienia `[3.5013]`) **usunięty po v0.2.0** — nieużywany
od chwili, gdy oba rysunki testowe były już znane. Patrz git historia, jeśli
trzeba znaleźć kolejnego kandydata na innym modelu.

## Wydania (GitHub Releases)

- Wersja w `RoAxisDimensionRemover.csproj` (`<Version>`), `installer/setup.iss`
  (`MyAppVersion`) i tag na GitHubie muszą się zgadzać — `UpdateCheck.cs`
  porównuje assembly version z tagiem najnowszego release.
- Instalator budowany lokalnie: `dotnet build -c Debug -p:Platform=x64`
  (WAŻNE: `-p:Platform=x64` explicit, bez tego `dotnet build` ląduje w
  `bin\Debug\net48` bez segmentu `x64`, co nie zgadza się ze ścieżką w
  `setup.iss`), potem `ISCC.exe installer\setup.iss` (Inno Setup 6,
  `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`).
- `v0.1.0` opublikowane jako **pre-release** (dry-run-only). `v0.2.0`-`v0.2.3`
  opublikowane jako pełne wydania (nie pre-release) - **z czego v0.2.1 i
  v0.2.3 miały `dryRun: false` z BŁĘDNĄ regułą, patrz PUŁAPKA 5 i historia
  niżej.** Wniosek: sama flaga pre-release na GitHubie NIE jest wiarygodnym
  sygnałem bezpieczeństwa w tym repo - nie ufaj jej, sprawdzaj kod.
  `v0.2.4` (bieżące) wróciło do `dryRun: true` po znalezieniu prawdziwej
  przyczyny PUŁAPKI 5. Historia wydań: `v0.1.0` (pre-release, dry-run) →
  `v0.2.0` (pierwsze `dryRun: false`, po "bramie" która okazała się
  niewystarczająca) → `v0.2.1` (dodał `TeklaWindowFocus`, wciąż
  `dryRun: false`, TU operator pierwszy raz zgłosił błąd) → `v0.2.2`
  (`dryRun: true`, bezpieczny rollback) → `v0.2.3` (`dryRun: false` po
  BŁĘDNYM zamknięciu PUŁAPKI 5 trzema "czystymi" testami, które w
  rzeczywistości niczego nie udowodniły - patrz brama bezpieczeństwa
  wyżej) → `v0.2.4` (`dryRun: true`, prawdziwa przyczyna znaleziona).

## Branche

- `dev` — domyślny branch repo, rozwojowy/WIP (dawniej `main`, zmieniony na
  `dev` i ustawiony jako domyślny przez GitHub API — `gh` nie jest
  zainstalowany na tej maszynie, patrz `..\CLAUDE.md`). `dryRun: true` jest
  tu dozwolony i oczekiwany dopóki reguła nie przejdzie bramy bezpieczeństwa
  wyżej.
- `release` — ma trzymać tylko kod potwierdzony jako bezpieczny. W praktyce
  do tej pory `dev` i `release` zwykle kończą z identyczną treścią po
  serii PR-ów w obie strony (`dev`→`release` żeby promować, `release`→`dev`
  żeby zsynchronizować historię po merge commicie z GitHuba) — sprawdzaj
  `git diff origin/dev origin/release` zamiast zgadywać, czy się rozjechały.
  **Merge do `release` sam w sobie NIE jest potwierdzeniem bezpieczeństwa.**

  **STAN NA 2026-09-16: `dev` i `release` ZNOWU IDENTYCZNE, obie
  `dryRun: true` (bezpieczne).** Historia rozjazdu tego dnia, dla
  kontekstu: PR #13 wniosło do `release` `dryRun: false` (BŁĘDNA
  poprawka, zanim znaleziono prawdziwą przyczynę PUŁAPKI 5). Po znalezieniu
  przyczyny, `dev` dostało `dryRun: true` z powrotem, a
  [PR #15](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/pull/15)
  (dev→release) + PR #16 (release→dev, sync) doprowadziły branche z
  powrotem do identycznej treści. **To NIE znaczy, że tak będzie zawsze -
  NIGDY nie zgaduj stanu branchy z tego opisu ani z ich nazw. Zawsze
  sprawdź `dryRun` w `MainForm.cs` wprost w plikach na branchu, z którego
  rzeczywiście korzystasz, i `git diff origin/dev origin/release`, żeby
  zobaczyć realną różnicę (jeśli jest) w chwili, gdy czytasz ten plik.**

## Następne kroki

**Historyczne kroki 1-4 (znalezienie obu rysunków testowych, pierwsze
przejście bramy, pierwsze wydanie instalatora, usunięcie `Inspector.cs`)
są zrobione i nieaktualne jako TODO - zobacz historię commitów, jeśli
potrzebne. Jedyny aktualny priorytet:**

**PUŁAPKA 5 — OTWARTA, ZDIAGNOZOWANA, DO NAPRAWY.** Pełny opis w bramie
bezpieczeństwa na początku pliku. Konkretne kroki, w tej kolejności:

0. ~~Scalić PR #15 (i sync PR #16), żeby `release` dostało `dryRun: true`.~~
   **Zrobione 2026-09-16.** `dev` i `release` są znowu identyczne - ale
   zawsze sprawdź to na nowo, nie ufaj temu opisowi (patrz sekcja
   "Branche" wyżej).
1. Dokończyć refleksję nad `Tekla.Structures.Drawing.dll`: gdzie
   dokładnie siedzi `UpDirection` (typ, klasa nadrzędna) i co jeszcze
   opisuje orientację/kierunek pomiaru `StraightDimension`. (Poprzednia
   sesja przerwała to w połowie - `UpDirection` znaleziono, ale nie
   ustalono jego dokładnego typu/lokalizacji w hierarchii klas.)
2. Dopisać te pola do logu `[diag]` w `RoAxisDimensionService.cs` (obok
   `DescribeDimensionSet`) i zebrać dane z OBU rysunków:
   `RoAxisDimensionRemover.exe --diag-mark "[35270]"` i
   `--diag-mark "[3.5013]"`. Nic nie kasuje, w pełni bezpieczne niezależnie
   od stanu `dryRun`.
3. Dopiero na tych ZMIERZONYCH danych zaprojektować regułę: prostopadłe
   wymiary nigdy nie są duplikatem, nawet przy identycznej wartości.
   Sprawdzić, czy `[35270]` (`24`/`12`, różne wartości) to para
   RÓWNOLEGŁA (ma dalej być kasowana - `12` to prawdziwy duplikat), a
   `[3.5013]` (`21`/`21`, ta sama wartość) to para PROSTOPADŁA (nie
   ruszać - to dwa różne wymiary skosu 45°).
4. Brama od zera, dla TEJ konkretnej zmiany reguły: dry-run na obu
   rysunkach → operator patrzy na żywy rysunek w Tekli w momencie
   kliknięcia → pytanie do operatora zadane BEZ podpowiadania odpowiedzi
   ("czy rysunek nadal opisuje wszystko, co musi opisywać?", NIE "czy to
   poprawnie usunęło duplikat?") → dopiero wtedy `dryRun: false`.
   **Nie licz potwierdzeń z poprzedniej (błędnej) bramy v4 - ta reguła
   jest inna i wymaga własnego, pełnego przejścia.**
