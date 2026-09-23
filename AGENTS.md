# AGENTS.md — baza wiedzy dla dowolnego asystenta AI w tym repo

Ten plik jest jedynym plikiem instrukcji w repo (2026-09-23: `CLAUDE.md`
usunięty na życzenie operatora — jeden plik wystarczy, mniej do
synchronizowania). Obowiązuje dla KAŻDEGO narzędzia: Claude Code, ChatGPT
Codex, GitHub Copilot, Qwen Coder, Cursor, Aider. **Przeczytaj to w całości
zanim zmienisz jakikolwiek kod w tym repo** — to pełna baza wiedzy: historia
nieudanych wersji reguły wykrywania, dokładny stan bramy bezpieczeństwa,
pułapki API, niedokończony research nad wymiarem wcięcia.

Nadrzędny `..\AGENTS.md` (środowisko Tekla Structures 2025, stos, konwencje,
pułapki API wspólne dla wszystkich projektów w `Projekty`) obowiązuje też
tutaj — to jest uzupełnienie specyficzne dla TEGO projektu. `README.md` to
krótki opis dla człowieka (i celowo NIE zawiera numerów rysunków ani nazw
siostrzanych projektów, repo jest publiczne — ten plik nie ma tego
ograniczenia, ma zostać szczegółowy, bo to baza do diagnozy).

## Najważniejsze zanim cokolwiek zrobisz

1. **To jest wtyczka do Tekla Structures 2025, która potrafi NAPRAWDĘ
   kasować dane w modelu.** Dwie wcześniejsze wersje reguły (v1, v2)
   realnie skasowały dobre wymiary na żywym modelu, zanim ktoś to
   zauważył. Historia niżej istnieje po to, żeby nie powtórzyć tych samych
   błędów pod inną postacią.
2. **Sprawdź `dryRun` w `MainForm.cs` (`RunButton_Click`) WPROST W PLIKU,
   nie z tego opisu, i sprawdź go NA BRANCHU, z którego faktycznie
   korzystasz** — `dev` i `release` mogą mieć RÓŻNY stan (patrz "Branche"
   niżej). **Stan na 2026-09-23: `dryRun: false` — przycisk NAPRAWDĘ
   kasuje**, brama bezpieczeństwa dla reguły v5 przeszła (patrz niżej).
   `DiagRunner.cs` (tryb konsolowy `--diag-*`) ma `dryRun` na sztywno
   `true` NA ZAWSZE, niezależnie od tego stanu — to się nigdy nie zmienia,
   to jedyna droga do bezpiecznego sprawdzenia reguły bez człowieka przy
   przycisku.
3. **NIE WALIDUJ REGUŁY PRZEZ JEJ WŁASNY DRY-RUN.** Najważniejsza lekcja z
   tego projektu (patrz PUŁAPKA 5 niżej) i powód, dla którego błąd przeżył
   trzy "czyste" testy: dry-run i realne kasowanie używają tego samego
   kodu, więc zawsze się zgodzą — także gdy oba są błędne. Waliduj
   pytaniem "czy po tej operacji rysunek nadal opisuje wszystko, co musi
   opisywać?", zadanym operatorowi BEZ podpowiadania odpowiedzi.
4. **Nigdy nie zgaduj progu/reguły detekcji ani geometrii "na wyczucie".**
   Każda stała w `RoAxisDimensionService.cs` ma komentarz skąd się wzięła
   (zmierzona, nie zgadana). Jeśli trzeba zmienić regułę albo dodać nową
   (np. wymiar wcięcia, patrz niżej) — najpierw zdobądź realne dane z
   dry-run/diagnostyki (`--diag-active`, `--diag-mark "[Mark]"`,
   `--diag-notch` — wszystkie zawsze bezpieczne, nigdy nie modyfikują
   rysunku ani modelu), dopiero potem pisz kod, który cokolwiek zmienia.
5. **Każda zmiana reguły detekcji/kasowania wymaga nowego przejścia bramy
   bezpieczeństwa od zera**, nawet jeśli poprzednia reguła była
   potwierdzona: dry-run → operator patrzy na żywy rysunek w Tekli w
   momencie kliknięcia → dopiero wtedy `dryRun: false`. Nie pytaj "czy mogę
   włączyć realne kasowanie/tworzenie" retorycznie — naprawdę czekaj na
   wyraźne "tak" od człowieka, konkretnie na TO pytanie.

## STAN NA 2026-09-23 — reguła v5, brama przeszła

**Reguła wykrywania została CAŁKOWICIE ZASTĄPIONA, nie tylko naprawiona.**
Zamiast grupować wymiary do osi i kasować "duplikaty" (v4, PUŁAPKA 5 niżej),
`RoAxisDimensionService.RemoveAxisDimensions` kasuje **KAŻDY** wymiar
dotykający osi w JEDNYM widoku wskazanym przez operatora, bez oceniania
"który jest ważniejszy". Powód: docelowo wymiar do osi ma być zastąpiony
osobnym wymiarem wcięcia (cut fitting, sekcja niżej) — skoro znikają
WSZYSTKIE, nie ma już decyzji "duplikat czy nie", więc **PUŁAPKA 5 (para
prostopadłych wymiarów 45° o tej samej wartości) przestaje dotyczyć tej
metody**. Cała stara logika grupowania (`GroupByProximity`,
`MinPointDistance`) została USUNIĘTA z kodu.

**Aktualna reguła — cała logika w `RoAxisDimensionService.RemoveAxisDimensions`:**

1. **`TouchesAxis`**: wymiar "dotyka osi", jeśli współrzędna, która MIĘDZY
   jego końcami się różni (Y albo Z — inaczej płaski wymiar 2D fałszywie
   łapie się jako "na osi", patrz v1 w historii niżej), jest bliska zeru na
   którymkolwiek końcu.
2. **Filtr długości własnej**: wymiar dotykający osi, ale dłuższy niż
   `SameJointDistanceMm` (300 mm) między własnym `StartPoint` a `EndPoint`,
   jest wykluczany z kandydatów — dwuznaczność oś/powierzchnia dotyczy z
   definicji tylko krótkiego, lokalnego wymiaru przy złączu, nie całkowitej
   długości profilu.
3. Każdy pozostały kandydat jest kasowany (albo tylko logowany w dry-run) —
   bez grupowania i bez wyboru "który zostaje".

**BRAMA DLA REGUŁY v5 PRZESZŁA 2026-09-23.** Operator zobaczył dry-run na
żywym `[35021]` (poprawnie znalazł `8 mm` i `21 mm`, zero fałszywych
trafień — dokładnie ten sam widok co w PUŁAPCE 5, teraz bez dedupu), i
wprost potwierdził na pytanie o realne kasowanie ("tak dryrun false").
Program realnie skasował oba wymiary na żywym rysunku, operator potwierdził
wizualnie (zrzut ekranu), że wygląda poprawnie.

**Wybór widoku:** przycisk w `MainForm.cs` woła `Picker.PickPoint`, żeby
operator kliknął widok w Tekli. **ZMIERZONE NA ŻYWO (2026-09-23,
dwukrotnie, potwierdzone przez `tasklist`): `Picker.PickPoint` w tym
środowisku potrafi zawiesić proces w nieskończoność**, jeśli operator
kliknie w widok bez trafienia w narysowaną geometrię (linię, wymiar,
kontur partu) — dla profili RO rysowanych bez kreskowania spora część
powierzchni widoku jest "martwa". Wymuszanie fokusu Windows na oknie Tekli
(`TeklaWindowFocus.BringToFront`) TUŻ PRZED startem pickera pogłębiało
problem (podejrzenie: ingerencja w stan "uzbrajania" interaktywnej komendy
Tekli) — usunięte stamtąd, `BringToFront` zostaje tylko PO operacji (do
Ctrl+Z). Obejście: **Esc w Tekli przerywa Picker**
(`PickerInterruptedException`), a `MainForm` łapie ten wyjątek i pokazuje
listę widoków z arkusza (`PickViewFromList`) jako gwarantowaną alternatywę.
**Operator zaakceptował ten stan 2026-09-23** ("klika tylko na parcie,
zostaw tak") — to znana, zaakceptowana wada UX, nie coś do "naprawienia"
bez nowego wyraźnego zgłoszenia. Nie znaleziono w publicznym API trybu
"zaznacz obszarem" (`Picker.PickerTypes` ma tylko: `OnePoint`, `TwoPoints`,
`ThreePoints`, `NPoints`, `Points`, `OneObject`, `NObjects` — zmierzone
reflection-only load), który by to obszedł.

**Fokus okna:** po dry-run (`dryRun: true`) fokus NIE wraca do Tekli — zostaje
w oknie programu, żeby operator od razu widział log. `TeklaWindowFocus.
BringToFront` na końcu operacji woła się TYLKO gdy `!dryRun` (coś realnie
skasowano, więc ma sens od razu wrócić do Tekli po Ctrl+Z).

**Skrót na pulpicie (stan lokalny na tym komputerze, NIE w repo):** od
2026-09-23 `RO Axis Dimension Remover.lnk` przekierowany na
`bin\x64\Debug\net48\RoAxisDimensionRemover.exe` (build z repo), NIE na
zainstalowaną kopię (`%LOCALAPPDATA%\Programs\RoAxisDimensionRemover\`,
która miała starą regułę v4 z 16 września — operator się na tym raz
przejechał, widząc nieaktualne zachowanie). Efekt: przebudowanie projektu
od razu aktualizuje to, co operator odpala z pulpitu — wygodne w tej fazie
częstych zmian reguły. Gdy reguła będzie gotowa do dystrybucji, rozważyć
przywrócenie skrótu na świeżo zbudowany instalator.

## Wymiar wcięcia (cut fitting) — W TRAKCIE, NIE DOKOŃCZONE

Program ma docelowo po skasowaniu wymiarów do osi dorysować nowy wymiar
(albo dwa: długość i szerokość) opisujący wcięcie (cut fitting) profilu w
złączu — żeby rysunek nie tracił informacji, tylko zmieniał jej formę.
**Kasowanie już działa (patrz wyżej), TWORZENIE nowego wymiaru jeszcze NIE
jest wpięte do przycisku** — cały research niżej jest w `DiagRunner.
RunNotchDiag`/`TryLogNotchCandidate`, czysto do odczytu, nic nie wstawia do
rysunku.

### Co już ustalone i zmierzone na żywo (`[35021]`, profil `RO42.4*3.2`)

1. **Rysunek→model→bryła działa**: `Part.ModelIdentifier` →
   `Model.SelectModelObject` → `Tekla.Structures.Model.Part` →
   `.GetSolid()` (wzorzec z oficjalnej dokumentacji Tekli, nie zgadany).
2. **Bryła tego złącza ma 26 ścian.** 24 to segmenty ścianki walca (dwa
   fazowane końce po 12 - bo ten konkretny Part to krótki kawałek ucięty
   na obu końcach). Ściana 25 (`Normal=(0,-1,0)`, dokładnie osiowa) to
   zwykłe proste przycięcie na dalszym końcu — nieistotne. **Ściana 26**
   (`Normal=(0; 0,94; -0,34)`) to faktyczna powierzchnia cięcia w złączu —
   jej normalna odpowiada DOKŁADNIE kątowi z adnotacji na rysunku
   (`19,90°`): `cos(19,9°)=0,94`, `sin(19,9°)=0,34`.
3. **PUŁAPKA odkryta i naprawiona w tej sesji: profil RO jest PUSTY W
   ŚRODKU (rura), więc ściana cięcia to PIERŚCIEŃ — ma DWIE pętle**
   (`Face.GetLoopEnumerator()` zwraca 2 obiekty `Loop`: obrys zewnętrzny +
   otwór wewnętrzny). Pierwsza wersja diagnostyki zlewała wierzchołki OBU
   pętli w jedną listę i próbowała szukać "przeciwległego" wierzchołka po
   indeksie (`i`, `i+N/2`) — dawało to bezsensowne dystanse (mylenie punktu
   z zewnętrznej pętli z bliskim punktem z pętli wewnętrznej). **Poprawka:
   logować/przetwarzać każdą pętlę OSOBNO**, brać tylko zewnętrzną (większy
   `LoopSpan` — max dystans między jej własnymi wierzchołkami).
4. **Na zewnętrznej pętli (12 wierzchołków) zmierzone poprawnie:**
   - Długość cięcia (najdłuższa cięciwa przez środek pętli): **45,09 mm**.
   - Szerokość cięcia (najkrótsza cięciwa przez środek): **42,40 mm**
     dokładnie — to średnica profilu (`RO42.4`), niezmieniona kątem
     nachylenia (kierunek prostopadły do nachylenia).
   - **Niezależna weryfikacja trygonometryczna zgadza się co do
     milimetra**: `42,4 mm / cos(19,9°) = 45,10 mm` ≈ zmierzone `45,09 mm`.
   - Ogólny, odporny na kolejność wierzchołków algorytm (nie zakłada, że
     "przeciwległy" = "N/2 dalej w liście"): dla każdej pary wierzchołków
     policz środek cięciwy i jego odległość od centroidu pętli; pary
     "przechodzące przez środek" to te z najmniejszym takim offsetem
     (z tolerancją); wśród nich najdłuższa = długość, najkrótsza = szerokość.
     Patrz `FindChord`/`Centroid`/`LoopSpan` w `DiagRunner.cs`.
5. **Przeliczenie punktów bryły (globalne współrzędne modelu) na
   współrzędne widoku, żeby dało się z nich zrobić `StraightDimension`:**
   `View.DisplayCoordinateSystem` — dokumentacja wprost mówi "can be used
   to transform global points from the model that is to be inserted in the
   view". `CoordinateSystem` (Origin + AxisX + AxisY, Vector) NIE ma
   gotowej metody transformacji w Open API — trzeba liczyć ręcznie:
   `relatywny = punkt - Origin`, potem rzut przez iloczyn skalarny na
   znormalizowane `AxisX`/`AxisY`/`AxisX.Cross(AxisY)`. Patrz
   `ToViewSpace` w `DiagRunner.cs`.
6. **Silna walidacja przeliczenia**: przeliczona "szerokość cięcia" w
   układzie widoku wyszła `(7,68;-21,20;0,00) -> (7,68;21,20;0,00)` —
   współrzędna X (7,68) niemal identyczna z X starego, skasowanego wymiaru
   `21 mm` (miał `End.X=7,7`), a rozpiętość Y (`±21,2`) to dokładnie promień
   profilu. To niezależne potwierdzenie, że transformacja model→widok jest
   poprawna (zgadza się z realną, wcześniej istniejącą geometrią rysunku).

### Co NIE jest jeszcze rozwiązane

- **Wymiar "długości cięcia" ma duży komponent głębi (Z) w tym
  konkretnym widoku**: przeliczone punkty to `(0,00;0,00;21,20) ->
  (15,35;0,00;-21,20)` — współrzędna Z (głębia, "w ekran") zmienia się o
  42,4 mm, a to, co widać na płasko (X) to tylko 15,35 mm. Ten konkretny
  widok jest ustawiony bokiem do tej osi cięcia, więc pełne 45 mm nie da
  się ładnie pokazać jako prosta pozioma/pionowa linia BEZ dodatkowej
  obróbki (inny kierunek pomiaru, może inny widok, może zaakceptować, że
  Tekla pokaże rzut jak przy starych wymiarach — `Dimension.Value` to
  zawsze rzut na kierunek pomiaru, nie odległość euklidesowa, patrz
  PUŁAPKA 2 niżej). "Szerokość cięcia" (42,4 mm) NIE ma tego problemu —
  leży całkowicie w płaszczyźnie widoku (Z≈0 na obu końcach).
- **Nic jeszcze nie wywołuje `StraightDimension.Insert()`.** Operator był
  pytany, czy spróbować realnie dorysować (przynajmniej samą "szerokość",
  co do której jest pewność) — sesja skończyła się przed odpowiedzią/próbą.
  **To pierwsza rzecz, którą trzeba zrobić w kolejnej sesji: albo dostać
  odpowiedź operatora, albo (jeśli kontekst jasno wskazuje kontynuację)
  spróbować wstawić TYLKO wymiar szerokości jako pierwszy, mały, odwracalny
  test (Ctrl+Z), i pokazać wynik operatorowi PRZED próbą z długością.**
- Reguła znajdowania "ściany cięcia" (>1 pętla + normalna nierównoległa do
  osi belki, `TSM.Beam.EndPoint - StartPoint`) sprawdzona na JEDNYM złączu
  (`[35021]`). Przed użyciem produkcyjnym: sprawdzić na kilku różnych
  złączach (różne kąty, może różne profile) — nie ufać jednemu przypadkowi,
  dokładnie tak samo jak przy regule kasowania (PUŁAPKA 5 była błędem
  uogólnionym z zbyt małej liczby przypadków).
- `StraightDimension` ma konstruktor `(targetView, startPoint, endPoint,
  upDirection, distance)` — `upDirection` i `distance` (offset linii
  wymiarowej od mierzonych punktów) jeszcze nie dobrane/przetestowane.

## Historia: PUŁAPKA 5 (dotyczyła reguły v4, ZASTĄPIONEJ przez v5 wyżej)

Para `21`/`21` na `[3.5013]` to NIE była duplikat, a dwa **PROSTOPADŁE**
wymiary tego samego skosu 45° — jeden mierzył offset poziomo (wzdłuż rury),
drugi pionowo (w poprzek). Oba pokazywały `21` tylko dlatego, że kąt to
dokładnie 45°. Reguła v4 (kasuj "duplikat", zostaw większą wartość) brała
je za duplikat i kasowała jeden — ginęła cała jedna informacja. **Ta reguła
już nie istnieje w kodzie** — v5 kasuje WSZYSTKIE wymiary do osi bez
wyjątku, więc pytanie "czy to duplikat" w ogóle już nie występuje.

**Najważniejsza lekcja z tamtej sesji, wciąż aktualna:** trzy "czyste"
testy nie złapały błędu, bo sprawdzały, czy realne kasowanie zgadza się z
PRZEWIDYWANIEM DRY-RUN tej samej reguły — to tautologia, zawsze się zgodzi,
także gdy oba są błędne. Do tego pytanie do operatora było zadane z
wbudowanym założeniem ("usuwa jeden z pary duplikatów, poprawnie?"), więc
potwierdzenie dotyczyło ramki, nie geometrii. **Waliduj przez pytanie "czy
rysunek nadal opisuje wszystko, co musi opisywać?", zadane BEZ
podpowiadania odpowiedzi.**

Historia zostaje jako kontekst diagnostyczny (i jako
[issue #18](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/issues/18)
na GitHubie), nie jako aktualne TODO.

### Historia nieudanych podejść do reguły kasowania (NIE powtarzać)

| Wersja | Błąd | Poprawka |
|---|---|---|
| v1 | `TouchesAxis` sprawdzał WSZYSTKIE współrzędne, nie tylko tę różniącą się między końcami — złapał prawidłowy wymiar "z boku rury" (jedna współrzędna = 0 dla obu końców, bo widok jest 2D) jako fałszywy duplikat. Skasował dobry wymiar na żywym modelu, `[35270]`. | Sprawdzać tylko współrzędną, która MIĘDZY końcami się różni. |
| v2 | Grupowanie po CAŁYM widoku zamiast po złączu — na `[3.5013]` (kilka złączy RO w jednym widoku ogólnym) zostawiał tylko jeden wymiar z całego widoku. Skasował dobre wymiary na żywym modelu. | Grupowanie po bliskości geometrycznej (próg 300 mm), nie po widoku. |
| v3 | Po poprawce v2 `[3.5013]` nadal traciło WSZYSTKIE wymiary do osi. | Zdiagnozowane w v4 przez odczyt logu diagnostycznego, nie przez zgadywanie. |
| v4, próba 1 (odrzucona) | Klaster bliskości mieszał 2 prawdziwe duplikaty (21 mm) z wymiarem całkowitej długości profilu (5796 mm), który też "dotyka osi" (lokalny początek układu współrzędnych rury leży na osi z definicji). Próba poprawki: kasować tylko wymiary o IDENTYCZNEJ wartości — zepsuło `[35270]` (tam para to 24/12 mm, RÓŻNE wartości, `12` był prawdziwym duplikatem). | Odrzucone — "różne wartości" samo w sobie nic nie mówi o duplikacie. |
| v4 | Filtr długości własnej naprawił [3.5013], ale reguła bazowa miała PUŁAPKĘ 5 (wyżej) — para prostopadłych wymiarów o identycznej wartości. Przeszedł bramę trzy razy (błędnie, patrz "najważniejsza lekcja" wyżej). | Zastąpione przez v5 (kasuj wszystko, bez dedupu) — patrz "STAN NA 2026-09-23". |

PUŁAPKA 2 (osobna, nie wersja): "krótszy" nie znaczy mniejszy surowy dystans
3D między `StartPoint`/`EndPoint` — `Dimension.Value` to RZUT rozpiętości na
kierunek wymiaru, nie odległość euklidesowa. Trzeba porównywać wyświetlaną
wartość, nie geometrię. Dotyczy też wymiaru wcięcia (sekcja wyżej) — punkty
oddalone głównie w kierunku prostopadłym do kierunku pomiaru dadzą mały
`Value`, mimo dużej rzeczywistej odległości 3D.

## Pułapki API specyficzne dla tego projektu

(Uzupełnienie do `..\AGENTS.md` — specyficzne dla wymiarów rysunkowych,
profilu RO i geometrii bryły, nie ogólne dla Tekla Open API).

- **`Dimension.Value` to kolekcja, nie liczba.** Trzeba iterować i szukać
  pierwszego elementu z właściwością `Value` dającą się sparsować jako
  liczba (obsługa mieszanego formatowania tekstu wymiaru) — zmierzone na
  `[35270]` przez zrzut refleksją. Patrz `GetDisplayedValue`.
- **Dla rury okrągłej lokalny punkt referencyjny (X=0) leży DOKŁADNIE na
  teoretycznej osi.** To nie jest awaria ani niejednoznaczność — to
  definicja układu współrzędnych profilu. Każdy wymiar odnoszący się do
  tego punktu (typowo: całkowita długość) będzie "dotykał osi" wg prostego
  testu współrzędnych, mimo że jest całkowicie poprawny.
- **`StraightDimension.StartPoint`/`EndPoint` są w jednostkach modelu (mm)
  w LOKALNYM układzie widoku**, nie mm na papierze i nie globalne
  współrzędne modelu — inna skala niż punkty z `Solid`/`Face`/`Loop`
  (te SĄ w globalnych współrzędnych modelu, duże liczby jak `-164387,4`).
  Żeby przejść z jednych na drugie, patrz `View.DisplayCoordinateSystem`
  wyżej (sekcja "Wymiar wcięcia").
- **`Picker.PickPoint` może zawiesić proces w nieskończoność** przy kliku w
  miejsce widoku bez ŻADNEJ geometrii w tolerancji — zmierzone na żywo
  2026-09-23, dwukrotnie, potwierdzone przez `tasklist`. Nie jest to
  kwestia fokusu okna — wymuszanie `SetForegroundWindow` na Tekli TUŻ PRZED
  startem pickera raczej pogłębiało problem niż go rozwiązywało. Jedyne
  potwierdzone wyjście z zawieszonego pickera to Esc w Tekli
  (`PickerInterruptedException`). `Picker` ma prywatny konstruktor
  (zmierzone reflection-only load: `GetConstructors()` publiczne zwraca 0,
  z `NonPublic` daje 1) — jedyna publiczna droga do instancji to
  `DrawingHandler.GetPicker()`, nie `new Picker(drawing)`.
- **`ViewBase.GetAllObjects()` działa na `ViewBase`, nie trzeba rzutować do
  `View`.** `DetailView`/`SectionView` nie są `View` (rzutowanie
  `pickedView as View` zawodziło dla widoków przekroju/detalu, mimo że
  Picker poprawnie zwracał widok) — operować na `ViewBase` wszędzie, gdzie
  to możliwe, rzutować w dół tylko gdy naprawdę trzeba coś specyficznego dla
  `View` (np. `.Name`, którego `ViewBase` nie ma).
- **`Face`/`Loop`/`FaceEnumerator`/`LoopEnumerator` są w namespace
  `Tekla.Structures.Solid`** (assembly `Tekla.Structures`), NIE w
  `Tekla.Structures.Model` mimo że `Solid.GetFaceEnumerator()` jest metodą
  `Model.Solid` — łatwo pomylić po przykładzie z dokumentacji, który ma
  osobny `using Tekla.Structures.Solid;`.
- **Profil PUSTY W ŚRODKU (np. RO = rura) → ściana cięcia to pierścień z
  DWIEMA pętlami (`Face.GetLoopEnumerator()` zwraca >1 `Loop`).** Nie zlewać
  wierzchołków różnych pętli w jedną listę — patrz sekcja "Wymiar wcięcia"
  wyżej, to był realny błąd w tej sesji (poprawiony). Zewnętrzny obrys =
  ten o większym rozstawie własnych wierzchołków.
- **`CoordinateSystem` (Origin/AxisX/AxisY) nie ma gotowej metody
  transformacji punktu do jej lokalnego układu** w publicznym Open API —
  trzeba liczyć ręcznie przez iloczyny skalarne (`Vector.Dot`), patrz
  `ToViewSpace` w `DiagRunner.cs`.

## Jak testować bez klikania w GUI

Automatyzacja (w tym agent AI) nie klika w przycisk `MainForm`. `Program.cs`
ma więc tryb konsolowy:

```
RoAxisDimensionRemover.exe --diag-active          # aktywny rysunek w Tekli, wszystkie widoki, reguła v5
RoAxisDimensionRemover.exe --diag-mark "[3.5013]" # otwiera rysunek po Mark, potem diagnostyka
RoAxisDimensionRemover.exe --diag-notch           # tylko odczyt: geometria bryły + kandydat na wymiar wcięcia
```

`dryRun` jest we wszystkich trzech na sztywno `true` w `DiagRunner.cs` — nie
da się tego przełączyć z linii poleceń. `--diag-notch` nawet nie ma pojęcia
`dryRun` — nic nie usuwa ani nie tworzy, tylko czyta model przez
`Tekla.Structures.Model.Model`. Log leci na `stdout` (przechwyć np.
`> plik.txt 2>&1` albo uruchom w tle i przeczytaj output). `--diag-mark`
woła `SetActiveDrawing(d, true)` — otwiera rysunek na ekranie.

`--diag-active`/`--diag-mark` przechodzą po WSZYSTKICH widokach na arkuszu
(Picker wymaga GUI) — inaczej niż przycisk w `MainForm`, który działa na
jednym widoku wybranym przez operatora.

Przed przebudowaniem: `taskkill /F /IM RoAxisDimensionRemover.exe`, jeśli
proces działa (patrz `..\AGENTS.md`, zasada o zamykaniu przed buildem) —
**szczególnie ważne przy debugowaniu Pickera: zawieszony `PickPoint`
blokuje proces, `taskkill` to jedyny sposób go zakończyć.**

## Struktura plików

| Plik | Zawartość |
|---|---|
| `RoAxisDimensionService.cs` | cała logika wykrywania i kasowania, zero UI (reguła v5 — kasuje wszystko w widoku) |
| `MainForm.cs` | UI: jeden przycisk, log do okna i do pliku (`dryRun: false` od 2026-09-23 — brama v5 przeszła). Wybór widoku: `Picker.PickPoint` (klik w Tekli), Esc → `PickViewFromList` (lista w oknie). Fokus na Teklę po operacji tylko gdy `!dryRun` |
| `Program.cs` | punkt wejścia; GUI domyślnie, `--diag-active`/`--diag-mark`/`--diag-notch` dla trybu konsolowego |
| `DiagRunner.cs` | headless runner dry-run + `RunNotchDiag`/`TryLogNotchCandidate` (research nad wymiarem wcięcia — geometria bryły, wybór ściany cięcia, przeliczenie na współrzędne widoku) — **świadomie trwały element projektu**, `dryRun` na sztywno `true` na zawsze, nie do usunięcia |
| `UpdateCheck.cs` | sprawdza w tle przy starcie, czy na GitHubie jest nowsza wersja (cisza przy braku internetu/błędzie) |
| `TeklaWindowFocus.cs` | przełącza fokus Windows na główne okno Tekla Structures (Win32 `SetForegroundWindow`, nie API Tekli). **Nie wołać PRZED startem Pickera** — podejrzenie, że to psuje stan interaktywnej komendy Tekli |
| `installer/setup.iss`, `installer/fetch-dependencies.ps1`, `installer/TeklaEULA.txt` | instalator Inno Setup — nie dołącza bibliotek Tekla, dociąga je z NuGet po instalacji |

`Inspector.cs` (tymczasowy skaner kandydatów RO po całym modelu, użyty
jednorazowo do znalezienia `[3.5013]`) **usunięty po v0.2.0** — nieużywany
od chwili, gdy oba rysunki testowe były już znane. Patrz git historia, jeśli
trzeba znaleźć kolejnego kandydata na innym modelu.

## Wydania (GitHub Releases) i wersjonowanie

- Wersja w `RoAxisDimensionRemover.csproj` (`<Version>`), `installer/setup.iss`
  (`MyAppVersion`) i tag na GitHubie muszą się zgadzać — `UpdateCheck.cs`
  porównuje assembly version z tagiem najnowszego release.
- Instalator budowany lokalnie: `dotnet build -c Debug -p:Platform=x64`
  (WAŻNE: `-p:Platform=x64` explicit, inaczej ścieżka wyjścia się nie zgadza
  z `setup.iss`), potem `ISCC.exe installer\setup.iss` (Inno Setup 6).
- Ostatnia opublikowana wersja: `v0.1.0` (pre-release, dry-run) →
  `v0.2.0`-`v0.2.3` (historia `dryRun` true/false w trakcie diagnozy PUŁAPKI
  5, dwie z nich BŁĘDNIE miały `dryRun: false`) →
  [v0.2.4](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/releases/tag/v0.2.4)
  (`dryRun: true`, opisuje jeszcze regułę v4). **Kod na `dev` jest od
  2026-09-23 znacznie nowszy niż v0.2.4 (reguła v5, `dryRun: false`,
  research nad wymiarem wcięcia) — numer wersji NIE był jeszcze podbity,
  żadna nowsza wersja nie została opublikowana.** Sama flaga pre-release
  na GitHubie nigdy nie była wiarygodnym sygnałem bezpieczeństwa w tym
  repo — nie ufać jej, sprawdzać kod.

## Branche i narzędzia repo

- `dev` — domyślny branch, rozwojowy/WIP. `release` — ma trzymać kod
  potwierdzony jako bezpieczny. W praktyce zwykle kończą z identyczną
  treścią po serii PR-ów w obie strony — **sprawdzaj
  `git diff origin/dev origin/release` zamiast zgadywać, czy się
  rozjechały, za KAŻDYM razem, niezależnie od tego, ile razy już się
  "zgodziły".** Merge do `release` sam w sobie NIE jest potwierdzeniem
  bezpieczeństwa.
- Merge pull requestów na GitHubie robi człowiek (operator), nie asystent —
  API do merge jest tu świadomie nieużywane, także przez `gh pr merge`.
  Commity na `dev` w tej sesji poszły bezpośrednio (bez PR) za zgodą
  operatora — to nie jest domyślny tryb, pytać, jeśli niejasne.
- **`gh` (GitHub CLI) jest zainstalowany i zalogowany** (`C:\Program Files\GitHub CLI\gh.exe`,
  konto `HoldFort-Bananza`, protokół HTTPS) — użyj `gh issue create`/`gh pr create`
  zamiast ręcznego REST API. Może nie być jeszcze na `PATH` w nowej sesji
  Bash (sprawdź `which gh` najpierw, wywołaj pełną ścieżką jeśli trzeba).
- **`README.md` celowo NIE zawiera numerów rysunków (Mark) ani nazw
  siostrzanych projektów** — świadoma decyzja operatora, bo repo jest
  publiczne. Ten plik może i powinien zachować konkretne dane (to baza
  wiedzy do diagnozy), ale jeśli edytujesz `README.md`, zachowaj ten sam
  brak identyfikatorów.

## Następne kroki

1. **Wymiar wcięcia — dokończyć.** Zacząć od pytania operatora (albo,
   jeśli sesja wyraźnie kontynuuje ten wątek, od razu spróbować): czy
   wstawić realnie SAMĄ szerokość cięcia (42,4 mm — ta, co do której mamy
   pewność, bez problemu głębi Z) jako pierwszy, mały, odwracalny test na
   żywym `[35021]`? Potem pokazać wynik, dopiero potem brać się za
   "długość" (ma problem z głębią w tym widoku, patrz sekcja "Wymiar
   wcięcia" wyżej — może wymagać innego podejścia niż prosta linia).
2. **Reguła wyboru ściany cięcia sprawdzona na JEDNYM złączu.** Przed
   wpięciem do przycisku: potwierdzić na kilku różnych złączach (różne
   kąty, ew. różne profile), że heurystyka ">1 pętla + normalna
   nierównoległa do osi belki" trafia poprawnie za każdym razem.
3. **Brama bezpieczeństwa dla TWORZENIA wymiaru** — osobna od bramy dla
   kasowania (już przeszła). Dry-run/podgląd (na razie: sam log w
   `--diag-notch`) → operator patrzy na żywy rysunek PO próbie wstawienia
   → potwierdza, że wygląda sensownie → dopiero wtedy wpiąć do przycisku
   na stałe.
4. **UX wyboru widoku** — zaakceptowane 2026-09-23 jako "działa po
   kliknięciu w geometrię partu; Esc → lista jako zapasowa ścieżka". Nie
   próbować dalej "naprawiać" bez nowego wyraźnego zgłoszenia operatora.
