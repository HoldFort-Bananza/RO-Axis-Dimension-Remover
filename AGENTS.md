# AGENTS.md — instrukcje dla dowolnego asystenta AI w tym repo

Ten plik jest tu specjalnie dla asystentów, które NIE czytają automatycznie
`CLAUDE.md` (np. GitHub Copilot, Qwen Coder, Cursor, Aider) — na wypadek gdy
praca przechodzi z Claude Code na inne narzędzie w połowie sesji. Jeśli
jesteś Claude Code: `CLAUDE.md` już się wczytał automatycznie, ten plik nie
wnosi nic nowego poza powtórzeniem tego samego w skrócie.

**Jeśli czytasz to jako inny asystent: przeczytaj `CLAUDE.md` w całości
zanim zmienisz jakikolwiek kod w tym repo.** To jest pełna baza wiedzy —
historia pięciu nieudanych/podejrzanych wersji reguły wykrywania, dokładny
stan bramy bezpieczeństwa, pułapki API. Ten plik to tylko skrót, żeby nic
krytycznego nie zginęło przy zmianie narzędzia.

## Najważniejsze zanim cokolwiek zrobisz

1. **To jest wtyczka do Tekla Structures 2025, która potrafi NAPRAWDĘ
   kasować dane w modelu.** Dwie wcześniejsze wersje reguły wykrywania
   realnie skasowały dobre wymiary na żywym modelu, zanim ktoś to
   zauważył. Trzecia (opisana niżej, "PUŁAPKA 5") miała niewyjaśnione
   zgłoszenie tego samego typu błędu - zamknięte decyzją operatora, ale
   PRZYCZYNA NIGDY NIE ZOSTAŁA ZDIAGNOZOWANA.
2. **Sprawdź `dryRun` w `MainForm.cs` (`RunButton_Click`) WPROST W PLIKU,
   nie z tego opisu** — ten opis może być nieaktualny w chwili, gdy go
   czytasz. Stan na 2026-09-04 (wieczór): `dryRun: false` na `dev` i na
   `release` — program KASUJE NAPRAWDĘ. Jeśli coś wygląda podobnie do
   zgłoszenia w PUŁAPCE 5 (usuwa więcej niż jeden zamierzony wymiar) -
   przełącz `dryRun` na `true` OD RAZU i traktuj to jako nawrót
   niezdiagnozowanego problemu, nie jako nowy, osobny błąd.
3. **Nigdy nie zgaduj progu/reguły detekcji "na wyczucie".** Każda stała w
   `RoAxisDimensionService.cs` ma komentarz skąd się wzięła (zmierzona, nie
   zgadana). Jeśli trzeba zmienić regułę — zdobądź realne współrzędne z
   dry-run (`RoAxisDimensionRemover.exe --diag-active` albo
   `--diag-mark "[Mark]"`, zawsze bezpieczne, nigdy nie kasuje) zanim
   napiszesz kod.
4. **Każda zmiana reguły detekcji wymaga nowego przejścia bramy
   bezpieczeństwa od zera**, nawet jeśli poprzednia reguła była
   potwierdzona: dry-run → operator patrzy na żywy rysunek w Tekli w
   momencie kliknięcia → dopiero wtedy `dryRun: false`. Nie pytaj "czy mogę
   włączyć realne kasowanie" retorycznie - naprawdę czekaj na wyraźne "tak"
   od człowieka, konkretnie na TO pytanie, nie na ogólne "kontynuuj".

## Zamknięty, ale niewyjaśniony problem (PUŁAPKA 5)

Operator zgłosił, że realne kasowanie (`dryRun: false`) na rysunku `[35270]`
usunęło więcej niż zamierzony duplikat - "całą szerokość albo całą długość".
Trzy nadzorowane testy powtórzone po tym zgłoszeniu (2x `[35270]`, 1x
`[3.5013]`, operator patrzył w momencie kliknięcia) usunęły TYLKO zamierzony
wymiar każdy raz i NIE odtworzyły problemu. Sprawdzone i odrzucone jako
przyczyna: wspólny `StraightDimensionSet` ("łańcuch" wymiarów w Tekli)
między parą 24mm/12mm - są w dwóch odrębnych, jednoelementowych zestawach,
więc to nie kaskada przez łańcuch, przynajmniej nie dla tej pary.

**Operator zdecydował** uznać oryginalne zgłoszenie za pojedynczy incydent i
przywrócić `dryRun: false` - to ŚWIADOMA DECYZJA O RYZYKU, nie dowód że
problemu nie ma. Przyczyna nigdy nie została zdiagnozowana. `CLAUDE.md`,
sekcja "brama bezpieczeństwa", ma pełną historię.

## Szybkie fakty

- Stos: C#, .NET Framework 4.8, x64, Tekla Open API 2025.0.0 (NuGet).
- Build: `dotnet build RoAxisDimensionRemover.csproj -c Debug -p:Platform=x64`
  (flaga `-p:Platform=x64` KONIECZNA, inaczej ścieżka wyjścia się nie zgadza
  z instalatorem).
- Zamknij `RoAxisDimensionRemover.exe` (`taskkill /F /IM RoAxisDimensionRemover.exe`)
  przed przebudowaniem, jeśli działa - inaczej build się nie uda.
- Testowanie bez GUI: `RoAxisDimensionRemover.exe --diag-active` (aktywny
  rysunek w Tekli) - zawsze bezpieczne, `dryRun` na sztywno `true`.
- Branche: `dev` (domyślny, WIP) i `release` (ma trzymać potwierdzony kod -
  w praktyce oba branche zwykle mają identyczną treść po serii PR-ów w obie
  strony; sprawdzaj `git diff origin/dev origin/release`, nie zgaduj).
- Merge pull requestów na GitHubie robi człowiek (operator), nie asystent -
  API do merge jest tu świadomie nieużywane.
- Pliki `RoAxisDimensionService.cs`, `MainForm.cs`, `Program.cs`,
  `DiagRunner.cs`, `UpdateCheck.cs`, `TeklaWindowFocus.cs` - patrz
  `CLAUDE.md`, sekcja "Struktura plików", po opis każdego.

## Konwencje repo (patrz też `..\CLAUDE.md`, nadrzędny dla wszystkich
projektów Tekla w tym katalogu)

- Komentarze w kodzie i logi po polsku (czyta je operator). Komunikaty
  commitów po angielsku.
- Dane wyłącznie z Tekla Open API - nigdy zrzuty ekranu / odczyt pikseli.
