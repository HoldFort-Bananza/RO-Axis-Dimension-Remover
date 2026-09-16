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
   skasowały dobre wymiary na żywym modelu. **Aktualna reguła (v4) MA
   ZNANY BŁĄD** - patrz "PUŁAPKA 5" niżej. Nie włączaj realnego kasowania.
2. **Sprawdź `dryRun` w `MainForm.cs` (`RunButton_Click`) WPROST W PLIKU,
   nie z tego opisu, i sprawdź go NA BRANCHU, z którego faktycznie
   korzystasz** — `dev` i `release` mogą mieć w tej chwili RÓŻNY stan
   (patrz "Branche" niżej, to nie jest tylko teoretyczne ostrzeżenie).
   Stan na 2026-09-04 (późny wieczór), na `dev`: `dryRun: true` — program
   NIE kasuje, tylko loguje. Tak ma zostać na obu branchach, dopóki
   PUŁAPKA 5 nie będzie naprawiona i brama nie przejdzie od zera.
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

## PUŁAPKA 5 — znany błąd, otwarty, zdiagnozowany, do naprawy

**To jest jedyny priorytet w tym projekcie.**

Para `21`/`21` na `[3.5013]` to NIE duplikat, a dwa **PROSTOPADŁE** wymiary
tego samego skosu 45° - jeden mierzy offset poziomo (wzdłuż rury), drugi
pionowo (w poprzek). Oba pokazują `21` tylko dlatego, że kąt to dokładnie
45°. Reguła v4 patrzy na wartość i bliskość, uznaje je za duplikat, kasuje
jeden - i ginie cała jedna informacja (albo poziom, albo pion):

```
21,0  Start=(5775,0; 0,0;  0,0)   End=(5796,2; 21,2; 0,0)
21,0  Start=(5775,0; 0,0; 21,2)   End=(5796,2; 21,2; 0,0)
```

**Naprawa (nie zaimplementowana):** porównywać KIERUNEK POMIARU, nie tylko
wartość. Uwaga: wyświetlana wartość to RZUT rozpiętości `StartPoint`→
`EndPoint` na kierunek pomiaru, więc sama rozpiętość nie rozróżnia tych
dwóch (rzut na `(1,0,0)` i na `(0,1,0)` daje to samo 21,2). Trop z
refleksji nad `Tekla.Structures.Drawing.dll`: **istnieje `UpDirection`** -
dokończyć sprawdzanie gdzie dokładnie (typ/klasa), dopisać do logu
`[diag]`, zebrać dane z obu rysunków, DOPIERO potem pisać regułę.

**`[35270]` musi dalej działać:** tam para to `24` i `12` (RÓŻNE wartości,
plus są DWA wymiary `24`) - to prawdziwy duplikat, operator potwierdził
wizualnie. Fix nie może tego zepsuć.

Pełna historia i kroki: `CLAUDE.md`, "brama bezpieczeństwa" + "Następne
kroki" pkt 5. Ten sam opis jest też jako
[issue #18](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/issues/18)
na GitHubie — zostaw tam komentarz z postępem, jeśli coś ustalisz, żeby
kolejne narzędzie/sesja nie zaczynały od zera.

## Szybkie fakty

- Stos: C#, .NET Framework 4.8, x64, Tekla Open API 2025.0.0 (NuGet).
- Build: `dotnet build RoAxisDimensionRemover.csproj -c Debug -p:Platform=x64`
  (flaga `-p:Platform=x64` KONIECZNA, inaczej ścieżka wyjścia się nie zgadza
  z instalatorem).
- Zamknij `RoAxisDimensionRemover.exe` (`taskkill /F /IM RoAxisDimensionRemover.exe`)
  przed przebudowaniem, jeśli działa - inaczej build się nie uda.
- Testowanie bez GUI: `RoAxisDimensionRemover.exe --diag-active` (aktywny
  rysunek w Tekli) - zawsze bezpieczne, `dryRun` na sztywno `true`.
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
- Bieżąca wersja (na `dev`): `0.2.4`, `dryRun: true`. Opublikowana jako
  GitHub Release
  [v0.2.4](https://github.com/HoldFort-Bananza/RO-Axis-Dimension-Remover/releases/tag/v0.2.4)
  - żadna nowsza wersja nie została jeszcze wydana, bo PUŁAPKA 5 wciąż nie
  jest naprawiona (patrz wyżej i issue #18).
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
