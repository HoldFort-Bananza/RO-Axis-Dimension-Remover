# RO Axis Dimension Remover — baza wiedzy dla agentów AI

Ten plik ładuje się automatycznie każdej sesji Claude Code otwartej w tym
katalogu. Nadrzędny `..\CLAUDE.md` (środowisko Tekla Structures 2025, stos,
konwencje, pułapki API wspólne dla wszystkich projektów w `Projekty`)
obowiązuje też tutaj — to jest uzupełnienie specyficzne dla TEGO projektu.
`README.md` to krótki opis dla człowieka; ten plik to szczegółowa baza
wiedzy — historia, powody decyzji, ślepe uliczki. **Uwaga przy edycji
`README.md`:** od 2026-09-16, na życzenie operatora, README celowo NIE
zawiera numerów rysunków (Mark) ani nazw siostrzanych projektów (repo jest
publiczne) — opisuj tam przypadki testowe geometrycznie (np. "para
prostopadłych wymiarów o tej samej wartości"), nie po numerze. Ten plik
(`CLAUDE.md`) i `AGENTS.md` nie mają tego ograniczenia — mają zostać
szczegółowe, bo to one są bazą do diagnozy.

## STAN NA 2026-09-23 — reguła v5, brama znowu od zera

**Reguła wykrywania została CAŁKOWICIE ZMIENIONA, nie tylko naprawiona.**
Zamiast grupować wymiary do osi i kasować "duplikaty" (v4, PUŁAPKA 5 niżej),
`RoAxisDimensionService.RemoveAxisDimensions` kasuje teraz **KAŻDY** wymiar
dotykający osi w JEDNYM widoku wskazanym przez operatora, bez oceniania
"który jest ważniejszy". Powód: docelowo wymiar do osi ma być zastąpiony
osobnym wymiarem wcięcia (cut fitting, patrz sekcja niżej) — skoro znikają
WSZYSTKIE, nie ma już decyzji "duplikat czy nie", więc **PUŁAPKA 5 (para
prostopadłych wymiarów 45° o tej samej wartości) przestaje dotyczyć tej
metody** - nie trzeba już rozróżniać duplikatu od pary prostopadłej, bo obie
i tak są kasowane. Cała stara logika grupowania (`GroupByProximity`,
`MinPointDistance`) została USUNIĘTA z kodu - historia niżej zostaje jako
kontekst diagnostyczny, ale nie opisuje już bieżącego zachowania.

**BRAMA DLA REGUŁY v5 PRZESZŁA 2026-09-23.** Operator zobaczył dry-run na
żywym `[35021]` (poprawnie znalazł `8 mm` i `21 mm`, zero fałszywych
trafień - dokładnie ten sam widok co w PUŁAPCE 5, teraz bez dedupu), i
wprost potwierdził na pytanie o realne kasowanie ("tak dryrun false").
**`MainForm.cs` ma teraz `dryRun: false` - przycisk NAPRAWDĘ kasuje.**
`DiagRunner.cs` (`--diag-active`/`--diag-mark`/`--diag-notch`) ma `dryRun`
na sztywno `true` NA ZAWSZE, niezależnie od stanu bramy - patrz sekcja
"Jak testować" niżej, to się nie zmienia nigdy.

**Wybór widoku (nowe w v5):** przycisk w `MainForm.cs` woła
`Picker.PickPoint`, żeby operator kliknął widok w Tekli. **ZMIERZONE NA
ŻYWO (2026-09-23, dwukrotnie, potwierdzone przez `tasklist`):
`Picker.PickPoint` w tym środowisku potrafi zawiesić proces w
nieskończoność**, jeśli operator kliknie w widok bez trafienia w
narysowaną geometrię (linię, wymiar, kontur partu) - dla profili RO
rysowanych bez kreskowania spora część powierzchni widoku jest "martwa".
Fokus Windows na oknie Tekli (`TeklaWindowFocus.BringToFront`) TUŻ PRZED
startem pickera pogłębiał problem (podejrzenie: ingerencja w stan
"uzbrajania" interaktywnej komendy Tekli) - usunięte, `BringToFront`
zostaje tylko PO operacji (do Ctrl+Z, jak było). Obejście: **Esc w Tekli
przerywa Picker** (`PickerInterruptedException`), a `MainForm` łapie ten
wyjątek i pokazuje listę widoków z arkusza (`PickViewFromList`) jako
gwarantowaną alternatywę. Operator zaakceptował ten stan 2026-09-23 ("klika
tylko na parcie, zostaw tak") - **to znana, zaakceptowana wada UX, nie coś
do "naprawienia" bez dalszych wskazówek operatora.**

## Wymiar wcięcia (cut fitting) — NIE ZAIMPLEMENTOWANE

Docelowo program ma po skasowaniu wymiarów do osi dorysować nowy wymiar
(zwykle poziomy), opisujący wcięcie (cut fitting) profilu w miejscu
złącza - żeby rysunek nie tracił informacji, tylko zmieniał jej formę.
**Tego kroku jeszcze nie ma w kodzie.** Powód: wymaga prawdziwej geometrii
bryły cięcia z modelu (`Tekla.Structures.Model.Part.GetSolid()`, analiza
ścian), nie samych punktów usuwanych wymiarów (te leżą na osi - to
dokładnie ten problem, który ma zniknąć). Zgadywanie, która z wielu ścian
bryły jest "ścianą cięcia", byłoby dokładnie tym błędem, który już raz
skasował dobre dane w tym projekcie (PUŁAPKA 5) - nie idzie się tą drogą
bez zmierzonych danych.

**Zrobiony pierwszy, bezpieczny krok:** `--diag-notch` (`DiagRunner.
RunNotchDiag`, tylko odczyt, nic nie zmienia) mostkuje rysunek→model przez
`Part.ModelIdentifier` → `Model.SelectModelObject` (wzorzec z oficjalnej
dokumentacji Tekli, nie zgadany) i loguje `Profile`, `Class` oraz bryłę
(`GetSolid()`: `MinimumPoint`/`MaximumPoint`, liczba ścian). Zmierzone na
żywo na `[35021]`: `RO42.4*3.2`, bryła 26 ścian. To potwierdza, że most
działa - **następny krok (nierozpoczęty): wybrać z tych ścian tę, która
jest powierzchnią cięcia, i przeliczyć ją na 2D wymiar w widoku. Zanim to
się napisze, trzeba zebrać dane z kilku różnych złączy (różne kąty cięcia),
nie projektować reguły na jednym przypadku.**

## Historia: PUŁAPKA 5 (dotyczyła reguły v4, zastąpionej przez v5 wyżej)

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
  **Ten sam opis PUŁAPKI 5 jest też jako
  [issue #18](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/issues/18)
  na GitHubie (otwarte 2026-09-16)** — jeśli pracujesz nad naprawą, zostaw
  tam komentarz z postępem, żeby kolejne narzędzie/sesja/agent nie
  zaczynały diagnozy od zera.
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

## Co robi program (v5, bieżące)

Kasuje **każdy** wymiar prosty (`StraightDimension`) "do osi" na profilach
RO (rura okrągła) w JEDNYM widoku wskazanym przez operatora — bez oceniania
duplikatów, patrz "STAN NA 2026-09-23" na górze pliku. Tekla czasem
auto-generuje taki wymiar zaczepiony o teoretyczną OŚ profilu (współrzędna
promienia ≈ 0) zamiast o jego widoczną powierzchnię — typowo przy
skosie/ucięciu pod kątem. Docelowo taki wymiar ma zostać zastąpiony nowym
wymiarem wcięcia (patrz sekcja "Wymiar wcięcia" wyżej - niezaimplementowane).

**Aktualna reguła — cała logika w `RoAxisDimensionService.RemoveAxisDimensions`:**

1. **`TouchesAxis`**: wymiar "dotyka osi", jeśli współrzędna, która MIĘDZY
   jego końcami się różni (Y albo Z — inaczej płaski wymiar 2D fałszywie
   łapie się jako "na osi", patrz v1 niżej), jest bliska zeru na
   którymkolwiek końcu.
2. **Filtr długości własnej**: wymiar dotykający osi, ale dłuższy niż
   `SameJointDistanceMm` (300 mm) między własnym `StartPoint` a `EndPoint`,
   jest wykluczany z kandydatów — dwuznaczność oś/powierzchnia dotyczy z
   definicji tylko krótkiego, lokalnego wymiaru przy złączu, nie całkowitej
   długości profilu.
3. Każdy pozostały kandydat jest kasowany (albo tylko logowany w dry-run) -
   **bez grupowania i bez wyboru "który zostaje"**, w przeciwieństwie do
   historycznej v4 niżej.

Historyczna reguła v4 (kroki `GroupByProximity`, wybór największej wartości
w klastrze) jest **usunięta z kodu** - opis niżej to już tylko historia
diagnostyczna, nie bieżące zachowanie.

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
- **`Picker.PickPoint` może zawiesić proces w nieskończoność** przy kliku w
  miejsce widoku bez ŻADNEJ geometrii w tolerancji - zmierzone na żywo
  2026-09-23, dwukrotnie, potwierdzone przez `tasklist` (proces siedział
  zablokowany, nie zwracał sterowania). Nie jest to kwestia fokusu okna -
  wymuszanie `SetForegroundWindow` na Tekli TUŻ PRZED startem pickera raczej
  pogłębiało problem niż go rozwiązywało. Jedyne potwierdzone wyjście z
  zawieszonego pickera to Esc w Tekli (`PickerInterruptedException`) - kod w
  `MainForm.cs` łapie ten wyjątek i pokazuje listę widoków. `Picker`
  ma prywatny konstruktor (zmierzone reflection-only load: `GetConstructors()`
  publiczne zwraca 0, z `NonPublic` daje 1) - jedyna publiczna droga do
  instancji to `DrawingHandler.GetPicker()`, nie `new Picker(drawing)`.
- **`ViewBase.GetAllObjects()` działa na `ViewBase`, nie trzeba rzutować do
  `View`.** `DetailView`/`SectionView` nie są `View` (rzutowanie
  `pickedView as View` zawodziło dla widoków przekroju/detalu, mimo że
  Picker poprawnie zwracał widok) - operować na `ViewBase` wszędzie, gdzie
  to możliwe, rzutować w dół tylko gdy naprawdę trzeba coś specyficznego dla
  `View` (np. `.Name`, którego `ViewBase` nie ma).

## Skrót na pulpicie operatora (stan lokalny, nie w repo)

**2026-09-23: skrót `RO Axis Dimension Remover.lnk` na pulpicie
przekierowany na `bin\x64\Debug\net48\RoAxisDimensionRemover.exe`
(build z tego repo), NIE na zainstalowaną kopię
(`%LOCALAPPDATA%\Programs\RoAxisDimensionRemover\`).** Powód: instalator
budowany z 16 września miał starą regułę v4 (klastrowanie/duplikaty),
operator testował przez skrót i widział nieaktualne zachowanie - klasyczna
pułapka "zainstalowana kopia to nie zbudowana" z `..\CLAUDE.md`, ale w
drugą stronę (skrót zamiast instalatora wskazuje teraz na build). **To
oznacza, że dopóki ten stan się nie zmieni, przebudowanie projektu
(`dotnet build ... -p:Platform=x64`) od razu aktualizuje to, co operator
odpala z pulpitu** - wygodne w tej fazie rozwoju (częste zmiany reguły),
ale gdy reguła przejdzie bramę bezpieczeństwa i będzie gotowa do
dystrybycji, warto rozważyć przywrócenie skrótu na zainstalowaną kopię
(przez świeżo zbudowany instalator) - to lokalna zmiana na tym komputerze,
nie ma śladu w plikach repo.

## Jak testować bez klikania w GUI

Automatyzacja (w tym Claude Code) nie klika w przycisk `MainForm`.
[Program.cs](Program.cs) ma więc tryb konsolowy:

```
RoAxisDimensionRemover.exe --diag-active          # aktywny rysunek w Tekli, wszystkie widoki, reguła v5
RoAxisDimensionRemover.exe --diag-mark "[3.5013]" # otwiera rysunek po Mark, potem diagnostyka
RoAxisDimensionRemover.exe --diag-notch           # tylko odczyt: geometria bryły (Model.Part.GetSolid()) partów na aktywnym rysunku, grunt pod wymiar wcięcia
```

`dryRun` jest we wszystkich trzech wywołaniach na sztywno `true` w
[DiagRunner.cs](DiagRunner.cs) — nie da się tego przełączyć z linii poleceń.
`--diag-notch` nawet nie ma pojęcia `dryRun` - nic nie usuwa ani nie
tworzy, tylko czyta model przez `Tekla.Structures.Model.Model`. Log leci na
`stdout` (przechwyć np. `> plik.txt 2>&1` albo uruchom w tle i przeczytaj
output). `--diag-mark` woła `SetActiveDrawing(d, true)` — otwiera rysunek na
ekranie, żeby operator mógł od razu spojrzeć na wynik.

`--diag-active`/`--diag-mark` przechodzą po WSZYSTKICH widokach na arkuszu
(Picker wymaga GUI, nie da się go odtworzyć z linii poleceń) — inaczej niż
przycisk w `MainForm`, który działa na jednym widoku wybranym przez
operatora (klik w Tekli lub, po Esc, lista w oknie programu — patrz "STAN
NA 2026-09-23" na górze pliku).

Przed przebudowaniem: `taskkill /F /IM RoAxisDimensionRemover.exe`, jeśli
proces działa (patrz `..\CLAUDE.md`, zasada 4) — **szczególnie ważne przy
debugowaniu Pickera: zawieszony `PickPoint` blokuje proces, `taskkill` to
jedyny sposób go zakończyć.**

## Struktura plików

| Plik | Zawartość |
|---|---|
| `RoAxisDimensionService.cs` | cała logika wykrywania i kasowania, zero UI (reguła v5 - kasuje wszystko w widoku, patrz wyżej) |
| `MainForm.cs` | UI: jeden przycisk, log do okna i do pliku (`dryRun: false` od 2026-09-23 — brama v5 przeszła, przycisk NAPRAWDĘ kasuje). Wybór widoku: `Picker.PickPoint` (klik w Tekli), Esc → `PickViewFromList` (lista w oknie) |
| `Program.cs` | punkt wejścia; GUI domyślnie, `--diag-active`/`--diag-mark`/`--diag-notch` dla trybu konsolowego |
| `DiagRunner.cs` | headless runner dry-run + `RunNotchDiag` (geometria bryły, grunt pod wymiar wcięcia) — **świadomie trwały element projektu**, `dryRun` na sztywno `true` na zawsze, nie do usunięcia |
| `UpdateCheck.cs` | sprawdza w tle przy starcie, czy na GitHubie jest nowsza wersja (cisza przy braku internetu/błędzie) — wzorzec 1:1 z `Radius Dimention Mover` |
| `TeklaWindowFocus.cs` | po realnym usunięciu przełącza fokus Windows na główne okno Tekla Structures (Win32 `SetForegroundWindow`, nie API Tekli - Open API nie ma metody do tego, zweryfikowane), żeby Ctrl+Z od razu trafił tam gdzie ma. **Nie wołać PRZED startem Pickera** — podejrzenie, że to psuje stan interaktywnej komendy Tekli, patrz "STAN NA 2026-09-23" |
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
  `dev` i ustawiony jako domyślny przez GitHub API w czasach, gdy `gh` nie
  był jeszcze zainstalowany na tej maszynie — od 2026-09-16 `gh` jest
  zainstalowany i zalogowany, patrz `..\CLAUDE.md`, sekcja "Narzędzia wokół
  repozytorium"). `dryRun: true` jest tu dozwolony i oczekiwany dopóki
  reguła nie przejdzie bramy bezpieczeństwa wyżej.
- `release` — ma trzymać tylko kod potwierdzony jako bezpieczny. W praktyce
  do tej pory `dev` i `release` zwykle kończą z identyczną treścią po
  serii PR-ów w obie strony (`dev`→`release` żeby promować, `release`→`dev`
  żeby zsynchronizować historię po merge commicie z GitHuba) — sprawdzaj
  `git diff origin/dev origin/release` zamiast zgadywać, czy się rozjechały.
  **Merge do `release` sam w sobie NIE jest potwierdzeniem bezpieczeństwa.**

  **STAN NA 2026-09-16 (wieczór): `dev` i `release` ZNOWU IDENTYCZNE, obie
  `dryRun: true` (bezpieczne).** To już drugi raz tego samego dnia, gdy
  branche się rozjechały i wróciły do zgodności - historia dla kontekstu:

  Pierwszy rozjazd (kod): PR #13 wniosło do `release` `dryRun: false`
  (BŁĘDNA poprawka, zanim znaleziono prawdziwą przyczynę PUŁAPKI 5). Po
  znalezieniu przyczyny, `dev` dostało `dryRun: true` z powrotem, a
  [PR #15](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/pull/15)
  (dev→release) + PR #16 (release→dev, sync) doprowadziły branche z
  powrotem do identycznej treści.

  Drugi rozjazd (dokumentacja, ten sam dzień): seria PR-ów porządkujących
  `README.md`/`CLAUDE.md`/`AGENTS.md` (usunięcie numerów rysunków i nazw
  projektów z README, otwarcie
  [issue #18](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/issues/18)
  z pełną diagnozą PUŁAPKI 5, naprawa sfabrykowanego mergea) trafiła na
  `dev` przez PR #19, #20, #21, a
  [PR #17](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/pull/17)
  (dev→release) + PR #22 (release→dev, sync) znowu zsynchronizowały
  branche. Zero zmian kodu w tej rundzie - tylko dokumentacja.

  **To NIE znaczy, że tak będzie zawsze - NIGDY nie zgaduj stanu branchy z
  tego opisu ani z ich nazw, niezależnie od tego, ile razy już się
  "zgodziły". Zawsze sprawdź `dryRun` w `MainForm.cs` wprost w plikach na
  branchu, z którego rzeczywiście korzystasz, i
  `git diff origin/dev origin/release`, żeby zobaczyć realną różnicę
  (jeśli jest) w chwili, gdy czytasz ten plik.**

## Następne kroki

**Historyczne kroki dotyczące PUŁAPKI 5 / reguły v4 (poniżej, w sekcji
"Historia: PUŁAPKA 5") są NIEAKTUALNE jako TODO — reguła v4 została
zastąpiona, nie naprawiona (patrz "STAN NA 2026-09-23" na górze pliku).
Aktualne priorytety, w tej kolejności:**

1. **Brama bezpieczeństwa dla reguły v5** (kasuj wszystko w widoku, bez
   dedupu). Dry-run na kilku różnych złączach → operator patrzy na żywy
   rysunek w Tekli → pytanie zadane BEZ podpowiadania odpowiedzi ("czy
   rysunek nadal opisuje wszystko, co musi opisywać, biorąc pod uwagę że
   te wymiary i tak mają zniknąć na rzecz wymiaru wcięcia?") → dopiero
   wtedy `dryRun: false`. Nie licz potwierdzeń z bramy v4 - inna reguła,
   własne pełne przejście.
2. **Wymiar wcięcia (cut fitting)** — pierwszy krok (`--diag-notch`,
   geometria bryły przez `Model.Part.GetSolid()`) zrobiony, patrz sekcja
   wyżej. Następny: zebrać dane z KILKU różnych złączy (różne kąty cięcia,
   nie tylko `[35021]`) i dopiero na zmierzonych danych zaprojektować,
   która ściana bryły jest ścianą cięcia i jak przeliczyć ją na 2D wymiar w
   widoku (`StraightDimension` ma konstruktor `(targetView, startPoint,
   endPoint, upDirection, distance)` - już zidentyfikowany, ale geometria
   punktów wejściowych jeszcze nie).
3. **UX wyboru widoku** — zaakceptowane 2026-09-23 jako "działa, gdy
   klikniesz w geometrię partu; Esc → lista jako zapasowa ścieżka".
   Operator świadomie zaakceptował ten kompromis (patrz "STAN NA
   2026-09-23"). Nie próbować dalej "naprawiać" klikania bez nowego
   wyraźnego zgłoszenia - `Picker.PickPoint` w tym środowisku ma
   potwierdzoną (dwukrotnie, przez `tasklist`) skłonność do wieszania się
   na klikach w puste miejsce, obejścia w publicznym API nie znaleziono
   (brak trybu "zaznacz obszarem" w `Picker.PickerTypes`).
