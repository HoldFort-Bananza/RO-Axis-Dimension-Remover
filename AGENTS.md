# AGENTS.md — instrukcje dla dowolnego asystenta AI w tym repo

Ten plik jest tu specjalnie dla asystentów, które NIE czytają automatycznie
`CLAUDE.md` — w szczególności **ChatGPT Codex** (`AGENTS.md` to jego natywna
konwencja), a także GitHub Copilot, Qwen Coder, Cursor, Aider — na wypadek
gdy praca przechodzi z Claude Code na inne narzędzie w połowie sesji.

Jeśli jesteś **Claude Code**: `CLAUDE.md` już się wczytał automatycznie, ten
plik nie wnosi nic nowego poza powtórzeniem tego samego w skrócie. Jeśli
jesteś **GLM Code** (binarka Claude Code wskazana na backend GLM): to
technicznie to samo narzędzie co Claude Code, więc `CLAUDE.md` też ci się
wczytał automatycznie — ten plik również nie wnosi nic nowego.

**Jeśli czytasz to jako inny asystent: przeczytaj `CLAUDE.md` w całości
zanim zmienisz jakikolwiek kod w tym repo.** To jest pełna baza wiedzy —
historia pięciu nieudanych/podejrzanych wersji reguły wykrywania, dokładny
stan bramy bezpieczeństwa, pułapki API. Ten plik to tylko skrót, żeby nic
krytycznego nie zginęło przy zmianie narzędzia.

## Najważniejsze zanim cokolwiek zrobisz

1. **To jest wtyczka do Tekla Structures 2025, która potrafi NAPRAWDĘ
   kasować dane w modelu.** Dwie wcześniejsze wersje reguły realnie
   skasowały dobre wymiary na żywym modelu. **Reguła detekcji została
   2026-09-23 CAŁKOWICIE ZASTĄPIONA (v5)** - zamiast kasować "duplikaty"
   (v4, PUŁAPKA 5 niżej - historia, nie bieżący stan), program kasuje
   TERAZ każdy wymiar do osi w wybranym widoku, bez wyjątku. To NOWA
   reguła i NIE przeszła jeszcze bramy bezpieczeństwa - nie włączaj
   realnego kasowania. Pełny opis: `CLAUDE.md`, sekcja "STAN NA
   2026-09-23".
2. **Sprawdź `dryRun` w `MainForm.cs` (`RunButton_Click`) WPROST W PLIKU,
   nie z tego opisu, i sprawdź go NA BRANCHU, z którego faktycznie
   korzystasz** — `dev` i `release` mogą mieć w tej chwili RÓŻNY stan
   (patrz "Branche" niżej, to nie jest tylko teoretyczne ostrzeżenie).
   Stan na 2026-09-23, na `dev`: `dryRun: true` — program NIE kasuje,
   tylko loguje. Tak ma zostać, dopóki reguła v5 nie przejdzie bramy od
   zera (nowa reguła, nie kontynuacja PUŁAPKI 5).
3. **NIE WALIDUJ REGUŁY PRZEZ JEJ WŁASNY DRY-RUN.** To najważniejsza
   lekcja z tego projektu i powód, dla którego błąd przeżył trzy "czyste"
   testy: dry-run i realne kasowanie używają tego samego kodu, więc zawsze
   się zgodzą - także gdy oba są błędne. Waliduj pytaniem "czy po tej
   operacji rysunek nadal opisuje wszystko, co musi opisywać?" I pytaj
   operatora BEZ podpowiadania odpowiedzi - "usuwa jeden z pary duplikatów,
   poprawnie?" to pytanie, które samo w sobie przemyca założenie.
4. **Nigdy nie zgaduj progu/reguły detekcji "na wyczucie".** Każda stała w
   `RoAxisDimensionService.cs` ma komentarz skąd się wzięła (zmierzona, nie
   zgadana). Jeśli trzeba zmienić regułę — zdobądź realne współrzędne z
   dry-run (`RoAxisDimensionRemover.exe --diag-active` albo
   `--diag-mark "[Mark]"`, zawsze bezpieczne, nigdy nie kasuje) zanim
   napiszesz kod.
5. **Każda zmiana reguły detekcji wymaga nowego przejścia bramy
   bezpieczeństwa od zera**, nawet jeśli poprzednia reguła była
   potwierdzona: dry-run → operator patrzy na żywy rysunek w Tekli w
   momencie kliknięcia → dopiero wtedy `dryRun: false`. Nie pytaj "czy mogę
   włączyć realne kasowanie" retorycznie - naprawdę czekaj na wyraźne "tak"
   od człowieka, konkretnie na TO pytanie, nie na ogólne "kontynuuj".

## PUŁAPKA 5 — historia (reguła v4, ZASTĄPIONA, nie bieżący stan)

Para `21`/`21` na `[3.5013]` to NIE duplikat, a dwa **PROSTOPADŁE** wymiary
tego samego skosu 45° - jeden mierzy offset poziomo (wzdłuż rury), drugi
pionowo (w poprzek). Reguła v4 (kasuj "duplikat", zostaw większą wartość)
brała je za duplikat i kasowała jeden - ginęła cała jedna informacja.
**Ta reguła już nie istnieje w kodzie** - v5 (2026-09-23) kasuje WSZYSTKIE
wymiary do osi bez wyjątku, więc pytanie "czy to duplikat" w ogóle już nie
występuje. Pełny opis zmiany: `CLAUDE.md`, sekcja "STAN NA 2026-09-23".
Historia zostaje jako kontekst diagnostyczny (i jako
[issue #18](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/issues/18)
na GitHubie), nie jako aktualne TODO.

## Aktualny priorytet: wymiar wcięcia (cut fitting) — NIE ZAIMPLEMENTOWANE

Program ma docelowo po skasowaniu wymiarów do osi dorysować nowy wymiar
(zwykle poziomy) opisujący wcięcie profilu w złączu. Wymaga prawdziwej
geometrii bryły cięcia z modelu (`Tekla.Structures.Model.Part.GetSolid()`),
nie punktów usuwanych wymiarów (te leżą na osi - to ten sam problem, który
ma zniknąć). **Nie zgaduj, która ściana bryły jest ścianą cięcia** - to
dokładnie ten rodzaj błędu, który już raz skasował dobre dane w tym
projekcie. Pierwszy bezpieczny krok zrobiony: `--diag-notch` (tylko odczyt)
mostkuje rysunek→model i loguje realną geometrię bryły. Pełny opis i
konkretne następne kroki: `CLAUDE.md`, sekcje "Wymiar wcięcia" i
"Następne kroki".

## Znany, zaakceptowany kompromis: wybór widoku w MainForm

`Picker.PickPoint` (klik w Tekli) potrafi zawiesić proces w nieskończoność
przy kliku w miejsce bez żadnej geometrii - zmierzone na żywo 2026-09-23,
dwukrotnie, przez `tasklist`. Brak w publicznym API trybu "zaznacz
obszarem", który by to obszedł. Operator zaakceptował kompromis: klik
działa, gdy trafi w narysowaną geometrię (linię, wymiar, kontur partu);
Esc w Tekli przerywa Picker i pokazuje listę widoków jako zapasową ścieżkę.
**To świadoma decyzja operatora z 2026-09-23, nie coś do dalszego
"naprawiania" bez nowego zgłoszenia.** Szczegóły: `CLAUDE.md`, sekcja
"STAN NA 2026-09-23".

## Szybkie fakty

- Stos: C#, .NET Framework 4.8, x64, Tekla Open API 2025.0.0 (NuGet).
- Build: `dotnet build RoAxisDimensionRemover.csproj -c Debug -p:Platform=x64`
  (flaga `-p:Platform=x64` KONIECZNA, inaczej ścieżka wyjścia się nie zgadza
  z instalatorem).
- Zamknij `RoAxisDimensionRemover.exe` (`taskkill /F /IM RoAxisDimensionRemover.exe`)
  przed przebudowaniem, jeśli działa - inaczej build się nie uda.
- Testowanie bez GUI: `RoAxisDimensionRemover.exe --diag-active` (aktywny
  rysunek w Tekli, wszystkie widoki) lub `--diag-notch` (tylko odczyt -
  geometria bryły `Model.Part.GetSolid()`, grunt pod wymiar wcięcia) -
  zawsze bezpieczne, `dryRun` na sztywno `true` (a `--diag-notch` w ogóle
  nic nie usuwa/tworzy).
- Branche: `dev` (domyślny, WIP) i `release` (ma trzymać potwierdzony kod).
  **STAN NA 2026-09-16 (po PR #17/#19-22): znowu identyczne, obie
  `dryRun: true` (bezpieczne)** - to już DRUGI raz w tym samym dniu, gdy
  branche się rozjeżdżały i wracały do zgodności (pierwszy raz: PR #13
  wniosło błędne `dryRun: false`, PR #15+#16 to naprawiły; drugi raz: same
  doc-only zmiany w README, PR #17 promowało, PR #19-21 dokładały kolejne
  poprawki README na `dev`, PR #22 zsynchronizowało `release`→`dev` z
  powrotem). **To znowu może się zmienić - zawsze sprawdzaj
  `git diff origin/dev origin/release` i realny `dryRun` w plikach na
  branchu, z którego korzystasz. Nie ufaj temu opisowi bez sprawdzenia,
  niezależnie od tego, ile razy już się "zgodziły".**
- Merge pull requestów na GitHubie robi człowiek (operator), nie asystent -
  API do merge jest tu świadomie nieużywane, także przez `gh pr merge`.
- **`gh` (GitHub CLI) jest zainstalowany i zalogowany od 2026-09-16**
  (`C:\Program Files\GitHub CLI\gh.exe`, konto `HoldFort-Bananza`, protokół
  HTTPS) - użyj `gh issue create`/`gh pr create` zamiast ręcznego REST API.
  Może nie być jeszcze na `PATH` w nowej sesji Bash (sprawdź `which gh`
  najpierw, wywołaj pełną ścieżką jeśli trzeba). Szczegóły i fallback
  (REST API przez `git credential fill`): `..\CLAUDE.md`, sekcja
  "Narzędzia wokół repozytorium".
- Bieżąca wersja (na `dev`): `0.2.4`, `dryRun: true`, ale kod reguły
  detekcji na `dev` jest od 2026-09-23 NOWSZY niż to, co opisuje ten numer
  wersji (v5, patrz wyżej) - numer nie był jeszcze podbity. Ostatnia
  opublikowana wersja to nadal
  [v0.2.4](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/releases/tag/v0.2.4)
  (opisuje jeszcze regułę v4) - żadna nowsza wersja nie została wydana, bo
  reguła v5 nie przeszła jeszcze bramy bezpieczeństwa.
- Pliki `RoAxisDimensionService.cs`, `MainForm.cs`, `Program.cs`,
  `DiagRunner.cs`, `UpdateCheck.cs`, `TeklaWindowFocus.cs` - patrz
  `CLAUDE.md`, sekcja "Struktura plików", po opis każdego.
- **`README.md` celowo NIE zawiera numerów rysunków (Mark) ani nazw
  siostrzanych projektów** - to świadoma decyzja operatora (2026-09-16),
  bo repo jest publiczne. Ten plik i `CLAUDE.md` mogą i powinny zachować
  konkretne dane (to baza wiedzy do diagnozy), ale jeśli edytujesz
  `README.md`, zachowaj ten sam brak identyfikatorów - opisuj przypadki
  testowe geometrycznie (np. "para prostopadłych wymiarów"), nie po
  numerze rysunku.

## Konwencje repo (patrz też `..\CLAUDE.md`, nadrzędny dla wszystkich
projektów Tekla w tym katalogu)

- Komentarze w kodzie i logi po polsku (czyta je operator). Komunikaty
  commitów po angielsku.
- Dane wyłącznie z Tekla Open API - nigdy zrzuty ekranu / odczyt pikseli.
