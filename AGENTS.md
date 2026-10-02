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
   niżej). **Stan na 2026-10-01 (v0.3.8): `dryRun: false` — przycisk
   NAPRAWDĘ kasuje**, reguła v6 przeszła bramę 2026-09-23 (historia: wiki `10-Dziennik-2026-09`).
   `DiagRunner.cs` (tryb konsolowy `--diag-*`) ma `dryRun` na sztywno
   `true` NA ZAWSZE, niezależnie od tego stanu — to się nigdy nie zmienia,
   to jedyna droga do bezpiecznego sprawdzenia reguły bez człowieka przy
   przycisku.
3. **NIE WALIDUJ REGUŁY PRZEZ JEJ WŁASNY DRY-RUN.** Najważniejsza lekcja z
   tego projektu (PUŁAPKA 5, wiki `10-Dziennik-2026-09`) i powód, dla którego błąd przeżył
   trzy "czyste" testy: dry-run i realne kasowanie używają tego samego
   kodu, więc zawsze się zgodzą — także gdy oba są błędne. Waliduj
   pytaniem "czy po tej operacji rysunek nadal opisuje wszystko, co musi
   opisywać?", zadanym operatorowi BEZ podpowiadania odpowiedzi.
4. **Nigdy nie zgaduj progu/reguły detekcji ani geometrii "na wyczucie".**
   Każda stała w `RoAxisDimensionService.cs` ma komentarz skąd się wzięła
   (zmierzona, nie zgadana). Jeśli trzeba zmienić regułę albo dodać nową
   (np. wymiar wcięcia, wiki `10-Dziennik-2026-09`) — najpierw zdobądź realne dane z
   dry-run/diagnostyki (`--diag-active`, `--diag-mark "[Mark]"`,
   `--diag-notch` — wszystkie zawsze bezpieczne, nigdy nie modyfikują
   rysunku ani modelu), dopiero potem pisz kod, który cokolwiek zmienia.
5. **Każda zmiana reguły detekcji/kasowania wymaga nowego przejścia bramy
   bezpieczeństwa od zera**, nawet jeśli poprzednia reguła była
   potwierdzona: dry-run → operator patrzy na żywy rysunek w Tekli w
   momencie kliknięcia → dopiero wtedy `dryRun: false`. Nie pytaj "czy mogę
   włączyć realne kasowanie/tworzenie" retorycznie — naprawdę czekaj na
   wyraźne "tak" od człowieka, konkretnie na TO pytanie.

## START SESJI TUTAJ — stan na 2026-10-01

Dziennik 23.09–29.09 jest na wiki (`10-Dziennik-2026-09`), 30.09–01.10 w „NA DALEJ” niżej; ta sekcja jest skrótem
aktualnego stanu. Przy sprzeczności wygrywa KOD, potem ta sekcja, potem
starsze wpisy dziennika.

**Wydanie:** v0.3.10 (2026-10-01) = v0.3.9 + długość wcięcia odsuwana od
sąsiedniego widoku (PR #62, NA DALEJ pkt 29); instalator
`installer\output\RoAxisDimensionRemover-Setup-v0.3.10.exe`. Wcześniej
v0.3.9 (2026-10-01) = v0.3.8 + Shift + klik na „Usuń” czyści
wszystkie widoki rysunku (PR #56), reguły bez zmian. Wcześniej
v0.3.8 (2026-10-01) = szerokości wcięcia odsuwane na zewnątrz swojego
końca rury (PR #51, koniec nakładania na `[35019]`).
Lista wszystkich wersji — sekcja „Wydania” niżej. Skrót z pulpitu wskazuje
na zainstalowaną kopię (NIE na `bin`) — nowy build testować przez
`bin\x64\Debug\net48\RoAxisDimensionRemover.exe`. Wersje z błędem
kasującym dane: v0.3.2 (rozciąganie zjadało całkowitą długość), v0.3.3
(łuki), v0.3.5 i starsze (całkowita długość krótkich rur) — v0.3.2 na wiki (`10-Dziennik-2026-09`), pozostałe niżej.

**Co robi program (dwa osobne przyciski):**
1. „Usuń wymiary do osi” — jeden widok wskazany kliknięciem; guardy:
   `SinglePartDrawing` + część o profilu `RO…` w widoku + brak giętej
   części w widoku (od v0.3.4); reguła v6
   (`TouchesAxis` z wymogiem głębi Z + filtr długości własnej 300 mm +
   od v0.3.6 oba końce wymiaru w obrębie jednej ściany cięcia, `CutZones`).
   **Shift + klik** (od 2026-10-01, prośba operatora, opcja — nie
   domyślne): ta sama `RemoveAxisDimensions` po kolei na każdym widoku
   arkusza (`DiagRunner.SheetViews`, jak `--diag-find-candidates`), bez
   pytania o widok. Każdy widok zatwierdza się osobno — Ctrl+Z cofa widok
   po widoku (operator: OK). Nie da się nim pominąć widoku (przypadek
   `[35020]`) — wtedy zwykły klik.
2. „Wstaw wymiar wcięcia” — `NotchPilot.InsertMissing`, cały rysunek:
   guard `SinglePartDrawing`; gięte części pomijane (od v0.3.4); ściany
   cięcia ≥ 6,5° (od v0.3.5; wcześniej 10°); długość i szerokość
   wcięcia w widoku, gdzie cięciwa jest płaska; styl z widoku docelowego,
   a gdy pusty — z innego widoku; `StretchRadiusToDiameter` zamienia płaski
   promień przy skosie (czubek cięcia → oś, mierzony w poprzek; od v0.3.6
   koniec na osi może być gdziekolwiek) na średnicę od czubka do czubka; `HasSameDimension`
   porównuje końce i `UpDirection`, więc ponowne kliknięcie nic nie dubluje.

**Potwierdzone na żywo przez operatora (pytanie „czy rysunek opisuje
wszystko”), 22 rysunki:** `[35021]`, `[3.5013]` (znów jest w modelu —
wcześniejsze wpisy mówią, że usunięty; wrócił), `[3.5027]`, `[35095]`,
`[35020]`, `[35092]`, `[35260]`, `[35598]`, `[35424]`, `[35572]`,
`[35101]`, `[35066]`, `[35016]`, `[35010]`, `[35085]`, `[35091]`,
`[35098]`, `[35028]`, `[35052]`, `[3.5029]`, `[35027]`, `[35019]`
(szczegóły w „NA DALEJ”). Rysunek zespołu `[225.130]` i blacha `[21050]`
— poprawnie odrzucone.

**Znane słabości (wszystkie zaakceptowane przez operatora):**
- Położenie wstawionych wymiarów (`side` + `Distance` wzorca) bywa złe:
  przy krawędzi arkusza (`35` na `[35095]`), na rurze (`42` na `[35021]`),
  w cudzej ramce (`15` na `[35020]`). Najczęstsza poprawka ręczna.
  Nakładanie dwóch szerokości na siebie naprawione w v0.3.8 (NA DALEJ
  pkt 10). Długość w cudzej ramce — poprawka na gałęzi
  `length-up-direction` (NA DALEJ pkt 29), wydane w v0.3.10.
- Widok zawierający same wymiary do osi zostaje po „Usuń” pusty — operator:
  takiego widoku się nie klika. „Wstaw” i tak go uzupełni stylem z innego
  widoku.
- Zagadka 24.09 (odrzucony koniec 45° na `[3.5013]`) nierozwiązana.
- Picker wisi przy kliku w pustą część widoku — Esc.
- Dwa `42` w jednym widoku przy 45° to NIE duplikaty (sprawdzone
  2026-09-29 na 12 rysunkach): średnica i długość cięcia, które przy 45°
  mają tę samą wartość. Szerokość wcięcia zawsze trafia do innego widoku.
- „Usuń” i „Wstaw” to osobne przyciski, nie jeden przepływ. Pierwotna wizja
  (skasuj i od razu wstaw) niezrealizowana — nie robić bez prośby operatora.

**Próg kąta cięcia 10° → 6,5° (2026-09-30, v0.3.5) — szara strefa
8–9°.** Skan 29.09: `[35092]` 8,7°, `[35260]`
  8,8°, `[35424]` 8,7°, `[35572]` 8,8°, `[35598]` 8,4° — przy 10° „Usuń”
  kasował tam wymiary do osi, a „Wstaw” nic nie dodawał. Operator na żywym
  `[35092]`: koniec 8,7° jest ścięty, `7` (długość skosu 6,52) dobre, `3`
  złe („pół ścięcia”). 6,5° = połowa między 4,8° (płaskie) a 8,4°.
  Dry-run na pięciu rysunkach: przy 10° zainstalowana kopia nic nie
  dodaje, przy 6,5° dochodzi szerokość (`42`/`48`) i długość skosu
  (`7`), a na `[35424]`/`[35598]` długości nie dubluje (płaski wymiar już
  jest). Na `[35598]` szerokość lekko po skosie (rura ze spadkiem 4 mm w Z),
  wartość i tak `48`. **Brama przeszła na `[35092]`:** Usuń w dolnym widoku
  (znikły `42`/`3`/`21`/`21`) → Wstaw (4 wymiary: `42`, `42`, `7`, `42`) →
  niezależny odczyt zgodny z dry-runem → operator: „ma wszystko”.
  Pozostałe cztery rysunki — tylko dry-run, nie oglądane na żywo.

**Środowisko i pułapki, które kosztowały czas:**
- Model testowy (~19 tys. części) NIE zapisuje się (limit licencji 2500) —
  zmiany na rysunku znikają po zamknięciu. Wygodne do testów.
- Każdą nową operację zapisu weryfikować ODCZYTEM Z OSOBNEGO PROCESU
  (`--diag-dimension-style`), nie logiem programu — `Modify()` z nowymi
  punktami i odczyt w tym samym procesie kłamały.
- `--diag-* "[Mark]"` nie przeładowuje już otwartego rysunku
  (`OpenUnlessActive`) — wcześniej cofało niezapisane zmiany operatora.
  Otwarcie INNEGO rysunku przez diag nadal zamyka bieżący.
- `--diag-find-candidates` (cały model, 2298 rysunków) trwa > 1 h (30.09,
  Tekla urosła do 6 GB) — tylko w tle z wyjściem do pliku. Do porównań reguł
  `--diag-find-candidates <plik z Mark>`: 61 rysunków z kandydatami
  (`~/scans/marks.txt`) w 4,5 min. Pętla `--diag-notch-fill-dryrun` po tych
  61 to ~1 min/rysunek, a rysunki nieaktualne względem modelu pomija
  (9 z 31 na 01.10) — `--diag-find-candidates` je liczy.
- `taskkill` przed buildem zamyka też program operatora — uprzedzić
  (01.10 dwa razy zapomniane). Commit/stash z `bin/*.exe` też się wywala,
  gdy program działa.
- Operator czasem ma w Tekli inny rysunek niż ten, który otworzyła
  diagnostyka — sprawdzać Mark w nagłówku jego logu.
- Zadanie w tle w Claude Code ma limit 2 h i potem jest zabijane. Pętla
  `--diag-notch-fill-dryrun` po 61 rysunkach dla DWÓCH buildów to ~2 h —
  01.10 urwała się na 56. rysunku. Dzielić listę albo puszczać jeden build
  i porównywać z zapisanym wynikiem w `scans/`.
- Diagnostyka otwierająca INNY rysunek zamyka ten, na którym operator
  właśnie testuje — nie puszczać skanów w trakcie testu na żywo.
- 30.09 komputer dwa razy padł (bugcheck `0x133`, błąd `nvlddmkm`) przy
  otwieraniu rysunku przez diagnostykę; w tle działała animowana tapeta
  (Lively/mpv, ~27% GPU, ma autostart). Po jej zamknięciu i czystej
  reinstalacji sterownika NVIDIA — spokój. Jeśli wróci: najpierw Lively,
  potem `%USERPROFILE%\gpu-log.ps1` (logger karty co 2 s, wyłączony).
- Wyjście konsoli jest w cp1250 — czytać przez `iconv -c -f cp1250 -t utf-8`.
  Bez `-c` iconv urywa wyjście na znaku spoza cp1250 (`≈` w
  `--diag-notch-raw`). Porównując dwa buildy, diffować surowe bajty.
- 2026-09-29 (po v0.3.3): refaktor bez zmiany reguł — geometria tylko w
  `NotchPilot`, `DiagRunner` z niej korzysta. Sprawdzone porównaniem
  wyjścia starego i nowego builda na `[35095]`/`[35021]`/`[35020]`: wszystkie
  tryby `--diag-*` identyczne co do bajtu (poza celowymi zmianami tekstu w
  `--diag-notch` i `--diag-notch-match`).
- Przy prośbie do operatora nazywać widok po tym, co w nim widać („górny,
  z `256`”), nie po numerze z logu — numeracja widoków w diagnostyce nie
  odpowiada położeniu na arkuszu (pomyłka z 29.09 na `[35020]`).

**Przegląd 2026-09-29 po południu (PR #36, wydane w v0.3.4):**
- **Gięte rury (łuki poręczy, `Bogen`) — ZNALEZIONA UTRATA DANYCH W v0.3.3.**
  Na `[35681]` reguła v6 oznaczała 10 z 15 wymiarów jedynego zwymiarowanego
  widoku (m.in. `63` i `29`, opisujące gięcie): oś Start→End to cięciwa
  łuku, więc prawie każdy punkt ma „głębię Z” przy osi. 13 z 74 rysunków
  z kandydatami to łuki: `[25031]`, `[35555]`, `[35577]`, `[35588]`,
  `[35599]`, `[35603]`, `[35638]`, `[35674]`, `[35675]`, `[35677]`,
  `[35678]`, `[35679]`, `[35681]`. Na `[35603]` ściana 13,7°
  łuku dostałaby wymiar wcięcia wzdłuż złej osi. Poprawka:
  `NotchPilot.IsStraight` (`GetCenterLine(false)`: prosta rura 2 punkty,
  łuk 7 punktów na łuku — zmierzone; prosta = wszystkie odcinki osi w tym
  samym kierunku, 0,5°), widok z giętą częścią pomijany w
  `RemoveAxisDimensions`, gięta część pomijana w `InsertMissing`. Pierwsza
  wersja (odległość osi od prostej ≤ 1 mm) przepuściła krótki łuk
  `[35678]` (~17 mm, gięty o ~17°). Ponowny pełny skan: 74 → 61 rysunków
  z kandydatami, wypadły dokładnie te 13 łuków, na pozostałych liczby
  identyczne co do jednej. **W v0.3.3 i starszych na widokach łuków NIE klikać
  „Usuń”.**
- Diagnostyka: dry-run `InsertMissing` podaje widok (`Origin`),
  `--diag-notch-raw` kąt cięcia każdej ściany i oś części, skan modelu
  kolumnę „ściany odrzucone filtrem kąta”; `--diag-* "[Mark]"` na rysunku
  nieaktualnym względem modelu (`[3.5027]`) wypisuje komunikat zamiast
  wyjątku.
- Instalator sprawdzony: `fetch-dependencies.ps1` dociąga dokładnie 23
  DLL-e, które kopiuje build — nic nie brakuje, nic zbędnego.

**BŁĄD W v0.3.5 I STARSZYCH (znaleziony 2026-09-30 na `[35260]`): „Usuń”
kasował całkowitą długość KRÓTKIEJ rury.** Rura 105 mm: wymiar `105`
(0 → 104,95, jeden koniec w głębi Z, drugi w płaszczyźnie widoku)
spełniał `TouchesAxis` i mieścił się w filtrze 300 mm. Poprawka
(`RoAxisDimensionService.CutZones`/`InCutZone`): kasujemy tylko wymiar,
którego OBA końce leżą wzdłuż osi w obrębie jednej ściany cięcia (rzut
zewnętrznej pętli ściany na oś w układzie widoku). Skan 61 rysunków z
kandydatami (lista w `~/scans/marks.txt`, tryb `--diag-find-candidates
<plik>`): 230 → 224, wypadło 6 wymiarów — `105` `[35260]`, `143`
`[2.5048]`, `93` `[35659]`, `46` `[2.5142]` (wszystkie od X=0, czyli
całkowite długości), `25` `[35244]` (odległość śruby od krawędzi blachy)
i `112` `[35076]` (od krawędzi belki do początku ścięcia, belka 154) —
oba ocenione przez operatora 2026-09-30 jako potrzebne. Wszystkie 6
wypadłych wymiarów MA przetrwać. Na pozostałych 55 rysunkach liczby bez zmian. Brama przeszła
na `[35260]`: skasowane tylko `4`, `105` przetrwało.

**Rozciąganie promienia: zdjęty warunek „koniec na osi w obrębie cięcia”**
(2026-09-30). Odrzucał promień `24` na `[35260]` — od czubka do osi na
DRUGIM końcu krótkiej rury (operator: „24 to powinno być 48”). Wymiary
całkowitej długości odrzuca już sam warunek `Up` wzdłuż osi. Skan 61
rysunków: 22 → 23 rozciągnięcia, jedyne nowe to `[35101]` (`17` → `34`).
Brama przeszła na `[35101]`: „tak jest dobrze na tym rysunku”.

**NA DALEJ — dziennik testów na żywo (stan 2026-10-01):**
1. Obie poprawki wyżej wydane w v0.3.6. Operator ma zainstalowaną v0.3.9
   (01.10). Sprawdzenie wersji:
   `(Get-Item "$env:LOCALAPPDATA\Programs\RoAxisDimensionRemover\RoAxisDimensionRemover.exe").VersionInfo.ProductVersion`.
2. Próg 6,5° wydany w v0.3.5. Cała szara strefa sprawdzona na żywo
   (2026-09-30, v0.3.6, ocena operatora „tak”): `[35092]`, `[35260]`,
   `[35598]`, `[35424]`, `[35572]`. Wzorzec za każdym razem ten sam: „Usuń”
   kasuje `24` i `4` (pół ścięcia), całkowita długość przetrwa, „Wstaw”
   dodaje `48` i — jeśli brak — `7` (pełne ścięcie).
3. `[35066]` sprawdzony na żywo (2026-09-30, v0.3.6): rura 550, ścięcia
   63,9° (długie, do X=89,73) i 25,1°. „Usuń” skasował `72` i `18` (punkt
   w połowie krawędzi ścięcia — operator: „do wywalenia”), `90`, `11`,
   `11`, `21`, `10`; łańcuch `21`+`11`+`11` się nie rozsypał, promień
   przetrwał. „Wstaw”: `90` i `20` (długości), `42` ×2 (szerokości),
   promień → `42`. Średnica jest więc na rysunku 3 razy — szerokość ścięcia
   okrągłej rury to ZAWSZE średnica. Operator: zostawić bez zmian (opcje
   „średnica raz na rysunek” / „raz na widok” odrzucone). Szerokość przy
   stromym ścięciu ma końce przesunięte o 10,6 mm wzdłuż rury (wartość OK).
4. `[35016]` sprawdzony na żywo (2026-09-30, v0.3.6, „jest dobrze”): rura
   856, ścięcia 19,9° i 45°. Skasowane `8`, `42` (pełna długość ścięcia
   45°, ale z końcem na osi — „Wstaw” dodał ją z powrotem w drugim widoku),
   pięć `21`; `856` i `15` przetrwały.
5. `[35010]` sprawdzony na żywo (2026-09-30, v0.3.6, „ma wszystko”): rura
   2677, oba końce 45°. Skasowane dwa `21` do osi, `2677` i `42` (długość
   ścięcia) przetrwały; oba promienie → `42`, „Wstaw” dodał długość i dwie
   szerokości. Lista „proponowanych” rysunków z 29.09 wyczerpana.
6. Rysunki „Einzelteil Bogen” z kandydatami (`[35085]`, `[35086]`, `[35091]`)
   to PROSTE rury (oś z 2 punktów, `Handlauf RO42.4`) — „Bogen” to tylko
   nazwa rysunku, guard łuków działa poprawnie. Na żywo (2026-09-30,
   v0.3.6, „opisuje wszystko”): `[35085]` (550, 63,9°/25,1°, lustro
   `[35066]`), `[35091]` (537, dwa 45°, długości `42` już były), `[35098]`
   (407, 64,3°/39,6°, rura ze spadkiem; łańcuch `19`+`352`+`20`+`16`
   malał po jednym, `352` przetrwało samo — ma >300 mm).
7. Log w oknie programu (2026-09-30, prośba operatora): czyści się dopiero
   przy operacji na INNYM rysunku (`MainForm.BeginLog`, porównanie Mark),
   nie przy każdym kliku. Log zostaje po przełączeniu rysunku aż do
   pierwszego kliku — operator: „zostawmy tak”, bez zegara.
8. Dalsze testy na żywo (2026-09-30 po południu, v0.3.6, „opisuje”):
   `[35028]` (537, 45°/44,8°; płaskie `23` — od osi na końcu do punktu w
   połowie ścięcia — ZOSTAJE, operator bez uwag), `[35052]` (537, dwa 45°;
   `42` do osi skasowane w jednym widoku, „Wstaw” dodał je w drugim),
   `[3.5029]` (537, 44,8°/45°, lustro `[35028]`).
9. **Koniec poniżej progu kąta może zostać bez wymiarów** — `[35027]`
   (373, 44,9°/4,8°, 2026-09-30): „Usuń” skasował `21`, `21`, `2` przy
   końcu 4,8° (cut-zone liczy KAŻDĄ ścianę cięcia, filtr kąta dotyczy tylko
   wstawiania), „Wstaw” nic tam nie dodał; zostały `373` i adnotacja kąta
   `4.75°`. Operator: rysunek opisuje wszystko. `373` ma głębię Z i dotyka
   osi — chroni go tylko filtr 300 mm, cut-zone też by go odrzucił.
10. `[35019]` (806, 19,9°/45°, 2026-09-30, „przeszedł”): `15` i `42` do osi
   skasowane w jednym widoku, „Wstaw” dodał je w drugim. **Nowy przypadek
   złego położenia: dwie szerokości `42` (po jednej na koniec) wstawione
   jedna na drugiej** — ten sam kierunek odsunięcia (`Up`=(1,0,0)), więc
   ta sama strona rury; operator przesunął ręcznie. Znana słabość `side`/
   `Distance`, tym razem z nakładaniem.
   **NAPRAWIONE 2026-10-01 (PR #51):** szerokość dostaje `Up` od środka
   rury (`Beam.StartPoint`/`EndPoint`) w stronę swojego końca — tak jak
   wymiary, które Tekla stawia sama przy końcach (`+oś` przy dalekim,
   `-oś` przy bliskim, zmierzone na `[35019]`). Długości bez zmian. Skan
   61 rysunków: liczby identyczne z v0.3.6. Dry-run na 31 z nich: to samo
   nakładanie miały też `[35052]`, `[35095]`, `[35098]`, `[35660]`,
   `[35662]` (pierwsze trzy oceniane wcześniej jako „opisuje” — nakładanie
   przeoczone). Brama na żywym `[35019]`: szerokości po obu stronach rury,
   niezależny odczyt zgodny z dry-runem, operator: „ma wszystko opisane”.
11. `[35660]` (2026-10-01, v0.3.8, „yep”): `Leiter` `RO48.3*3.6`, rura 93,
   oba końce 22,6°. „Usuń” w widoku z wymiarami: `24`, `24`, `10`, `10`
   do osi; `93` przetrwało. „Wstaw”: dwie długości `20` w tym widoku, dwie
   szerokości `48` w pustym widoku (styl z innego widoku) — po przeciwnych
   stronach rury, drugi przypadek poprawki v0.3.8. Obie `20` mają `Up` w
   tę samą stronę, więc stoją jedna nad drugą (nie nakładają się); ciasno,
   ale operator: „nie jest źle” — ładniej wymagałoby przesuwania widoków.
12. `[35662]` (2026-10-01, v0.3.8, „opisuje wszystko”): lustro `[35660]`,
   rura 91. Ten sam wynik: skasowane `24`, `24`, `10`, `10`; `91`
   przetrwało; dwie `20` jedna nad drugą, dwie `48` po przeciwnych stronach
   rury. Stare `10` miały `Up` w dół — nowe `20` i tak idą w górę (`perp`
   z osi, nie z wzorca).
13. `[35086]` (2026-10-01, v0.3.8, „wszystko potrzebne”): `Handlauf`
   `RO42.4*3.2`, rura 407 (prosta mimo „Bogen”), 64,3° (do 89,65) /
   39,6°, lustro `[35098]`. Usuń w obu widokach: `3`, `90`, `21`, `21`
   (widok zostaje pusty) oraz `11`, `21`, `21`, `20`, `19`; `407` i
   płaskie `35` (pełna długość cięcia 39,6°) przetrwały. Wstaw: płaskie
   `90` w widoku z `407`, dwie `42` w pustym widoku po przeciwnych stronach
   (trzeci przypadek v0.3.8). Adnotacje Tekli `9.04°`/`5.18°` w widoku
   szerokości to kąty pozorne w rzucie, jak na `[35095]`.
14. `[35067]` (2026-10-01, v0.3.8, „jest git”): `Handlauf` `RO42.4*3.2`,
   rura 936, 19,9° / 25,1°. Usuń: `10`, `8`, cztery `21` — widok zostaje
   pusty. Wstaw: długości `20` i `15` w tym pustym widoku (styl z widoku z
   `936`), dwie `42` w widoku z `936` po przeciwnych stronach. **Drugi
   przypadek (po `[35020]`) „pusty widok → wymiar w cudzej ramce”:** `20` i
   `15` wylądowały w ramce widoku z `936` (`Distance` po insercie 421 i
   329). Operator obniża ręcznie.
15. `[35029]` (2026-10-01, v0.3.8, „jest dobrze”): rura 537, 44,8°/45°,
   jak `[35028]`. Usuń: cztery `21` (widok pusty) oraz `42` i `21` do osi;
   `537` i płaskie `23` przetrwały. Wstaw: dwie długości `42` w pustym
   widoku — tym razem we własnej ramce — i dwie szerokości `42` po
   przeciwnych stronach.
16. `[35014]` (2026-10-01, build z repo, „ma wszystko”): `Gelaender`
   `RO42.4*3.2`, rura 4350, oba końce 45°. **Pierwszy test trybu Shift**
   („Usuń” z Shift = wszystkie widoki): widok 1 — sześć `21` do osi,
   widok 2 (z `4350`) — nic, zgodnie z `--diag-active`. Wstaw: dwie
   długości `42` w opróżnionym widoku (we własnej ramce), dwie szerokości
   `42` po przeciwnych stronach.
17. `[35019]` ponownie (2026-10-01, Shift, „yep”): drugi test Shift. Ten sam
   zestaw co rano przy klikaniu widoków po kolei (skasowane `15`, `42` /
   `21`, `21`, `8`, `21`; wstawione te same 4 wymiary, te same końce).
   Inne tylko `Distance` po insercie — prawa długość `42` z dolnego widoku
   dotyka napisu szerokości `42` w górnym (znana słabość położenia, nie
   Shift).
18. `[35068]` (2026-10-01, Shift, „jest dobrze”): **pierwsza rura
   `RO33.7*3.2`** (`Knielauf`), 587, 19,9° / 39,8°. Usuń: `17`, `17`, `14`,
   `6` do osi, `587` przetrwało. Wstaw: długości `28` i `12` w widoku z
   `587`, dwie szerokości `34` po przeciwnych stronach w pustym widoku —
   wartości z geometrii tej średnicy, reguła nie zakłada 42,4.
19. `[35030]` (2026-10-01, v0.3.9 z instalatora, Shift, „yep”): rura 373,
   44,9° / 4,8°, lustro `[35027]`. Usuń: `2` (koniec 4,8°) oraz `21`, `21`
   (44,9°) i `4` (4,8°); `373` i płaskie `21` (promień przy końcu 4,8°)
   przetrwały. Wstaw: tylko koniec 44,9° — szerokość `42` w widoku z
   `373`, długość `42` w opróżnionym widoku. Koniec 4,8° bez wymiarów
   cięcia, jak na `[35027]` (pkt 9).
20. `[35288]` (2026-10-01, v0.3.9, Shift, „opisuje”): `Leiter`
   `RO48.3*3.6`, rura 3186, jedno cięcie 22,6° (drugi koniec prosty).
   **Łańcuch `10`+`10`+`3166` (jeden `StraightDimensionSet`, 3 elementy):
   skasowane oba `10`, łańcuch malał 3→2→1, `3166` przetrwał** — kolejny
   przypadek bez kaskady. Wstaw: szerokość `48` w widoku z `3186`, długość
   `20` w widoku z `3166`. **Trzeci przypadek „wymiar w cudzej ramce”**,
   tym razem widok NIE był pusty: długość dostaje zawsze `Up` = +prostopadła
   do osi, a wzorzec `3166` w tym widoku miał `Up` w dół — `20` poszło w
   stronę sąsiedniego widoku. Trop do analizy położenia: zwrot `Up` długości
   brać ze wzorca w widoku, nie zawsze +.
21. `[35099]` (2026-10-01, v0.3.9, Shift, „jest wszystko”): rura 190
   (krótsza niż filtr 300 mm), jedno cięcie 45°. Usuń: trzy `21` do osi;
   `190` przetrwało — płaskie (Z=0 na obu końcach), więc `TouchesAxis` go
   nie łapie; jego łańcuch 2→1 bez kaskady. Wstaw: długość `42` nad `190`,
   szerokość `42` w pustym widoku (styl z drugiego), na zewnątrz końca.
22. `[35055]` (2026-10-01, v0.3.9, Shift, „rysunek jest dobry”): lustro
   `[35099]`, rura 173, jedno cięcie 45° na bliskim końcu. Usuń: trzy `21`;
   `173` (płaskie) przetrwało. Wstaw: długość `42` nad `173`, szerokość
   `42` w pustym widoku, na zewnątrz bliskiego końca (`Up=(-1;0)`).
23. `[35006]` (2026-10-01, v0.3.9, Shift, „rysunek ma wszystko”): rura
   4436, jedno cięcie 45°. Usuń: trzy `21`; `4436` i istniejące płaskie
   `42` (pełna długość cięcia) przetrwały. Wstaw: tylko szerokość `42` w
   pustym widoku — długości nie zdublował (`HasSameDimension`). Drugie
   kliknięcie Usuń (Shift) i Wstaw: nic do zrobienia w żadnym widoku.
24. `[35013]` (2026-10-01, v0.3.9, Shift, „jest dobrze”): **rysunek z
   błędu v0.3.2** — rura 5796, oba końce 45°. Usuń: po jednym `21` do osi w
   każdym widoku. Wstaw: 4 wymiary wcięcia + 2 rozciągnięte promienie
   (`21` → `42`). **`5796` przetrwało** — warunek `Up` wzdłuż osi z v0.3.3
   działa. W każdym widoku średnica i długość cięcia mają te same końce,
   różne `Up` (przy 45° obie `42`).
25. `[35663]` (2026-10-01, v0.3.9, Shift, „opisuje wszystko co powinien”):
   `Leiter` `RO48.3*3.6`, rura 210, jedno cięcie 22,6°. Usuń: `20` (długość
   cięcia z końcem na osi) i `10`; `210` (płaskie) przetrwało. Wstaw:
   szerokość `48` na zewnątrz końca, długość `20` (płaska, w drugim widoku),
   promień `24` → `48`.
26. `[35100]` (2026-10-01, v0.3.9, Shift, „poza tym jest git”): `Knielauf`
   `RO33.7*3.2`, rura 157, jedno cięcie 45°. Usuń: `17` (z głębią) i `34`
   (długość z końcem na osi); `157` i płaskie `17` przetrwały. Wstaw:
   długość `34`, szerokość `34`, promień `17` → `34`. **Czwarty przypadek
   „długość w cudzej ramce”**: `34` wstawione w górę, w ramkę górnego
   widoku; operator przeniósł je pod dolny widok i wskazał to jako lepsze.
   Wzorzec z 4 przypadków (`[35020]`, `[35067]`, `[35288]`, `[35100]`):
   długość dostaje zawsze `Up` = +prostopadła, a jej widok leży NIŻEJ na
   arkuszu niż sąsiedni. Na `[35660]` (widok z długością wyżej) `Up` w
   górę był dobry. Hipoteza do sprawdzenia danymi: zwrot `Up` długości w
   stronę OD sąsiedniego widoku (porównanie `View.Origin`). Niezmierzone:
   czy lokalne +Y widoku = +Y arkusza na wszystkich rysunkach.
27. `[35077]` (2026-10-01, v0.3.9, Shift, „jest dobrze”): lustro `[35100]`,
   rura 121, cięcie 45° na bliskim końcu. Usuń: `34` (długość z końcem na
   osi) i `17` (z głębią); `121` przetrwało. Wstaw: szerokość `34`,
   promień `17` → `34`, długość `34`. **Piąty przypadek wzorca, tym razem
   PRZEWIDZIANY przed testem**: długość w dolnym widoku z `Up=(0;1)` weszła
   w ramkę górnego (zapisane `Distance=-196,50`); operator: „powinna w dół”.
28. `[35073]` (2026-10-01, v0.3.9, Shift, „jest git”): `Knielauf`
   `RO33.7*3.2`, rura 741, jedno cięcie 45°. **`741` ma głębię Z i dotyka
   osi — chroni go tylko filtr 300 mm (log: „ma 741 mm własnej długości,
   pomijam”) — przetrwało.** Usuń: `34` (długość z końcem na osi) i `17`.
   Wstaw: szerokość `34`, promień `17` → `34`, długość `34`. Długość w
   dolnym widoku znów z `Up=(0;1)`, ale odstęp między widokami był duży —
   stanęła w przerwie, nie w cudzej ramce. Zwrot jest więc stały; czy
   wymiar wejdzie w sąsiedni widok, zależy od odstępu.

29. **Zwrot `Up` długości wcięcia** (2026-10-01, gałąź
   `length-up-direction`, PR #62, wydane w v0.3.10). `--diag-view-bounds`
   na `[35020]`, `[35067]`, `[35288]`, `[35100]`, `[35077]`, `[35660]`,
   `[35073]`: lokalne +Y widoku = +Y arkusza (wymiary z `Up=(0;1)`
   poszerzają ramkę widoku w górę, z `(0;-1)` w dół; oś X nie odwrócona);
   wszystkie mają dwa widoki jeden nad drugim, ramki stykają się. Poprawka
   w `InsertResolvedIfMissing`: długość dostaje odwrócone `Up`, gdy inny
   widok (`View.Origin`) leży tylko po stronie `Up` — widoki po obu
   stronach bez zmian. Dry-run 61 rysunków, v0.3.9 vs nowy build
   (`scans/lenup-old.txt`/`lenup-new.txt`, lokalne, w `.gitignore`): 56
   długości, 34 odwrócone z góry w dół na 27 rysunkach, 22 bez zmian (w
   górę, np. `[35660]`), zero innych różnic (wartości, końce, widoki,
   liczba wymiarów identyczne). Wszystkie 5 przypadków „w cudzej ramce”
   odwrócone. Zmienia też rysunki już ocenione „opisuje”, m.in. `[35663]`,
   `[35073]` (długość pójdzie pod dolny widok), `[35019]` (odsunie prawą
   `42` od napisu szerokości, pkt 17). Skan trwał ~2 h (dwa buildy) — przy
   następnej zmianie wystarczy nowy build i porównanie z `lenup-new.txt`,
   o ile model się nie zmienił.
   **Brama przeszła (2026-10-01, build z `bin`, Shift):** `[35100]` —
   Usuń: `34` i `17` do osi, `157` przetrwało; Wstaw: długość `34` z
   `Up=(0;-1)` POD dolnym widokiem, szerokość `34`, promień `17` → `34`;
   niezależny odczyt zgodny z dry-runem, operator: „ma wszystko”. Kontrola
   `[35660]` — wynik identyczny z v0.3.9 (dwie `20` w górę, dwie `48` po
   przeciwnych stronach, `93` przetrwało), operator: „tak”.

30. **Odstęp `Distance` wstawianych wymiarów — research, BEZ kodu**
   (2026-10-01 wieczór; `--diag-view-bounds` + `--diag-notch-fill-dryrun`
   na `[35095]`, `[35021]`, `[35013]`, `[35099]`, `[35014]`, wyniki w
   `scans/dist-*.txt`, lokalne):
   - `Distance` jest w jednostkach MODELU w układzie widoku (nie mm na
     papierze — inaczej niż mówi ogólna uwaga w `..\AGENTS.md`) i liczy się
     od `StartPoint` wzdłuż `Up`. Dowód: na `[35095]` dwa wymiary z
     `Up=(1;0)`, starty X=372,37 i 394,18, `Distance` 127,63 i 105,82 —
     oba na linii X=500; na `[35014]` 4328,81+106,40 = 4350,01+85,20 =
     4435,21. Liczone od dalszego końca linie by się rozjechały.
   - **Wymiary Tekli z tym samym `Up` leżą w widoku na wspólnej linii**
     (`Start·Up + Distance` równe) na wszystkich 5 rysunkach. Położenie
     linii zależy od rysunku: ±100 przy 1:10 (`[35095]`, `[35021]`), 50
     (`[35014]`), ±200 przy 1:20 (`[35013]`); `[35099]` ma dwa rzędy, 100 i
     200 (odstęp 10 mm na papierze).
   - Nasz insert bierze `Distance` z PIERWSZEGO innego wymiaru w widoku
     (`FindReferenceDimension`) i odmierza od własnego startu — linia
     wypada przypadkowo. `[35095]`: długość `34,5` z `Up=(0;1)` dostaje
     127,63 → linia Y=148,8 (na papierze ~193 z 210, krawędź arkusza),
     a rząd Tekli w tym kierunku jest na Y=100.
   - Propozycja reguły (NIEZATWIERDZONA): stawiać nowy wymiar na linii
     wymiarów z tym samym `Up` w widoku. Otwarte: co, gdy na tej linii stoi
     wymiar zachodzący zakresem (na `[35095]` całkowite `407`, 0–406,88, na
     Y=100) — następny rząd na zewnątrz (bliżej krawędzi) czy druga strona
     rury (rząd −100 ma tam łańcuch wymiarów do osi, które „Usuń” kasuje:
     10 kandydatów — `21`, `21`, `11`, `11`, `19`, `22`, `13`, `18`, `21`,
     `90`). Najpierw sprawdzić, co zostaje po Usuń, potem zdecydować z
     operatorem.
   - `[35021]` („`42` na rurze”): dziś szerokość `42,4` ma `Up=(-1;0)` od
     X=7,68, `Distance` 100 → linia X=−92, poza rurą. Najpewniej naprawione
     już przez v0.3.8 — potwierdzić na żywo.

   - **Wynik 2026-10-02 — Tekla sama przestawia wstawione wymiary,
     reguła rzędów ODRZUCONA.** Operator wybrał wariant A (zajęty rząd →
     rząd dalej o 10 mm na papierze), kod policzył dla `[35095]` po Usuń
     linie 200/200/494/−58,5. Niezależny odczyt po Wstaw (dwa przebiegi,
     za drugim bez ruszania czegokolwiek): Tekla postawiła 150/250/500/−50
     — wszystko na siatce co 50 (5 mm papieru), `35` uciekł pod ramkę
     (najpewniej przed napisem `64.69°`). `--diag-dimension-style` (od
     02.10 wypisuje `Placing`): WSZYSTKIE wymiary na rysunku, także
     Tekli, mają `Placing=Free` (kier. +, `SearchMargin` 1); nasze mają
     `MinimalDistance` 8 (ze stylu wzorca), Tekli 5. `Distance` przy
     `Free` to tylko punkt startowy. Operator: „niech program robi tak,
     jak Tekla chce” — kod wstawiania bez zmian (jak v0.3.10), w logu
     dry-runu `[dry-run] brakująca …` doszło `Distance`. Rysunek po teście
     „wszystko opisuje”.
   - **Sidequest (otwarty):** czy przez API da się wymusić położenie —
     `DimensionSetBaseAttributes.Placing` = `DimensionPlacingAttributes`
     (`Placings.Fixed`/`Free`, `PlacingDirectionAttributes`
     `Positive`/`Negative`, `PlacingDistanceAttributes`
     `MinimalDistance`/`MaximalDistance`/`SearchMargin`). Nie sprawdzone:
     czy insert z `Fixed` trzyma `Distance` i jak wygląda rysunek. To
     zmiana zapisu — eksperyment tylko za zgodą operatora, z bramą.

**Proponowany następny krok:** sidequest z pkt 30 — eksperyment z
`Placings.Fixed` przy insercie na `[35095]` (za zgodą operatora).

Lista ze skanu do testów na żywo:
(`[35660]`, `[35662]`, `[35086]`, `[35067]`, `[35029]`, `[35014]`, `[35068]`, `[35030]`, `[35288]`, `[35099]`, `[35055]`, `[35006]`, `[35013]`, `[35663]`, `[35100]`, `[35077]`, `[35073]` sprawdzone 01.10; rysunki `[3.5xxx]` z listy nieaktualne względem modelu — zaktualizować w Tekli przed testem.) Niesprawdzone na żywo:
`[3.5028]`, `[3.5068]`, `[3.5003]`,
`[3.5002]`, `[3.5030]`,
(lista ze skanu wyczerpana poza nieaktualnymi). Dobierać
też rysunki z wymiarem, który MA przetrwać — brak takiego w testach
przepuścił błąd v0.3.2.

**Cykl testu na żywo (sprawdzony, trzymać się):** operator otwiera rysunek
→ `--diag-notch-raw`, `--diag-dimension-style`, `--diag-active`,
`--diag-notch-fill-dryrun` → tabelka „widok / Usuń skasuje / zostaje /
Wstaw doda”, widoki nazywane po zawartości → operator: Usuń w każdym
widoku (Shift + klik — operator to lubi), Wstaw, wkleja log → odczyt `--diag-dimension-style` z osobnego
procesu → pytanie BEZ podpowiedzi „Czy po tej operacji rysunek nadal
opisuje wszystko, co musi opisywać?”. Wyniki kilku rysunków zbierać w
jednej gałęzi z `AGENTS.md`, jeden PR.

## Historia rozwoju 23–29.09 — przeniesiona na wiki

Pełny chronologiczny dziennik pracy nad regułą v6 i wymiarem wcięcia (23.09 →
29.09) został 2026-10-01 przeniesiony **dosłownie, bez skracania i bez zmiany
treści** na stronę wiki
[10-Dziennik-2026-09](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/wiki/10-Dziennik-2026-09).
W tym pliku zostaje tylko stan aktualny — powyżej sekcja „START SESJI TUTAJ”.

Co jest na tej stronie wiki:

- **23.09** — reguła v6 (`TouchesAxis` z wymogiem głębi Z + filtr długości
  własnej), brama bezpieczeństwa drugi raz tego dnia; PUŁAPKA 6 (`21 mm`,
  promień rury, niepotrzebnie kasowany przez v5) i jej naprawa.
- **23–24.09** — wymiar wcięcia od zera: pomiary z `[35021]` (ściana cięcia
  jako pierścień z dwiema pętlami, wybór cięciwy, transformacja
  model → układ widoku), pierwszy realny insert, reguła dopasowania
  ściana ↔ wymiar, trzy błędy wykryte dopiero po insercie na `[3.5013]`
  (fałszywy sukces, wymiar po skosie), nierozwiązana zagadka z 24.09
  („które złącze potrzebuje wymiaru wcięcia”).
- **25.09** — `[3.5013]`: szerokość i długość wylądowały w dwóch różnych
  widokach (przyczyna i finalna naprawa — asymetria długość/szerokość);
  zdjęcie blokady rysunku, pilot → produkcja; krytyczne znalezisko, że
  `TouchesAxis` nie była ograniczona do profili RO (blacha `[21050]`,
  4 fałszywe kandydaty); filtr kąta cięcia (10°, potem 6,5°);
  `InsertMissing` — insert napędzany geometrią, nie wymiarem do osi.
- **28.09** — testy skanera modelu: `[225.130]` (rysunek zespołu — reguła
  fałszywa, operator nie kasuje), `[35095]` (podwójny skos, 4/4 wymiary
  poprawnie); wymiar wcięcia poza krawędzią arkusza — zbadane i porzucone
  (brak wiarygodnej skali z API).
- **29.09** — test na `[35095]`: kasowanie + wstawianie + rozciąganie promienia
  do średnicy; pułapka `StraightDimension.Modify()` (fałszywy sukces) i
  `OpenUnlessActive` (diagnostyka cofała niezapisane zmiany); refaktor
  geometrii do `NotchPilot` bez zmiany reguł.
- **29.09** — gięte rury: znaleziona utrata danych w v0.3.3 na łukach
  (`[35681]`), 13 z 74 rysunków z kandydatami to łuki, guard `IsStraight`.
- **29.09** — BŁĄD w wydanej v0.3.2: rozciąganie promienia zjadało wymiar
  całkowitej długości (`[35010]`, `[35020]`, `[35013]`, `[3.5013]`).
- **24–29.09** — PUŁAPKA 5 (historia reguły v4) i jej doprecyzowanie
  przez PUŁAPKĘ 6 — najważniejsza lekcja o walidacji: nie walidować reguły
  przez jej własny dry-run.

### Historia nieudanych podejść do reguły kasowania (NIE powtarzać)

| Wersja | Błąd | Poprawka |
|---|---|---|
| v1 | `TouchesAxis` sprawdzał WSZYSTKIE współrzędne, nie tylko tę różniącą się między końcami — złapał prawidłowy wymiar "z boku rury" (jedna współrzędna = 0 dla obu końców, bo widok jest 2D) jako fałszywy duplikat. Skasował dobry wymiar na żywym modelu, `[35270]`. | Sprawdzać tylko współrzędną, która MIĘDZY końcami się różni. |
| v2 | Grupowanie po CAŁYM widoku zamiast po złączu — na `[3.5013]` (kilka złączy RO w jednym widoku ogólnym) zostawiał tylko jeden wymiar z całego widoku. Skasował dobre wymiary na żywym modelu. | Grupowanie po bliskości geometrycznej (próg 300 mm), nie po widoku. |
| v3 | Po poprawce v2 `[3.5013]` nadal traciło WSZYSTKIE wymiary do osi. | Zdiagnozowane w v4 przez odczyt logu diagnostycznego, nie przez zgadywanie. |
| v4, próba 1 (odrzucona) | Klaster bliskości mieszał 2 prawdziwe duplikaty (21 mm) z wymiarem całkowitej długości profilu (5796 mm), który też "dotyka osi" (lokalny początek układu współrzędnych rury leży na osi z definicji). Próba poprawki: kasować tylko wymiary o IDENTYCZNEJ wartości — zepsuło `[35270]` (tam para to 24/12 mm, RÓŻNE wartości, `12` był prawdziwym duplikatem). | Odrzucone — "różne wartości" samo w sobie nic nie mówi o duplikacie. |
| v4 | Filtr długości własnej naprawił [3.5013], ale reguła bazowa miała PUŁAPKĘ 5 ([na wiki](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/wiki/10-Dziennik-2026-09)) — para prostopadłych wymiarów o identycznej wartości. Przeszedł bramę trzy razy (błędnie, patrz "najważniejsza lekcja" wyżej). | Zastąpione przez v5 (kasuj wszystko, bez dedupu) — patrz „STAN NA 2026-09-23" [na wiki](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/wiki/10-Dziennik-2026-09). |
| v5 | PUŁAPKA 6: `TouchesAxis` sprawdzał Y i Z niezależnie, na dowolnym końcu — łapał zwykłe płaskie wymiary promienia (Z=0 zawsze), które przypadkiem miały Y=0 na jednym końcu. Skasował realnie `21 mm` (promień rury) na żywym [35021]. | Dodany wymóg realnej głębi Z (`zDiffers`) jako warunek konieczny — patrz „PUŁAPKA 6" [na wiki](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/wiki/10-Dziennik-2026-09). |

PUŁAPKA 2 (osobna, nie wersja): "krótszy" nie znaczy mniejszy surowy dystans
3D między `StartPoint`/`EndPoint` — `Dimension.Value` to RZUT rozpiętości na
kierunek wymiaru, nie odległość euklidesowa. Trzeba porównywać wyświetlaną
wartość, nie geometrię. Dotyczy też wymiaru wcięcia (sekcja „Wymiar wcięcia",
[strona wiki 9](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/wiki/9-Wymiar-Wciecia)) — punkty
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
  (sekcja „Wymiar wcięcia", [strona wiki 9](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/wiki/9-Wymiar-Wciecia)).
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
  wierzchołków różnych pętli w jedną listę — patrz sekcja „Wymiar wcięcia"
  ([strona wiki 9](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/wiki/9-Wymiar-Wciecia)
  i [dziennik](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/wiki/10-Dziennik-2026-09),
  23.09), to był realny błąd w tej sesji (poprawiony). Zewnętrzny obrys =
  ten o większym rozstawie własnych wierzchołków.
- **`StraightDimension.Modify()` po zmianie `StartPoint`/`EndPoint` nic nie
  zmienia, choć zwraca `true`** (zmierzone 2026-09-29) — a odczyt w tym
  samym procesie kłamie, że zmienił. Zmiana punktów = nowy `Insert()` +
  `Delete()` starego.
- **`SetActiveDrawing` na już otwartym rysunku go przeładowuje** i gubi
  niezapisane zmiany (zmierzone 2026-09-29). Patrz `OpenUnlessActive`.
- **`CoordinateSystem` (Origin/AxisX/AxisY) nie ma gotowej metody
  transformacji punktu do jej lokalnego układu** w publicznym Open API —
  trzeba liczyć ręcznie przez iloczyny skalarne (`Vector.Dot`), patrz
  `ToViewSpace` w `NotchPilot.cs`.

## Jak testować bez klikania w GUI

Automatyzacja (w tym agent AI) nie klika w przycisk `MainForm`. `Program.cs`
ma więc tryb konsolowy:

```
RoAxisDimensionRemover.exe --diag-active           # aktywny rysunek w Tekli, wszystkie widoki, ta sama reguła co „Usuń”
RoAxisDimensionRemover.exe --diag-mark "[3.5013]"  # otwiera rysunek po Mark, potem diagnostyka
RoAxisDimensionRemover.exe --diag-notch            # tylko odczyt: geometria bryły + kandydat na wymiar wcięcia
RoAxisDimensionRemover.exe --diag-dimension-style  # tylko odczyt: styl (Attributes/UpDirection/Distance) istniejących wymiarów
RoAxisDimensionRemover.exe --diag-notch-match "[3.5013]"  # tylko odczyt: dopasowanie wymiar do osi -> najbliższa ściana cięcia
RoAxisDimensionRemover.exe --diag-notch-insert-dryrun "[3.5013]"  # tylko odczyt: NotchPilot w dry-run dla każdego wymiaru do osi
RoAxisDimensionRemover.exe --diag-view-objects "[3.5013]"  # tylko odczyt: typy wszystkich obiektów w widoku (research nad regułą "które złącze pokazać")
RoAxisDimensionRemover.exe --diag-notch-raw "[Mark]"       # tylko odczyt: WSZYSCY kandydaci na ścianę cięcia, obie cięciwy, bez filtra płaskości
RoAxisDimensionRemover.exe --diag-notch-fill-dryrun "[Mark]"  # tylko odczyt: NotchPilot.InsertMissing w dry-run - reguła napędzana geometrią, nie wymiarem do osi
RoAxisDimensionRemover.exe --diag-connection "[Mark]"      # tylko odczyt: typ/strony Connection dla każdego widoku (research "które złącze potrzebuje wymiaru")
RoAxisDimensionRemover.exe --diag-find-candidates [plik]   # tylko odczyt: bez argumentu CAŁY model (> 1 h), z plikiem (Mark w liniach) tylko te rysunki; prawdziwa RemoveAxisDimensions + InsertMissing w dry-run
RoAxisDimensionRemover.exe --diag-view-bounds "[Mark]"      # tylko odczyt: rozmiar arkusza/widoku, bounding box zawartości, skala widoku, bryła części przeliczona na arkusz, wymiary z Up/Distance (od 2026-10-01: dowód, że lokalne +Y widoku = +Y arkusza; pierwotnie research "czy wymiar wychodzi poza arkusz" - PORZUCONE, patrz sekcja "Zgłoszony, ZBADANY i PORZUCONY" [na wiki](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/wiki/10-Dziennik-2026-09))
```

`dryRun` jest we wszystkich na sztywno `true` w `DiagRunner.cs` — nie da się
tego przełączyć z linii poleceń. `--diag-notch`/`--diag-dimension-style`
nawet nie mają pojęcia `dryRun` — nic nie usuwają ani nie tworzą, tylko
czytają (model przez `Tekla.Structures.Model.Model`, albo istniejące
wymiary na rysunku). Log leci na `stdout` (przechwyć np.
`> plik.txt 2>&1` albo uruchom w tle i przeczytaj output). Tryby z
`"[Mark]"` otwierają rysunek na ekranie (`SetActiveDrawing(d, true)`),
chyba że jest już aktywny — wtedy biorą go bez przeładowania
(`OpenUnlessActive`, od 2026-09-29).

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
| `RoAxisDimensionService.cs` | cała logika wykrywania i kasowania, zero UI (reguła v6 — kasuje wszystko w widoku, co spełnia `TouchesAxis`). Guardy na wejściu `RemoveAxisDimensions`: `SinglePartDrawing` (od 2026-09-29), `ViewHasRoProfile` (od 2026-09-25) i `ViewHasBentPart` (od v0.3.4) — przy którymkolwiek niespełnionym nic nie kasuje. Od v0.3.6 kandydat musi leżeć w obrębie ściany cięcia (`CutZones`/`InCutZone`) |
| `MainForm.cs` | UI: główny przycisk kasowania, log do okna i do pliku (`dryRun: false` od 2026-09-23 — brama v5 przeszła). Wybór widoku: `Picker.PickPoint` (klik w Tekli), Esc → `PickViewFromList` (lista w oknie). Fokus na Teklę po operacji tylko gdy `!dryRun`. Plus jeden przycisk `_insertNotchButton` ("Wstaw wymiar wcięcia dla złączy na wybranym rysunku", handler `InsertNotchButton_Click`) — woła `NotchPilot.InsertMissing` (patrz „InsertMissing — insert napędzany geometrią" [na wiki](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/wiki/10-Dziennik-2026-09)), bez blokady marki, uzupełnia brakujące wymiary wcięcia niezależnie od tego, czy wymiar do osi jeszcze istnieje |
| `NotchPilot.cs` | TWORZENIE wymiaru wcięcia — produkcyjne (nazwa „Pilot” historyczna). Przycisk woła `InsertMissing` (guard `SinglePartDrawing`, gięte części pomijane przez `IsStraight`) → `FindQualifyingChordPairs` (ściany ≥ 6,5°) → `InsertResolvedIfMissing` (widok z płaską cięciwą, styl z tego widoku albo z innego) + `StretchRadiusToDiameter` (promień → średnica: `Insert()` nowego, potem `Delete()` starego). `HasSameDimension` porównuje końce i `UpDirection`. `InsertWidth`/`InsertLength` zostają tylko dla `--diag-notch-insert-dryrun`. Problem „które złącze faktycznie potrzebuje wymiaru” (24.09) bez ochrony — świadoma decyzja operatora |
| `Program.cs` | punkt wejścia; GUI domyślnie, przełączniki `--diag-*` (pełna lista w „Jak testować bez klikania w GUI”) dla trybu konsolowego |
| `DiagRunner.cs` | headless runner dry-run; całą geometrię (`CutFaces`, `FindChord`, `ToViewSpace`, `BeamAxis`) bierze z `NotchPilot`, żeby diagnostyka liczyła dokładnie to samo co przycisk (do 2026-09-29 miała własne kopie) + `RunNotchDiag` (research geometrii wcięcia) + `RunDimensionStyleDiag` (styl istniejących wymiarów, źródło danych dla `NotchPilot`) + `RunNotchMatchDiag` (dopasowanie wymiar↔ściana cięcia po najbliższości w układzie widoku, patrz „Reguła dopasowania ściana↔wymiar" [na wiki](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/wiki/10-Dziennik-2026-09)) + `RunNotchInsertDryRun` (woła `NotchPilot` w dry-run dla każdego wymiaru do osi, potwierdzone na żywym [3.5013]) + `RunNotchRawDiag` (2026-09-25: zrzuca WSZYSTKICH kandydatów, obie cięciwy, bez filtra płaskości, z flagą płaska(Z≈0) — źródło danych dla poprawki asymetrii widoku, patrz „Poprawiona przyczyna i finalna naprawa" [na wiki](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/wiki/10-Dziennik-2026-09)) + `RunFindCandidatesDiag` (cały model; od 2026-09-29 liczy też braki wymiaru wcięcia i promienie do rozciągnięcia) + `OpenUnlessActive` (nie przeładowuje otwartego rysunku) — **świadomie trwały element projektu**, `dryRun` na sztywno `true` na zawsze, nie do usunięcia |
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
  (`dryRun: true`, opisuje jeszcze regułę v4) →
  [v0.3.0](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/releases/tag/v0.3.0)
  (2026-09-28: wymiar wcięcia produkcyjny, guard RO, filtr kąta,
  `InsertMissing`; tag na `release`, `dev` i `release` identyczne, wersja
  podbita w `csproj` i `setup.iss`) →
  v0.3.1 (2026-09-29: guard `SinglePartDrawing` w `RemoveAxisDimensions`
  i `InsertMissing` — v0.3.0 potrafiła fałszywie kasować na rysunkach
  zespołów) →
  v0.3.2 (2026-09-29: rozciąganie promienia do średnicy, `HasSameDimension`
  z kierunkiem, diagnostyka nie przeładowuje otwartego rysunku; **miała błąd** — patrz „BŁĄD W WYDANEJ v0.3.2" [na wiki](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/wiki/10-Dziennik-2026-09)) →
  v0.3.3 (2026-09-29: rozciąganie nie rusza wymiaru całkowitej długości, styl wzorca z innego widoku) →
  v0.3.4 (2026-09-29: gięte rury pomijane przy kasowaniu i wstawianiu, diagnostyka widoku/kąta cięcia, refaktor bez zmiany reguł — geometria tylko w `NotchPilot`, poprawka nakładania się przycisków pod paskiem „nowsza wersja”) →
  v0.3.5 (2026-09-30: próg kąta cięcia 10° → 6,5°, PR #39) →
  v0.3.6 (2026-09-30: „Usuń” nie kasuje całkowitej długości krótkich rur, rozciąganie promienia do osi na drugim końcu, PR #42) →
  v0.3.7 (2026-09-30: log w oknie czyszczony dopiero przy innym rysunku, PR #46, reguły bez zmian) →
  v0.3.8 (2026-10-01: szerokość wcięcia odsuwana od środka rury w stronę swojego końca, PR #51) →
  v0.3.9 (2026-10-01: Shift + klik na „Usuń” = wszystkie widoki, PR #56, reguły bez zmian) →
  v0.3.10 (2026-10-01: długość wcięcia odsuwana od sąsiedniego widoku, PR #62). Release na GitHubie tworzy operator
  ręcznie — `gh release create` blokuje klasyfikator auto mode. Sama flaga pre-release
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
  Zmiany idą przez PR do `dev`; commit wprost na `dev` tylko za wyraźną
  zgodą operatora. **Przed każdym `git commit` sprawdzić
  `git branch --show-current`, pushować zawsze z jawną nazwą gałęzi** —
  01.10 katalog został przełączony na `dev` (najpewniej przez drugiego
  agenta) i 10 commitów z notatkami poszło na `origin/dev` bez PR-a
  (operator: zostawić, nie powtarzać).
- Drugi agent pracujący równolegle: własny `git worktree` i zakaz
  uruchamiania Tekli, exe, buildu i `taskkill` (ten wzór działał — PR #60). Usuwanie gałęzi (lokalnie i zdalnie) też blokuje
  klasyfikator — zostawić operatorowi.
- Wydanie: bump `csproj` + `setup.iss` → build → ISCC → PR do `dev` → PR
  `dev → release` → tag na `release` → operator tworzy release z
  instalatorem.
- **`gh` (GitHub CLI) jest zainstalowany i zalogowany** (`C:\Program Files\GitHub CLI\gh.exe`,
  konto `HoldFort-Bananza`, protokół HTTPS) — użyj `gh issue create`/`gh pr create`
  zamiast ręcznego REST API. Może nie być jeszcze na `PATH` w nowej sesji
  Bash (sprawdź `which gh` najpierw, wywołaj pełną ścieżką jeśli trzeba).
- **`README.md` celowo NIE zawiera numerów rysunków (Mark) ani nazw
  siostrzanych projektów** — świadoma decyzja operatora, bo repo jest
  publiczne. Numery rysunków w pozostałych plikach repo i w historii
  zostają (decyzja operatora 01.10), historii nie przepisywać. Ten plik może i powinien zachować konkretne dane (to baza
  wiedzy do diagnozy), ale jeśli edytujesz `README.md`, zachowaj ten sam
  brak identyfikatorów.
