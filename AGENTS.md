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
   niżej). **Stan na 2026-09-29 (v0.3.3): `dryRun: false` — przycisk
   NAPRAWDĘ kasuje**, reguła v6 przeszła bramę 2026-09-23 (patrz niżej).
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

## START SESJI TUTAJ — stan na koniec 2026-09-29

Sekcje niżej to dziennik chronologiczny (23.09 → 29.09); ta jest skrótem
aktualnego stanu. Przy sprzeczności wygrywa KOD, potem ta sekcja, potem
starsze wpisy dziennika.

**Wydanie:** v0.3.6 (2026-09-30) = v0.3.5 + ochrona całkowitej długości
krótkich rur przy „Usuń” + rozciąganie promienia do osi na drugim końcu
(PR #42); instalator `installer\output\RoAxisDimensionRemover-Setup-v0.3.6.exe`.
Wcześniej v0.3.5 (2026-09-30) = v0.3.4 + próg kąta cięcia 6,5° (PR
#39, #40),
release na GitHubie tworzy operator. Wcześniej v0.3.4 — tag na `release`
(2026-09-29, PR #35, #36, #37). v0.3.4 = porządki bez zmiany reguł +
poprawka paska + **pomijanie giętych rur** (v0.3.3 kasowała na łukach dobre
wymiary — patrz „Przegląd 2026-09-29 po południu”). Skrót z pulpitu
wskazuje na zainstalowaną kopię (NIE na `bin`). **v0.3.2 miała błąd
kasujący dane** (rozciąganie zamieniało wymiar całkowitej długości na `42`)
— naprawione w v0.3.3, patrz sekcja „BŁĄD W WYDANEJ v0.3.2”.

**Co robi program (dwa osobne przyciski):**
1. „Usuń wymiary do osi” — jeden widok wskazany kliknięciem; guardy:
   `SinglePartDrawing` + część o profilu `RO…` w widoku + brak giętej
   części w widoku (od v0.3.4); reguła v6
   (`TouchesAxis` z wymogiem głębi Z + filtr długości własnej 300 mm).
2. „Wstaw wymiar wcięcia” — `NotchPilot.InsertMissing`, cały rysunek:
   guard `SinglePartDrawing`; gięte części pomijane (od v0.3.4); ściany
   cięcia ≥ 10°; długość i szerokość
   wcięcia w widoku, gdzie cięciwa jest płaska; styl z widoku docelowego,
   a gdy pusty — z innego widoku; `StretchRadiusToDiameter` zamienia płaski
   promień przy skosie (czubek cięcia → oś, mierzony w poprzek, koniec na osi
   w obrębie cięcia) na średnicę od czubka do czubka; `HasSameDimension`
   porównuje końce i `UpDirection`, więc ponowne kliknięcie nic nie dubluje.

**Potwierdzone na żywo przez operatora (pytanie „czy rysunek opisuje
wszystko”):** `[35021]`, `[3.5013]` (znów jest w modelu — wcześniejsze
wpisy mówią, że usunięty; wrócił), `[3.5027]`, `[35095]`, `[35020]`
(kasowanie + wstawianie + rozciąganie, `256` przetrwało). Rysunek zespołu
`[225.130]` i blacha `[21050]` — poprawnie odrzucone.

**Znane słabości (wszystkie zaakceptowane przez operatora):**
- Położenie wstawionych wymiarów (`side` + `Distance` wzorca) bywa złe:
  przy krawędzi arkusza (`35` na `[35095]`), na rurze (`42` na `[35021]`),
  w cudzej ramce (`15` na `[35020]`). Najczęstsza poprawka ręczna.
- Widok zawierający same wymiary do osi zostaje po „Usuń” pusty — operator:
  takiego widoku się nie klika. „Wstaw” i tak go uzupełni stylem z innego
  widoku.
- Zagadka 24.09 (odrzucony koniec 45° na `[3.5013]`) nierozwiązana.
- Picker wisi przy kliku w pustą część widoku — Esc.
- ~~Na `[35010]` przy 45° kilka `42` w jednym widoku~~ — SPRAWDZONE
  2026-09-29 (dry-run z widokiem w logu): to nie duplikaty. Szerokość
  wcięcia zawsze trafia do INNEGO widoku niż rozciągnięta średnica (12
  rysunków); dwa `42` w jednym widoku to średnica i długość cięcia, które
  przy 45° mają tę samą wartość — jak na zaakceptowanym `[35095]`.
- **ROZSTRZYGNIĘTE 2026-09-30: szara strefa filtra kąta 8–9° → próg
  `MinCutAngleDegrees` 10° → 6,5°.** Skan 29.09: `[35092]` 8,7°, `[35260]`
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

**Środowisko i pułapki, które dziś kosztowały czas:**
- Model testowy (~19 tys. części) NIE zapisuje się (limit licencji 2500) —
  zmiany na rysunku znikają po zamknięciu. Wygodne do testów.
- Każdą nową operację zapisu weryfikować ODCZYTEM Z OSOBNEGO PROCESU
  (`--diag-dimension-style`), nie logiem programu — `Modify()` z nowymi
  punktami i odczyt w tym samym procesie kłamały.
- `--diag-* "[Mark]"` nie przeładowuje już otwartego rysunku
  (`OpenUnlessActive`) — wcześniej cofało niezapisane zmiany operatora.
  Otwarcie INNEGO rysunku przez diag nadal zamyka bieżący.
- `--diag-find-candidates` (cały model, 2298 rysunków) trwa > 10 min —
  tylko w tle z wyjściem do pliku. Wynik 29.09: 74 rysunki z kandydatami,
  22 z promieniem do rozciągnięcia (lista w sekcji „Test na `[35095]`”).
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

**NA DALEJ (stan 2026-09-30):**
1. Obie poprawki wyżej wydane w v0.3.6, operator ją zainstalował.
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
   `42` do osi skasowane w jednym widoku, „Wstaw” dodał je w drugim).
3. 30.09 komputer dwa razy padł (bugcheck `0x133`, błąd `nvlddmkm`) przy
   otwieraniu rysunku przez diagnostykę; w tle działała animowana tapeta
   (Lively/mpv, ~27% GPU). Po jej zamknięciu i czystej reinstalacji
   sterownika NVIDIA — spokój. Jeśli wróci: `%USERPROFILE%\gpu-log.csv`
   (logger `gpu-log.ps1`, co 2 s) pokaże stan karty przed padem.

- Pełny `--diag-find-candidates` 30.09 trwał ponad godzinę (Tekla urosła
  do 6 GB) — do porównań reguł używać wersji z plikiem listy Mark (4,5 min
  na 61 rysunków).

**Proponowany następny krok** (operator nie zdecydował): 2–3 kolejne
rysunki z listy skanu, tym samym cyklem (Usuń → Wstaw → odczyt z osobnego
procesu → ocena operatora), najlepiej `[35016]`/`[35066]`/`[35092]` (7–8
kandydatów do usunięcia) i `[35010]`. Dobierać też rysunki z wymiarem,
który MA przetrwać — brak takiego w testach przepuścił błąd v0.3.2.

## STAN NA 2026-09-23 — reguła v6 (poprawka TouchesAxis), brama przeszła DRUGI RAZ tego dnia

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

1. **`TouchesAxis`** (v6, POPRAWIONA 2026-09-23 - patrz "PUŁAPKA 6" niżej):
   wymiar "dotyka osi" tylko jeśli (a) ma REALNĄ GŁĘBIĘ - współrzędna Z
   faktycznie się różni między jego końcami, ORAZ (b) współrzędna, która
   MIĘDZY jego końcami się różni (Y albo Z), jest bliska zeru na
   którymkolwiek końcu. Warunek (b) sam w sobie to jeszcze v5 (i miał
   PUŁAPKĘ 6) - warunek (a) jest nowy i odróżnia faktyczny artefakt
   skośnego cięcia od zwykłego, płaskiego wymiaru 2D (promień, pozycja),
   który tylko PRZYPADKIEM dotyka zera w Y.
2. **Filtr długości własnej**: wymiar dotykający osi, ale dłuższy niż
   `SameJointDistanceMm` (300 mm) między własnym `StartPoint` a `EndPoint`,
   jest wykluczany z kandydatów — dwuznaczność oś/powierzchnia dotyczy z
   definicji tylko krótkiego, lokalnego wymiaru przy złączu, nie całkowitej
   długości profilu.
3. Każdy pozostały kandydat jest kasowany (albo tylko logowany w dry-run) —
   bez grupowania i bez wyboru "który zostaje".

**BRAMA DLA REGUŁY v5 PRZESZŁA 2026-09-23 (pierwsza runda).** Operator
zobaczył dry-run na żywym `[35021]` (poprawnie znalazł `8 mm` i `21 mm`,
zero fałszywych trafień — dokładnie ten sam widok co w PUŁAPCE 5, teraz bez
dedupu), i wprost potwierdził na pytanie o realne kasowanie ("tak dryrun
false"). Program realnie skasował oba wymiary na żywym rysunku, operator
potwierdził wizualnie (zrzut ekranu), że wygląda poprawnie.

### PUŁAPKA 6 (v5, ten sam dzień): `21 mm` (promień rury) niepotrzebnie kasowany

Zaraz PO przejściu bramy dla v5, operator użył programu na żywo na
`[35021]` i zgłosił: program skasował `21 mm` - **wymiar promienia rury,
potrzebny na budowie ("skąd oni będą wiedzieć jakiej średnicy ma być
rura")**. To NIE była kwestia brakującego zamiennika (wymiar wcięcia wciąż
nie jest wpięty do głównego przycisku) - `21 mm` to w ogóle NIE jest
artefakt złącza, tylko normalny, płaski wymiar 2D promienia profilu
okrągłego, który przypadkiem "dotyka osi" (lokalny punkt referencyjny rury
leży na osi z definicji - patrz "Pułapki API" niżej), dokładnie jak w
zdaniu ostrzegawczym, które już tam było, zanim ten bug wystąpił.

**Diagnoza przez `--diag-dimension-style` (Wartość+Start+End w jednym
logu, dodane w tej sesji specjalnie do tej diagnozy) na żywym `[35021]`:**

```
"21 mm": Start=(0;-21,20;0) End=(7,68;0;0)      - Z=0 na OBU końcach (płaski)
"8 mm":  Start=(0;-21,20;0) End=(7,68;0;21,20)  - Z zmienia się 0 -> 21,2
```

Obie mają `Y=0` na jednym końcu (dlatego v5 łapała obie), ale tylko `8 mm`
ma realną głębię `Z` - to znak, że jej koniec leży na SKOŚNYM cięciu (stąd
faktyczny artefakt złącza). `21 mm` jest w całości płaska (`Z=0` zawsze) -
zwykły wymiar promienia, nie ma nic wspólnego ze skosem.

**Potwierdzone na `[3.5013]` (regresyjnie, oba końce osobno):** ten sam
wzorzec powtarza się na KAŻDYM końcu tego elementu - para dwóch wymiarów
`21mm`/`21mm` (nie do odróżnienia po samej wyświetlanej wartości!), jeden
płaski (Z=0 zawsze, zostaje), jeden ze skosem (Z się zmienia, artefakt,
kasowany). **To doprecyzowuje starą "PUŁAPKĘ 5"**: para `21`/`21` na
`[3.5013]` to nie były "dwa prostopadłe pomiary tego samego skosu" (jak
zgadywano wcześniej), tylko dokładnie ta sama para "promień (płaski) +
artefakt (skośny)" co na `[35021]`, tylko powielona na obu końcach.

**Naprawa (`RoAxisDimensionService.TouchesAxis`):** dodany wymóg, że `Z`
musi faktycznie różnić się między końcami wymiaru (`zDiffers`), zanim
w ogóle rozważamy, czy któraś współrzędna dotyka zera. Płaskie wymiary
(`Z` stałe, typowo `0`) są od razu wykluczone - niezależnie od tego, czy
ich `Y` przypadkiem trafia w zero.

**Znana granica tej poprawki** (NIE zmierzona, do sprawdzenia przy
kolejnym złączu): gdyby skos leżał tak, że artefakt wychodzi płaski w `Z`
(cała rozpiętość w `Y`), ten warunek błędnie by go NIE złapał. Nie
zaobserwowane na `[35021]`/`[3.5013]` - oba mają skos, który daje realną
głębię `Z` na artefakcie.

**Operator poprosił dodatkowo:** `21 mm` (promień) docelowo warto ręcznie
rozciągnąć na pełną szerokość rury (`42 mm`, średnica) - POKAZANE na żywo
w Tekli (ręczne przeciągnięcie uchwytu wymiaru), NIE zautomatyzowane w
kodzie - to osobna, przyszła praca, nie część tej naprawy.

**BRAMA DLA POPRAWIONEJ REGUŁY PRZESZŁA 2026-09-23 (druga runda,
tego samego dnia).** `dryRun` cofnięty na `true` na czas weryfikacji.
Operator sprawdził dry-run żywym przyciskiem `MainForm` na obu końcach
`[3.5013]` (każdy koniec dał dokładnie 1 kandydata, `21 mm` zachowany w
logu), obejrzał widoki w Tekli, i na pytanie "czy rysunek nadal opisuje
wszystko co musi opisywać?" (zadane bez podpowiadania) odpowiedział "tak".
Na wprost zadane pytanie o `dryRun: false` odpowiedział "tak". Realne
kasowanie NIE zostało jeszcze użyte na żywo z tą regułą (tylko dry-run) -
do potwierdzenia przy najbliższym realnym użyciu.

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
częstych zmian reguły. **Od 2026-09-29 skrót znowu wskazuje na
zainstalowaną kopię** (operator zainstalował v0.3.3) — przebudowanie
projektu NIE zmienia tego, co operator odpala z pulpitu. Do testu nowej
zmiany uruchamiać `bin\x64\Debug\net48\RoAxisDimensionRemover.exe` wprost.

## Wymiar wcięcia (cut fitting) — PRODUKCYJNE OD 2026-09-25, BEZ BLOKADY RYSUNKU

Program po skasowaniu wymiarów do osi dorysowuje nowy wymiar (długość i
szerokość) opisujący wcięcie (cut fitting) profilu w złączu — żeby rysunek
nie tracił informacji, tylko zmieniał jej formę. **Kasowanie już działa
(patrz wyżej). Tworzenie nowego wymiaru działa też, POTWIERDZONE WIZUALNIE
PRZEZ OPERATORA na `[35021]` (jedna ściana cięcia) i `[3.5013]` (dwie
ściany, asymetria widoku szerokość/długość - patrz sekcja "REALNY test na
[3.5013] (2026-09-25)").**

**Klasa `NotchPilot` przestała być "pilotem"** — 2026-09-25 operator
świadomie zdjął blokadę `PilotDrawingMark`/`AllowedRealInsertMarks`
(metody `InsertWidthTest`/`InsertLengthTest` przemianowane na
`InsertWidth`/`InsertLength`, komunikaty logu bez prefiksu "TEST", trzy
przyciski testowe w `MainForm.cs` zastąpione jednym: "Wstaw wymiar
wcięcia dla złączy na wybranym rysunku"). Program wstawia teraz wymiar
wcięcia dla KAŻDEGO wymiaru do osi na KAŻDYM rysunku, nie tylko na
dwóch przetestowanych.

**Świadomie zaakceptowane ryzyko rezydualne:** nierozwiązany problem z
2026-09-24 ("ma ścianę cięcia" ≠ "potrzebuje wymiaru wcięcia" — patrz
sekcja "REALNY test na [3.5013] (2026-09-24)") NIE został rozwiązany,
tylko świadomie zignorowany na życzenie operatora, poinformowanego wprost
o tym konkretnym ryzyku (drugi koniec `[3.5013]`, który operator sam
odrzucił 24.09, znowu dostanie wymiar, bo program nie ma żadnej reguły,
która by to odróżniła). Nie traktować braku dalszych zgłoszeń jako
potwierdzenia, że to już nieszkodliwe — po prostu nikt jeszcze tego nie
sprawdził na tym konkretnym złączu po zdjęciu blokady.

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
     Patrz `FindChord`/`Centroid`/`LoopSpan` w `NotchPilot.cs`.
5. **Przeliczenie punktów bryły (globalne współrzędne modelu) na
   współrzędne widoku, żeby dało się z nich zrobić `StraightDimension`:**
   `View.DisplayCoordinateSystem` — dokumentacja wprost mówi "can be used
   to transform global points from the model that is to be inserted in the
   view". `CoordinateSystem` (Origin + AxisX + AxisY, Vector) NIE ma
   gotowej metody transformacji w Open API — trzeba liczyć ręcznie:
   `relatywny = punkt - Origin`, potem rzut przez iloczyn skalarny na
   znormalizowane `AxisX`/`AxisY`/`AxisX.Cross(AxisY)`. Patrz
   `ToViewSpace` w `NotchPilot.cs`.
6. **Silna walidacja przeliczenia**: przeliczona "szerokość cięcia" w
   układzie widoku wyszła `(7,68;-21,20;0,00) -> (7,68;21,20;0,00)` —
   współrzędna X (7,68) niemal identyczna z X starego, skasowanego wymiaru
   `21 mm` (miał `End.X=7,7`), a rozpiętość Y (`±21,2`) to dokładnie promień
   profilu. To niezależne potwierdzenie, że transformacja model→widok jest
   poprawna (zgadza się z realną, wcześniej istniejącą geometrią rysunku).

### `NotchPilot.cs` — pierwszy realny insert, potwierdzony operatorem

W kolejnej sesji (kontynuacja przez ChatGPT, ten sam wątek) dopisano
`NotchPilot.cs` i dwa przyciski testowe w `MainForm.cs` ("TEST: wstaw
szerokość wcięcia 42,4 mm ([35021])" i analogiczny dla długości 45,09 mm).
**Oba przyciski zostały użyte na żywo 2026-09-23, oba `StraightDimension.
Insert()` + `CommitChanges()` powiodły się, i operator PO OBEJRZENIU
rysunku w Tekli potwierdził wprost: "ta na koniec połozenia były
poprawne."** To pierwsze udane przejście przez wzorzec bramy bezpieczeństwa
dla TWORZENIA (nie tylko kasowania) na tym projekcie - dowód, że cała
matematyka wyżej (wybór ściany, cięciwa, transformacja do układu widoku)
faktycznie daje wizualnie poprawny wynik, nie tylko liczbowo spójny.

**Dlaczego to wciąż "pilot", nie gotowa reguła produkcyjna:**

- **Twardo zablokowany do `PilotDrawingMark = "[35021]"`** w kodzie
  (`NotchPilot.InsertTest`) — próba na innym rysunku od razu przerywa się
  komunikatem "TEST WSTRZYMANY", nic nie wstawia. To świadome ograniczenie,
  nie bug — zdjąć dopiero po sprawdzeniu na kilku różnych złączach.
- Wymaga **dokładnie jednego** płaskiego (Z≈0 po projekcji na widok)
  kandydata w całym rysunku — jeśli znajdzie 0 albo >1, zatrzymuje się i
  loguje liczbę, nie zgaduje który wybrać. Bezpieczne, ale też oznacza, że
  nie zadziała automatycznie na rysunku z wieloma złączami w jednym widoku
  bez dalszej pracy (np. ograniczenia do widoku wybranego przez operatora,
  tak jak robi to już `RemoveAxisDimensions`).
- **Przejmuje styl (Attributes) z innego, istniejącego `StraightDimension`
  w tym samym widoku** (`FindReferenceDimension` — pierwszy napotkany
  wymiar, który NIE jest tym samym punktem startu/końca) zamiast domyślnego
  stylu Tekli - dobre, żeby nowy wymiar wyglądał jak reszta rysunku, ale
  zakłada, że w widoku JEST jakiś inny wymiar do skopiowania; jeśli nie ma
  żadnego, `InsertTest` zatrzymuje się z "nie znaleziono istniejącego
  wymiaru jako wzorca stylu" (bezpieczne, ale trzeba to obsłużyć inaczej,
  jeśli reguła ma działać ogólnie).
- Kierunek odsunięcia linii wymiarowej (`side`) liczony jako obrót
  `UpDirection` wzorca o 90° w płaszczyźnie widoku - zadziałało wizualnie
  w tym jednym teście, ale to heurystyka, nie zmierzona reguła - potwierdzić
  na kolejnych złączach, zwłaszcza o innej orientacji.
- **Problem głębi (Z) w widoku, opisany wcześniej dla "długości cięcia",
  NIE zablokował insertu** (`Math.Abs(candidate.Start.Z) <= NumericalZero`
  akurat przeszło dla obu testów na `[35021]` - `NumericalZero = 0.000001`,
  czyli w praktyce wymaga Z dokładnie zero, nie tylko "małe"). Nie wiadomo
  jeszcze, czy na INNYM złączu (inny kąt, inna orientacja widoku) ta sama
  ściana cięcia da Z bliskie zeru, czy nie - do sprawdzenia przy kolejnych
  próbach, nie zakładać, że problem zniknął na stałe.
- Reguła znajdowania "ściany cięcia" (>1 pętla + normalna nierównoległa do
  osi belki) wciąż sprawdzona na JEDNYM złączu. Przed użyciem produkcyjnym:
  sprawdzić na kilku różnych złączach (różne kąty, może różne profile) —
  nie ufać jednemu przypadkowi, dokładnie tak samo jak przy regule
  kasowania (PUŁAPKA 5 była błędem uogólnionym z zbyt małej liczby
  przypadków).
- **Nowa diagnostyka `--diag-dimension-style`** (`DiagRunner.
  RunDimensionStyleDiag`, tylko odczyt) loguje `StartPoint`/`EndPoint`/
  `UpDirection`/`Distance`/`Attributes` wszystkich istniejących wymiarów na
  aktywnym rysunku — to źródło danych, z którego `NotchPilot` bierze
  realny styl zamiast zgadywać.

### POTWIERDZONY NA ŻYWO nowy gap: bryła może mieć WIĘCEJ NIŻ JEDNĄ ścianę cięcia

Test na drugim złączu (`--diag-mark "[3.5013]"` + `--diag-notch`,
2026-09-23, dokładnie to, co pkt 1 "Następnych kroków" kazał zrobić przed
odblokowaniem pilota) pokazał realny przypadek: bryła tego złącza ma
**DWIE** ściany cięcia (obie pierścienie z 2 pętlami, obie 24
wierzchołki), nie jedną jak na `[35021]`:

```
[notch]   UWAGA: 2 kwalifikujących się ścian cięcia w tej bryle
[notch]   KANDYDAT (Normal=(-0,71;0,71;0,00)): długość=59,96 mm szerokość=42,40 mm
[notch]   KANDYDAT (Normal=(0,71;0,00;-0,71)):  długość=59,96 mm szerokość=42,40 mm
```

To dwa RÓŻNE końce tego samego krótkiego kawałka, każdy przycięty pod 45°
do innego sąsiedniego elementu (`42,4/cos(45°)=59,96` — zgadza się).
**Poprzednia wersja `TryLogNotchCandidate` cicho wybierała tylko ścianę o
większym `LoopSpan` w CAŁEJ bryle i gubiła drugą** — to była niesprawdzona,
zgadywana reguła (dokładnie to, przed czym ostrzega AGENTS.md pkt 4).
Naprawione: funkcja teraz zbiera i loguje WSZYSTKIE kwalifikujące się
ściany osobno (`candidateFaces`), zamiast automatycznie wybierać jedną.

**To doprecyzowuje starą historię PUŁAPKI 5** (para `21`/`21` na tym samym
`[3.5013]`): pasujące dwa razy dwa (4 kandydaty do usunięcia = po 2 na
każdy koniec) sugeruje, że to mogły być dwa RÓŻNE cięcia z każdej strony
złącza, niekoniecznie tylko "dwa prostopadłe pomiary jednego skosu" jak
opisano wcześniej. Nie zmieniono opisu PUŁAPKI 5 niżej (wciąż trafny co do
WNIOSKU: to nie duplikat), ale mechanizm mógł być prostszy niż sądzono.

**Konsekwencja dla `NotchPilot`/produkcyjnej reguły: potrzebna jest reguła
"która ściana cięcia odpowiada któremu usuwanemu wymiarowi/któremu
złączu"** — obecny kod (i `NotchPilot.TryFindChord`, który ma tę samą
"weź największy `LoopSpan` w całej bryle" logikę co stara wersja diagu) NIE
ma jeszcze tej reguły. To nierozwiązane, potwierdzone na żywo, NIE
hipoteza — patrz "Następne kroki" niżej.

### Reguła dopasowania ściana↔wymiar — ZAPROJEKTOWANA I ZWERYFIKOWANA (2026-09-23), NIE WPIĘTA JESZCZE do `NotchPilot`

Zaprojektowana na danych z `[35021]` (1 ściana) i `[3.5013]` (2 ściany) —
bez trzeciego przykładu, na wyraźną decyzję operatora ("te co wiemy z 35021
i 3.5013"), więc AGENTS.md pkt 4 (nie zgadywać reguły) jest spełniony przez
dane, które już mieliśmy, nie przez pominięcie kroku.

**Kluczowy fakt, który to umożliwia:** `StraightDimension.StartPoint`/
`EndPoint` (wymiar do osi, do usunięcia) i punkty ściany cięcia po
`View.DisplayCoordinateSystem`/`ToViewSpace` żyją w TYM SAMYM lokalnym
układzie widoku — więc "który kandydat jest bliżej" to bezpośrednie
porównanie odległości, nie coś wymagającego dodatkowej transformacji.

**Reguła:** dla usuwanego wymiaru do osi weź środek jego `StartPoint`/
`EndPoint`; dla każdej kandydującej ściany cięcia weź centroid jej
zewnętrznej pętli, przeliczony do układu widoku; wybierz ścianę o
najmniejszym dystansie środek-wymiaru↔centroid-ściany.

**Zweryfikowane nowym trybem diagnostycznym `--diag-notch-match "[Mark]"`**
(`DiagRunner.RunNotchMatchDiag`, tylko odczyt, jak cała reszta `--diag-*`):

- `[3.5013]`: dwa końce tego samego kawałka są ~5775 mm od siebie w
  układzie widoku. Każdy z 4 wymiarów do osi (po 2 na koniec) trafił we
  właściwą, BLISKĄ ścianę (odległość 15,0-18,4 mm), nie w drugą, odległą o
  ~5775 mm — separacja jest więc jednoznaczna, nie przypadkowa.
- `[35021]`: wszystkie 3 znalezione wymiary do osi trafiły w jedyną
  istniejącą ścianę (odległości 11,3-78,5 mm) — zgodne z oczekiwaniem
  (jest tylko jeden kandydat, więc "dopasowanie" jest trywialne, ale
  potwierdza, że reguła nie psuje prostego przypadku).

**Nadal NIE wpięte do głównego przycisku.** Sama reguła była
zweryfikowana ODCZYTOWO (log, brak modyfikacji rysunku) - to nieskrócone,
mimo że reguła wygląda obiecująco na obu przykładach.

**2026-09-24: reguła wpięta do `NotchPilot`, i to potwierdzone DRY-RUNEM
na żywym `[3.5013]`.** `InsertWidthTest`/`InsertLengthTest`/`InsertTest`
przyjmują teraz opcjonalny `TSG.Point referencePoint = null` (domyślnie
`null` - zachowanie obu istniejących przycisków testowych w `MainForm.cs`,
które go NIE przekazują, jest więc niezmienione: nadal wymagają dokładnie
1 kandydata w całym rysunku). Gdy `referencePoint` jest podany I
kandydatów jest więcej niż 1, `InsertTest` wybiera najbliższy (środek
cięciwy kandydata vs `referencePoint`) zamiast się zatrzymywać - i JAWNIE
loguje, że wybrał, z odległością (nie cicho, zgodnie z ostrzeżeniem w
kodzie o poprzednim błędzie z `LoopSpan`).

**`InsertTest` dostał też `dryRun` (domyślnie `false`, zachowanie
przycisków bez zmian).** Gdy `dryRun: true`: blokada
`PilotDrawingMark = "[35021]"` jest POMIJANA (dry-run nic nie modyfikuje,
więc bezpiecznie testować regułę na INNYCH złączach bez zdejmowania
blokady dla realnego insertu), a na końcu zamiast `Insert()`/
`CommitChanges()` program tylko LOGUJE, co by wstawił. Nowy tryb
konsolowy `--diag-notch-insert-dryrun "[Mark]"` (`DiagRunner.
RunNotchInsertDryRun`, zawsze `dryRun: true`, jak cała reszta `--diag-*`)
dla KAŻDEGO wymiaru do osi w rysunku (tego samego, który
`RemoveAxisDimensions` by skasował) woła `NotchPilot` z jego środkiem jako
`referencePoint` - dokładnie scenariusz produkcyjny, tylko bez ryzyka.

**Wynik dry-run na żywym `[3.5013]` (2026-09-24, przed realnym testem):**
oba końce złącza (odległe ~5775 mm) dały spójne liczby: szerokość
`42,40 mm`, "długość" `59,96 mm` (rzut RAW distance, patrz niżej dlaczego
to się okazało mylące). Dopasowanie wybrało właściwego kandydata za każdym
razem (odległość `18,36 mm`).

### REALNY test na `[3.5013]` (2026-09-24) - trzy kolejne poprawki, jeden nierozwiązany problem domenowy

Operator tymczasowo dopuścił `[3.5013]` do realnego insertu
(`AllowedRealInsertMarks` w `NotchPilot.cs`, OBOK `PilotDrawingMark`, nie
zamiast). Realny test ujawnił po kolei TRZY błędy, których żaden dry-run
by nie złapał (dry-run tylko liczy geometrię, nie sprawdza co się NAPRAWDĘ
dzieje po `Insert()`/`CommitChanges()` ani jak Tekla to renderuje):

1. **Przyciski blokowały się na stałe** (`Enabled = false` bez powrotnego
   `= true` w `finally`) - operator nie mógł kliknąć drugi raz. Naprawione:
   usunięte blokowanie w ogóle (guard zostaje tylko `_busy`).
2. **`Insert()`+`CommitChanges()` zwróciły `true`, ale niezależny odczyt
   (osobny proces, `--diag-dimension-style`) NIE znalazł wstawionego
   wymiaru** - fałszywy "sukces" w logu. Naprawione: `InsertTest` teraz
   odczytuje widok PONOWNIE po insercie (`HasSameDimension`) i dopiero
   wtedy loguje sukces - jeśli się nie utrwaliło, log mówi to wprost.
   Przy okazji: reużycie TEGO SAMEGO uchwytu `Drawing` na drugi insert
   (szerokość, potem długość) po `CommitChanges()` dawało błędną blokadę
   marki (`drawing.Mark` przestawał się zgadzać) - `MainForm` odświeża
   uchwyt (`handler.GetActiveDrawing()`) między wywołaniami.
3. **Kierunek wymiaru - DWIE złe wersje po kolei:**
   - Kopiowanie `UpDirection` z przypadkowego istniejącego wymiaru w
     widoku dało wymiarowi długości (cięciwa UKOŚNA) rzut na zły kierunek -
     wyświetliło "42" zamiast prawdziwych 59,96 mm (PUŁAPKA 2: `Straight
     Dimension` pokazuje RZUT na kierunek prostopadły do `Up`, nie surowy
     dystans).
   - Poprawka "licz kierunek z samej cięciwy" (Start→End) DAWAŁA poprawną
     liczbę (59,96 mm), ale wymiar wychodził **PO SKOSIE** - operator to
     odrzucił na żywo: "wymiaru nie daje się po skosie, zawsze prostopadle
     lub równolegle do parta". To twarda zasada tego biura rysunkowego, nie
     kwestia gustu.
   - Poprawka finalna: kierunek liczony z **OSI BELKI** (`TSM.Beam.
     StartPoint/EndPoint`, przeliczonej do układu widoku przez nowy
     `ToViewSpaceVector` - jak `ToViewSpace`, ale dla wektora/kierunku, bez
     odejmowania `Origin`). Długość wcięcia = rzut cięciwy na kierunek
     RÓWNOLEGŁY do osi; szerokość = rzut na kierunek PROSTOPADŁY. Log też
     poprawiony - liczy tę samą rzutowaną wartość, nie surowy
     `Distance(Start, End)` (który wcześniej kłamał identycznie jak sama
     Tekla, tylko w drugą stronę).
   - **Dla złącza pod DOKŁADNIE 45° oba rzuty (równoległy i prostopadły)
     tej samej przekątnej cięciwy wychodzą sobie równe** (`42,40 mm` obie).
     Operator potwierdził to na żywo jako POPRAWNE ("no jest 42 tak jak
     powinno być") - to geometria tego konkretnego kąta, nie błąd. Na
     innym kącie (np. `19,9°` na `[35021]`) wyjdą różne liczby.

**Przycisk testowy też naprawiony, żeby próbował WSZYSTKICH znalezionych
wymiarów do osi w jednym kliknięciu** (wcześniej brał tylko pierwszy -
drugi klik zawsze trafiał w ten sam, już wstawiony wymiar i mówił "już
istnieje", nigdy nie docierając do drugiego końca złącza).

**NIEROZWIĄZANY PROBLEM DOMENOWY (nowy, 2026-09-24, NIE kodowy):** po
wstawieniu wymiarów na OBU końcach `[3.5013]`, operator obejrzał wynik i
stwierdził, że drugi koniec (geometrycznie MA drugą ścianę cięcia,
potwierdzoną wcześniej przez `--diag-notch` - patrz sekcja wyżej) **nie
powinien dostać wymiaru wcięcia** - w tym konkretnym widoku wygląda jak
zwykłe, proste (okrągłe) zakończenie profilu, nie jak widoczny skos.
Operator: "to ma ścięcia jakby, ale nie powinno tu być tego chyba... tylko
żeby na skosach było, wystarczy z jednej strony." Czyli: **"ściana cięcia
istnieje w bryle" (`FindChordCandidates`/`--diag-notch`) NIE jest tym samym
co "to złącze potrzebuje wymiaru wcięcia w TYM rysunku"** - to jest
zupełnie nowe rozróżnienie, którego obecny kod nie robi WCALE (insertuje
dla każdego geometrycznego kandydata, bez pytania, czy to złącze jest
tym, które operator faktycznie chce opisać). Nie zgadywać reguły - do
zaprojektowania z operatorem, prawdopodobnie na podstawie: czy w widoku
jest już adnotacja kąta (`"45°"`/`"19,90°"`) blisko tego końca (sygnał, że
skos jest tam widoczny/ważny), a nie tylko czy bryła ma tam drugą ścianę.
**Test na `[3.5013]` zakończony przez operatora zamknięciem rysunku bez
zapisu** (nie Ctrl+Z) - rysunek z powrotem w stanie sprzed testu
(potwierdzone `--diag-dimension-style`: 5 oryginalnych wymiarów, zero
śladów testu). Blokada `PilotDrawingMark`/`AllowedRealInsertMarks` dla
REALNEGO insertu NIE przeszła jeszcze pełnej bramy - patrz "Następne
kroki" pkt 2/3, wymaga rozwiązania powyższego problemu domenowego
najpierw.

**Próba rozwiązania problemu domenowego (2026-09-24, ODŁOŻONA - dwie
hipotezy obalone danymi, operator zdecydował nie kopać dalej teraz):**
dopisano `--diag-view-objects "[Mark]"` (`DiagRunner.RunViewObjectsDiag`,
tylko odczyt - zrzuca typy WSZYSTKICH obiektów w każdym widoku, nie tylko
wymiarów). Sprawdzone na `[3.5013]`:
- Hipoteza 1 (obecność `AngleDimension` - obiektu "45°" - jako sygnał "to
  złącze trzeba opisać"): OBALONA. Oba widoki (dobry koniec i ten, który
  operator odrzucił) mają dokładnie `AngleDimension x1` każdy - nie
  odróżnia.
- Hipoteza 2 (typ widoku - `DetailView` vs zwykły `View`): OBALONA. Oba
  widoki to zwykły `View`.
- Oba widoki mają też identyczny zestaw typów obiektów poza tym
  (`Connection x1`, `Part x1`, różne tylko liczbą `StraightDimension`/
  `Mark`/`LeaderLine`) - żaden prosty sygnał typu obiektu nie odróżnia.
**Wniosek: to rozróżnienie NIE wynika z prostego inwentarza typów obiektów
w widoku - albo trzeba by zajrzeć głębiej w konkretne właściwości
`Connection` (np. relację do sąsiedniego elementu `31056` widocznego na
zrzutach operatora), albo to faktycznie wymaga oceny wizualnej człowieka,
której nie da się łatwo zredukować do reguły API.** Operator zdecydował
(2026-09-24) odłożyć to na razie, zamiast kopać dalej "na wyczucie" -
zgodnie z AGENTS.md pkt 4. `--diag-view-objects` zostaje w kodzie jako
narzędzie do ewentualnego podjęcia tematu później.

### REALNY test na `[3.5013]` (2026-09-25) - NOWY błąd: szerokość i długość wylądowały w DWÓCH RÓŻNYCH widokach

Operator tymczasowo dopuścił `[3.5013]` do realnego insertu (jak 2026-09-24,
ta sama para flag: `AllowedRealInsertMarks` w `NotchPilot.cs` + limit w
`MainForm.NotchMultiFaceTestButton_Click` do PIERWSZEGO znalezionego końca,
żeby nie dotykać spornego drugiego końca z sesji 2026-09-24). Insert
zwrócił sukces dla obu wymiarów (szerokość i długość), z potwierdzeniem
przez ponowny odczyt widoku (`HasSameDimension`) - żadna z lekcji
2026-09-24 (fałszywy sukces, zły kierunek) się nie powtórzyła. Mimo to
operator, patrząc na żywy rysunek, zgłosił: **"na drugiej belce na ścięciu
nic nie ma, a na dolnej jest jedno 42 na długość, nie ma na szerokość"** -
czyli oba wstawione wymiary NIE są w tym samym widoku, mimo że powinny
opisywać TEN SAM koniec.

**Potwierdzone przez `--diag-dimension-style` (nie zgadywanie):** widok A
(ten z oryginalnymi wymiarami bliskiego końca, `21mm`/`21mm`/`42mm`-
długość) dostał tylko wymiar DŁUGOŚCI. Widok B (ten z oryginalnymi
wymiarami DALEKIEGO końca i całkowitą długością `5796mm`) dostał wymiar
SZEROKOŚCI - mimo że oba inserty startowały z TEGO SAMEGO
`referencePoint` (środek tego samego usuwanego wymiaru do osi, bliski
koniec). Insert szerokości i insert długości, wywołane jeden po drugim z
tym samym punktem referencyjnym, wybrały kandydatów z DWÓCH RÓŻNYCH
widoków.

**Przyczyna:** `NotchPilot.InsertTest` zbiera kandydatów (ściana cięcia →
cięciwa) ze WSZYSTKICH widoków rysunku naraz, licząc "najbliższy
punktowi referencyjnemu" po surowej odległości, BEZ wiedzy o tym, z
którego widoku pochodzi sam `referencePoint`. Odległość porównuje punkty
przeliczone przez `ToViewSpace` przy użyciu `view.DisplayCoordinateSystem`
KAŻDEGO widoku z osobna - a jeśli lokalne układy współrzędnych dwóch
widoków tego samego złącza dają zbieżne liczby dla tego samego fizycznego
miejsca, porównanie "najbliższy" może (i tu zrobiło) wybrać kandydata z
niewłaściwego widoku dla jednego z dwóch wywołań (szerokość), podczas gdy
drugie (długość) tego samego `referencePoint` trafiło poprawnie.

**Próba naprawy (WYCOFANA, dała wynik GORSZY niż brak poprawki):**
ograniczenie wyszukiwania kandydatów do widoku, z którego pochodzi
`referencePoint` (nowy parametr `referenceView` przekazywany z
`MainForm`/`DiagRunner`, dopasowanie po `view.Origin` z tolerancją -
`View.Name` okazał się PUSTY dla tych widoków, więc porównanie po nazwie
nigdy by nie zadziałało, `""!=""` zawsze `false`). Zweryfikowane
`--diag-notch-insert-dryrun` PRZED realnym insertem (dobra dyscyplina -
błąd złapany bez dotykania modelu): dla wymiaru bliskiego końca (mid
X≈10,6) reguła z ograniczeniem do widoku wybrała kandydata o
współrzędnych DALEKIEGO końca (X≈5775), i odwrotnie dla dalekiego końca -
dopasowanie kompletnie ODWRÓCONE, gorsze niż oryginalny błąd.

**Wniosek z surowych danych (zmierzone, nie zgadane):** `view.Origin`
dwóch widoków tego rysunku faktycznie się różni (`(37,10;102,92)` vs
`(37,10;167,90)` - to naprawdę dwa różne obiekty widoku), ale **JEDEN
widok potrafi poprawnie reprezentować geometrię OBU końców złącza
naraz** - lokalna oś X widoku odpowiada odległości wzdłuż całej belki,
wspólnej dla obu końców, nie jest "wyzerowana" osobno przy każdym końcu.
To obala założenie stojące za próbą naprawy ("ogranicz do widoku
źródłowego wymiaru") - ograniczenie do jednego widoku nie eliminuje
dwuznaczności, bo w TYM widoku nadal istnieją kandydaci obu końców, tylko
z tej pary każdy widok "widzi płasko" (Z≈0 po projekcji) inną ścianę niż
się spodziewano. Prawdziwa przyczyna leży najpewniej w tym, KTÓRA ściana
wychodzi płaska w danym widoku (zależne od orientacji widoku względem
płaszczyzny cięcia), nie w tym, z którego widoku pochodzi wymiar do
usunięcia - ale to WCIĄŻ nierozwiązane, nie zgadywać dalej bez kolejnej
rundy surowych danych (np. zrzut WSZYSTKICH kandydatów - obu ścian, z ich
Z po projekcji - osobno dla KAŻDEGO z dwóch widoków, zanim napisze się
kolejną wersję reguły).

**Ważna, nowa lekcja o samym procesie diagnozy:** ten błąd byłby NIEWYKRYTY
przez sam dry-run w takiej postaci, w jakiej istniał do tej pory -
`--diag-notch-insert-dryrun` loguje Start/End/wartość dla szerokości i
długości OSOBNO, ale nie loguje (i nie porównuje), czy oba insert dla
TEGO SAMEGO wymiaru do osi wylądowałyby w TYM SAMYM widoku. Liczby same w
sobie wyglądały wiarygodnie (42,40 mm i 59,96 mm, zgodne z wcześniejszymi
pomiarami) - dopiero PO realnym insercie i wizualnym obejrzeniu rysunku
było widać, że to dwa osobne widoki. Zanim ktoś zaufa temu dry-runowi
ponownie: dopisać do niego log identyfikujący widok (np. `view.Origin`)
obok każdego wstawianego wymiaru, żeby złapać tę klasę błędu ODCZYTOWO,
bez realnego insertu.

**Stan repo po sesji (2026-09-25, pierwsza runda):** wszystkie tymczasowe
zmiany (dopuszczenie `[3.5013]`, limit do pierwszego końca, próba filtra po
widoku) COFNIĘTE ręcznie (edycja, nie `git checkout` - zablokowany przez
klasyfikator auto mode jako nieodwracalna operacja na śledzonych plikach).
`dev` jest niezmieniony względem stanu przed sesją. Realny insert na żywym
`[3.5013]` z tej sesji cofnięty przez operatora w Tekli (Ctrl+Z),
potwierdzone `--diag-dimension-style`: 5 oryginalnych wymiarów, zero
śladów testu.

### Poprawiona przyczyna i finalna naprawa (2026-09-25, druga runda tego samego dnia)

Po przerwie na zwolnienie licencji Tekli, sesja wróciła do problemu z
nowym narzędziem: **`--diag-notch-raw "[Mark]"`** (`DiagRunner.
RunNotchRawDiag`, tylko odczyt) — zrzuca dla KAŻDEGO widoku i KAŻDEJ
kwalifikującej się ściany cięcia obie cięciwy (długość i szerokość) z
pełnym `X;Y;Z`, BEZ filtra płaskości, plus jawną flagę `płaska(Z≈0)`.
Surowe dane na żywym `[3.5013]` ujawniły prawdziwy mechanizm: **w KAŻDYM z
dwóch widoków, jedna ściana ma płaską cięciwę DŁUGOŚCI, a DRUGA (przeciwna)
ma płaską cięciwę SZEROKOŚCI — nigdy obie naraz dla tej samej ściany w tym
samym widoku.** Fizycznie: krótki kierunek owalnego przecięcia rury
"ucieka w głąb kartki" akurat w tym widoku, który pokazuje długi kierunek
płasko, i odwrotnie w drugim widoku. To NIE jest błąd dopasowania (wczorajsza
diagnoza była błędna) — to fakt geometryczny o TYM konkretnym złączu (kąt +
orientacja obu widoków).

**Konsekwencja poprzedniego kodu:** filtr "tylko płaskie kandydaty"
(potrzebny, bo bez niego `StraightDimension` policzona z nieplaskiej
cięciwy daje `0,00 mm` - rzut na kierunek pomiaru wychodzi zerowy, patrz
niżej) w KAŻDYM widoku z osobna zostawiał dokładnie JEDNEGO płaskiego
kandydata - ale dla wymiaru SZEROKOŚCI był to zawsze kandydat PRZECIWNEJ
ściany, nie tej, do której należy wymiar do osi w tym widoku. Stąd błąd
"szerokość i długość w dwóch różnych widokach" z pierwszej rundy tej sesji.

**Operator wyjaśnił kryterium akceptacji w prostych słowach** (po tym, jak
żargon "widok"/"ToViewSpace" nie trafiał): liczy się tylko niebieska ramka
widoczna na rysunku (czyli faktycznie `View` z Open API - potwierdzone na
zrzutach ekranu). Zasada operatora: **pojedynczy wymiar nigdy nie jest
rozdzielony między dwie ramki (to i tak niemożliwe w tym API), ale długość
i szerokość TEGO SAMEGO końca MOGĄ być w RÓŻNYCH ramkach, jeśli inaczej się
nie da — najważniejsze, żeby nic się nie nakładało i nic nie wychodziło poza
ramkę.**

**Finalna reguła w `NotchPilot` (funkcja `Insert`, dawniej `InsertTest`)
— asymetryczna, zależna od `longest`:**
- **Długość** ogranicza wyszukiwanie kandydatów do widoku źródłowego
  wymiaru do osi (`referenceView`, dopasowanie po `View.Origin` z
  tolerancją - `View.Name` bywa puste, zmierzone 2026-09-25, więc
  porównanie po nazwie nigdy by nie zadziałało). W tym widoku płaski
  kandydat zawsze jest właściwą ścianą - zmierzone na obu końcach
  `[3.5013]`.
- **Szerokość** szuka po CAŁYM rysunku (bez ograniczenia do widoku) wśród
  TYLKO płaskich kandydatów (`flatCandidates`, filtr Z≈0 nie usunięty, w
  przeciwieństwie do próby z pierwszej rundy tej sesji) - to bezpieczne,
  bo w puli samych płaskich kandydatów każda ściana ma dokładnie jedną
  flat-reprezentację (we WŁAŚCIWYM dla niej widoku), więc "najbliższy
  środek cięciwy do środka wymiaru do osi" jednoznacznie trafia we
  właściwą ścianę - nawet jeśli ląduje w innym widoku niż długość tego
  samego końca. To ZAMIERZONE, potwierdzone wizualnie przez operatora na
  żywym `[3.5013]`: nowy wymiar szerokości pojawił się w drugiej ramce
  arkusza, w pustym miejscu, bez nakładania na istniejącą geometrię.

**BRAMA BEZPIECZEŃSTWA DLA TEJ POPRAWKI PRZESZŁA 2026-09-25.** Realny
insert na pierwszym końcu żywego `[3.5013]` (drugi koniec pominięty -
osobny, wciąż otwarty problem domenowy z 24.09, patrz niżej), operator
obejrzał wynik w Tekli i na pytanie "czy to wygląda poprawnie, nic się nie
nakłada, nic nie wychodzi poza ramkę?" odpowiedział "tak jest dobrze".

### Zdjęcie blokady rysunku i przejście z pilota na produkcję (2026-09-25)

Po potwierdzeniu poprawki operator poprosił wprost o zamianę przycisków
testowych na produkcyjne i zdjęcie blokady rysunku. Agent WYRAŹNIE nazwał
ryzyko przed wykonaniem (drugi koniec `[3.5013]`, odrzucony 24.09, znowu
dostanie wymiar, bo nie ma reguły odróżniającej) i zapytał operatora wprost
przez wybór z dwóch opcji - operator wybrał "zdejmij blokadę całkowicie",
świadomy tego konkretnego ryzyka. Wykonane zmiany:
- `NotchPilot.cs`: usunięte `PilotDrawingMark`/`AllowedRealInsertMarks` i
  cała blokada marki w `InsertTest`. Metody przemianowane
  `InsertWidthTest`→`InsertWidth`, `InsertLengthTest`→`InsertLength`,
  `InsertTest`→`Insert`. Komunikaty logu bez prefiksu "TEST"/"TEST
  WSTRZYMANY" (teraz "WSTRZYMANO").
- `MainForm.cs`: usunięte dwa przyciski testowe ograniczone do `[35021]`
  bez `referencePoint` (`_notchTestButton`, `_notchLengthTestButton` i ich
  handlery) - były zbędne, w pełni zastąpione ogólnym. Trzeci przycisk
  (`_notchMultiFaceTestButton`) przemianowany na `_insertNotchButton`
  ("Wstaw wymiar wcięcia dla złączy na wybranym rysunku"), limit do
  pierwszego końca (tymczasowy, z tej sesji) USUNIĘTY - przetwarza teraz
  WSZYSTKIE wymiary do osi na rysunku.
- `DiagRunner.cs`: zaktualizowane wywołania na nowe nazwy metod.

**Nierozwiązany problem z 2026-09-24 pozostaje otwarty i TERAZ BEZ
OSŁONY** - patrz ostrzeżenie w sekcji "Wymiar wcięcia" wyżej.

## KRYTYCZNE ZNALEZISKO 2026-09-25: `TouchesAxis` NIE była ograniczona do profili RO

Przy szukaniu nowego kandydata do testów (po tym, jak `[3.5013]` zostało
usunięte z modelu przez operatora - "All parts deleted") dopisano
`--diag-find-candidates` (skanuje WSZYSTKIE rysunki w modelu, bez
otwierania żadnego na ekranie, loguje te z choć jednym wymiarem do osi).
Pierwszy skan (2285 rysunków) pokazał **setki** trafień na `Blech`
(blacha), `Träger` (dźwigar), `Winkel` (kątownik) - profilach
CAŁKOWICIE INNYCH niż RO. Sprawdzenie kodu potwierdziło: `TouchesAxis`
(i cała `RemoveAxisDimensions`) to czysto geometryczny test (współrzędna
blisko zera + głębia Z + krótka własna długość) - **nic nigdy nie
sprawdzało, czy część jest w ogóle profilem RO**, mimo że nazwa
narzędzia i wszystkie komentarze zakładają wyłącznie RO.

**Potwierdzone na żywym `[21050]`** (blacha z rozstawem otworów
20/30/70/120/130 mm, otwarta w Tekli operatorowi do wglądu): program
"znalazł" 4 kandydatów do usunięcia (3× `30 mm`, 1× `130 mm`) - zwykłe,
potrzebne wymiary rozstawu otworów, zero związku ze skosem/cięciem rury.
Operator: "moim zdaniem nic nie powinniśmy kasować z tej blachy." To był
REALNY, nie teoretyczny, risk: gdyby ktoś kiedyś kliknął "Usuń wymiary do
osi" na widoku takiej blachy (albo dźwigara, kątownika), program
naprawdę by tam coś skasował.

**Naprawa (`RoAxisDimensionService.RemoveAxisDimensions`):** nowy guard
`ViewHasRoProfile` na samym początku metody - przez `Model.
SelectModelObject(Part.ModelIdentifier)` sprawdza, czy widok zawiera choć
jedną część, której `Profile.ProfileString` zaczyna się na `"RO"`
(konwencja katalogu Tekli, zmierzona na `[35021]`/`[3.5013]`:
`"RO42.4*3.2"`). Jeśli nie - metoda kończy się natychmiast, loguje
"Ten widok nie zawiera części o profilu RO... nic nie kasuję" i NIC nie
sprawdza dalej (`TouchesAxis` w ogóle się nie odpala dla takiego widoku).
Brak połączenia z `Model` = bezpieczny domyślny wynik `false` (nic nie
kasuj), nigdy "zgaduj, że to RO".

**Zweryfikowane na żywo po naprawie:**
- `[21050]` (blacha): `0` kandydatów (było `4`) - guard działa.
- `[35021]` (RO, pilot): dalej poprawnie `1` kandydat (`8 mm`, zgodne z
  historią wyżej) - guard NIE zepsuł istniejącej, potwierdzonej ścieżki.
- Pełny re-skan modelu (`--diag-find-candidates`, 2285 rysunków): `96`
  rysunków z kandydatami, WSZYSTKIE teraz `Geländer`/`Gitterrost`
  (poręcz/krata), `Bogen` (łuk poręczy), `Leiter` (drabina) - zero
  `Blech`/`Träger`/`Winkel`. Lista tych 96 rysunków to teraz gotowa pula
  kandydatów do dalszych testów (np. problemu z "Następne kroki" pkt 2).

**`DiagRunner.RunFindCandidatesDiag`** (nowa, trwała diagnostyka,
`--diag-find-candidates`, bez argumentu - skanuje CAŁY model) woła
PRAWDZIWĄ `RemoveAxisDimensions(dryRun: true)` zamiast duplikować
`TouchesAxis` samodzielnie - żeby ten skaner ZAWSZE korzystał z tego
samego guardu co produkcyjny przycisk, bez ryzyka, że ktoś naprawi jedno
miejsce a zapomni o drugim.

## Filtr kąta cięcia w `NotchPilot` (2026-09-25) - CZĘŚCIOWE rozwiązanie problemu z "Następne kroki" pkt 2

Po znalezieniu nowego kandydata `[3.5027]` (przez `--diag-find-candidates`
po tym, jak `[3.5013]` zniknęło z modelu) trafiono na kolejny przykład
złącza z DWIEMA ścianami cięcia - ale tym razem o wyraźnie różnym
charakterze: `--diag-notch-raw` pokazał, że jeden koniec ma cięciwę
długość/szerokość w stosunku `59,86/42,40 ≈ 1,41` (kąt cięcia ~45°), a
drugi `42,55/42,40 ≈ 1,003` (kąt cięcia ~4,8°). Operator, patrząc na żywy
rysunek: "z jednej strony płaskie z drugiej ścięte" - i Tekla ma tam
własne adnotacje kąta potwierdzające dokładnie te liczby: `4,75°` i
`44,90°` (niezależna weryfikacja, nie nasze liczenie).

**Wniosek: kąt cięcia ściany (a nie tylko sam fakt "ma ścianę cięcia") da
się zmierzyć i użyć jako filtr** - ściana o kącie bliskim zeru to
praktycznie zwykłe, płaskie zakończenie rury, nie potrzebuje wymiaru
wcięcia. Dodano `MinCutAngleDegrees = 10.0` w `NotchPilot.
FindChordCandidates`: liczy stosunek najkrótszej do najdłuższej cięciwy
zewnętrznej pętli ściany (`Math.Acos(minor/major)`), i jeśli wychodzący
kąt jest mniejszy niż próg, CAŁA ściana jest pomijana (nie trafia do puli
kandydatów w ogóle, ani dla długości, ani dla szerokości).

**(2026-09-30: próg obniżony do 6,5° — patrz „START SESJI TUTAJ”.)**
**Próg `10°` to SZACUNEK, nie pomiar** - w połowie między jedynymi trzema
zmierzonymi punktami danych: `~4,8°` (pomiń, `[3.5027]`), `~19,9°`
(wstaw, `[35021]`), `~45°` (wstaw, `[3.5013]`/`[3.5027]` drugi koniec).
Do doprecyzowania, gdyby pojawiło się złącze bliżej granicy.

**Zweryfikowane na żywo na `[3.5027]`:** dry-run pokazał `0 kandydatów
długości` dla płaskiego końca (filtr zadziałał), a realny insert
poprawnie wstawił szerokość i długość TYLKO dla ściętego końca - płaski
koniec nie dostał niczego nowego. `--diag-dimension-style` po insercie
potwierdził: widok1 +1 wymiar (`42,00 mm` szerokość, przy X≈352 - ścięty
koniec), widok2 +1 wymiar (`42,00 mm` długość, ten sam ścięty koniec),
zero zmian przy X≈0 (płaski koniec).

**To NIE rozwiązuje oryginalnej zagadki z `[3.5013]` (24.09)** - tamten
drugi, odrzucony koniec miał TEN SAM ~45° kąt co pierwszy, zaakceptowany
koniec (identyczny stosunek długość/szerokość), więc filtr kąta by go NIE
złapał. Powód odrzucenia tamtego konkretnego złącza pozostaje nieznany -
`[3.5013]` nie istnieje już w modelu (usunięte przez operatora), więc nie
da się go już zbadać dalej. Ten filtr jest dodatkowym, niezależnym
zabezpieczeniem przeciwko INNEJ kategorii fałszywych trafień (kąt bliski
zeru), nie pełnym rozwiązaniem "które złącze potrzebuje wymiaru wcięcia".

## `NotchPilot.InsertMissing` — insert napędzany geometrią, nie wymiarem do osi (2026-09-25)

Podczas testów na `[3.5027]` operator skasował stary wymiar do osi w
jednym widoku (`Usuń wymiary do osi`), a wcześniej wstawiony wymiar
DŁUGOŚCI wcięcia w TYM SAMYM widoku zniknął razem z nim — mimo że kod nigdy
nie prosił o usunięcie tego drugiego obiektu. To pasuje do znanego, ale
wcześniej tylko TEORETYCZNEGO ryzyka opisanego przy
`RoAxisDimensionService.DescribeDimensionSet`: Tekla potrafi grupować
sąsiadujące wymiary we wspólny `StraightDimensionSet` ("łańcuch"), i
skasowanie jednego elementu może kaskadowo zabrać resztę łańcucha. To
pierwszy RAZ, kiedy to faktycznie zaobserwowano na żywo, nie tylko w
komentarzu ostrzegawczym.

**Konsekwencja praktyczna:** stary mechanizm insertu (`InsertWidth`/
`InsertLength`, wywoływane z `MainForm` z punktem odniesienia
znalezionym przez skan ISTNIEJĄCYCH wymiarów do osi) nie ma jak
zadziałać, gdy ten wymiar do osi już zniknął (kaskadowo albo przez zwykłe
"Usuń") - nie ma punktu odniesienia, więc nic się nie wstawia, nawet jeśli
wymiar wcięcia faktycznie brakuje.

**Naprawa: `NotchPilot.InsertMissing(Drawing, log, dryRun)`** - całkiem
nowy, niezależny mechanizm. Zamiast szukać punktu odniesienia po
istniejącym wymiarze do osi, przechodzi PROSTO po geometrii: dla każdej
unikalnej części (po `ModelIdentifier`, żeby nie liczyć dwa razy tej samej
części widocznej w dwóch widokach) i każdej jej kwalifikującej się ściany
cięcia (ten sam filtr kąta co wyżej), liczy obie cięciwy (długość,
szerokość) we WSPÓŁRZĘDNYCH MODELU (`FindQualifyingChordPairs`), a
dopiero potem szuka WŚRÓD WSZYSTKICH widoków rysunku tego jednego, w
którym dana cięciwa wychodzi płasko (`InsertResolvedIfMissing`). Jeśli
wymiar w tym miejscu już istnieje (`HasSameDimension`) - nic nie robi,
cicho (to normalny, częsty przypadek). Jeśli nie istnieje - wstawia go,
tym samym stylem/kierunkiem co poprzednio (kod stylu/side/measureDir
przeniesiony bez zmian). Więcej niż jeden płaski widok dla tej samej
cięciwy = niejednoznaczne, program się wstrzymuje zamiast zgadywać.

**To całkowicie odsprzęga insert od tego, czy wymiar do osi jeszcze
istnieje** - działa tak samo dobrze na świeżym rysunku (nic jeszcze nie
wstawione), po częściowym wstawieniu (uzupełnia braki), i po kaskadowym
skasowaniu (naprawia lukę), bez rozróżniania tych przypadków w kodzie.

**`MainForm.InsertNotchButton_Click`** uproszczony: cała stara logika
skanowania wymiarów do osi (~30 linii) zastąpiona jednym wywołaniem
`NotchPilot.InsertMissing(drawing, Log)`, które zwraca liczbę realnie
wstawionych wymiarów (do ustawienia komunikatu w UI). Stare
`InsertWidth`/`InsertLength` (z `referencePoint`/`referenceView`) ZOSTAJĄ
w kodzie - wciąż używane przez `DiagRunner.RunNotchInsertDryRun`
(`--diag-notch-insert-dryrun`, oparte na istniejących wymiarach do osi,
przydatne jako niezależna, druga metoda weryfikacji tych samych liczb).
Nowa diagnostyka **`--diag-notch-fill-dryrun "[Mark]"`** (zawsze
`dryRun: true`) testuje właśnie `InsertMissing`.

**Zweryfikowane na żywo na `[3.5027]`:** dry-run na świeżym rysunku
(5 oryginalnych wymiarów, nic wstawione) poprawnie znalazł DOKŁADNIE 2
braki (długość i szerokość ściętego końca), zero dla płaskiego końca.
Realny insert przez przycisk potwierdzony `--diag-dimension-style`. Po
kolejnym "Usuń wymiary do osi" na widoku ściętego końca (tym razem BEZ
kaskady - `42 mm` długości przetrwało) przycisk "Wstaw" poprawnie
zgłosił "nie wstawiono nowego wymiaru", bo faktycznie niczego nie
brakowało - potwierdza, że `HasSameDimension` nie wstawia duplikatów.

## Dalsze testy skanera modelu (2026-09-28): `[225.130]` (nie kasować!), `[35095]` (kolejny sukces filtra kąta)

Skorzystano z listy 96 kandydatów z `--diag-find-candidates` (patrz
"KRYTYCZNE ZNALEZISKO 2026-09-25"), żeby przetestować regułę na świeżych
złączach.

**`[225.130]` (`Gelander`, CAŁY ZESPÓŁ balustrady z płytkami i śrubami
M16, NIE `Einzelteil`) - 14 kandydatów do usunięcia, WSZYSTKIE
FAŁSZYWE.** `--diag-notch-raw` pokazał wszystkie ściany z normalnymi
DOKŁADNIE osiowymi (`(1,0,0)`, `(0,0,1)` itd.) i identyczną wartością
`48,30 mm` dla długości I szerokości każdej ściany (kąt cięcia = 0°,
zwykłe proste zakończenia rur `RO48.3*3.2`) - filtr kąta poprawnie
odrzucił wszystkie (`--diag-notch-fill-dryrun` nie znalazł nic do
wstawienia). ALE same 14 kandydatów do USUNIĘCIA (wartości `50/193/57
mm`) po sprawdzeniu współrzędnych (`--diag-dimension-style`) okazały się
**geometrią detali śrubowych/płytkowych** (powtarzający się wzorzec przy
każdym z ~7 słupków, pasujący do symboli "2 x M16x45-ISO4017" widocznych
na rysunku) - NIE artefaktami skosu rury. **Wniosek: reguła
`TouchesAxis`/`RemoveAxisDimensions` była projektowana i testowana
WYŁĄCZNIE na rysunkach `Einzelteil` (pojedyncza część) - na rysunkach
CAŁEGO ZESPOŁU (`Gelander` bez `Einzelteil`, z płytkami/śrubami) może się
mylić z zupełnie innego powodu (geometria złącza śrubowego, nie skos
rury). Operator zdecydował NIE kasować nic na `[225.130]`.** To nowe,
nieprzetestowane wcześniej ograniczenie zakresu - nie próbować kasować na
rysunkach zespołów bez dodatkowej weryfikacji.

**`[35095]` (`Einzelteil Geländer`, `Handlauf` `RO42.4*3.2`) - kolejne
potwierdzenie filtra kąta na złożonym, PODWÓJNYM skosie.** Ten sam koniec
ma DWIE różne ściany cięcia (kąty ~37,5° i ~64,3° policzone z geometrii
modelu), pasujące niemal dokładnie do WŁASNYCH adnotacji kąta Tekli na
rysunku (`39,14°` i `64,69°`) - niezależne potwierdzenie poprawności
liczenia kąta. W 3D bryła wygląda jak płaski, zaostrzony "nóż" (na
zrzucie z modelu) - to tylko efekt patrzenia niemal wzdłuż osi rury przy
podwójnym skosie, NIE płaski element (`Profile=RO42.4*3.2` potwierdzone
przez nowy log w `--diag-notch-raw`, patrz niżej). Osobna para małych
adnotacji kąta (`9,04°`/`12,12°`) w innym widoku tego samego rysunku NIE
odpowiada żadnej ścianie znalezionej przez `FindQualifyingChordPairs` -
najpewniej to ten sam fizyczny skos, tylko zmierzony/pokazany w innym
rzucie 2D (kąt pozorny w rzucie zależy od kierunku patrzenia, w
przeciwieństwie do kąta liczonego w tym kodzie, który używa
współrzędnych MODELU, nie widoku - patrz `FindQualifyingChordPairs`).
Dry-run i realny insert oba potwierdzone: 4/4 wymiarów (długość+szerokość
obu ścian) wstawione poprawnie, wartości `35,00`/`90,00`/`42,00`/`42,00`
mm zgodne z `--diag-dimension-style` po insercie.

**`--diag-notch-raw` dostał trwały log profilu części** (`Profile={ProfileString}`
obok każdej znalezionej części) - przydatne przy każdej przyszłej
wątpliwości "czy to na pewno rura RO", bez zgadywania z wyglądu w 3D.

**Guard na rysunki zespołów (2026-09-29):** `RemoveAxisDimensions` na
wejściu odrzuca wszystko, co nie jest `SinglePartDrawing` (zmierzone:
`[35095]` = `SinglePartDrawing`, przechodzi; `[225.130]` =
`AssemblyDrawing`, odrzucony w każdym widoku). Guard celowo przepuszcza
TYLKO `SinglePartDrawing` (allowlista, nie blocklista) — `CastUnitDrawing`,
`GADrawing`, `MultiDrawing` nie były mierzone, więc są blokowane; dopisać
typ dopiero po zmierzeniu go na żywym rysunku. Skutek uboczny:
`--diag-find-candidates` (woła prawdziwą `RemoveAxisDimensions`) nie
zgłasza już rysunków zespołów — lista 96 kandydatów z 25.09 jest
nieaktualna.

**Ten sam guard w `NotchPilot.InsertMissing` (2026-09-29):** na zespole
metoda przeszłaby po WSZYSTKICH częściach i wstawiła wymiar wcięcia każdej
skośnej rury — nikt nie potwierdził, że biuro tego chce na zespołach.
Zweryfikowane `--diag-notch-fill-dryrun`: `[225.130]` odrzucony
(`AssemblyDrawing`), `[35095]` bez zmian (te same 4 brakujące wymiary co
28.09).

### Zgłoszony, ZBADANY i PORZUCONY: wymiar wcięcia może wyjść poza krawędź arkusza

Na `[35095]` wstawiona długość (`35 mm`) wylądowała blisko/za krawędzią
arkusza - operator: "trochę do cofnięcia, ale poza tym jest dobrze"
(zaakceptowane jako drobna wada do ręcznej poprawki, nie blokujące).
Zbadano, czy da się to wykryć/uniknąć w kodzie (`--diag-view-bounds`,
nowa trwała diagnostyka - loguje `Sheet.Width/Height`, `View.Width/
Height`, `GetAxisAlignedBoundingBox()`, długość wektorów
`ViewCoordinateSystem`/`DisplayCoordinateSystem`):

- **Żaden z wektorów osi (View ani Display CoordinateSystem) nie niesie
  skali** - oba mają długość dokładnie `1,0` (znormalizowane). Nie ma
  jawnej właściwości `Scale` na `View`.
- **`View.Width`/`Height` DOKŁADNIE pokrywają się z bounding boxem
  narysowanej zawartości** (zmierzone: `72,60×60,60`, dokładnie równe
  różnicy Max-Min bounding boxa) - czyli ramka widoku automatycznie
  dopasowuje się do
  zawartości. Nie ma tam żadnego "zapasu" do sprawdzenia - dodanie
  czegokolwiek zawsze "zmieści się" we własnym, rosnącym pudełku widoku.
  To, co faktycznie ogranicza miejsce (fizyczna krawędź arkusza, margines
  szablonu), nie jest tym samym co ten bounding box.
- Fizyczna krawędź arkusza (`Sheet.Width/Height`, np. `297×210` dla A4)
  jest znana, ale bez skali (mm modelu -> mm papieru) i bez marginesu
  szablonu `.lay` (potwierdzone nieczytelne, patrz `..\AGENTS.md`) nie da
  się wiarygodnie policzyć, czy punkt w jednostkach modelu (lokalnych
  widoku) wypadnie w bezpiecznej odległości od krawędzi papieru.

**Decyzja: NIE implementować tej poprawki - brak wiarygodnej ścieżki bez
zgadywania.** Zostaje jako znane ograniczenie kierunku odsunięcia linii
wymiarowej (`side`, heurystyka z 2026-09-24) - poprawka ręczna w Tekli,
tak jak wcześniej z `21 mm` na `[35021]`. `--diag-view-bounds` zostaje w
kodzie, gdyby ktoś kiedyś znalazł sposób na odzyskanie skali (np. przez
inny obiekt o znanej relacji model↔papier).

## Test na `[35095]` (2026-09-29): kasowanie + wstawianie + rozciąganie promienia

Pierwszy test KASOWANIA na `[35095]` (wcześniej tylko wstawianie).
Operator skasował 7 wymiarów do osi w dużym widoku (`21`,`21`,`11`,`11`,
`19`,`22`,`13`). `407`, `353` i płaski `21` (promień) przetrwały — mimo że
`353` i `21` siedziały w tych samych `StraightDimensionSet` co kasowane
wymiary, kaskada NIE wystąpiła (log: łańcuchy malały 4→3→2, znikały
pojedyncze wymiary). Potem „Wstaw” dodał 4 wymiary wcięcia
(`35`/`42`/`90`/`42`). Operator: rysunek „opisuje” wszystko.

**PUŁAPKA: diagnostyka po Mark cofała niezapisane zmiany.** `--diag-notch-raw
"[35095]"` uruchomione PO kasowaniu przyciskiem przywróciło wszystkie 7
skasowanych wymiarów. `SetActiveDrawing` na rysunku, który jest już
otwarty, przeładowuje go, a model testowy nie zapisuje się (limit
licencji), więc zmiany przepadły. Naprawione: `DiagRunner.OpenUnlessActive`
— jeśli aktywny rysunek ma ten sam Mark, bierze go tak, jak jest, bez
`SetActiveDrawing`. Wszystkie tryby `--diag-*` z Mark przez to przechodzą.

**Nowa funkcja: rozciąganie promienia do średnicy** (prośba operatora,
wskazana strzałkami na zrzucie — czekała od 2026-09-23, patrz PUŁAPKA 6).
`NotchPilot.StretchRadiusToDiameter`, wołane z `InsertMissing` (przycisk
„Wstaw”) dla każdej cięciwy DŁUGOŚCI. W widoku, gdzie ta cięciwa jest
płaska, szuka płaskiego wymiaru, którego jeden koniec leży DOKŁADNIE
(`SamePointToleranceMm = 0,01`) na końcu cięciwy (czubek cięcia), a drugi w
połowie średnicy od niego mierząc prostopadle do osi (czyli na osi rury,
tolerancja `AxisToleranceMm`). Taki wymiar zastępuje nowym, od czubka do
czubka, w stylu/kierunku/odsunięciu starego. Na `[35095]` dokładnie 1
trafienie: `21` (41,45;0)→(89,65;21,2) staje się `42` (0;-21,2)→(89,65;21,2).
`407` (startuje z czubka, ale drugi koniec 42,4 mm dalej) i reszta się nie
kwalifikują. Sprawdzone TYLKO na tym jednym złączu — nie wydawać przed
drugim przypadkiem.

**PUŁAPKA: `StraightDimension.Modify()` ze zmienionym `StartPoint`/`EndPoint`
to fałszywy sukces.** Pierwsza wersja przesuwała punkt i wołała `Modify()`:
`Modify()` i `CommitChanges()` zwróciły `true`, ponowny odczyt widoku W TYM
SAMYM PROCESIE pokazał nowy punkt, a niezależny odczyt
(`--diag-dimension-style`, osobny proces) — stary. Tekla zmiany nie
zastosowała. Obecnie: `Insert()` nowego wymiaru, dopiero potem `Delete()`
starego (przy porażce insertu nic nie ginie). Potwierdzone niezależnym
odczytem. **Wniosek ogólny: odczyt w tym samym procesie nie jest
dowodem — po każdej nowej operacji zapisu sprawdzać osobnym procesem.**

**`HasSameDimension` porównuje teraz też kierunek (`UpDirection`).**
Rozciągnięta średnica (`Up` wzdłuż osi) i długość wcięcia (`Up` w poprzek)
mają na `[35095]` IDENTYCZNE końce — bez kierunku jedna udawałaby drugą i
brakujący wymiar nigdy by się nie wstawił. Zmierzone: wymiary wstawione
przez `InsertMissing` mają `UpDirection` równe przekazanemu `side`, więc
ponowne kliknięcie „Wstaw” nic nie dubluje (dry-run po wszystkim: 0 braków).

**Drugi przypadek rozciągania: `[35021]` (2026-09-29, kąt 19,9°).**
Wybrany przez rozszerzony skaner (niżej). `21` (0;-21,2)→(7,68;0) stał się
`42` (0;-21,2)→(15,35;21,2) — te same końce co istniejące `15` (długość),
inny kierunek. Szerokość wcięcia `42` trafiła do DRUGIEGO widoku (tego z
`171`), nie do widoku ze średnicą — obawa o zdublowane `42` w jednym
widoku się nie potwierdziła (agent przewidział to błędnie przed testem).
Linia szerokości stoi na środku rury (odsunięcie `Distance=92,32` z
heurystyki `side`/wzorca), operator ocenił rysunek jako dobry. Niezależny
odczyt potwierdził, ponowny dry-run: 0 braków. **Rozciąganie potwierdzone
na dwóch rysunkach** — `[35095]` i `[35021]`.

**`--diag-find-candidates` liczy też braki wymiaru wcięcia i promienie do
rozciągnięcia** (woła `InsertMissing` w dry-run dla każdego rysunku z
kandydatem do usunięcia). Skan 2298 rysunków trwa DŁUŻEJ niż 10 minut —
uruchamiać w tle z przekierowaniem do pliku, nie jako jedno polecenie z
limitem czasu. Wynik 2026-09-29: 74 rysunki z kandydatami do usunięcia, 22
z promieniem do rozciągnięcia (po 1: m.in. `[35004]`, `[35016]`,
`[35044]`, `[35270]`; po 2: `[35020]`; po 3: `[3.5013]` — znowu jest w
modelu — `[35010]`, `[35013]`). Na `[35010]` przy 45° wychodzi kilka `42`
w jednym widoku — niesprawdzone na żywo, pierwszy kandydat, jeśli pojawi
się zgłoszenie o duplikatach.

## BŁĄD W WYDANEJ v0.3.2 (znaleziony 2026-09-29, naprawiony w v0.3.3): rozciąganie zjadało całkowitą długość

Przegląd dry-runu `[35010]` pokazał, że jedno z trzech „rozciągnięć” to
wymiar CAŁKOWITEJ DŁUGOŚCI `2677` — od punktu referencyjnego (0;0) na osi
do czubka cięcia (2677,2;-21,2). Spełniał oba warunki v0.3.2 (koniec na
czubku, drugi pół średnicy w poprzek) i zostałby zastąpiony wymiarem `42`
— **utrata długości rury**. Ten sam przypadek: `[35020]` (`256`),
`[35013]`, `[3.5013]`. Na `[35095]`/`[35021]` takiego wymiaru nie ma,
dlatego oba testy v0.3.2 tego nie złapały (lekcja: testować też na
rysunku, gdzie jest wymiar, który MA przetrwać).

Poprawka (`StretchRadiusToDiameter`), dwa dodatkowe warunki, oba zmierzone
na wszystkich znanych promieniach (`[35095]`, `[35021]`, `[35010]`,
`[35020]`): `UpDirection` równoległe do osi rury (promień mierzy W POPRZEK;
`2677`/`256` mają `Up` w poprzek, czyli mierzą wzdłuż), oraz koniec na osi
leży wzdłuż osi w obrębie cięcia. Dry-run po poprawce: `[35013]`/`[3.5013]`
3→2, `[35010]` 3→2, `[35020]` 2→1 — wypadły wyłącznie wymiary całkowitej
długości; `[35095]`/`[35021]` bez zmian. **Na żywo na `[35020]`: `256`
przetrwało, `21` przy końcu 45° → `42`.**

**Pusty widok bez wzorca stylu (też `[35020]`).** Dolny widok miał tylko
dwa wymiary do osi (`21`, `8` — oba z głębią Z, więc słusznie wg reguły
v6); po „Usuń” był pusty i „Wstaw” wstrzymał długość/szerokość („nie
znaleziono wzorca stylu w tym widoku”) — koniec 19,9° bez opisu. Operator
najpierw: „oba powinny zostać (potem człowiek je rozciągnie na całą
długość i szerokość ścięcia)”, potem wybrał wariant: program wstawia
wymiary wcięcia sam, ze stylem z INNEGO widoku rysunku (to ten sam wynik
co ręczne rozciągnięcie). `InsertResolvedIfMissing` szuka wzorca najpierw
w widoku docelowym, potem w pozostałych. Na żywo: dolny widok dostał `15`
(długość 19,9°) i `42` (szerokość 45°); `15` stoi nad rurą i wchodzi w
obszar górnej ramki (kierunek `side` + `Distance` wzorca) — operator: „do
przyjęcia”. Operator dodał praktyczną zasadę: takiego widoku (same
wymiary do osi) i tak się nie klika — „Usuń” działa tylko po kliknięciu.

## Historia: PUŁAPKA 5 (dotyczyła reguły v4, ZASTĄPIONEJ przez v5 wyżej)

Para `21`/`21` na `[3.5013]` to NIE była duplikat. Reguła v4 (kasuj
"duplikat", zostaw większą wartość) brała ją za duplikat i kasowała jeden —
ginęła cała jedna informacja. **Ta reguła już nie istnieje w kodzie** — v5
kasuje WSZYSTKIE wymiary do osi bez wyjątku, więc pytanie "czy to duplikat"
w ogóle już nie występuje.

**DOPRECYZOWANE przez PUŁAPKĘ 6 (patrz "STAN NA 2026-09-23" wyżej):**
pierwotnie sądzono (ZAPIS HISTORYCZNY, błędny co do mechanizmu), że to dwa
**PROSTOPADŁE** wymiary tego samego skosu 45° — jeden mierzący offset
poziomo (wzdłuż rury), drugi pionowo (w poprzek), oba pokazujące `21` tylko
dlatego że kąt to dokładnie 45°. W rzeczywistości to ten sam wzorzec co na
`[35021]`: jeden `21` to zwykły płaski wymiar promienia rury (musi zostać),
drugi to faktyczny artefakt skośnego cięcia (ma głębię w Z, słusznie
kasowany). Wniosek sprzed poprawki ("to nie duplikat, nie kasować obu przez
wartość") pozostaje trafny, tylko powód był inny niż sądzono.

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
| v5 | PUŁAPKA 6: `TouchesAxis` sprawdzał Y i Z niezależnie, na dowolnym końcu — łapał zwykłe płaskie wymiary promienia (Z=0 zawsze), które przypadkiem miały Y=0 na jednym końcu. Skasował realnie `21 mm` (promień rury) na żywym [35021]. | Dodany wymóg realnej głębi Z (`zDiffers`) jako warunek konieczny — patrz "PUŁAPKA 6" w "STAN NA 2026-09-23". |

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
RoAxisDimensionRemover.exe --diag-active           # aktywny rysunek w Tekli, wszystkie widoki, reguła v5
RoAxisDimensionRemover.exe --diag-mark "[3.5013]"  # otwiera rysunek po Mark, potem diagnostyka
RoAxisDimensionRemover.exe --diag-notch            # tylko odczyt: geometria bryły + kandydat na wymiar wcięcia
RoAxisDimensionRemover.exe --diag-dimension-style  # tylko odczyt: styl (Attributes/UpDirection/Distance) istniejących wymiarów
RoAxisDimensionRemover.exe --diag-notch-match "[3.5013]"  # tylko odczyt: dopasowanie wymiar do osi -> najbliższa ściana cięcia
RoAxisDimensionRemover.exe --diag-notch-insert-dryrun "[3.5013]"  # tylko odczyt: NotchPilot w dry-run dla każdego wymiaru do osi
RoAxisDimensionRemover.exe --diag-view-objects "[3.5013]"  # tylko odczyt: typy wszystkich obiektów w widoku (research nad regułą "które złącze pokazać")
RoAxisDimensionRemover.exe --diag-notch-raw "[Mark]"       # tylko odczyt: WSZYSCY kandydaci na ścianę cięcia, obie cięciwy, bez filtra płaskości
RoAxisDimensionRemover.exe --diag-notch-fill-dryrun "[Mark]"  # tylko odczyt: NotchPilot.InsertMissing w dry-run - reguła napędzana geometrią, nie wymiarem do osi
RoAxisDimensionRemover.exe --diag-connection "[Mark]"      # tylko odczyt: typ/strony Connection dla każdego widoku (research "które złącze potrzebuje wymiaru")
RoAxisDimensionRemover.exe --diag-find-candidates          # tylko odczyt, BEZ argumentu: skanuje CAŁY model, loguje rysunki z kandydatem (używa prawdziwej RemoveAxisDimensions, więc respektuje guard RO)
RoAxisDimensionRemover.exe --diag-view-bounds "[Mark]"      # tylko odczyt: rozmiar arkusza/widoku, bounding box zawartości, skala widoku (research "czy wymiar wychodzi poza arkusz" - PORZUCONE, patrz sekcja "Zgłoszony, ZBADANY i PORZUCONY")
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
| `RoAxisDimensionService.cs` | cała logika wykrywania i kasowania, zero UI (reguła v6 — kasuje wszystko w widoku, co spełnia `TouchesAxis`). Guardy na wejściu `RemoveAxisDimensions`: `SinglePartDrawing` (od 2026-09-29), `ViewHasRoProfile` (od 2026-09-25) i `ViewHasBentPart` (od v0.3.4) — przy którymkolwiek niespełnionym nic nie kasuje |
| `MainForm.cs` | UI: główny przycisk kasowania, log do okna i do pliku (`dryRun: false` od 2026-09-23 — brama v5 przeszła). Wybór widoku: `Picker.PickPoint` (klik w Tekli), Esc → `PickViewFromList` (lista w oknie). Fokus na Teklę po operacji tylko gdy `!dryRun`. Plus jeden przycisk `_insertNotchButton` ("Wstaw wymiar wcięcia dla złączy na wybranym rysunku", handler `InsertNotchButton_Click`) — woła `NotchPilot.InsertMissing` (patrz "InsertMissing — insert napędzany geometrią" wyżej), bez blokady marki, uzupełnia brakujące wymiary wcięcia niezależnie od tego, czy wymiar do osi jeszcze istnieje |
| `NotchPilot.cs` | TWORZENIE wymiaru wcięcia — produkcyjne (nazwa „Pilot” historyczna). Przycisk woła `InsertMissing` (guard `SinglePartDrawing`, gięte części pomijane przez `IsStraight`) → `FindQualifyingChordPairs` (ściany ≥ 10°) → `InsertResolvedIfMissing` (widok z płaską cięciwą, styl z tego widoku albo z innego) + `StretchRadiusToDiameter` (promień → średnica: `Insert()` nowego, potem `Delete()` starego). `HasSameDimension` porównuje końce i `UpDirection`. `InsertWidth`/`InsertLength` zostają tylko dla `--diag-notch-insert-dryrun`. Problem „które złącze faktycznie potrzebuje wymiaru” (24.09) bez ochrony — świadoma decyzja operatora |
| `Program.cs` | punkt wejścia; GUI domyślnie, przełączniki `--diag-*` (pełna lista w „Jak testować bez klikania w GUI”) dla trybu konsolowego |
| `DiagRunner.cs` | headless runner dry-run; całą geometrię (`CutFaces`, `FindChord`, `ToViewSpace`, `BeamAxis`) bierze z `NotchPilot`, żeby diagnostyka liczyła dokładnie to samo co przycisk (do 2026-09-29 miała własne kopie) + `RunNotchDiag` (research geometrii wcięcia) + `RunDimensionStyleDiag` (styl istniejących wymiarów, źródło danych dla `NotchPilot`) + `RunNotchMatchDiag` (dopasowanie wymiar↔ściana cięcia po najbliższości w układzie widoku, patrz "Reguła dopasowania ściana↔wymiar") + `RunNotchInsertDryRun` (woła `NotchPilot` w dry-run dla każdego wymiaru do osi, potwierdzone na żywym [3.5013]) + `RunNotchRawDiag` (2026-09-25: zrzuca WSZYSTKICH kandydatów, obie cięciwy, bez filtra płaskości, z flagą płaska(Z≈0) — źródło danych dla poprawki asymetrii widoku, patrz "Poprawiona przyczyna i finalna naprawa") + `RunFindCandidatesDiag` (cały model; od 2026-09-29 liczy też braki wymiaru wcięcia i promienie do rozciągnięcia) + `OpenUnlessActive` (nie przeładowuje otwartego rysunku) — **świadomie trwały element projektu**, `dryRun` na sztywno `true` na zawsze, nie do usunięcia |
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
  z kierunkiem, diagnostyka nie przeładowuje otwartego rysunku; **miała błąd** — patrz „BŁĄD W WYDANEJ v0.3.2”) →
  v0.3.3 (2026-09-29: rozciąganie nie rusza wymiaru całkowitej długości, styl wzorca z innego widoku) →
  v0.3.4 (2026-09-29: gięte rury pomijane przy kasowaniu i wstawianiu, diagnostyka widoku/kąta cięcia, refaktor bez zmiany reguł — geometria tylko w `NotchPilot`, poprawka nakładania się przycisków pod paskiem „nowsza wersja”) →
  v0.3.5 (2026-09-30: próg kąta cięcia 10° → 6,5°, PR #39) →
  v0.3.6 (2026-09-30: „Usuń” nie kasuje całkowitej długości krótkich rur, rozciąganie promienia do osi na drugim końcu, PR #42). Release na GitHubie tworzy operator
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

**Aktualna lista (2026-09-29) jest w sekcji „START SESJI TUTAJ” na górze
pliku.** Punkty niżej to stan z 25.09, zostawione jako historia decyzji.

1. **Reguła "który kandydat odpowiada któremu złączu/wymiarowi" —
   ROZWIĄZANA I POTWIERDZONA NA ŻYWO 2026-09-25** (druga runda tej samej
   sesji, po przerwie na licencję - patrz "Poprawiona przyczyna i finalna
   naprawa" wyżej): asymetria widoku - długość ogranicza się do widoku
   źródłowego wymiaru do osi, szerokość szuka po całym rysunku wśród
   płaskich kandydatów. Prawdziwa przyczyna nie była "mylenie widoków"
   (błędna diagnoza z pierwszej rundy tej sesji), tylko fakt geometryczny:
   w danym widoku tylko JEDNA z dwóch ścian ma płaską cięciwę danego typu
   (długość/szerokość). Zamknięte.
2. **Wymiar wcięcia — pilot potwierdzony WIZUALNIE na `[35021]`, na
   `[3.5013]` i na `[3.5027]` (patrz wyżej).** Problem "ma ścianę cięcia w
   bryle" ≠ "potrzebuje wymiaru wcięcia w rysunku" (odkryty 24.09 na
   drugim końcu `[3.5013]`) **CZĘŚCIOWO rozwiązany 2026-09-25** - filtr
   kąta cięcia (`MinCutAngleDegrees = 10.0`, patrz sekcja "Filtr kąta
   cięcia" wyżej) poprawnie odsiewa ściany o kącie bliskim zeru
   (praktycznie proste zakończenia, zweryfikowane na żywo na `[3.5027]`).
   **Nie tłumaczy jednak oryginalnego `[3.5013]`** - tamten odrzucony
   koniec miał TEN SAM ~45° kąt co zaakceptowany, więc filtr kąta by go
   nie złapał, a `[3.5013]` już nie istnieje w modelu do dalszego badania.
   Jeśli operator zgłosi błędnie wstawiony wymiar na złączu o WYRAŹNYM
   kącie (nie bliskim zeru) - to wciąż ten sam, nierozwiązany rodzaj
   problemu.
3. **Główny przycisk kasowania i wstawiania wymiaru wcięcia są teraz
   OSOBNYMI przyciskami w `MainForm.cs`, nie połączone w jeden przepływ**
   ("skasuj i od razu wstaw wcięcie w to miejsce") - to była pierwotna
   wizja z sekcji "Wymiar wcięcia", wciąż niezrealizowana. Do rozważenia
   przy kolejnej sesji, jeśli operator tego zechce - nie zakładać, że to
   oczywisty kolejny krok bez pytania.
4. **UX wyboru widoku** — zaakceptowane 2026-09-23 jako "działa po
   kliknięciu w geometrię partu; Esc → lista jako zapasowa ścieżka". Nie
   próbować dalej "naprawiać" bez nowego wyraźnego zgłoszenia operatora.
